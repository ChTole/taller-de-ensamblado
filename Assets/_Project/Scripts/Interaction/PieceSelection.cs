using System;
using AssemblyWorkshop.Pieces;
using UnityEngine;

namespace AssemblyWorkshop.Interaction
{
    /// <summary>
    /// The selected group, and the piece of that group that was touched.
    /// If the group is merged into another, the selection moves to the resulting group;
    /// if it is split into several, the selection is cleared.
    /// </summary>
    [DisallowMultipleComponent]
    public class PieceSelection : MonoBehaviour
    {
        /// <summary>The selected group changed. Receives null when nothing is selected.</summary>
        public event Action<PieceGroup> SelectionChanged;

        public PieceGroup Selected { get; private set; }

        /// <summary>The piece that was touched to select the group. Used to remove a single piece on touch screens.</summary>
        public AssemblyPiece SelectedPiece { get; private set; }

        public void Select(PieceGroup group)
        {
            Select(group, null);
        }

        public void Select(PieceGroup group, AssemblyPiece piece)
        {
            SelectedPiece = piece != null && piece.Group == group ? piece : null;

            if (Selected == group)
            {
                return;
            }

            if (Selected != null)
            {
                Selected.Dissolved -= OnSelectedDissolved;
            }

            Selected = group;

            if (Selected != null)
            {
                Selected.Dissolved += OnSelectedDissolved;
            }

            SelectionChanged?.Invoke(Selected);
        }

        private void OnSelectedDissolved(PieceGroup dissolved, PieceGroup successor)
        {
            // When groups merge, the pieces already belong to the successor
            Select(successor, SelectedPiece);
        }

        private void OnDestroy()
        {
            if (Selected != null)
            {
                Selected.Dissolved -= OnSelectedDissolved;
            }
        }
    }
}
