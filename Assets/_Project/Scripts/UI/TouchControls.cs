using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AssemblyWorkshop.UI
{
    /// <summary>
    /// Shows or hides the on-screen buttons for touch screens.
    /// Automatic by default: they appear on mobile browsers or as soon as the screen is touched.
    /// An optional Toggle lets the user force them on or off; the choice is remembered.
    /// </summary>
    [DisallowMultipleComponent]
    public class TouchControls : MonoBehaviour
    {
        private const string PreferenceKey = "AssemblyWorkshop.TouchControls";

        private enum Mode
        {
            Automatic = 0,
            On = 1,
            Off = 2
        }

        [Tooltip("Panel with the touch buttons: rotations, vertical mode, undo and remove.")]
        [SerializeField] private GameObject touchPanel;

        [Tooltip("Optional. Toggle to show or hide the touch buttons manually.")]
        [SerializeField] private Toggle visibilityToggle;

        [Tooltip("Optional. Vertical mode Toggle inside the panel. It is turned off when the panel is hidden.")]
        [SerializeField] private Toggle verticalModeToggle;

        private Mode mode;
        private bool touchDetected;

        private void Awake()
        {
            if (touchPanel == null)
            {
                Debug.LogError($"[{nameof(TouchControls)}] 'touchPanel' is not assigned.", this);
                enabled = false;
                return;
            }

            mode = LoadMode();
            Apply();
        }

        private void OnEnable()
        {
            if (visibilityToggle != null)
            {
                visibilityToggle.onValueChanged.AddListener(OnToggleChanged);
            }
        }

        private void OnDisable()
        {
            if (visibilityToggle != null)
            {
                visibilityToggle.onValueChanged.RemoveListener(OnToggleChanged);
            }
        }

        private void Update()
        {
            // In automatic mode, the first touch shows the buttons (for example, on tablets that report a desktop browser)
            if (mode != Mode.Automatic || touchDetected)
            {
                return;
            }

            Touchscreen screen = Touchscreen.current;
            if (screen != null && screen.primaryTouch.press.wasPressedThisFrame)
            {
                touchDetected = true;
                Apply();
            }
        }

        private void OnToggleChanged(bool isOn)
        {
            mode = isOn ? Mode.On : Mode.Off;
            SaveMode(mode);
            Apply();
        }

        private void Apply()
        {
            bool visible = mode == Mode.On
                || (mode == Mode.Automatic && (Application.isMobilePlatform || touchDetected));

            touchPanel.SetActive(visible);

            if (visibilityToggle != null)
            {
                visibilityToggle.SetIsOnWithoutNotify(visible);
            }

            if (!visible && verticalModeToggle != null)
            {
                verticalModeToggle.isOn = false;
            }
        }

        private static Mode LoadMode()
        {
            try
            {
                int value = PlayerPrefs.GetInt(PreferenceKey, (int)Mode.Automatic);
                return Enum.IsDefined(typeof(Mode), value) ? (Mode)value : Mode.Automatic;
            }
            catch (Exception)
            {
                return Mode.Automatic;
            }
        }

        private static void SaveMode(Mode value)
        {
            try
            {
                PlayerPrefs.SetInt(PreferenceKey, (int)value);
                PlayerPrefs.Save();
            }
            catch (Exception)
            {
                // Browser storage can be unavailable (private mode): the choice only lasts for this session
            }
        }
    }
}
