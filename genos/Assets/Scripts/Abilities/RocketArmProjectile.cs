using UnityEngine;
using System.Collections.Generic;

namespace Climbing.Abilities
{
    public class RocketArmProjectile : MonoBehaviour
    {
        public float speed = 100f;
        public float returnSpeed = 130f;
        public float maxDistance = 100f;
        public float damage = 25f;
        public float impactForce = 40f;
        public GameObject impactVFX;
        public GameObject trailVFX;
        public GameObject explosionVFX;
        public GameObject hunterFireVFX;
        public LineRenderer wireRenderer;

        private Vector3 startPosition;
        private Transform owner;
        private Transform socket;
        private bool isReturning = false;
        private bool isLaunched = false;
        private System.Action<Transform> onReturn;
        private GameObject activeTrail;

        private RocketArmType armType = RocketArmType.Normal;
        private int ricochetCount = 0;
        private const int MaxRicochets = 20; // Increased for heavy crowd control
        private Transform homingTarget;
        private bool isGrabbing = false;
        private Transform grabbedObject;
        private bool showWire = false;

        private bool isHunterDragging = false;
        private float hunterTimer = 0f;
        private float hunterAngle = 0f;
        private Vector3 hunterCenter;
        private const float HunterDuration = 2.5f; // Gives enough time for 2 revolutions
        private const float HunterRadius = 12f; // Large arena drag
        private const float HunterStartHeight = 10f; // Starts elevated
        private GameObject activeHunterVFX;
        private float flightTime = 0f;

        public bool IsGrabbing => isGrabbing || isHunterDragging;

        public void SetupType(RocketArmType type, GameObject explosive, bool forceWire = false)
        {
            armType = type;
            explosionVFX = explosive;
            showWire = forceWire; 
        }

        public void Launch(Transform owner, Transform socket, float flightSpeed, System.Action<Transform> onReturn)
        {
            this.owner = owner;
            this.socket = socket;
            this.onReturn = onReturn;
            this.speed = flightSpeed;
            this.returnSpeed = flightSpeed * 1.3f;
            startPosition = transform.position;
            isLaunched = true;
            isReturning = false;
            transform.SetParent(null);

            if (trailVFX != null)
            {
                activeTrail = Instantiate(trailVFX, transform);
                activeTrail.transform.localPosition = Vector3.zero;
            }

            if (showWire && wireRenderer == null)
            {
                GameObject wireObj = new GameObject("ArmWire");
                wireRenderer = wireObj.AddComponent<LineRenderer>();
                wireRenderer.startWidth = 0.05f;
                wireRenderer.endWidth = 0.02f;
                wireRenderer.material = new Material(Shader.Find("Sprites/Default"));
                wireRenderer.startColor = Color.gray;
                wireRenderer.endColor = Color.black;
                wireRenderer.positionCount = 2;
            }

            if (armType == RocketArmType.Homing) FindHomingTarget();
        }

        void Update()
        {
            if (!isLaunched) return;

            flightTime += Time.deltaTime;
            float maxFlight = (armType == RocketArmType.Ricochet) ? 4.0f : 2.0f;
            float hardReset = (armType == RocketArmType.Ricochet) ? 10.0f : 6.0f;

            if (flightTime > maxFlight && !isReturning && !isHunterDragging) StartReturn();
            if (flightTime > hardReset) { Cleanup(); return; } // hard reset if stuck

            // Manual detach
            if (IsGrabbing && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cleanup();
                return;
            }

            if (isHunterDragging) UpdateHunterDragging();
            else if (isReturning) ReturnToSocket();
            else MoveForward();

            UpdateWire();
        }

