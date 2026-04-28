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
        public float wallRunSpeedBase = 16.5f;
        public float wallRunSpeedMax = 32f;
        public float wallRunAcceleration = 12f;
        public float wallGravity = 1.8f;
        public float wallStickyForce = 60f;
public float maxWallRunTime = 4.0f;
        public float wallRunCooldown = 0.2f;
        
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
        private float currentWallDistance;
        private const float probeOffset = 0.8f; // Probe is offset 0.8m AWAY from player center
        private const float targetWallDistance = 1.22f; // Probe origin to wall surface (0.8m offset + 0.42m character radius)

        private ClimbController climbController;

        private void Awake()
        {
            thirdPersonController = GetComponent<ThirdPersonController>();
            characterMovement = GetComponent<MovementCharacterController>();
            characterInput = GetComponent<InputCharacterController>();
            climbController = GetComponent<ClimbController>();
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

            CheckForWall();

            if (isWallRunning)
            {
                if (climbController != null && climbController.ClimbCheck())
                {
                    if (thirdPersonController.characterDetection.showDebug) Debug.Log("[WallRun] Stopping: Transition to Ledge Grab.");
                    StopWallRun(startCooldown: false);
                    return;
                }

                wallRunTimer += Time.deltaTime;

                if (wallRunTimer >= maxWallRunTime)
                {
                    if (thirdPersonController.characterDetection.showDebug) Debug.Log("[WallRun] Stopping: Max time reached.");
                    StopWallRun();
                }
                else if (thirdPersonController.characterDetection.IsGrounded(0.2f) && wallRunTimer > 0.5f)
                {
                    if (characterMovement.rb.linearVelocity.y <= 0.1f)
                    {
                        if (thirdPersonController.characterDetection.showDebug) Debug.Log("[WallRun] Stopping: Player is grounded.");
                        StopWallRun();
                    }
                }
                else if (characterInput.ConsumeJumpPressedBuffered())
                {
                    WallKick();
                }
            }
        }

        private bool IsValidWall(Collider col)
        {
            if (!isWallRunning && col == lastWall && !thirdPersonController.isGrounded) return false;
            if (string.IsNullOrEmpty(wallRunTag)) return true;
            return col.CompareTag(wallRunTag);
        }

        private void CheckForWall()
        {
            if (thirdPersonController.isGrounded && !isWallRunning)
            {
                lastWall = null; 
            }

            if (!canWallRun) return;

            Vector3 rayOrigin = transform.position + (Vector3.up * raycastYOffset);
            bool wallFound = false;
            RaycastHit bestHit = new RaycastHit();

            float checkDist = isWallRunning ? wallCheckDistance + 2.5f : wallCheckDistance;

            // Maintenance Phase: Stay on current wall using a robust SphereCast fan
            if (isWallRunning)
            {
                Vector3[] stickDirections = {
                    -wallNormal,
                    (Quaternion.AngleAxis(45, Vector3.up) * -wallNormal).normalized,
                    (Quaternion.AngleAxis(-45, Vector3.up) * -wallNormal).normalized,
                    (Quaternion.AngleAxis(30, transform.right) * -wallNormal).normalized,
                    (Quaternion.AngleAxis(-30, transform.right) * -wallNormal).normalized
                };

                foreach (Vector3 probeDir in stickDirections)
                {
                    // Start probe offset away from wall looking in to avoid 'initial overlap'
                    Vector3 offsetOrigin = rayOrigin + (wallNormal * probeOffset);
                    if (Physics.SphereCast(offsetOrigin, sphereCastRadius * 0.8f, probeDir, out RaycastHit hit, checkDist, wallLayer) && IsValidWall(hit.collider))
                    {
                        bestHit = hit;
                        currentWallDistance = hit.distance;
                        wallFound = true;
                        break;
                    }
                }

                if (!wallFound && currentDir == WallRunDirection.Up)
                {
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
                Vector3[] searchDirs = {
                    transform.right,
                    -transform.right,
                    (transform.forward + transform.right).normalized,
                    (transform.forward - transform.right).normalized,
                    transform.forward,
                    (transform.forward + transform.right * 1.5f).normalized,
                    (transform.forward - transform.right * 1.5f).normalized
                };

                foreach (Vector3 dir in searchDirs)
                {
                    if (dir == transform.forward && thirdPersonController.isGrounded) continue;
                    if (Physics.SphereCast(rayOrigin, sphereCastRadius, dir, out RaycastHit hit, wallCheckDistance, wallLayer) && IsValidWall(hit.collider))
                    {
                        bestHit = hit;
                        wallFound = true;
                        break;
                    }
                }
            }

            if (wallFound)
            {
                // Capture distance for position correction logic
                currentWallDistance = bestHit.distance;

                // Aggressively blend normal to handle curved walls and jitter
                if (wallNormal == Vector3.zero) wallNormal = bestHit.normal.normalized;
                else wallNormal = Vector3.Slerp(wallNormal, bestHit.normal.normalized, Time.deltaTime * 20f);

                lastWall = bestHit.collider;

                Vector3 localHit = transform.InverseTransformPoint(bestHit.point);
                float fwdDot = Vector3.Dot(wallNormal, transform.forward);

                if (fwdDot < -0.7f && !thirdPersonController.isGrounded)
                {
                    currentDir = WallRunDirection.Up;
                }
                else if (localHit.x < 0)
                {
                    currentDir = WallRunDirection.Left;
                }
                else
                {
                    currentDir = WallRunDirection.Right;
                }

                if (!isWallRunning) StartWallRun();
            }
            else if (isWallRunning)
            {
                if (thirdPersonController.characterDetection.showDebug) Debug.Log("[WallRun] Stopping: Wall lost.");
                StopWallRun();
            }
        }

        private void StartWallRun()
        {
            if (currentDir == WallRunDirection.None) return;
            float sideVal = (currentDir == WallRunDirection.Left) ? -1f : (currentDir == WallRunDirection.Right ? 1f : 0f);

            if (!thirdPersonController.TrySetParkourState(ParkourState.WallRunning)) return;

            if (thirdPersonController.characterDetection.showDebug)
                Debug.Log($"[WallRun] Starting wallrun. Direction: {currentDir}, SideVal: {sideVal}, Normal: {wallNormal}");

            isWallRunning = true;
            wallRunTimer = 0f;
            characterMovement.stopMotion = true;

            thirdPersonController.isJumping = false;
            thirdPersonController.onAir = true;

            Vector3 horizontalVel = new Vector3(characterMovement.rb.linearVelocity.x, 0, characterMovement.rb.linearVelocity.z);
            currentWallRunSpeed = Mathf.Clamp(horizontalVel.magnitude, wallRunSpeedBase, wallRunSpeedMax);

            characterMovement.rb.useGravity = false;
            characterMovement.DisableFeetIK();

            float startY = characterMovement.rb.linearVelocity.y;
            characterMovement.rb.linearVelocity = new Vector3(characterMovement.rb.linearVelocity.x, Mathf.Clamp(startY, -0.5f, 2.5f), characterMovement.rb.linearVelocity.z);

            SwapColliders(true);

            thirdPersonController.cameraController?.SetFOVState(CameraFOVState.WallRun);
            thirdPersonController.cameraController?.SetWallRunTilt(sideVal * 20f);

            if (thirdPersonController.characterAnimation != null)
            {
                thirdPersonController.characterAnimation.SetWallRunning(true, sideVal);
                if (thirdPersonController.characterAnimation.HasState("Wallrun"))
                {
                    thirdPersonController.characterAnimation.animator.CrossFade("Wallrun", 0.1f);
                }
                else if (thirdPersonController.characterAnimation.HasState("Wall Run"))
                {
                    thirdPersonController.characterAnimation.animator.CrossFade("Wall Run", 0.1f);
                }
            }
        }

        private void ApplyWallRunPhysics()
        {
            Rigidbody rb = characterMovement.rb;
            currentWallRunSpeed = Mathf.MoveTowards(currentWallRunSpeed, wallRunSpeedMax, wallRunAcceleration * Time.fixedDeltaTime);

            Vector3 camFwd = thirdPersonController.mainCamera.forward;
            camFwd.y = 0; camFwd.Normalize();
            Vector3 camRt = thirdPersonController.mainCamera.right;
            camRt.y = 0; camRt.Normalize();
            
            Vector3 inputDir = (camFwd * characterInput.movement.y + camRt * characterInput.movement.x).normalized;
            if (characterInput.movement.sqrMagnitude < 0.01f) inputDir = transform.forward;

            Vector3 finalVelocity;

            if (currentDir == WallRunDirection.Up)
            {
                Vector3 projectedInput = Vector3.ProjectOnPlane(inputDir, wallNormal).normalized;
                Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
                Vector3 moveDir = (wallUp + projectedInput * 0.35f).normalized;
                finalVelocity = moveDir * currentWallRunSpeed;
            }
            else
            {
                Vector3 wallTangent = Vector3.Cross(wallNormal, Vector3.up).normalized;
                if (Vector3.Dot(inputDir, wallTangent) < 0) wallTangent = -wallTangent;
                
                float currentY = rb.linearVelocity.y;
                currentY -= wallGravity * Time.fixedDeltaTime;

                finalVelocity = (wallTangent * currentWallRunSpeed) + (Vector3.up * currentY);
            }

            // SNAPPY LATERAL CORRECTION (STICKINESS):
            // targetWallDistance is 1.22m (0.8m probe offset + ~0.42m character radius).
            // We use the distance from the offset probe to target exactly that distance from the face.
            float lateralError = currentWallDistance - targetWallDistance;
            Vector3 horizontalNormal = new Vector3(wallNormal.x, 0, wallNormal.z).normalized;
            
            // Correction: strongly pull character to exactly the target distance
            float correctionVelocity = -lateralError * 16f; 
            correctionVelocity = Mathf.Clamp(correctionVelocity, -10f, 10f);

            rb.linearVelocity = finalVelocity + (horizontalNormal * correctionVelocity);
            }

        private void WallKick()
        {
            bool isLaunch = characterInput.run;
            StopWallRun(startCooldown: false);

            Rigidbody rb = characterMovement.rb;
            Vector3 wallForward = Vector3.ProjectOnPlane(transform.forward, wallNormal).normalized;
            if (Vector3.Dot(transform.forward, wallForward) < 0) wallForward = -wallForward;

            float forceMult = isLaunch ? 1.25f : 1f;
            Vector3 kickDir = (wallNormal * wallKickForce * forceMult) + (Vector3.up * wallKickUpForce * forceMult) + (wallForward * wallKickForwardForce * forceMult);

            rb.linearVelocity = kickDir;
            thirdPersonController.ResetJumpTime();
            characterMovement.DisableFeetIK();

            thirdPersonController.cameraController?.ShakeMedium();
            
            if (isLaunch)
            {
                thirdPersonController.cameraController?.SetFOVState(CameraFOVState.AirDash);
                thirdPersonController.characterAnimation.animator.CrossFade("Predicted Jump", 0.1f);
            }

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
                thirdPersonController.characterAnimation.animator.CrossFade("Deep Jump", 0.1f);
            }

            thirdPersonController.cameraController?.ShakeMedium();
            StartCoroutine(WallRunCooldownRoutine(wallRunCooldown));
        }

        private void StopWallRun(bool startCooldown = true)
        {
            if (!isWallRunning) return;

            if (thirdPersonController.characterDetection.showDebug)
                Debug.Log("[WallRun] Stopping wallrun.");

            isWallRunning = false;
            currentDir = WallRunDirection.None;

            characterMovement.rb.useGravity = true;
            characterMovement.stopMotion = false;
            characterMovement.EnableFeetIK();
            thirdPersonController.ClearParkourState(ParkourState.WallRunning);
            
            if (!thirdPersonController.isGrounded)
            {
                thirdPersonController.isJumping = true;
                thirdPersonController.onAir = true;
            }

            SwapColliders(false);

            thirdPersonController.cameraController?.SetFOVState(CameraFOVState.Walk);
            thirdPersonController.cameraController?.ResetTilt();

            if (characterModel != null)
            {
                characterModel.DOLocalRotate(Vector3.zero, 0.6f).SetEase(Ease.OutSine);
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
