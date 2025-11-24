// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;

#endregion

namespace NWH.Common.Utility
{
    /// <summary>
    /// Extension methods for advanced Quaternion operations.
    /// Provides interpolation methods with control over rotation direction.
    /// </summary>
    public static class QuaternionExtensions
    {
        /// <summary>
        /// Linear interpolation between two quaternions with optional short/long path control.
        /// Unlike Unity's Quaternion.Lerp, this allows choosing rotation direction.
        /// </summary>
        /// <param name="p">Starting rotation.</param>
        /// <param name="q">Target rotation.</param>
        /// <param name="t">Interpolation factor (0 to 1).</param>
        /// <param name="shortWay">True for shortest rotation path, false for longest.</param>
        /// <returns>Interpolated quaternion.</returns>
        public static Quaternion Lerp(Quaternion p, Quaternion q, float t, bool shortWay)
        {
            if (shortWay)
            {
                float dot = Quaternion.Dot(p, q);
                if (dot < 0.0f)
                {
                    return Lerp(ScalarMultiply(p, -1.0f), q, t, true);
                }
            }

            Quaternion r = Quaternion.identity;
            r.x = p.x * (1f - t) + q.x * t;
            r.y = p.y * (1f - t) + q.y * t;
            r.z = p.z * (1f - t) + q.z * t;
            r.w = p.w * (1f - t) + q.w * t;
            return r;
        }


        /// <summary>
        /// Spherical linear interpolation between two quaternions with optional short/long path control.
        /// Provides smooth rotation interpolation with control over rotation direction.
        /// </summary>
        /// <param name="p">Starting rotation.</param>
        /// <param name="q">Target rotation.</param>
        /// <param name="t">Interpolation factor (0 to 1).</param>
        /// <param name="shortWay">True for shortest rotation path, false for longest.</param>
        /// <returns>Interpolated quaternion.</returns>
        public static Quaternion Slerp(Quaternion p, Quaternion q, float t, bool shortWay)
        {
            float dot = Quaternion.Dot(p, q);
            if (shortWay)
            {
                if (dot < 0.0f)
                {
                    return Slerp(ScalarMultiply(p, -1.0f), q, t, true);
                }
            }

            float      angle    = Mathf.Acos(dot);
            Quaternion first    = ScalarMultiply(p, Mathf.Sin((1f - t) * angle));
            Quaternion second   = ScalarMultiply(q, Mathf.Sin(t * angle));
            float      division = 1f / Mathf.Sin(angle);
            return ScalarMultiply(Add(first, second), division);
        }


        /// <summary>
        /// Multiplies all components of a quaternion by a scalar value.
        /// </summary>
        /// <param name="input">Input quaternion.</param>
        /// <param name="scalar">Scalar multiplier.</param>
        /// <returns>Scaled quaternion.</returns>
        public static Quaternion ScalarMultiply(Quaternion input, float scalar)
        {
            return new Quaternion(input.x * scalar, input.y * scalar, input.z * scalar, input.w * scalar);
        }


        /// <summary>
        /// Adds two quaternions component-wise.
        /// </summary>
        /// <param name="p">First quaternion.</param>
        /// <param name="q">Second quaternion.</param>
        /// <returns>Component-wise sum.</returns>
        public static Quaternion Add(Quaternion p, Quaternion q)
        {
            return new Quaternion(p.x + q.x, p.y + q.y, p.z + q.z, p.w + q.w);
        }
    }
}