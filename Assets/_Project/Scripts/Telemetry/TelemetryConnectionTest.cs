using System;
using AssemblyWorkshop.Common;
using Bitacora;
using UnityEngine;

namespace AssemblyWorkshop.Telemetry
{
    /// <summary>
    /// Development check: starts a session, records one event and finishes the session, without any UI.
    /// Use it in a test scene to confirm that a device reaches the API. Not meant for production scenes.
    /// </summary>
    [DisallowMultipleComponent]
    public class TelemetryConnectionTest : MonoBehaviour
    {
        [Tooltip("Seconds to wait after the scene starts, so Bitácora can verify the connection first.")]
        [SerializeField, Min(0f)] private float startDelay = 2f;

        private async void Start()
        {
            if (startDelay > 0f)
            {
                await Awaitable.WaitForSecondsAsync(startDelay);
            }

            SessionManager session = SessionManager.Instance;
            if (session == null || EventManager.Instance == null)
            {
                Guard.Error(this, "Bitácora was not found in the scene.");
                return;
            }

            try
            {
                Guard.Info(this, "Starting a test session...");
                SessionInfo info = await session.StartSessionAsync();
                if (info == null)
                {
                    Guard.Error(this, "The session could not be started. Check the API address, the key and the network.");
                    return;
                }

                Guard.Info(this, "Session started. Recording a test event...");
                EventManager.Instance.Track("connection_test", new
                {
                    platform = Application.platform.ToString(),
                    device = SystemInfo.deviceModel,
                    app_version = Application.version
                });

                await session.FinishSessionAsync(SessionStatus.Completed);
                Guard.Info(this, "Test finished: the session should appear in the dashboard.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}