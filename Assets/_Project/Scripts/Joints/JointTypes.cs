using System;
using System.Collections.Generic;
using AssemblyWorkshop.Pieces;
using UnityEngine;

namespace AssemblyWorkshop.Joints
{
    /// <summary>A tenon and a mortise, either joined or candidates to be joined.</summary>
    public readonly struct JointPair : IEquatable<JointPair>
    {
        public JointPair(JointPoint tenon, JointPoint mortise)
        {
            Tenon = tenon;
            Mortise = mortise;
        }

        public JointPoint Tenon { get; }
        public JointPoint Mortise { get; }

        public bool Equals(JointPair other) => Tenon == other.Tenon && Mortise == other.Mortise;
        public override bool Equals(object obj) => obj is JointPair other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Tenon, Mortise);
    }

    /// <summary>
    /// A possible fit: which group moves, toward which one, which joints are formed
    /// and the required movement, split into lateral alignment and insertion.
    /// </summary>
    public sealed class JointMatch
    {
        public JointMatch(PieceGroup movingGroup, PieceGroup targetGroup, IReadOnlyList<JointPair> pairs,
            bool movesTenon, Vector3 lateralOffset, Vector3 axialOffset)
        {
            MovingGroup = movingGroup;
            TargetGroup = targetGroup;
            Pairs = pairs;
            MovesTenon = movesTenon;
            LateralOffset = lateralOffset;
            AxialOffset = axialOffset;
        }

        public PieceGroup MovingGroup { get; }
        public PieceGroup TargetGroup { get; }

        /// <summary>The joints that are formed. The first one is the primary joint; the rest are simultaneous.</summary>
        public IReadOnlyList<JointPair> Pairs { get; }

        public JointPair Primary => Pairs[0];
        public bool MovesTenon { get; }
        public Vector3 LateralOffset { get; }
        public Vector3 AxialOffset { get; }

        public bool IsSamePrimary(JointMatch other)
        {
            return other != null && other.MovingGroup == MovingGroup && other.Primary.Equals(Primary);
        }
    }

    public enum JointFailureReason
    {
        // The tenon does not point toward the mortise, or is rolled around its axis
        Orientation,
        // Correctly oriented, but off the entry
        Position,
        // Different profile or piece not allowed
        Incompatible,
        // Correctly oriented and placed, but another piece prevents the fit
        Blocked
    }

    /// <summary>A fit attempt that could not be completed.</summary>
    public sealed class JointFailure
    {
        public JointFailure(JointPoint tenon, JointPoint mortise, PieceGroup movingGroup, JointFailureReason reason)
        {
            Tenon = tenon;
            Mortise = mortise;
            MovingGroup = movingGroup;
            Reason = reason;
        }

        public JointPoint Tenon { get; }
        public JointPoint Mortise { get; }
        public PieceGroup MovingGroup { get; }
        public JointFailureReason Reason { get; }
    }

    /// <summary>
    /// A completed fit, with all of its joints. It is the unit that "undo" reverts.
    /// </summary>
    public sealed class JointOperation
    {
        private readonly List<JointPair> pairs;

        public JointOperation(IEnumerable<JointPair> pairs, AssemblyPiece movingPiece)
        {
            this.pairs = new List<JointPair>(pairs);
            MovingPiece = movingPiece;
        }

        public IReadOnlyList<JointPair> Pairs => pairs;

        /// <summary>One of the pieces that moved to fit. Defines which side is separated on undo.</summary>
        public AssemblyPiece MovingPiece { get; }

        internal void RemovePairs(HashSet<JointPair> removed)
        {
            pairs.RemoveAll(removed.Contains);
        }
    }

    public enum DetachResult
    {
        Detached,
        // There are no fits to undo, or the piece is not joined
        NotConnected,
        // The piece is joined in more than one direction
        MultipleDirections,
        // Another piece prevents removing it
        Blocked,
        // A movement is in progress
        Busy
    }
}
