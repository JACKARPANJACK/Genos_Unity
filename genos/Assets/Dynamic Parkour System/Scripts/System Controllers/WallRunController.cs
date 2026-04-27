using DG.Tweening;
using System.Collections;
using UnityEngine;

namespace Climbing
{
    public enum WallRunDirection { None, Left, Right, Up }

    [RequireComponent(typeof(ThirdPersonController))]
    [RequireComponent(typeof(MovementCharacterController))]
    [RequireComponent(typeof(InputCharacterController))]
    public class WallRunController : MonoBehaviour
    {
        [Header("References")]
        private ThirdPersonController thirdPersonController;
        private MovementCharacterController characterMovement;
        private InputCharacterController characterInput;
        [Tooltip("The actual 3D model/mesh to rotate towards the wall")]
        public Transform characterModel;

        [Header("Titanfall Physics")]
        public float wallRunSpeedBase = 13.5f;
        public float wallRunSpeedMax = 28f;
        public float wallRunAcceleration = 8f;
        public float wallGravity = 3.0f;
        public float wallStickyForce = 40f;
public float maxWallRunTime = 4.0f;
        public float wallRunCooldown = 0.25f;
        
        [Header("Wall Kick")]
        public float wallKickForce = 22f;
        public float wallKickUpForce = 13f;
        public float wallKickForwardForce = 8f;

        [Header("Colliders")]
        public Collider wallRunCollider;
        private Collider normalCollider;
        private bool wallRunColliderWasEnabled;

        [Header("Detection Settings")]
        public LayerMask wallLayer;
        public LayerMask groundLayer;
        public string wallRunTag = "WallRunnable";
        public float wallCheckDistance = 2.0f;
        public float sphereCastRadius = 0.5f;
        public float minJumpHeight = 0.3f;
        public float raycastYOffset = 1.0f; 

        [Header("Animation Settings")]
        public float modelRotationSpeed = 20f;

        [HideInInspector] public bool isWallRunning = false;
        private bool canWallRun = true;
        private Collider lastWall;

        private WallRunDirection currentDir = WallRunDirection.None;
        private Vector3 wallNormal;
        private float wallRunTimer;
        private float currentWallRunSpeed;

        private void Awake()
        {
            thirdPersonController = GetComponent<ThirdPersonController>();
            characterMovement = GetComponent<MovementCharacterController>();
            characterInput = GetComponent<InputCharacterController>();
        }

        private void Start()
        {
            normalCollider = thirdPersonController.normalCapsuleCollider;
            if (wallRunCollider != null) wallRunCollider.enabled = false;
        }

        private void Update()
        {
            HandleWallRunLogic();
            if (isWallRunning) UpdateAnimationsAndVisuals();
        }

        private void FixedUpdate()
        {
            if (isWallRunning) ApplyWallRunPhysics();
        }

        private void HandleWallRunLogic()
        {
            if (thirdPersonController.IsParkourBusy && !thirdPersonController.HasParkourState(ParkourState.WallRunning))
                return;

            // Shift must be held to start or stay on a wall
            if (!characterInput.run)
            {
                if (isWallRunning) StopWallRun();
                return;
            }

            CheckForWall();

            if (isWallRunning)
            {
                wallRunTimer += Time.deltaTime;

                // Exit conditions: time limit, grounded (but give it a tiny grace period to leave the ground)
                if (wallRunTimer >= maxWallRunTime || (thirdPersonController.isGrounded && wallRunTimer > 0.2f))
                {
                    StopWallRun();
                }
                else if (characterInput.ConsumeJumpPressedBuffered())
                {
                    WallKick();
                }
            }
        }

        private bool IsValidWall(Collider col)
        {
            // Allow re-sticking to the same wall if we are already wallrunning (to stay on it)
            // But prevent immediate re-sticking after a jump/kick from the same wall.
            if (!isWallRunning && col == lastWall && !thirdPersonController.isGrounded) return false;
            
            if (string.IsNullOrEmpty(wallRunTag)) return true;
            return col.CompareTag(wallRunTag);
        }

