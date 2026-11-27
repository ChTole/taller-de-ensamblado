using System;
using System.Collections;
using System.Collections.Generic;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Pieces;
using UnityEngine;

namespace AssemblyWorkshop.Joints
{
    /// <summary>
    /// Animates fits and removals. Fits when JointDetector reports a valid fit,
    /// and offers undoing the last fit or removing a piece.
    /// </summary>
    [DisallowMultipleComponent]
    public class JointSnapper : MonoBehaviour
    {
        private const float AlignmentThreshold = 0.99f;
        // Step used to check the path when removing a piece
        private const float PathStep = 0.01f;

        [SerializeField] private AssemblyGraph graph;
        [SerializeField] private JointDetector detector;

        [Header("Animation")]
        [Tooltip("Duration of the alignment with the mortise axis (turn and sideways shift), in seconds.")]
        [SerializeField, Min(0.01f)] private float alignDuration = 0.15f;

        [Tooltip("Duration of the tenon insertion, in seconds.")]
        [SerializeField, Min(0.01f)] private float insertDuration = 0.35f;

        [Tooltip("Duration of the extraction when removing, in seconds.")]
        [SerializeField, Min(0.01f)] private float extractDuration = 0.35f;

        [Tooltip("Extra distance the piece moves away when removed, in addition to the tenon length.")]
        [SerializeField, Min(0f)] private float extractionMargin = 0.02f;

        /// <summary>The last fit was undone. Receives its joints.</summary>
        public event Action<IReadOnlyList<JointPair>> Undone;

        /// <summary>A piece was removed. Receives the piece and the joints that were separated.</summary>
        public event Action<AssemblyPiece, IReadOnlyList<JointPair>> Detached;

        public bool IsBusy { get; private set; }

        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(graph, nameof(graph), this) & Guard.Required(detector, nameof(detector), this);
            if (!isValid)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (isValid)
            {
                detector.MatchReleased += OnMatchReleased;
            }
        }

        private void OnDisable()
        {
            if (isValid)
            {
                detector.MatchReleased -= OnMatchReleased;
            }
        }

        private void OnMatchReleased(JointMatch match)
        {
            if (!IsBusy)
            {
                StartCoroutine(SnapRoutine(match));
            }
        }

        /// <summary>
        /// Animated fit: first lines the tenon up with the mortise axis (turning and shifting it), then inserts it.
        /// When finished, AssemblyGraph merges the groups.
        /// </summary>
        private IEnumerator SnapRoutine(JointMatch match)
        {
            IsBusy = true;
            PieceGroup moving = match.MovingGroup;
            PieceGroup target = match.TargetGroup;
            moving.IsLocked = true;
            target.IsLocked = true;

            yield return Align(moving.transform, match.Rotation, match.Pivot, match.LateralOffset, alignDuration);
            yield return Translate(moving.transform, match.AxialOffset, insertDuration);

            moving.IsLocked = false;
            target.IsLocked = false;

            AssemblyPiece movingPiece = match.MovesTenon ? match.Primary.Tenon.Piece : match.Primary.Mortise.Piece;
            graph.Connect(new JointOperation(match.Pairs, movingPiece));
            IsBusy = false;
        }

        /// <summary>Undoes the last fit, pulling out the side that had moved.</summary>
        public DetachResult TryUndoLast()
        {
            if (IsBusy || graph.FindHeldGroup() != null)
            {
                return DetachResult.Busy;
            }

            if (!graph.TryGetLastOperation(out JointOperation operation))
            {
                return DetachResult.NotConnected;
            }

            var pairs = new List<JointPair>(operation.Pairs);
            HashSet<AssemblyPiece> movingSet = ResolveMovingSet(operation.MovingPiece, pairs);
            return Extract(movingSet, pairs, () => Undone?.Invoke(pairs));
        }

        /// <summary>
        /// Removes a piece from its group, sliding it back along the axis of its joints.
        /// Only possible if all of its joints come out in the same direction.
        /// </summary>
        public DetachResult TryDetach(AssemblyPiece piece)
        {
            if (IsBusy || graph.FindHeldGroup() != null)
            {
                return DetachResult.Busy;
            }

            List<JointPair> pairs = graph.GetConnections(piece);
            if (pairs.Count == 0)
            {
                return DetachResult.NotConnected;
            }

            return Extract(new HashSet<AssemblyPiece> { piece }, pairs, () => Detached?.Invoke(piece, pairs));
        }

