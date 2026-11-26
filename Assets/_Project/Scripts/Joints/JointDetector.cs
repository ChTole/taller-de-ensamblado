using System;
using System.Collections.Generic;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Pieces;
using UnityEngine;

namespace AssemblyWorkshop.Joints
{
    /// <summary>
    /// Looks for possible fits for the group being dragged and, when it is released,
    /// reports either a valid fit or a failed attempt. It moves nothing: it only detects and reports.
    /// </summary>
    [DisallowMultipleComponent]
    public class JointDetector : MonoBehaviour
    {
        [SerializeField] private AssemblyGraph graph;

        [Header("Tolerances")]
        [Tooltip("Maximum angle, in degrees, between the tenon and the mortise axis, and of roll around it. The fit corrects the difference. About 8° suits the mouse; about 15° suits hands.")]
        [SerializeField, Range(1f, 30f)] private float angleTolerance = 8f;

        [Tooltip("Maximum sideways distance between the base of the tenon and the axis of the mortise.")]
        [SerializeField, Min(0f)] private float lateralTolerance = 0.02f;

        [Tooltip("Maximum distance between the tip of the tenon and the entry of the mortise.")]
        [SerializeField, Min(0f)] private float approachDistance = 0.03f;

        [Tooltip("In a simultaneous fit, maximum distance for each additional joint to count as aligned.")]
        [SerializeField, Min(0.0001f)] private float coincidenceTolerance = 0.002f;

        /// <summary>The possible fit for the dragged group changed. Receives null when there is none.</summary>
        public event Action<JointMatch> CandidateChanged;

        /// <summary>A group was released with a valid fit.</summary>
        public event Action<JointMatch> MatchReleased;

        /// <summary>A group was released near a mortise without being able to fit.</summary>
        public event Action<JointFailure> AttemptFailed;

        public JointMatch Current { get; private set; }

        private struct Entry
        {
            public Vector3 Position;
            public Vector3 Forward;
            public Vector3 Up;
        }

        // A through mortise has two entries
        private readonly Entry[] entries = new Entry[2];

        // Cosine of the angle tolerance, to compare it with dot products
        private float AlignmentCosine => Mathf.Cos(angleTolerance * Mathf.Deg2Rad);

        private void Awake()
        {
            if (!Guard.Required(graph, nameof(graph), this))
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (graph != null)
            {
                graph.GroupReleased += HandleRelease;
            }
        }

        private void OnDisable()
        {
            if (graph != null)
            {
                graph.GroupReleased -= HandleRelease;
            }
        }

        private void Update()
        {
            // Only the candidate for the highlight is polled. The release is handled by the event,
            // at the exact pose where the piece was released, before it starts settling or falling.
            PieceGroup held = graph.FindHeldGroup();
            SetCurrent(held != null ? FindMatch(held) : null);
        }

        private void HandleRelease(PieceGroup group)
        {
            SetCurrent(null);

            if (group.IsHeld || group.IsRotating)
            {
                return;
            }

            JointMatch match = FindMatch(group);
            if (match != null)
            {
                MatchReleased?.Invoke(match);
                return;
            }

            JointFailure failure = FindFailure(group);
            if (failure != null)
            {
                AttemptFailed?.Invoke(failure);
            }
        }

        private void SetCurrent(JointMatch match)
        {
            bool changed = (Current == null) != (match == null) || (match != null && !match.IsSamePrimary(Current));
            Current = match;

            if (changed)
            {
                CandidateChanged?.Invoke(match);
            }
        }