        private void UpdateHunterDragging()
        {
            hunterTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(hunterTimer / HunterDuration);

            // 720 degrees for two complete revolutions
            hunterAngle = progress * 720f; 
            float rad = hunterAngle * Mathf.Deg2Rad;

            // Gradually come down from elevated height to 0
            float currentHeightOffset = Mathf.Lerp(HunterStartHeight, 0f, progress);

            Vector3 offset = new Vector3(Mathf.Cos(rad) * HunterRadius, currentHeightOffset, Mathf.Sin(rad) * HunterRadius);
            transform.position = hunterCenter + offset;

            // Calculate a point slightly ahead to look at for correct orientation
            float nextRad = (hunterAngle + 15f) * Mathf.Deg2Rad;
            float nextHeightOffset = Mathf.Lerp(HunterStartHeight, 0f, Mathf.Clamp01((hunterTimer + 0.1f) / HunterDuration));
            Vector3 lookOffset = new Vector3(Mathf.Cos(nextRad) * HunterRadius, nextHeightOffset, Mathf.Sin(nextRad) * HunterRadius);
            transform.LookAt(hunterCenter + lookOffset);

            if (grabbedObject != null) grabbedObject.position = transform.position;

            if (hunterTimer >= HunterDuration)
            {
                isHunterDragging = false;
                Explode();
                if (activeHunterVFX != null) Destroy(activeHunterVFX);
                StartReturn();
            }
        }

        public void ManualDetach()
        {
            if (IsGrabbing) Cleanup();
            else StartReturn();
        }

        private void UpdateWire()
        {
            if (showWire && wireRenderer != null && socket != null)
            {
                wireRenderer.SetPosition(0, socket.position);
                wireRenderer.SetPosition(1, transform.position);
            }
        }

        private void MoveForward()
        {
            if (armType == RocketArmType.Homing)
            {
                if (homingTarget == null) FindHomingTarget();
                if (homingTarget != null)
                {
                    Vector3 dir = (homingTarget.position - transform.position).normalized;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 60f); // Fast tracking
                }
            }

            float currentSpeed = speed + (ricochetCount * 25f);
            transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);

