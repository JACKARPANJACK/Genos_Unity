using UnityEngine;

namespace Climbing.Effects
{
    public static class EffectManager
    {
        private static PlayerEffectAttacher _playerAttacher;

        public static void RegisterPlayer(PlayerEffectAttacher attacher)
        {
            _playerAttacher = attacher;
        }

        public static GameObject SpawnEffectAtBone(GameObject prefab, BodyPart part, Vector3 localOffset = default, Quaternion localRotation = default, bool attach = true)
        {
            if (_playerAttacher == null)
            {
                _playerAttacher = Object.FindAnyObjectByType<PlayerEffectAttacher>();
                if (_playerAttacher == null)
                {
                    Debug.LogWarning("No PlayerEffectAttacher found in scene.");
                    return null;
                }
            }

            Transform bone = _playerAttacher.GetBone(part);
            if (bone == null) return null;

            GameObject effect;
            if (attach)
            {
                effect = Object.Instantiate(prefab, bone);
                effect.transform.localPosition = localOffset;
                effect.transform.localRotation = localRotation == default ? Quaternion.identity : localRotation;
            }
            else
            {
                effect = Object.Instantiate(prefab, bone.position + bone.TransformDirection(localOffset), bone.rotation * localRotation);
            }

            return effect;
        }

        /// <summary>
        /// Spawns a decal at a specific world position and rotation.
        /// </summary>
        public static GameObject SpawnDecal(GameObject decalPrefab, Vector3 position, Quaternion rotation, Transform parent = null, float duration = -1f)
        {
            GameObject decal = Object.Instantiate(decalPrefab, position, rotation);
            if (parent != null)
            {
                decal.transform.SetParent(parent);
            }
            
            if (duration > 0)
            {
                Object.Destroy(decal, duration);
            }
            
            return decal;
        }
        
        /// <summary>
        /// Spawns a decal at a RaycastHit location, oriented along the normal.
        /// </summary>
        public static GameObject SpawnDecal(GameObject decalPrefab, RaycastHit hit, bool attachToHitObject = true, float duration = -1f)
        {
            // Offset slightly from surface to prevent z-fighting if not using URP Decal Projector
            Vector3 pos = hit.point + hit.normal * 0.01f;
            Quaternion rot = Quaternion.LookRotation(-hit.normal); 
            
            return SpawnDecal(decalPrefab, pos, rot, attachToHitObject ? hit.transform : null, duration);
        }

        /// <summary>
        /// Simple utility to play a particle system and destroy it after completion.
        /// </summary>
        public static void PlayOneShotVFX(GameObject vfxPrefab, Vector3 position, Quaternion rotation)
        {
            GameObject instance = Object.Instantiate(vfxPrefab, position, rotation);
            ParticleSystem ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                Object.Destroy(instance, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            else
            {
                Object.Destroy(instance, 5f); // Fallback
            }
        }
    }
}
