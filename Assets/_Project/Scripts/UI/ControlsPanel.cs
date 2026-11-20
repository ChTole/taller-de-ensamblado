using AssemblyWorkshop.Common;
using AssemblyWorkshop.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace AssemblyWorkshop.UI
{
    /// <summary>
    /// Controls panel in the simulation. The "Controles" button calls Toggle from On Click ();
    /// the optional close button inside the panel closes it. ControlsTracker measures each view.
    /// The panel background should keep Raycast Target enabled, so pieces behind it cannot be grabbed while it is open.
    /// </summary>
    [DisallowMultipleComponent]
    public class ControlsPanel : MonoBehaviour
    {
        [SerializeField] private ControlsTracker tracker;
        [SerializeField] private GameObject panel;

        [Tooltip("Optional. Button inside the panel that closes it.")]
        [SerializeField] private Button closeButton;

        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(tracker, nameof(tracker), this)
                & Guard.Required(panel, nameof(panel), this);

            if (!isValid)
            {
                enabled = false;
                return;
            }

            panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (!isValid)
            {
                return;
            }

            tracker.Opened += OnOpened;
            tracker.Closed += OnClosed;
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
        }

        private void OnDisable()
        {
            if (!isValid)
            {
                return;
            }

            tracker.Opened -= OnOpened;
            tracker.Closed -= OnClosed;
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }
        }

        public void Toggle()
        {
            if (isValid)
            {
                tracker.Toggle();
            }
        }

        public void Close()
        {
            if (isValid)
            {
                tracker.Close();
            }
        }

        private void OnOpened()
        {
            panel.SetActive(true);
        }

        private void OnClosed(float duration)
        {
            // Also covers closes that do not come from the buttons, such as the assembly ending
            panel.SetActive(false);
        }
    }
}