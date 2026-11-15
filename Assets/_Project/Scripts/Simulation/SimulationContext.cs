using UnityEngine;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Data passed between scenes: the furniture chosen in the menu
    /// and the result of the last assembly, for the feedback scene.
    /// </summary>
    public static class SimulationContext
    {
        public static FurnitureDefinition SelectedFurniture { get; set; }
        public static AssemblyResult LastResult { get; set; }

        // Clears the data when entering Play Mode, also with Domain Reload disabled
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SelectedFurniture = null;
            LastResult = null;
        }
    }
}
