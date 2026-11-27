using System;
using System.Collections.Generic;
using AssemblyWorkshop.Common;
using UnityEngine;

namespace AssemblyWorkshop.Pieces
{
    /// <summary>
    /// A set of pieces that move together. Every piece always belongs to one group:
    /// at first, its own; once joined, the group of all the connected pieces.
    /// The group is what gets grabbed, rotated and settled onto the surface (or, with physics, what has the rigidbody).
    /// Groups are created and dissolved by AssemblyGraph.
    /// </summary>
    [DisallowMultipleComponent]
    public class PieceGroup : MonoBehaviour
    {
        public event Action<PieceGroup> Grabbed;
        public event Action<PieceGroup> Released;

        /// <summary>
        /// Raised when the group stops existing. The second parameter is the group that replaces it,
        /// or null if it was split into several.
        /// </summary>
        public event Action<PieceGroup, PieceGroup> Dissolved;

        public IReadOnlyList<AssemblyPiece> Pieces => pieces;
        public IReadOnlyList<BoxCollider> Colliders => colliders;
        public bool IsHeld { get; private set; }

        /// <summary>A locked group cannot be grabbed or rotated, and does not settle onto the surface.</summary>
        public bool IsLocked { get; set; }

        /// <summary>A rotation requested with TryRotate is still being animated.</summary>
        public bool IsRotating => hasRotationGoal;

        /// <summary>Vertical distance between the group position and its bottom.</summary>
        public float BottomOffset => transform.position.y - GetBounds().min.y;

        private readonly List<AssemblyPiece> pieces = new List<AssemblyPiece>();
        private readonly List<BoxCollider> colliders = new List<BoxCollider>();
        private WorkArea workArea;
        private Quaternion goalRotation = Quaternion.identity;
        private Vector3 rotationPivot;
        private bool hasRotationGoal;
        private bool isDissolved;

        /// <summary>
        /// Creates a group with the given pieces. Its pivot is placed at the center of the pieces.
        /// If a template is given, the group object is an instance of it (for example, with a rigidbody
        /// and grab components); otherwise it is an empty object.
        /// </summary>
        public static PieceGroup Create(IEnumerable<AssemblyPiece> members, Transform parent, WorkArea area,
            GameObject template = null)
        {
            if (area == null)
            {
                throw new ArgumentNullException(nameof(area));
            }

            var list = new List<AssemblyPiece>(members);
            Vector3 center = ComputeCenter(list);
            GameObject groupObject = template != null
                ? Instantiate(template, center, Quaternion.identity, parent)
                : new GameObject("Group");

            if (template == null)
            {
                groupObject.transform.SetParent(parent, worldPositionStays: false);
                groupObject.transform.SetPositionAndRotation(center, Quaternion.identity);
            }

            PieceGroup group = groupObject.AddComponent<PieceGroup>();
            group.workArea = area;

            foreach (AssemblyPiece piece in list)
            {
                piece.transform.SetParent(groupObject.transform, worldPositionStays: true);
                piece.Group = group;
                group.pieces.Add(piece);
                group.colliders.AddRange(piece.Colliders);
            }

            groupObject.name = list.Count == 1 ? $"Group ({list[0].PieceId})" : $"Group ({list.Count} pieces)";
            return group;
        }

        /// <summary>
        /// Destroys the group. AssemblyGraph calls it after moving its pieces to another group.
        /// </summary>
        public void Dissolve(PieceGroup successor)
        {
            if (isDissolved)
            {
                return;
            }

            foreach (AssemblyPiece piece in pieces)
            {
                if (piece != null && piece.Group == this)
                {
                    Guard.Error(this, $"The group is being dissolved, but '{piece.PieceId}' still belongs to it.");
                }
            }

            isDissolved = true;
            IsHeld = false;
            Dissolved?.Invoke(this, successor);
            Destroy(gameObject);
        }

