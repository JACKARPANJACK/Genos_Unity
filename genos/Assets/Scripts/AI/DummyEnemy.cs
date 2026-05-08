using UnityEngine;
using UnityEngine.AI; // Added for NavMeshAgent
using System.Collections;

namespace Climbing.AI
{
public class DummyEnemy : MonoBehaviour
    {
        public float health = 100f;
        public float detectionRadius = 15f;
        public float attackRadius = 2f;
        public float updatePathInterval = 0.2f;

        private Rigidbody rb;
        private NavMeshAgent agent;
        private Transform playerTransform;
        private float lastPathUpdateTime;

        public enum EnemyState { Idle, Chase, Attack }
        public EnemyState currentState = EnemyState.Idle;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            
            // Set tag for detection
            gameObject.tag = "Enemy";
            gameObject.layer = LayerMask.NameToLayer("Default"); // Ensure layer is suitable

            agent = GetComponent<NavMeshAgent>();
            if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();

            // If using physics with NavMeshAgent, we usually want it kinematic
            rb.isKinematic = true;
        }

        void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        void Update()
        {
            if (playerTransform == null || !agent.enabled) return;

            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // State Machine
            switch (currentState)
            {
                case EnemyState.Idle:
                    if (distanceToPlayer <= detectionRadius)
                    {
                        currentState = EnemyState.Chase;
                    }
                    break;
                case EnemyState.Chase:
                    if (distanceToPlayer > detectionRadius * 1.5f) // Lose interest
                    {
                        currentState = EnemyState.Idle;
                        agent.ResetPath();
                    }
                    else if (distanceToPlayer <= attackRadius)
                    {
                        currentState = EnemyState.Attack;
                        agent.ResetPath(); // Stop moving to attack
                    }
                    else
                    {
                        FollowPlayer();
                    }
                    break;
                case EnemyState.Attack:
                    if (distanceToPlayer > attackRadius)
                    {
                        currentState = EnemyState.Chase;
                    }
                    else
                    {
                        // Perform Attack logic here (face player, etc.)
                        FaceTarget(playerTransform.position);
                    }
                    break;
            }
        }

        private void FollowPlayer()
        {
            if (Time.time - lastPathUpdateTime > updatePathInterval)
            {
                agent.SetDestination(playerTransform.position);
                lastPathUpdateTime = Time.time;
            }
        }

        private void FaceTarget(Vector3 targetPos)
        {
            Vector3 direction = (targetPos - transform.position).normalized;
            direction.y = 0; // Keep horizontal only
            if (direction != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
            }
        }

        public bool IsFinisherReady => health > 0 && health < 40f; // Threshold for finishers
        private bool isBeingFinished = false;

        public void PlayFinisher(string finisherType)
        {
            if (isBeingFinished) return;
            isBeingFinished = true;
            
            if (agent != null) agent.enabled = false;
            if (rb != null) rb.isKinematic = true;

            // Play victim animation if animator exists
            var animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                // Mapping finisher type to victim state
                string victimState = finisherType switch
                {
                    "Finisher_01" => "Finisher_01_Victim",
                    "Finisher_02" => "Finisher_02_Victim",
                    "Finisher_Neck" => "Finisher_Neck_Victim",
                    _ => "Finisher_01_Victim"
                };
                animator.CrossFadeInFixedTime(victimState, 0.1f);
            }

            StartCoroutine(FinisherDeathRoutine());
        }

        private IEnumerator FinisherDeathRoutine()
        {
            yield return new WaitForSeconds(2.5f); // Duration of finisher
            Die();
        }

        public void TakeDamage(float amount)
{
            health -= amount;

            // Hit Feedback: Visual Scale Pulse
            if (hitFeedbackCoroutine != null) StopCoroutine(hitFeedbackCoroutine);
            hitFeedbackCoroutine = StartCoroutine(HitFeedbackRoutine());

            // Hit Stun & Physics
            if (hitStunCoroutine != null) StopCoroutine(hitStunCoroutine);
            hitStunCoroutine = StartCoroutine(HitStunRoutine());

            if (health <= 0)
            {
                Die();
            }
            else
            {
                // React to getting hit, e.g., if idle, start chasing
                if (currentState == EnemyState.Idle && playerTransform != null)
                {
                    currentState = EnemyState.Chase;
                }
            }
        }

        private Coroutine hitFeedbackCoroutine;
        private Coroutine hitStunCoroutine;

        private IEnumerator HitStunRoutine()
        {
            if (agent != null) agent.enabled = false;
            if (rb != null) rb.isKinematic = false;

            yield return new WaitForSeconds(0.6f); // Time to fly back and react

            if (health > 0)
            {
                // Try to recover to NavMesh
                if (rb != null) rb.isKinematic = true;
                if (agent != null) agent.enabled = true;
            }
        }

        private IEnumerator HitFeedbackRoutine()
{
            Vector3 originalScale = transform.localScale;
            transform.localScale = originalScale * 1.15f;
            float t = 0;
            while (t < 0.2f)
            {
                transform.localScale = Vector3.Lerp(originalScale * 1.15f, originalScale, t / 0.2f);
                t += Time.deltaTime;
                yield return null;
            }
            transform.localScale = originalScale;
        }

        private void Die()
        {
            Debug.Log(gameObject.name + " defeated!");
            if (agent != null) agent.enabled = false;
            
            // Re-enable physics for "death" fly-away effect
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            
            this.enabled = false; 
            Destroy(gameObject, 3f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRadius);
        }
    }
}
