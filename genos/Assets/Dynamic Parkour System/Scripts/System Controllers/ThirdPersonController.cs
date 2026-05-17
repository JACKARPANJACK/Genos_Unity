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
using Climbing.Effects;

namespace Climbing
{
    public enum ParkourState { None, ScriptedTraversal, WallRunning, Dashing }

    [RequireComponent(typeof(InputCharacterController))]
    [RequireComponent(typeof(MovementCharacterController))]
    [RequireComponent(typeof(AnimationCharacterController))]
    [RequireComponent(typeof(DetectionCharacterController))]
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
        [HideInInspector] public bool isMeleeAttacking = false;
        [HideInInspector] public bool dummy = false;
public bool isDashing => isAirDashing || isGroundDashing;
        [HideInInspector] public ParkourState activeParkourState = ParkourState.None;

        private float maxFallHeight = 0f;
        private bool isChargeJumpActive = false;

        [Header("Air Dash Settings")]
        public DashSetting airDashSetting = new DashSetting { dashForce = 35f, dashTime = 0.2f, recoverTime = 0.4f };
        public bool canAirDash = true;
        private bool hasAirDashed = false;
        private bool isAirDashing = false;
        private float airDashRecoverTimer = 0f;
        private Vector3 airDashDirection;
        private Tween airDashTween;

        [Header("Ground Dash Settings")]
        public DashSetting groundDashSetting = new DashSetting { dashForce = 45f, dashTime = 0.25f, recoverTime = 0.3f };
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

        [Header("VFX Sockets")]
        public Transform leftHandSocket;
        public Transform rightHandSocket;
        public Transform leftShoulderSocket;
        public Transform rightShoulderSocket;

        [Header("VFX Particle Systems")]
        public GameObject dashVFX;
        public GameObject chargeJumpVFX;
        public GameObject jumpLaunchVFX; // New
        public GameObject landingCrackVFX; // New
        public GameObject electricityAfterImagePrefab;
public Material afterImageMaterial;
        public ParticleSystem electricityTrailPS;

        private List<GameObject> activeDashEffects = new List<GameObject>();
private float nextAfterImageTime = 0f;
        public float afterImageInterval = 0.05f;

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

