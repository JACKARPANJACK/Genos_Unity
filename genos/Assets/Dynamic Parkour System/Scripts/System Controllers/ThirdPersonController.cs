/*
MIT License

Copyright (c) 2023 Èric Canela
Contact: knela96@gmail.com or @knela96 twitter

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (Dynamic Parkour System), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Climbing
{
    public enum ParkourState { None, ScriptedTraversal, WallRunning }

    [RequireComponent(typeof(InputCharacterController))]
    [RequireComponent(typeof(MovementCharacterController))]
    [RequireComponent(typeof(AnimationCharacterController))]
    [RequireComponent(typeof(DetectionCharacterController))]
    [RequireComponent(typeof(CameraController))]
    [RequireComponent(typeof(VaultingController))]
    [RequireComponent(typeof(WallRunController))]

    public class ThirdPersonController : MonoBehaviour
    {
        [HideInInspector] public InputCharacterController characterInput;
        [HideInInspector] public MovementCharacterController characterMovement;
        [HideInInspector] public AnimationCharacterController characterAnimation;
        [HideInInspector] public DetectionCharacterController characterDetection;
        [HideInInspector] public VaultingController vaultingController;
        [HideInInspector] public WallRunController wallRunController;
        [HideInInspector] public bool isGrounded = false;
        [HideInInspector] public bool allowMovement = true;
        [HideInInspector] public bool onAir = false;
        [HideInInspector] public bool isJumping = false;
        [HideInInspector] public bool inSlope = false;
        [HideInInspector] public bool isVaulting = false;
        [HideInInspector] public bool dummy = false;
        [HideInInspector] public ParkourState activeParkourState = ParkourState.None;

        [Header("Air Dash Settings")]
        public float airDashForce = 15f;
        public float airDashTime = 0.2f;
        public bool canAirDash = true;
        private bool hasAirDashed = false;
        private bool isAirDashing = false;
        private Vector3 airDashDirection;
        private Tween airDashTween;

        [Header("Ground Dash Settings")]
        public float groundDashForce = 20f;
        public float groundDashTime = 0.25f;
        public bool canGroundDash = true;
        private bool isGroundDashing = false;
        private Vector3 groundDashDirection;
        private Tween groundDashTween;

        [Header("Charge Jump Settings")]
        public float baseJumpVelocity = 6f;
        public float maxJumpChargeTime = 1f;
        public float maxExtraJumpVelocity = 6f;
        private float jumpChargeTimer = 0f;
        private bool isChargingJump = false;

        [Header("Cameras")]
        public CameraController cameraController;
        public Transform mainCamera;
        public Transform freeCamera;

        [Header("Step Settings")]
        [Range(0, 10.0f)] public float stepHeight = 0.8f;
        public float stepVelocity = 0.2f;

        [Header("Colliders")]
        public CapsuleCollider normalCapsuleCollider;
        public CapsuleCollider slidingCapsuleCollider;

        [Header("Rotation Settings")]
        [SerializeField] private float movementTurnSmoothTime = 0.1f;
        [SerializeField] private float idleTurnToCameraSmoothTime = 2.5f;
        [SerializeField] private float idleTurnToCameraDelay = 0.35f;
        [SerializeField] private float idleTurnToCameraThreshold = 15f;

        private float movementTurnSmoothVelocity;
        private float idleTurnSmoothVelocity;
        private float idleTurnTimer;

        private void Awake()
        {
            characterInput = GetComponent<InputCharacterController>();
            characterMovement = GetComponent<MovementCharacterController>();
            characterAnimation = GetComponent<AnimationCharacterController>();
            characterDetection = GetComponent<DetectionCharacterController>();
            vaultingController = GetComponent<VaultingController>();
            wallRunController = GetComponent<WallRunController>();

            if (cameraController == null)
                Debug.LogError("Attach the Camera Controller located in the Free Look Camera");
        }

        private void Start()
        {
            characterMovement.OnLanded += characterAnimation.Land;
            characterMovement.OnFall += characterAnimation.Fall;
        }

        void Update()
        {
            //Detect if Player is on Ground
            isGrounded = OnGround();

            //Get Input if controller and movement are not disabled
            if (!dummy && allowMovement)
            {
                if (!isGroundDashing) 
                {
                    AddMovementInput(characterInput.movement);
                }

                //Detects if Joystick is being pushed hard
                if (characterInput.run && characterInput.movement.magnitude > 0.5f)
                {
                    ToggleRun();
                }
                else if (!characterInput.run)
                {
                    ToggleWalk();
                }

                if (cameraController != null)
                {
                    cameraController.isIdle = characterInput.movement.sqrMagnitude < 0.01f;
                    cameraController.swayAmount = 0.5f; // More pronounced random movement like DMC/MGR
                }
            }
            else
            {
                ResetJumpCharge();
            }
        }

        private void LateUpdate()
        {
            if (!dummy && allowMovement)
            {
                if (!isGroundDashing)
                    UpdateGroundJumpCharge();

                HandleAirDash();
                HandleGroundDash();
            }
        }

        private float lastJumpTime = -1f;

        private void HandleAirDash()
        {
            if (isGrounded)
            {
                hasAirDashed = false;
                if (isAirDashing)
                {
                    isAirDashing = false;
                    airDashTween?.Kill();
                }
                return;
            }

            if (isAirDashing)
                return; // Managed by DoTween

            if (canAirDash && !hasAirDashed && !isGrounded && !isVaulting && !IsParkourBusy)
            {
                // Has to have been in the air slightly so it doesn't trigger instantly when jumping off ground
                if (Time.time > lastJumpTime + 0.25f && characterInput.ConsumeJumpPressedBuffered())
                {
                    PerformAirDash();
                }
            }
        }

        private void PerformAirDash()
        {
            hasAirDashed = true;
            isAirDashing = true;

            airDashTween?.Kill();

            // Consume out the input so it doesn't try firing it elsewhere
            characterInput.ConsumeJumpPressedBuffered();

            // Apply dash in the direction relative to camera
            Vector3 intentDir = new Vector3(characterInput.movement.x, 0f, characterInput.movement.y).normalized;

            if (intentDir.magnitude > 0)
            {
                freeCamera.eulerAngles = new Vector3(0, mainCamera.eulerAngles.y, 0);
                airDashDirection = (freeCamera.transform.forward * characterInput.movement.y + freeCamera.transform.right * characterInput.movement.x).normalized;

                // Immediately snap rotation towards air dash
                transform.rotation = Quaternion.LookRotation(airDashDirection, Vector3.up);
            }
            else
            {
                airDashDirection = transform.forward; // Default to forward if no input
            }

            // You can optionally trigger an air dash animation here
            characterAnimation.animator.Play("AirDash"); // Change to whatever your air dash animation state is!

            // DoTween AirDash Logic
            characterMovement.stopMotion = true;
            airDashTween = DOVirtual.Float(airDashForce, 0f, airDashTime, (force) => 
            {
                if (characterMovement != null && characterMovement.rb != null && !isGrounded)
                {
                    characterMovement.rb.linearVelocity = (airDashDirection * force) + new Vector3(0, characterMovement.rb.linearVelocity.y > 0 ? 0 : characterMovement.rb.linearVelocity.y, 0);
                }
            })
            .SetEase(Ease.OutCirc)
            .SetUpdate(UpdateType.Fixed)
            .OnComplete(() => {
                isAirDashing = false;
                characterMovement.stopMotion = false;
            })
            .OnKill(() => {
                isAirDashing = false;
                if (characterMovement != null) characterMovement.stopMotion = false;
            });
        }

        private void HandleGroundDash()
        {
            if (!isGrounded || isVaulting || IsParkourBusy)
            {
                if (isGroundDashing)
                {
                    isGroundDashing = false;
                    groundDashTween?.Kill();
                }
                return;
            }

            if (isGroundDashing)
                return; // Let DoTween manage the velocity updates inside FixedUpdate via SetUpdate(Fixed)

            if (canGroundDash && characterInput.ConsumeDoubleTapDashBuffered())
            {
                PerformGroundDash();
            }
        }

        private void PerformGroundDash()
        {
            isGroundDashing = true;
            groundDashTween?.Kill();

            Vector3 intentDir = new Vector3(characterInput.movement.x, 0f, characterInput.movement.y).normalized;
            if (intentDir.magnitude > 0)
            {
                freeCamera.eulerAngles = new Vector3(0, mainCamera.eulerAngles.y, 0);
                groundDashDirection = (freeCamera.transform.forward * characterInput.movement.y + freeCamera.transform.right * characterInput.movement.x).normalized;
            }
            else
            {
                groundDashDirection = transform.forward;
            }

            // You can optionally trigger a ground dodge roll animation here
            characterAnimation.animator.Play("Dash");

            // Look in dash dir
            transform.rotation = Quaternion.LookRotation(groundDashDirection, Vector3.up);

            characterMovement.stopMotion = true;
            groundDashTween = DOVirtual.Float(groundDashForce, 0f, groundDashTime, (force) =>
            {
                if (characterMovement != null && characterMovement.rb != null && isGrounded)
                {
                    characterMovement.rb.linearVelocity = new Vector3(groundDashDirection.x * force, characterMovement.rb.linearVelocity.y, groundDashDirection.z * force);
                }
            })
            .SetEase(Ease.OutCubic)
            .SetUpdate(UpdateType.Fixed)
            .OnComplete(() =>
            {
                isGroundDashing = false;
                characterMovement.stopMotion = false;
            })
            .OnKill(() =>
            {
                isGroundDashing = false;
                if (characterMovement != null) characterMovement.stopMotion = false;
            });
        }

        private void UpdateGroundJumpCharge()
        {
            bool canChargeJump = isGrounded &&
                                 !isJumping &&
                                 !isVaulting &&
                                 !characterMovement.stopMotion &&
                                 activeParkourState == ParkourState.None;

            if (!canChargeJump)
            {
                ResetJumpCharge();
                return;
            }

            if (!isChargingJump)
            {
                if (!characterInput.ConsumeJumpPressedBuffered())
                {
                    ResetJumpCharge();
                    return;
                }

                isChargingJump = true;
                jumpChargeTimer = 0f;
            }

            characterAnimation.animator.SetBool("isChargingJump", true);

            if (characterInput.jump)
            {
                jumpChargeTimer += Time.deltaTime;
                jumpChargeTimer = Mathf.Clamp(jumpChargeTimer, 0f, maxJumpChargeTime);
                characterAnimation.animator.SetFloat("jumpChargeRatio", jumpChargeTimer / maxJumpChargeTime);
                return;
            }

            ManualJump(jumpChargeTimer / maxJumpChargeTime);
            ResetJumpCharge();
        }

        private void ResetJumpCharge()
        {
            isChargingJump = false;
            jumpChargeTimer = 0f;
            characterAnimation.animator.SetBool("isChargingJump", false);
            characterAnimation.animator.SetFloat("jumpChargeRatio", 0f);
        }

        private void ManualJump(float chargeRatio)
        {
            isJumping = true;
            onAir = true;
            isGrounded = false;
            lastJumpTime = Time.time;

            // Set animation trigger
            characterAnimation.animator.Play("Jump"); // You can change this to your actual jump animation state name

            // Apply upward force using DOTween to smoothly transition vertical velocity extremely quickly (more natural "muscle push" feel)
            float jumpVelocity = baseJumpVelocity + (maxExtraJumpVelocity * chargeRatio);

            DOVirtual.Float(0f, jumpVelocity, 0.12f, (v) => 
            {
                if (characterMovement != null && characterMovement.rb != null)
                {
                    Vector3 currentVelocity = characterMovement.rb.linearVelocity;
                    characterMovement.rb.linearVelocity = new Vector3(currentVelocity.x, v, currentVelocity.z);
                }
            })
            .SetEase(Ease.OutExpo)
            .SetUpdate(UpdateType.Fixed);

            characterMovement.Fall();
        }

        private bool OnGround()
        {
            // Add a slight cooldown before we can be considered grounded again
            // This prevents the system from immediately grounding the player while the jump is launching
            if (Time.time < lastJumpTime + 0.2f) return false;
            
            return characterDetection.IsGrounded(stepHeight);
        }

        public void AddMovementInput(Vector2 direction)
        {
            Vector3 translation = Vector3.zero;

            translation = GroundMovement(direction);

            characterMovement.SetVelocity(Vector3.ClampMagnitude(translation, 1.0f));
        }

        Vector3 GroundMovement(Vector2 input)
        {
            Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;

            //Gets direction of movement relative to the camera rotation
            freeCamera.eulerAngles = new Vector3(0, mainCamera.eulerAngles.y, 0);
            Vector3 translation = freeCamera.transform.forward * input.y + freeCamera.transform.right * input.x;
            translation.y = 0;

            //Detects if player is moving to any direction
            if (translation.magnitude > 0)
            {
                idleTurnTimer = 0f;
                idleTurnSmoothVelocity = 0f;
                RotatePlayer(direction);
                characterAnimation.animator.SetBool("Released", false);

                // Reset turning angle when player starts moving
                characterAnimation.animator.SetFloat("TurnAngle", 0f);
            }
            else
            {
                idleTurnTimer += Time.deltaTime;

                // Smoothly rotate the player to face the camera direction when idle
                // This mimics modern games where the character aligns with the camera look direction
                float angleDiff = Mathf.DeltaAngle(transform.eulerAngles.y, mainCamera.eulerAngles.y);

                // Pass the angle difference to the Animator so you can use it in blend trees or transitions
                // for 45, 90, or 180 degree turn animations.
                characterAnimation.animator.SetFloat("TurnAngle", angleDiff);

                if (Mathf.Abs(angleDiff) > idleTurnToCameraThreshold && idleTurnTimer >= idleTurnToCameraDelay)
                {
                    float angle = Mathf.SmoothDampAngle(
                        transform.eulerAngles.y,
                        mainCamera.eulerAngles.y,
                        ref idleTurnSmoothVelocity,
                        idleTurnToCameraSmoothTime);
                    transform.rotation = Quaternion.Euler(0f, angle, 0f);
                }

                ToggleWalk();
                characterAnimation.animator.SetBool("Released", true);
            }

            return translation;
        }

        public void RotatePlayer(Vector3 direction)
        {
            //Get direction with camera rotation
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + mainCamera.eulerAngles.y;

            //Rotate Mesh to Movement
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref movementTurnSmoothVelocity, movementTurnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
        public Quaternion RotateToCameraDirection(Vector3 direction)
        {
            //Get direction with camera rotation
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + mainCamera.eulerAngles.y;

            //Rotate Mesh to Movement
            return Quaternion.Euler(0f, targetAngle, 0f);
        }

        public void ResetMovement()
        {
            characterMovement.ResetSpeed();
        }

        public void ToggleRun()
        {
            if (characterMovement.GetState() != MovementState.Running)
            {
                characterMovement.SetCurrentState(MovementState.Running);
                characterMovement.curSpeed = characterMovement.RunSpeed;
                characterAnimation.animator.SetBool("Run", true);
            }
        }
        public void ToggleWalk()
        {
            if (characterMovement.GetState() != MovementState.Walking)
            {
                characterMovement.SetCurrentState(MovementState.Walking);
                characterMovement.curSpeed = characterMovement.walkSpeed;
                characterAnimation.animator.SetBool("Run", false);
            }
        }


        public float GetCurrentVelocity()
        {
            return characterMovement.GetVelocity().magnitude;
        }

        public bool WantsParkourTraversal(float minMagnitude = 0.01f)
        {
            return characterInput.run && characterInput.movement.sqrMagnitude >= minMagnitude;
        }

        public bool WantsAutoParkour(float minForwardInput = 0.25f)
        {
            return WantsParkourTraversal() && characterInput.movement.y >= minForwardInput;
        }

        public bool IsParkourBusy
        {
            get { return activeParkourState != ParkourState.None; }
        }

        public bool HasParkourState(ParkourState state)
        {
            return activeParkourState == state;
        }

        public bool TrySetParkourState(ParkourState state)
        {
            if (activeParkourState != ParkourState.None && activeParkourState != state)
                return false;

            activeParkourState = state;
            return true;
        }

        public void ClearParkourState(ParkourState state)
        {
            if (activeParkourState == state)
                activeParkourState = ParkourState.None;
        }

        public void ClearParkourState()
        {
            activeParkourState = ParkourState.None;
        }

        public void DisableController()
        {
            activeParkourState = ParkourState.ScriptedTraversal;
            characterMovement.SetKinematic(true);
            characterMovement.enableFeetIK = false;
            dummy = true;
            allowMovement = false;
        }
        public void EnableController()
        {
            activeParkourState = ParkourState.None;
            characterMovement.SetKinematic(false);
            characterMovement.EnableFeetIK();
            characterMovement.ApplyGravity();
            characterMovement.stopMotion = false;
            dummy = false; 
            allowMovement = true;
        }
    }
}
