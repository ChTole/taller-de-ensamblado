using AssemblyWorkshop.Common;
using UnityEngine;

namespace AssemblyWorkshop.Pieces
{
    /// <summary>
    /// Work area: the surface where pieces rest, its limits and the movement rules for pieces.
    /// The center and surface height are taken from this object. The area ignores its rotation.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorkArea : MonoBehaviour
    {
        [Tooltip("Size of the area on X and Z. The center of a piece cannot leave it.")]
        [SerializeField] private Vector2 size = new Vector2(2f, 2f);

        [Tooltip("Maximum height above the surface that the bottom of a piece can be lifted to.")]
        [SerializeField, Min(0f)] private float maxLiftHeight = 0.5f;

        [Tooltip("Layers of the pieces, used for selection and collision checks.")]
        [SerializeField] private LayerMask pieceLayers = ~0;

        [Header("Piece Movement")]
        [Tooltip("Rotation speed, in degrees per second.")]
        [SerializeField, Min(1f)] private float rotationSpeed = 540f;

        [Tooltip("Lower released pieces onto the surface and keep them from going through it. Turn it off when the pieces use physics.")]
        [SerializeField] private bool settlePieces = true;

        [Tooltip("Speed at which a released piece settles onto the surface, in meters per second.")]
        [SerializeField, Min(0.01f)] private float settleSpeed = 1.5f;

        public float SurfaceHeight => transform.position.y;
        public LayerMask PieceLayers => pieceLayers;
        public float RotationSpeed => rotationSpeed;
        public bool SettlePieces => settlePieces;
        public float SettleSpeed => settleSpeed;

        /// <summary>
        /// Clamps a position to the area and to the allowed height.
        /// bottomOffset is the vertical distance between the position and the bottom of the piece.
        /// </summary>
        public Vector3 Clamp(Vector3 position, float bottomOffset)
        {
            float minY = SurfaceHeight + bottomOffset;
            position.y = Mathf.Clamp(position.y, minY, minY + maxLiftHeight);

            Vector3 center = transform.position;
            Vector2 half = size * 0.5f;
            position.x = Mathf.Clamp(position.x, center.x - half.x, center.x + half.x);
            position.z = Mathf.Clamp(position.z, center.z - half.y, center.z + half.y);

            return position;
        }

        private void OnValidate()
        {
            if (size.x <= 0f || size.y <= 0f)
            {
                Guard.Warning(this, "'Size' must be greater than 0 on both X and Z.");
            }

            if (pieceLayers.value == 0)
            {
                Guard.Warning(this, "'Piece Layers' includes no layers: no piece can be selected.");
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0f, size.y));
        }
    }
}
