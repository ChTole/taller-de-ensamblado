using System.Collections.Generic;
using AssemblyWorkshop.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AssemblyWorkshop.CameraControl
{
    /// <summary>
    /// Orbit camera: always looks at the target and moves over a hemisphere around it.
    /// Mouse: right button and drag to orbit, wheel to zoom.
    /// Touch: two fingers dragging to orbit, pinch to zoom.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraMovement : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Point the camera always looks at. An empty object at the center of the work surface.")]
        [SerializeField] private Transform target;

        [Header("Initial View")]
        [Tooltip("Angle around the surface, in degrees. At 0, the camera sits on the -Z side looking toward +Z.")]
        [SerializeField] private float initialHorizontalAngle = 0f;
        [SerializeField, Range(0f, 89f)] private float initialElevation = 50f;
        [SerializeField, Min(0.01f)] private float initialDistance = 1.5f;

        [Header("Limits")]
        [SerializeField, Range(0f, 89f)] private float minElevation = 20f;
        [SerializeField, Range(0f, 89f)] private float maxElevation = 80f;
        [SerializeField, Min(0.01f)] private float minDistance = 0.6f;
        [SerializeField, Min(0.01f)] private float maxDistance = 4f;

        [Header("Sensitivity")]
        [Tooltip("Degrees the camera rotates per pixel of mouse movement.")]
        [SerializeField, Min(0f)] private float orbitSensitivity = 0.2f;
        [Tooltip("Fraction of the distance that changes with each wheel step.")]
        [SerializeField, Range(0.01f, 0.5f)] private float zoomStep = 0.1f;
        [SerializeField] private bool invertVertical = false;
        [Tooltip("Degrees the camera rotates per pixel of two-finger movement.")]
        [SerializeField, Min(0f)] private float touchOrbitSensitivity = 0.15f;

        [Header("Smoothing")]
        [Tooltip("Higher values respond faster. At 0 there is no smoothing.")]
        [SerializeField, Min(0f)] private float smoothing = 12f;

        [Header("Actions")]
        [SerializeField] private InputActionReference orbitAction;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference zoomAction;

        // Values set by the user with the mouse
        private float goalAngle;
        private float goalElevation;
        private float goalDistance;

        // Values applied to the camera, which approach the ones above
        private float currentAngle;
        private float currentElevation;
        private float currentDistance;

        private bool isOrbiting;
        private bool isValid;

        // Two-finger gesture state
        private readonly List<TouchControl> sceneTouches = new List<TouchControl>();
        private float previousPinchDistance = -1f;

        private void Awake()
        {
            isValid = Guard.Required(target, nameof(target), this)
                & InputGuard.Required(orbitAction, nameof(orbitAction), this)
                & InputGuard.Required(lookAction, nameof(lookAction), this)
                & InputGuard.Required(zoomAction, nameof(zoomAction), this);

            if (!isValid)
            {
                enabled = false;
                return;
            }

            ResetView(immediate: true);
        }

        private void OnEnable()
        {
            if (!isValid)
            {
                return;
            }

            orbitAction.action.Enable();
            lookAction.action.Enable();
            zoomAction.action.Enable();
        }

        private void OnDisable()
        {
            if (!isValid)
            {
                return;
            }

            orbitAction.action.Disable();
            lookAction.action.Disable();
            zoomAction.action.Disable();
            isOrbiting = false;
            previousPinchDistance = -1f;
        }

        private void LateUpdate()
        {
            ReadInput();
            ReadTouchGestures();
            ApplyView(Time.deltaTime);
        }

        /// <summary>Returns to the initial view. With immediate set to true, it jumps without a transition.</summary>
        public void ResetView(bool immediate = false)
        {
            goalAngle = initialHorizontalAngle;
            goalElevation = Mathf.Clamp(initialElevation, minElevation, maxElevation);
            goalDistance = Mathf.Clamp(initialDistance, minDistance, maxDistance);

            if (immediate)
            {
                currentAngle = goalAngle;
                currentElevation = goalElevation;
                currentDistance = goalDistance;
            }
        }

        private void ReadInput()
        {
            // Do not orbit or zoom while the pointer is over the UI
            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (orbitAction.action.WasPressedThisFrame() && !pointerOverUI)
            {
                isOrbiting = true;
            }

            if (orbitAction.action.WasReleasedThisFrame())
            {
                isOrbiting = false;
            }

            if (isOrbiting)
            {
                Vector2 delta = lookAction.action.ReadValue<Vector2>();
                goalAngle += delta.x * orbitSensitivity;

                float vertical = delta.y * orbitSensitivity;
                goalElevation += invertVertical ? vertical : -vertical;
                goalElevation = Mathf.Clamp(goalElevation, minElevation, maxElevation);
            }

            float wheel = zoomAction.action.ReadValue<Vector2>().y;
            if (!pointerOverUI && !Mathf.Approximately(wheel, 0f))
            {
                // Only the wheel direction is used, because its value varies across systems and browsers
                goalDistance *= wheel > 0f ? 1f - zoomStep : 1f + zoomStep;
                goalDistance = Mathf.Clamp(goalDistance, minDistance, maxDistance);
            }
        }

        /// <summary>
        /// Two fingers on the scene: their average movement orbits, and the change in their distance zooms.
        /// Fingers that started on the UI are ignored.
        /// </summary>
        private void ReadTouchGestures()
        {
            if (PointerUtility.GetSceneTouches(sceneTouches) < 2)
            {
                previousPinchDistance = -1f;
                return;
            }

            TouchControl first = sceneTouches[0];
            TouchControl second = sceneTouches[1];
            Vector2 firstPosition = first.position.ReadValue();
            Vector2 secondPosition = second.position.ReadValue();
            float distance = Vector2.Distance(firstPosition, secondPosition);

            if (previousPinchDistance < 0f)
            {
                previousPinchDistance = distance;
                return;
            }

            Vector2 delta = (first.delta.ReadValue() + second.delta.ReadValue()) * 0.5f;
            goalAngle += delta.x * touchOrbitSensitivity;
            float vertical = delta.y * touchOrbitSensitivity;
            goalElevation = Mathf.Clamp(goalElevation + (invertVertical ? vertical : -vertical), minElevation, maxElevation);

            if (distance > 0f)
            {
                // Fingers apart: the distance shrinks and the camera gets closer
                goalDistance = Mathf.Clamp(goalDistance * (previousPinchDistance / distance), minDistance, maxDistance);
            }

            previousPinchDistance = distance;
        }

        private void ApplyView(float deltaTime)
        {
            // Exponential smoothing: behaves the same at any frame rate
            float t = smoothing > 0f ? 1f - Mathf.Exp(-smoothing * deltaTime) : 1f;

            currentAngle = Mathf.Lerp(currentAngle, goalAngle, t);
            currentElevation = Mathf.Lerp(currentElevation, goalElevation, t);
            currentDistance = Mathf.Lerp(currentDistance, goalDistance, t);

            Quaternion rotation = Quaternion.Euler(currentElevation, currentAngle, 0f);
            transform.SetPositionAndRotation(
                target.position + rotation * new Vector3(0f, 0f, -currentDistance),
                rotation);
        }

        private void OnValidate()
        {
            if (maxElevation < minElevation)
            {
                maxElevation = minElevation;
            }

            if (maxDistance < minDistance)
            {
                maxDistance = minDistance;
            }
        }
    }
}