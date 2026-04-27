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
    public enum ParkourState { None, ScriptedTraversal, WallRunning, Dashing }

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
        public bool isDashing => isAirDashing || isGroundDashing;
        [HideInInspector] public ParkourState activeParkourState = ParkourState.None;

        [Header("Air Dash Settings")]
        public DashSetting airDashSetting = new DashSetting { dashForce = 15f, dashTime = 0.2f, recoverTime = 0.4f };
        public bool canAirDash = true;
        private bool hasAirDashed = false;
        private bool isAirDashing = false;
        private float airDashRecoverTimer = 0f;
        private Vector3 airDashDirection;
        private Tween airDashTween;

        [Header("Ground Dash Settings")]
        public DashSetting groundDashSetting = new DashSetting { dashForce = 20f, dashTime = 0.25f, recoverTime = 0.3f };
        public bool canGroundDash = true;
        private bool isGroundDashing = false;
        private bool isGroundDashRecovering = false;
        private float groundDashRecoverTimer = 0f;
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
            characterMovement.OnLanded += () => cameraController?.ShakeMedium();
            characterMovement.OnFall += characterAnimation.Fall;
        }

        void Update()
        {
            //Detect if Player is on Ground
            isGrounded = OnGround();

            //Get Input if controller and movement are not disabled
            if (!dummy && allowMovement)
            {
                if (!isGroundDashing && !isAirDashing) 
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
                airDashRecoverTimer = 0f;
                if (isAirDashing)
                {
                    isAirDashing = false;
                    airDashTween?.Kill();
                }
                return;
            }

            // Explicitly prevent air dash during wallrun
            if (wallRunController != null && wallRunController.isWallRunning)
                return;

            // Tick recovery after an air dash
            if (airDashRecoverTimer > 0f)
{
                airDashRecoverTimer -= Time.deltaTime;
                return;
            }

            if (isAirDashing)
                return; // Managed by DoTween

            if (canAirDash && !hasAirDashed && !isGrounded && !isVaulting && !IsParkourBusy)
            {
                if (Time.time > lastJumpTime + 0.25f && characterInput.ConsumeJumpPressedBuffered())
                {
                    PerformAirDash();
                }
            }
        }

        private void PerformAirDash()
        {
            if (!TrySetParkourState(ParkourState.Dashing)) return;

            hasAirDashed = true;
            isAirDashing = true;

            airDashTween?.Kill();

            // Consume the input so it doesn't fire elsewhere
            characterInput.ConsumeJumpPressedBuffered();

            // Compute dash direction based on camera relative input
            Vector2 input = characterInput.movement;
            Vector3 inputDir = new Vector3(input.x, 0, input.y);

            int dashAnimDir = 0; // 0 = Forward, 1 = Backward, 2 = Left, 3 = Right

            if (inputDir.sqrMagnitude > 0.01f && mainCamera != null)
            {
                inputDir.Normalize();
                Vector3 cameraForwardFlat = mainCamera.forward;
                cameraForwardFlat.y = 0;
                cameraForwardFlat.Normalize();

                Vector3 cameraRightFlat = mainCamera.right;
                cameraRightFlat.y = 0;
                cameraRightFlat.Normalize();

                airDashDirection = cameraForwardFlat * inputDir.z + cameraRightFlat * inputDir.x;
                airDashDirection.Normalize();
            }
            else
            {
                // Back dash when no input is pressed
                airDashDirection = -transform.forward;
            }

            // Deduce the correct animation simply based on the dash direction relative to where the character is already facing.
            // This cleanly handles both free movement and lock-on behavior without camera glitches or forced rotation snapping.
            Vector3 localDashDir = transform.InverseTransformDirection(airDashDirection);
            if (Mathf.Abs(localDashDir.x) > Mathf.Abs(localDashDir.z))
            {
                dashAnimDir = localDashDir.x > 0 ? 3 : 2; // Right or Left
            }
            else
            {
                dashAnimDir = localDashDir.z > 0 ? 0 : 1; // Forward or Backward
            }

            // Turn off IK during dash
            characterMovement.DisableFeetIK();

            characterAnimation.Dash(dashAnimDir);
            characterAnimation.SetDashing(true);

            characterMovement.stopMotion = true;
            cameraController?.SetFOVState(CameraFOVState.AirDash);
            cameraController?.ShakeMedium();

            // Sample the curve over normalised time (0→1) to build a designer-tunable force profile
            airDashTween = DOVirtual.Float(0f, 1f, airDashSetting.dashTime, (t) =>
            {
                if (characterMovement == null || characterMovement.rb == null || isGrounded)
                    return;

                float force = airDashSetting.dashForce * airDashSetting.dashCurve.Evaluate(t);
                float keepY = characterMovement.rb.linearVelocity.y > 0
                    ? 0f
                    : characterMovement.rb.linearVelocity.y;
                characterMovement.rb.linearVelocity = airDashDirection * force + new Vector3(0, keepY, 0);
            })
            .SetEase(Ease.Linear) // Curve drives the shape; Linear keeps sampling honest
            .SetUpdate(UpdateType.Fixed)
            .OnComplete(() =>
            {
                isAirDashing = false;
                characterAnimation.SetDashing(false);
                ClearParkourState(ParkourState.Dashing);
                characterMovement.stopMotion = false;
                characterMovement.EnableFeetIK();
                airDashRecoverTimer = airDashSetting.recoverTime;
                cameraController?.SetFOVState(CameraFOVState.Walk);
            })
            .OnKill(() =>
            {
                isAirDashing = false;
                characterAnimation?.SetDashing(false);
                ClearParkourState(ParkourState.Dashing);
                if (characterMovement != null) 
                {
                    characterMovement.stopMotion = false;
                    characterMovement.EnableFeetIK();
                }
                cameraController?.SetFOVState(CameraFOVState.Walk);
            });
}

        private void HandleGroundDash()
        {
            // Tick recovery between ground dashes
            if (isGroundDashRecovering)
            {
                groundDashRecoverTimer -= Time.deltaTime;
                if (groundDashRecoverTimer <= 0f)
                    isGroundDashRecovering = false;
            }

            if (!isGrounded || isVaulting || IsParkourBusy || (wallRunController != null && wallRunController.isWallRunning))
            {
                if (isGroundDashing)
                {
                    isGroundDashing = false;
                    groundDashTween?.Kill();
                }
                return;
            }

            if (isGroundDashing)
                return; // Let DoTween manage velocity updates inside FixedUpdate via SetUpdate(Fixed)

            if (canGroundDash && !isGroundDashRecovering && characterInput.ConsumeDoubleTapDashBuffered())
            {
                PerformGroundDash();
            }
        }

        private void PerformGroundDash()
        {
            if (!TrySetParkourState(ParkourState.Dashing)) return;

            isGroundDashing = true;
            groundDashTween?.Kill();

            // Compute dash direction based on camera relative input
            Vector2 input = characterInput.movement;
            Vector3 inputDir = new Vector3(input.x, 0, input.y);

            int dashAnimDir = 0; // 0 = Forward, 1 = Backward, 2 = Left, 3 = Right

            if (inputDir.sqrMagnitude > 0.01f && mainCamera != null)
            {
                inputDir.Normalize();
                Vector3 cameraForwardFlat = mainCamera.forward;
                cameraForwardFlat.y = 0;
                cameraForwardFlat.Normalize();

                Vector3 cameraRightFlat = mainCamera.right;
                cameraRightFlat.y = 0;
                cameraRightFlat.Normalize();

                groundDashDirection = cameraForwardFlat * inputDir.z + cameraRightFlat * inputDir.x;
                groundDashDirection.Normalize();
            }
            else
            {
                // Fallback (Straight backward dash, no input)
                groundDashDirection = -transform.forward;
            }

            // Deduce the correct animation simply based on the dash direction relative to where the character is already facing.
            // This cleanly handles both free movement and lock-on behavior without camera glitches or forced rotation snapping.
            Vector3 localDashDir = transform.InverseTransformDirection(groundDashDirection);
            if (Mathf.Abs(localDashDir.x) > Mathf.Abs(localDashDir.z))
            {
                dashAnimDir = localDashDir.x > 0 ? 3 : 2; // Right or Left
            }
            else
            {
                dashAnimDir = localDashDir.z > 0 ? 0 : 1; // Forward or Backward
            }

            // Turn off IK
            characterMovement.DisableFeetIK();

            characterAnimation.Dash(dashAnimDir);
            characterAnimation.SetDashing(true);

            characterMovement.stopMotion = true;
            cameraController?.SetFOVState(CameraFOVState.Parkour);
            cameraController?.ShakeLight();

            // Sample the curve over normalised time (0→1) for a designer-tunable force profile
            groundDashTween = DOVirtual.Float(0f, 1f, groundDashSetting.dashTime, (t) =>
            {
                if (characterMovement == null || characterMovement.rb == null || !isGrounded)
                    return;

                float force = groundDashSetting.dashForce * groundDashSetting.dashCurve.Evaluate(t);
                characterMovement.rb.linearVelocity = new Vector3(
                    groundDashDirection.x * force,
                    characterMovement.rb.linearVelocity.y,
                    groundDashDirection.z * force);
            })
            .SetEase(Ease.Linear) // Curve drives the shape; Linear keeps sampling honest
            .SetUpdate(UpdateType.Fixed)
            .OnComplete(() =>
            {
                isGroundDashing = false;
                characterAnimation.SetDashing(false);
                ClearParkourState(ParkourState.Dashing);
                characterMovement.stopMotion = false;
                characterMovement.EnableFeetIK();
                isGroundDashRecovering = true;
                groundDashRecoverTimer = groundDashSetting.recoverTime;
                cameraController?.SetFOVState(CameraFOVState.Run);
            })
            .OnKill(() =>
            {
                isGroundDashing = false;
                characterAnimation?.SetDashing(false);
                ClearParkourState(ParkourState.Dashing);
                if (characterMovement != null) 
                {
                    characterMovement.stopMotion = false;
                    characterMovement.EnableFeetIK();
                }
                cameraController?.SetFOVState(CameraFOVState.Walk);
            });
}

        /// <summary>
        /// Explicitly triggers a backward dash, pushing the player away from their current facing direction.
        /// </summary>
        public void PerformBackDash()
        {
            if (isGroundDashing || isAirDashing || !allowMovement) return;

            // Lock camera
            cameraController?.SetCameraRotationLock(true);

            // Turn off IK
            characterMovement.DisableFeetIK();

            // Compute dash direction (strict backward relative to current facing)
            Vector3 dashDir = -transform.forward;

            if (isGrounded)
            {
                if (!TrySetParkourState(ParkourState.Dashing)) return;

                isGroundDashing = true;
                groundDashTween?.Kill();
                groundDashDirection = dashDir;
                characterAnimation.Dash(1); // 1 = Backward
                characterAnimation.SetDashing(true);
                
                characterMovement.stopMotion = true;
                cameraController?.SetFOVState(CameraFOVState.Parkour);
                cameraController?.ShakeLight();

                groundDashTween = DOVirtual.Float(0f, 1f, groundDashSetting.dashTime, (t) =>
                {
                    if (characterMovement == null || characterMovement.rb == null || !isGrounded) return;
                    float force = groundDashSetting.dashForce * groundDashSetting.dashCurve.Evaluate(t);
                    characterMovement.rb.linearVelocity = new Vector3(groundDashDirection.x * force, characterMovement.rb.linearVelocity.y, groundDashDirection.z * force);
                })
                .SetEase(Ease.Linear)
                .SetUpdate(UpdateType.Fixed)
                .OnComplete(() => {
                    isGroundDashing = false;
                    characterAnimation.SetDashing(false);
                    ClearParkourState(ParkourState.Dashing);
                    characterMovement.stopMotion = false;
                    characterMovement.EnableFeetIK();
                    isGroundDashRecovering = true;
                    groundDashRecoverTimer = groundDashSetting.recoverTime;
                    cameraController?.SetFOVState(CameraFOVState.Walk);
                    cameraController?.SetCameraRotationLock(false);
                })
                .OnKill(() => {
                    isGroundDashing = false;
                    characterAnimation?.SetDashing(false);
                    ClearParkourState(ParkourState.Dashing);
                    if (characterMovement != null) 
                    {
                        characterMovement.stopMotion = false;
                        characterMovement.EnableFeetIK();
                    }
                    cameraController?.SetCameraRotationLock(false);
                });
                }
                else if (!hasAirDashed)
                {
                if (!TrySetParkourState(ParkourState.Dashing)) return;

                hasAirDashed = true;
                isAirDashing = true;
                airDashTween?.Kill();
                airDashDirection = dashDir;
                characterAnimation.Dash(1); // 1 = Backward
                characterAnimation.SetDashing(true);
                
                characterMovement.stopMotion = true;
                cameraController?.SetFOVState(CameraFOVState.AirDash);
                cameraController?.ShakeMedium();

                airDashTween = DOVirtual.Float(0f, 1f, airDashSetting.dashTime, (t) =>
                {
                    if (characterMovement == null || characterMovement.rb == null || isGrounded) return;
                    float force = airDashSetting.dashForce * airDashSetting.dashCurve.Evaluate(t);
                    characterMovement.rb.linearVelocity = airDashDirection * force + new Vector3(0, characterMovement.rb.linearVelocity.y, 0);
                })
                .SetEase(Ease.Linear)
                .SetUpdate(UpdateType.Fixed)
                .OnComplete(() => {
                    isAirDashing = false;
                    characterAnimation.SetDashing(false);
                    ClearParkourState(ParkourState.Dashing);
                    characterMovement.stopMotion = false;
                    characterMovement.EnableFeetIK();
                    airDashRecoverTimer = airDashSetting.recoverTime;
                    cameraController?.SetFOVState(CameraFOVState.Walk);
                    cameraController?.SetCameraRotationLock(false);
                })
                .OnKill(() => {
                    isAirDashing = false;
                    characterAnimation?.SetDashing(false);
                    ClearParkourState(ParkourState.Dashing);
                    if (characterMovement != null) 
                    {
                        characterMovement.stopMotion = false;
                        characterMovement.EnableFeetIK();
                    }
                    cameraController?.SetCameraRotationLock(false);
                });
                }
else
{
    // Fallback unlock if nothing happened
    characterMovement.EnableFeetIK();
    cameraController?.SetCameraRotationLock(false);
}
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

            cameraController?.ShakeLight();

            // Set animation trigger via unified controller
            characterAnimation.Jump();

            // Apply upward force using DOTweento smoothly transition vertical velocity extremely quickly (more natural "muscle push" feel)
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

                // Restore FOV to current intended movement speed
                if (characterMovement.GetState() == MovementState.Running)
                    cameraController?.SetFOVState(CameraFOVState.Run);
                else
                    cameraController?.SetFOVState(CameraFOVState.Walk);
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
                cameraController?.SetFOVState(CameraFOVState.Idle);
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
            cameraController?.SetFOVState(CameraFOVState.Run);
        }
        public void ToggleWalk()
        {
            if (characterMovement.GetState() != MovementState.Walking)
            {
                characterMovement.SetCurrentState(MovementState.Walking);
                characterMovement.curSpeed = characterMovement.walkSpeed;
                characterAnimation.animator.SetBool("Run", false);
            }
            cameraController?.SetFOVState(CameraFOVState.Walk);
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
