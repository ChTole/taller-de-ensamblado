using AssemblyWorkshop.Common;
using UnityEngine;

namespace AssemblyWorkshop.XRUI
{
    /// <summary>
    /// Keeps a world-space panel in front of the user without gluing it to the head:
    /// the panel stays still while the user looks around, and only catches up when the user
    /// turns or moves beyond a margin. It always faces the user and stays upright.
    /// </summary>
    [DisallowMultipleComponent]
    public class LazyFollow : MonoBehaviour
    {
        [Tooltip("The user's head: CenterEyeAnchor of the camera rig.")]
        [SerializeField] private Transform head;

        [Header("Placement")]
        [Tooltip("Horizontal distance from the head to the panel, in meters.")]
        [SerializeField, Min(0.1f)] private float distance = 0.5f;

        [Tooltip("Height of the panel relative to the eyes, in meters. Negative places it lower.")]
        [SerializeField] private float heightOffset = 0f;

        [Header("Margins")]
        [Tooltip("The panel starts following when it is more than this angle away from where the user looks, in degrees.")]
        [SerializeField, Range(5f, 90f)] private float angleMargin = 35f;

        [Tooltip("The panel starts following when it is farther than this from its place, in meters.")]
        [SerializeField, Min(0.01f)] private float distanceMargin = 0.25f;

        [Tooltip("Approximate time the panel takes to reach its place, in seconds.")]
        [SerializeField, Min(0.01f)] private float smoothTime = 0.3f;

        // Beyond this distance the panel jumps directly, e.g. at the start or after recentering the view
        private const float TeleportDistance = 2f;

        private Vector3 velocity;
        private bool isFollowing;
        private bool isPlaced;

        private void Awake()
        {
            if (!Guard.Required(head, nameof(head), this))
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            isPlaced = false;
        }

        // After the camera rig updates the head pose
        private void LateUpdate()
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                // Looking straight up or down: keep the last direction
                forward = Vector3.ProjectOnPlane(transform.position - head.position, Vector3.up);
                if (forward.sqrMagnitude < 0.0001f)
                {
                    return;
                }
            }

            forward.Normalize();
            Vector3 goal = head.position + forward * distance + Vector3.up * heightOffset;
            float offset = (transform.position - goal).magnitude;

            if (!isPlaced || offset > TeleportDistance)
            {
                transform.position = goal;
                velocity = Vector3.zero;
                isPlaced = true;
                isFollowing = false;
            }
            else
            {
                Vector3 toPanel = Vector3.ProjectOnPlane(transform.position - head.position, Vector3.up);
                float angle = Vector3.Angle(forward, toPanel);

                if (angle > angleMargin || offset > distanceMargin)
                {
                    isFollowing = true;
                }

                if (isFollowing)
                {
                    transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
                    if ((transform.position - goal).magnitude < 0.01f)
                    {
                        isFollowing = false;
                    }
                }
            }

            // Upright and facing the user: a canvas is read from behind its forward axis
            Vector3 facing = Vector3.ProjectOnPlane(transform.position - head.position, Vector3.up);
            if (facing.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            }
        }
    }
}