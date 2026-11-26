using System.Collections.Generic;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Pieces;
using Oculus.Interaction;
using UnityEngine;

namespace AssemblyWorkshop.XRInteraction
{
    /// <summary>
    /// Connects a piece group with the Meta grab and with physics. Goes on the root of the group template
    /// assigned to AssemblyGraph, next to the Rigidbody and the Grabbable.
    /// Tells the group when it is grabbed and released, keeps it still while a joint is being animated,
    /// and limits the speed it carries when released so pieces cannot be thrown.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class GrabbableGroupBody : MonoBehaviour
    {
        [Tooltip("Grabbable of this template. It reports when hands or controllers grab and release the group.")]
        [SerializeField] private Grabbable grabbable;

        [Header("Physics")]
        [Tooltip("Mass of each piece, in kilograms. The group mass is this value times its number of pieces.")]
        [SerializeField, Min(0.01f)] private float massPerPiece = 0.5f;

        [Tooltip("Maximum speed of a released group, in meters per second.")]
        [SerializeField, Min(0f)] private float maxReleaseSpeed = 1f;

        [Tooltip("Maximum angular speed of a released group, in radians per second.")]
        [SerializeField, Min(0f)] private float maxReleaseAngularSpeed = 3f;

        // Hands or controllers currently holding the group
        private readonly HashSet<int> holders = new HashSet<int>();

        /// <summary>The piece closest to where the group was last grabbed. Commands such as "detach" act on it.</summary>
        public AssemblyPiece GrabbedPiece { get; private set; }

        private Rigidbody body;
        private PieceGroup group;
        private bool isValid;
        private bool wasLocked;
        private RigidbodyInterpolation interpolation;
        private bool limitPending;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            isValid = Guard.Required(grabbable, nameof(grabbable), this);
        }

        private void OnEnable()
        {
            if (isValid)
            {
                grabbable.WhenPointerEventRaised += OnPointerEvent;
            }
        }

        private void OnDisable()
        {
            if (isValid)
            {
                grabbable.WhenPointerEventRaised -= OnPointerEvent;
            }
        }

        private void Start()
        {
            // PieceGroup is added right after the template is instantiated, so it is looked up here and not in Awake
            group = GetComponent<PieceGroup>();
            if (group == null)
            {
                Guard.Error(this, "No PieceGroup found: this object must be the 'Group Template' of AssemblyGraph, not a scene object.");
                enabled = false;
                return;
            }

            body.mass = massPerPiece * Mathf.Max(1, group.Pieces.Count);
        }

        private void OnPointerEvent(PointerEvent evt)
        {
            if (group == null)
            {
                return;
            }

            switch (evt.Type)
            {
                case PointerEventType.Select:
                    if (holders.Add(evt.Identifier) && holders.Count == 1)
                    {
                        GrabbedPiece = FindClosestPiece(evt.Pose.position);
                        group.BeginHold();
                    }

                    break;

                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    if (holders.Remove(evt.Identifier) && holders.Count == 0)
                    {
                        // Released at this exact pose: AssemblyGraph raises GroupReleased before physics moves it
                        limitPending = true;
                        group.EndHold();
                    }

                    break;
            }
        }

        private AssemblyPiece FindClosestPiece(Vector3 point)
        {
            AssemblyPiece closest = null;
            float bestDistance = float.MaxValue;

            foreach (AssemblyPiece piece in group.Pieces)
            {
                foreach (BoxCollider box in piece.Colliders)
                {
                    float distance = (box.ClosestPoint(point) - point).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        closest = piece;
                    }
                }
            }

            return closest;
        }

        private void FixedUpdate()
        {
            if (group == null)
            {
                return;
            }

            // While a joint is being animated, the animation moves the group through its Transform, not physics.
            // Interpolation is turned off meanwhile, so it does not pull the group back to the last physics pose.
            if (group.IsLocked)
            {
                if (!wasLocked)
                {
                    interpolation = body.interpolation;
                    body.interpolation = RigidbodyInterpolation.None;
                    body.isKinematic = true;
                    wasLocked = true;
                }

                limitPending = false;
                return;
            }

            if (wasLocked && holders.Count == 0)
            {
                body.interpolation = interpolation;
                body.isKinematic = false;
                body.WakeUp();
                wasLocked = false;
            }

            if (limitPending && !body.isKinematic)
            {
                limitPending = false;
                body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, maxReleaseSpeed);
                body.angularVelocity = Vector3.ClampMagnitude(body.angularVelocity, maxReleaseAngularSpeed);
            }
        }
    }
}
