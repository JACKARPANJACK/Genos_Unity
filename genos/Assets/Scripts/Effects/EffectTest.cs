using UnityEngine;
using UnityEngine.InputSystem;

namespace Climbing.Effects
{
    public class EffectTest : MonoBehaviour
    {
        public GameObject firePrefab;
        public GameObject decalPrefab;
        public LayerMask groundLayer;

        void Update()
        {
            if (Keyboard.current == null) return;

            // Press 1 to attach fire to right hand
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                EffectManager.SpawnEffectAtBone(firePrefab, BodyPart.RightHand);
                Debug.Log("Spawned effect on Right Hand");
            }

            // Press 2 to attach fire to head
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                EffectManager.SpawnEffectAtBone(firePrefab, BodyPart.Head, new Vector3(0, 0.2f, 0));
                Debug.Log("Spawned effect on Head");
            }

            // Press 3 to spawn a decal on ground via raycast
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
                {
                    EffectManager.SpawnDecal(decalPrefab, hit, true, 5f);
                    Debug.Log("Spawned decal on surface");
                }
            }
        }
    }
}
