using System;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Joints;
using AssemblyWorkshop.Pieces;
using UnityEngine;

namespace AssemblyWorkshop.XRInteraction
{
    /// <summary>
    /// Undo and detach in the headset. "Detach" acts on the last piece that was grabbed, which plays the role
    /// of the selected piece in the web version. Both commands run once the piece is released.
    /// They can be triggered with controller buttons or from the UI, through the public methods.
    /// </summary>
    [DisallowMultipleComponent]
    public class XRAssemblyCommands : MonoBehaviour
    {
        [SerializeField] private AssemblyGraph graph;
        [SerializeField] private JointSnapper snapper;

        [Header("Controller buttons")]
        [Tooltip("Button that undoes the last fit. Default: B, on the right controller.")]
        [SerializeField] private OVRInput.Button undoButton = OVRInput.Button.Two;

        [Tooltip("Button that detaches the last grabbed piece. Default: Y, on the left controller.")]
        [SerializeField] private OVRInput.Button detachButton = OVRInput.Button.Four;

        /// <summary>A command could not be carried out. Receives the reason.</summary>
        public event Action<DetachResult> CommandRefused;

        /// <summary>The last piece that was grabbed: the one "detach" acts on.</summary>
        public AssemblyPiece LastGrabbedPiece { get; private set; }

        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(graph, nameof(graph), this) & Guard.Required(snapper, nameof(snapper), this);
            if (!isValid)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (isValid)
            {
                graph.GroupGrabbed += OnGroupGrabbed;
            }
        }

        private void OnDisable()
        {
            if (isValid)
            {
                graph.GroupGrabbed -= OnGroupGrabbed;
            }
        }

        private void Update()
        {
            if (OVRInput.GetDown(undoButton))
            {
                UndoLast();
            }

            if (OVRInput.GetDown(detachButton))
            {
                DetachLastGrabbed();
            }
        }

        public void UndoLast()
        {
            DetachResult result = snapper.TryUndoLast();
            if (result != DetachResult.Detached)
            {
                Refuse("Undo", result);
            }
        }

        public void DetachLastGrabbed()
        {
            if (LastGrabbedPiece == null)
            {
                Refuse("Detach", DetachResult.NotConnected);
                return;
            }

            DetachResult result = snapper.TryDetach(LastGrabbedPiece);
            if (result != DetachResult.Detached)
            {
                Refuse($"Detach '{LastGrabbedPiece.PieceId}'", result);
            }
        }

        private void OnGroupGrabbed(PieceGroup group)
        {
            if (group.TryGetComponent(out GrabbableGroupBody body) && body.GrabbedPiece != null)
            {
                LastGrabbedPiece = body.GrabbedPiece;
            }
        }

        private void Refuse(string command, DetachResult result)
        {
            Guard.Info(this, $"{command} refused: {result}.");
            CommandRefused?.Invoke(result);
        }
    }
}