        /// <summary>
        /// Finds the best fit for the group, including simultaneous joints.
        /// Returns null if there is none or if the final position overlaps another piece.
        /// </summary>
        public JointMatch FindMatch(PieceGroup moving)
        {
            if (moving == null || moving.IsRotating || moving.IsLocked)
            {
                return null;
            }

            bool found = false;
            float bestScore = float.MaxValue;
            JointPair bestPair = default;
            Entry bestEntry = default;
            bool bestMovesTenon = false;
            Quaternion bestCorrection = Quaternion.identity;

            foreach (JointPoint own in graph.Joints)
            {
                if (!own.IsFree || own.Piece.Group != moving)
                {
                    continue;
                }

                foreach (JointPoint other in graph.Joints)
                {
                    if (!IsAvailablePartner(own, other, moving))
                    {
                        continue;
                    }

                    JointPoint tenon = own.Kind == JointKind.Tenon ? own : other;
                    JointPoint mortise = own.Kind == JointKind.Mortise ? own : other;
                    bool movesTenon = tenon == own;
                    int count = FillEntries(mortise);

                    for (int i = 0; i < count; i++)
                    {
                        if (TryEvaluate(tenon, entries[i], movesTenon, out float score, out Quaternion correction)
                            && score < bestScore)
                        {
                            found = true;
                            bestScore = score;
                            bestPair = new JointPair(tenon, mortise);
                            bestEntry = entries[i];
                            bestMovesTenon = movesTenon;
                            bestCorrection = correction;
                        }
                    }
                }
            }

            if (!found)
            {
                return null;
            }

            // The moving group first turns about the base of the tenon, so that the tenon lines up with the mortise.
            // If the mortise side is the one moving, its entry turns with it.
            Vector3 pivot = bestPair.Tenon.transform.position;
            Entry entry = bestMovesTenon ? bestEntry : Move(bestEntry, bestCorrection, pivot, Vector3.zero);

            // Then it moves so that the base of the tenon reaches the entry of the mortise
            Vector3 fullDelta = bestMovesTenon ? entry.Position - pivot : pivot - entry.Position;

            PieceGroup target = (bestMovesTenon ? bestPair.Mortise : bestPair.Tenon).Piece.Group;
            Vector3 movingDirection = bestMovesTenon ? entry.Forward : -entry.Forward;

            var pairs = new List<JointPair> { bestPair };
            CollectSimultaneousPairs(moving, target, bestCorrection, pivot, fullDelta, movingDirection, pairs);

            // If pieces would overlap in the final position (for example, a second tenon
            // that does not line up with its mortise), the fit is not possible
            if (PieceCollision.WouldOverlap(moving, fullDelta, bestCorrection, pivot, graph.WorkArea.PieceLayers))
            {
                return null;
            }

            Vector3 axial = Vector3.Project(fullDelta, entry.Forward);
            return new JointMatch(moving, target, pairs, bestMovesTenon, bestCorrection, pivot, fullDelta - axial, axial);
        }

