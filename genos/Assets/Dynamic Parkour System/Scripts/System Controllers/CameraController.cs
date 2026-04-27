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
    public enum CameraFOVState { Idle, Walk, Run, WallRun, Parkour, AirDash }

    // ?? Data types ??????????????????????????????????????????????????????????????

    /// <summary>
    /// Full camera profile for one movement state:
    /// target FOV, blend timing + ease, and Dutch (roll) angle for wall-lean.
    /// </summary>
    [System.Serializable]
    public class CameraStateProfile
    {
        [Tooltip("Target field of view in degrees.")]
        public float fieldOfView = 60f;

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

        // ?? FOV & Dutch profiles ????????????????????????????????????????????

        [Header("Base FOV")]
        [Tooltip("Default field of view used as the Idle/Walk baseline.")]
        public float baseFOV = 60f;

        [Header("FOV Profiles")]
        public CameraStateProfile walkProfile  = new CameraStateProfile { fieldOfView = 60f, fovBlendTime = 0.4f };
        public CameraStateProfile runProfile   = new CameraStateProfile { fieldOfView = 65f, fovBlendTime = 0.35f };
        public CameraStateProfile wallRunProfile = new CameraStateProfile
        {
            fieldOfView = 75f,  fovBlendTime = 0.25f, fovEase = Ease.OutCubic,
            dutch = 0f,         dutchBlendTime = 0.2f, dutchEase = Ease.OutSine
        };
        public CameraStateProfile parkourProfile = new CameraStateProfile
        {
            fieldOfView = 70f, fovBlendTime = 0.2f, fovEase = Ease.OutQuad
        };
        public CameraStateProfile airDashProfile = new CameraStateProfile
        {
            fieldOfView = 82f, fovBlendTime = 0.12f, fovEase = Ease.OutExpo
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

        private float currentFOV;
        private float currentDutch;
        private CameraFOVState activeFOVState = CameraFOVState.Idle;
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

            // Seed runtime FOV from whatever is in the lens right now
            currentFOV = freeLookCamera.Lens.FieldOfView;
            float fovOffset = currentFOV - baseFOV;
            baseFOV = currentFOV;

            // Make profiles relative to the camera's base FOV so proper FOV values are used
            walkProfile.fieldOfView += fovOffset;
            runProfile.fieldOfView += fovOffset;
            wallRunProfile.fieldOfView += fovOffset;
            parkourProfile.fieldOfView += fovOffset;
            airDashProfile.fieldOfView += fovOffset;
        }
        else
        {
            currentFOV = baseFOV;
        }

            currentDutch = 0f;
        }

        void Update()
        {
            HandleScrollZoom();
            HandleIdleSway();
            HandleDynamicFOV();

            // Re-apply Lens values every frame to prevent Cinemachine from overriding them
            if (activeFOVState != CameraFOVState.Idle || true) // Actually always apply just to be safe
                ApplyLensValues();
        }

        private void HandleDynamicFOV()
        {
            if (!useDynamicFOV || playerMovement == null || fovTween != null) return;

            float currentSpeed = playerMovement.rb.linearVelocity.magnitude;
            float targetFOVAdd = Mathf.Clamp(currentSpeed * dynamicFOVSensitivity, 0, maxDynamicFOVAdd);
            
            // Smoothed return to profile FOV or base FOV
            float targetBase = GetProfile(activeFOVState).fieldOfView;
            currentFOV = Mathf.Lerp(currentFOV, targetBase + targetFOVAdd, Time.deltaTime * 5f);
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
                currentZoom -= Mathf.Clamp(scrollDelta, -1f, 1f) * (zoomSpeed * 0.25f);
                currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
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
            if (isIdle && offsetTween == null)
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
        /// <param name="side">+1 = right wall lean, -1 = left wall lean, 0 = upright.</param>
        public void SetWallRunTilt(float side)
        {
            float targetDutch = side * wallRunProfile.dutch;
            dutchTween?.Kill();
            dutchTween = DOVirtual.Float(currentDutch, targetDutch, wallRunProfile.dutchBlendTime, v =>
            {
                currentDutch = v;
                ApplyLensValues();
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
                case CameraFOVState.Idle:    return new CameraStateProfile { fieldOfView = baseFOV, fovBlendTime = 0.5f, fovEase = Ease.OutCubic };
                case CameraFOVState.Run:     return runProfile;
                case CameraFOVState.WallRun: return wallRunProfile;
                case CameraFOVState.Parkour: return parkourProfile;
                case CameraFOVState.AirDash: return airDashProfile;
                default:                     return walkProfile;
            }
        }

        private void ApplyProfile(CameraStateProfile profile)
        {
            // FOV tween
            fovTween?.Kill();
            fovTween = DOVirtual.Float(currentFOV, profile.fieldOfView, profile.fovBlendTime, v =>
            {
                currentFOV = v;
                ApplyLensValues();
            })
            .SetEase(profile.fovEase)
            .OnComplete(() => fovTween = null);

            // Dutch tween (only if profile has a Dutch value — wall run overrides via SetWallRunTilt)
            dutchTween?.Kill();
            dutchTween = DOVirtual.Float(currentDutch, profile.dutch, profile.dutchBlendTime, v =>
            {
                currentDutch = v;
                ApplyLensValues();
            })
            .SetEase(profile.dutchEase)
            .OnComplete(() => dutchTween = null);
        }

        private void ApplyLensValues()
        {
            if (freeLookCamera != null)
            {
                var lens = freeLookCamera.Lens;
                lens.FieldOfView = currentFOV;
                lens.Dutch = currentDutch;
                freeLookCamera.Lens = lens;
            }
        }
    }
}
