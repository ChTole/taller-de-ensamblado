using AssemblyWorkshop.Common;
using AssemblyWorkshop.Simulation;
using Bitacora;
using UnityEngine;

namespace AssemblyWorkshop.Telemetry
{
    /// <summary>
    /// Starts a Bitácora session. Assign it to SimulationLauncher in the main menu and feedback scenes.
    /// </summary>
    [DisallowMultipleComponent]
    public class BitacoraSessionStarter : SessionStarter
    {
        [Tooltip("Maximum seconds to wait for a previous session to finish before starting a new one.")]
        [SerializeField, Min(0f)] private float finishTimeoutSeconds = 5f;

        public override async Awaitable<bool> StartSessionAsync()
        {
            SessionManager session = SessionManager.Instance;
            if (session == null)
            {
                Guard.Warning(this, "Bitácora was not found: add its object to the scene.");
                return false;
            }

            // When retrying from the feedback scene, the previous session may still be finishing
            float waitUntil = Time.unscaledTime + finishTimeoutSeconds;
            while (session.HasActiveSession && session.IsFinishing && Time.unscaledTime < waitUntil)
            {
                await Awaitable.NextFrameAsync();
            }

            if (session.HasActiveSession)
            {
                if (session.IsFinishing)
                {
                    Guard.Warning(this, "The previous session is still finishing.");
                    return false;
                }

                return true;
            }

            SessionInfo info = await session.StartSessionAsync();
            return info != null;
        }
    }
}
