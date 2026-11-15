using System.Collections.Generic;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Interaction;
using AssemblyWorkshop.Joints;
using AssemblyWorkshop.Pieces;
using UnityEngine;

namespace AssemblyWorkshop.Feedback
{
    /// <summary>
    /// Piece highlighting: the selected group, the group that can be fitted to
    /// and a flash on the piece of a failed attempt. It only listens to events: no other class paints.
    /// </summary>
    [DisallowMultipleComponent]
    public class PieceHighlighter : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private PieceSelection selection;
        [SerializeField] private JointDetector detector;

        [Header("Colors")]
        [SerializeField] private Color selectionColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] private Color validJointColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField] private Color failureColor = new Color(0.95f, 0.3f, 0.25f);

        [Tooltip("How much the highlight color is blended with the original color.")]
        [SerializeField, Range(0f, 1f)] private float amount = 0.35f;

        [Tooltip("Duration of the flash for a failed attempt, in seconds.")]
        [SerializeField, Min(0f)] private float failureFlashDuration = 0.6f;

        private readonly Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();
        private readonly HashSet<Renderer> painted = new HashSet<Renderer>();
        private MaterialPropertyBlock block;

        private PieceGroup selected;
        private PieceGroup target;
        private AssemblyPiece flashPiece;
        private float flashUntil;
        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(selection, nameof(selection), this)
                & Guard.Required(detector, nameof(detector), this);

            if (!isValid)
            {
                enabled = false;
                return;
            }

            block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (!isValid)
            {
                return;
            }

            selection.SelectionChanged += OnSelectionChanged;
            detector.CandidateChanged += OnCandidateChanged;
            detector.AttemptFailed += OnAttemptFailed;
        }

        private void OnDisable()
        {
            if (!isValid)
            {
                return;
            }

            selection.SelectionChanged -= OnSelectionChanged;
            detector.CandidateChanged -= OnCandidateChanged;
            detector.AttemptFailed -= OnAttemptFailed;
        }

        private void Update()
        {
            if (flashPiece != null && Time.time >= flashUntil)
            {
                flashPiece = null;
                Refresh();
            }
        }

        private void OnSelectionChanged(PieceGroup group)
        {
            selected = group;
            Refresh();
        }

        private void OnCandidateChanged(JointMatch match)
        {
            target = match?.TargetGroup;
            Refresh();
        }

        private void OnAttemptFailed(JointFailure failure)
        {
            // Flash the piece that was not being moved
            flashPiece = failure.Tenon.Piece.Group == failure.MovingGroup ? failure.Mortise.Piece : failure.Tenon.Piece;
            flashUntil = Time.time + failureFlashDuration;
            Refresh();
        }

        /// <summary>Clears all highlighting and applies it again from the current state.</summary>
        private void Refresh()
        {
            foreach (Renderer pieceRenderer in painted)
            {
                if (pieceRenderer != null)
                {
                    pieceRenderer.SetPropertyBlock(null);
                }
            }

            painted.Clear();

            PaintGroup(selected, selectionColor);
            PaintGroup(target, validJointColor);

            if (flashPiece != null)
            {
                PaintPiece(flashPiece, failureColor);
            }
        }

        private void PaintGroup(PieceGroup group, Color color)
        {
            if (group == null)
            {
                return;
            }

            foreach (AssemblyPiece piece in group.Pieces)
            {
                PaintPiece(piece, color);
            }
        }

        private void PaintPiece(AssemblyPiece piece, Color color)
        {
            foreach (Renderer pieceRenderer in piece.Renderers)
            {
                block.Clear();
                block.SetColor(BaseColorId, Color.Lerp(GetOriginalColor(pieceRenderer), color, amount));
                pieceRenderer.SetPropertyBlock(block);
                painted.Add(pieceRenderer);
            }
        }

        private Color GetOriginalColor(Renderer pieceRenderer)
        {
            if (!originalColors.TryGetValue(pieceRenderer, out Color color))
            {
                Material material = pieceRenderer.sharedMaterial;
                color = material != null && material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId)
                    : Color.white;
                originalColors[pieceRenderer] = color;
            }

            return color;
        }
    }
}
