using System;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Interaction;
using AssemblyWorkshop.Joints;
using AssemblyWorkshop.Pieces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Commands to take pieces apart: undo the last fit and remove a piece.
    /// On the keyboard, X removes the piece under the pointer. On touch screens, the buttons call
    /// UndoLast and DetachSelected, which removes the last piece that was touched.
    /// </summary>
    [DisallowMultipleComponent]
    public class AssemblyCommands : MonoBehaviour
    {
        [SerializeField] private JointSnapper snapper;
        [SerializeField] private PieceDragger dragger;
        [SerializeField] private PieceSelection selection;

        [Header("Actions")]
        [SerializeField] private InputActionReference undoAction;
        [SerializeField] private InputActionReference detachAction;

        /// <summary>
        /// A command could not be performed. Receives the reason; the UI decides which text to show.
        /// </summary>
        public event Action<DetachResult> CommandRefused;

        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(snapper, nameof(snapper), this)
                & Guard.Required(dragger, nameof(dragger), this)
                & Guard.Required(selection, nameof(selection), this)
                & InputGuard.Required(undoAction, nameof(undoAction), this)
                & InputGuard.Required(detachAction, nameof(detachAction), this);

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

            undoAction.action.Enable();
            detachAction.action.Enable();
        }

        private void OnDisable()
        {
            if (!isValid)
            {
                return;
            }

            undoAction.action.Disable();
            detachAction.action.Disable();
        }

        private void Update()
        {
            if (undoAction.action.WasPressedThisFrame())
            {
                UndoLast();
            }

            if (detachAction.action.WasPressedThisFrame())
            {
                DetachUnderPointer();
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

        public void DetachUnderPointer()
        {
            if (!dragger.TryGetPieceUnderPointer(out AssemblyPiece piece, out _))
            {
                return;
            }

            DetachResult result = snapper.TryDetach(piece);
            if (result != DetachResult.Detached)
            {
                Refuse($"Detach '{piece.PieceId}'", result);
            }
        }

        /// <summary>Removes the last piece that was touched, if it is still selected.</summary>
        public void DetachSelected()
        {
            AssemblyPiece piece = selection.SelectedPiece;
            if (piece == null)
            {
                Refuse("Detach selected", DetachResult.NotConnected);
                return;
            }

            DetachResult result = snapper.TryDetach(piece);
            if (result != DetachResult.Detached)
            {
                Refuse($"Detach '{piece.PieceId}'", result);
            }
        }

        private void Refuse(string command, DetachResult result)
        {
            Guard.Info(this, $"{command} refused: {result}.");
            CommandRefused?.Invoke(result);
        }
    }
}
