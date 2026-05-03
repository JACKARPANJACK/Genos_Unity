using UnityEngine;
using Climbing;
using System.Collections;
using System.Collections.Generic;

namespace Climbing.Abilities
{
    public class WireAbility : MonoBehaviour
    {
        public GameObject wirePrefab;
        public GameObject trapPrefab; 
        public Transform socket;
        public float pullForce = 50f;
        public float trapDuration = 12f;

        private WireProjectile activeWire;
        private Vector3 trapPointA;
        private bool hasPointA = false;
        private ThirdPersonController controller;
        private InputCharacterController input;
        private bool isPulling = false;

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            input = GetComponent<InputCharacterController>();
            if (socket == null) socket = controller.leftHandSocket;
        }

        public void UseAbility()
        {
            if (isPulling)
            {
                isPulling = false;
                if (activeWire != null) activeWire.Detach();
                activeWire = null;
                return;
            }

            FireWire();
        }

        private void FireWire()
        {
            if (activeWire != null) activeWire.Detach();

            // SUPPRESS MOVEMENT
            if (controller != null)
            {
                controller.allowMovement = false;
                controller.characterMovement.DisableFeetIK();
                controller.characterAnimation.animator.SetFloat("Velocity", 0f);
                StartCoroutine(RestoreMovementAfterWire());
            }

            Vector3 targetPoint;
            Camera cam = controller.mainCamera.GetComponent<Camera>();
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int mask = ~(LayerMask.GetMask("Player") | LayerMask.GetMask("Ignore Raycast"));
            
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, mask, QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;
            }
            else
            {
                targetPoint = ray.GetPoint(100f);
            }

            GameObject wireObj = Instantiate(wirePrefab, socket.position, Quaternion.LookRotation((targetPoint - socket.position).normalized));
            activeWire = wireObj.GetComponent<WireProjectile>();
            activeWire.Launch(transform, socket, OnWireImpact);
        }

        private IEnumerator RestoreMovementAfterWire()
        {
            yield return new WaitForSeconds(0.4f);
            if (!isPulling)
            {
                controller.allowMovement = true;
                controller.characterMovement.EnableFeetIK();
            }
        }

        private void OnWireImpact(Transform target, Vector3 point)
        {
            // If we hit an enemy, reel them in
            if (target != null && (target.CompareTag("Enemy") || target.GetComponent<Rigidbody>() != null))
            {
                if (!hasPointA)
                {
                    StartCoroutine(PullTarget(target));
                }
                else
                {
                    // If we already had a point A, create a trap involving the enemy (stuck to point B)
                    CreateTrap(trapPointA, point);
                    hasPointA = false;
                    if (activeWire != null) activeWire.Detach();
                    activeWire = null;
                }
            }
            else
            {
                // Hit a surface
                if (!hasPointA)
                {
                    trapPointA = point;
                    hasPointA = true;
                    // Keep wire attached to the surface visually if needed? 
                    // Current WireProjectile destroys itself unless it's attached.
                }
                else
                {
                    CreateTrap(trapPointA, point);
                    hasPointA = false;
                    if (activeWire != null) activeWire.Detach();
                    activeWire = null;
                }
            }
        }

        private void CreateTrap(Vector3 a, Vector3 b)
        {
            GameObject trap = new GameObject("WireTrap_Laser");
            LineRenderer lr = trap.AddComponent<LineRenderer>();
            lr.startWidth = 0.08f;
            lr.endWidth = 0.08f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = Color.cyan; // Laser feel
            lr.endColor = Color.cyan;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);

            BoxCollider col = trap.AddComponent<BoxCollider>();
            col.isTrigger = true;
            Vector3 mid = (a + b) / 2f;
            trap.transform.position = mid;
            trap.transform.LookAt(b);
            col.size = new Vector3(0.2f, 0.2f, Vector3.Distance(a, b));

            trap.AddComponent<WireTrapLogic>().duration = trapDuration;
        }

        private IEnumerator PullTarget(Transform target)
        {
            isPulling = true;
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            
            while (activeWire != null && isPulling)
            {
                if (target == null) break;

                Vector3 direction = (transform.position - target.position).normalized;
                float distance = Vector3.Distance(transform.position, target.position);

                if (distance < 2.5f)
                {
                    isPulling = false;
                    break;
                }

                if (targetRb != null)
                {
                    targetRb.linearVelocity = direction * pullForce;
                }
                else
                {
                    target.position = Vector3.MoveTowards(target.position, transform.position, pullForce * Time.deltaTime);
                }

                yield return null;
            }

            if (activeWire != null)
            {
                activeWire.Detach();
                activeWire = null;
            }
            isPulling = false;
        }
    }

    public class WireTrapLogic : MonoBehaviour
    {
        public float duration = 12f;
        void Start() { Destroy(gameObject, duration); }
        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Enemy"))
            {
                Rigidbody rb = other.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.AddForce(Vector3.up * 10f, ForceMode.Impulse); // Stun pop
                }
            }
        }
    }
}
