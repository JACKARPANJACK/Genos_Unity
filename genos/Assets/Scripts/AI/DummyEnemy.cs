using UnityEngine;
using UnityEngine.AI; // Added for NavMeshAgent

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

        public void TakeDamage(float amount)
        {
            health -= amount;
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

        private void Die()
        {
            Debug.Log(gameObject.name + " defeated!");
            // Detach and fly away, don't reset immediately
            if (agent != null) agent.enabled = false;
            
            // Allow them to look like they died
            Collider col = GetComponent<Collider>();
            if (rb != null && col != null)
            {
                // Optionally disable specific scripts or components
                this.enabled = false; 
            }
            
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
