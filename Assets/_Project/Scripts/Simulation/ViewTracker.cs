using System;
using UnityEngine;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Measures how a view is consulted: how many times it was opened and for how long in total.
    /// Base class for HelpTracker and ControlsTracker. It only records; it never judges the use.
    /// </summary>
    public abstract class ViewTracker : MonoBehaviour
    {
        public event Action Opened;

        /// <summary>The view was closed. Receives how long it was open, in seconds.</summary>
        public event Action<float> Closed;

        public bool IsOpen { get; private set; }
        public int Requests { get; private set; }

        /// <summary>Total time the view was open, including the current one if it is open.</summary>
        public float TotalSeconds => totalSeconds + (IsOpen ? Time.time - openedAt : 0f);

        private float totalSeconds;
        private float openedAt;

        public void Open()
        {
            if (IsOpen || !isActiveAndEnabled)
            {
                return;
            }

            IsOpen = true;
            Requests++;
            openedAt = Time.time;
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            float duration = Time.time - openedAt;
            totalSeconds += duration;
            Closed?.Invoke(duration);
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Time away from the app is not time spent on the view
            if (!hasFocus)
            {
                Close();
            }
        }

        private void OnDisable()
        {
            Close();
        }
    }
}