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
using UnityEngine.InputSystem;

namespace Climbing
{
    public class InputCharacterController : MonoBehaviour
    {
        private PlayerControls controls = null;
        [SerializeField] private float inputBufferTime = 0.15f;

        [HideInInspector] public Vector2 movement;
        [HideInInspector] public bool run;
        [HideInInspector] public bool jump;
        [HideInInspector] public bool drop;
        [HideInInspector] public bool dash;
        [HideInInspector] public bool aim;
        [HideInInspector] public bool fire;
        [HideInInspector] public bool lightAttack;
        [HideInInspector] public bool heavyAttack;
        [HideInInspector] public bool doubleTapDashTriggered;
        private float _cycleDelta;
        private bool cycleDeltaConsumed = false;

        public float GetCycleDelta()
        {
            if (cycleDeltaConsumed) return 0f;
            return _cycleDelta;
        }

        public void ConsumeCycleDelta()
        {
            cycleDeltaConsumed = true;
        }

        private bool _runHeld = false;
        private bool _jumpHeld = false;
        private bool _dropHeld = false;
        private bool _dashHeld = false;
        private bool _aimHeld = false;
        private bool _fireHeld = false;
        private bool _lightAttackHeld = false;
        private bool _heavyAttackHeld = false;

        private InputAction dashAction;
        private float lastJumpPressedTime = float.NegativeInfinity;
        private float lastJumpReleasedTime = float.NegativeInfinity;
        private float lastDropPressedTime = float.NegativeInfinity;
        private float lastDashPressedTime = float.NegativeInfinity;
        private float lastFirePressedTime = float.NegativeInfinity;
        private float lastLightAttackPressedTime = float.NegativeInfinity;
        private float lastHeavyAttackPressedTime = float.NegativeInfinity;

        private float consumedJumpPressedTime = float.NegativeInfinity;
        private float consumedJumpReleasedTime = float.NegativeInfinity;
        private float consumedDropPressedTime = float.NegativeInfinity;
        private float consumedDashPressedTime = float.NegativeInfinity;
        private float consumedFirePressedTime = float.NegativeInfinity;
        private float consumedLightAttackPressedTime = float.NegativeInfinity;
        private float consumedHeavyAttackPressedTime = float.NegativeInfinity;

        [Header("Double Tap Settings")]
        public float doubleTapTimeFrame = 0.3f;
        private Vector2 lastMovementInput = Vector2.zero;
        private float lastMovementPressTime = float.NegativeInfinity;
        private Vector2 currentMovementDirection = Vector2.zero;

        private void OnEnable()
        {
            if(controls != null)
                controls.Enable();

            if (dashAction != null)
                dashAction.Enable();
        }

        private void OnDisable()
        {
            if (controls != null)
                controls.Disable();

            if (dashAction != null)
                dashAction.Disable();
        }

        void Awake()
        {
            controls = new PlayerControls();

            // Movement
            controls.Player.Movement.performed += ctx =>
            {
                Vector2 newMove = ctx.ReadValue<Vector2>();
                if (newMove.sqrMagnitude > 0.5f)
                {
                    if (Time.time - lastMovementPressTime <= doubleTapTimeFrame)
                    {
                        float dot = Vector2.Dot(newMove.normalized, currentMovementDirection);
                        if (dot > 0.7f && lastMovementInput.sqrMagnitude < 0.3f) doubleTapDashTriggered = true;
                    }
                    currentMovementDirection = newMove.normalized;
                    lastMovementPressTime = Time.time;
                }
                movement = newMove;
                lastMovementInput = newMove;
            };
            controls.Player.Movement.canceled += ctx => { movement = Vector2.zero; lastMovementInput = Vector2.zero; };

            // Jump
            controls.Player.Jump.performed += ctx => { _jumpHeld = true; lastJumpPressedTime = Time.time; };
            controls.Player.Jump.canceled += ctx => { _jumpHeld = false; lastJumpReleasedTime = Time.time; };

            // Drop
            controls.Player.Drop.performed += ctx => { _dropHeld = true; lastDropPressedTime = Time.time; };
            controls.Player.Drop.canceled += ctx => _dropHeld = false;

            // Run
            controls.Player.Run.performed += ctx => _runHeld = true;
            controls.Player.Run.canceled += ctx => _runHeld = false;

            // Dash
            dashAction = controls.Player.Dash;
            controls.Player.Dash.performed += ctx => { _dashHeld = true; lastDashPressedTime = Time.time; };
            controls.Player.Dash.canceled += ctx => _dashHeld = false;

            // Aim
            controls.Player.Aim.performed += ctx => _aimHeld = true;
            controls.Player.Aim.canceled += ctx => _aimHeld = false;

            // Fire
            controls.Player.Fire.performed += ctx => { _fireHeld = true; lastFirePressedTime = Time.time; };
            controls.Player.Fire.canceled += ctx => _fireHeld = false;

            // Light Attack
            controls.Player.LightAttack.performed += ctx => { _lightAttackHeld = true; lastLightAttackPressedTime = Time.time; };
            controls.Player.LightAttack.canceled += ctx => _lightAttackHeld = false;

            // Heavy Attack
            controls.Player.HeavyAttack.performed += ctx => { _heavyAttackHeld = true; lastHeavyAttackPressedTime = Time.time; };
            controls.Player.HeavyAttack.canceled += ctx => _heavyAttackHeld = false;

            // Cycle
            controls.Player.CycleMode.performed += ctx => { _cycleDelta = ctx.ReadValue<float>(); cycleDeltaConsumed = false; };
            controls.Player.CycleMode.canceled += ctx => { _cycleDelta = 0; cycleDeltaConsumed = false; };

            // Exit
            controls.GameManager.Exit.performed += ctx => Exit();
        }

