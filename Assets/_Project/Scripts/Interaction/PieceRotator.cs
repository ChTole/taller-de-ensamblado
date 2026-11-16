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
    /// The public methods can be called from UI buttons, for touch screens.
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
            if (rotateLeftAction.action.WasPressedThisFrame())
            {
                RotateLeft();
            }

            if (rotateRightAction.action.WasPressedThisFrame())
            {
                RotateRight();
            }

            if (tiltForwardAction.action.WasPressedThisFrame())
            {
                TiltForward();
            }

            if (tiltBackAction.action.WasPressedThisFrame())
            {
                TiltBack();
            }
        }

        public void RotateLeft()
        {
            Rotate(Vector3.up, -90f);
        }

        public void RotateRight()
        {
            Rotate(Vector3.up, 90f);
        }

        public void TiltForward()
        {
            Rotate(TiltAxis(), 90f);
        }

        public void TiltBack()
        {
            Rotate(TiltAxis(), -90f);
        }

        /// <summary>Horizontal axis of the camera, snapped to world X or Z so the rotation is exact.</summary>
        private Vector3 TiltAxis()
        {
            return AxisAlignment.SnapToAxis(Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up));
        }

        private void Rotate(Vector3 axis, float angle)
        {
            if (!isValid)
            {
                return;
            }

            PieceGroup group = selection.Selected;
            if (group == null || group.IsLocked)
            {
                return;
            }

            Vector3 pivot = dragger != null && dragger.IsDragging
                ? dragger.GrabPoint
                : group.GetBounds().center;

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