            AutoAssignSockets();
            }

        private void Start()
        {
            characterMovement.OnLanded += characterAnimation.Land;
            characterMovement.OnLanded += () => 
            {
                float fallDistance = maxFallHeight - transform.position.y;
                if (isChargeJumpActive || fallDistance > 20f)
                {
                    cameraController?.ShakeMedium();
                    PlayVFXAtFeet(landingCrackVFX);
                }
                isChargeJumpActive = false;
            };
            characterMovement.OnLanded += () => 
            {
                if (characterInput.run && characterInput.movement.sqrMagnitude > 0.01f)
                    cameraController?.SetFOVState(CameraFOVState.Run);
                else if (characterInput.movement.sqrMagnitude > 0.01f)
                    cameraController?.SetFOVState(CameraFOVState.Walk);
                else
                    cameraController?.SetFOVState(CameraFOVState.Idle);
            };
            characterMovement.OnFall += characterAnimation.Fall;
        }

            private void PlayVFXAtFeet(GameObject vfx)
            {
                if (vfx == null) return;
                Vector3 pos = transform.position;
                // Ensure we spawn at the feet level
                pos.y += 0.05f; 
                
                GameObject instance = Instantiate(vfx, pos, vfx.transform.rotation);
                instance.layer = 0; // Force Default layer
                foreach (Transform t in instance.GetComponentsInChildren<Transform>())
                    t.gameObject.layer = 0;

                if (characterDetection != null && characterDetection.showDebug)
                    Debug.Log($"[VFX] Spawned {vfx.name} at {pos} on layer {instance.layer}");

                // If the prefab has its own destruction logic (like DecalFadeOut), skip auto-destruction
                if (instance.GetComponent<Climbing.Effects.DecalFadeOut>() != null)
                    return;

                float maxDuration = 2f;
                var systems = instance.GetComponentsInChildren<ParticleSystem>();
                if (systems.Length > 0)
                {
                    foreach (var ps in systems)
                    {
                        float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                        if (duration > maxDuration) maxDuration = duration;
                    }
                    Destroy(instance, maxDuration);
                }
                else
                {
                    Destroy(instance, 2f);
                }
            }

        void Update()
        {
            //Detect if Player is on Ground
            bool previousGrounded = isGrounded;
            isGrounded = OnGround();

            if (!isGrounded)
            {
                if (previousGrounded)
                    maxFallHeight = transform.position.y;
                else if (transform.position.y > maxFallHeight)
                    maxFallHeight = transform.position.y;
            }

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
                    cameraController.swayAmount = 0.15f; // Reduced from 0.5 for stability
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

        public void ResetJumpTime()
        {
            lastJumpTime = Time.time;
            isJumping = true;
            onAir = true;
            isGrounded = false;
        }

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
            {
                UpdateAfterImages();
                return; // Managed by DoTween
            }

            if (canAirDash && !hasAirDashed && !isGrounded && !isVaulting && !IsParkourBusy)
            {
                if ((Time.time > lastJumpTime + 0.25f && characterInput.ConsumeJumpPressedBuffered()) || characterInput.ConsumeDashPressedBuffered())
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

            SpawnHandEffects(dashVFX, airDashSetting.dashTime + 0.5f);

            airDashTween?.Kill();

            // Consume the input so it doesn't fire elsewhere
            characterInput.ConsumeJumpPressedBuffered();

            // Find if backwards input is applied relative to camera
            bool isBackDash = characterInput.movement.y < -0.1f;
            bool hasInput = characterInput.movement.sqrMagnitude > 0.01f;

            Vector3 camFwd = mainCamera.forward;
            camFwd.y = 0;
            camFwd.Normalize();

            Vector3 camRt = mainCamera.right;
            camRt.y = 0;
            camRt.Normalize();

            Vector3 dashDir;
            float overrideDashForce = -1f;

            if (camFwd.sqrMagnitude > 0.001f)
            {
                if (hasInput)
                {
                    dashDir = (camFwd * characterInput.movement.y + camRt * characterInput.movement.x).normalized;
                }
                else
                {
                    dashDir = camFwd;
                }

                airDashDirection = dashDir;

                // Determine if we should rotate the character to face the dash direction or keep camera facing
                // Keep the character facing forward (camera direction) when side dashing to trigger directional animations properly
                float angle = Vector3.Angle(camFwd, dashDir);
                if (angle > 45f)
                {
                    transform.rotation = Quaternion.LookRotation(camFwd, Vector3.up);
                }
                else
                {
                    transform.rotation = Quaternion.LookRotation(dashDir, Vector3.up);
                }
            }
            else
            {
                // Fallback
                airDashDirection = isBackDash ? -transform.forward : transform.forward;
            }

            // Turn off IK during dash
            characterMovement.DisableFeetIK();

            // Calculate directional animation based on the dash direction relative to current rotation
            var dashDirAnim = GetDashDirection(airDashDirection);
            characterAnimation.Dash(dashDirAnim);
            characterAnimation.SetDashing(true);

            characterMovement.stopMotion = true;
            cameraController?.SetFOVState(CameraFOVState.AirDash);
            cameraController?.ShakeMedium();

            // Sample the curve over normalised time (0→1) to build a designer-tunable force profile
            airDashTween = DOVirtual.Float(0f, 1f, airDashSetting.dashTime, (t) =>
            {
                if (characterMovement == null || characterMovement.rb == null || isGrounded)
                    return;

                float baseForce = (overrideDashForce > 0) ? overrideDashForce : airDashSetting.dashForce;
                float force = baseForce * airDashSetting.dashCurve.Evaluate(t);
                
                float keepY = characterMovement.rb.linearVelocity.y > 0
                    ? 0f
                    : characterMovement.rb.linearVelocity.y;
                
                // If targeting a point, we include vertical velocity in the dash vector
                Vector3 finalVel = airDashDirection * force;
                if (overrideDashForce <= 0) finalVel.y = keepY;
                
                characterMovement.rb.linearVelocity = finalVel;
            })
            .SetEase(Ease.Linear) // Curve drives the shape; Linear keeps sampling honest
            .SetUpdate(UpdateType.Fixed)
            .OnComplete(() =>
            {
                isAirDashing = false;
                characterAnimation.SetDashing(false);
                ClearDashEffects();
                StopElectricityEffects();
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
                ClearDashEffects();
                StopElectricityEffects();
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

            // Fix: Only cancel if we enter a conflicting state (Vault, WallRun), 
            // but NOT just because isGrounded is false for a frame (jitter).
            if (isVaulting || (wallRunController != null && wallRunController.isWallRunning))
            {
                if (isGroundDashing)
                {
                    isGroundDashing = false;
                    groundDashTween?.Kill();
                }
                return;
            }

            if (isGroundDashing)
            {
                UpdateAfterImages();
                return; // Managed by DoTween
            }

            if (isGrounded && canGroundDash && !isGroundDashRecovering && (characterInput.ConsumeDoubleTapDashBuffered() || characterInput.ConsumeDashPressedBuffered()))
            {
                PerformGroundDash();
            }
        }

        private void UpdateAfterImages()
        {
            if (electricityTrailPS != null && !electricityTrailPS.isEmitting)
                electricityTrailPS.Play();

            if (Time.time >= nextAfterImageTime)
            {
                SpawnAfterImage();
                nextAfterImageTime = Time.time + afterImageInterval;
            }
        }

        private void StopElectricityEffects()
        {
            if (electricityTrailPS != null) electricityTrailPS.Stop();
        }

        private void SpawnAfterImage()
        {
            if (electricityAfterImagePrefab == null) return;

            // Get the current mesh snapshot from the character
            SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in renderers)
            {
                if (!smr.gameObject.activeInHierarchy || !smr.enabled) continue;

                Mesh mesh = new Mesh();
                smr.BakeMesh(mesh);

                GameObject go = Instantiate(electricityAfterImagePrefab);
                var ai = go.GetComponent<Climbing.Effects.PlayerAfterImage>();
                if (ai == null) ai = go.AddComponent<Climbing.Effects.PlayerAfterImage>();

                ai.Setup(mesh, smr.transform.position, smr.transform.rotation, afterImageMaterial != null ? afterImageMaterial : smr.sharedMaterial, new Color(0, 0.8f, 1f, 0.6f));
                }
                }

        private void PerformGroundDash()
        {
            if (!TrySetParkourState(ParkourState.Dashing)) return;

            isGroundDashing = true;

            SpawnHandEffects(dashVFX, groundDashSetting.dashTime + 0.5f);

            groundDashTween?.Kill();

            // Find if backwards input is applied
            bool isBackDash = characterInput.movement.y < -0.1f;
            bool hasInput = characterInput.movement.sqrMagnitude > 0.01f;

            Vector3 camFwd = mainCamera.forward;
            camFwd.y = 0;
            camFwd.Normalize();

            Vector3 camRt = mainCamera.right;
            camRt.y = 0;
            camRt.Normalize();

            Vector3 dashDir;
            float overrideDashForce = -1f;

            if (camFwd.sqrMagnitude > 0.001f)
            {
                if (hasInput)
                {
                    dashDir = (camFwd * characterInput.movement.y + camRt * characterInput.movement.x).normalized;
                }
                else
                {
                    dashDir = camFwd;
                }

                groundDashDirection = dashDir;

                // Determine if we should rotate to face dash or keep camera facing
                float angle = Vector3.Angle(camFwd, dashDir);
                if (angle > 45f)
                {
                    transform.rotation = Quaternion.LookRotation(camFwd, Vector3.up);
                }
                else
                {
                    transform.rotation = Quaternion.LookRotation(dashDir, Vector3.up);
                }
            }
            else
            {
                // Fallback
                groundDashDirection = isBackDash ? -transform.forward : transform.forward;
            }

            // Turn off IK
            characterMovement.DisableFeetIK();

            // Calculate directional animation
            var dashDirAnim = GetDashDirection(groundDashDirection);
            characterAnimation.Dash(dashDirAnim);
            characterAnimation.SetDashing(true);

            characterMovement.stopMotion = true;
            cameraController?.SetFOVState(CameraFOVState.Parkour);
            cameraController?.ShakeLight();

            // Sample the curve over normalised time (0→1) for a designer-tunable force profile
            groundDashTween = DOVirtual.Float(0f, 1f, groundDashSetting.dashTime, (t) =>
            {
                if (characterMovement == null || characterMovement.rb == null || !isGrounded)
                    return;

                float baseForce = (overrideDashForce > 0) ? overrideDashForce : groundDashSetting.dashForce;
                float force = baseForce * groundDashSetting.dashCurve.Evaluate(t);
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
                StopElectricityEffects();
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
                StopElectricityEffects();
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

            // Prevent backdash during wallrun
            if (wallRunController != null && wallRunController.isWallRunning) return;

            // Lock camera
cameraController?.SetCameraRotationLock(true);

            // Turn off IK
            characterMovement.DisableFeetIK();

            // Compute dash direction (strict backward relative to camera)
            Vector3 dashDir;

            Vector3 camFwd = mainCamera.forward;
            camFwd.y = 0;
            camFwd.Normalize();

            Vector3 camRt = mainCamera.right;
            camRt.y = 0;
            camRt.Normalize();

            if (camFwd.sqrMagnitude > 0.001f)
            {
                bool hasInput = characterInput.movement.sqrMagnitude > 0.01f;
                if (hasInput)
                {
                    dashDir = (camFwd * characterInput.movement.y + camRt * characterInput.movement.x).normalized;
                }
                else
                {
                    dashDir = -camFwd; 
                }

                transform.rotation = Quaternion.LookRotation(camFwd, Vector3.up);
            }
            else
            {
                dashDir = -transform.forward;
            }

            if (isGrounded)
            {
                if (!TrySetParkourState(ParkourState.Dashing)) return;

                isGroundDashing = true;
                PlayVFX(dashVFX);
                SpawnHandEffects(dashVFX);
                groundDashTween?.Kill();
                groundDashDirection = dashDir;
                characterAnimation.Dash(AnimationCharacterController.DashDirection.Backward);
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

                    // Depending on settings or velocity, map appropriately. 
                    // Let's default Dash/Jump calls to the explicit ChargeJump/Jump profile here directly.
                    cameraController?.SetFOVState(CameraFOVState.ChargeJump);

                hasAirDashed = true;
                isAirDashing = true;
                PlayVFX(dashVFX);
                SpawnHandEffects(dashVFX);
                airDashTween?.Kill();
                airDashDirection = dashDir;
                characterAnimation.Dash(AnimationCharacterController.DashDirection.Backward);
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

                // Set FOV to 'ChargeJump' while holding
                if (jumpChargeTimer > 0.1f)
                    cameraController?.SetFOVState(CameraFOVState.ChargeJump);

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
            if (chargeRatio > 0.1f)
            {
                PlayVFX(chargeJumpVFX);
                PlayVFXAtFeet(jumpLaunchVFX);
                isChargeJumpActive = true;
                if (chargeRatio > 0.5f) StartCoroutine(AfterImageBurst(0.3f));
            }

            isJumping = true;
            onAir = true;
            isGrounded = false;
            lastJumpTime = Time.time;

            cameraController?.ShakeLight();
            cameraController?.SetFOVState(CameraFOVState.Jump);

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

        private IEnumerator AfterImageBurst(float duration)
        {
            float end = Time.time + duration;
            while (Time.time < end)
            {
                UpdateAfterImages();
                yield return new WaitForSeconds(afterImageInterval);
            }
        }

        public Point FindNearestParkourPoint(float maxDistance, float maxAngle)
{
            Point[] allPoints = UnityEngine.Object.FindObjectsByType<Point>(FindObjectsSortMode.None);
            Point bestPoint = null;
            float closestDist = maxDistance;

            Vector3 searchDir = transform.forward;
            if (characterInput.movement.sqrMagnitude > 0.01f)
            {
                Vector3 camFwd = mainCamera.forward;
                camFwd.y = 0;
                camFwd.Normalize();
                Vector3 camRt = mainCamera.right;
                camRt.y = 0;
                camRt.Normalize();
                searchDir = (camFwd * characterInput.movement.y + camRt * characterInput.movement.x).normalized;
            }

            foreach (Point p in allPoints)
            {
                Vector3 toPoint = p.transform.position - transform.position;
                float dist = toPoint.magnitude;
                if (dist < closestDist)
                {
                    float angle = Vector3.Angle(searchDir, toPoint.normalized);
                    if (angle < maxAngle)
                    {
                        closestDist = dist;
                        bestPoint = p;
                    }
                }
            }

            return bestPoint;
        }

        private bool OnGround()
        {
            // Add a slight cooldown before we can be considered grounded again
            // This prevents the system from immediately grounding the player while the jump is launching
            if (Time.time < lastJumpTime + 0.2f) return false;

            // If we are actively wallrunning, we are NOT grounded, even if close to the floor.
            // This prevents the wallrun from cancelling itself immediately after the 0.2s grace period.
            if (activeParkourState == ParkourState.WallRunning) return false;
            
            return characterDetection.IsGrounded(stepHeight);
            }

            private AnimationCharacterController.DashDirection GetDashDirection(Vector3 dashDir)
            {
            Vector3 localDash = transform.InverseTransformDirection(dashDir);
            float forward = localDash.z;
            float right = localDash.x;

            if (Mathf.Abs(forward) > Mathf.Abs(right))
            {
                return (forward > -0.1f) ? AnimationCharacterController.DashDirection.Forward : AnimationCharacterController.DashDirection.Backward;
            }
            else
            {
                return (right > 0) ? AnimationCharacterController.DashDirection.Right : AnimationCharacterController.DashDirection.Left;
            }
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

                // Check for combat transitions to prevent snapping
                bool transitioningFromCombat = false;
                if (characterAnimation != null && characterAnimation.animator != null)
                {
                    var info = characterAnimation.animator.GetCurrentAnimatorStateInfo(0);
                    transitioningFromCombat = info.IsTag("Melee") && characterAnimation.animator.IsInTransition(0);
                }

                if (!characterInput.aim && !isMeleeAttacking && !transitioningFromCombat)
                {
                    RotatePlayer(direction);
                }
characterAnimation.animator.SetBool("Released", false);

                // Reset turning angle when player starts moving
                characterAnimation.animator.SetFloat("TurnAngle", 0f);

                // Restore FOV to current intended movement speed
                bool skipMovementFOV = IsParkourBusy;
                if (!skipMovementFOV)
                {
                    if (characterMovement.GetState() == MovementState.Running)
                        cameraController?.SetFOVState(CameraFOVState.Run);
                    else
                        cameraController?.SetFOVState(CameraFOVState.Walk);
                }
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

                if (!characterInput.aim && Mathf.Abs(angleDiff) > idleTurnToCameraThreshold && idleTurnTimer >= idleTurnToCameraDelay)
                {
                    float angle = Mathf.SmoothDampAngle(
                        transform.eulerAngles.y,
                        mainCamera.eulerAngles.y,
                        ref idleTurnSmoothVelocity,
                        idleTurnToCameraSmoothTime);
                    transform.rotation = Quaternion.Euler(0f, angle, 0f);
                }

                ToggleWalk();

                bool skipIdleFOV = IsParkourBusy;
                if (!skipIdleFOV)
                {
                    cameraController?.SetFOVState(CameraFOVState.Idle);
                }

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
            return characterInput.movement.sqrMagnitude >= minMagnitude;
        }

        public bool WantsAutoParkour(float minForwardInput = 0.25f)
        {
            return WantsParkourTraversal(0.01f) && characterInput.movement.y >= minForwardInput;
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

        private void PlayVFX(GameObject vfx)
        {
            if (vfx == null) return;

            if (vfx.scene.IsValid()) // In Scene
            {
                foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.Play();
                }
            }
            else // Prefab
            {
                GameObject instance = Instantiate(vfx, transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;

                float maxDuration = 2f;
                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.Play();
                    float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                    if (duration > maxDuration) maxDuration = duration;
                }
                Destroy(instance, maxDuration);
            }
        }

        private void SpawnHandEffects(GameObject vfx, float durationOverride = -1f)
        {
            if (vfx == null) return;

            // Ensure sockets are assigned if null
            if (leftHandSocket == null || rightHandSocket == null || leftShoulderSocket == null || rightShoulderSocket == null)
            {
                AutoAssignSockets();
            }

            GameObject left = null;
            GameObject right = null;
            GameObject leftShoulder = null;
            GameObject rightShoulder = null;

            if (leftHandSocket != null)
            {
                left = Instantiate(vfx, leftHandSocket, false);
                left.transform.localPosition = Vector3.zero;
                left.transform.localRotation = Quaternion.identity;
                activeDashEffects.Add(left);
            }

            if (rightHandSocket != null)
            {
                right = Instantiate(vfx, rightHandSocket, false);
                right.transform.localPosition = Vector3.zero;
                right.transform.localRotation = Quaternion.identity;
                activeDashEffects.Add(right);
            }

            if (leftShoulderSocket != null)
            {
                leftShoulder = Instantiate(vfx, leftShoulderSocket, false);
                leftShoulder.transform.localPosition = Vector3.zero;
                leftShoulder.transform.localRotation = Quaternion.identity;
                activeDashEffects.Add(leftShoulder);
            }

            if (rightShoulderSocket != null)
            {
                rightShoulder = Instantiate(vfx, rightShoulderSocket, false);
                rightShoulder.transform.localPosition = Vector3.zero;
                rightShoulder.transform.localRotation = Quaternion.identity;
                activeDashEffects.Add(rightShoulder);
            }

            float maxDuration = durationOverride > 0f ? durationOverride : 2f;
            if (durationOverride <= 0f)
            {
                foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
                {
                    float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                    if (duration > maxDuration) maxDuration = duration;
                }
            }

            if (left != null) Destroy(left, maxDuration);
            if (right != null) Destroy(right, maxDuration);
            if (leftShoulder != null) Destroy(leftShoulder, maxDuration);
            if (rightShoulder != null) Destroy(rightShoulder, maxDuration);
        }

        private void ClearDashEffects()
        {
            foreach (var fx in activeDashEffects)
            {
                if (fx != null) Destroy(fx);
            }
            activeDashEffects.Clear();
        }

        private void AutoAssignSockets()
        {
            if (characterAnimation == null || characterAnimation.animator == null) return;
            var anim = characterAnimation.animator;

            if (leftHandSocket == null) leftHandSocket = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            if (rightHandSocket == null) rightHandSocket = anim.GetBoneTransform(HumanBodyBones.RightHand);

            // Shoulders: Default to UpperArm because LeftShoulder in Unity is exactly the clavicle, making thrusters spawn inside the chest instead of the actual shoulder edge.
            if (leftShoulderSocket == null) leftShoulderSocket = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (rightShoulderSocket == null) rightShoulderSocket = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);

            // Fallback for shoulders if UpperArm is missing
            if (leftShoulderSocket == null) leftShoulderSocket = anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
            if (rightShoulderSocket == null) rightShoulderSocket = anim.GetBoneTransform(HumanBodyBones.RightShoulder);
        }
}
        }
