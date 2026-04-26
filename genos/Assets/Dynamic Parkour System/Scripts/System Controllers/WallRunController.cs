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

                // Exit conditions: time limit, grounded
                if (wallRunTimer >= maxWallRunTime || thirdPersonController.isGrounded)
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
            if (thirdPersonController.isGrounded && !isWallRunning)
            {
                lastWall = null; 
                return;
            }
            if (!canWallRun) return;

            Vector3 rayOrigin = transform.position + (Vector3.up * raycastYOffset);

            bool wallFound = false;
            RaycastHit hit;

            // forgiving distance
            float checkDist = isWallRunning ? wallCheckDistance + 0.8f : wallCheckDistance;

            // Search priority: Check current direction first if already running
            if (isWallRunning)
            {
                Vector3 checkDir = (currentDir == WallRunDirection.Right) ? transform.right : ((currentDir == WallRunDirection.Left) ? -transform.right : transform.forward);
                if (Physics.SphereCast(rayOrigin, sphereCastRadius * 1.2f, checkDir, out hit, checkDist, wallLayer) && IsValidWall(hit.collider))
                {
                    wallNormal = hit.normal;
                    wallFound = true;
                }
            }

            if (!wallFound)
            {
                // General detection
                if (Physics.SphereCast(rayOrigin, sphereCastRadius, transform.right, out hit, checkDist, wallLayer) && IsValidWall(hit.collider))
                {
                    wallNormal = hit.normal;
                    currentDir = WallRunDirection.Right;
                    wallFound = true;
                    lastWall = hit.collider;
                }
                else if (Physics.SphereCast(rayOrigin, sphereCastRadius, -transform.right, out hit, checkDist, wallLayer) && IsValidWall(hit.collider))
                {
                    wallNormal = hit.normal;
                    currentDir = WallRunDirection.Left;
                    wallFound = true;
                    lastWall = hit.collider;
                }
                else if (Physics.SphereCast(rayOrigin, sphereCastRadius, transform.forward, out hit, checkDist, wallLayer) && IsValidWall(hit.collider))
                {
                    wallNormal = hit.normal;
                    currentDir = WallRunDirection.Up;
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

            // Vector parallel to the wall and the floor
            Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up).normalized;
            if (Vector3.Dot(transform.forward, wallForward) < 0) wallForward = -wallForward;

            if (currentDir == WallRunDirection.Up)
            {
                // Vertical wallrun (climb)
                Vector3 targetVel = Vector3.up * currentWallRunSpeed;
                // Add a small push into the wall to stay attached
                targetVel += -wallNormal * 4f; 
                rb.linearVelocity = targetVel;
            }
            else
            {
                // Horizontal wallrun
                float currentY = rb.linearVelocity.y;
                currentY -= wallGravity * Time.fixedDeltaTime;

                // Combine forward movement along wall with gravitational fall
                Vector3 targetVel = (wallForward * currentWallRunSpeed) + (Vector3.up * currentY);
                
                // STICKY FORCE: Push harder into the wall normal
                targetVel += -wallNormal * 6f;

                rb.linearVelocity = targetVel;
            }
}

        private void WallKick()
        {
            StopWallRun(startCooldown: false);

            Rigidbody rb = characterMovement.rb;
            
            // Titanfall style "Kick" - Push away from wall and forward
            Vector3 kickDir = (wallNormal * wallKickForce) + (Vector3.up * wallKickUpForce) + (transform.forward * wallKickForwardForce);
            
            rb.linearVelocity = kickDir;
            
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
                Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up).normalized;
                if (Vector3.Dot(transform.forward, wallForward) < 0) wallForward = -wallForward;

                Vector3 targetUp = Vector3.up;
                if (currentDir == WallRunDirection.Left) targetUp = Quaternion.AngleAxis(-20f, wallForward) * Vector3.up;
                else if (currentDir == WallRunDirection.Right) targetUp = Quaternion.AngleAxis(20f, wallForward) * Vector3.up;

                Quaternion targetRotation = Quaternion.LookRotation(wallForward, targetUp);
                characterModel.rotation = Quaternion.Slerp(characterModel.rotation, targetRotation, Time.deltaTime * modelRotationSpeed);
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