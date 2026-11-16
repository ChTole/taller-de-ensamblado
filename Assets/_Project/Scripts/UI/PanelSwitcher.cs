using System.Collections.Generic;
using AssemblyWorkshop.Common;
using UnityEngine;

namespace AssemblyWorkshop.UI
{
    /// <summary>
    /// Shows one panel at a time. Buttons call Show from their On Click () event, passing the panel to show.
    /// </summary>
    [DisallowMultipleComponent]
    public class PanelSwitcher : MonoBehaviour
    {
        [Tooltip("Panel shown at the start and when nothing else is selected.")]
        [SerializeField] private GameObject defaultPanel;

        [Tooltip("Every panel managed by this switcher, including the default one.")]
        [SerializeField] private List<GameObject> panels = new List<GameObject>();

        public GameObject Current { get; private set; }

        private void Awake()
        {
            if (!Guard.Required(defaultPanel, nameof(defaultPanel), this))
            {
                enabled = false;
                return;
            }

            if (!panels.Contains(defaultPanel))
            {
                panels.Add(defaultPanel);
            }

            ShowDefault();
        }

        public void Show(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            if (!panels.Contains(panel))
            {
                Guard.Warning(this, $"'{panel.name}' is not in 'Panels': it is added now.");
                panels.Add(panel);
            }

            foreach (GameObject item in panels)
            {
                if (item != null)
                {
                    item.SetActive(item == panel);
                }
            }

            Current = panel;
        }

        public void ShowDefault()
        {
            Show(defaultPanel);
        }

        private void OnValidate()
        {
            if (panels.Contains(null))
            {
                Guard.Warning(this, "'Panels' has an empty element.");
            }
        }
    }
}
