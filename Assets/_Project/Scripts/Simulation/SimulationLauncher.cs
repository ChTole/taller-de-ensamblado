using System;
using AssemblyWorkshop.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Starts the session and loads the simulation scene with the furniture in SimulationContext.
    /// Used by the main menu ("Comenzar") and by the feedback scene ("Nuevo intento").
    /// </summary>
    [DisallowMultipleComponent]
    public class SimulationLauncher : MonoBehaviour
    {
        [SerializeField] private string simulationScene = "Simulation";

        [Tooltip("Optional. Starts the session before loading the simulation. Without it, the simulation runs without telemetry.")]
        [SerializeField] private SessionStarter sessionStarter;

        /// <summary>The launch started: the session is being created.</summary>
        public event Action LaunchStarted;

        public bool IsLaunching { get; private set; }

        public async void Launch()
        {
            if (IsLaunching)
            {
                return;
            }

            if (SimulationContext.SelectedFurniture == null)
            {
                Guard.Error(this, "No furniture is selected.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(simulationScene))
            {
                Guard.Error(this, $"Scene '{simulationScene}' is not in the build's scene list.");
                return;
            }

            IsLaunching = true;
            LaunchStarted?.Invoke();

            if (sessionStarter != null)
            {
                bool started = false;
                try
                {
                    started = await sessionStarter.StartSessionAsync();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }

                if (!started)
                {
                    Guard.Warning(this, "The session could not be started: the simulation runs without telemetry.");
                }
            }

            if (this == null)
            {
                // The scene was unloaded while the session was being created
                return;
            }

            SimulationContext.LastResult = null;
            SceneManager.LoadScene(simulationScene);
        }
    }
}