        /// <summary>
        /// Additional joints formed by the same movement, for example when closing a frame,
        /// where one piece enters two mortises at once. Joints of the moving group are evaluated
        /// at their final pose: after the correction and the translation.
        /// </summary>
        private void CollectSimultaneousPairs(PieceGroup moving, PieceGroup target, Quaternion correction,
            Vector3 pivot, Vector3 delta, Vector3 movingDirection, List<JointPair> pairs)
        {
            var used = new HashSet<JointPoint> { pairs[0].Tenon, pairs[0].Mortise };
            float cosine = AlignmentCosine;

            foreach (JointPoint own in graph.Joints)
            {
                if (!own.IsFree || own.Piece.Group != moving || used.Contains(own))
                {
                    continue;
                }

                foreach (JointPoint other in graph.Joints)
                {
                    if (other.Piece.Group != target || used.Contains(other) || !IsAvailablePartner(own, other, moving))
                    {
                        continue;
                    }

                    JointPoint tenon = own.Kind == JointKind.Tenon ? own : other;
                    JointPoint mortise = own.Kind == JointKind.Mortise ? own : other;
                    bool movesTenon = tenon == own;

                    Transform tenonTransform = tenon.transform;
                    Vector3 tenonPosition = tenonTransform.position;
                    Vector3 tenonForward = tenonTransform.forward;
                    Vector3 tenonUp = tenonTransform.up;

                    if (movesTenon)
                    {
                        tenonPosition = pivot + correction * (tenonPosition - pivot) + delta;
                        tenonForward = correction * tenonForward;
                        tenonUp = correction * tenonUp;
                    }

                    int count = FillEntries(mortise);
                    bool added = false;

                    for (int i = 0; i < count && !added; i++)
                    {
                        Entry entry = movesTenon ? entries[i] : Move(entries[i], correction, pivot, delta);
                        Vector3 direction = movesTenon ? entry.Forward : -entry.Forward;

                        if (Vector3.Dot(direction, movingDirection) < cosine
                            || !IsAligned(tenonForward, tenonUp, IsSquare(tenon), entry))
                        {
                            continue;
                        }

                        if ((tenonPosition - entry.Position).magnitude <= coincidenceTolerance)
                        {
                            pairs.Add(new JointPair(tenon, mortise));
                            used.Add(tenon);
                            used.Add(mortise);
                            added = true;
                        }
                    }

                    if (added)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>Finds the closest joint that could not be formed and classifies the reason.</summary>
        private JointFailure FindFailure(PieceGroup moving)
        {
            JointFailure best = null;
            float bestDistance = float.MaxValue;

            foreach (JointPoint own in graph.Joints)
            {
                if (!own.IsFree || own.Piece.Group != moving)
                {
                    continue;
                }

                foreach (JointPoint other in graph.Joints)
                {
                    if (!other.IsFree || other.Kind == own.Kind || other.Piece.Group == moving)
                    {
                        continue;
                    }

                    JointPoint tenon = own.Kind == JointKind.Tenon ? own : other;
                    JointPoint mortise = own.Kind == JointKind.Mortise ? own : other;
                    bool movesTenon = tenon == own;
                    Transform tenonTransform = tenon.transform;
                    Vector3 tip = tenonTransform.position + tenonTransform.forward * tenon.Length;

                    float distance = float.MaxValue;
                    bool aligned = false;
                    bool inRange = false;
                    int count = FillEntries(mortise);

                    for (int i = 0; i < count; i++)
                    {
                        Entry entry = entries[i];
                        distance = Mathf.Min(distance,
                            Mathf.Min((tenonTransform.position - entry.Position).magnitude, (tip - entry.Position).magnitude));

                        if (IsAligned(tenonTransform.forward, tenonTransform.up, IsSquare(tenon), entry))
                        {
                            aligned = true;
                            inRange |= TryEvaluate(tenon, entry, movesTenon, out _, out _);
                        }
                    }

                    if (distance > tenon.Length + approachDistance || distance >= bestDistance)
                    {
                        continue;
                    }

                    JointFailureReason reason;
                    if (!own.IsCompatibleWith(other))
                    {
                        reason = JointFailureReason.Incompatible;
                    }
                    else if (!aligned)
                    {
                        reason = JointFailureReason.Orientation;
                    }
                    else
                    {
                        reason = inRange ? JointFailureReason.Blocked : JointFailureReason.Position;
                    }

                    best = new JointFailure(tenon, mortise, moving, reason);
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static bool IsAvailablePartner(JointPoint own, JointPoint other, PieceGroup moving)
        {
            return other.IsFree
                && other.Piece.Group != moving
                && !other.Piece.Group.IsLocked
                && own.IsCompatibleWith(other);
        }

        /// <summary>
        /// Evaluates whether the tenon is in position to enter through that entry, once the moving group
        /// is turned to line it up. The lower the score, the better placed it is.
        /// 'correction' is the rotation to apply to the moving group, about the base of the tenon.
        /// </summary>
        private bool TryEvaluate(JointPoint tenon, Entry entry, bool movesTenon, out float score, out Quaternion correction)
        {
            score = float.MaxValue;

            if (!TryGetCorrection(tenon, entry, movesTenon, out correction))
            {
                return false;
            }

            // The base of the tenon is the pivot of the correction, so it does not move.
            // If the mortise side moves, its entry turns about that point.
            Vector3 pivot = tenon.transform.position;
            Entry aligned = movesTenon ? entry : Move(entry, correction, pivot, Vector3.zero);

            Vector3 offset = pivot - aligned.Position;
            float along = Vector3.Dot(offset, aligned.Forward);
            float lateral = (offset - aligned.Forward * along).magnitude;

            // 'along' is negative while the base of the tenon is in front of the entry.
            // At -Length, the tip touches the entry.
            if (along > 0.001f || along < -(tenon.Length + approachDistance) || lateral > lateralTolerance)
            {
                return false;
            }

            score = lateral + Mathf.Abs(along + tenon.Length);
            return true;
        }

        /// <summary>
        /// Rotation that lines the tenon up with the entry: the same axis and the closest valid roll.
        /// It is applied to the moving group: the tenon side, or the mortise side in reverse.
        /// Returns false if the difference is beyond the angle tolerance.
        /// </summary>
        private bool TryGetCorrection(JointPoint tenon, Entry entry, bool movesTenon, out Quaternion correction)
        {
            correction = Quaternion.identity;
            Transform tenonTransform = tenon.transform;
            bool isSquare = IsSquare(tenon);

            if (!IsAligned(tenonTransform.forward, tenonTransform.up, isSquare, entry))
            {
                return false;
            }

            Vector3 up = ClosestValidUp(tenonTransform.up, entry, isSquare);
            Quaternion tenonGoal = Quaternion.LookRotation(entry.Forward, up);
            Quaternion toGoal = tenonGoal * Quaternion.Inverse(tenonTransform.rotation);
            correction = movesTenon ? toGoal : Quaternion.Inverse(toGoal);
            return true;
        }

        /// <summary>
        /// The tenon points into the mortise and has a valid roll around that axis, within the angle tolerance.
        /// Rolled 180° it also fits; with a square cross-section, any roll fits and is corrected to the closest 90°.
        /// </summary>
        private bool IsAligned(Vector3 tenonForward, Vector3 tenonUp, bool isSquare, Entry entry)
        {
            float cosine = AlignmentCosine;

            if (Vector3.Dot(tenonForward, entry.Forward) < cosine)
            {
                return false;
            }

            return isSquare || Mathf.Abs(Vector3.Dot(tenonUp, entry.Up)) >= cosine;
        }

        /// <summary>Roll the tenon should have in the mortise: the valid "up" closest to the current one.</summary>
        private static Vector3 ClosestValidUp(Vector3 tenonUp, Entry entry, bool isSquare)
        {
            Vector3 best = Vector3.Dot(tenonUp, entry.Up) >= 0f ? entry.Up : -entry.Up;

            if (isSquare)
            {
                Vector3 right = Vector3.Cross(entry.Up, entry.Forward);
                Vector3 side = Vector3.Dot(tenonUp, right) >= 0f ? right : -right;
                if (Vector3.Dot(tenonUp, side) > Vector3.Dot(tenonUp, best))
                {
                    best = side;
                }
            }

            return best;
        }

        private static bool IsSquare(JointPoint tenon)
        {
            return tenon.Profile != null && tenon.Profile.IsSquare;
        }

        /// <summary>The entry after turning it about 'pivot' and translating it.</summary>
        private static Entry Move(Entry entry, Quaternion rotation, Vector3 pivot, Vector3 translation)
        {
            return new Entry
            {
                Position = pivot + rotation * (entry.Position - pivot) + translation,
                Forward = rotation * entry.Forward,
                Up = rotation * entry.Up
            };
        }

        private int FillEntries(JointPoint mortise)
        {
            Transform mortiseTransform = mortise.transform;
            entries[0] = new Entry
            {
                Position = mortiseTransform.position,
                Forward = mortiseTransform.forward,
                Up = mortiseTransform.up
            };

            if (!mortise.IsThrough)
            {
                return 1;
            }

            // Entry through the opposite face, with the insertion direction reversed
            entries[1] = new Entry
            {
                Position = mortiseTransform.position + mortiseTransform.forward * mortise.Length,
                Forward = -mortiseTransform.forward,
                Up = mortiseTransform.up
            };
            return 2;
        }
    }
}
