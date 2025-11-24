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
    /// Represents a triangle in the mesh decimation algorithm.
    /// Stores triangle vertices, normals, UVs, and adjacency information.
    /// </summary>
    public class Tri
    {
        /// <summary>
        /// Original mesh vertex index for vertex 0.
        /// </summary>
        public int defaultIndex0;

        /// <summary>
        /// Original mesh vertex index for vertex 1.
        /// </summary>
        public int defaultIndex1;

        /// <summary>
        /// Original mesh vertex index for vertex 2.
        /// </summary>
        public int defaultIndex2;

        /// <summary>
        /// Whether this triangle has been deleted.
        /// </summary>
        public bool deleted;

        /// <summary>
        /// Unique identifier for this triangle.
        /// </summary>
        public int id;

        /// <summary>
        /// Face normal vector.
        /// </summary>
        public Vector3 normal;

        /// <summary>
        /// Texture coordinate for vertex 0.
        /// </summary>
        public Vector2 uv0;

        /// <summary>
        /// Texture coordinate for vertex 1.
        /// </summary>
        public Vector2 uv1;

        /// <summary>
        /// Texture coordinate for vertex 2.
        /// </summary>
        public Vector2 uv2;

        /// <summary>
        /// First vertex of the triangle.
        /// </summary>
        public Vert v0;

        /// <summary>
        /// Second vertex of the triangle.
        /// </summary>
        public Vert v1;

        /// <summary>
        /// Third vertex of the triangle.
        /// </summary>
        public Vert v2;

        /// <summary>
        /// Vertex normal for vertex 0.
        /// </summary>
        public Vector3 vn0;

        /// <summary>
        /// Vertex normal for vertex 1.
        /// </summary>
        public Vector3 vn1;

        /// <summary>
        /// Vertex normal for vertex 2.
        /// </summary>
        public Vector3 vn2;


        /// <summary>
        /// Initializes a new triangle with vertices, texture coordinates, and establishes vertex relationships.
        /// </summary>
        /// <param name="id">Unique identifier for this triangle.</param>
        /// <param name="v0">First vertex.</param>
        /// <param name="v1">Second vertex.</param>
        /// <param name="v2">Third vertex.</param>
        /// <param name="uv0">Texture coordinate for first vertex.</param>
        /// <param name="uv1">Texture coordinate for second vertex.</param>
        /// <param name="uv2">Texture coordinate for third vertex.</param>
        public Tri(int id, Vert v0, Vert v1, Vert v2, Vector2 uv0, Vector2 uv1, Vector2 uv2)
        {
            this.id  = id;
            this.v0  = v0;
            this.v1  = v1;
            this.v2  = v2;
            this.uv0 = uv0;
            this.uv1 = uv1;
            this.uv2 = uv2;

            RecalculateNormal();

            v0.AddFace(this);
            v1.AddFace(this);
            v2.AddFace(this);

            v0.AddNeighbor(v1);
            v0.AddNeighbor(v2);
            v1.AddNeighbor(v0);
            v1.AddNeighbor(v2);
            v2.AddNeighbor(v0);
            v2.AddNeighbor(v1);
        }


        /// <summary>
        /// Sets the original mesh vertex indices for this triangle.
        /// </summary>
        /// <param name="n0">Original index for vertex 0.</param>
        /// <param name="n1">Original index for vertex 1.</param>
        /// <param name="n2">Original index for vertex 2.</param>
        public void SetDefaultIndices(int n0, int n1, int n2)
        {
            defaultIndex0 = n0;
            defaultIndex1 = n1;
            defaultIndex2 = n2;
        }


        /// <summary>
        /// Removes this triangle and updates vertex relationships.
        /// Records the removal in history for potential undo operations.
        /// </summary>
        /// <param name="his">History object to record the removal.</param>
        public void RemoveTriangle(History his)
        {
            v0.RemoveFace(this);
            v1.RemoveFace(this);
            v2.RemoveFace(this);

            v0.RemoveIfNonNeighbor(v1);
            v0.RemoveIfNonNeighbor(v2);
            v1.RemoveIfNonNeighbor(v0);
            v1.RemoveIfNonNeighbor(v2);
            v2.RemoveIfNonNeighbor(v1);
            v2.RemoveIfNonNeighbor(v0);

            deleted = true;
            his.RemovedTriangle(id);
        }


        /// <summary>
        /// Gets the texture coordinate for a given vertex.
        /// </summary>
        /// <param name="v">Vertex to query.</param>
        /// <returns>Texture coordinate for the vertex, or zero vector if not found.</returns>
        public Vector2 uvAt(Vert v)
        {
            Vector3 vec = v.position;
            if (vec == v0.position)
            {
                return uv0;
            }

            if (vec == v1.position)
            {
                return uv1;
            }

            if (vec == v2.position)
            {
                return uv2;
            }

            return new Vector2();
        }


        /// <summary>
        /// Gets the vertex normal for a given vertex.
        /// </summary>
        /// <param name="v">Vertex to query.</param>
        /// <returns>Vertex normal for the vertex, or zero vector if not found.</returns>
        public Vector3 normalAt(Vert v)
        {
            Vector3 vec = v.position;
            if (vec == v0.position)
            {
                return vn0;
            }

            if (vec == v1.position)
            {
                return vn1;
            }

            if (vec == v2.position)
            {
                return vn2;
            }

            return new Vector3();
        }


        /// <summary>
        /// Sets the texture coordinate for a given vertex.
        /// </summary>
        /// <param name="v">Vertex to update.</param>
        /// <param name="newuv">New texture coordinate.</param>
        public void setUV(Vert v, Vector2 newuv)
        {
            Vector3 vec = v.position;
            if (vec == v0.position)
            {
                uv0 = newuv;
            }
            else if (vec == v1.position)
            {
                uv1 = newuv;
            }
            else if (vec == v2.position)
            {
                uv2 = newuv;
            }
        }


        /// <summary>
        /// Sets the vertex normal for a given vertex.
        /// </summary>
        /// <param name="v">Vertex to update.</param>
        /// <param name="newNormal">New vertex normal.</param>
        public void setVN(Vert v, Vector3 newNormal)
        {
            Vector3 vec = v.position;
            if (vec == v0.position)
            {
                vn0 = newNormal;
            }
            else if (vec == v1.position)
            {
                vn1 = newNormal;
            }
            else if (vec == v2.position)
            {
                vn2 = newNormal;
            }
        }


        /// <summary>
        /// Checks if this triangle contains a given vertex.
        /// </summary>
        /// <param name="v">Vertex to check.</param>
        /// <returns>True if the triangle contains the vertex, false otherwise.</returns>
        public bool HasVertex(Vert v)
        {
            Vector3 vec = v.position;
            return vec == v0.position || vec == v1.position || vec == v2.position;
        }


        /// <summary>
        /// Recalculates the face normal using cross product of edge vectors.
        /// </summary>
        public void RecalculateNormal()
        {
            Vector3 v1pos = v1.position;
            normal = Vector3.Cross(v1pos - v0.position, v2.position - v1pos);
            if (normal.magnitude == 0)
            {
                return;
            }

            normal.Normalize();
        }


        /// <summary>
        /// Recalculates averaged vertex normals based on adjacent face normals.
        /// Only called when normal recalculation is enabled. Smooths normals even at UV seams.
        /// </summary>
        /// <param name="smoothAngleDot">Dot product threshold for normal smoothing.</param>
        public void RecalculateAvgNormals(float smoothAngleDot)
        {
            int       i;
            List<Tri> flist = new();
            List<Tri> slist = new();
            int       n     = flist.Count;
            Tri       f;
            Vector3   fn;

            flist = v0.face;
            slist.Clear();
            for (i = 0; i < n; ++i)
            {
                f  = flist[i];
                fn = f.normal;
                if (fn.x * normal.x + fn.y * normal.y + fn.z * normal.z > smoothAngleDot)
                {
                    vn0 += fn;
                    slist.Add(f);
                }
            }

            vn0.Normalize();
            n = slist.Count;
            for (i = 0; i < n; ++i)
            {
                f = slist[i];
                f.setVN(v0, vn0);
            }

            flist = v1.face;
            n     = flist.Count;
            slist.Clear();
            for (i = 0; i < n; ++i)
            {
                f  = flist[i];
                fn = f.normal;
                if (fn.x * normal.x + fn.y * normal.y + fn.z * normal.z > smoothAngleDot)
                {
                    vn1 += fn;
                    slist.Add(f);
                }
            }

            vn1.Normalize();
            n = slist.Count;
            for (i = 0; i < n; ++i)
            {
                f = slist[i];
                f.setVN(v1, vn1);
            }

            flist = v2.face;
            n     = flist.Count;
            slist.Clear();
            for (i = 0; i < n; ++i)
            {
                f  = flist[i];
                fn = f.normal;
                if (fn.x * normal.x + fn.y * normal.y + fn.z * normal.z > smoothAngleDot)
                {
                    vn2 += fn;
                    slist.Add(f);
                }
            }

            vn2.Normalize();
            n = slist.Count;
            for (i = 0; i < n; ++i)
            {
                f = slist[i];
                f.setVN(v2, vn2);
            }
        }


        /// <summary>
        /// Replaces a vertex in this triangle with a new vertex and updates all relationships.
        /// Records the replacement in history for potential undo operations.
        /// </summary>
        /// <param name="vo">Old vertex to be replaced.</param>
        /// <param name="vnew">New vertex to replace with.</param>
        /// <param name="newUV">New texture coordinate.</param>
        /// <param name="newVN">New vertex normal.</param>
        /// <param name="his">History object to record the replacement.</param>
        public void ReplaceVertex(Vert vo, Vert vnew, Vector2 newUV, Vector3 newVN, History his)
        {
            Vector3 vec             = vo.position;
            Vert    changedVertex   = v2;
            int     changedVertexId = 2;
            Vector3 changedNormal   = vn2;
            Vector2 changedUV       = uv2;

            if (vec == v0.position)
            {
                changedVertex   = v0;
                changedVertexId = 0;
                changedNormal   = vn0;
                changedUV       = uv0;
                v0              = vnew;
                vn0             = newVN;
                uv0             = newUV;
            }
            else if (vec == v1.position)
            {
                changedVertex   = v1;
                changedVertexId = 1;
                changedNormal   = vn1;
                changedUV       = uv1;
                v1              = vnew;
                vn1             = newVN;
                uv1             = newUV;
            }
            else
            {
                v2  = vnew;
                vn2 = newVN;
                uv2 = newUV;
            }

            vo.RemoveFace(this);
            vnew.AddFace(this);

            vo.RemoveIfNonNeighbor(v0);
            v0.RemoveIfNonNeighbor(vo);
            vo.RemoveIfNonNeighbor(v1);
            v1.RemoveIfNonNeighbor(vo);
            vo.RemoveIfNonNeighbor(v2);
            v2.RemoveIfNonNeighbor(vo);

            v0.AddNeighbor(v1);
            v0.AddNeighbor(v2);
            v1.AddNeighbor(v0);
            v1.AddNeighbor(v2);
            v2.AddNeighbor(v0);
            v2.AddNeighbor(v1);

            RecalculateNormal();

            his.ReplaceVertex(id, changedVertexId, changedVertex.id, changedNormal, changedUV, vnew.id, newVN, newUV);
        }
    }
}