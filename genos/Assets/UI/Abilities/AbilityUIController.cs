using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using Climbing.Abilities;

namespace Climbing.UI
{
    public class AbilityUIController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        
        private Label weaponLabel;
        private Label notificationLabel;
        private VisualElement crosshair;
        
        private Coroutine notificationCoroutine;

        private void OnEnable()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            var root = uiDocument.rootVisualElement;
            
            weaponLabel = root.Q<Label>("weaponLabel");
            notificationLabel = root.Q<Label>("notificationLabel");
            crosshair = root.Q<VisualElement>("crosshair");
            
            notificationLabel.style.opacity = 0;
        }

        public void UpdateWeaponDisplay(RocketArmType type)
        {
            if (weaponLabel != null)
            {
                weaponLabel.text = type.ToString() + " Rocket Arm";
            }
            
            ShowNotification(type.ToString().ToUpper() + " MODE");
        }

        public void ShowNotification(string text)
        {
            if (notificationLabel == null) return;
            
            notificationLabel.text = text;
            if (notificationCoroutine != null) StopCoroutine(notificationCoroutine);
            notificationCoroutine = StartCoroutine(NotificationSequence());
        }

        private IEnumerator NotificationSequence()
        {
            notificationLabel.style.opacity = 1;
            yield return new WaitForSeconds(2f);
            notificationLabel.style.opacity = 0;
        }

        public void SetCrosshairVisible(bool visible)
        {
            if (crosshair != null)
            {
                if (visible) crosshair.AddToClassList("crosshair-visible");
                else crosshair.RemoveFromClassList("crosshair-visible");
            }
        }

        public void SetCrosshairTargeting(bool isTargeting)
        {
            if (crosshair != null)
            {
                if (isTargeting) crosshair.AddToClassList("crosshair-targeting");
                else crosshair.RemoveFromClassList("crosshair-targeting");
            }
        }
    }
}
