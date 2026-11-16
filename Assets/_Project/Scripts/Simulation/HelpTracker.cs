using System;
using UnityEngine;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Measures help usage: how many times the assembled furniture was shown and for how long.
    /// The help view calls Open while the button is held and Close when it is released.
    /// </summary>
    [DisallowMultipleComponent]
    public class HelpTracker : MonoBehaviour
    {
        public event Action HelpOpened;

        /// <summary>The help was closed. Receives how long it was shown, in seconds.</summary>
        public event Action<float> HelpClosed;

        public bool IsOpen { get; private set; }
        public int Requests { get; private set; }

        /// <summary>Total time the help was shown, including the current view if it is open.</summary>
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
            HelpOpened?.Invoke();
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
            HelpClosed?.Invoke(duration);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // If the window loses focus while the button is held, the release never arrives
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
