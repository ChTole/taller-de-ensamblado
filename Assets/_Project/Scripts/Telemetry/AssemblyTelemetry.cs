using System;
using System.Collections.Generic;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Joints;
using AssemblyWorkshop.Pieces;
using AssemblyWorkshop.Simulation;
using Bitacora;
using UnityEngine;

namespace AssemblyWorkshop.Telemetry
{
    /// <summary>
    /// Records the assembly events in Bitácora. It is the only class in the project that knows about Bitácora:
    /// the rest of the simulation only raises C# events.
    /// Finishes the session when the assembly is checked or when returning to the menu without checking it.
    /// </summary>
    [DisallowMultipleComponent]
    public class AssemblyTelemetry : MonoBehaviour
    {
        [SerializeField] private SimulationController controller;
        [SerializeField] private AssemblyGraph graph;
        [SerializeField] private JointDetector detector;
        [SerializeField] private JointSnapper snapper;
        [SerializeField] private HelpTracker helpTracker;

        [Tooltip("Optional. Records each time the controls are consulted.")]
        [SerializeField] private ControlsTracker controlsTracker;

        [Tooltip("Editor only: if there is no active session when entering Play Mode in this scene, starts one. " +
                 "Useful for testing without going through the menu.")]
        [SerializeField] private bool startSessionInEditor = true;

        // Events recorded before there is an active session
        private readonly List<KeyValuePair<string, object>> pending = new List<KeyValuePair<string, object>>();

        // Failed attempts per mortise, reported when that joint is completed
        private readonly Dictionary<string, int> failuresByMortise = new Dictionary<string, int>();

        private bool isValid;
        private bool isAvailable;
        private bool isResolved;

        private void Awake()
        {
            isValid = Guard.Required(controller, nameof(controller), this)
                & Guard.Required(graph, nameof(graph), this)
                & Guard.Required(detector, nameof(detector), this)
                & Guard.Required(snapper, nameof(snapper), this)
                & Guard.Required(helpTracker, nameof(helpTracker), this);

            if (!isValid)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!isValid)
            {
                return;
            }

            controller.AssemblyStarted += OnAssemblyStarted;
            controller.AssemblyChecked += OnAssemblyChecked;
            controller.AssemblyRestarted += OnAssemblyRestarted;
            controller.AssemblyAbandoned += OnAssemblyAbandoned;
            graph.JointConnected += OnJointConnected;
            detector.AttemptFailed += OnAttemptFailed;
            snapper.Undone += OnUndone;
            snapper.Detached += OnDetached;
            helpTracker.Closed += OnHelpClosed;
            if (controlsTracker != null)
            {
                controlsTracker.Closed += OnControlsClosed;
            }
        }

