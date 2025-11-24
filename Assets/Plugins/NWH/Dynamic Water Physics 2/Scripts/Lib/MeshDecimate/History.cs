// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace NWH.DWP2.MeshDecimation
{
    /// <summary>
    /// Tracks mesh decimation history for undo/redo operations during progressive mesh simplification.
    /// </summary>
    public class History
    {
        /// <summary>
        /// Unique identifier for this history entry.
        /// </summary>
        public int id;

        /// <summary>
        /// List of triangle indices that were removed during this step.
        /// </summary>
        public List<int> removedTriangles = new();

        /// <summary>
        /// List of vertex replacement operations performed during this step.
        /// </summary>
        public List<ArrayList> replacedVertex = new();


        /// <summary>
        /// Records a triangle removal operation.
        /// </summary>
        /// <param name="f">Index of the triangle that was removed.</param>
        public void RemovedTriangle(int f)
        {
            removedTriangles.Add(f);
        }


        /// <summary>
        /// Records a vertex replacement operation including original and new vertex data.
        /// </summary>
        /// <param name="f">Face index.</param>
        /// <param name="u">Original vertex index in the triangle.</param>
        /// <param name="v">Vertex ID being replaced.</param>
        /// <param name="normal">Original vertex normal.</param>
        /// <param name="uv">Original texture coordinates.</param>
        /// <param name="newV">New vertex ID.</param>
        /// <param name="newNormal">New vertex normal.</param>
        /// <param name="newUv">New texture coordinates.</param>
        public void ReplaceVertex(int f, int u, int v, Vector3 normal, Vector2 uv, int newV, Vector3 newNormal,
            Vector2                   newUv)
        {
            ArrayList list = new();
            list.Insert(0, f);
            list.Insert(1, u);
            list.Insert(2, v);
            list.Insert(3, normal);
            list.Insert(4, uv);
            list.Insert(5, newV);
            list.Insert(6, newNormal);
            list.Insert(7, newUv);
            replacedVertex.Add(list);
        }
    }
}