using UnityEngine;

public class DemoMovement : MonoBehaviour
{
    public float speed = 10f;
    public float range = 20f;
    public bool active = true;

    private Vector3 startPos;
    private float timer;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        if (!active) return;

        timer += Time.deltaTime;
        float x = Mathf.PingPong(timer * speed, range) - (range / 2f);
        transform.position = startPos + transform.right * x;
    }
}
