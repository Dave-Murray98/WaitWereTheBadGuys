// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

namespace NWH.Common
{
    /// <summary>
    /// Mathematical utility functions for common calculations.
    /// </summary>
    public static class MathUtility
    {
        /// <summary>
        /// Clamps a value to a range and outputs how much it exceeded the range.
        /// Useful for clamping values while preserving overflow information.
        /// </summary>
        /// <param name="x">Value to clamp (will be modified).</param>
        /// <param name="range">Range limit (value will be clamped to [-range, +range]).</param>
        /// <param name="remainder">Amount by which x exceeded the range (output).</param>
        public static void ClampWithRemainder(ref float x, in float range, out float remainder)
        {
            if (x > range)
            {
                remainder = x - range;
                x         = range;
            }
            else if (x < -range)
            {
                remainder = x + range;
                x         = -range;
            }
            else
            {
                remainder = 0;
            }
        }
    }
}