using AssemblyWorkshop.Common;
using TMPro;
using UnityEngine;

namespace AssemblyWorkshop.UI
{
    /// <summary>
    /// Shows the app version, taken from Player Settings → Version, so it only has to be changed in one place.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class VersionLabel : MonoBehaviour
    {
        [Tooltip("Format of the text. {0}: the version, e.g. 1.1.1.")]
        [SerializeField] private string format = "Versión {0}";

        private void Awake()
        {
            TMP_Text label = GetComponent<TMP_Text>();
            label.text = string.Format(format, Application.version);
        }

        private void OnValidate()
        {
            if (!format.Contains("{0}"))
            {
                Guard.Warning(this, "'Format' has no {0}: the version will not be shown.");
            }
        }
    }
}