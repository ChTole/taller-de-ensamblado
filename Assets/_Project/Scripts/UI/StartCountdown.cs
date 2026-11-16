using System.Collections;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AssemblyWorkshop.UI
{
    /// <summary>
    /// "Comenzar" button and countdown. The button is visible only when a furniture is selected.
    /// Pressing it hides the button and starts the countdown; when it reaches zero, the simulation is launched.
    /// While counting, the menu options are locked; the optional cancel button stops it.
    /// </summary>
    [DisallowMultipleComponent]
    public class StartCountdown : MonoBehaviour
    {
        [SerializeField] private FurnitureSelector selector;
        [SerializeField] private SimulationLauncher launcher;

        [Header("Objects")]
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject countdownRoot;
        [SerializeField] private TMP_Text countdownValue;

        [Tooltip("Optional. Stops the countdown and shows the start button again.")]
        [SerializeField] private Button cancelButton;

        [Tooltip("Menu buttons locked while counting, e.g. About, Objectives and How to Play.")]
        [SerializeField] private Selectable[] lockWhileCounting = new Selectable[0];

        [Header("Countdown")]
        [SerializeField, Min(1)] private int seconds = 5;

        [Tooltip("Text shown when the countdown reaches zero, while the session is being created.")]
        [SerializeField] private string finishedText = "¡Ya!";

        public bool IsCounting => routine != null;

        private Coroutine routine;
        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(selector, nameof(selector), this)
                & Guard.Required(launcher, nameof(launcher), this)
                & Guard.Required(startButton, nameof(startButton), this)
                & Guard.Required(countdownRoot, nameof(countdownRoot), this)
                & Guard.Required(countdownValue, nameof(countdownValue), this);

            if (!isValid)
            {
                enabled = false;
                return;
            }

            countdownRoot.SetActive(false);
            startButton.gameObject.SetActive(false);
            SetCancelVisible(false);
        }

        private void OnEnable()
        {
            if (!isValid)
            {
                return;
            }

            selector.SelectionChanged += OnSelectionChanged;
            startButton.onClick.AddListener(Begin);
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(Cancel);
            }
        }

        private void OnDisable()
        {
            if (!isValid)
            {
                return;
            }

            selector.SelectionChanged -= OnSelectionChanged;
            startButton.onClick.RemoveListener(Begin);
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(Cancel);
            }
        }

        public void Begin()
        {
            if (!isValid || IsCounting || launcher.IsLaunching || selector.Selected == null)
            {
                return;
            }

            startButton.gameObject.SetActive(false);
            countdownRoot.SetActive(true);
            SetCancelVisible(true);
            SetLocked(true);
            routine = StartCoroutine(CountRoutine());
        }

        public void Cancel()
        {
            if (!IsCounting || launcher.IsLaunching)
            {
                return;
            }

            StopCoroutine(routine);
            routine = null;
            countdownRoot.SetActive(false);
            SetCancelVisible(false);
            SetLocked(false);
            startButton.gameObject.SetActive(selector.Selected != null);
        }

        private IEnumerator CountRoutine()
        {
            for (int remaining = seconds; remaining > 0; remaining--)
            {
                countdownValue.text = remaining.ToString();
                yield return new WaitForSecondsRealtime(1f);
            }

            countdownValue.text = finishedText;
            if (cancelButton != null)
            {
                cancelButton.interactable = false;
            }

            launcher.Launch();
        }

        private void OnSelectionChanged(FurnitureDefinition definition)
        {
            if (!IsCounting)
            {
                startButton.gameObject.SetActive(definition != null);
            }
        }

        /// <summary>Shows or hides the cancel button, wherever it is in the hierarchy.</summary>
        private void SetCancelVisible(bool visible)
        {
            if (cancelButton == null)
            {
                return;
            }

            cancelButton.gameObject.SetActive(visible);
            cancelButton.interactable = true;
        }

        private void SetLocked(bool locked)
        {
            selector.SetInteractable(!locked);

            foreach (Selectable selectable in lockWhileCounting)
            {
                if (selectable != null)
                {
                    selectable.interactable = !locked;
                }
            }
        }
    }
}