        private void CheckForWall()
        {
            // Check if jumping or in air
            if (thirdPersonController.isGrounded && !isWallRunning)
            {
                lastWall = null; 
                // Do not return here so we can start wall running from the ground
            }

            if (!canWallRun) return;

            Vector3 rayOrigin = transform.position + (Vector3.up * raycastYOffset);

            bool wallFound = false;
            RaycastHit hit;

            // forgiving distance
            float checkDist = isWallRunning ? wallCheckDistance + 0.8f : wallCheckDistance;

            // Only search for Up Direction if we are actively jumping into the wall or looking straight up at it
            bool wantsUpClimb = !thirdPersonController.isGrounded;

            // Search priority: Check current direction first if already running
            if (isWallRunning)
            {
                Vector3 checkDir = (currentDir == WallRunDirection.Right) ? transform.right : ((currentDir == WallRunDirection.Left) ? -transform.right : transform.forward);
                if (Physics.SphereCast(rayOrigin, sphereCastRadius * 1.2f, checkDir, out hit, checkDist, wallLayer) && IsValidWall(hit.collider))
                {
                    wallNormal = hit.normal;
                    wallFound = true;
                }
                else if (currentDir == WallRunDirection.Up)
                {
                    // Upper cast missed; check lower body to detect ledge for automatic mantle
                    Vector3 lowerRayOrigin = transform.position + (Vector3.up * 0.4f);
                    if (Physics.Raycast(lowerRayOrigin, transform.forward, out RaycastHit lowerHit, checkDist + 0.5f, wallLayer) && IsValidWall(lowerHit.collider))
                    {
                        WallMantle();
                        return;
                    }
                }
            }

            if (!wallFound)
            {
                // General detection
                // Right
                if (Physics.SphereCast(rayOrigin, sphereCastRadius, transform.right, out hit, checkDist, wallLayer) && IsValidWall(hit.collider))
                {
                    wallNormal = hit.normal;
                    currentDir = WallRunDirection.Right;
                    wallFound = true;
                    lastWall = hit.collider;
                }
                // Left
                else if (Physics.SphereCast(rayOrigin, sphereCastRadius, -transform.right, out hit, checkDist, wallLayer) && IsValidWall(hit.collider))
                {
                    wallNormal = hit.normal;
                    currentDir = WallRunDirection.Left;
                    wallFound = true;
                    lastWall = hit.collider;
                }
                // Up (Require jumping/midair to trigger upwards run)
                else if (wantsUpClimb && Physics.SphereCast(rayOrigin, sphereCastRadius, transform.forward, out hit, checkDist * 1.5f, wallLayer) && IsValidWall(hit.collider))
                {
                    wallNormal = hit.normal;
                    currentDir = WallRunDirection.Up;
                    wallFound = true;
                    lastWall = hit.collider;
                }
                // Diagonal Forward Checks (Helps with jumping INTO walls at angles)
                else if (Physics.SphereCast(rayOrigin, sphereCastRadius, (transform.forward + transform.right).normalized, out hit, checkDist, wallLayer) && IsValidWall(hit.collider))
                {
                    wallNormal = hit.normal;
                    currentDir = WallRunDirection.Right;
                    wallFound = true;
                    lastWall = hit.collider;
                }
                else if (Physics.SphereCast(rayOrigin, sphereCastRadius, (transform.forward - transform.right).normalized, out hit, checkDist, wallLayer) && IsValidWall(hit.collider))
                {
                    wallNormal = hit.normal;
                    currentDir = WallRunDirection.Left;
                    wallFound = true;
                    lastWall = hit.collider;
                }
            }

            if (wallFound)
            {
                if (!isWallRunning) StartWallRun();
            }
            else if (isWallRunning)
            {
                StopWallRun();
            }
        }

