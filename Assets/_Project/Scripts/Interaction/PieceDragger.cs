using System;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Pieces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AssemblyWorkshop.Interaction
{
    /// <summary>
    /// Grabs, drags and releases groups of pieces, with mouse or touch.
    /// Drag: move across the surface. With vertical mode (Shift, or the touch toggle): move up or down.
    /// Pieces never go through or push each other. Two-finger gestures are left to the camera.
    /// </summary>
    [DisallowMultipleComponent]
    public class PieceDragger : MonoBehaviour
    {
        // Maximum step checked at once, so a fast movement cannot go through thin pieces
        private const float MaxStepLength = 0.01f;
        private const int MaxStepsPerFrame = 60;
        // Maximum pointer movement, in pixels, for a press on empty space to count as a tap
        private const float TapMaxMovement = 12f;

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

        /// <summary>Keeps vertical mode on without holding Shift. Used by the touch controls.</summary>
        public bool VerticalModeLocked { get; private set; }

        private PieceGroup dragged;
        private bool isVerticalMode;
        private Plane dragPlane;
        // Grabbed point in the group's local coordinates, so it stays under the pointer even when the group rotates
        private Vector3 grabLocalPoint;
        // A press on empty space clears the selection only if it ends as a tap
        private bool pendingDeselect;
        private Vector2 pressPosition;
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

            pendingDeselect = false;
            grabAction.action.Disable();
            pointAction.action.Disable();
            verticalModeAction.action.Disable();
        }

        /// <summary>Turns the locked vertical mode on or off. Can be called from a UI Toggle.</summary>
        public void SetVerticalModeLocked(bool locked)
        {
            VerticalModeLocked = locked;
        }

        private void Update()
        {
            // Two or more fingers are a camera gesture
            if (PointerUtility.ActiveTouchCount() >= 2)
            {
                pendingDeselect = false;
                if (dragged != null)
                {
                    Release();
                }

                return;
            }

            Vector2 pointer = pointAction.action.ReadValue<Vector2>();

            if (grabAction.action.WasPressedThisFrame())
            {
                TryGrab(pointer);
            }

            if (pendingDeselect)
            {
                if ((pointer - pressPosition).sqrMagnitude > TapMaxMovement * TapMaxMovement)
                {
                    pendingDeselect = false;
                }
                else if (!grabAction.action.IsPressed())
                {
                    pendingDeselect = false;
                    selection.Select(null);
                }
            }

            if (dragged == null)
            {
                return;
            }

            // The button state is polled, not just the release event,
            // so dragging does not get stuck if the window loses focus
            if (grabAction.action.IsPressed())
            {
                Drag(pointer);
            }
            else
            {
                Release();
            }
        }

        /// <summary>Piece under the pointer, if any and not covered by the UI.</summary>
        public bool TryGetPieceUnderPointer(out AssemblyPiece piece, out RaycastHit hit)
        {
            return TryGetPieceAt(pointAction.action.ReadValue<Vector2>(), out piece, out hit);
        }

        private bool TryGetPieceAt(Vector2 screenPosition, out AssemblyPiece piece, out RaycastHit hit)
        {
            piece = null;
            hit = default;

            if (!isValid || PointerUtility.IsOverUI(screenPosition))
            {
                return false;
            }

            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out hit, maxRayDistance, workArea.PieceLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            piece = hit.collider.GetComponentInParent<AssemblyPiece>();
            return piece != null && piece.Group != null;
        }

        private void TryGrab(Vector2 pointer)
        {
            if (PointerUtility.IsOverUI(pointer))
            {
                return;
            }

            if (!TryGetPieceAt(pointer, out AssemblyPiece piece, out RaycastHit hit))
            {
                pendingDeselect = true;
                pressPosition = pointer;
                return;
            }

            PieceGroup group = piece.Group;
            if (group.IsLocked)
            {
                return;
            }

            selection.Select(group, piece);
            dragged = group;
            isVerticalMode = IsVerticalModeRequested();
            grabLocalPoint = group.transform.InverseTransformPoint(hit.point);
            dragPlane = CreateDragPlane(hit.point);
            group.BeginHold();
            DragStarted?.Invoke(group);
        }

        private void Drag(Vector2 pointer)
        {
            Transform groupTransform = dragged.transform;
            Vector3 grabPoint = groupTransform.TransformPoint(grabLocalPoint);

            // If vertical mode changes while dragging, the plane is rebuilt
            // from the grabbed point so the group does not jump
            bool vertical = IsVerticalModeRequested();
            if (vertical != isVerticalMode)
            {
                isVerticalMode = vertical;
                dragPlane = CreateDragPlane(grabPoint);
            }

            Ray ray = viewCamera.ScreenPointToRay(pointer);
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

        private bool IsVerticalModeRequested()
        {
            return VerticalModeLocked || verticalModeAction.action.IsPressed();
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
    }
}
