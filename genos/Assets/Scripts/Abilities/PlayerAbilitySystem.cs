using UnityEngine;
using UnityEngine.InputSystem;
using Climbing;

namespace Climbing.Abilities
{
    public class PlayerAbilitySystem : MonoBehaviour
    {
        private InputCharacterController input;
        private RocketArmAbility rocketArmAbility;
        private WireAbility wireAbility;
        private CameraController camController;
        private UI.AbilityUIController uiController;

        private float aimPressTime;
        private bool isAimed = false;
        private const float quickshotWindow = 0.2f;

        void Awake()
        {
            input = GetComponent<InputCharacterController>();
            rocketArmAbility = GetComponent<RocketArmAbility>();
            wireAbility = GetComponent<WireAbility>();
            camController = Object.FindFirstObjectByType<CameraController>();
            uiController = Object.FindFirstObjectByType<UI.AbilityUIController>();
        }

        void Update()
        {
            if (input == null) return;

            // 1. AIM LOGIC - Immediate switch
            if (input.aim)
            {
                if (!isAimed)
                {
                    isAimed = true;
                    aimPressTime = Time.time;
                    if (camController != null) camController.SetFOVState(CameraFOVState.Aim);
                    if (uiController != null) uiController.SetCrosshairVisible(true);
                }
            }
            else
            {
                if (isAimed)
                {
                    isAimed = false;
                    if (camController != null) camController.SetFOVState(CameraFOVState.Walk);
                    if (uiController != null) uiController.SetCrosshairVisible(false);
                }
            }

            // 2. FIRE LOGIC
            if (input.ConsumeFirePressedBuffered())
            {
                // Quickshot: Both buttons pressed nearly at once
                // Since we transition camera immediately now, we check the time since aim start
                bool quick = (Time.time - aimPressTime < quickshotWindow);
                
                // Allow firing only if Aiming is held
                if (input.aim)
                {
                    rocketArmAbility?.UseAbility(quick);
                }
            }

            // 3. WIRE TRAP LOGIC
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                wireAbility?.UseAbility();
            }
        }
    }
}
