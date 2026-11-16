using AssemblyWorkshop.Common;
using AssemblyWorkshop.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AssemblyWorkshop.UI
{
    /// <summary>
    /// Goes on the help button. While it is held down, it shows the image of the assembled furniture
    /// and HelpTracker measures the time. Releasing it or sliding the pointer off it closes the help.
    /// </summary>
    [DisallowMultipleComponent]
    public class HelpButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private SimulationController controller;
        [SerializeField] private HelpTracker tracker;

        [Tooltip("Panel shown while the button is held. It must not overlap the button and must not receive raycasts.")]
        [SerializeField] private GameObject helpPanel;

        [Tooltip("Image inside the panel. Receives the furniture thumbnail.")]
        [SerializeField] private Image helpImage;

        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(controller, nameof(controller), this)
                & Guard.Required(tracker, nameof(tracker), this)
                & Guard.Required(helpPanel, nameof(helpPanel), this)
                & Guard.Required(helpImage, nameof(helpImage), this);

            if (!isValid)
            {
                enabled = false;
                return;
            }

            helpPanel.SetActive(false);

            if (helpImage.raycastTarget)
            {
                Guard.Warning(this, "'Raycast Target' is enabled on the help image: if it covers the button, the help closes right after opening.");
            }
        }

        private void OnEnable()
        {
            if (isValid)
            {
                tracker.HelpClosed += OnHelpClosed;
            }
        }

        private void OnDisable()
        {
            if (!isValid)
            {
                return;
            }

            tracker.HelpClosed -= OnHelpClosed;
            tracker.Close();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isValid || !controller.IsRunning || controller.Furniture == null)
            {
                return;
            }

            if (controller.Furniture.Thumbnail == null)
            {
                Guard.Warning(this, $"Furniture '{controller.Furniture.FurnitureId}' has no 'Thumbnail' to show as help.");
                return;
            }

            helpImage.sprite = controller.Furniture.Thumbnail;
            helpImage.preserveAspect = true;
            helpPanel.SetActive(true);
            tracker.Open();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Close();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Close();
        }

        private void Close()
        {
            if (isValid)
            {
                tracker.Close();
            }
        }

        private void OnHelpClosed(float duration)
        {
            // Also covers closes that do not come from the button, such as the window losing focus
            helpPanel.SetActive(false);
        }
    }
}
