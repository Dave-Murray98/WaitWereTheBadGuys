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

namespace NWH.DWP2.MiConvexHull
{
    /// <summary>
    /// Unity-specific vertex implementation for convex hull calculations.
    /// </summary>
    public class Vertex : IVertex
    {
        /// <summary>
        /// Initializes a new vertex with specified coordinates.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="z">Z coordinate.</param>
        public Vertex(double x, double y, double z)
        {
            Position = new double[3] { x, y, z, };
        }


        /// <summary>
        /// Initializes a new vertex from a Unity Vector3.
        /// </summary>
        /// <param name="ver">Unity Vector3 position.</param>
        public Vertex(Vector3 ver)
        {
            Position = new double[3] { ver.x, ver.y, ver.z, };
        }


        /// <summary>
        /// Position of the vertex in 3D space.
        /// </summary>
        public double[] Position { get; set; }


        /// <summary>
        /// Converts the vertex position to a Unity Vector3.
        /// </summary>
        /// <returns>Unity Vector3 representation of the vertex position.</returns>
        public Vector3 ToVec()
        {
            return new Vector3((float)Position[0], (float)Position[1], (float)Position[2]);
        }
    }
}