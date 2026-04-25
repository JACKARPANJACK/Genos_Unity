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
    public class VaultObstacle : VaultAction
    {
        private Vector3 leftHandPosition;
        private Quaternion leftHandRotation;
        private string HandAnimVariableName;

        public VaultObstacle(ThirdPersonController _vaultingController, Action _actionInfo) : base(_vaultingController, _actionInfo)
        {
            ActionVaultObstacle action = (ActionVaultObstacle)_actionInfo;

            //Loads Action Info
            HandAnimVariableName = action.HandAnimVariableName;
        }

        /// <summary>
        /// Checks if can Vault the Fence Obstacle
        /// </summary>
        public override bool CheckAction()
        {
            if (WantsAutoParkour() && CanStartTraversal())
            {
                RaycastHit hit;
                Vector3 origin = controller.transform.position + kneeRaycastOrigin;
                Vector3 moveDirection = GetObstacleMoveDirection();

                //Checks if Vault obstacle in front
                if (controller.characterDetection.ThrowRayOnDirection(origin, moveDirection, kneeRaycastLength, out hit))
                {
                    if (hit.transform.tag != tag)
                        return false;

                    // Allow any angle
                    Vector3 horizontalMove = Vector3.ProjectOnPlane(moveDirection, Vector3.up).normalized;
                    Vector3 horizontalNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                    if (Vector3.Dot(-horizontalNormal, horizontalMove) < -0.5f)
                        return false;

                    //Gets Fence width and adds an offset for the downward ray
                    Vector3 origin2 = origin + (-hit.normal * (hit.transform.localScale.z + landOffset));

                    RaycastHit hit2;
                    //Get landing position
                    if (controller.characterDetection.ThrowRayOnDirection(origin2, Vector3.down, 10, out hit2))
                    {
                        if (hit2.collider)
                        {
                            controller.characterAnimation.animator.CrossFade("Vaulting", 0.2f);

                            startPos = controller.transform.position;
                            startRot = controller.transform.rotation;
                            targetPos = hit2.point;
                            targetRot = Quaternion.LookRotation(targetPos - startPos);
                            vaultTime = startDelay; //This adds a delay to allow animation start in correct time
                            animLength = clip.length + startDelay;
                            controller.DisableController();

                            //Calculate Hand Rest Position n Rotation
                            Vector3 left = Vector3.Cross(hit.normal, Vector3.up);
                            leftHandPosition = hit.point + (-hit.normal * (hit.transform.localScale.z / 2));
                            leftHandPosition.y = hit.transform.position.y + hit.transform.localScale.y / 2;
                            leftHandPosition.x += left.x * animator.animator.GetBoneTransform(HumanBodyBones.LeftHand).localPosition.x;
                            leftHandRotation = Quaternion.LookRotation(-hit.normal, Vector3.up);

                            return true;
                        }
                    }
                }
            }            

            return false;
        }

        /// <summary>
        /// Executes Vaulting Animation
        /// </summary>
        public override bool Update()
        {
            if (controller.isVaulting)
            {
                vaultTime += GetTraversalDelta();

                if (vaultTime > 1)
                {
                    controller.EnableController();
                }
                else
                {
                    if (vaultTime >= 0)
                    {
                        controller.transform.rotation = Quaternion.Lerp(startRot, targetRot, vaultTime * 4);
                        controller.transform.position = Vector3.Lerp(startPos, targetPos, vaultTime);
                    }
                    return true;
                }
            }

            return false;
        }

        public override void OnAnimatorIK(int layerIndex)
        {
            if (!controller.isVaulting)
                return;

            float curve = animator.animator.GetFloat(HandAnimVariableName);
            animator.animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, curve);
            animator.animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandPosition);
            animator.animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, curve);
            animator.animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandRotation);
        }

        public override void DrawGizmos()
        {
            Gizmos.DrawSphere(targetPos, 0.08f);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(leftHandPosition, 0.08f);
        }

        private Vector3 GetObstacleMoveDirection()
        {
            Transform reference = controller.freeCamera != null ? controller.freeCamera : controller.mainCamera;
            Vector2 input = controller.characterInput.movement;

            if (reference != null)
            {
                Vector3 forward = reference.forward;
                Vector3 right = reference.right;

                forward.y = 0f;
                right.y = 0f;

                forward.Normalize();
                right.Normalize();

                Vector3 desiredMove = forward * input.y + right * input.x;
                if (desiredMove.sqrMagnitude > 0.001f)
                    return desiredMove.normalized;
            }

            Vector3 velocity = controller.characterMovement.rb != null ? controller.characterMovement.rb.linearVelocity : Vector3.zero;
            velocity.y = 0f;

            if (velocity.sqrMagnitude > 0.001f)
                return velocity.normalized;

            return controller.transform.forward;
        }
    }
}
