using UnityEngine;
using UnityEngine.InputSystem;

namespace AssemblyWorkshop.Common
{
    /// <summary>
    /// Checks for input action references. Kept apart from Guard so that assemblies
    /// which do not reference the Input System can still use Guard.Required.
    /// </summary>
    public static class InputGuard
    {
        /// <summary>Checks a required input action. Logs an error if it is missing.</summary>
        public static bool Required(InputActionReference reference, string fieldName, Object owner)
        {
            if (reference != null && reference.action != null)
            {
                return true;
            }

            Guard.Error(owner, $"Input action '{fieldName}' is not assigned.");
            return false;
        }
    }
}
