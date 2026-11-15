using System;
using AssemblyWorkshop.Pieces;
using UnityEngine;

namespace AssemblyWorkshop.Interaction
{
    /// <summary>
    /// The selected group. If the group is merged into another, the selection moves to the resulting group;
    /// if it is split into several, the selection is cleared.
    /// </summary>
    [DisallowMultipleComponent]
    public class PieceSelection : MonoBehaviour
    {
        /// <summary>The selection changed. Receives null when nothing is selected.</summary>
        public event Action<PieceGroup> SelectionChanged;

        public PieceGroup Selected { get; private set; }

        public void Select(PieceGroup group)
        {
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
            Select(successor);
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