        private DetachResult Extract(HashSet<AssemblyPiece> movingSet, List<JointPair> pairs, Action onDone)
        {
            if (!TryGetExtraction(movingSet, pairs, out Vector3 direction, out float distance))
            {
                return DetachResult.MultipleDirections;
            }

            if (IsPathBlocked(movingSet, direction, distance))
            {
                return DetachResult.Blocked;
            }

            StartCoroutine(ExtractRoutine(movingSet, pairs, direction * distance, onDone));
            return DetachResult.Detached;
        }

        private IEnumerator ExtractRoutine(HashSet<AssemblyPiece> movingSet, List<JointPair> pairs, Vector3 delta, Action onDone)
        {
            IsBusy = true;

            // Once the joints are separated, the pieces coming out end up in their own group
            graph.Disconnect(pairs);
            PieceGroup moving = null;
            foreach (AssemblyPiece piece in movingSet)
            {
                moving = piece.Group;
                break;
            }

            if (moving != null)
            {
                moving.IsLocked = true;
                yield return Translate(moving.transform, delta, extractDuration);
                moving.IsLocked = false;
            }

            IsBusy = false;
            onDone?.Invoke();
        }

        /// <summary>
        /// Pieces that separate on undo: those joined to the piece that had moved,
        /// ignoring the joints of the operation.
        /// </summary>
        private HashSet<AssemblyPiece> ResolveMovingSet(AssemblyPiece movingPiece, List<JointPair> pairs)
        {
            if (movingPiece != null)
            {
                HashSet<AssemblyPiece> set = graph.CollectConnected(movingPiece, pairs);
                foreach (JointPair pair in pairs)
                {
                    if (set.Contains(pair.Tenon.Piece) || set.Contains(pair.Mortise.Piece))
                    {
                        return set;
                    }
                }
            }

            // If the piece that moved is no longer part of those joints (for example, it was removed earlier),
            // the tenon side is separated
            return graph.CollectConnected(pairs[0].Tenon.Piece, pairs);
        }

        private bool TryGetExtraction(HashSet<AssemblyPiece> movingSet, List<JointPair> pairs,
            out Vector3 direction, out float distance)
        {
            direction = Vector3.zero;
            distance = 0f;

            for (int i = 0; i < pairs.Count; i++)
            {
                JointPair pair = pairs[i];
                bool tenonMoves = movingSet.Contains(pair.Tenon.Piece);
                bool mortiseMoves = movingSet.Contains(pair.Mortise.Piece);

                // Both pieces of the joint would stay on the same side: there is no way to separate them
                if (tenonMoves == mortiseMoves)
                {
                    return false;
                }

                // The tenon comes out against its insertion direction, or the mortise moves along it
                Vector3 insertion = pair.Tenon.transform.forward;
                Vector3 pairDirection = tenonMoves ? -insertion : insertion;

                if (i == 0)
                {
                    direction = pairDirection;
                }
                else if (Vector3.Dot(direction, pairDirection) < AlignmentThreshold)
                {
                    return false;
                }

                distance = Mathf.Max(distance, pair.Tenon.Length);
            }

            distance += extractionMargin;
            return pairs.Count > 0;
        }

        private bool IsPathBlocked(HashSet<AssemblyPiece> movingSet, Vector3 direction, float distance)
        {
            var colliders = new List<BoxCollider>();
            foreach (AssemblyPiece piece in movingSet)
            {
                colliders.AddRange(piece.Colliders);
            }

            LayerMask mask = graph.WorkArea.PieceLayers;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / PathStep));

            for (int i = 1; i <= steps; i++)
            {
                Vector3 offset = direction * (distance * i / steps);
                if (PieceCollision.WouldOverlap(colliders, movingSet.Contains, offset, Quaternion.identity, Vector3.zero, mask))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Turns the object 'rotation' about 'pivot' while shifting it 'offset', both gradually.</summary>
        private static IEnumerator Align(Transform target, Quaternion rotation, Vector3 pivot, Vector3 offset, float duration)
        {
            Vector3 startPosition = target.position;
            Quaternion startRotation = target.rotation;
            float t = 0f;

            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                float s = Mathf.SmoothStep(0f, 1f, t);
                Quaternion step = Quaternion.Slerp(Quaternion.identity, rotation, s);
                target.SetPositionAndRotation(pivot + step * (startPosition - pivot) + offset * s, step * startRotation);
                yield return null;
            }
        }

        private static IEnumerator Translate(Transform target, Vector3 delta, float duration)
        {
            Vector3 start = target.position;
            float t = 0f;

            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                target.position = start + delta * Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }
        }
    }
}
