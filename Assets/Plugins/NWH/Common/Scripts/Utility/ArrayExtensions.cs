// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System;

#endregion

namespace NWH.Common.Utility
{
    /// <summary>
    /// Extension methods for array manipulation.
    /// </summary>
    public static class ArrayExtensions
    {
        /// <summary>
        /// Efficiently fills an array by repeating a pattern of values.
        /// Uses doubling strategy for performance.
        /// </summary>
        /// <typeparam name="T">Type of array elements.</typeparam>
        /// <param name="destinationArray">Array to fill.</param>
        /// <param name="value">Pattern of values to repeat throughout the array.</param>
        public static void Fill<T>(this T[] destinationArray, params T[] value)
        {
            int destinationLength = destinationArray.Length;
            if (destinationLength == 0)
            {
                return;
            }

            int valueLength = value.Length;

            // set the initial array value
            Array.Copy(value, destinationArray, valueLength);

            int arrayToFillHalfLength = destinationLength / 2;
            int copyLength;

            for (copyLength = valueLength; copyLength < arrayToFillHalfLength; copyLength <<= 1)
            {
                Array.Copy(destinationArray, 0, destinationArray, copyLength, copyLength);
            }

            Array.Copy(destinationArray, 0, destinationArray, copyLength,
                       destinationLength - copyLength);
        }
    }
}