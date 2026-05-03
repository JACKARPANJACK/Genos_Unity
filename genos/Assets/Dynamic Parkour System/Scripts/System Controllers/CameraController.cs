/*
MIT License

Copyright (c) 2023 Èric Canela
Contact: knela96@gmail.com or @knela96 twitter

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (Dynamic Parkour System), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;

namespace Climbing
{
    // ?? State enum ??????????????????????????????????????????????????????????????

    /// <summary>Named movement states that each carry their own camera profile.</summary>
    public enum CameraFOVState { Idle, Walk, Run, WallRun, Parkour, AirDash, Jump, ChargeJump, Aim }

    // ?? Data types ??????????????????????????????????????????????????????????????

    /// <summary>
    /// Full camera profile for one movement state:
    /// target FOV, blend timing + ease, and Dutch (roll) angle for wall-lean.
    /// </summary>
    [System.Serializable]
    public class CameraStateProfile
    {
        [Tooltip("Target field of view in degrees.")]
        public float fieldOfView = 10f;

        [Tooltip("Seconds to blend toward this FOV.")]
        public float fovBlendTime = 0.3f;
        public Ease fovEase = Ease.OutCubic;

        [Tooltip("Dutch / roll angle in degrees. Negative = lean left, positive = lean right. " +
                 "WallRun overrides this per-side at runtime.")]
        public float dutch = 0f;

        [Tooltip("Seconds to blend toward this Dutch angle.")]
        public float dutchBlendTime = 0.2f;
        public Ease dutchEase = Ease.OutSine;
    }

    /// <summary>Single camera-shake impulse configuration.</summary>
    [System.Serializable]
    public class ShakeProfile
    {
        [Tooltip("Magnitude of the impulse force.")]
        public float force = 1f;

        [Tooltip("World-space direction of the impulse velocity. " +
                 "Vector3.up gives the standard vertical kick.")]
        public Vector3 direction = Vector3.up;
    }

    // ?? Component ???????????????????????????????????????????????????????????????

    public class CameraController : MonoBehaviour
    {
        // ?? Existing offset system ???????????????????????????????????????????

        private CinemachineCameraOffset cameraOffset;

        public Vector3 _offset;
        public Vector3 _default;
        private Vector3 _target;

        public float maxTime = 2.0f;
        private Tween offsetTween;
        private Tween[] zoomTweens = new Tween[3];

        [Header("Idle Sway Settings")]
        public bool isIdle = false;
        public float swaySpeed = 1.0f;
        public float swayAmount = 0.1f;
        public Vector3 baseOffset;

        [Header("Zoom Settings")]
        public float zoomSpeed = 2.0f;
        [Tooltip("Multipliers for the orbit radii based on zoom level. 1 = default")]
        public float minZoom = 0.5f;
        public float maxZoom = 2.0f;
        private float currentZoom = 1.0f;

        private CinemachineCamera freeLookCamera;
        private float originalRadius = 2.5f;
        private float[] originalRadii = new float[3] { 2.5f, 3.5f, 1.5f };

        [Header("Aiming Settings")]
        public int aimPriority = 20;
        public Vector3 aimShoulderOffset = new Vector3(0.65f, 0.4f, -0.3f);
        private Vector3 currentShoulderOffset;

        [Header("Manual Zoom")]
        public float minFOV = 15f;
        public float maxFOV = 60f;
        public float zoomSensitivity = 2f;
        private float manualFOVOffset = 0f;
        private float manualDistanceOffset = 0f;
        private CinemachineCamera aimCamera;

        // ?? FOV & Dutch profiles ????????????????????????????????????????????

        [Header("Base FOV")]
        [Tooltip("Master baseline field of view. Lower values = closer view. " +
                 "Profiles are applied as offsets relative to this value (assuming 50 is neutral).")]
        public float baseFOV = 10f;

        [Header("FOV Profiles")]
        public CameraStateProfile idleProfile  = new CameraStateProfile { fieldOfView = 5f, fovBlendTime = 0.5f, fovEase = Ease.OutCubic };
        public CameraStateProfile walkProfile  = new CameraStateProfile { fieldOfView = 10f, fovBlendTime = 0.4f };
        public CameraStateProfile runProfile   = new CameraStateProfile { fieldOfView = 15f, fovBlendTime = 0.35f };
        public CameraStateProfile wallRunProfile = new CameraStateProfile
        {
            fieldOfView = 55f,  fovBlendTime = 0.25f, fovEase = Ease.OutCubic,
            dutch = 0f,         dutchBlendTime = 0.2f, dutchEase = Ease.OutSine
        };
        public CameraStateProfile parkourProfile = new CameraStateProfile
        {
            fieldOfView = 50f, fovBlendTime = 0.2f, fovEase = Ease.OutQuad
        };
        public CameraStateProfile airDashProfile = new CameraStateProfile
        {
            fieldOfView = 72f, fovBlendTime = 0.12f, fovEase = Ease.OutExpo
        };
        public CameraStateProfile jumpProfile = new CameraStateProfile
        {
            fieldOfView = 80f, fovBlendTime = 0.2f, fovEase = Ease.OutQuad
        };
        public CameraStateProfile chargeJumpProfile = new CameraStateProfile
        {
            fieldOfView = 150f, fovBlendTime = 0.1f, fovEase = Ease.OutExpo
        };
        public CameraStateProfile aimProfile = new CameraStateProfile 
        { 
            fieldOfView = 30f, fovBlendTime = 0.1f 
        };

        // ?? Camera shake ????????????????????????????????????????????????????

        [Header("Camera Shake")]
        [Tooltip("Assign a CinemachineImpulseSource component (on this object or the player).")]
        public CinemachineImpulseSource impulseSource;

        [Tooltip("Soft landing, small collision, footstep accent.")]
        public ShakeProfile lightShake  = new ShakeProfile { force = 0.25f, direction = Vector3.up };
        [Tooltip("Dash, wall jump, medium collision.")]
        public ShakeProfile mediumShake = new ShakeProfile { force = 0.6f,  direction = Vector3.up };
        [Tooltip("Hard landing, heavy impact.")]
        public ShakeProfile heavyShake  = new ShakeProfile { force = 1.2f,  direction = Vector3.up };

        // ?? Runtime state ???????????????????????????????????????????????????

        private float stateFOV;
        private float dynamicFOVAdd;
        private float currentDutch;

        public CameraFOVState ActiveFOVState => activeFOVState;
        private CameraFOVState activeFOVState = (CameraFOVState)(-1);
        private Tween fovTween;
        private Tween dutchTween;

        [Header("Dynamic FOV")]
        public bool useDynamicFOV = true;
        public float dynamicFOVSensitivity = 1.0f;
        public float maxDynamicFOVAdd = 15f;
        private MovementCharacterController playerMovement;

        // ?? Lifecycle ???????????????????????????????????????????????????????

        void Start()
        {
            cameraOffset = GetComponent<CinemachineCameraOffset>();
            if (cameraOffset != null)
            {
                baseOffset = cameraOffset.Offset;
            }
            else
            {
                baseOffset = Vector3.zero;
            }

            playerMovement = GameObject.FindAnyObjectByType<MovementCharacterController>();

            var aimGo = GameObject.Find("AimCam");
            if (aimGo != null) aimCamera = aimGo.GetComponent<CinemachineCamera>();

            freeLookCamera = GetComponent<CinemachineCamera>();
            if (freeLookCamera != null)
            {
                var orbitalFollow = freeLookCamera.GetComponent<CinemachineOrbitalFollow>();
                if (orbitalFollow != null)
                {
                    originalRadius = orbitalFollow.Radius;
                    originalRadii[0] = orbitalFollow.Orbits.Top.Radius;
                    originalRadii[1] = orbitalFollow.Orbits.Center.Radius;
                    originalRadii[2] = orbitalFollow.Orbits.Bottom.Radius;
                }
                else
                {
                    var thirdPersonFollow = freeLookCamera.GetComponent<CinemachineThirdPersonFollow>();
                    if (thirdPersonFollow != null)
                    {
                        originalRadius = thirdPersonFollow.CameraDistance;
                    }
                }
            }

            // Seed stateFOV from the baseline to ensure we don't start at 0
            stateFOV = baseFOV;
            currentDutch = 0f;
            dynamicFOVAdd = 0f;

            // Immediately apply the Idle state to synchronize the camera lens
            SetFOVState(CameraFOVState.Idle);
            ApplyLensValues();
        }

        void LateUpdate()
        {
            HandleScrollZoom();
            HandleIdleSway();
            HandleDynamicFOV();
            UpdatePriorities();
            ApplyOffsets();

            // Re-apply Lens values every frame to prevent Cinemachine from overriding them
            ApplyLensValues();
        }

        private void UpdatePriorities()
        {
            if (aimCamera != null)
            {
                aimCamera.Priority.Value = (activeFOVState == CameraFOVState.Aim) ? aimPriority : 0;
            }
        }

        private void ApplyOffsets()
        {
            if (cameraOffset != null)
            {
                Vector3 targetOffset = (activeFOVState == CameraFOVState.Aim) ? aimShoulderOffset : Vector3.zero;
                currentShoulderOffset = Vector3.Lerp(currentShoulderOffset, targetOffset, Time.deltaTime * 25f);
                cameraOffset.Offset = baseOffset + currentShoulderOffset;
            }
        }

        private void HandleDynamicFOV()
        {
            if (!useDynamicFOV || playerMovement == null)
            {
                dynamicFOVAdd = Mathf.Lerp(dynamicFOVAdd, 0f, Time.deltaTime * 5f);
                return;
            }

            // Exclude Y velocity so jumping/falling doesn't warp the FOV drastically.
            Vector3 horizontalVelocity = new Vector3(playerMovement.rb.linearVelocity.x, 0, playerMovement.rb.linearVelocity.z);
            float currentSpeed = horizontalVelocity.magnitude;

            // Dynamic FOV should only add once we exceed normal walking speed
            float speedThreshold = playerMovement.walkSpeed;
            float targetFOVAdd = Mathf.Max(0, currentSpeed - speedThreshold) * dynamicFOVSensitivity;
            targetFOVAdd = Mathf.Min(targetFOVAdd, maxDynamicFOVAdd);

            dynamicFOVAdd = Mathf.Lerp(dynamicFOVAdd, targetFOVAdd, Time.deltaTime * 5f);
        }

        // ?? Scroll zoom (unchanged from original) ??????????????????????????

        private void HandleScrollZoom()
        {
            if (freeLookCamera == null) return;

            float scrollDelta = 0f;
            try { scrollDelta = UnityEngine.Input.mouseScrollDelta.y; } catch { }
            if (Mathf.Abs(scrollDelta) < 0.01f)
            {
                try { scrollDelta = UnityEngine.Input.GetAxis("Mouse ScrollWheel"); } catch { }
            }
            if (Mathf.Abs(scrollDelta) < 0.01f)
            {
                try
                {
                    if (UnityEngine.InputSystem.Mouse.current != null)
                        scrollDelta = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
                } catch { }
            }

            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                if (activeFOVState == CameraFOVState.Aim)
                {
                    manualFOVOffset -= Mathf.Clamp(scrollDelta, -1f, 1f) * zoomSensitivity * 2f;
                    manualFOVOffset = Mathf.Clamp(manualFOVOffset, -15f, 15f);
                }
                else
                {
                    currentZoom -= Mathf.Clamp(scrollDelta, -1f, 1f) * (zoomSpeed * 0.25f);
                    currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
                }
            }

            var orbitalFollow = freeLookCamera.GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow != null)
            {
                orbitalFollow.Radius = Mathf.Lerp(orbitalFollow.Radius, originalRadius * currentZoom, Time.deltaTime * 10f);

                var orbits = orbitalFollow.Orbits;
                orbits.Top.Radius = Mathf.Lerp(orbits.Top.Radius, originalRadii[0] * currentZoom, Time.deltaTime * 10f);
                orbits.Center.Radius = Mathf.Lerp(orbits.Center.Radius, originalRadii[1] * currentZoom, Time.deltaTime * 10f);
                orbits.Bottom.Radius = Mathf.Lerp(orbits.Bottom.Radius, originalRadii[2] * currentZoom, Time.deltaTime * 10f);
                orbitalFollow.Orbits = orbits;
            }
            else
            {
                var thirdPersonFollow = freeLookCamera.GetComponent<CinemachineThirdPersonFollow>();
                if (thirdPersonFollow != null)
                {
                    thirdPersonFollow.CameraDistance = Mathf.Lerp(thirdPersonFollow.CameraDistance, originalRadius * currentZoom, Time.deltaTime * 10f);
                }
            }
        }

        // ?? Idle sway (unchanged from original) ???????????????????????????

        private void HandleIdleSway()
        {
            if (isIdle && offsetTween == null && cameraOffset != null)
            {
                float offsetX = Mathf.PerlinNoise(Time.time * swaySpeed, 0f) * swayAmount - (swayAmount / 2f);
                float offsetY = Mathf.PerlinNoise(0f, Time.time * swaySpeed) * swayAmount - (swayAmount / 2f);
                cameraOffset.Offset = baseOffset + new Vector3(offsetX, offsetY, 0f);
            }
        }

        // ?? Offset API (unchanged from original) ??????????????????????????

        /// <summary>Adds an offset to the camera (e.g. during climbing or sliding).</summary>
        public void newOffset(bool offset)
        {
            if (cameraOffset == null) return;

            _target = offset ? _offset : _default;

            offsetTween?.Kill();
            offsetTween = DOVirtual.Vector3(cameraOffset.Offset, _target, maxTime, val =>
            {
                cameraOffset.Offset = val;
                baseOffset = val;
            })
            .SetEase(Ease.InOutSine)
            .OnComplete(() =>
            {
                baseOffset = _target;
                offsetTween = null;
            });
        }

        // ?? FOV state API ?????????????????????????????????????????????????

        /// <summary>
        /// Transitions to the named movement state's FOV and Dutch profile.
        /// Calling the same state twice is a no-op.
        /// </summary>
        public void SetFOVState(CameraFOVState state)
        {
            if (activeFOVState == state) return;
            activeFOVState = state;
            ApplyProfile(GetProfile(state));
        }

        /// <summary>
        /// Overrides only the Dutch (roll) angle — used during wall running
        /// so the lean correctly mirrors the wall side without changing the FOV profile.
        /// </summary>
        /// <param name="angle">The exact angle to tilt the camera.</param>
        public void SetWallRunTilt(float angle)
        {
            float targetDutch = angle;
            dutchTween?.Kill();
            dutchTween = DOVirtual.Float(currentDutch, targetDutch, wallRunProfile.dutchBlendTime, v =>
            {
                currentDutch = v;
            })
            .SetEase(wallRunProfile.dutchEase)
            .OnComplete(() => dutchTween = null);
        }

        /// <summary>Convenience: instantly reset Dutch (roll) to zero.</summary>
        public void ResetTilt() => SetWallRunTilt(0f);

        public void SetCameraRotationLock(bool locked)
        {
            var axisController = GetComponent<CinemachineInputAxisController>();
            if (axisController != null)
            {
                axisController.enabled = !locked;
            }
        }

        // ?? Shake API ?????????????????????????????????????????????????????

        /// <summary>Fires a custom shake impulse. Requires CinemachineImpulseListener on the virtual camera.</summary>
        public void Shake(ShakeProfile profile)
        {
            if (impulseSource == null) return;
            impulseSource.GenerateImpulseWithVelocity(profile.direction.normalized * profile.force);
        }

        /// <summary>Soft footstep-level shake.</summary>
        public void ShakeLight()  => Shake(lightShake);

        /// <summary>Dash / wall-jump level shake.</summary>
        public void ShakeMedium() => Shake(mediumShake);

        /// <summary>Hard landing / heavy impact shake.</summary>
        public void ShakeHeavy()  => Shake(heavyShake);

        // ?? Internal helpers ??????????????????????????????????????????????

        private CameraStateProfile GetProfile(CameraFOVState state)
        {
            switch (state)
            {
                case CameraFOVState.Idle:    return idleProfile;
                case CameraFOVState.Run:     return runProfile;
                case CameraFOVState.WallRun: return wallRunProfile;
                case CameraFOVState.Parkour: return parkourProfile;
                case CameraFOVState.AirDash: return airDashProfile;
                case CameraFOVState.Jump:    return jumpProfile;
                case CameraFOVState.ChargeJump: return chargeJumpProfile;
                case CameraFOVState.Aim:     return aimProfile;
                default:                     return walkProfile;
            }
        }

        private void ApplyProfile(CameraStateProfile profile)
        {
            // Transition target is the profile's fieldOfView setting.
            float targetFOV = profile.fieldOfView;
            targetFOV = Mathf.Clamp(targetFOV, 1f, 175f);

            fovTween?.Kill();
            fovTween = DOVirtual.Float(stateFOV, targetFOV, profile.fovBlendTime, v =>
            {
                stateFOV = v;
            })
            .SetEase(profile.fovEase)
            .OnComplete(() => fovTween = null);

            // Dutch tween (only if profile has a Dutch value — wall run overrides via SetWallRunTilt)
            dutchTween?.Kill();
            dutchTween = DOVirtual.Float(currentDutch, profile.dutch, profile.dutchBlendTime, v =>
            {
                currentDutch = v;
            })
            .SetEase(profile.dutchEase)
            .OnComplete(() => dutchTween = null);
        }

        private void ApplyLensValues()
        {
            float finalTargetFOV = stateFOV;

            // If we're not actively tweening, make sure stateFOV tracks baseFOV changes in the inspector.
            if (fovTween == null || !fovTween.IsActive())
            {
                var profile = GetProfile(activeFOVState);
                stateFOV = profile.fieldOfView;
                stateFOV = Mathf.Clamp(stateFOV, 1f, 175f);
            }

            // Final FOV = Blended State FOV + Dynamic speed-based addition + manual zoom FOV
            finalTargetFOV = Mathf.Clamp(stateFOV + dynamicFOVAdd + manualFOVOffset, 1f, 175f);

            if (freeLookCamera != null)
            {
                var lens = freeLookCamera.Lens;
                if (Mathf.Abs(lens.FieldOfView - finalTargetFOV) > 0.01f || Mathf.Abs(lens.Dutch - currentDutch) > 0.01f)
                {
                    lens.FieldOfView = finalTargetFOV;
                    lens.Dutch = currentDutch;
                    freeLookCamera.Lens = lens;
                }
            }

            if (aimCamera != null)
            {
                var lens = aimCamera.Lens;
                if (Mathf.Abs(lens.FieldOfView - finalTargetFOV) > 0.01f)
                {
                    lens.FieldOfView = finalTargetFOV;
                    aimCamera.Lens = lens;
                }

                var tpf = aimCamera.GetComponent<CinemachineThirdPersonFollow>();
                if (tpf != null)
                {
                    tpf.CameraDistance = Mathf.Clamp(1.5f + (currentZoom - 1.0f) * 0.5f, 0.5f, 3f);
                }
            }
        }
    }
}
