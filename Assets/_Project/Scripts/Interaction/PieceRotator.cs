using System;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Pieces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AssemblyWorkshop.Interaction
{
    /// <summary>
    /// 90° rotations of the selected group, relative to the camera.
    /// Left/right: around the vertical axis. Forward/back: tip away from or toward the camera.
    /// </summary>
    [DisallowMultipleComponent]
    public class PieceRotator : MonoBehaviour
    {
        [Tooltip("Camera that defines the direction of the rotations.")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PieceSelection selection;

        [Tooltip("Optional. If assigned, a grabbed group rotates around the grabbed point so it stays under the pointer.")]
        [SerializeField] private PieceDragger dragger;

        [Header("Actions")]
        [SerializeField] private InputActionReference rotateLeftAction;
        [SerializeField] private InputActionReference rotateRightAction;
        [SerializeField] private InputActionReference tiltForwardAction;
        [SerializeField] private InputActionReference tiltBackAction;

        /// <summary>A rotation was accepted.</summary>
        public event Action<PieceGroup> Rotated;

        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(viewCamera, nameof(viewCamera), this)
                & Guard.Required(selection, nameof(selection), this)
                & InputGuard.Required(rotateLeftAction, nameof(rotateLeftAction), this)
                & InputGuard.Required(rotateRightAction, nameof(rotateRightAction), this)
                & InputGuard.Required(tiltForwardAction, nameof(tiltForwardAction), this)
                & InputGuard.Required(tiltBackAction, nameof(tiltBackAction), this);

            if (!isValid)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            SetActionsEnabled(true);
        }

        private void OnDisable()
        {
            SetActionsEnabled(false);
        }

        private void Update()
        {
            PieceGroup group = selection.Selected;
            if (group == null || group.IsLocked)
            {
                return;
            }

            Vector3 pivot = dragger != null && dragger.IsDragging
                ? dragger.GrabPoint
                : group.GetBounds().center;

            if (rotateLeftAction.action.WasPressedThisFrame())
            {
                Rotate(group, Vector3.up, -90f, pivot);
            }

            if (rotateRightAction.action.WasPressedThisFrame())
            {
                Rotate(group, Vector3.up, 90f, pivot);
            }

            bool tiltForward = tiltForwardAction.action.WasPressedThisFrame();
            if (tiltForward || tiltBackAction.action.WasPressedThisFrame())
            {
                // Horizontal axis of the camera, snapped to world X or Z so the rotation is exact
                Vector3 tiltAxis = AxisAlignment.SnapToAxis(
                    Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up));
                Rotate(group, tiltAxis, tiltForward ? 90f : -90f, pivot);
            }
        }

        private void Rotate(PieceGroup group, Vector3 axis, float angle, Vector3 pivot)
        {
            if (group.TryRotate(axis, angle, pivot))
            {
                Rotated?.Invoke(group);
            }
        }

        private void SetActionsEnabled(bool enable)
        {
            if (!isValid)
            {
                return;
            }

            foreach (InputActionReference reference in new[] { rotateLeftAction, rotateRightAction, tiltForwardAction, tiltBackAction })
            {
                if (enable)
                {
                    reference.action.Enable();
                }
                else
                {
                    reference.action.Disable();
                }
            }
        }
    }
}
