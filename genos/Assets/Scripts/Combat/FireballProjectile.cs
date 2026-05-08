using UnityEngine;

public class FireballProjectile : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 20f;
    public float lifeTime = 5f;
    public GameObject impactEffect;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * speed;
        }
        Destroy(gameObject, lifeTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (impactEffect != null)
        {
            Instantiate(impactEffect, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
