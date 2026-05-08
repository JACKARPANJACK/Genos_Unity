using UnityEngine;
using Climbing;
using Climbing.Abilities;
using System.Collections;
using System.Collections.Generic;

namespace Climbing.Combat
{
    public class MeleeCombatController : MonoBehaviour
    {
        [System.Serializable]
        public class AttackData
        {
            public string stateName;
            public float damage = 25f;
            public float lungeForce = 8f;
            public float lungeDuration = 0.25f;
            public float targetPushForce = 15f;
            public float targetUpwardForce = 0f;
            public int shakeLevel = 1;
            public float totalDuration = 0.6f;
            public float cancelWindowStart = 0.45f;
            public VFXType vfxType = VFXType.DefaultImpact;
        }

        [Header("Settings")]
        public float comboResetTime = 1.0f;
        public float hitRange = 3.5f;
        public float hitAngle = 100f;
        public float magnetismRange = 12.0f;
public float magnetismAngle = 110f;
        public LayerMask enemyLayer;

        [Header("References")]
        private InputCharacterController input;
        private ThirdPersonController controller;
        private Animator animator;
        
        private float lastAttackTime;
private bool isAttacking = false;
        private bool canCancelCurrent = false;
        private float lightHoldTimer = 0f;
        private bool launcherTriggered = false;
        private Transform currentMagnetTarget;
        private Coroutine rotationCoroutine;
        private AttackData currentMove;

        // Combo Tracking
        private string activeString = "None"; 
        private int comboStep = 0;

        public bool IsInCombo => (Time.time - lastAttackTime < comboResetTime) || isAttacking;

        void Awake()
        {
            input = GetComponent<InputCharacterController>();
            controller = GetComponent<ThirdPersonController>();
            animator = GetComponentInChildren<Animator>();
            if (enemyLayer == 0) enemyLayer = LayerMask.GetMask("Enemy");
        }

        void Update()
        {
            if (input == null || controller.dummy) return;

            bool lightPressed = input.ConsumeLightAttackPressedBuffered();
            bool heavyPressed = input.ConsumeHeavyAttackPressedBuffered();

            // Finisher Check (RMB on low health enemy)
            if (heavyPressed && !isAttacking && controller.isGrounded)
            {
                var target = FindFinisherTarget();
                if (target != null)
                {
                    ExecuteFinisher(target);
                    return;
                }
            }

            // Handle Long Press for Launcher
            if (input.lightAttack)
            {
                lightHoldTimer += Time.deltaTime;
                if (lightHoldTimer > 0.35f && !launcherTriggered && !isAttacking && controller.isGrounded)
                {
                    launcherTriggered = true;
                    activeString = "W8";
                    comboStep = 1;
                    ExecuteAttack(true); 
                    return;
                }
            }
            else
            {
                lightHoldTimer = 0f;
                launcherTriggered = false;
            }

            if (Time.time - lastAttackTime > comboResetTime && !isAttacking)
            {
                if (activeString != "None") ResetCombo();
            }

            if ((!isAttacking || canCancelCurrent) && (lightPressed || heavyPressed))
            {
                canCancelCurrent = false;
                ExecuteAttack(lightPressed);
            }
        }

        private void ExecuteFinisher(Climbing.AI.DummyEnemy target)
        {
            StopAllCoroutines();
            isAttacking = true;
            controller.isMeleeAttacking = true;
            ResetCombo();

            string[] finishers = { "Finisher_01", "Finisher_02", "Finisher_Neck" };
            string selected = finishers[Random.Range(0, finishers.Length)];

            transform.rotation = Quaternion.LookRotation((target.transform.position - transform.position).normalized);
            target.PlayFinisher(selected);
            animator.CrossFadeInFixedTime("MeleeCombat." + selected, 0.1f);
            
            if (VFXManager.Instance != null)
            {
                VFXManager.Instance.ExecuteFinisherFlourish();
                VFXManager.Instance.SetPlayerTrailsActive(true);
            }

            AttackData finisherMove = new AttackData { 
                stateName = "MeleeCombat." + selected, 
                totalDuration = 2.5f, 
                cancelWindowStart = 2.2f 
            };
            StartCoroutine(HandleAttackTiming(finisherMove));
        }

