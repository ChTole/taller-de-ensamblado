using System;
using System.Collections.Generic;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Pieces;
using UnityEngine;

namespace AssemblyWorkshop.Joints
{
    /// <summary>
    /// Record of the furniture being assembled: its pieces, its joints, which pieces are joined to which
    /// and the groups they form. It is the only class that creates, merges and splits groups.
    /// </summary>
    [DisallowMultipleComponent]
    public class AssemblyGraph : MonoBehaviour
    {
        [SerializeField] private WorkArea workArea;

        public event Action<JointPair> JointConnected;
        public event Action<JointPair> JointDisconnected;
        public event Action AssemblyCompleted;

        /// <summary>
        /// A group was released, raised at the exact moment of release, before it starts settling or falling.
        /// </summary>
        public event Action<PieceGroup> GroupReleased;

        public WorkArea WorkArea => workArea;
        public IReadOnlyList<AssemblyPiece> Pieces => pieces;
        public IReadOnlyList<JointPoint> Joints => joints;
        public IReadOnlyList<PieceGroup> Groups => groups;
        public IReadOnlyList<JointPair> Connections => connections;

        /// <summary>Number of joints in the furniture, i.e. its number of tenons.</summary>
        public int TotalJoints { get; private set; }

        public bool IsComplete => TotalJoints > 0 && connections.Count >= TotalJoints;

        private readonly List<AssemblyPiece> pieces = new List<AssemblyPiece>();
        private readonly List<JointPoint> joints = new List<JointPoint>();
        private readonly List<PieceGroup> groups = new List<PieceGroup>();
        private readonly List<JointPair> connections = new List<JointPair>();
        private readonly List<JointOperation> history = new List<JointOperation>();
        private Transform groupsParent;
        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(workArea, nameof(workArea), this);
        }

        /// <summary>
        /// Registers the pieces and joints of an instantiated piece of furniture and creates one group per piece.
        /// Groups are created inside the furniture object.
        /// </summary>
        public bool Register(GameObject furnitureRoot)
        {
            if (!isValid)
            {
                Guard.Error(this, "Cannot register the furniture: configuration is missing.");
                return false;
            }

            if (furnitureRoot == null)
            {
                Guard.Error(this, "The furniture to register is null.");
                return false;
            }

            if (pieces.Count > 0)
            {
                Guard.Error(this, "A piece of furniture is already registered.");
                return false;
            }

            pieces.AddRange(furnitureRoot.GetComponentsInChildren<AssemblyPiece>());
            if (pieces.Count == 0)
            {
                Guard.Error(this, $"'{furnitureRoot.name}' has no pieces (AssemblyPiece).");
                return false;
            }

            int tenons = 0;
            int mortises = 0;
            foreach (JointPoint joint in furnitureRoot.GetComponentsInChildren<JointPoint>())
            {
                if (joint.Piece == null)
                {
                    continue;
                }

                joints.Add(joint);
                if (joint.Kind == JointKind.Tenon)
                {
                    tenons++;
                }
                else
                {
                    mortises++;
                }
            }

            TotalJoints = tenons;

            if (tenons == 0)
            {
                Guard.Warning(this, $"'{furnitureRoot.name}' has no joints (JointPoint).");
            }
            else if (tenons != mortises)
            {
                Guard.Warning(this, $"'{furnitureRoot.name}' has {tenons} tenons and {mortises} mortises: it cannot be completed.");
            }

            groupsParent = furnitureRoot.transform;
            foreach (AssemblyPiece piece in pieces)
            {
                AddGroup(PieceGroup.Create(new[] { piece }, groupsParent, workArea));
            }

            return true;
        }

        public PieceGroup FindHeldGroup()
        {
            foreach (PieceGroup group in groups)
            {
                if (group.IsHeld)
                {
                    return group;
                }
            }

            return null;
        }

