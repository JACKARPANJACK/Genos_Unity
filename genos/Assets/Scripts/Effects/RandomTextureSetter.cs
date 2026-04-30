using UnityEngine;

namespace Climbing.Effects
{
    public class RandomTextureSetter : MonoBehaviour
    {
        public Texture2D[] possibleTextures;
        public string texturePropertyName = "_BaseMap";

        private void Awake()
        {
            if (possibleTextures == null || possibleTextures.Length == 0) return;
            
            Texture2D selected = possibleTextures[Random.Range(0, possibleTextures.Length)];
            
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                // Create a unique material instance so we don't change the shared asset
                renderer.material.SetTexture(texturePropertyName, selected);
            }
            
            var ps = GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var psRenderer = GetComponent<ParticleSystemRenderer>();
                if (psRenderer != null)
                {
                    psRenderer.material.SetTexture(texturePropertyName, selected);
                }
            }
        }
    }
}
