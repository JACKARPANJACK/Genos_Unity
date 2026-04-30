using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

namespace Climbing.Effects
{
    public class DecalFadeOut : MonoBehaviour
    {
        public float lifetime = 2.0f;
        public float fadeDuration = 0.5f;
        
        private DecalProjector projector;
        private MeshRenderer meshRenderer;
        private Material material;

        void Start()
        {
            projector = GetComponent<DecalProjector>();
            meshRenderer = GetComponent<MeshRenderer>();
            
            if (meshRenderer != null) material = meshRenderer.material;

            StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            yield return new WaitForSeconds(lifetime - fadeDuration);
            
            float elapsed = 0f;
            Color startColor = material != null ? material.GetColor("_Color") : Color.white;
            float startFadeFactor = projector != null ? projector.fadeFactor : 1f;
            
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                
                if (projector != null) projector.fadeFactor = Mathf.Lerp(startFadeFactor, 0f, t);
                if (material != null) material.SetColor("_Color", Color.Lerp(startColor, new Color(0,0,0,0), t));
                
                yield return null;
            }
            
            Destroy(gameObject);
        }
    }
}