        private Climbing.AI.DummyEnemy FindFinisherTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 3.5f, enemyLayer);
            foreach (var hit in hits)
            {
                var dummy = hit.GetComponentInParent<Climbing.AI.DummyEnemy>();
                if (dummy != null && dummy.IsFinisherReady) return dummy;
            }
            return null;
        }

        private void ResetCombo()
        {
            activeString = "None";
            comboStep = 0;
            controller.cameraController?.SetFOVState(CameraFOVState.Idle);
        }

        private void ExecuteAttack(bool isLight)
        {
            if (!controller.isGrounded)
            {
                PerformAirAttack(isLight);
                return;
            }

            // Kick: W + RMB (Heavy)
            if (!isLight && input.movement.y > 0.5f && activeString == "None")
            {
                activeString = "W4";
                comboStep = 2; // Lightning Kick
            }
            else if (isLight)
            {
                if (activeString != "W8")
                {
                    if (activeString == "None" && controller.characterMovement.GetState() == MovementState.Running)
                    {
                        activeString = "W4"; comboStep = 2; 
                    }
                    else if (activeString == "W1")
                    {
                        comboStep++; if (comboStep > 7) comboStep = 1;
                    }
                    else if (activeString == "W4") { activeString = "W1"; comboStep = 2; }
                    else if (activeString == "DWJ") { comboStep++; if (comboStep > 7) { ResetCombo(); ExecuteAttack(true); return; } }
                    else { activeString = "W1"; comboStep = 1; }
                }
            }
            else // Heavy Branching
            {
                if (activeString == "W1")
                {
                    if (comboStep == 1) { activeString = "W8"; comboStep = 1; }
                    else if (comboStep == 2) { activeString = "W2"; comboStep = 1; }
                    else if (comboStep == 3) { activeString = "W3"; comboStep = 1; }
                    else if (comboStep == 4) { activeString = "W13"; comboStep = 1; }
                    else if (comboStep == 5) { activeString = "DWJ"; comboStep = 1; }
                    else if (comboStep >= 6) { activeString = "W11"; comboStep = 1; }
                    else { activeString = "W2"; comboStep = 1; }
                }
                else if (activeString == "None") { activeString = "Basic"; comboStep = 1; }
                else if (activeString == "Basic") { comboStep++; if (comboStep > 2) { ResetCombo(); ExecuteAttack(false); return; } }
                else { comboStep++; }
            }

            string sName = GetStateName();
            if (string.IsNullOrEmpty(sName)) return;

            AttackData move = new AttackData { stateName = "MeleeCombat." + sName };
            ConfigureMoveDynamics(move);
            StartAttackRoutine(move);
        }

        private string GetStateName()
        {
            if (activeString == "W2" && comboStep > 2) { ResetCombo(); return null; }
            if (activeString == "W3" && comboStep > 3) { ResetCombo(); return null; }
            if (activeString == "W4" && comboStep > 2) { ResetCombo(); return null; }
            if (activeString == "W8" && comboStep > 2) { ResetCombo(); return null; }
            if (activeString == "W11" && comboStep > 5) { ResetCombo(); return null; }
            if (activeString == "W13" && comboStep > 4) { ResetCombo(); return null; }
            if (activeString == "Basic" && comboStep > 2) { ResetCombo(); return null; }
            if (activeString == "DWJ" && comboStep > 7) { ResetCombo(); return null; }

            if (activeString == "Basic") return comboStep == 1 ? "W_Jab" : "W_Cross";
            if (activeString == "DWJ") {
                string[] dwj = { "JG", "MSINGE", "PD", "PD2", "QS", "TZ", "ZL" };
                return "W_DWJ_" + dwj[comboStep - 1];
            }

            string n = activeString + "_" + comboStep;
            if (!HasState(n)) { 
                if (HasState(n + "_1")) n += "_1";
                else if (HasState(n + "_1_1")) n += "_1_1";
            }
            return n;
        }

        private bool HasState(string s) { return animator.HasState(0, Animator.StringToHash("MeleeCombat." + s)); }

        private void ConfigureMoveDynamics(AttackData move)
        {
            move.damage = 35f + comboStep * 6f;
            move.lungeForce = 10f + comboStep * 1.5f;
            move.targetPushForce = 20f + comboStep * 4f;
            move.totalDuration = 0.55f;
            move.cancelWindowStart = 0.35f;
            move.vfxType = VFXType.Slash;

            if (activeString == "Basic") { move.lungeForce = 12f; move.totalDuration = 0.45f; move.cancelWindowStart = 0.25f; move.vfxType = VFXType.DefaultImpact; }
            if (activeString == "DWJ") { move.damage = 55f; move.lungeForce = 14f; move.shakeLevel = 2; move.vfxType = VFXType.FireImpact; }
            if (activeString == "W4" && comboStep == 2) { move.damage = 85f; move.lungeForce = 38f; move.shakeLevel = 2; move.totalDuration = 0.85f; move.cancelWindowStart = 0.6f; move.vfxType = VFXType.LightningImpact; }
            if (activeString == "W1" && comboStep == 5) { move.totalDuration = 1.9f; move.cancelWindowStart = 1.6f; move.vfxType = VFXType.FireImpact; }
            if (activeString == "W8") { move.targetUpwardForce = 19f; move.shakeLevel = 2; move.vfxType = VFXType.IncinerationCannon; }
            if (activeString == "W11") { 
                move.damage += 45f; 
                move.shakeLevel = 3; 
                move.vfxType = (comboStep == 5) ? VFXType.BigExplosion : VFXType.IncinerationCannon; 
            }
            if (activeString == "W1" && comboStep == 7) { 
                move.damage = 160f; move.shakeLevel = 3; move.vfxType = VFXType.BigExplosion; 
                if (VFXManager.Instance != null) VFXManager.Instance.ExecuteImpactFrame(true);
                ResetCombo(); 
            }
}

        private void PerformAirAttack(bool isLight)
        {
            AttackData move = new AttackData();
            move.vfxType = VFXType.Slash;
            if (isLight) {
                if (!activeString.StartsWith("Air1")) { activeString = "Air1"; comboStep = 1; }
                else { comboStep++; if (comboStep > 2) comboStep = 1; }
                move.stateName = "MeleeCombat." + activeString + "_" + comboStep + "_1";
                move.lungeForce = 12f;
                move.damage = 30f;
                move.totalDuration = 0.45f;
                move.cancelWindowStart = 0.25f;
                Vector3 currentVel = controller.characterMovement.rb.linearVelocity;
                controller.characterMovement.rb.linearVelocity = new Vector3(currentVel.x, 3.5f, currentVel.z);
            } else {
                if (!activeString.StartsWith("Air2")) { activeString = "Air2"; comboStep = 1; }
                else { comboStep++; if (comboStep > 3) { ResetCombo(); PerformAirAttack(false); return; } }
                move.stateName = "MeleeCombat." + activeString + "_" + comboStep + "_1";
                move.lungeForce = 15f;
                move.damage = 45f;
                move.totalDuration = 0.55f;
                move.cancelWindowStart = 0.35f;
                if (comboStep == 3) { 
                    move.targetUpwardForce = -35f; move.lungeForce = 45f; move.shakeLevel = 2; move.totalDuration = 0.8f;
                    move.vfxType = VFXType.BigExplosion;
                    StartCoroutine(AirDivePhysics()); 
                } else {
                    Vector3 currentVel = controller.characterMovement.rb.linearVelocity;
                    controller.characterMovement.rb.linearVelocity = new Vector3(currentVel.x, 4.0f, currentVel.z);
                }
            }
            StartAttackRoutine(move);
        }

        private IEnumerator AirDivePhysics() {
            float t = 0; while (t < 0.5f) {
                controller.characterMovement.rb.linearVelocity = (transform.forward + Vector3.down * 4.2f).normalized * 55f;
                t += Time.deltaTime; yield return null;
            }
        }

        private void StartAttackRoutine(AttackData move)
        {
            if (isAttacking && !canCancelCurrent && currentMove != null && currentMove.stateName == move.stateName)
                return;
            StopAllCoroutines(); currentMove = move; lastAttackTime = Time.time;
            isAttacking = true; canCancelCurrent = false; controller.isMeleeAttacking = true;
            
            if (VFXManager.Instance != null) VFXManager.Instance.SetPlayerTrailsActive(true);
            
            controller.cameraController?.SetFOVState(CameraFOVState.Combat);
            currentMagnetTarget = FindMagnetTarget();
            rotationCoroutine = StartCoroutine(SmoothRotateToTarget());
            animator.applyRootMotion = false;
            string stateToPlay = move.stateName.Replace("MeleeCombat.", "");
            if (HasState(stateToPlay)) animator.CrossFadeInFixedTime(move.stateName, canCancelCurrent ? 0.12f : 0.05f);
            else Debug.LogWarning("Missing state: " + move.stateName);
            if (activeString == "W8") {
                Vector3 v = controller.characterMovement.rb.linearVelocity;
                controller.characterMovement.rb.linearVelocity = new Vector3(v.x, 15f, v.z);
            }
            StartCoroutine(AttackMovement(move));
            StartCoroutine(HandleAttackTiming(move));
        }

        private IEnumerator HandleAttackTiming(AttackData move)
        {
            float timer = 0;
            bool hitTriggered = false;
            float hitTime = move.totalDuration * 0.4f; // Auto-trigger hit at 40% duration

            yield return null; 

            while (timer < move.totalDuration) {
                timer += Time.deltaTime;
                
                if (!hitTriggered && timer >= hitTime)
                {
                    hitTriggered = true;
                    OnHitEvent();
                }

                if (timer >= move.cancelWindowStart) {
                    canCancelCurrent = true;
                    if (VFXManager.Instance != null) VFXManager.Instance.SetPlayerTrailsActive(false);
                }
                yield return null;
            }
            
            isAttacking = false; canCancelCurrent = false; controller.isMeleeAttacking = false;
            if (VFXManager.Instance != null) VFXManager.Instance.SetPlayerTrailsActive(false);
            yield return new WaitForSeconds(0.1f);
            if (!isAttacking) animator.applyRootMotion = true;
        }

        private IEnumerator SmoothRotateToTarget()
        {
            Vector3 tDir = transform.forward; Transform n = FindNearestEnemy(15f);
            if (n != null) { tDir = (n.position - transform.position); tDir.y = 0; }
            else if (input.movement.sqrMagnitude > 0.01f) {
                Vector3 f = controller.mainCamera.forward; Vector3 r = controller.mainCamera.right;
                f.y = 0; r.y = 0; tDir = (f.normalized * input.movement.y + r.normalized * input.movement.x);
            }
            if (tDir.sqrMagnitude > 0.01f) {
                Quaternion targetRot = Quaternion.LookRotation(tDir.normalized);
                float timer = 0; float rSpd = 0.06f;
                while (timer < rSpd) { transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, timer / rSpd); timer += Time.deltaTime; yield return null; }
                transform.rotation = targetRot;
            }
            rotationCoroutine = null;
        }

        private IEnumerator AttackMovement(AttackData move)
        {
            float t = 0; float d = 0.22f;
            while (t < d) {
                Vector3 cur = controller.characterMovement.rb.position;
                Vector3 fwd = transform.forward * move.lungeForce * Time.fixedDeltaTime;
                Vector3 mag = Vector3.zero;
                if (currentMagnetTarget != null) {
                    Vector3 toT = currentMagnetTarget.position - cur;
                    if (!controller.isGrounded) { if (toT.magnitude > 1.1f) mag = toT.normalized * 30f * Time.fixedDeltaTime; }
                    else { toT.y = 0; if (toT.magnitude > 1.4f) mag = toT.normalized * 25f * Time.fixedDeltaTime; }
                }
                Vector3 target = cur + fwd + mag;
                if (controller.isGrounded) {
                    if (Physics.Raycast(target + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3.0f, LayerMask.GetMask("Default", "Environment", "Ground")))
                        target.y = hit.point.y;
                }
                controller.characterMovement.rb.MovePosition(target);
                t += Time.fixedDeltaTime; yield return new WaitForFixedUpdate();
            }
        }

        private Transform FindMagnetTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, magnetismRange, enemyLayer);
            Transform best = null; float minA = magnetismAngle;
            foreach (var h in hits) {
                if (h.transform.root == transform.root) continue;
                Vector3 toH = (h.transform.position - transform.position).normalized;
                float a = Vector3.Angle(transform.forward, toH);
                if (a < minA) { minA = a; best = h.transform; }
            }
            return best;
        }

        private Transform FindNearestEnemy(float range)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, range, enemyLayer);
            Transform best = null; float minDist = float.MaxValue;
            int mask = LayerMask.GetMask("Default", "Environment", "Ground");
            foreach (var h in hits) {
                float d = Vector3.Distance(transform.position, h.transform.position);
                Vector3 origin = transform.position + Vector3.up * 1.2f;
                Vector3 target = h.transform.position + Vector3.up * 1.2f;
                if (!Physics.Raycast(origin, (target - origin).normalized, d, mask))
                    if (d < minDist) { minDist = d; best = h.transform; }
            }
            return best;
        }

        public void OnHitEvent()
        {
            if (currentMove == null) return;
            Vector3 hitPos = transform.position + transform.forward * 1.6f + Vector3.up * 1.1f;
            Collider[] targets = Physics.OverlapSphere(hitPos, hitRange, enemyLayer);
            bool landed = false;
            foreach (var t in targets) {
                if (t.transform.root == transform.root) continue;
                Vector3 toT = (t.transform.position - transform.position).normalized;
                if (Vector3.Angle(transform.forward, toT) > hitAngle) continue;
                var dummy = t.GetComponentInParent<Climbing.AI.DummyEnemy>();
                if (dummy != null) {
                    dummy.TakeDamage(currentMove.damage); landed = true;
                    if (VFXManager.Instance != null)
                        VFXManager.Instance.PlayEffect(currentMove.vfxType, t.bounds.center, transform.rotation, 1f, currentMove.shakeLevel);
                }
                Rigidbody rb = t.GetComponentInParent<Rigidbody>();
                if (rb != null) {
                    rb.isKinematic = false;
                    rb.AddForce(transform.forward * currentMove.targetPushForce + Vector3.up * currentMove.targetUpwardForce, ForceMode.Impulse);
                    rb.AddTorque(Random.insideUnitSphere * 20f, ForceMode.Impulse);
                }
            }
            if (landed) StartCoroutine(ImpactFreeze(0.08f));
        }

        private IEnumerator ImpactFreeze(float duration)
        {
            float old = animator.speed;
            animator.speed = 0.02f;
            yield return new WaitForSecondsRealtime(duration);
            if (animator != null) animator.speed = old;
        }

        private IEnumerator Hitstop(float d)
{
            if (animator == null) yield break;
            float s = animator.speed; animator.speed = 0.01f;
            yield return new WaitForSecondsRealtime(d);
            if (animator != null) animator.speed = s;
        }

        public void OnLaunchEvent() {
            AttackData l = new AttackData { damage = 50f, targetUpwardForce = 22f, shakeLevel = 2, vfxType = VFXType.FireImpact };
            currentMove = l; OnHitEvent();
        }
}
}