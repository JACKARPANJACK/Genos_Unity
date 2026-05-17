using UnityEngine;

using Climbing.Combat;

public class FireballProjectile : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 20f;
    public float lifeTime = 5f;
    public float scale = 1.0f;
    public float damage = 40f;
    public int shakeLevel = 1;
    public bool isHeavy = false;
    public GameObject impactEffect;

    private Rigidbody rb;

    void Start()
    {
        transform.localScale = Vector3.one * scale;
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = transform.forward * speed;
            rb.useGravity = false;
        }

        // Ignore player collision
        var player = GameObject.Find("PlayerModel");
        if (player != null)
        {
            var pCol = player.GetComponent<Collider>();
            var mCol = GetComponentInChildren<Collider>();
            if (pCol != null && mCol != null) Physics.IgnoreCollision(pCol, mCol);
        }

        Destroy(gameObject, lifeTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (impactEffect != null)
        {
            GameObject fx = Instantiate(impactEffect, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * scale * 1.5f;
            Destroy(fx, 2f);
        }

        // Trigger Screen Effects for heavy projectiles
        if (VFXManager.Instance != null)
        {
            if (isHeavy)
            {
                VFXManager.Instance.ExecuteImpactFrame(true);
                VFXManager.Instance.ExecuteRadialBlurSpike();
            }
            
            // Screen Shake
            var cam = Object.FindAnyObjectByType<Climbing.CameraController>();
            if (cam != null)
            {
                if (shakeLevel == 1) cam.ShakeLight();
                else if (shakeLevel == 2) cam.ShakeMedium();
                else if (shakeLevel >= 3) cam.ShakeHeavy();
            }
        }

        var dummy = collision.gameObject.GetComponentInParent<Climbing.AI.DummyEnemy>();
        if (dummy != null)
        {
            dummy.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}