        private void Update()
        {
            if (IsLocked || isDissolved)
            {
                return;
            }

            if (hasRotationGoal)
            {
                Quaternion next = Quaternion.RotateTowards(
                    transform.rotation, goalRotation, workArea.RotationSpeed * Time.deltaTime);
                Quaternion step = next * Quaternion.Inverse(transform.rotation);
                transform.SetPositionAndRotation(rotationPivot + step * (transform.position - rotationPivot), next);
                hasRotationGoal = Quaternion.Angle(next, goalRotation) > 0.01f;
            }

            if (workArea.SettlePieces)
            {
                KeepOnSurface();
            }
        }

        /// <summary>
        /// Only when the work area settles pieces (without physics).
        /// Keeps the group from going through the surface and, when it is not held, lowers it until it rests
        /// on the surface or on another piece. While rotating, it rises and lowers as if tipping over.
        /// </summary>
        private void KeepOnSurface()
        {
            float gap = GetBounds().min.y - workArea.SurfaceHeight;

            if (gap < 0f)
            {
                transform.position += Vector3.up * -gap;
                return;
            }

            if (IsHeld || gap <= 0.0001f)
            {
                return;
            }

            Vector3 step = Vector3.down * Mathf.Min(gap, workArea.SettleSpeed * Time.deltaTime);
            transform.position += PieceCollision.LimitTranslation(this, step, workArea.PieceLayers);
        }

        public void BeginHold()
        {
            IsHeld = true;
            Grabbed?.Invoke(this);
        }

        public void EndHold()
        {
            IsHeld = false;
            Released?.Invoke(this);
        }

        /// <summary>
        /// Rotates the group 'angle' degrees around a world axis, about 'pivot'.
        /// The result is always a multiple of 90°. Returns false if the final position overlaps another piece.
        /// </summary>
        public bool TryRotate(Vector3 worldAxis, float angle, Vector3 pivot)
        {
            if (IsLocked || isDissolved)
            {
                return false;
            }

            // Without a rotation in progress, the base is the current rotation: the group may have been
            // rotated by other means, such as a hand
            Quaternion current = hasRotationGoal ? goalRotation : AxisAlignment.SnapRotation(transform.rotation);
            Quaternion newGoal = AxisAlignment.SnapRotation(Quaternion.AngleAxis(angle, worldAxis) * current);
            Quaternion delta = newGoal * Quaternion.Inverse(transform.rotation);
            LayerMask mask = workArea.PieceLayers;

            // If it already overlaps (because of how it was placed in the scene), rotating is allowed so it can get free
            bool overlapsNow = PieceCollision.WouldOverlap(this, Vector3.zero, Quaternion.identity, Vector3.zero, mask);
            if (!overlapsNow && PieceCollision.WouldOverlap(this, Vector3.zero, delta, pivot, mask))
            {
                return false;
            }

            goalRotation = newGoal;
            rotationPivot = pivot;
            hasRotationGoal = true;
            return true;
        }

        /// <summary>Box containing every piece of the group, in world coordinates.</summary>
        public Bounds GetBounds()
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(transform.position, Vector3.zero);

            foreach (AssemblyPiece piece in pieces)
            {
                foreach (Renderer pieceRenderer in piece.Renderers)
                {
                    if (!hasBounds)
                    {
                        bounds = pieceRenderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(pieceRenderer.bounds);
                    }
                }
            }

            return bounds;
        }

        private static Vector3 ComputeCenter(List<AssemblyPiece> list)
        {
            bool hasBounds = false;
            Bounds bounds = default;
            Vector3 sum = Vector3.zero;

            foreach (AssemblyPiece piece in list)
            {
                sum += piece.transform.position;
                foreach (Renderer pieceRenderer in piece.Renderers)
                {
                    if (!hasBounds)
                    {
                        bounds = pieceRenderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(pieceRenderer.bounds);
                    }
                }
            }

            if (hasBounds)
            {
                return bounds.center;
            }

            return list.Count > 0 ? sum / list.Count : Vector3.zero;
        }
    }
}
