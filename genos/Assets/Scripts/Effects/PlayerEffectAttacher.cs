using UnityEngine;
using System.Collections.Generic;

namespace Climbing.Effects
{
    public enum BodyPart
    {
        Hips,
        Spine,
        Chest,
        Head,
        LeftHand,
        RightHand,
        LeftFoot,
        RightFoot,
        LeftShoulder,
        RightShoulder,
        LeftUpperArm,
        RightUpperArm,
        LeftLowerArm,
        RightLowerArm,
        LeftUpperLeg,
        RightUpperLeg,
        LeftLowerLeg,
        RightLowerLeg
    }

    public class PlayerEffectAttacher : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private Dictionary<BodyPart, Transform> boneMap = new Dictionary<BodyPart, Transform>();

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            InitializeBoneMap();
            EffectManager.RegisterPlayer(this);
        }

        private void InitializeBoneMap()
        {
            boneMap[BodyPart.Hips] = animator.GetBoneTransform(HumanBodyBones.Hips);
            boneMap[BodyPart.Spine] = animator.GetBoneTransform(HumanBodyBones.Spine);
            boneMap[BodyPart.Chest] = animator.GetBoneTransform(HumanBodyBones.Chest);
            boneMap[BodyPart.Head] = animator.GetBoneTransform(HumanBodyBones.Head);
            boneMap[BodyPart.LeftHand] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            boneMap[BodyPart.RightHand] = animator.GetBoneTransform(HumanBodyBones.RightHand);
            boneMap[BodyPart.LeftFoot] = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            boneMap[BodyPart.RightFoot] = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            boneMap[BodyPart.LeftShoulder] = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            boneMap[BodyPart.RightShoulder] = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            boneMap[BodyPart.LeftUpperArm] = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            boneMap[BodyPart.RightUpperArm] = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            boneMap[BodyPart.LeftLowerArm] = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            boneMap[BodyPart.RightLowerArm] = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            boneMap[BodyPart.LeftUpperLeg] = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            boneMap[BodyPart.RightUpperLeg] = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            boneMap[BodyPart.LeftLowerLeg] = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            boneMap[BodyPart.RightLowerLeg] = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }

        public Transform GetBone(BodyPart part)
        {
            if (boneMap.TryGetValue(part, out Transform bone))
            {
                return bone;
            }
            return transform;
        }

        public GameObject AttachEffect(GameObject effectPrefab, BodyPart part, Vector3 localPosition = default, Quaternion localRotation = default)
        {
            Transform targetBone = GetBone(part);
            if (targetBone == null) return null;

            GameObject effect = Instantiate(effectPrefab, targetBone);
            effect.transform.localPosition = localPosition;
            effect.transform.localRotation = localRotation == default ? Quaternion.identity : localRotation;
            
            return effect;
        }
    }
}
