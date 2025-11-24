// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System.Collections;

#endregion

namespace NWH.DWP2.MeshDecimation
{
    /// <summary>
    /// Comparer for sorting vertices by their edge collapse cost during mesh decimation.
    /// </summary>
    public class Comparer : IComparer
    {
        private Vert vx;
        private Vert vy;


        /// <summary>
        /// Compares two vertices based on their edge collapse cost.
        /// </summary>
        /// <param name="x">First vertex to compare.</param>
        /// <param name="y">Second vertex to compare.</param>
        /// <returns>-1 if x has lower cost, 0 if equal, 1 if x has higher cost.</returns>
        public int Compare(object x, object y)
        {
            vx = (Vert)x;
            vy = (Vert)y;
            if (vx == vy)
            {
                return 0;
            }

            if (vx.cost < vy.cost)
            {
                return -1;
            }

            return 1;
        }
    }
}