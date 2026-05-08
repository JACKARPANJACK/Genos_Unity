using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace Climbing.Combat
{
    public enum VFXType
    {
        None,
        DefaultImpact,
        Slash,
        FireImpact,
        LightningImpact,
        BigExplosion,
        IncinerationCannon,
        GroundCrack,
        ElectricSpark
    }

    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("VFX Prefabs")]
        public GameObject defaultImpactPrefab;
        public GameObject slashPrefab;
        public GameObject fireImpactPrefab;
        public GameObject lightningImpactPrefab;
        public GameObject bigExplosionPrefab;
        public GameObject incinerationCannonPrefab;
        public GameObject groundCrackPrefab;
        public GameObject electricSparkPrefab;

        [Header("Screen Effects")]
        public Volume globalVolume;
        private ColorAdjustments colorAdjustments;
        private ChromaticAberration chromaticAberration;
        private LensDistortion lensDistortion;

        [Header("Anime UI")]
        public Image impactFlashImage;
        public Image actionLinesImage;

        // Player specific effects (Booster trails/Particles)
        private TrailRenderer[] playerTrails;
        private ParticleSystem[] playerBoosterParticles;
        private CameraController cameraController;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            InitializePostProcessing();
            cameraController = Object.FindAnyObjectByType<CameraController>();
        }

        private void InitializePostProcessing()
        {
            if (globalVolume == null) globalVolume = FindFirstObjectByType<Volume>();
            if (globalVolume != null && globalVolume.sharedProfile != null)
            {
                globalVolume.sharedProfile.TryGet(out colorAdjustments);
                globalVolume.sharedProfile.TryGet(out chromaticAberration);
                globalVolume.sharedProfile.TryGet(out lensDistortion);
            }

            if (impactFlashImage != null) impactFlashImage.gameObject.SetActive(false);
            if (actionLinesImage != null) actionLinesImage.gameObject.SetActive(false);
        }

        /// <summary>Registers the current player's trails and boosters to the global manager.</summary>
        public void RegisterPlayerVFX(TrailRenderer[] trails, ParticleSystem[] boosters)
        {
            playerTrails = trails;
            playerBoosterParticles = boosters;
            SetPlayerTrailsActive(false);
        }

        public void PlayEffect(VFXType type, Vector3 position, Quaternion rotation, float scale = 1f, int shakeLevel = 0)
        {
            GameObject prefab = GetPrefab(type);
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, position, rotation);
                instance.transform.localScale = Vector3.one * scale;
                
                // Cleanup handled by ParticleSystem.main.stopAction = Destroy (set in prefab fix)
            }

            HandleContextualEffects(type, position, shakeLevel);
        }

        public void SetPlayerTrailsActive(bool active)
        {
            if (playerTrails != null) foreach (var t in playerTrails) if (t != null) t.emitting = active;
            if (playerBoosterParticles != null) foreach (var p in playerBoosterParticles)
            {
                if (p != null) { if (active) p.Play(); else p.Stop(); }
            }
        }

        private GameObject GetPrefab(VFXType type)
        {
            switch (type)
            {
                case VFXType.DefaultImpact: return defaultImpactPrefab;
                case VFXType.Slash: return slashPrefab;
                case VFXType.FireImpact: return fireImpactPrefab;
                case VFXType.LightningImpact: return lightningImpactPrefab;
                case VFXType.BigExplosion: return bigExplosionPrefab;
                case VFXType.IncinerationCannon: return incinerationCannonPrefab;
                case VFXType.GroundCrack: return groundCrackPrefab;
                case VFXType.ElectricSpark: return electricSparkPrefab;
                default: return null;
            }
        }

        private void HandleContextualEffects(VFXType type, Vector3 position, int shakeLevel)
        {
            if (cameraController != null)
            {
                if (shakeLevel == 1) cameraController.ShakeLight();
                else if (shakeLevel == 2) cameraController.ShakeMedium();
                else if (shakeLevel == 3) cameraController.ShakeHeavy();
            }

            if (type == VFXType.BigExplosion || type == VFXType.IncinerationCannon)
            {
                ExecuteImpactFrame(true);
                ExecuteActionLines();
                ExecuteRadialBlurSpike();
                SpawnGroundCrack(position);
                StartCoroutine(Hitstop(0.12f));
            }
            else if (type == VFXType.FireImpact || type == VFXType.LightningImpact)
            {
                ExecuteImpactFrame(false);
                SpawnGroundCrack(position);
            }
        }

        public void ExecuteFinisherFlourish()
        {
            StartCoroutine(FinisherSlowMoRoutine());
        }

        private IEnumerator FinisherSlowMoRoutine()
        {
            Time.timeScale = 0.15f;
            ExecuteImpactFrame(true);
            ExecuteActionLines();
            ExecuteRadialBlurSpike();
            yield return new WaitForSecondsRealtime(0.4f);
            Time.timeScale = 1.0f;
        }

        public void ExecuteImpactFrame(bool isHeavy)
{
            if (impactFlashImage != null) StartCoroutine(ImpactFlashRoutine(isHeavy));
            StartCoroutine(PostProcessSpikeRoutine(isHeavy));
        }

        private IEnumerator ImpactFlashRoutine(bool isHeavy)
        {
            impactFlashImage.gameObject.SetActive(true);
            impactFlashImage.color = Color.white;
            yield return new WaitForSecondsRealtime(isHeavy ? 0.08f : 0.05f);
            impactFlashImage.gameObject.SetActive(false);
        }

        private IEnumerator PostProcessSpikeRoutine(bool isHeavy)
        {
            if (colorAdjustments != null)
            {
                colorAdjustments.saturation.overrideState = true;
                colorAdjustments.contrast.overrideState = true;
                colorAdjustments.saturation.value = -100f;
                colorAdjustments.contrast.value = 100f;
            }
            if (chromaticAberration != null)
            {
                chromaticAberration.intensity.overrideState = true;
                chromaticAberration.intensity.value = isHeavy ? 1f : 0.6f;
            }
            yield return new WaitForSecondsRealtime(isHeavy ? 0.08f : 0.05f);
            if (colorAdjustments != null)
            {
                colorAdjustments.saturation.value = 20f;
                colorAdjustments.contrast.value = 15f;
            }
            if (chromaticAberration != null) chromaticAberration.intensity.value = 0.2f;
        }

        public void ExecuteRadialBlurSpike()
        {
            if (lensDistortion != null) StartCoroutine(RadialBlurRoutine());
        }

        private IEnumerator RadialBlurRoutine()
        {
            lensDistortion.intensity.overrideState = true;
            lensDistortion.intensity.value = -0.4f;
            yield return new WaitForSecondsRealtime(0.12f);
            lensDistortion.intensity.value = 0f;
        }

        public void ExecuteActionLines()
        {
            if (actionLinesImage != null) StartCoroutine(ActionLinesRoutine());
        }

        private IEnumerator ActionLinesRoutine()
        {
            actionLinesImage.gameObject.SetActive(true);
            float timer = 0, duration = 0.25f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                Color c = actionLinesImage.color;
                c.a = 1f - (timer / duration);
                actionLinesImage.color = c;
                yield return null;
            }
            actionLinesImage.gameObject.SetActive(false);
        }

        public void SpawnGroundCrack(Vector3 impactPoint)
        {
            if (groundCrackPrefab == null) return;
            if (Physics.Raycast(impactPoint + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 4f, LayerMask.GetMask("Default", "Environment", "Ground")))
            {
                GameObject crack = Instantiate(groundCrackPrefab, hit.point + hit.normal * 0.1f, Quaternion.LookRotation(-hit.normal));
                crack.transform.Rotate(0, 0, Random.Range(0, 360));
            }
        }

        private IEnumerator Hitstop(float duration)
        {
            float original = Time.timeScale;
            Time.timeScale = 0.01f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = original;
        }
    }
}