        private void OnDisable()
        {
            if (!isValid)
            {
                return;
            }

            controller.AssemblyStarted -= OnAssemblyStarted;
            controller.AssemblyChecked -= OnAssemblyChecked;
            controller.AssemblyRestarted -= OnAssemblyRestarted;
            controller.AssemblyAbandoned -= OnAssemblyAbandoned;
            graph.JointConnected -= OnJointConnected;
            detector.AttemptFailed -= OnAttemptFailed;
            snapper.Undone -= OnUndone;
            snapper.Detached -= OnDetached;
            helpTracker.Closed -= OnHelpClosed;
            if (controlsTracker != null)
            {
                controlsTracker.Closed -= OnControlsClosed;
            }

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.SessionStarted -= OnSessionStarted;
            }
        }

        private async void Start()
        {
            if (!isValid)
            {
                return;
            }

            isResolved = true;

            if (SessionManager.Instance == null || EventManager.Instance == null)
            {
                Guard.Warning(this, "Bitácora was not found (the AppManager object is created in the main menu scene). " +
                                    "Telemetry is disabled.");
                pending.Clear();
                return;
            }

            isAvailable = true;
            SessionManager.Instance.SessionStarted += OnSessionStarted;

            if (SessionManager.Instance.HasActiveSession)
            {
                FlushPending();
                return;
            }

#if UNITY_EDITOR
            if (startSessionInEditor)
            {
                try
                {
                    // Once the session is created, OnSessionStarted sends the pending events
                    SessionInfo session = await SessionManager.Instance.StartSessionAsync();
                    if (session == null)
                    {
                        Guard.Warning(this, "Could not start a test session.");
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }

                return;
            }
#endif

            Guard.Warning(this, "There is no active session: events are held until one starts.");
        }

        // ───────────── Assembly events ─────────────

        private void OnAssemblyStarted(FurnitureDefinition furniture)
        {
            Track("assembly_started", new
            {
                furniture_id = furniture.FurnitureId,
                pieces = graph.Pieces.Count,
                joints = graph.TotalJoints,
                mobile = Application.isMobilePlatform
            });
        }

        private void OnJointConnected(JointPair pair)
        {
            failuresByMortise.TryGetValue(pair.Mortise.JointId, out int previousFailures);

            Track("joint_completed", new
            {
                tenon = pair.Tenon.JointId,
                mortise = pair.Mortise.JointId,
                expected = pair.Tenon.IsExpectedPartner(pair.Mortise),
                elapsed_seconds = Round(controller.ElapsedSeconds),
                previous_failures = previousFailures
            });
        }

        private void OnAttemptFailed(JointFailure failure)
        {
            string mortiseId = failure.Mortise.JointId;
            failuresByMortise.TryGetValue(mortiseId, out int count);
            failuresByMortise[mortiseId] = count + 1;

            Track("joint_attempt_failed", new
            {
                tenon = failure.Tenon.JointId,
                mortise = mortiseId,
                reason = ReasonId(failure.Reason),
                elapsed_seconds = Round(controller.ElapsedSeconds)
            });
        }

        private void OnUndone(IReadOnlyList<JointPair> pairs)
        {
            foreach (JointPair pair in pairs)
            {
                Track("joint_undone", new
                {
                    tenon = pair.Tenon.JointId,
                    mortise = pair.Mortise.JointId,
                    mode = "undo",
                    elapsed_seconds = Round(controller.ElapsedSeconds)
                });
            }
        }

        private void OnDetached(AssemblyPiece piece, IReadOnlyList<JointPair> pairs)
        {
            foreach (JointPair pair in pairs)
            {
                Track("joint_undone", new
                {
                    tenon = pair.Tenon.JointId,
                    mortise = pair.Mortise.JointId,
                    mode = "detach",
                    piece = piece.PieceId,
                    elapsed_seconds = Round(controller.ElapsedSeconds)
                });
            }
        }

        private void OnHelpClosed(float duration)
        {
            Track("help_viewed", new
            {
                duration_seconds = Round(duration),
                elapsed_seconds = Round(controller.ElapsedSeconds)
            });
        }

        private void OnControlsClosed(float duration)
        {
            Track("controls_viewed", new
            {
                duration_seconds = Round(duration),
                elapsed_seconds = Round(controller.ElapsedSeconds)
            });
        }

        private void OnAssemblyRestarted(AssemblyResult result)
        {
            // The session continues: restarting the assembly is part of the learning process
            Track("assembly_reset", new
            {
                joints_completed = result.JointsCompleted,
                elapsed_seconds = Round(result.DurationSeconds)
            });
        }

        private void OnAssemblyChecked(AssemblyResult result)
        {
            // The session is completed because the user decided to finish; 'completed' in the data
            // says whether every joint was done
            Track("assembly_checked", Summary(result));
            FinishSession(SessionStatus.Completed);
        }

        private void OnAssemblyAbandoned(AssemblyResult result)
        {
            Track("assembly_abandoned", Summary(result));
            FinishSession(SessionStatus.Abandoned);
        }

        // ───────────── Bitácora ─────────────

        private void Track(string type, object data)
        {
            if (isAvailable && SessionManager.Instance != null
                && SessionManager.Instance.HasActiveSession && !SessionManager.Instance.IsFinishing)
            {
                EventManager.Instance.Track(type, data);
                return;
            }

            // Until we know whether Bitácora is available, or while there is no session, the event is held
            if (!isResolved || isAvailable)
            {
                pending.Add(new KeyValuePair<string, object>(type, data));
            }
        }

        private void OnSessionStarted(SessionInfo session)
        {
            FlushPending();
        }

        private void FlushPending()
        {
            if (pending.Count == 0)
            {
                return;
            }

            var events = new List<KeyValuePair<string, object>>(pending);
            pending.Clear();

            foreach (KeyValuePair<string, object> item in events)
            {
                Track(item.Key, item.Value);
            }
        }

        private async void FinishSession(SessionStatus status)
        {
            SessionManager session = SessionManager.Instance;
            if (!isAvailable || session == null || !session.HasActiveSession || session.IsFinishing)
            {
                return;
            }

            try
            {
                await session.FinishSessionAsync(status);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static object Summary(AssemblyResult result)
        {
            return new
            {
                furniture_id = result.FurnitureId,
                completed = result.Completed,
                duration_seconds = Round(result.DurationSeconds),
                joints_completed = result.JointsCompleted,
                joints_total = result.JointsTotal,
                incorrect_joints = result.IncorrectJoints,
                correct = result.IsCorrect,
                failed_attempts = result.FailedAttempts,
                grabs = result.Grabs,
                rotations = result.Rotations,
                undos = result.Undos,
                detaches = result.Detaches,
                help_requests = result.HelpRequests,
                help_seconds = Round(result.HelpSeconds),
                controls_views = result.ControlsViews,
                controls_seconds = Round(result.ControlsSeconds)
            };
        }

        private static string ReasonId(JointFailureReason reason)
        {
            switch (reason)
            {
                case JointFailureReason.Orientation: return "orientation";
                case JointFailureReason.Position: return "position";
                case JointFailureReason.Incompatible: return "incompatible";
                case JointFailureReason.Blocked: return "blocked";
                default: return reason.ToString().ToLowerInvariant();
            }
        }

        private static float Round(float seconds)
        {
            return Mathf.Round(seconds * 10f) / 10f;
        }
    }
}