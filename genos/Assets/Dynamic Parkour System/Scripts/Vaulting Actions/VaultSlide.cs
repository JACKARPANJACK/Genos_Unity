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

namespace Climbing
{
    public class VaultSlide : VaultAction
    {
        private const float SlideLandingProbeHeight = 2.5f;
        private const float SlideLandingProbeDistance = 4.5f;
        private const float SlideLandingProbeStep = 0.35f;
        private const float SlideMaxRise = 2.5f;
        private const float SlideMaxDrop = 4.5f;

        private const float SlideMaxSlopeAngle = 55f;
        private const float SurfaceProbeHeight = 1.25f;
        private const float SurfaceProbeDistance = 2.25f;
        private const float GroundSnapHeight = 3f;
        private const float GroundSnapDistance = 6f;

        private float dis;
        private SlideMode slideMode = SlideMode.None;
        private Vector3 slideDirection;
        private Vector3 surfaceNormal;

        private enum SlideMode
        {
            None,
            Vault,
            Surface
        }

        public VaultSlide(ThirdPersonController _vaultingController, Action _actionInfo) : base(_vaultingController, _actionInfo)
        {
        }

        public override bool CheckAction()
        {
            if (!CanStartTraversal())
                return false;

            Vector3 moveDirection = GetSlideMoveDirection();
            if (moveDirection.sqrMagnitude <= 0.001f)
                return false;

            // Always test if it's an explicit "Slidable" surface to automatically trigger surface sliding
            if (TryStartSurfaceSlide(moveDirection))
                return true;

            bool wantsToSlide = (controller.characterInput.run && controller.characterInput.aim) || WantsAutoParkour(0.1f);
            if (!wantsToSlide)
                return false;

            if (TryStartVaultSlide(moveDirection))
                return true;

            return false;
        }

        public override bool Update()
        {
            if (!controller.isVaulting)
                return false;

            float slideSpeed = 12f;
            float distance = Vector3.Distance(startPos, targetPos);
            if (distance > 0.1f)
                vaultTime += Time.deltaTime * (slideSpeed / distance);
            else
                vaultTime += Time.deltaTime * slideSpeed;

            if (vaultTime > 1f)
            {
                controller.characterAnimation.animator.SetFloat("AnimSpeed", 1f);
                TrySetBool("isSliding", false);
                controller.characterAnimation.switchCameras.FreeLookCam();
                controller.EnableController();
                controller.characterMovement.EnableFeetIK(); // Re-enable feet IK
                slideMode = SlideMode.None;
                return false;
            }

            if (slideMode == SlideMode.Surface)
                UpdateSurfaceSlide();
            else if (slideMode == SlideMode.Vault)
                UpdateVaultSlide();

            return true;
        }

