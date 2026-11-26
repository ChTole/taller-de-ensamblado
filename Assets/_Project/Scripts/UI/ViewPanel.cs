using AssemblyWorkshop.Common;
using AssemblyWorkshop.Simulation;
using UnityEngine;

namespace AssemblyWorkshop.UI
{
    /// <summary>
    /// A view that opens and closes with a button, measured by a ViewTracker (help, controls).
    /// The button calls Toggle from On Click (). Opening it can close other views, so only one is shown at a time.
    /// It must be on an object that stays active, not on the panel itself; several can share the same object.
    /// </summary>
    public class ViewPanel : MonoBehaviour
    {
        [Tooltip("Tracker that measures this view: HelpTracker or ControlsTracker.")]
        [SerializeField] private ViewTracker tracker;

        [Tooltip("Object shown while the view is open.")]
        [SerializeField] private GameObject panel;

        [Tooltip("Optional. Views closed when this one opens.")]
        [SerializeField] private ViewTracker[] closeOnOpen;

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
        }

        private void OnDisable()
        {
            if (!isValid)
            {
                return;
            }

            tracker.Opened -= OnOpened;
            tracker.Closed -= OnClosed;
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
            if (closeOnOpen != null)
            {
                foreach (ViewTracker other in closeOnOpen)
                {
                    if (other != null && other != tracker)
                    {
                        other.Close();
                    }
                }
            }

            panel.SetActive(true);
        }

        private void OnClosed(float duration)
        {
            panel.SetActive(false);
        }
    }
}