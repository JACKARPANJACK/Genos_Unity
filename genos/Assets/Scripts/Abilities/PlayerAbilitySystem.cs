using UnityEngine;
using UnityEngine.InputSystem;
using Climbing;

namespace Climbing.Abilities
{
    public class PlayerAbilitySystem : MonoBehaviour
    {
        private InputCharacterController input;
        private RocketArmAbility rocketArmAbility;
        private CameraController camController;
        private SwitchCameras switchCameras;
        private UI.AbilityUIController uiController;

        void Awake()
        {
            input = GetComponent<InputCharacterController>();
            rocketArmAbility = GetComponent<RocketArmAbility>();
            camController = Object.FindFirstObjectByType<CameraController>();
            switchCameras = Object.FindFirstObjectByType<SwitchCameras>();
            uiController = Object.FindFirstObjectByType<UI.AbilityUIController>();
        }

        void Update()
        {
            if (input == null) return;

            // ROCKET ARM LOGIC (Q key via Input System)
            if (input.ConsumeRocketArmPressedBuffered())
            {
                rocketArmAbility?.UseAbility();
            }
}
    }
}
