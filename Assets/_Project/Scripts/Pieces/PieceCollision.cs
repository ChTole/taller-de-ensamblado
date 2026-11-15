using System;
using System.Collections.Generic;
using UnityEngine;

namespace AssemblyWorkshop.Pieces
{
    /// <summary>
    /// Overlap queries between pieces, without physics simulation: no piece ever pushes another.
    /// Only Box Colliders are considered, and only overlaps with other pieces (not with the surface).
    /// </summary>
    public static class PieceCollision
    {
        // Margin subtracted from every box, so that two pieces resting on each other
        // or separated by a joint's clearance do not count as overlapping
        private const float Skin = 0.0005f;

        private const int SearchIterations = 6;

        /// <summary>
        /// Whether the group would overlap another piece after rotating it about 'pivot'
        /// and translating it. For a plain translation, 'rotation' is Quaternion.identity.
        /// </summary>
        public static bool WouldOverlap(PieceGroup group, Vector3 translation, Quaternion rotation,
            Vector3 pivot, LayerMask mask)
        {
            return WouldOverlap(group.Colliders, piece => piece.Group == group, translation, rotation, pivot, mask);
        }

        /// <summary>
        /// General version: 'colliders' are the boxes being moved, and 'isOwn' tells which pieces
        /// move with them, so they are not counted as overlaps.
        /// </summary>
        public static bool WouldOverlap(IReadOnlyList<BoxCollider> colliders, Func<AssemblyPiece, bool> isOwn,
            Vector3 translation, Quaternion rotation, Vector3 pivot, LayerMask mask)
        {
            // Pieces are moved through their Transform, so physics must be synced before querying it
            Physics.SyncTransforms();

            foreach (BoxCollider box in colliders)
            {
                if (box == null || !box.enabled || box.isTrigger)
                {
                    continue;
                }

                Transform boxTransform = box.transform;
                Vector3 center = boxTransform.TransformPoint(box.center);
                center = pivot + rotation * (center - pivot) + translation;

                Vector3 scale = boxTransform.lossyScale;
                Vector3 halfExtents = new Vector3(
                    Mathf.Max(0f, Mathf.Abs(box.size.x * scale.x) * 0.5f - Skin),
                    Mathf.Max(0f, Mathf.Abs(box.size.y * scale.y) * 0.5f - Skin),
                    Mathf.Max(0f, Mathf.Abs(box.size.z * scale.z) * 0.5f - Skin));

                Collider[] hits = Physics.OverlapBox(center, halfExtents, rotation * boxTransform.rotation,
                    mask, QueryTriggerInteraction.Ignore);

                foreach (Collider hit in hits)
                {
                    AssemblyPiece other = hit.GetComponentInParent<AssemblyPiece>();
                    if (other != null && !isOwn(other))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the part of the translation the group can perform without overlapping.
        /// If the full translation overlaps, the contact point is found by bisection.
        /// </summary>
        public static Vector3 LimitTranslation(PieceGroup group, Vector3 delta, LayerMask mask)
        {
            if (!WouldOverlap(group, delta, Quaternion.identity, Vector3.zero, mask))
            {
                return delta;
            }

            // If it already overlaps, moving is allowed so it can get free
            if (WouldOverlap(group, Vector3.zero, Quaternion.identity, Vector3.zero, mask))
            {
                return delta;
            }

            float free = 0f;
            float blocked = 1f;
            for (int i = 0; i < SearchIterations; i++)
            {
                float middle = (free + blocked) * 0.5f;
                if (WouldOverlap(group, delta * middle, Quaternion.identity, Vector3.zero, mask))
                {
                    blocked = middle;
                }
                else
                {
                    free = middle;
                }
            }

            return delta * free;
        }
    }
}
