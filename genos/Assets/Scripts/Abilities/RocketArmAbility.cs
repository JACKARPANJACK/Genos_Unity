using UnityEngine;
using Climbing;
using System.Collections;
using System.Collections.Generic;

namespace Climbing.Abilities
{
    public class RocketArmAbility : MonoBehaviour
    {
        public RocketArmType currentType = RocketArmType.Normal;
        public GameObject armPrefab;
        public GameObject explosiveVFX;
        public GameObject hunterFireVFX;
        public float cooldown = 0.2f;
        public float punchRange = 3f;
        
        private float lastFireTime;
        private bool isRightArmOut = false;
        private bool isLeftArmOut = false;
        private bool lastWasRight = false;
        private bool isPerformingTakedown = false;

        private bool takedownGrabTriggered = false;
        private bool takedownReleaseTriggered = false;
        private bool punchHitTriggered = false;
        private Transform punchTarget;
        private int punchVariant = 0; // 0 = Normal, 1 = Launcher, 2 = Smasher

        private ThirdPersonController controller;
        private InputCharacterController input;
        private Transform rightForearm;
        private Transform leftForearm;
        private Vector3 originalRightScale;
        private Vector3 originalLeftScale;
        private Animator playerAnim;

        private RocketArmType[] armTypes = { RocketArmType.Normal, RocketArmType.Ricochet, RocketArmType.Explosive, RocketArmType.Grabber, RocketArmType.Homing, RocketArmType.Hunter };
        private int currentTypeIndex = 0;
        private UI.AbilityUIController uiController;
        private List<RocketArmProjectile> activeProjectiles = new List<RocketArmProjectile>();
        private float lastCycleTime;
        private const float cycleCooldown = 0.2f;

        private string[] launchAnimStates = { "Launch_0", "Launch_1", "Launch_2", "Launch_3" };
private string takedownStateBase = "Takedown_";

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            input = GetComponent<InputCharacterController>();
            uiController = Object.FindFirstObjectByType<UI.AbilityUIController>();
            playerAnim = GetComponentInChildren<Animator>();
            
            if (playerAnim != null)
            {
                rightForearm = playerAnim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                leftForearm = playerAnim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                if (rightForearm != null) originalRightScale = rightForearm.localScale;
                if (leftForearm != null) originalLeftScale = leftForearm.localScale;
            }
        }

        void Update()
        {
            if (input == null || isPerformingTakedown) return;

            float delta = input.GetCycleDelta();
            if (Mathf.Abs(delta) > 0.1f && Time.time > lastCycleTime + cycleCooldown)
            {
                int dir = delta > 0 ? 1 : -1;
                CycleType(dir);
                lastCycleTime = Time.time;
            }
        }

        public void CycleType(int direction)
        {
            currentTypeIndex = (currentTypeIndex + direction + armTypes.Length) % armTypes.Length;
            currentType = armTypes[currentTypeIndex];
            if (uiController != null) uiController.UpdateWeaponDisplay(currentType);
        }

