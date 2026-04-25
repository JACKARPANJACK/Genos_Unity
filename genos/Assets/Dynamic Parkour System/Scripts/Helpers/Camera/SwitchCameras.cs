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

namespace Climbing
{
    public class SwitchCameras : MonoBehaviour
    {
        // Start is called before the first frame update
        Animator animator;

        enum CameraType
        {
            None,
            Freelook,
            Slide,
            IdleCutscene
        }

        CameraType curCam = CameraType.None;

        [SerializeField] private CinemachineCamera FreeLook;
        [SerializeField] private CinemachineCamera Slide;
        [SerializeField] private CinemachineCamera IdleCutscene;

        [Header("Idle Camera Panning")]
        [SerializeField] private float panSpeed = 5f; // Degrees per second
        private Transform currentPlayerTransform;
        private Transform currentHeadTransform;
        private float panAngle = 15f; // Start slightly off-center
        private float panRadius = 1.55f;
        private float panHeight = 1.4f;

        void Start()
        {
            animator = GetComponent<Animator>();

            FreeLookCam();
        }

        private void Update()
        {
            if (curCam == CameraType.IdleCutscene && IdleCutscene != null && currentPlayerTransform != null)
            {
                // Smooth orbit logic
                panAngle += panSpeed * Time.deltaTime;

                // Calculate the orbit position relative to the player
                float x = Mathf.Sin(panAngle * Mathf.Deg2Rad) * panRadius;
                float z = Mathf.Cos(panAngle * Mathf.Deg2Rad) * panRadius;

                Vector3 newLocalPos = new Vector3(x, panHeight, z);

                // Lerp towards the destination position to avoid harsh jumping
                IdleCutscene.transform.localPosition = Vector3.Lerp(
                    IdleCutscene.transform.localPosition, 
                    newLocalPos, 
                    Time.deltaTime * 2.5f
                );

                if (currentHeadTransform == null)
                {
                    // Fallback to manually looking at the player's face space locally
                    Vector3 lookDir = new Vector3(0f, panHeight, 0f) - IdleCutscene.transform.localPosition;
                    IdleCutscene.transform.localRotation = Quaternion.Slerp(
                        IdleCutscene.transform.localRotation,
                        Quaternion.LookRotation(lookDir.normalized),
                        Time.deltaTime * 5f
                    );
                }
            }
        }

        //Switches To FreeLook Cam
        public void FreeLookCam()
        {
            if (curCam != CameraType.Freelook)
            {
                Slide.Priority = 0;
                if (IdleCutscene != null) 
                {
                    IdleCutscene.Priority = 0;
                    IdleCutscene.transform.SetParent(null); // Unparent so it doesn't spin wildly when player resumes movement
                    currentPlayerTransform = null;
                    currentHeadTransform = null;
                }
                FreeLook.Priority = 1;
                curCam = CameraType.Freelook;
            }
        }

        //Switches To Slide Cam
        public void SlideCam()
        {
            if (curCam != CameraType.Slide)
            {
                FreeLook.Priority = 0;
                if (IdleCutscene != null) IdleCutscene.Priority = 0;
                Slide.Priority = 1;
                curCam = CameraType.Slide;
            }
        }

        //Switches To Idle Cutscene Cam
        public void IdleCutsceneCam(Transform playerTransform = null, Transform headTransform = null)
        {
            if (curCam != CameraType.IdleCutscene && IdleCutscene != null)
            {
                FreeLook.Priority = 0;
                Slide.Priority = 0;

                if (playerTransform != null)
                {
                    // Store references
                    currentPlayerTransform = playerTransform;
                    currentHeadTransform = headTransform;
                    panAngle = 15f; // Hard reset to right-side start of face (0 is pure center face, 180 is pure back)

                    // Physically attach the camera to the player to guarantee perfectly rigid positioning
                    IdleCutscene.transform.SetParent(playerTransform);

                    // Place the camera 1.5m in front, 1.4m high (face level), and slightly offset initially (x=0.4m)
                    Vector3 targetLocalPos = new Vector3(Mathf.Sin(panAngle * Mathf.Deg2Rad) * panRadius, panHeight, Mathf.Cos(panAngle * Mathf.Deg2Rad) * panRadius);

                    // Instantly snap to the starting position so the initial Cinemachine blend looks valid 
                    IdleCutscene.transform.localPosition = targetLocalPos;

                    if (headTransform != null)
                    {
                        // Assign LookAt so Cinemachine handles small secondary head tracking
                        IdleCutscene.LookAt = headTransform;
                    }
                    else
                    {
                        // Aim backwards directly at the player's face (which is roughly 0, 1.4, 0 locally)
                        Vector3 lookDir = new Vector3(0f, panHeight, 0f) - targetLocalPos;
                        IdleCutscene.transform.localRotation = Quaternion.LookRotation(lookDir.normalized);
                        IdleCutscene.LookAt = null;
                    }

                    // Force clear Cinemachine follow so no inspector presets override our manual offset math
                    IdleCutscene.Follow = null;
                }

                IdleCutscene.Priority = 1;
                curCam = CameraType.IdleCutscene;
            }
        }
    }
}
