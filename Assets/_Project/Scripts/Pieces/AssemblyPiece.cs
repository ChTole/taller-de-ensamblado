using System;
using System.Collections.Generic;
using AssemblyWorkshop.Common;
using UnityEngine;

namespace AssemblyWorkshop.Pieces
{
    /// <summary>
    /// A wooden piece. Goes on the root object of each piece in the furniture prefab.
    /// A piece never moves on its own: it always belongs to a group (PieceGroup), and the group is what moves.
    /// </summary>
    [DisallowMultipleComponent]
    public class AssemblyPiece : MonoBehaviour
    {
        [Tooltip("Identifier used in telemetry, e.g. leg_front_left. If empty, the object name is used.")]
        [SerializeField] private string pieceId;

        public string PieceId => string.IsNullOrWhiteSpace(pieceId) ? name : pieceId;

        /// <summary>Group the piece belongs to.</summary>
        public PieceGroup Group { get; internal set; }

        public IReadOnlyList<Renderer> Renderers => renderers;
        public IReadOnlyList<BoxCollider> Colliders => colliders;

        private Renderer[] renderers = Array.Empty<Renderer>();
        private BoxCollider[] colliders = Array.Empty<BoxCollider>();

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<BoxCollider>();

            if (colliders.Length == 0)
            {
                Guard.Error(this, $"'{PieceId}' has no Box Colliders: it cannot be grabbed and will not collide with other pieces.");
            }

            foreach (Collider other in GetComponentsInChildren<Collider>())
            {
                if (!(other is BoxCollider))
                {
                    Guard.Warning(this, $"'{other.name}' has a {other.GetType().Name}: collisions between pieces only consider Box Colliders.");
                }
            }

            if (!AxisAlignment.IsAligned(transform.rotation))
            {
                Guard.Warning(this, $"The rotation of '{PieceId}' is not a multiple of 90°: it is snapped to the closest orientation.");
                transform.rotation = AxisAlignment.SnapRotation(transform.rotation);
            }
        }

        private void OnValidate()
        {
            if ((transform.localScale - Vector3.one).sqrMagnitude > 1e-6f)
            {
                Guard.Warning(this, "The scale of the piece's root object must be (1, 1, 1). Apply scale to the child cubes instead.");
            }
        }
    }
}
