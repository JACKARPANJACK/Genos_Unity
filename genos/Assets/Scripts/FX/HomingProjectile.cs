using UnityEngine;

public class HomingProjectile : MonoBehaviour
{
    public Transform target;
    public float speed = 150f;
    public float turnSpeed = 5f;
    public float targetOffset = 1.0f; // Target center mass

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (target == null || rb == null) return;

        Vector3 targetPos = target.position + Vector3.up * targetOffset;
        Vector3 direction = (targetPos - transform.position).normalized;
        
        // Smoothly rotate velocity towards target
        Vector3 newVelocity = Vector3.RotateTowards(rb.linearVelocity, direction * speed, turnSpeed * Time.fixedDeltaTime, 0f);
        rb.linearVelocity = newVelocity;
        
        // Align forward with velocity
        if (rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }
    }
}