            if (Vector3.Distance(startPosition, transform.position) >= maxDistance) StartReturn();
        }

        private void ReturnToSocket()
        {
            if (socket == null) { Cleanup(); return; }

            if (grabbedObject != null)
            {
                Rigidbody targetRb = grabbedObject.GetComponent<Rigidbody>();
                bool isStatic = targetRb == null || grabbedObject.gameObject.isStatic || (targetRb.isKinematic && armType != RocketArmType.Hunter);
                bool grappleInput = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed;

                if (isStatic || (armType == RocketArmType.Grabber && grappleInput))
                {
                    Rigidbody ownerRb = owner.GetComponent<Rigidbody>();
                    if (ownerRb != null)
                    {
                        ownerRb.isKinematic = false;
                        Vector3 moveDir = (transform.position - owner.position).normalized;
                        ownerRb.linearVelocity = moveDir * returnSpeed * 1.5f;
                        if (Vector3.Distance(owner.position, transform.position) < 2.0f) { onReturn?.Invoke(null); Cleanup(); return; }
                    }
                    }
                    else
                    {
                    grabbedObject.position = transform.position;
                    transform.position = Vector3.MoveTowards(transform.position, socket.position, returnSpeed * 1.5f * Time.deltaTime);
                    if (targetRb != null) 
                    {
                        targetRb.isKinematic = false;
                        targetRb.linearVelocity = (socket.position - transform.position).normalized * returnSpeed;
                    }
                    }
}
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, socket.position, returnSpeed * Time.deltaTime);
            }

            transform.LookAt(socket);
            if (Vector3.Distance(transform.position, socket.position) < 0.5f) 
            { 
                Transform tempGrab = grabbedObject;
                grabbedObject = null;
                if (onReturn != null)
                {
                    onReturn.Invoke(tempGrab);
                    onReturn = null;
                }
                Cleanup(); 
            }
        }

        private void Cleanup()
        {
            if (onReturn != null)
            {
                onReturn.Invoke(null);
                onReturn = null;
            }
            if (activeTrail != null) { activeTrail.transform.SetParent(null); Destroy(activeTrail, 1.5f); }
            if (activeHunterVFX != null) Destroy(activeHunterVFX);
            if (wireRenderer != null) Destroy(wireRenderer.gameObject);
            if (grabbedObject != null) { Rigidbody rb = grabbedObject.GetComponent<Rigidbody>(); if (rb != null) { rb.isKinematic = false; rb.AddForce(Vector3.up * 5f, ForceMode.Impulse); } }
            Destroy(gameObject);
        }

        private void StartReturn() { isReturning = true; homingTarget = null; }

        private void OnTriggerEnter(Collider other)
        {
            if (isReturning || !isLaunched || isHunterDragging) return;
            if (other.transform.root == owner.root) return;
            HandleImpact(other);
        }

        private void HandleImpact(Collider other)
        {
            if (armType == RocketArmType.Explosive) { Explode(); StartReturn(); return; }

            if (impactVFX != null) Instantiate(impactVFX, transform.position, Quaternion.identity);

            Climbing.AI.DummyEnemy dummy = other.GetComponent<Climbing.AI.DummyEnemy>();
            if (dummy != null) dummy.TakeDamage(damage);

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && armType != RocketArmType.Hunter && armType != RocketArmType.Grabber) 
                rb.AddForce(transform.forward * impactForce, ForceMode.Impulse);

            if (armType == RocketArmType.Ricochet && ricochetCount < MaxRicochets) 
            { 
                ricochetCount++; 
                flightTime = 0f; // refresh flight time on bounce

                if (!FindNextRicochetTarget(other.transform)) 
                { 
                    // If no enemy found, artificially bounce off the collided surface to keep hunting!
                    Vector3 estimatedNormal = (transform.position - other.bounds.center).normalized;
                    if (estimatedNormal == Vector3.zero) estimatedNormal = Random.onUnitSphere;

                    Vector3 reflectDir = Vector3.Reflect(transform.forward, estimatedNormal);
                    transform.rotation = Quaternion.LookRotation(reflectDir);
                } 
                return; 
            }

            if (armType == RocketArmType.Hunter && !isHunterDragging) 
            { 
                if (other.CompareTag("Enemy") || dummy != null)
                {
                    StartHunterDragging(other.transform); 
                    return; 
                }
            }

            if (armType == RocketArmType.Grabber && !isGrabbing) 
            { 
                isGrabbing = true; 
                grabbedObject = other.transform; 
                UnityEngine.AI.NavMeshAgent agent = grabbedObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = false;
                if (rb != null) rb.isKinematic = false;
            }

            StartReturn();
        }

        private void StartHunterDragging(Transform target)
        {
            isHunterDragging = true;
            grabbedObject = target;
            hunterTimer = 0f;
            hunterAngle = 0f;
            
            UnityEngine.AI.NavMeshAgent agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            if (targetRb != null) targetRb.isKinematic = true;

            Vector3 toTarget = transform.position - owner.position;
            toTarget.y = 0; // Flatten circle to XZ plane
            if (toTarget.sqrMagnitude < 0.01f) toTarget = owner.forward;
            toTarget.Normalize();

            hunterCenter = transform.position - toTarget * HunterRadius;
            hunterCenter.y = owner.position.y; // Ensure center starts horizontally from ground level

            if (hunterFireVFX != null) { activeHunterVFX = Instantiate(hunterFireVFX, transform); activeHunterVFX.transform.localPosition = Vector3.zero; }
        }

        private void Explode()
        {
            if (explosionVFX != null) Instantiate(explosionVFX, transform.position, Quaternion.identity);
            Collider[] colliders = Physics.OverlapSphere(transform.position, 10f);
            foreach (var col in colliders) { if (col.transform.root == owner.root) continue; Rigidbody rb = col.GetComponent<Rigidbody>(); if (rb != null) rb.AddExplosionForce(2000f, transform.position, 10f); }
        }

        private void FindHomingTarget()
        {
            Collider[] targets = Physics.OverlapSphere(transform.position, 40f);
            float bestDist = float.MaxValue;
            foreach (var t in targets) { if (t.CompareTag("Enemy")) { float dist = Vector3.Distance(transform.position, t.transform.position); if (dist < bestDist) { bestDist = dist; homingTarget = t.transform; } } }
        }

        private bool FindNextRicochetTarget(Transform current)
        {
            Collider[] targets = Physics.OverlapSphere(transform.position, 40f);
            List<Transform> validTargets = new List<Transform>();
            foreach (var t in targets) { if (t.transform == current || t.transform.root == owner.root) continue; if (t.CompareTag("Enemy") || t.GetComponent<Rigidbody>() != null) validTargets.Add(t.transform); }
            if (validTargets.Count > 0) { Transform next = validTargets[Random.Range(0, validTargets.Count)]; Vector3 randomOffset = Random.insideUnitSphere * 2.5f; transform.rotation = Quaternion.LookRotation((next.position + randomOffset - transform.position).normalized); return true; }
            return false;
        }
    }
}
