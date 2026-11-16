using System.Collections.Generic;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Pieces;
using UnityEngine;

namespace AssemblyWorkshop.Joints
{
    public enum JointKind
    {
        Tenon,
        Mortise
    }

    public enum JointState
    {
        Free,
        Engaged,
        // Reserved for version 2: a joint that is engaged and screwed
        Secured
    }

    /// <summary>
    /// Joint point. Goes on TenonRef (base of the tenon) and on Mortise (entry face of the mortise).
    /// Convention: the Z axis points in the insertion direction and the Y axis defines the roll around it.
    /// Once the joint is assembled, the base of the tenon matches the entry face of the mortise.
    /// </summary>
    [DisallowMultipleComponent]
    public class JointPoint : MonoBehaviour
    {
        [Tooltip("Identifier used in telemetry. If empty, the piece identifier and the object name are used.")]
        [SerializeField] private string jointId;

        [SerializeField] private JointKind kind = JointKind.Tenon;

        [Tooltip("Joint type. A tenon only fits into a mortise with the same profile.")]
        [SerializeField] private JointProfile profile;

        [Tooltip("Tenon: length from base to tip. Mortise: depth, i.e. the thickness of the piece.")]
        [SerializeField, Min(0f)] private float length = 0.045f;

        [Tooltip("Mortises only. A through mortise also accepts the tenon from the opposite face.")]
        [SerializeField] private bool isThrough = true;

        [Tooltip("Optional. If not empty, the joint is only accepted with these points.")]
        [SerializeField] private List<JointPoint> allowedPartners = new List<JointPoint>();

        [Tooltip("Optional. Points this one should be joined to according to the design. " +
                 "A joint with any other compatible point still fits, but counts as incorrect. Empty: any partner is correct.")]
        [SerializeField] private List<JointPoint> expectedPartners = new List<JointPoint>();

        public string JointId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(jointId))
                {
                    return jointId;
                }

                // Outside Play Mode, Piece is not set yet, so it is looked up in the hierarchy
                AssemblyPiece owner = Piece != null ? Piece : GetComponentInParent<AssemblyPiece>(true);
                return $"{(owner != null ? owner.PieceId : "?")}/{name}";
            }
        }

        public JointKind Kind => kind;
        public JointProfile Profile => profile;
        public float Length => length;
        public bool IsThrough => kind == JointKind.Mortise && isThrough;
        public AssemblyPiece Piece { get; private set; }
        public JointState State { get; internal set; }
        public JointPoint ConnectedTo { get; internal set; }
        public bool IsFree => State == JointState.Free;

        private void Awake()
        {
            Piece = GetComponentInParent<AssemblyPiece>();

            if (Piece == null)
            {
                Guard.Error(this, $"'{name}' is not inside a piece (AssemblyPiece): it is ignored.");
            }

            if (length <= 0f)
            {
                Guard.Error(this, $"'{JointId}': 'Length' must be greater than 0.");
            }
        }

        /// <summary>
        /// Whether this point and another can form a joint, regardless of position:
        /// opposite kinds, same profile and, if restricted, allowed partners.
        /// </summary>
        public bool IsCompatibleWith(JointPoint other)
        {
            if (other == null || other == this || other.kind == kind || other.profile != profile)
            {
                return false;
            }

            return AllowsPartner(other) && other.AllowsPartner(this);
        }

        /// <summary>
        /// Whether a joint with the other point is the one the design expects.
        /// Both sides are checked: if either lists expected partners, the other must be among them.
        /// </summary>
        public bool IsExpectedPartner(JointPoint other)
        {
            return other != null && ExpectsPartner(other) && other.ExpectsPartner(this);
        }

        private bool ExpectsPartner(JointPoint other)
        {
            return expectedPartners.Count == 0 || expectedPartners.Contains(other);
        }

        private bool AllowsPartner(JointPoint other)
        {
            return allowedPartners.Count == 0 || allowedPartners.Contains(other);
        }

        private void OnValidate()
        {
            if (length <= 0f)
            {
                Guard.Warning(this, $"'{name}': 'Length' must be greater than 0.");
            }

            if (profile == null)
            {
                Guard.Warning(this, $"'{name}' has no 'Profile': it will only fit points that have no profile either.");
            }

            foreach (JointPoint partner in allowedPartners)
            {
                if (partner != null && partner.kind == kind)
                {
                    Guard.Warning(this, $"'{name}': '{partner.name}' in 'Allowed Partners' is of the same kind and will never fit.");
                }
            }

            foreach (JointPoint partner in expectedPartners)
            {
                if (partner != null && partner.kind == kind)
                {
                    Guard.Warning(this, $"'{name}': '{partner.name}' in 'Expected Partners' is of the same kind and can never be joined.");
                }
            }
        }

        private void OnDrawGizmos()
        {
            // Line in the insertion direction, as long as the tenon or as deep as the mortise,
            // plus a short green line in the direction of the Y axis
            Vector3 position = transform.position;

            Gizmos.color = kind == JointKind.Tenon ? new Color(1f, 0.5f, 0.1f) : new Color(0.2f, 0.8f, 1f);
            Gizmos.DrawLine(position, position + transform.forward * length);
            Gizmos.DrawWireSphere(position, 0.004f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(position, position + transform.up * 0.015f);
        }
    }
}