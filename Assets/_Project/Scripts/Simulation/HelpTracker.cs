using UnityEngine;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Measures help usage: how many times the assembled furniture was shown and for how long.
    /// The help view opens it while the button is held and closes it when the button is released.
    /// </summary>
    [DisallowMultipleComponent]
    public class HelpTracker : ViewTracker
    {
    }
}