        private void StartWallRun()
        {
            if (!thirdPersonController.TrySetParkourState(ParkourState.WallRunning)) return;

            isWallRunning = true;
            wallRunTimer = 0f;

            // Prevent standard movement from interfering
            characterMovement.stopMotion = true;

            // Maintain entry momentum
            Vector3 horizontalVel = new Vector3(characterMovement.rb.linearVelocity.x, 0, characterMovement.rb.linearVelocity.z);
            currentWallRunSpeed = Mathf.Max(horizontalVel.magnitude, wallRunSpeedBase);
            currentWallRunSpeed = Mathf.Min(currentWallRunSpeed + 2f, wallRunSpeedMax); // Slight entry boost

            characterMovement.rb.useGravity = false;
            characterMovement.DisableFeetIK();  // Disable IK for the run duration

            // Initial vertical adjustment
            float yVel = characterMovement.rb.linearVelocity.y;
            characterMovement.rb.linearVelocity = new Vector3(characterMovement.rb.linearVelocity.x, Mathf.Max(yVel * 0.5f, 0), characterMovement.rb.linearVelocity.z);

            SwapColliders(true);

            // Camera Effects
            thirdPersonController.cameraController?.SetFOVState(CameraFOVState.WallRun);
            float tilt = currentDir == WallRunDirection.Left ? -20f : (currentDir == WallRunDirection.Right ? 20f : 0f);
            thirdPersonController.cameraController?.SetWallRunTilt(tilt);

            if (thirdPersonController.characterAnimation != null)
            {
                float side = currentDir == WallRunDirection.Left ? -1f : (currentDir == WallRunDirection.Right ? 1f : 0f);
                thirdPersonController.characterAnimation.SetWallRunning(true, side);
            }
            }

            private void ApplyWallRunPhysics()
            {
                Rigidbody rb = characterMovement.rb;

                // Accelerate over time
                currentWallRunSpeed = Mathf.MoveTowards(currentWallRunSpeed, wallRunSpeedMax, wallRunAcceleration * Time.fixedDeltaTime);

                Vector3 projectedVelocity;

                if (currentDir == WallRunDirection.Up)
                {
                    // Ninja Gaiden style vertical wallrun - projected onto the wall perfectly
                    Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
                    projectedVelocity = wallUp * currentWallRunSpeed;
                }
                else
                {
                    // Titanfall style horizontal wallrun projected along the wall
                    Vector3 wallForward = Vector3.ProjectOnPlane(transform.forward, wallNormal).normalized;
                    if (Vector3.Dot(transform.forward, wallForward) < 0) wallForward = -wallForward;

                    float currentY = rb.linearVelocity.y;
                    currentY -= wallGravity * Time.fixedDeltaTime;

                    projectedVelocity = (wallForward * currentWallRunSpeed) + (Vector3.up * currentY);
                }

                // STICKY FORCE: Push harder into the wall normal to ride corners/curves
                projectedVelocity += -wallNormal * wallStickyForce;

                rb.linearVelocity = projectedVelocity;
            }

        private void WallKick()
        {
            StopWallRun(startCooldown: false);

            Rigidbody rb = characterMovement.rb;

            // Titanfall / MGR style "Kick" - project forward momentum and burst outward/upward
            Vector3 wallForward = Vector3.ProjectOnPlane(transform.forward, wallNormal).normalized;
            if (Vector3.Dot(transform.forward, wallForward) < 0) wallForward = -wallForward;

            Vector3 kickDir = (wallNormal * wallKickForce) + (Vector3.up * wallKickUpForce) + (wallForward * wallKickForwardForce);

            rb.linearVelocity = kickDir;

            thirdPersonController.cameraController?.ShakeMedium();
            StartCoroutine(WallRunCooldownRoutine(wallRunCooldown));
        }