        private void TrySetBool(string paramName, bool value)
        {
            Animator animator = controller.characterAnimation.animator;
            if (animator == null) return;

            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == paramName && param.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(paramName, value);
                    break;
                }
            }
        }

        public override void DrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(targetPos, 0.08f);
        }

        // =========================================================
        // START ROUTES
        // =========================================================

        private bool TryStartVaultSlide(Vector3 moveDirection)
        {
            Vector3 origin = controller.transform.position + kneeRaycastOrigin;
            RaycastHit hit;

            if (!controller.characterDetection.ThrowRayOnDirection(origin, moveDirection, kneeRaycastLength, out hit))
                return false;

            if (!IsVaultSlideSurface(hit, moveDirection))
                return false;

            if (!TryFindVaultLanding(hit, moveDirection, out RaycastHit landingHit))
                return false;

            if (!landingHit.collider)
                return false;

            BeginSlide(landingHit.point, moveDirection, hit.normal, SlideMode.Vault);
            return true;
        }

        private bool TryStartSurfaceSlide(Vector3 moveDirection)
        {
            Vector3 flatMove = Vector3.ProjectOnPlane(moveDirection, Vector3.up);
            if (flatMove.sqrMagnitude <= 0.001f)
                return false;

            flatMove.Normalize();

            Vector3 origin = controller.transform.position + Vector3.up * SurfaceProbeHeight + flatMove * SurfaceProbeDistance;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit surfaceHit, SurfaceProbeHeight + GroundSnapDistance))
                return false;

            bool isManualSlide = controller.characterInput.run && controller.characterInput.aim;
            if (!IsExplicitSlideSurface(surfaceHit.collider) && !isManualSlide)
                return false;

            float slopeAngle = Vector3.Angle(surfaceHit.normal, Vector3.up);
            // Allow sliding up/down sloped surfaces
            if (slopeAngle > SlideMaxSlopeAngle + 30f)
                return false;

            BeginSlide(surfaceHit.point, moveDirection, surfaceHit.normal, SlideMode.Surface);
            return true;
        }

        // =========================================================
        // BEGIN / UPDATE
        // =========================================================

        private void BeginSlide(Vector3 landingPoint, Vector3 moveDirection, Vector3 hitNormal, SlideMode mode)
        {
            startPos = controller.transform.position;
            startRot = controller.transform.rotation;
            slideMode = mode;
            surfaceNormal = hitNormal;

            Vector3 flatMove = Vector3.ProjectOnPlane(moveDirection, Vector3.up);
            if (flatMove.sqrMagnitude < 0.001f)
                flatMove = controller.transform.forward;

            slideDirection = flatMove.normalized;

            if (slideMode == SlideMode.Surface)
            {
                Vector3 onSurface = Vector3.ProjectOnPlane(slideDirection, surfaceNormal);
                if (onSurface.sqrMagnitude > 0.001f)
                    slideDirection = onSurface.normalized;

                targetPos = FindSurfaceSlideEnd(startPos, slideDirection);

                Vector3 lookDir = Vector3.ProjectOnPlane(slideDirection, Vector3.up);
                targetRot = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir.normalized) : startRot;
            }
            else
            {
                targetPos = landingPoint;

                Vector3 lookDirection = Vector3.ProjectOnPlane(moveDirection, Vector3.up);
                targetRot = lookDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDirection.normalized) : startRot;
            }

            if (slideMode == SlideMode.Surface)
            {
                controller.characterAnimation.animator.CrossFade("Slide", 0.05f);
                TrySetBool("isSliding", true);
            }
            else
            {
                controller.characterAnimation.animator.CrossFade("Running Slide", 0.05f);
            }

            float distance = Vector3.Distance(startPos, targetPos);
            dis = distance > 0.01f ? Mathf.Clamp(4f / distance, 1f, 3f) : 1f;

            controller.characterAnimation.animator.SetFloat("AnimSpeed", dis);
            controller.characterAnimation.switchCameras.SlideCam();

            vaultTime = 0f;
            animLength = clip.length + startDelay;
            controller.DisableController(); // VaultingAction automatically switches off IK inside DisableController if mapped properly but lets just force it
            controller.characterMovement.enableFeetIK = false; // Disable IK explicitly during sliding
        }

        private void UpdateVaultSlide()
        {
            controller.transform.rotation = Quaternion.Lerp(startRot, targetRot, vaultTime * 4f);
            controller.transform.position = Vector3.Lerp(startPos, targetPos, vaultTime);
        }

        private void UpdateSurfaceSlide()
        {
            Vector3 wantedPos = Vector3.Lerp(startPos, targetPos, vaultTime);

            Vector3 probeOrigin = wantedPos + Vector3.up * SurfaceProbeHeight;
            if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit groundHit, SurfaceProbeHeight + GroundSnapDistance))
            {
                bool isManualSlide = slideMode == SlideMode.Surface && controller.characterInput.run && controller.characterInput.aim;
                if (IsExplicitSlideSurface(groundHit.collider) || isManualSlide)
                {
                    float slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
                    if (slopeAngle <= SlideMaxSlopeAngle + 30f)
                    {
                        wantedPos = groundHit.point;

                        Vector3 forwardOnGround = Vector3.ProjectOnPlane(slideDirection, groundHit.normal);
                        if (forwardOnGround.sqrMagnitude > 0.001f)
                        {
                            Quaternion desiredRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(forwardOnGround, Vector3.up).normalized);
                            controller.transform.rotation = Quaternion.Slerp(startRot, desiredRot, vaultTime * 4f);
                        }
                    }
                }
            }

            controller.transform.position = wantedPos;
        }

        // =========================================================
        // FILTERS
        // =========================================================

        private bool IsVaultSlideSurface(RaycastHit hit, Vector3 moveDirection)
        {
            if (!MatchesSlideTag(hit.collider))
                return false;

            Vector3 horizontalMove = Vector3.ProjectOnPlane(moveDirection, Vector3.up);
            if (horizontalMove.sqrMagnitude <= 0.001f)
                return false;

            horizontalMove.Normalize();

            Vector3 horizontalNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
            if (horizontalNormal.sqrMagnitude <= 0.001f)
                return false;

            horizontalNormal.Normalize();
            return Vector3.Dot(-horizontalNormal, horizontalMove) > -0.5f;
        }

        private bool IsExplicitSlideSurface(Collider col)
        {
            if (col == null)
                return false;

            return MatchesSlideTag(col);
        }

        private bool MatchesSlideTag(Collider col)
        {
            if (col == null)
                return false;

            string colliderTag = col.tag;

            return string.Equals(colliderTag, tag, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(colliderTag, "Slide", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(colliderTag, "Slidable", System.StringComparison.OrdinalIgnoreCase);
        }

        // =========================================================
        // LANDING / END POINTS
        // =========================================================

        private bool TryFindVaultLanding(RaycastHit obstacleHit, Vector3 moveDirection, out RaycastHit landingHit)
        {
            Bounds bounds = obstacleHit.collider.bounds;
            Vector3 horizontalMove = Vector3.ProjectOnPlane(moveDirection, Vector3.up).normalized;

            float obstacleDepth = GetProjectedBoundsExtent(bounds, horizontalMove) * 2f;
            float centerToHit = Vector3.Dot(obstacleHit.point - bounds.center, horizontalMove);
            float distanceToFarEdge = Mathf.Max(landOffset, GetProjectedBoundsExtent(bounds, horizontalMove) - centerToHit + landOffset);
            float startOffset = Mathf.Max(landOffset, distanceToFarEdge);
            float maxOffset = startOffset + obstacleDepth + SlideLandingProbeDistance;

            for (float offset = startOffset; offset <= maxOffset; offset += SlideLandingProbeStep)
            {
                Vector3 probeOrigin = obstacleHit.point + horizontalMove * offset + Vector3.up * SlideLandingProbeHeight;

                if (!Physics.Raycast(probeOrigin, Vector3.down, out landingHit, SlideLandingProbeHeight + SlideMaxDrop))
                    continue;

                if (landingHit.collider == obstacleHit.collider)
                    continue;

                float heightDelta = landingHit.point.y - controller.transform.position.y;
                if (heightDelta > SlideMaxRise || heightDelta < -SlideMaxDrop)
                    continue;

                return true;
            }

            landingHit = default;
            return false;
        }

        private Vector3 FindSurfaceSlideEnd(Vector3 start, Vector3 direction)
        {
            Vector3 end = start + direction * SlideLandingProbeDistance;

            if (Physics.Raycast(end + Vector3.up * GroundSnapHeight, Vector3.down, out RaycastHit snapHit, GroundSnapHeight + GroundSnapDistance))
            {
                if (IsExplicitSlideSurface(snapHit.collider))
                    end = snapHit.point;
            }

            return end;
        }

        private float GetProjectedBoundsExtent(Bounds bounds, Vector3 direction)
        {
            Vector3 absDir = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            return Vector3.Dot(bounds.extents, absDir);
        }

        private Vector3 GetSlideMoveDirection()
        {
            Vector2 input = controller.characterInput.movement;
            Transform reference = controller.transform; // Disable camera-based direction, use character transform instead

            Vector3 forward = reference.forward;
            Vector3 right = reference.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            Vector3 desiredMove = forward * input.y + right * input.x;
            if (desiredMove.sqrMagnitude > 0.001f)
                return desiredMove.normalized;

            Vector3 velocity = controller.characterMovement != null ? controller.characterMovement.rb.linearVelocity : Vector3.zero;
            velocity.y = 0f;

            if (velocity.sqrMagnitude > 0.001f)
                return velocity.normalized;

            return controller.transform.forward;
        }
    }
}