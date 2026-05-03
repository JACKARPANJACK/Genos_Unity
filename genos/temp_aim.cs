using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;

namespace Climbing
{
    public enum CameraFOVState { Idle, Walk, Run, WallRun, Parkour, AirDash, Jump, ChargeJump, Aim }

    [System.Serializable]
    public class CameraStateProfile
    {
        public float fieldOfView = 50f;
        public float fovBlendTime = 0.3f;
        public Ease fovEase = Ease.OutCubic;
        public float dutch = 0f;
        public float dutchBlendTime = 0.2f;
        public Ease dutchEase = Ease.OutSine;
    }

    public class CameraController : MonoBehaviour
    {
        private CinemachineCameraOffset cameraOffset;
        private CinemachineCamera freeLookCamera;
        private CinemachineCamera aimCamera;
        private InputCharacterController input;

        [Header("FOV Profiles")]
        public CameraStateProfile walkProfile = new CameraStateProfile { fieldOfView = 50f };
        public CameraStateProfile aimProfile = new CameraStateProfile { fieldOfView = 30f, fovBlendTime = 0.1f };

        public CameraFOVState ActiveFOVState => activeFOVState;
        private CameraFOVState activeFOVState = CameraFOVState.Walk;
        private float stateFOV = 50f;
        private float currentDutch = 0f;
        private Tween fovTween;
        private Tween dutchTween;

        [HideInInspector] public bool isIdle;
        [HideInInspector] public float swayAmount;

        [Header("Aiming Settings")]
public int aimPriority = 20;
        public Vector3 aimShoulderOffset = new Vector3(0.65f, 0.4f, -0.3f);
        private Vector3 currentShoulderOffset;
        private Vector3 baseOffset;

        [Header("Manual Zoom")]
        public float minFOV = 15f;
        public float maxFOV = 60f;
        public float zoomSensitivity = 2f;
        private float manualFOVOffset = 0f;
        
        public float minDistance = 1f;
        public float maxDistance = 6f;
        private float manualDistanceOffset = 0f;

        void Awake()
        {
            input = Object.FindFirstObjectByType<InputCharacterController>();
            cameraOffset = GetComponent<CinemachineCameraOffset>();
            baseOffset = cameraOffset != null ? cameraOffset.Offset : Vector3.zero;
            freeLookCamera = GetComponent<CinemachineCamera>();
            var aimGo = GameObject.Find("AimCam");
            if (aimGo != null) aimCamera = aimGo.GetComponent<CinemachineCamera>();
        }

        void Start()
        {
            SetFOVState(CameraFOVState.Walk);
        }

        void LateUpdate()
        {
            HandleManualZoom();
            UpdatePriorities();
            ApplyOffsets();
            ApplyLens();
        }

        private void HandleManualZoom()
        {
            if (input == null) return;
            float delta = input.GetCycleDelta();
            
            if (Mathf.Abs(delta) > 0.01f)
            {
                if (activeFOVState == CameraFOVState.Aim)
                {
                    // Zoom FOV while aiming
                    manualFOVOffset -= delta * 0.01f * zoomSensitivity;
                    manualFOVOffset = Mathf.Clamp(manualFOVOffset, -15f, 15f);
                }
                else
                {
                    // Zoom Distance while walking
                    manualDistanceOffset -= delta * 0.005f * zoomSensitivity;
                    manualDistanceOffset = Mathf.Clamp(manualDistanceOffset, -2f, 3f);
                }
            }
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

        private void ApplyLens()
        {
            float finalFOV = Mathf.Clamp(stateFOV + manualFOVOffset, minFOV, maxFOV);
            
            if (freeLookCamera != null)
            {
                var lens = freeLookCamera.Lens;
                lens.FieldOfView = finalFOV;
                lens.Dutch = currentDutch;
                freeLookCamera.Lens = lens;
                
                var orb = freeLookCamera.GetComponent<CinemachineOrbitalFollow>();
                if (orb != null)
                {
                    orb.Radius = Mathf.Clamp(2.61f + manualDistanceOffset, minDistance, maxDistance);
                }
            }
            if (aimCamera != null)
            {
                var lens = aimCamera.Lens;
                lens.FieldOfView = finalFOV;
                aimCamera.Lens = lens;
                
                var tpf = aimCamera.GetComponent<CinemachineThirdPersonFollow>();
                if (tpf != null)
                {
                    tpf.CameraDistance = Mathf.Clamp(1.5f + manualDistanceOffset * 0.5f, 0.5f, 3f);
                }
            }
        }

        public void SetFOVState(CameraFOVState state)
        {
            if (activeFOVState == state && fovTween != null) return;
            activeFOVState = state;
            CameraStateProfile profile = GetProfile(state);

            // Reset manual offset on state change? No, let's keep it for variable zoom.
            
            fovTween?.Kill();
            fovTween = DOVirtual.Float(stateFOV, profile.fieldOfView, profile.fovBlendTime, v => stateFOV = v).SetEase(profile.fovEase);

            dutchTween?.Kill();
            dutchTween = DOVirtual.Float(currentDutch, profile.dutch, profile.dutchBlendTime, v => currentDutch = v).SetEase(profile.dutchEase);
        }

        private CameraStateProfile GetProfile(CameraFOVState state)
        {
            if (state == CameraFOVState.Aim) return aimProfile;
            return walkProfile; 
        }

        // Methods to keep Parkour system happy
        public void newOffset(bool o) {}
        public void SetWallRunTilt(float a) {}
        public void ResetTilt() {}
        public void SetCameraRotationLock(bool l) {}
        public void ShakeMedium() {}
        public void ShakeHeavy() {}
        public void ShakeLight() {}
    }
}
