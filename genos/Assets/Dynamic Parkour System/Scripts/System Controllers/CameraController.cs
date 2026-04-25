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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;

namespace Climbing
{
    public class CameraController : MonoBehaviour
    {
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

#pragma warning disable CS0618
        private CinemachineFreeLook freeLookCamera;
        private float[] originalRadii = new float[3];
#pragma warning restore CS0618



        void Start()
        {
            cameraOffset = GetComponent<CinemachineCameraOffset>();
            baseOffset = cameraOffset.Offset;

            // Optional: Automatically configure CinemachineFreeLook to modern DMC/Zelda style
#pragma warning disable CS0618 // Type or member is obsolete
            freeLookCamera = GetComponent<CinemachineFreeLook>();
            if (freeLookCamera != null)
            {
                // Set specific orbits to look like action games
                freeLookCamera.m_Orbits[0].m_Height = 4.0f;
                freeLookCamera.m_Orbits[0].m_Radius = 2.5f;

                freeLookCamera.m_Orbits[1].m_Height = 1.5f;
                freeLookCamera.m_Orbits[1].m_Radius = 3.5f;

                freeLookCamera.m_Orbits[2].m_Height = 0.2f;
                freeLookCamera.m_Orbits[2].m_Radius = 1.5f;

                for (int i = 0; i < 3; i++)
                {
                    originalRadii[i] = freeLookCamera.m_Orbits[i].m_Radius;
                }

                freeLookCamera.m_YAxis.m_MaxSpeed = 4f;
                freeLookCamera.m_XAxis.m_MaxSpeed = 300f;

                // Auto-center camera carefully to track the player's back
                freeLookCamera.m_Heading.m_Definition = CinemachineOrbitalTransposer.Heading.HeadingDefinition.TargetForward;

                // Keep recentering active, waiting slightly before realigning behind the back
                freeLookCamera.m_RecenterToTargetHeading.m_enabled = true;
                freeLookCamera.m_RecenterToTargetHeading.m_WaitTime = 2.0f; // Don't fight the user! Wait 2 full seconds of no camera input.
                freeLookCamera.m_RecenterToTargetHeading.m_RecenteringTime = 1.0f; // Smooth, gentle drift behind the player
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }


        void Update()
        {
            // Zoom Logic
#pragma warning disable CS0618
            if (freeLookCamera != null)
            {
                float scrollDelta = 0f;

                // Safely grab scroll input from whatever system is active
                try { scrollDelta = UnityEngine.Input.mouseScrollDelta.y; } catch {}
                if (Mathf.Abs(scrollDelta) < 0.01f) {
                    try { scrollDelta = UnityEngine.Input.GetAxis("Mouse ScrollWheel"); } catch {}
                }
                if (Mathf.Abs(scrollDelta) < 0.01f) {
                    try { 
                        if (UnityEngine.InputSystem.Mouse.current != null)
                            scrollDelta = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
                    } catch {}
                }

                if (Mathf.Abs(scrollDelta) > 0.01f)
                {
                    // Zoom IN (decreasing radius) or OUT
                    float normalizedScroll = Mathf.Clamp(scrollDelta, -1f, 1f);
                    // Use a slightly larger step (0.25) so it feels responsive immediately
                    currentZoom -= normalizedScroll * (zoomSpeed * 0.25f); 
                    currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
                }

                // Explicitly re-assign the array elements to ensure Cinemachine properties update correctly
                var orbits = freeLookCamera.m_Orbits;
                for (int i = 0; i < 3; i++)
                {
                    float targetRadius = originalRadii[i] * currentZoom;
                    orbits[i].m_Radius = Mathf.Lerp(orbits[i].m_Radius, targetRadius, Time.deltaTime * 10f);
                }
                freeLookCamera.m_Orbits = orbits; 
            }
#pragma warning restore CS0618

            //Lerps Camera Position to the new offset
            if (isIdle && offsetTween == null)
            {
                // Idle random movement
                float offsetX = Mathf.PerlinNoise(Time.time * swaySpeed, 0f) * swayAmount - (swayAmount / 2f);
                float offsetY = Mathf.PerlinNoise(0f, Time.time * swaySpeed) * swayAmount - (swayAmount / 2f);
                cameraOffset.Offset = baseOffset + new Vector3(offsetX, offsetY, 0f);
            }
        }

        /// <summary>
        /// Adds Offset to the camera while being on Climbing or inGround
        /// </summary>
        public void newOffset(bool offset)
        {
            if (offset)
                _target = _offset;
            else
                _target = _default;

            offsetTween?.Kill();
            offsetTween = DOVirtual.Vector3(cameraOffset.Offset, _target, maxTime, (val) => 
            {
                cameraOffset.Offset = val;
                baseOffset = val;
            })
            .SetEase(Ease.InOutSine)
            .OnComplete(() => {
                baseOffset = _target;
                offsetTween = null;
            });
        }
    }
}