        public void UseAbility(bool isQuickshot = false)
        {
            if (isPerformingTakedown) return;

            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeProjectiles[i] != null && activeProjectiles[i].IsGrabbing)
                {
                    activeProjectiles[i].ManualDetach();
                    return;
                }
            }

            if (Time.time < lastFireTime + cooldown) return;

            if (IsEnemyInMeleeRange(out Transform enemy))
            {
                PerformRocketPunch(enemy);
                return;
            }

            bool useRight = !lastWasRight;
            if (useRight && isRightArmOut) useRight = false;
            if (!useRight && isLeftArmOut) useRight = true;
            if ((useRight && isRightArmOut) || (!useRight && isLeftArmOut)) return;

            FireArm(useRight, isQuickshot);
            lastWasRight = useRight;
        }

        private bool IsEnemyInMeleeRange(out Transform enemy)
        {
            enemy = null;
            Vector3 detectCenter = transform.position + transform.forward * 1.5f + Vector3.up * 1.0f;
            Collider[] hits = Physics.OverlapSphere(detectCenter, punchRange);
            foreach (var hit in hits)
            {
                if (hit.transform.root == transform.root) continue;
                if (hit.CompareTag("Enemy") || hit.GetComponent<Rigidbody>() != null)
                {
                    enemy = hit.transform;
                    return true;
                }
            }
            return false;
        }

        public void PerformRocketPunch(Transform target)
        {
            if (target == null)
            {
                // Try to find target if null
                IsEnemyInMeleeRange(out target);
            }
            if (target != null)
                StartCoroutine(PerformRocketPunchRoutine(target));
        }

        private IEnumerator PerformRocketPunchRoutine(Transform target)
        {
            lastFireTime = Time.time;
            
            bool useRight = !lastWasRight;
            if (useRight && isRightArmOut) useRight = false;
            if (!useRight && isLeftArmOut) useRight = true;
            if ((useRight && isRightArmOut) || (!useRight && isLeftArmOut)) yield break;

            if (useRight) isRightArmOut = true; else isLeftArmOut = true;
            lastWasRight = useRight;

            SuppressMovement(0.7f);
            
            Transform targetForearm = useRight ? rightForearm : leftForearm;
            if (targetForearm != null) targetForearm.localScale = Vector3.one * 0.001f;

            punchVariant = Random.Range(0, 3); // 0, 1, or 2
            
            punchHitTriggered = false;
            punchTarget = target;

            string triggerName = "RocketPunch";
            string stateFallback = "RocketPunch";

            if (punchVariant == 1) 
            { 
                triggerName = "RocketPunchLaunch"; 
                stateFallback = "RocketPunch_Launch"; 
            }
            else if (punchVariant == 2) 
            { 
                triggerName = "RocketPunchSmash"; 
                stateFallback = "RocketPunch_Smash"; 
            }

            if (playerAnim != null) 
            {
                playerAnim.SetTrigger(triggerName);
                playerAnim.CrossFadeInFixedTime(stateFallback, 0.05f, 0);
                
                float timer = 0f;
                while (!punchHitTriggered && timer < 0.6f) 
                { 
                    yield return null; 
                    timer += Time.deltaTime; 
                }
            }

            // Launch the hand visually for the punch!
            Transform socket = useRight ? controller.rightHandSocket : controller.leftHandSocket;
            if (socket == null) socket = transform;

            Vector3 launchDir = (target.position - socket.position).normalized;
            GameObject arm = Instantiate(armPrefab, socket.position, Quaternion.LookRotation(launchDir));
            
            if (!useRight)
            {
                Transform visual = arm.transform.Find("Visual");
                if (visual != null) visual.localScale = new Vector3(visual.localScale.x, -visual.localScale.y, visual.localScale.z);
                else arm.transform.localScale = new Vector3(arm.transform.localScale.x, -arm.transform.localScale.y, arm.transform.localScale.z);
            }

            RocketArmProjectile projectile = arm.GetComponent<RocketArmProjectile>();
            projectile.hunterFireVFX = hunterFireVFX;
            projectile.SetupType(RocketArmType.Normal, explosiveVFX, false); // Just visually go and come back
            
            // Disable its collider so it doesn't double-interact with the main rocket punch physics
            Collider projCol = projectile.GetComponent<Collider>();
            if (projCol != null) projCol.enabled = false;

            activeProjectiles.Add(projectile);
            
            projectile.Launch(transform, socket, 250f, (returnedTarget) => {
                activeProjectiles.Remove(projectile);
                if (useRight) isRightArmOut = false; else isLeftArmOut = false;
                StartCoroutine(RestoreArm(targetForearm, useRight ? originalRightScale : originalLeftScale));
            });

            StartCoroutine(FailsafeRestoreArm(useRight, targetForearm, useRight ? originalRightScale : originalLeftScale, projectile));

            // Force it to return almost immediately so it acts as a short melee blast
            StartCoroutine(ForceReturnVisualArm(projectile, 0.1f));

            if (punchTarget != null)
            {
                if (explosiveVFX != null) Instantiate(explosiveVFX, punchTarget.position + Vector3.up * 1f, Quaternion.identity);

                Camera mainCam = controller?.mainCamera?.GetComponent<Camera>();
                if (mainCam != null)
                {
                    var shaker = mainCam.GetComponent<AllIn1VfxToolkit.Demo.Scripts.AllIn1Shaker>();
                    if (shaker == null) shaker = mainCam.gameObject.AddComponent<AllIn1VfxToolkit.Demo.Scripts.AllIn1Shaker>();
                    shaker.DoCameraShake(0.7f); // Single argument
                }

                Climbing.AI.DummyEnemy dummy = punchTarget.GetComponent<Climbing.AI.DummyEnemy>();
                if (dummy != null) dummy.TakeDamage(150f);

                UnityEngine.AI.NavMeshAgent agent = punchTarget.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = false;

                Rigidbody rb = punchTarget.GetComponent<Rigidbody>();
                if (rb != null) 
                {
                    rb.isKinematic = false; // Ensure not kinematic so they can fly
                    if (punchVariant == 1) // Launcher
                    {
                        rb.AddForce(Vector3.up * 400f + transform.forward * 50f, ForceMode.Impulse);
                        rb.AddTorque(Random.insideUnitSphere * 100f, ForceMode.Impulse);
                    }
                    else if (punchVariant == 2) // Smasher
                    {
                        rb.AddForce(Vector3.down * 400f + transform.forward * 50f, ForceMode.Impulse);
                    }
                    else // Normal
                    {
                        rb.AddForce((punchTarget.position - transform.position + Vector3.up * 1.5f).normalized * 200f, ForceMode.Impulse);
                        rb.AddTorque(Random.insideUnitSphere * 100f, ForceMode.Impulse);
                    }
                }
            }
        }

        private IEnumerator ForceReturnVisualArm(RocketArmProjectile proj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (proj != null) proj.ManualDetach();
        }

        private void FireArm(bool isRight, bool isQuickshot)
        {
            lastFireTime = Time.time;
            if (isRight) isRightArmOut = true; else isLeftArmOut = true;

            SuppressMovement(0.4f);

            Transform targetForearm = isRight ? rightForearm : leftForearm;
            if (targetForearm != null) targetForearm.localScale = Vector3.one * 0.001f;

            // Randomized Launch Animation
            if (playerAnim != null)
            {
                string stateName = launchAnimStates[Random.Range(0, launchAnimStates.Length)];
                playerAnim.CrossFadeInFixedTime(stateName, 0.05f, 0);
            }

            Transform socket = isRight ? controller.rightHandSocket : controller.leftHandSocket;
            if (socket == null) socket = transform;

            Vector3 targetPos;
            bool isAimedShot = false; // Aim cam logic removed

            if (isAimedShot)
{
                Camera cam = Camera.main;
                if (cam == null) cam = controller.mainCamera.GetComponent<Camera>();
                
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                // Target everything except Player
                int mask = ~(LayerMask.GetMask("Player") | LayerMask.GetMask("Ignore Raycast"));
                if (Physics.Raycast(ray, out RaycastHit hit, 300f, mask, QueryTriggerInteraction.Ignore))
                    targetPos = hit.point;
                else
                    targetPos = ray.GetPoint(150f);
            }
            else
            {
                targetPos = socket.position + transform.forward * 150f;
            }

            // Ensure the target isn't too close behind or inside the socket
            Vector3 toTarget = targetPos - socket.position;
            if (Vector3.Dot(toTarget, transform.forward) < 0.5f)
            {
                targetPos = socket.position + transform.forward * 10f;
            }

            Vector3 launchDir = (targetPos - socket.position).normalized;
            GameObject arm = Instantiate(armPrefab, socket.position, Quaternion.LookRotation(launchDir));

            if (!isRight)
            {
                Transform visual = arm.transform.Find("Visual");
                if (visual != null) visual.localScale = new Vector3(visual.localScale.x, -visual.localScale.y, visual.localScale.z);
                else arm.transform.localScale = new Vector3(arm.transform.localScale.x, -arm.transform.localScale.y, arm.transform.localScale.z);
            }

            RocketArmProjectile projectile = arm.GetComponent<RocketArmProjectile>();
            projectile.hunterFireVFX = hunterFireVFX;
            projectile.SetupType(currentType, explosiveVFX, currentType == RocketArmType.Grabber);
            
            float flightSpeed = isQuickshot ? 150f : 100f;
            activeProjectiles.Add(projectile);
            
            projectile.Launch(transform, socket, flightSpeed, (target) => {
                activeProjectiles.Remove(projectile);
                if (isRight) isRightArmOut = false; else isLeftArmOut = false;
                StartCoroutine(RestoreArm(targetForearm, isRight ? originalRightScale : originalLeftScale));
                
                if (target != null && currentType == RocketArmType.Hunter)
                    StartCoroutine(PerformTakedown(target));
            });

            StartCoroutine(FailsafeRestoreArm(isRight, targetForearm, isRight ? originalRightScale : originalLeftScale, projectile));
        }

        private IEnumerator FailsafeRestoreArm(bool isRight, Transform forearm, Vector3 scale, RocketArmProjectile proj)
        {
            yield return new WaitForSeconds(6.5f); // Must be longer than max flight timeout
            
            if (proj != null)
            {
                if (activeProjectiles.Contains(proj)) activeProjectiles.Remove(proj);
                Destroy(proj.gameObject);
            }

            if (forearm != null) forearm.localScale = scale;
            if (isRight) isRightArmOut = false; else isLeftArmOut = false;
        }

        private void SuppressMovement(float duration)
        {
            controller.allowMovement = false;
            controller.activeParkourState = ParkourState.ScriptedTraversal;
            controller.characterMovement.DisableFeetIK();
            controller.characterAnimation.animator.SetFloat("Velocity", 0f);
            
            // Fix root motion jitter by allowing it during suppress if needed
            // playerAnim.applyRootMotion = true; 

            StartCoroutine(DelayedRestoreMovement(duration));
        }

        private IEnumerator DelayedRestoreMovement(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!isPerformingTakedown)
            {
                controller.allowMovement = true;
                controller.activeParkourState = ParkourState.None;
                controller.characterMovement.EnableFeetIK();
            }
        }

        private IEnumerator RestoreArm(Transform forearm, Vector3 scale)
        {
            if (forearm != null) forearm.localScale = scale;
            yield break;
        }

        public void TakedownGrabEvent()
        {
            takedownGrabTriggered = true;
        }

        public void TakedownReleaseEvent()
        {
            takedownReleaseTriggered = true;
        }

        public void PunchHitEvent()
        {
            punchHitTriggered = true;
        }

        private IEnumerator PerformTakedown(Transform enemy)
        {
            isPerformingTakedown = true;
            controller.characterMovement.DisableFeetIK();
            controller.allowMovement = false;
            controller.activeParkourState = ParkourState.ScriptedTraversal;
            
            // 1. COLLISION IGNORE (ENVIRONMENT)
            int originalLayer = gameObject.layer;
            gameObject.layer = 2; // Ignore Raycast layer
            
            Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
            if (enemyRb != null) enemyRb.isKinematic = true;
            Collider enemyCol = enemy.GetComponent<Collider>();
            if (enemyCol != null) enemyCol.enabled = false;

            // Ignore collision with enemy specifically
            Collider playerCol = GetComponent<Collider>();
            if (playerCol != null && enemyCol != null)
                Physics.IgnoreCollision(playerCol, enemyCol, true);

            // 2. POSITIONING
            enemy.position = transform.position + transform.forward * 1.0f;
            enemy.rotation = transform.rotation * Quaternion.Euler(0, 180, 0); 

            Animator enemyAnim = enemy.GetComponentInChildren<Animator>();
            List<KeyValuePair<Transform, Transform>> attachments = new List<KeyValuePair<Transform, Transform>>();

            int rand = Random.Range(0, 3);
            string animState = takedownStateBase + rand;
            
            takedownGrabTriggered = false;
            takedownReleaseTriggered = false;

            if (enemyAnim != null)
            {
                enemyAnim.enabled = false;
                
                if (playerAnim != null)
                {
                    playerAnim.applyRootMotion = true; // Use root motion
                    playerAnim.CrossFadeInFixedTime(animState, 0.05f, 0);
                    
                    float timer = 0f;
                    while (!takedownGrabTriggered && timer < 1.0f) { yield return null; timer += Time.deltaTime; }
                    
                    AttachBone(HumanBodyBones.Head, HumanBodyBones.LeftHand, enemyAnim, attachments); 
                    AttachBone(HumanBodyBones.Hips, HumanBodyBones.RightHand, enemyAnim, attachments);
                    
                    timer = 0f;
                    while (!takedownReleaseTriggered && timer < 1.5f) { yield return null; timer += Time.deltaTime; }
                }
            }
            else
            {
                if (playerAnim != null)
                {
                    playerAnim.applyRootMotion = true;
                    playerAnim.CrossFadeInFixedTime(animState, 0.05f, 0);

                    float timer = 0f;
                    while (!takedownGrabTriggered && timer < 1.0f) { yield return null; timer += Time.deltaTime; }

                    enemy.SetParent(playerAnim.GetBoneTransform(HumanBodyBones.RightHand));
                    enemy.localPosition = new Vector3(0, -0.5f, 0); // Snap so player actually grabs the dummy

                    timer = 0f;
                    while (!takedownReleaseTriggered && timer < 1.5f) { yield return null; timer += Time.deltaTime; }
                }
            }

            // 3. CLEANUP
            foreach (var att in attachments) att.Key.SetParent(enemy);
            enemy.SetParent(null); // Unparent from player
            
            if (playerCol != null && enemyCol != null)
                Physics.IgnoreCollision(playerCol, enemyCol, false);

            if (explosiveVFX != null) Instantiate(explosiveVFX, enemy.position, Quaternion.identity);
            
            // Screen Shake for impact!
            Camera mainCamInner = controller?.mainCamera?.GetComponent<Camera>();
            if (mainCamInner != null)
            {
                var shaker = mainCamInner.GetComponent<AllIn1VfxToolkit.Demo.Scripts.AllIn1Shaker>();
                if (shaker == null) shaker = mainCamInner.gameObject.AddComponent<AllIn1VfxToolkit.Demo.Scripts.AllIn1Shaker>();
                shaker.DoCameraShake(1.0f); // Massive screen shake single argument
            }

            if (enemyRb != null)
            {
                UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = false;

                enemyRb.isKinematic = false;
                Vector3 knockbackDir = (transform.forward + Vector3.up * 0.5f).normalized;
                enemyRb.AddForce(knockbackDir * 200f, ForceMode.Impulse); // Boosted knockback for visibility
                enemyRb.AddTorque(Random.insideUnitSphere * 100f, ForceMode.Impulse); // Tumble factor
            }

            Climbing.AI.DummyEnemy dummy = enemy.GetComponent<Climbing.AI.DummyEnemy>();
            if (dummy != null) dummy.TakeDamage(9999f); // True damage

            Destroy(enemy.gameObject, 1.5f); // Let them fly before destroying
            
            gameObject.layer = originalLayer;
            isPerformingTakedown = false;
            controller.allowMovement = true;
            controller.activeParkourState = ParkourState.None;
            controller.characterMovement.EnableFeetIK();
        }

        private void AttachBone(HumanBodyBones enemyBone, HumanBodyBones playerBone, Animator enemyAnim, List<KeyValuePair<Transform, Transform>> attachments)
        {
            Transform eb = enemyAnim.GetBoneTransform(enemyBone);
            Transform pb = playerAnim.GetBoneTransform(playerBone);
            if (eb != null && pb != null)
            {
                eb.SetParent(pb);
                eb.localPosition = Vector3.zero;
                eb.localRotation = Quaternion.identity;
                attachments.Add(new KeyValuePair<Transform, Transform>(eb, pb));
            }
        }
    }
}
