// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace NWH.DWP2.MeshDecimation
{
    /// <summary>
    /// Represents a vertex in the mesh decimation algorithm.
    /// Stores vertex data and relationships for edge collapse operations.
    /// </summary>
    public class Vert
    {
        /// <summary>
        /// Target vertex for edge collapse operation.
        /// </summary>
        public Vert collapse;

        /// <summary>
        /// Edge collapse cost for this vertex.
        /// </summary>
        public float cost;

        /// <summary>
        /// Whether this vertex has been deleted.
        /// </summary>
        public bool deleted;

        /// <summary>
        /// List of triangles that use this vertex.
        /// </summary>
        public List<Tri> face = new();

        /// <summary>
        /// Unique identifier for this vertex.
        /// </summary>
        public int id;

        /// <summary>
        /// List of neighboring vertices connected by edges.
        /// </summary>
        public List<Vert> neighbor = new();

        /// <summary>
        /// 3D position of the vertex.
        /// </summary>
        public Vector3 position;

        /// <summary>
        /// Whether this vertex is marked as selected (prevents collapse if locked).
        /// </summary>
        public bool selected;


        /// <summary>
        /// Initializes a new vertex with position, ID, and selection status.
        /// </summary>
        /// <param name="position">3D position of the vertex.</param>
        /// <param name="id">Unique identifier.</param>
        /// <param name="selected">Whether the vertex is selected.</param>
        public Vert(Vector3 position, int id, bool selected)
        {
            this.position = position;
            this.id       = id;
            this.selected = selected;

            cost     = 0f;
            collapse = null;
        }


        /// <summary>
        /// Removes this vertex and breaks all neighbor connections.
        /// </summary>
        public void RemoveVert()
        {
            Vert nb;
            while (neighbor.Count > 0)
            {
                nb = neighbor[0];
                nb.neighbor.Remove(this);
                neighbor.Remove(nb);
            }

            deleted = true;
        }


        /// <summary>
        /// Checks if this vertex is on the mesh border.
        /// A vertex is on the border if any of its edges is shared by only one triangle.
        /// </summary>
        /// <returns>True if the vertex is on the border, false otherwise.</returns>
        public bool IsBorder()
        {
            int  j;
            int  n = neighbor.Count;
            Vert nb;
            int  face_len;
            Tri  f;
            int  count = 0;

            for (int i = 0; i < n; ++i)
            {
                count    = 0;
                nb       = neighbor[i];
                face_len = face.Count;
                for (j = 0; j < face_len; ++j)
                {
                    f = face[j];
                    if (f.HasVertex(nb))
                    {
                        ++count;
                    }
                }

                if (count == 1)
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Adds a triangle to this vertex's face list.
        /// </summary>
        /// <param name="f">Triangle to add.</param>
        public void AddFace(Tri f)
        {
            face.Add(f);
        }


        /// <summary>
        /// Removes a triangle from this vertex's face list.
        /// </summary>
        /// <param name="f">Triangle to remove.</param>
        public void RemoveFace(Tri f)
        {
            face.Remove(f);
        }


        /// <summary>
        /// Adds a vertex to the neighbor list if not already present.
        /// </summary>
        /// <param name="v">Vertex to add as neighbor.</param>
        public void AddNeighbor(Vert v)
        {
            int i;
            int foundAt = -1;
            int n       = neighbor.Count;

            for (i = 0; i < n; ++i)
            {
                if (neighbor[i] == v)
                {
                    foundAt = i;
                    break;
                }
            }

            if (foundAt == -1)
            {
                neighbor.Add(v);
            }
        }


        /// <summary>
        /// Removes a vertex from the neighbor list if they no longer share any faces.
        /// </summary>
        /// <param name="v">Vertex to potentially remove from neighbors.</param>
        public void RemoveIfNonNeighbor(Vert v)
        {
            int i;
            int foundAt = -1;
            int n       = neighbor.Count;
            Tri f;

            for (i = 0; i < n; ++i)
            {
                if (neighbor[i] == v)
                {
                    foundAt = i;
                    break;
                }
            }

            if (foundAt == -1)
            {
                return;
            }

            n = face.Count;
            for (i = 0; i < n; ++i)
            {
                f = face[i];
                if (f.HasVertex(v))
                {
                    return;
                }
            }

            neighbor.Remove(v);
        }
    }
}