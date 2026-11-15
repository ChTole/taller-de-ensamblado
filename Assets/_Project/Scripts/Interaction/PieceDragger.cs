using System;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Pieces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AssemblyWorkshop.Interaction
{
    /// <summary>
    /// Grabs, drags and releases groups of pieces.
    /// Drag: move across the surface. With vertical mode (Shift): move up or down.
    /// Pieces never go through or push each other.
    /// </summary>
    [DisallowMultipleComponent]
    public class PieceDragger : MonoBehaviour
    {
        // Maximum step checked at once, so a fast movement cannot go through thin pieces
        private const float MaxStepLength = 0.01f;
        private const int MaxStepsPerFrame = 60;

        [Tooltip("Camera used for pointing.")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private WorkArea workArea;
        [SerializeField] private PieceSelection selection;
        [SerializeField, Min(0.1f)] private float maxRayDistance = 20f;

        [Header("Actions")]
        [SerializeField] private InputActionReference grabAction;
        [SerializeField] private InputActionReference pointAction;
        [SerializeField] private InputActionReference verticalModeAction;

        public event Action<PieceGroup> DragStarted;
        public event Action<PieceGroup> DragEnded;

        public bool IsDragging => dragged != null;

        /// <summary>Point where the group was grabbed, in world coordinates.</summary>
        public Vector3 GrabPoint => dragged != null ? dragged.transform.TransformPoint(grabLocalPoint) : Vector3.zero;

        private PieceGroup dragged;
        private bool isVerticalMode;
        private Plane dragPlane;
        // Grabbed point in the group's local coordinates, so it stays under the pointer even when the group rotates
        private Vector3 grabLocalPoint;
        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(viewCamera, nameof(viewCamera), this)
                & Guard.Required(workArea, nameof(workArea), this)
                & Guard.Required(selection, nameof(selection), this)
                & InputGuard.Required(grabAction, nameof(grabAction), this)
                & InputGuard.Required(pointAction, nameof(pointAction), this)
                & InputGuard.Required(verticalModeAction, nameof(verticalModeAction), this);

            if (!isValid)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!isValid)
            {
                return;
            }

            grabAction.action.Enable();
            pointAction.action.Enable();
            verticalModeAction.action.Enable();
        }

        private void OnDisable()
        {
            if (!isValid)
            {
                return;
            }

            if (dragged != null)
            {
                Release();
            }

            grabAction.action.Disable();
            pointAction.action.Disable();
            verticalModeAction.action.Disable();
        }

        private void Update()
        {
            if (grabAction.action.WasPressedThisFrame())
            {
                TryGrab();
            }

            if (dragged == null)
            {
                return;
            }

            // The button state is polled, not just the release event,
            // so dragging does not get stuck if the window loses focus
            if (grabAction.action.IsPressed())
            {
                Drag();
            }
            else
            {
                Release();
            }
        }

        /// <summary>Piece under the pointer, if any and not covered by the UI.</summary>
        public bool TryGetPieceUnderPointer(out AssemblyPiece piece, out RaycastHit hit)
        {
            piece = null;
            hit = default;

            if (!isValid || IsPointerOverUI())
            {
                return false;
            }

            if (!Physics.Raycast(CurrentRay(), out hit, maxRayDistance, workArea.PieceLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            piece = hit.collider.GetComponentInParent<AssemblyPiece>();
            return piece != null && piece.Group != null;
        }

        private void TryGrab()
        {
            if (IsPointerOverUI())
            {
                return;
            }

            if (!TryGetPieceUnderPointer(out AssemblyPiece piece, out RaycastHit hit))
            {
                // Click on empty space: clear the selection
                selection.Select(null);
                return;
            }

            PieceGroup group = piece.Group;
            if (group.IsLocked)
            {
                return;
            }

            selection.Select(group);
            dragged = group;
            isVerticalMode = verticalModeAction.action.IsPressed();
            grabLocalPoint = group.transform.InverseTransformPoint(hit.point);
            dragPlane = CreateDragPlane(hit.point);
            group.BeginHold();
            DragStarted?.Invoke(group);
        }

        private void Drag()
        {
            Transform groupTransform = dragged.transform;
            Vector3 grabPoint = groupTransform.TransformPoint(grabLocalPoint);

            // If vertical mode changes while dragging, the plane is rebuilt
            // from the grabbed point so the group does not jump
            bool vertical = verticalModeAction.action.IsPressed();
            if (vertical != isVerticalMode)
            {
                isVerticalMode = vertical;
                dragPlane = CreateDragPlane(grabPoint);
            }

            Ray ray = CurrentRay();
            if (!dragPlane.Raycast(ray, out float distance))
            {
                return;
            }

            // Position at which the grabbed point stays under the pointer
            Vector3 goal = ray.GetPoint(distance) - (grabPoint - groupTransform.position);
            Vector3 current = groupTransform.position;
            Vector3 desired = current;

            if (isVerticalMode)
            {
                desired.y = goal.y;
            }
            else
            {
                desired.x = goal.x;
                desired.z = goal.z;
            }

            desired = workArea.Clamp(desired, dragged.BottomOffset);
            MoveWithCollisions(dragged, desired - current);
        }

        private void Release()
        {
            PieceGroup group = dragged;
            dragged = null;

            if (group != null)
            {
                group.EndHold();
                DragEnded?.Invoke(group);
            }
        }

        /// <summary>
        /// Moves the group in short steps. If a step overlaps, it moves up to the contact
        /// and tries to slide along the axes that are still free. It never pushes another piece.
        /// </summary>
        private void MoveWithCollisions(PieceGroup group, Vector3 delta)
        {
            int steps = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / MaxStepLength), 1, MaxStepsPerFrame);
            Vector3 step = delta / steps;

            for (int i = 0; i < steps; i++)
            {
                Vector3 moved = MoveLimited(group, step);
                Vector3 remaining = step - moved;

                if (remaining.sqrMagnitude < 1e-10f)
                {
                    continue;
                }

                Vector3 slid = MoveLimited(group, new Vector3(remaining.x, 0f, 0f))
                    + MoveLimited(group, new Vector3(0f, remaining.y, 0f))
                    + MoveLimited(group, new Vector3(0f, 0f, remaining.z));

                if ((moved + slid).sqrMagnitude < 1e-10f)
                {
                    break;
                }
            }
        }

        private Vector3 MoveLimited(PieceGroup group, Vector3 delta)
        {
            if (delta.sqrMagnitude < 1e-12f)
            {
                return Vector3.zero;
            }

            Vector3 allowed = PieceCollision.LimitTranslation(group, delta, workArea.PieceLayers);
            group.transform.position += allowed;
            return allowed;
        }

        /// <summary>
        /// Horizontal plane to move across the surface, or a vertical plane facing the camera to move up and down.
        /// </summary>
        private Plane CreateDragPlane(Vector3 grabPoint)
        {
            Vector3 normal = Vector3.up;

            if (isVerticalMode)
            {
                normal = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up);
                if (normal.sqrMagnitude < 0.0001f)
                {
                    normal = Vector3.ProjectOnPlane(viewCamera.transform.up, Vector3.up);
                }
            }

            return new Plane(normal.normalized, grabPoint);
        }

        private Ray CurrentRay()
        {
            return viewCamera.ScreenPointToRay(pointAction.action.ReadValue<Vector2>());
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
