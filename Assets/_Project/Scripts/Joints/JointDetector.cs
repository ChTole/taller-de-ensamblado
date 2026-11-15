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
        // Cosine of the maximum angle between two axes to consider them aligned (about 8°)
        private const float AlignmentThreshold = 0.99f;

        [SerializeField] private AssemblyGraph graph;

        [Header("Tolerances")]
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
        private PieceGroup trackedGroup;

        private void Awake()
        {
            if (!Guard.Required(graph, nameof(graph), this))
            {
                enabled = false;
            }
        }

        private void Update()
        {
            PieceGroup held = graph.FindHeldGroup();

            if (trackedGroup != null && trackedGroup != held)
            {
                HandleRelease(trackedGroup);
            }

            trackedGroup = held;
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
                    int count = FillEntries(mortise);

                    for (int i = 0; i < count; i++)
                    {
                        if (TryEvaluate(tenon, entries[i], out float score) && score < bestScore)
                        {
                            found = true;
                            bestScore = score;
                            bestPair = new JointPair(tenon, mortise);
                            bestEntry = entries[i];
                            bestMovesTenon = tenon == own;
                        }
                    }
                }
            }

            if (!found)
            {
                return null;
            }

            // Movement that brings the base of the tenon to the entry of the mortise
            Vector3 tenonPosition = bestPair.Tenon.transform.position;
            Vector3 fullDelta = bestMovesTenon
                ? bestEntry.Position - tenonPosition
                : tenonPosition - bestEntry.Position;

            PieceGroup target = (bestMovesTenon ? bestPair.Mortise : bestPair.Tenon).Piece.Group;
            Vector3 movingDirection = bestMovesTenon ? bestEntry.Forward : -bestEntry.Forward;

            var pairs = new List<JointPair> { bestPair };
            CollectSimultaneousPairs(moving, target, fullDelta, movingDirection, pairs);

            // If pieces would overlap in the final position (for example, a second tenon
            // that does not line up with its mortise), the fit is not possible
            if (PieceCollision.WouldOverlap(moving, fullDelta, Quaternion.identity, Vector3.zero, graph.WorkArea.PieceLayers))
            {
                return null;
            }

            Vector3 axial = Vector3.Project(fullDelta, bestEntry.Forward);
            return new JointMatch(moving, target, pairs, bestMovesTenon, fullDelta - axial, axial);
        }

        /// <summary>
        /// Additional joints formed by the same movement, for example when closing a frame,
        /// where one piece enters two mortises at once.
        /// </summary>
        private void CollectSimultaneousPairs(PieceGroup moving, PieceGroup target, Vector3 delta,
            Vector3 movingDirection, List<JointPair> pairs)
        {
            var used = new HashSet<JointPoint> { pairs[0].Tenon, pairs[0].Mortise };

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
                    Vector3 tenonFinal = tenon.transform.position + (movesTenon ? delta : Vector3.zero);
                    int count = FillEntries(mortise);
                    bool added = false;

                    for (int i = 0; i < count && !added; i++)
                    {
                        Entry entry = entries[i];
                        Vector3 direction = movesTenon ? entry.Forward : -entry.Forward;

                        if (Vector3.Dot(direction, movingDirection) < AlignmentThreshold || !IsAligned(tenon, entry))
                        {
                            continue;
                        }

                        Vector3 entryFinal = entry.Position + (movesTenon ? Vector3.zero : delta);
                        if ((tenonFinal - entryFinal).magnitude <= coincidenceTolerance)
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

                        if (IsAligned(tenon, entry))
                        {
                            aligned = true;
                            inRange |= TryEvaluate(tenon, entry, out _);
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
        /// Evaluates whether the tenon is in position to enter through that entry.
        /// The lower the score, the better placed it is.
        /// </summary>
        private bool TryEvaluate(JointPoint tenon, Entry entry, out float score)
        {
            score = float.MaxValue;

            if (!IsAligned(tenon, entry))
            {
                return false;
            }

            Vector3 offset = tenon.transform.position - entry.Position;
            float along = Vector3.Dot(offset, entry.Forward);
            float lateral = (offset - entry.Forward * along).magnitude;

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
        /// The tenon points into the mortise and has the right roll around that axis.
        /// Rolled 180° it also fits; with a square cross-section, any multiple of 90° fits.
        /// </summary>
        private static bool IsAligned(JointPoint tenon, Entry entry)
        {
            Transform tenonTransform = tenon.transform;

            if (Vector3.Dot(tenonTransform.forward, entry.Forward) < AlignmentThreshold)
            {
                return false;
            }

            if (tenon.Profile != null && tenon.Profile.IsSquare)
            {
                return true;
            }

            return Mathf.Abs(Vector3.Dot(tenonTransform.up, entry.Up)) >= AlignmentThreshold;
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
