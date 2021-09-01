using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Easy2DPlayerMovement
{
    public class MovementManager : MonoBehaviour
    {
        /*
           =====================================================
           Created by Lost Relic Games.
           This project can be used for commercial game making purposes.
           This project may not be re-distributed.
           =====================================================
         */

        /*
         * =====================================================
           This is the movement manager that controls the players movement.
           It listens to the InputManager for input.
           =====================================================
         */

        //audio source for jump sound, used for sound demonstration purposes.
        AudioSource jumpAudioSource;

        const string LEFT = "left";
        const string RIGHT = "right";

        [Header("Walk Settings")]
        [Range(2f, 20f)]
        [SerializeField] private int walkSpeed = 7;
        [Range(2f, 20f)]
        [SerializeField] private int runSpeed = 12;

        [Header("Collision Sensitivity")]

        [Tooltip("how close must the player be to the ground to detect a hit")]
        [Range(0.1f, 0.3f)]
        [SerializeField] float groundCheckDistance = 0.1f;

        [Tooltip("how close must the player be to a wall allow for a wall jump")]
        [Range(0.1f, 0.3f)]
        [SerializeField] float wallCheckDistance = 0.2f;
        internal string facingDirection;

        [Header("Jump Settings")]
        //these jump specic values can be tweaked through the inspector

        [Tooltip("when enabled player can jump off walls")]
        [SerializeField] private bool wallJumpEnabled;

        [Tooltip("when enabled player can double jump")]
        [SerializeField] private bool doubleJumpEnabled;

        [Tooltip("Initial force when jump is pressed.")]
        [SerializeField] private float initialJumpVelocity = 15;

        [Tooltip("Offset for jump down phase.")]
        [SerializeField] private float fallMultiplier = 1;

        [Tooltip("Offset for jump up phase.")]
        [SerializeField] private float jumpMultiplier = 2f;

        [Tooltip("Maximum time the jump button can be held down.")]
        [SerializeField] private float maxJumpTime = 0.22f;

        [Tooltip("minimum time the jump button can be held down.")]
        [SerializeField] private float minJumpTime = 0.09f;

        [Header("Terrain checks ")]
        [Tooltip("The name of the ground layer. Used for collision.")]
        [SerializeField] string groundLayerName = null;

        [Tooltip("Ground check to the left of the player's feet.")]
        [SerializeField] Transform groundCheckL = null;

        [Tooltip("Ground check in the mmiddle of the player's feet.")]
        [SerializeField] Transform groundCheckM = null;

        [Tooltip("Ground check to the right of the player's feet.")]
        [SerializeField] Transform groundCheckR = null;

        [Tooltip("Ceiling check above the player's head.")]
        [SerializeField] Transform ceilingCheckL = null;

        [Tooltip("Ceiling check above the player's head.")]
        [SerializeField] Transform ceilingCheckR = null;

        [Tooltip("if enabled raycasts will be visualised in the editor")]
        [SerializeField] private bool showRayCastLines = true;

        //private references used by the controller logic, leave these unassigned
        private InputManager inputManager;
        private Rigidbody2D rb2d;
        private Transform t;
        private bool wasJumpPressed;
        private bool isTryingToJump;
        private float totalJumpTime;
        private int currentSpeed;
        private int groundMask;
        private int jumpCount = 0;
        private bool isGrounded;
        private bool isTouchingWall;
        private bool jumpedDuringSprint;
        private float sprintJumpDirection;


        //=====================================================================
        // The controller is awake, this is called before start
        //=====================================================================
        private void Awake()
        {
            if(GetComponent<AudioSource>() != null)
            {
                jumpAudioSource = GetComponent<AudioSource>();
            }

            //assign the rigidbody2D
            inputManager = GetComponent<InputManager>();
            rb2d = GetComponent<Rigidbody2D>();
            t = transform;
        }

        //=====================================================================
        // The controller is starting
        //=====================================================================
        private void Start()
        {
            //make sure a ground layer has been assigned
            if (groundLayerName == null || groundLayerName == "")
            {
                Debug.LogWarning("Warning: A ground layer has not beed assigned");
            }

            //create a ground mask (for raycasting) based on the assigned ground layer name
            groundMask = 1 << LayerMask.NameToLayer(groundLayerName);

            //assign call backs jumps
            inputManager.onJumpPressed = OnJumpPressed;
            inputManager.onJumpReleased = OnJumpReleased;

            //assigning the starting facing direction
            DetermineFacingDirection();

            //check to see if all ray cast points have been assigned
            if (groundCheckL == null ||
               groundCheckR == null ||
               groundCheckM == null ||
               ceilingCheckL == null ||
               ceilingCheckR == null)
            {
                Debug.LogWarning("one of the ceiling or ground check points has not been assigned");
            }
        }

        //=====================================================================
        // The main update loop, this gets called every frame
        //=====================================================================
        private void Update()
        {
            if (showRayCastLines) ShowRayCastLines();
        }

        //=====================================================================
        // Main physics update loop, all rigidbody manipulations 
        // and movement code gets executed here
        //=====================================================================
        private void FixedUpdate()
        {
            ProcessRayCastChecks();
            UpdateHorizontalMovement();
            UpdateVerticalMovement();
        }

        //=====================================================================
        // get facing direction based on player scale
        //=====================================================================
        void DetermineFacingDirection()
        {
            if (t.localScale.x < 0)
            {
                facingDirection = LEFT;
            }
            else
            {
                facingDirection = RIGHT;
            }
        }

        ///=====================================================================
        // Shows a visual representation of all the terrain 
        // collision checks within the editor view
        //=====================================================================
        private void ShowRayCastLines()
        {
            //set a colour for all raycast visualizations
            Color c = Color.green;
            float gDist = groundCheckDistance;
            float wDist = wallCheckDistance;

            //visualize ground check rays
            Debug.DrawLine(groundCheckL.position, groundCheckL.position + Vector3.down * gDist, c);
            Debug.DrawLine(groundCheckM.position, groundCheckM.position + Vector3.down * gDist, c);
            Debug.DrawLine(groundCheckR.position, groundCheckR.position + Vector3.down * gDist, c);

            //visualise ceiling hit ray
            Debug.DrawLine(ceilingCheckL.position, ceilingCheckL.position + Vector3.up * gDist, c);
            Debug.DrawLine(ceilingCheckR.position, ceilingCheckR.position + Vector3.up * gDist, c);

            //visualize wall check rays
            if (facingDirection == Direction.RIGHT)
            {
                Debug.DrawLine(groundCheckR.position, groundCheckR.position + Vector3.right * wDist, c);
                Debug.DrawLine(groundCheckL.position, groundCheckL.position + Vector3.left * wDist, c);
            }
            else
            {
                Debug.DrawLine(groundCheckR.position, groundCheckR.position + Vector3.left * wDist, c);
                Debug.DrawLine(groundCheckL.position, groundCheckL.position + Vector3.right * wDist, c);
            }
        }

        //=====================================================================
        // All terrain ray cast terrain collision checks
        //=====================================================================
        private void ProcessRayCastChecks()
        {
            float gDist = groundCheckDistance;
            float wDist = wallCheckDistance;

            //shoots raycasts down from feet to look for ground
            RaycastHit2D groundHitL = Physics2D.Raycast(groundCheckL.position, Vector3.down, gDist, groundMask);
            RaycastHit2D groundHitM = Physics2D.Raycast(groundCheckM.position, Vector3.down, gDist, groundMask);
            RaycastHit2D groundHitR = Physics2D.Raycast(groundCheckR.position, Vector3.down, gDist, groundMask);

            //shoots a ray upwards to look for a ceiling
            RaycastHit2D ceilingHitL = Physics2D.Raycast(ceilingCheckL.position, Vector3.up, gDist, groundMask);
            RaycastHit2D ceilingHitR = Physics2D.Raycast(ceilingCheckR.position, Vector3.up, gDist, groundMask);

            //shoots a ray Right looking for a wall (used for wall jumping)
            RaycastHit2D wallHitR;
            RaycastHit2D wallHitL;

            if (facingDirection == Direction.RIGHT)
            {
                wallHitR = Physics2D.Raycast(groundCheckR.position, Vector3.right, wDist, groundMask);
                wallHitL = Physics2D.Raycast(groundCheckR.position, Vector3.left, wDist, groundMask);
            }
            else
            {
                wallHitR = Physics2D.Raycast(groundCheckR.position, Vector3.left, wDist, groundMask);
                wallHitL = Physics2D.Raycast(groundCheckR.position, Vector3.right, wDist, groundMask);
            }

            //check if we are hitting a roof during a jump
            if (ceilingHitL.collider != null || ceilingHitR.collider != null)
            {
                //player has hit roof while jumping, reset the jump
                wasJumpPressed = false;
                isTryingToJump = false;
            }

            //check if the a ray has found ground below the feet
            if (groundHitL.collider != null || 
                groundHitM.collider != null || 
                groundHitR.collider != null)
            {
                //has found ground
                if (!isGrounded)
                {
                    //landing from jump
                    jumpedDuringSprint = false;
                }
                isGrounded = true;
            }
            else
            {
                //has not found ground
                isGrounded = false;
            }

            //wallChecking: check if the player is touching a wall (used for wall jump)
            if (wallJumpEnabled && (wallHitR.collider != null || wallHitL.collider != null))
            {
                isTouchingWall = true;
            }
            else
            {
                isTouchingWall = false;
            }
        }

        //=====================================================================
        //Jump has been pressed (is called by the inputController class)
        //=====================================================================
        internal void OnJumpPressed()
        {
            //only allow the player to jump if grounded or touching wall
            if ((isGrounded || (doubleJumpEnabled && jumpCount < 2)) || isTouchingWall)
            {
                PlayJumpSound();

                if (inputManager.isSprintPressed)
                {
                    jumpedDuringSprint = true; //flag the sprint jump for later use
                    sprintJumpDirection = inputManager.xAxis; //store the direction for later reference
                }

                wasJumpPressed = true;
                isTryingToJump = true;

                if (isGrounded)
                {
                    jumpCount = 0; //reset double jump count
                }

                jumpCount++;//increment jump count
            }
        }

        //=====================================================================
        //Example jump sound, this is used for demonstartion purposes
        // you will want to replace it with your own custom sound manager
        private void PlayJumpSound()
        {
            if(jumpAudioSource != null)
            {
                jumpAudioSource.Play();
            }
        }

        //=====================================================================
        //Jump has been released ( this function is called by the inputController class)
        //=====================================================================
        internal void OnJumpReleased()
        {
            wasJumpPressed = false;
        }

        //=====================================================================
        //All the jump based vertical calculations 
        //=====================================================================
        private void UpdateVerticalMovement()
        {
            //If our player pressed the jump key... this is to maintain minimum jump height
            if (wasJumpPressed)
            {
                wasJumpPressed = false;
                totalJumpTime = 0; //reset it to zero before the jump
            }

            //The following code is the heart of the variable jump, 
            if (isTryingToJump)
            {
                totalJumpTime += Time.deltaTime;

                if (inputManager.isJumpPressed)
                {
                    if (totalJumpTime <= maxJumpTime)
                    {
                        rb2d.velocity = new Vector2(rb2d.velocity.x, initialJumpVelocity);
                    }
                    else
                    {
                        isTryingToJump = false;
                    }
                }
                else
                {
                    if (totalJumpTime < minJumpTime)
                    {
                        rb2d.velocity = new Vector2(rb2d.velocity.x, initialJumpVelocity);
                    }
                    else
                    {
                        isTryingToJump = false;
                    }
                }
            }

            //create a temp gravity value for convenience
            Vector2 vGravityY = Vector2.up * Physics2D.gravity.y;

            //check if the players jump is in the rising or falling phase and calulate physics
            if (rb2d.velocity.y < 0)
            {
                rb2d.velocity += vGravityY * fallMultiplier * Time.deltaTime;
            }
            else if (rb2d.velocity.y > 0 && isTryingToJump)
            {
                //determine how far though the jump we are as a decimal percentage 
                float t = totalJumpTime / maxJumpTime * 1;
                float tempJumpM = jumpMultiplier;

                //smooth out the peak of the jump, just like in super mario
                if (t > 0.5f)
                {
                    tempJumpM = jumpMultiplier * (1 - t);
                }

                //assign the final calculation to the rigidbody2D
                rb2d.velocity += vGravityY * tempJumpM * Time.deltaTime;
            }
        }

        //=====================================================================
        //All the horizontal ground and air calculations 
        //=====================================================================
        private void UpdateHorizontalMovement()
        {
            Vector2 vel = rb2d.velocity;

            //check if currently walking or in the air
            if (isGrounded)
            {
                if (inputManager.isSprintPressed)
                {
                    currentSpeed = runSpeed;
                }
                else
                {
                    currentSpeed = walkSpeed;
                }
            }
            else
            {
                //handle sprint jumps and movement
                if (jumpedDuringSprint)
                {
                    if ((int)inputManager.xAxis == (int)sprintJumpDirection)
                    {
                        currentSpeed = runSpeed;
                    }
                    else
                    {
                        jumpedDuringSprint = false;
                        currentSpeed = walkSpeed;
                    }
                }
            }

            //check of left, right or nothing is pressed and set the velocity and facing position
            if (inputManager.isLeftPressed)
            {
                vel.x = -currentSpeed;
                t.localScale = new Vector2(-1, 1);
                facingDirection = LEFT;
            }
            else if (inputManager.isRightPressed)
            {
                vel.x = currentSpeed;
                t.localScale = new Vector2(1, 1);
                facingDirection = RIGHT;
            }
            else
            {
                vel.x = 0;
            }

            rb2d.velocity = vel;
        }
    }
}