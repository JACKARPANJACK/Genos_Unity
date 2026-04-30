using UnityEngine;
using System.Collections;

namespace Climbing.Effects
{
    public class PlayerAfterImage : MonoBehaviour
    {
        [Header("Settings")]
        public float lifetime = 0.5f;
        public Color startColor = new Color(0, 0.5f, 1f, 0.8f);
        public Color endColor = new Color(0, 0.2f, 0.8f, 0f);
        
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material material;
        private float timeActive;

        public void Setup(Mesh mesh, Vector3 position, Quaternion rotation, Material baseMaterial, Color color)
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

            meshFilter.mesh = mesh;
            transform.position = position;
            transform.rotation = rotation;
            
            material = new Material(baseMaterial);
            material.color = color;
            meshRenderer.material = material;
            
            startColor = color;
            timeActive = 0;
            
            StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            while (timeActive < lifetime)
            {
                timeActive += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, timeActive / lifetime);
                Color c = Color.Lerp(startColor, endColor, timeActive / lifetime);
                material.color = c;
                yield return null;
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
