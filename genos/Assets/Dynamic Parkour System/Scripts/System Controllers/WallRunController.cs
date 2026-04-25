using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace Climbing
{
    public enum WallRunDirection { None, Left, Right, Up }

    [RequireComponent(typeof(ThirdPersonController))]
    public class WallRunController : MonoBehaviour
    {
        private ThirdPersonController thirdPersonController;
        private MovementCharacterController characterMovement;
        private InputCharacterController characterInput;

        [Header("Wallrun Settings")]
        public LayerMask wallLayer;
        public float wallRunSpeed = 7f;
        public float upWallRunSpeed = 5f;
        public float wallRunInputAcceleration = 1.25f;
        public float maxWallRunTime = 2.0f;
        public float wallDetectDistance = 0.8f;
        public float wallEntryForwardThreshold = 0.55f;
        public float wallParallelThreshold = 0.45f;
        public float wallStickForce = 15f;
        public float minimumHeight = 0.5f;

        [Header("Jump / Charge Settings")]
        public float wallJumpUpForce = 7f;
        public float wallJumpSideForce = 8f;
        public float maxWallChargeTime = 1f;
        public float extraWallChargeForceMultiplier = 1.5f;

        [Header("Dash Settings")]
        public float wallDashForce = 12f;
        public float wallDashDuration = 0.2f;

        [Header("Auto Climb / Mantle")]
        public LayerMask ledgeLayer;
        public float mantleCheckDistance = 1.0f;
        public float mantleHeight = 1.2f;
        public float mantleForwardOffset = 0.5f;
        public float mantleDuration = 0.18f;

        [Header("Collision Tweaks")]
        public float wallRunColliderRadius = 0.2f;
        private float originalColliderRadius;

        [HideInInspector] public bool isWallRunning = false;

        private float wallRunTimer;
        private Vector3 wallNormal;
        private Vector3 wallForward;
        private bool isChargingWallJump = false;
        private float wallJumpChargeTimer = 0f;
        private WallRunDirection currentDir = WallRunDirection.None;

        private bool isWallDashing = false;
        private bool isMantling = false;
        private Tween wallDashTween;
        private Tween mantleTween;

        private void Awake()
        {
            thirdPersonController = GetComponent<ThirdPersonController>();
            characterMovement = GetComponent<MovementCharacterController>();
            characterInput = GetComponent<InputCharacterController>();
        }

        private void Start()
        {
            if (thirdPersonController.normalCapsuleCollider != null)
                originalColliderRadius = thirdPersonController.normalCapsuleCollider.radius;
        }

        private void Update()
        {
            HandleStateTransitions();
        }

        private void FixedUpdate()
        {
            if (isWallRunning && !isWallDashing)
                ExecuteWallRunPhysics();
        }

        private void OnDisable()
        {
            KillTraversalTweens();
        }

        // =========================
        // STATE FLOW
        // =========================

        private void HandleStateTransitions()
        {
            if (isMantling)
                return;

            if (thirdPersonController.IsParkourBusy && !thirdPersonController.HasParkourState(ParkourState.WallRunning))
                return;

            if (!isWallRunning)
            {
                if (CheckWallEntry())
                    StartWallRun();
            }
            else
            {
                wallRunTimer += Time.deltaTime;

                if (wallRunTimer > maxWallRunTime || thirdPersonController.isGrounded || !UpdateWallNormal())
                {
                    StopWallRun();
                    return;
                }

                HandleWallInputs();
                TryAutoClimb();
            }
        }

        // =========================
        // INPUT DURING WALLRUN
        // =========================

        private void HandleWallInputs()
        {
            bool isJumping = characterInput.jump;
            bool isGrabbing = characterInput.run;

            // HOLD JUMP = CHARGE
            if (isJumping)
            {
                isChargingWallJump = true;
                wallJumpChargeTimer += Time.deltaTime;
                wallJumpChargeTimer = Mathf.Clamp(wallJumpChargeTimer, 0f, maxWallChargeTime);
                UpdateAnimatorSticking(true, wallJumpChargeTimer / maxWallChargeTime);
            }
            else
            {
                if (isChargingWallJump)
                {
                    WallJump(wallJumpChargeTimer / maxWallChargeTime);
                    return;
                }
                else if (isGrabbing)
                {
                    UpdateAnimatorSticking(true, 0f);
                }
                else
                {
                    UpdateAnimatorSticking(false, 0f);
                }
            }

            // DASH INPUT (optional bind)
            if (characterInput.ConsumeDashPressedBuffered() && !isWallDashing)
            {
                StartWallDash();
            }
        }

        // =========================
        // WALLRUN PHYSICS
        // =========================

        private void ExecuteWallRunPhysics()
        {
            Vector3 targetVelocity = Vector3.zero;
            bool isGrabbing = characterInput.run || isChargingWallJump;

            if (isGrabbing)
            {
                targetVelocity = -wallNormal * wallStickForce;
            }
            else
            {
                if (currentDir == WallRunDirection.Up)
                {
                    wallForward = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
                    targetVelocity = (wallForward * upWallRunSpeed)
                                   + (-wallNormal * wallStickForce);
                }
                else
                {
                    // Auto-curve and adjust along the wall continuously based on momentum
                    Vector3 curveForward = currentDir == WallRunDirection.Right
                        ? Vector3.Cross(Vector3.up, wallNormal).normalized
                        : Vector3.Cross(wallNormal, Vector3.up).normalized;

                    wallForward = curveForward; // Securely fallback to strict curvature facing

                    targetVelocity = (wallForward * wallRunSpeed)
                                   + (-wallNormal * wallStickForce);
                }
            }

            characterMovement.rb.linearVelocity = targetVelocity;

            if (!isGrabbing && wallForward != Vector3.zero)
            {
                // Add a dynamic lean into the wall so the character's legs stick properly
                Vector3 leanedUp = (Vector3.up + (-wallNormal * 0.35f)).normalized;
                Quaternion targetRot = Quaternion.LookRotation(wallForward, leanedUp);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 12f);
            }
        }

        // =========================
        // ENTRY / EXIT
        // =========================

        private bool CheckWallEntry()
        {
            if (thirdPersonController.IsParkourBusy) return false;
            if (thirdPersonController.isGrounded) return false;

            // Allow wall run to automate seamlessly without strictly requiring minimumHeight.
            // if (GetGroundDistance() < minimumHeight) return false;

            Vector3 origin = transform.position + Vector3.up;
            Vector3 moveDirection = GetWallEntryMoveDirection();
            float detectRadius = 0.5f; // Slightly larger for better automation

            // Check aggressively around the player when mid-air moving towards a wall.
            if (Physics.SphereCast(origin, detectRadius, transform.forward, out RaycastHit frontHit, wallDetectDistance * 1.5f, wallLayer) &&
                IsFrontWallRunnable(frontHit.normal, moveDirection))
            {
                wallNormal = frontHit.normal;
                currentDir = WallRunDirection.Up;
                return true;
            }

            if (Physics.SphereCast(origin, detectRadius, transform.right, out RaycastHit rightHit, wallDetectDistance * 1.5f, wallLayer) &&
                IsWallRunnable(rightHit.normal, moveDirection, wallParallelThreshold))
            {
                wallNormal = rightHit.normal;
                currentDir = WallRunDirection.Right;
                return true;
            }

            if (Physics.SphereCast(origin, detectRadius, -transform.right, out RaycastHit leftHit, wallDetectDistance * 1.5f, wallLayer) &&
                IsWallRunnable(leftHit.normal, moveDirection, wallParallelThreshold))
            {
                wallNormal = leftHit.normal;
                currentDir = WallRunDirection.Left;
                return true;
            }

            return false;
        }

        private bool UpdateWallNormal()
        {
            Vector3 origin = transform.position + Vector3.up;
            float dist = wallDetectDistance + 0.5f;

            Vector3 rayDir = -wallNormal;
            if (currentDir == WallRunDirection.Right) rayDir = transform.right;
            else if (currentDir == WallRunDirection.Left) rayDir = -transform.right;
            else if (currentDir == WallRunDirection.Up) rayDir = transform.forward;

            // Simple raycasts fanning out slightly to catch curves cleanly without SphereCast internal overlaps
            Vector3[] rayDirs = {
                rayDir,
                Quaternion.AngleAxis(15f, Vector3.up) * rayDir,
                Quaternion.AngleAxis(-15f, Vector3.up) * rayDir
            };

            foreach (var dir in rayDirs)
            {
                if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, wallLayer))
                {
                    wallNormal = hit.normal;
                    return true;
                }
            }

            // Absolute fallback along the tracked normal
            if (Physics.Raycast(origin + (wallNormal * 0.2f), -wallNormal, out RaycastHit fbHit, dist + 0.2f, wallLayer))
            {
                wallNormal = fbHit.normal;
                return true;
            }

            return false;
        }

        private void StartWallRun()
        {
            if (!thirdPersonController.TrySetParkourState(ParkourState.WallRunning))
                return;

            isWallRunning = true;
            wallRunTimer = 0f;
            wallJumpChargeTimer = 0f;
            isChargingWallJump = false;

            thirdPersonController.allowMovement = false;
            characterMovement.stopMotion = true;
            characterMovement.rb.useGravity = false;
            characterMovement.enableFeetIK = false; // Disable IK explicitly during wallruns

            if (thirdPersonController.normalCapsuleCollider != null)
                thirdPersonController.normalCapsuleCollider.radius = wallRunColliderRadius;

            characterMovement.rb.linearVelocity = new Vector3(
                characterMovement.rb.linearVelocity.x,
                0,
                characterMovement.rb.linearVelocity.z
            );

            if (currentDir == WallRunDirection.Up)
            {
                wallForward = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
                SetAnimatorFloat("wallRunSide", 0f);
            }
            else
            {
                wallForward = currentDir == WallRunDirection.Right
                    ? Vector3.Cross(Vector3.up, wallNormal).normalized
                    : Vector3.Cross(wallNormal, Vector3.up).normalized;

                SetAnimatorFloat("wallRunSide", currentDir == WallRunDirection.Right ? 1f : -1f);
            }

            SetAnimatorBool("isWallRunning", true);
        }

        private void StopWallRun()
        {
            if (!isWallRunning) return;

            isWallRunning = false;
            currentDir = WallRunDirection.None;
            isChargingWallJump = false;
            wallDashTween?.Kill();
            wallDashTween = null;
            isWallDashing = false;

            thirdPersonController.allowMovement = true;
            characterMovement.stopMotion = false;
            characterMovement.rb.useGravity = true;
            characterMovement.EnableFeetIK(); // Re-enable IK explicitly
            thirdPersonController.ClearParkourState(ParkourState.WallRunning);

            if (thirdPersonController.normalCapsuleCollider != null)
                thirdPersonController.normalCapsuleCollider.radius = originalColliderRadius;

            SetAnimatorBool("isWallRunning", false);
            UpdateAnimatorSticking(false, 0f);
        }

        // =========================
        // WALL JUMP
        // =========================

        private void WallJump(float chargeRatio)
        {
            StopWallRun();

            float up = wallJumpUpForce * (1f + chargeRatio * extraWallChargeForceMultiplier);
            float side = wallJumpSideForce * (1f + chargeRatio * extraWallChargeForceMultiplier);

            Vector3 jumpDir = Vector3.up * up + wallNormal * side;

            characterMovement.rb.linearVelocity = Vector3.zero;
            characterMovement.rb.AddForce(jumpDir, ForceMode.Impulse);

            StartCoroutine(JumpMomentumRoutine());
        }

        private IEnumerator JumpMomentumRoutine()
        {
            characterMovement.stopMotion = true;
            thirdPersonController.isJumping = true;
            thirdPersonController.isGrounded = false;

            yield return new WaitForSeconds(0.4f);

            characterMovement.stopMotion = false;
        }

        // =========================
        // WALL DASH
        // =========================

        private void StartWallDash()
        {
            wallDashTween?.Kill();
            isWallDashing = true;

            Vector3 dashDir = currentDir == WallRunDirection.Up
                ? (wallForward != Vector3.zero ? wallForward : Vector3.up)
                : (wallForward != Vector3.zero ? wallForward : transform.forward);
            float stickForce = currentDir == WallRunDirection.Up ? wallStickForce : wallStickForce * 0.5f;

            wallDashTween = DOVirtual.Float(wallDashForce, wallRunSpeed, wallDashDuration, speed =>
                {
                    if (characterMovement == null || characterMovement.rb == null)
                        return;

                    characterMovement.rb.linearVelocity = (dashDir * speed) + (-wallNormal * stickForce);
                })
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Fixed)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    isWallDashing = false;
                    wallDashTween = null;
                })
                .OnKill(() =>
                {
                    isWallDashing = false;
                    wallDashTween = null;
                });
        }

        // =========================
        // AUTO CLIMB (MANTLE)
        // =========================

        private void TryAutoClimb()
        {
            if (isMantling)
                return;

            Vector3 origin = transform.position + Vector3.up * mantleHeight;

            if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, mantleCheckDistance, ledgeLayer))
            {
                Vector3 target = hit.point + Vector3.up * mantleHeight + transform.forward * mantleForwardOffset;
                StartMantle(target);
            }
        }

        private void StartMantle(Vector3 target)
        {
            mantleTween?.Kill();
            isMantling = true;
            StopWallRun();

            thirdPersonController.DisableController();
            characterMovement.rb.useGravity = false;
            characterMovement.rb.linearVelocity = Vector3.zero;

            mantleTween = transform.DOMove(target, mantleDuration)
                .SetEase(Ease.OutQuad)
                .SetTarget(this)
                .OnComplete(FinishMantle)
                .OnKill(() => mantleTween = null);
        }

        private void FinishMantle()
        {
            characterMovement.rb.useGravity = true;
            thirdPersonController.EnableController();
            isMantling = false;
            mantleTween = null;
        }

        private void KillTraversalTweens()
        {
            wallDashTween?.Kill();
            wallDashTween = null;
            mantleTween?.Kill();
            mantleTween = null;
            isWallDashing = false;
            isMantling = false;
        }

        // =========================
        // HELPERS
        // =========================

        private void UpdateAnimatorSticking(bool sticking, float ratio)
        {
            SetAnimatorBool("isWallSticking", sticking);
            SetAnimatorFloat("wallChargeRatio", ratio);
        }

        private Vector3 GetWallEntryMoveDirection()
        {
            Vector3 desiredMove = GetCameraRelativeMoveDirection();
            if (desiredMove.sqrMagnitude > 0.001f)
                return desiredMove;

            Vector3 velocity = characterMovement != null && characterMovement.rb != null
                ? characterMovement.rb.linearVelocity
                : Vector3.zero;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.001f)
                return velocity.normalized;

            return transform.forward;
        }

        private Vector3 GetCameraRelativeMoveDirection()
        {
            Vector2 input = characterInput.movement;
            Transform reference = transform; // Disable camera-based direction, use character transform instead

            Vector3 forward = reference.forward;
            Vector3 right = reference.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return (forward * input.y + right * input.x).normalized;
        }

        private bool IsFrontWallRunnable(Vector3 candidateNormal, Vector3 moveDirection)
        {
            float alignment = Vector3.Dot(candidateNormal.normalized, transform.forward);
            return alignment <= -wallEntryForwardThreshold;
        }

        private bool IsWallRunnable(Vector3 candidateNormal, Vector3 moveDirection, float threshold)
        {
            float alignment = Mathf.Abs(Vector3.Dot(candidateNormal.normalized, transform.forward));
            return alignment <= threshold;
        }

        private void SetAnimatorBool(string parameterName, bool value)
        {
            Animator animator = thirdPersonController.characterAnimation.animator;
            if (HasAnimatorParameter(animator, parameterName, AnimatorControllerParameterType.Bool))
                animator.SetBool(parameterName, value);
        }

        private void SetAnimatorFloat(string parameterName, float value)
        {
            Animator animator = thirdPersonController.characterAnimation.animator;
            if (HasAnimatorParameter(animator, parameterName, AnimatorControllerParameterType.Float))
                animator.SetFloat(parameterName, value);
        }

        private bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (animator == null)
                return false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == parameterType && parameter.name == parameterName)
                    return true;
            }

            return false;
        }

        private float GetGroundDistance()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 5f))
                return hit.distance;

            return 999f;
        }
    }
}
