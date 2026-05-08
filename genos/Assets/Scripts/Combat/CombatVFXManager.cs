using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Climbing.Combat
{
    /// <summary>
    /// Lightweight component on the player to register their trails and boosters to the global VFXManager.
    /// </summary>
    public class CombatVFXManager : MonoBehaviour
    {
        [Header("Trails & Boosters")]
        public TrailRenderer[] boosterTrails;
        public ParticleSystem[] boosterParticles;

        void Start()
        {
            if (VFXManager.Instance != null)
            {
                VFXManager.Instance.RegisterPlayerVFX(boosterTrails, boosterParticles);
            }
        }

        public void SetTrailsActive(bool active)
        {
            if (VFXManager.Instance != null)
            {
                VFXManager.Instance.SetPlayerTrailsActive(active);
            }
        }

        public void ExecuteFinisherFlourish()
        {
            if (VFXManager.Instance != null)
            {
                VFXManager.Instance.ExecuteImpactFrame(true);
                VFXManager.Instance.ExecuteActionLines();
            }
        }
    }
}
