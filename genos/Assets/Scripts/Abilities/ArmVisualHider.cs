using UnityEngine;

namespace Climbing.Abilities
{
    public class ArmVisualHider : MonoBehaviour
    {
        public SkinnedMeshRenderer smr;
        
        void Awake()
        {
            if (smr == null) smr = GetComponentInChildren<SkinnedMeshRenderer>();
            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            if (smr == null) return;

            foreach (var bone in smr.bones)
            {
                if (bone == null) continue;

                if (!IsRightArmPart(bone.name) && !IsAncestorOfRightArm(bone.name))
                {
                    bone.localScale = Vector3.zero;
                }
            }
        }

        private bool IsRightArmPart(string boneName)
        {
            // Genos specific bone names
            if (boneName.Contains(".r"))
            {
                if (boneName.Contains("forearm") || boneName.Contains("hand") || boneName.Contains("c_"))
                    return true;
            }
            return false;
        }

        private bool IsAncestorOfRightArm(string boneName)
        {
            // These bones must stay scale 1 for the arm to be visible/positioned
            string[] ancestors = { "root.x", "spine_01.x", "spine_02.x", "spine_03.x", "shoulder.r", "arm_stretch.r" };
            foreach (var a in ancestors)
            {
                if (boneName == a) return true;
            }
            return false;
        }
    }
}
