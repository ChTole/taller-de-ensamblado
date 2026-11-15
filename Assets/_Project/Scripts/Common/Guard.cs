using UnityEngine;

namespace AssemblyWorkshop.Common
{
    /// <summary>
    /// Configuration checks with consistent console messages: "[Component] message".
    /// Every message passes the object as context, so clicking it in the Console selects it in the Hierarchy.
    /// </summary>
    public static class Guard
    {
        /// <summary>Checks a required Inspector reference. Logs an error if it is missing.</summary>
        public static bool Required(Object value, string fieldName, Object owner)
        {
            if (value != null)
            {
                return true;
            }

            Error(owner, $"'{fieldName}' is not assigned.");
            return false;
        }

        public static void Error(Object owner, string message)
        {
            Debug.LogError(Format(owner, message), owner);
        }

        public static void Warning(Object owner, string message)
        {
            Debug.LogWarning(Format(owner, message), owner);
        }

        public static void Info(Object owner, string message)
        {
            Debug.Log(Format(owner, message), owner);
        }

        private static string Format(Object owner, string message)
        {
            string name = owner != null ? owner.GetType().Name : "AssemblyWorkshop";
            return $"[{name}] {message}";
        }
    }
}
