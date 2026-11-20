using UnityEngine;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Measures how the controls are consulted during the simulation: how many times and for how long.
    /// It is recorded for analysis only and is not part of the assembly result's evaluation.
    /// </summary>
    [DisallowMultipleComponent]
    public class ControlsTracker : ViewTracker
    {
    }
}