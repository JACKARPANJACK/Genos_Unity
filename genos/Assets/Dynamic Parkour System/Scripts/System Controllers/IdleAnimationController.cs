using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Climbing
{
    [System.Serializable]
    public class IdleFaceMapping
    {
        [Tooltip("The random idle animator state index (e.g. 5)")]
        public int idleAnimationIndex;
        [Tooltip("The exact name of the facial animation preset in AnimeFaceController (e.g. 'Yawn')")]
        public string facialExpressionName;
    }

    [RequireComponent(typeof(ThirdPersonController))]
    public class IdleAnimationController : MonoBehaviour
    {
        private ThirdPersonController thirdPersonController;
        private InputCharacterController characterInput;
        private AnimationCharacterController characterAnimation;
        private AnimeFaceController faceController;

        [Header("Idle Animation Settings")]
        public float minWaitTime = 5f;
        public float maxWaitTime = 15f;
        public int minAnimationIndex = 1;
        public int maxAnimationIndex = 24;

        [Header("Facial Mappings")]
        [Tooltip("Map explicit Idle Indexes (e.g. 5) directly to your Facial Animation Preset names (e.g. 'Yawn'). Ignored if left blank.")]
        public List<IdleFaceMapping> idleFaceMappings = new List<IdleFaceMapping>();

        [Header("Animator Parameters")]
        public float crossfadeDuration = 0.25f;
        public string defaultIdleStateName = "Idle";

        private float idleTimer = 0f;
        private float currentTargetTime = 0f;
        private bool isPlayingRandomIdle = false;
        private string currentIdleStateName = "";

        private void Awake()
        {
            thirdPersonController = GetComponent<ThirdPersonController>();
            characterInput = GetComponent<InputCharacterController>();
            characterAnimation = GetComponent<AnimationCharacterController>();
            faceController = GetComponentInChildren<AnimeFaceController>();
        }

        private void Start()
        {
            SetNewTargetTime();
        }

        private void Update()
        {
            if (HasInput())
            {
                ResetIdleState();
            }
            else if (thirdPersonController.isGrounded && (thirdPersonController.allowMovement || isPlayingRandomIdle))
            {
                if (!isPlayingRandomIdle)
                {
                    // Strict check: Only count idle time if we are actually in the default Idle state!
                    // This prevents idles from queuing or playing while jumping, falling, or stuck in another animation.
                    if (characterAnimation.animator != null)
                    {
                        AnimatorStateInfo stateInfo = characterAnimation.animator.GetCurrentAnimatorStateInfo(0);
                        if (!stateInfo.IsName(defaultIdleStateName))
                        {
                            idleTimer = 0f;
                            return; // Block incrementing timer if we aren't truly 'idle'
                        }
                    }

                    idleTimer += Time.deltaTime;

                    if (idleTimer >= currentTargetTime)
                    {
                        PlayRandomIdle();
                    }
                }
                else
                {
                    idleTimer += Time.deltaTime; // Keep tracking time to check fallback

                    // Check if animation finished to return to normal camera
                    if (characterAnimation.animator != null)
                    {
                        AnimatorStateInfo stateInfo = characterAnimation.animator.GetCurrentAnimatorStateInfo(0);

                        // We must ensure the animator actually switched into the target state before we check if it finished
                        if (stateInfo.IsName(currentIdleStateName))
                        {
                            // Trigger slightly earlier to allow the default animation crossfade transition out
                            if (stateInfo.normalizedTime >= 0.95f && !characterAnimation.animator.IsInTransition(0))
                            {
                                ResetIdleState();
                            }
                        }
                        else if (!characterAnimation.animator.IsInTransition(0) && idleTimer > currentTargetTime + 1f)
                        {
                            // Fallback exactly in case the state name didn't match the animator (failsafe to return after 1 sec of no transition)
                            ResetIdleState();
                        }
                    }
                }
            }
            else if (!isPlayingRandomIdle)
            {
                // Reset if we lose grounding or movement permissions (and not currently playing idle)
                ResetIdleState();
            }
            else if (!thirdPersonController.isGrounded)
            {
                ResetIdleState();
            }
        }

        private bool HasInput()
        {
            return characterInput.movement.sqrMagnitude > 0.01f ||
                   characterInput.jump ||
                   characterInput.drop ||
                   characterInput.run ||
                   characterInput.dash;
        }

        [ContextMenu("Reset Idle State")]
        private void ResetIdleState()
        {
            idleTimer = 0f;
            if (isPlayingRandomIdle)
            {
                isPlayingRandomIdle = false;

                if (thirdPersonController != null)
                {
                    thirdPersonController.allowMovement = true;
                }

                if (characterAnimation.animator != null && !string.IsNullOrEmpty(defaultIdleStateName))
                {
                    characterAnimation.animator.CrossFadeInFixedTime(defaultIdleStateName, crossfadeDuration);
                }

                if (characterAnimation.switchCameras != null)
                {
                    characterAnimation.switchCameras.FreeLookCam();
                }

                if (faceController != null)
                {
                    // Cleanly reset back to the default static configuration
                    faceController.ResetToDefault();
                }
            }
            SetNewTargetTime();
        }

        [ContextMenu("Play Random Idle")]
        private void PlayRandomIdle()
        {
            if (characterAnimation.animator == null)
            {
                return;
            }

            // Triple check we are in the default idle state before allowing a crossfade
            AnimatorStateInfo stateInfo = characterAnimation.animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName(defaultIdleStateName))
            {
                idleTimer = 0f;
                return;
            }

            isPlayingRandomIdle = true;

            if (thirdPersonController != null)
            {
                thirdPersonController.allowMovement = false;
            }

            int randomIndex = Random.Range(minAnimationIndex, maxAnimationIndex + 1);
            currentIdleStateName = randomIndex.ToString();

            characterAnimation.animator.CrossFadeInFixedTime(currentIdleStateName, crossfadeDuration);

            // Optional: Map the animation index to a specific facial expression preset array!
            if (faceController != null && idleFaceMappings != null)
            {
                var mapping = idleFaceMappings.Find(m => m.idleAnimationIndex == randomIndex);
                if (mapping != null && !string.IsNullOrEmpty(mapping.facialExpressionName))
                {
                    faceController.PlayFacialAnimation(mapping.facialExpressionName);
                }
                else
                {
                    // Fallback back to Default expression if no mapping exists for this specific idle!
                    faceController.ResetToDefault();
                }
            }

            if (characterAnimation.switchCameras != null)
            {
                Transform headTransform = characterAnimation.animator.GetBoneTransform(HumanBodyBones.Head);
                characterAnimation.switchCameras.IdleCutsceneCam(transform, headTransform);
            }
        }

        private void SetNewTargetTime()
        {
            currentTargetTime = Random.Range(minWaitTime, maxWaitTime);
        }
    }
}
