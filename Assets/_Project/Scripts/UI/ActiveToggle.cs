using AssemblyWorkshop.Common;
using UnityEngine;

namespace AssemblyWorkshop.UI
{
    /// <summary>
    /// Shows and hides an object from a button, e.g. the options panel. The button calls Toggle from On Click ().
    /// It must be on an object that stays active, not on the target itself.
    /// </summary>
    [DisallowMultipleComponent]
    public class ActiveToggle : MonoBehaviour
    {
        [SerializeField] private GameObject target;

        [Tooltip("Whether the object starts visible.")]
        [SerializeField] private bool startVisible;

        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(target, nameof(target), this);
            if (isValid)
            {
                target.SetActive(startVisible);
            }
        }

        public void Toggle()
        {
            if (isValid)
            {
                target.SetActive(!target.activeSelf);
            }
        }

        public void Hide()
        {
            if (isValid)
            {
                target.SetActive(false);
            }
        }
    }
}