        private void Update()
        {
            run = _runHeld;
            jump = _jumpHeld;
            drop = _dropHeld;
            dash = _dashHeld;
            aim = _aimHeld;
            fire = _fireHeld;
            lightAttack = _lightAttackHeld;
            heavyAttack = _heavyAttackHeld;

            if (ConsumeDashPressedBuffered()) doubleTapDashTriggered = true;
        }

        private void OnDestroy() { if (controls != null) controls.Dispose(); }
        void Exit() => Application.Quit();

        public bool JumpPressedBuffered(float customBuffer = -1f) => IsBuffered(lastJumpPressedTime, customBuffer);
        public bool ConsumeJumpPressedBuffered(float customBuffer = -1f)
        {
            if (!JumpPressedBuffered(customBuffer) || consumedJumpPressedTime == lastJumpPressedTime) return false;
            consumedJumpPressedTime = lastJumpPressedTime;
            return true;
        }

        public bool DashPressedBuffered(float customBuffer = -1f) => IsBuffered(lastDashPressedTime, customBuffer);
        public bool ConsumeDashPressedBuffered(float customBuffer = -1f)
        {
            if (!DashPressedBuffered(customBuffer) || consumedDashPressedTime == lastDashPressedTime) return false;
            consumedDashPressedTime = lastDashPressedTime;
            return true;
        }

        public bool FirePressedBuffered(float customBuffer = -1f) => IsBuffered(lastFirePressedTime, customBuffer);
        public bool ConsumeFirePressedBuffered(float customBuffer = -1f)
        {
            if (!FirePressedBuffered(customBuffer) || consumedFirePressedTime == lastFirePressedTime) return false;
            consumedFirePressedTime = lastFirePressedTime;
            return true;
        }

        public bool LightAttackPressedBuffered(float customBuffer = -1f) => IsBuffered(lastLightAttackPressedTime, customBuffer);
        public bool ConsumeLightAttackPressedBuffered(float customBuffer = -1f)
        {
            if (!LightAttackPressedBuffered(customBuffer) || consumedLightAttackPressedTime == lastLightAttackPressedTime) return false;
            consumedLightAttackPressedTime = lastLightAttackPressedTime;
            return true;
        }

        public bool HeavyAttackPressedBuffered(float customBuffer = -1f) => IsBuffered(lastHeavyAttackPressedTime, customBuffer);
        public bool ConsumeHeavyAttackPressedBuffered(float customBuffer = -1f)
        {
            if (!HeavyAttackPressedBuffered(customBuffer) || consumedHeavyAttackPressedTime == lastHeavyAttackPressedTime) return false;
            consumedHeavyAttackPressedTime = lastHeavyAttackPressedTime;
            return true;
        }

        public bool ConsumeDoubleTapDashBuffered(float customBuffer = -1f)
        {
            if (doubleTapDashTriggered) { doubleTapDashTriggered = false; return true; }
            return false;
        }

        private bool IsBuffered(float timestamp, float customBuffer)
        {
            float buffer = customBuffer >= 0f ? customBuffer : inputBufferTime;
            return Time.time - timestamp <= buffer;
        }
    }
}
