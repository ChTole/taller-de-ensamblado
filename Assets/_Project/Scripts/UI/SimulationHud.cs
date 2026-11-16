using AssemblyWorkshop.Common;
using AssemblyWorkshop.Joints;
using AssemblyWorkshop.Simulation;
using TMPro;
using UnityEngine;

namespace AssemblyWorkshop.UI
{
    /// <summary>
    /// Simulation HUD: elapsed time and completed joints.
    /// The joint counter only shows progress; it does not say whether the joints are correct.
    /// </summary>
    [DisallowMultipleComponent]
    public class SimulationHud : MonoBehaviour
    {
        [SerializeField] private SimulationController controller;
        [SerializeField] private AssemblyGraph graph;

        [Header("Texts")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text jointsText;

        [Tooltip("Show the time as minutes and seconds (03:25). If disabled, only seconds are shown (205).")]
        [SerializeField] private bool showMinutes = true;

        [Tooltip("Format of the joint counter. {0}: completed joints, {1}: total joints.")]
        [SerializeField] private string jointsFormat = "{0} de {1}";

        private int shownSeconds = -1;
        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(controller, nameof(controller), this)
                & Guard.Required(graph, nameof(graph), this)
                & Guard.Required(timerText, nameof(timerText), this)
                & Guard.Required(jointsText, nameof(jointsText), this);

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
            graph.JointConnected += OnJointsChanged;
            graph.JointDisconnected += OnJointsChanged;
            RefreshJoints();
            RefreshTimer(force: true);
        }

        private void OnDisable()
        {
            if (!isValid)
            {
                return;
            }

            controller.AssemblyStarted -= OnAssemblyStarted;
            graph.JointConnected -= OnJointsChanged;
            graph.JointDisconnected -= OnJointsChanged;
        }

        private void Update()
        {
            RefreshTimer(force: false);
        }

        private void OnAssemblyStarted(FurnitureDefinition furniture)
        {
            RefreshJoints();
            RefreshTimer(force: true);
        }

        private void OnJointsChanged(JointPair pair)
        {
            RefreshJoints();
        }

        private void RefreshJoints()
        {
            jointsText.text = string.Format(jointsFormat, graph.Connections.Count, graph.TotalJoints);
        }

        private void RefreshTimer(bool force)
        {
            // The text is only rebuilt when the shown second changes
            int seconds = Mathf.FloorToInt(controller.ElapsedSeconds);
            if (!force && seconds == shownSeconds)
            {
                return;
            }

            shownSeconds = seconds;
            timerText.text = showMinutes
                ? $"{seconds / 60:00}:{seconds % 60:00}"
                : seconds.ToString();
        }
    }
}