        private void WallMantle()
        {
            StopWallRun(startCooldown: false);

            Rigidbody rb = characterMovement.rb;
            rb.linearVelocity = Vector3.zero;

            Vector3 mantleDir = (Vector3.up * wallKickUpForce * 0.85f) + (transform.forward * wallKickForwardForce * 1.5f);
            rb.AddForce(mantleDir, ForceMode.Impulse);

            if (thirdPersonController.characterAnimation != null)
            {
                // Play a generic vault/deep jump to simulate pulling over the edge
                thirdPersonController.characterAnimation.animator.CrossFade("Deep Jump", 0.1f);
            }

            thirdPersonController.cameraController?.ShakeMedium();
            StartCoroutine(WallRunCooldownRoutine(wallRunCooldown));
        }

        private void StopWallRun(bool startCooldown = true)
        {
            if (!isWallRunning) return;

            isWallRunning = false;
            currentDir = WallRunDirection.None;

            characterMovement.rb.useGravity = true;
            characterMovement.stopMotion = false;
            characterMovement.EnableFeetIK(); // Re-enable feet IK
            thirdPersonController.ClearParkourState(ParkourState.WallRunning);
            SwapColliders(false);

            thirdPersonController.cameraController?.SetFOVState(CameraFOVState.Walk);
            thirdPersonController.cameraController?.ResetTilt();

            // Reset model rotation to local identity
            if (characterModel != null)
            {
                characterModel.DOLocalRotate(Vector3.zero, 0.2f);
            }

            if (thirdPersonController.characterAnimation != null)
            {
                thirdPersonController.characterAnimation.SetWallRunning(false);
            }

            if (startCooldown) StartCoroutine(WallRunCooldownRoutine(wallRunCooldown));
        }

        private void UpdateAnimationsAndVisuals()
        {
            if (thirdPersonController.characterAnimation != null)
            {
                float targetSide = (currentDir == WallRunDirection.Left) ? -1f : ((currentDir == WallRunDirection.Right) ? 1f : 0f);
                float currentSide = thirdPersonController.characterAnimation.animator.GetFloat("WallRunSide");
                thirdPersonController.characterAnimation.UpdateWallRunSide(Mathf.Lerp(currentSide, targetSide, Time.deltaTime * 10f));
            }

            if (characterModel != null)
            {
                if (currentDir == WallRunDirection.Up)
                {
                    // Look up along the wall while orienting the back towards the wall normal
                    Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(wallUp, wallNormal);
                    characterModel.rotation = Quaternion.Slerp(characterModel.rotation, targetRotation, Time.deltaTime * modelRotationSpeed);
                }
                else
                {
                    Vector3 wallForward = Vector3.ProjectOnPlane(transform.forward, wallNormal).normalized;
                    if (Vector3.Dot(transform.forward, wallForward) < 0) wallForward = -wallForward;

                    Vector3 targetUp = Vector3.up;
                    if (currentDir == WallRunDirection.Left) targetUp = Quaternion.AngleAxis(-25f, wallForward) * Vector3.up;
                    else if (currentDir == WallRunDirection.Right) targetUp = Quaternion.AngleAxis(25f, wallForward) * Vector3.up;

                    // Blend with the wall normal to hug angled surfaces seamlessly
                    targetUp = Vector3.Slerp(targetUp, wallNormal + Vector3.up, 0.4f).normalized;

                    Quaternion targetRotation = Quaternion.LookRotation(wallForward, targetUp);
                    characterModel.rotation = Quaternion.Slerp(characterModel.rotation, targetRotation, Time.deltaTime * modelRotationSpeed);
                }
            }
        }

        private void SwapColliders(bool useWallRunCollider)
        {
            if (wallRunCollider == null || normalCollider == null) return;
            if (useWallRunCollider)
            {
                wallRunColliderWasEnabled = wallRunCollider.enabled;
                normalCollider.enabled = false;
                wallRunCollider.enabled = true;
            }
            else
            {
                normalCollider.enabled = true;
                wallRunCollider.enabled = wallRunColliderWasEnabled;
            }
        }

        private IEnumerator WallRunCooldownRoutine(float duration)
        {
            canWallRun = false;
            yield return new WaitForSeconds(duration);
            canWallRun = true;
        }
    }
}