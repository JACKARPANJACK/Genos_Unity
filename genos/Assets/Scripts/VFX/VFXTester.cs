using UnityEngine;

public class VFXTester : MonoBehaviour
{
    public GameObject vfxPrefab;
    public float interval = 3f;
    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0;
            if (vfxPrefab != null)
            {
                Instantiate(vfxPrefab, transform.position, transform.rotation);
            }
        }
    }
}
