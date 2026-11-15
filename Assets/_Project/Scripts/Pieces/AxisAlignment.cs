using UnityEngine;

namespace AssemblyWorkshop.Pieces
{
    /// <summary>
    /// Helpers that keep pieces aligned with the world axes, in 90° orientations.
    /// </summary>
    public static class AxisAlignment
    {
        /// <summary>Returns the world axis (±X, ±Y or ±Z) closest to the vector.</summary>
        public static Vector3 SnapToAxis(Vector3 vector)
        {
            float x = Mathf.Abs(vector.x);
            float y = Mathf.Abs(vector.y);
            float z = Mathf.Abs(vector.z);

            if (x >= y && x >= z)
            {
                return new Vector3(Mathf.Sign(vector.x), 0f, 0f);
            }

            if (y >= z)
            {
                return new Vector3(0f, Mathf.Sign(vector.y), 0f);
            }

            return new Vector3(0f, 0f, Mathf.Sign(vector.z));
        }

        /// <summary>
        /// Snaps a rotation to the closest orientation whose angles are multiples of 90°.
        /// Prevents successive rotations from accumulating rounding errors.
        /// </summary>
        public static Quaternion SnapRotation(Quaternion rotation)
        {
            Vector3 forward = SnapToAxis(rotation * Vector3.forward);
            Vector3 up = SnapToAxis(rotation * Vector3.up);
            return Quaternion.LookRotation(forward, up);
        }

        /// <summary>Whether the rotation is already aligned, within a tolerance in degrees.</summary>
        public static bool IsAligned(Quaternion rotation, float toleranceDegrees = 0.5f)
        {
            return Quaternion.Angle(rotation, SnapRotation(rotation)) <= toleranceDegrees;
        }
    }
}
