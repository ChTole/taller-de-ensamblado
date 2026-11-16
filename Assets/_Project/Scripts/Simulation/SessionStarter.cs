using UnityEngine;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Starts a recording session before the simulation is loaded.
    /// Defined here, without depending on any telemetry package: the implementation lives
    /// in the Telemetry module, so the simulation never references Bitácora directly.
    /// </summary>
    public abstract class SessionStarter : MonoBehaviour
    {
        /// <summary>Starts a session, or keeps the active one. Returns false if it could not be started.</summary>
        public abstract Awaitable<bool> StartSessionAsync();
    }
}
