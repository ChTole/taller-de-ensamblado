using System;
using System.Collections.Generic;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AssemblyWorkshop.UI
{
    /// <summary>
    /// Difficulty dropdown built from the furniture catalog. The choice is stored in SimulationContext
    /// and shown in its own panel, with the image of the assembled furniture.
    /// </summary>
    [DisallowMultipleComponent]
    public class FurnitureSelector : MonoBehaviour
    {
        [SerializeField] private FurnitureCatalog catalog;
        [SerializeField] private TMP_Dropdown dropdown;
        [SerializeField] private PanelSwitcher panels;

        [Header("Selected Furniture Panel")]
        [SerializeField] private GameObject selectedPanel;
        [SerializeField] private Image furnitureImage;

        [Tooltip("Optional. Shows the furniture description.")]
        [SerializeField] private TMP_Text descriptionText;

        [Header("Texts")]
        [Tooltip("First option, shown while nothing is selected.")]
        [SerializeField] private string placeholder = "Seleccionar";

        [Tooltip("Format of each option. {0}: furniture name, {1}: difficulty level.")]
        [SerializeField] private string optionFormat = "Nivel {1}: {0}";

        /// <summary>The selected furniture changed. Receives null when the selection is cleared.</summary>
        public event Action<FurnitureDefinition> SelectionChanged;

        public FurnitureDefinition Selected => SimulationContext.SelectedFurniture;

        private readonly List<FurnitureDefinition> options = new List<FurnitureDefinition>();
        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(catalog, nameof(catalog), this)
                & Guard.Required(dropdown, nameof(dropdown), this)
                & Guard.Required(panels, nameof(panels), this)
                & Guard.Required(selectedPanel, nameof(selectedPanel), this)
                & Guard.Required(furnitureImage, nameof(furnitureImage), this);

            if (!isValid)
            {
                enabled = false;
                return;
            }

            BuildOptions();
        }

        private void OnEnable()
        {
            if (isValid)
            {
                dropdown.onValueChanged.AddListener(OnValueChanged);
            }
        }

        private void OnDisable()
        {
            if (isValid)
            {
                dropdown.onValueChanged.RemoveListener(OnValueChanged);
            }
        }

        private void Start()
        {
            if (!isValid)
            {
                return;
            }

            // When returning from the simulation, the previous choice is kept
            int index = Selected != null ? options.IndexOf(Selected) : -1;
            dropdown.SetValueWithoutNotify(index >= 0 ? index + 1 : 0);
            dropdown.RefreshShownValue();
            Apply(index >= 0 ? options[index] : null, showPanel: index >= 0);
        }

        /// <summary>Enables or disables the dropdown, for example during the countdown.</summary>
        public void SetInteractable(bool interactable)
        {
            if (isValid)
            {
                dropdown.interactable = interactable;
            }
        }

        private void BuildOptions()
        {
            options.Clear();
            var labels = new List<string> { placeholder };

            foreach (FurnitureDefinition definition in catalog.Furniture)
            {
                if (definition == null)
                {
                    continue;
                }

                options.Add(definition);
                labels.Add(string.Format(optionFormat, definition.DisplayName, definition.Difficulty));
            }

            if (options.Count == 0)
            {
                Guard.Warning(this, $"Catalog '{catalog.name}' has no furniture.");
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(labels);
        }

        private void OnValueChanged(int index)
        {
            FurnitureDefinition definition = index > 0 && index <= options.Count ? options[index - 1] : null;
            Apply(definition, showPanel: true);
        }

        private void Apply(FurnitureDefinition definition, bool showPanel)
        {
            SimulationContext.SelectedFurniture = definition;

            if (definition == null)
            {
                if (showPanel || panels.Current == selectedPanel)
                {
                    panels.ShowDefault();
                }
            }
            else
            {
                if (definition.Thumbnail == null)
                {
                    Guard.Warning(this, $"Furniture '{definition.FurnitureId}' has no 'Thumbnail'.");
                }

                furnitureImage.sprite = definition.Thumbnail;
                furnitureImage.preserveAspect = true;

                if (descriptionText != null)
                {
                    descriptionText.text = definition.Description;
                }

                if (showPanel)
                {
                    panels.Show(selectedPanel);
                }
            }

            SelectionChanged?.Invoke(definition);
        }
    }
}