        /// <summary>Joins the pieces of the operation and merges their groups into one.</summary>
        public void Connect(JointOperation operation)
        {
            if (operation == null || operation.Pairs.Count == 0)
            {
                return;
            }

            var involved = new List<PieceGroup>();

            foreach (JointPair pair in operation.Pairs)
            {
                pair.Tenon.State = JointState.Engaged;
                pair.Mortise.State = JointState.Engaged;
                pair.Tenon.ConnectedTo = pair.Mortise;
                pair.Mortise.ConnectedTo = pair.Tenon;
                connections.Add(pair);

                AddDistinct(involved, pair.Tenon.Piece.Group);
                AddDistinct(involved, pair.Mortise.Piece.Group);
            }

            history.Add(operation);

            if (involved.Count > 1)
            {
                Merge(involved);
            }

            foreach (JointPair pair in operation.Pairs)
            {
                JointConnected?.Invoke(pair);
            }

            if (IsComplete)
            {
                AssemblyCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Separates the given joints. If a group ends up split, it is rebuilt as one group per part.
        /// </summary>
        public void Disconnect(IReadOnlyList<JointPair> pairs)
        {
            var removed = new HashSet<JointPair>();
            var affected = new List<PieceGroup>();

            foreach (JointPair pair in pairs)
            {
                if (!connections.Remove(pair))
                {
                    continue;
                }

                removed.Add(pair);
                pair.Tenon.State = JointState.Free;
                pair.Mortise.State = JointState.Free;
                pair.Tenon.ConnectedTo = null;
                pair.Mortise.ConnectedTo = null;
                AddDistinct(affected, pair.Tenon.Piece.Group);
            }

            if (removed.Count == 0)
            {
                return;
            }

            foreach (JointOperation operation in history)
            {
                operation.RemovePairs(removed);
            }

            history.RemoveAll(operation => operation.Pairs.Count == 0);

            foreach (PieceGroup group in affected)
            {
                SplitIfNeeded(group);
            }

            foreach (JointPair pair in removed)
            {
                JointDisconnected?.Invoke(pair);
            }
        }

        public bool TryGetLastOperation(out JointOperation operation)
        {
            operation = history.Count > 0 ? history[history.Count - 1] : null;
            return operation != null;
        }

        /// <summary>Active joints of a piece.</summary>
        public List<JointPair> GetConnections(AssemblyPiece piece)
        {
            var result = new List<JointPair>();
            foreach (JointPair pair in connections)
            {
                if (pair.Tenon.Piece == piece || pair.Mortise.Piece == piece)
                {
                    result.Add(pair);
                }
            }

            return result;
        }

        /// <summary>
        /// Pieces joined to 'start', directly or indirectly, ignoring the joints in 'ignored'.
        /// </summary>
        public HashSet<AssemblyPiece> CollectConnected(AssemblyPiece start, ICollection<JointPair> ignored = null)
        {
            var visited = new HashSet<AssemblyPiece> { start };
            var pending = new Stack<AssemblyPiece>();
            pending.Push(start);

            while (pending.Count > 0)
            {
                AssemblyPiece current = pending.Pop();

                foreach (JointPair pair in connections)
                {
                    if (ignored != null && ignored.Contains(pair))
                    {
                        continue;
                    }

                    AssemblyPiece other = null;
                    if (pair.Tenon.Piece == current)
                    {
                        other = pair.Mortise.Piece;
                    }
                    else if (pair.Mortise.Piece == current)
                    {
                        other = pair.Tenon.Piece;
                    }

                    if (other != null && visited.Add(other))
                    {
                        pending.Push(other);
                    }
                }
            }

            return visited;
        }

        private void Merge(List<PieceGroup> involved)
        {
            var members = new List<AssemblyPiece>();
            foreach (PieceGroup group in involved)
            {
                members.AddRange(group.Pieces);
            }

            PieceGroup merged = AddGroup(PieceGroup.Create(members, groupsParent, workArea));

            foreach (PieceGroup group in involved)
            {
                RemoveGroup(group);
                group.Dissolve(merged);
            }
        }

        private void SplitIfNeeded(PieceGroup group)
        {
            var remaining = new List<AssemblyPiece>(group.Pieces);
            var parts = new List<HashSet<AssemblyPiece>>();

            while (remaining.Count > 0)
            {
                HashSet<AssemblyPiece> part = CollectConnected(remaining[0]);
                parts.Add(part);
                remaining.RemoveAll(part.Contains);
            }

            if (parts.Count <= 1)
            {
                return;
            }

            RemoveGroup(group);
            foreach (HashSet<AssemblyPiece> part in parts)
            {
                AddGroup(PieceGroup.Create(part, groupsParent, workArea));
            }

            group.Dissolve(null);
        }

        private PieceGroup AddGroup(PieceGroup group)
        {
            groups.Add(group);
            group.Released += OnGroupReleased;
            return group;
        }

        private void RemoveGroup(PieceGroup group)
        {
            groups.Remove(group);
            group.Released -= OnGroupReleased;
        }

        private void OnGroupReleased(PieceGroup group)
        {
            GroupReleased?.Invoke(group);
        }

        private static void AddDistinct(List<PieceGroup> list, PieceGroup group)
        {
            if (group != null && !list.Contains(group))
            {
                list.Add(group);
            }
        }
    }
}