// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System;
using UnityEngine;

#endregion

namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    /// Serializable representation of a mesh.
    /// Used to store simulation mesh data without keeping mesh references in game files.
    /// </summary>
    [Serializable]
    public class SerializedMesh
    {
        /// <summary>
        /// Triangle indices of the mesh.
        /// </summary>
        [SerializeField]
        public int[] triangles;

        /// <summary>
        /// Vertex positions of the mesh.
        /// </summary>
        [SerializeField]
        public Vector3[] vertices;


        /// <summary>
        /// Serializes a mesh into vertex and triangle arrays.
        /// </summary>
        /// <param name="mesh">Mesh to serialize.</param>
        public void Serialize(Mesh mesh)
        {
            vertices  = mesh.vertices;
            triangles = mesh.triangles;
        }


        /// <summary>
        /// Deserializes the stored data back into a Mesh object.
        /// </summary>
        /// <returns>Reconstructed mesh or null if data is invalid.</returns>
        public Mesh Deserialize()
        {
            if (vertices != null && triangles != null)
            {
                Mesh m = MeshUtility.GenerateMesh(vertices, triangles);
                m.name = "DWP_SIM_MESH";
                return m;
            }

            return null;
        }
    }
}