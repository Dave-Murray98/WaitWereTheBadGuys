// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Utility {
    public class MarchingCubes {
        // Get index in MarchingCubesTables.TriTable for a given cube (8 voxels).
        // A voxel is considered "on" if it belongs to the given region.
        // x, y, z is the position of the min position of the cube.
        public static byte GetMarchingCubesIndex(in Fast3DArray regions, int regionID, int x, int y, int z) {

            // Note: this code is ugly and fast, it was much slower as a loop using Vector math.
            // Read it and weep.

            byte caseIndex = 0;

            int sx = regions.SizeX - 1;
            int sy = regions.SizeY - 1;
            int sz = regions.SizeZ - 1;

            int nx = x + 1;
            int ny = y + 1;
            int nz = z + 1;

            bool xv = x >= 0;
            bool yv = y >= 0;
            bool zv = z >= 0;

            bool nxv = x < sx;
            bool nyv = y < sy;
            bool zbv = z < sz;

            if (yv) {
                if (xv && zv && regions[x, y, z] == regionID) caseIndex |= 1;
                if (nxv && zv && regions[nx, y, z] == regionID) caseIndex |= 2;
                if (nxv && zbv && regions[nx, y, nz] == regionID) caseIndex |= 4;
                if (xv && zbv && regions[x, y, nz] == regionID) caseIndex |= 8;
            }

            if (nyv) {
                if (xv && zv && regions[x, ny, z] == regionID) caseIndex |= 16;
                if (nxv && zv &&  regions[nx, ny, z] == regionID) caseIndex |= 32;
                if (nxv && zbv && regions[nx, ny, nz] == regionID) caseIndex |= 64;
                if (xv && zbv && regions[x, ny, nz] == regionID) caseIndex |= 128;
            }

            return caseIndex;
        }

        // Get index in MarchingCubesTables.TriTable for a given cube (8 voxels).
        // A voxel is considered "on" if it belongs to either of the given regions.
        // x, y, z is the position of the min position of the cube.
        public static byte GetMarchingCubesIndex(in Fast3DArray regions, int regionID1, int regionID2,
                                                 int x, int y, int z) {
            // Note: this code is ugly and fast, it was much slower as a loop

            byte caseIndex = 0;

            int sx = regions.SizeX - 1;
            int sy = regions.SizeY - 1;
            int sz = regions.SizeZ - 1;

            int nx = x + 1;
            int ny = y + 1;
            int nz = z + 1;

            bool xv = x >= 0;
            bool yv = y >= 0;
            bool zv = z >= 0;

            bool nxv = x < sx;
            bool nyv = y < sy;
            bool zbv = z < sz;

            if (yv) {
                if (xv && zv && regions.IsOneOf(x, y, z, regionID1, regionID2)) caseIndex |= 1;
                if (nxv && zv && regions.IsOneOf(nx, y, z, regionID1, regionID2)) caseIndex |= 2;
                if (nxv && zbv && regions.IsOneOf(nx, y, nz, regionID1, regionID2)) caseIndex |= 4;
                if (xv && zbv && regions.IsOneOf(x, y, nz, regionID1, regionID2)) caseIndex |= 8;
            }

            if (nyv) {
                if (xv && zv && regions.IsOneOf(x, ny, z, regionID1, regionID2)) caseIndex |= 16;
                if (nxv && zv &&  regions.IsOneOf(nx, ny, z, regionID1, regionID2)) caseIndex |= 32;
                if (nxv && zbv && regions.IsOneOf(nx, ny, nz, regionID1, regionID2)) caseIndex |= 64;
                if (xv && zbv && regions.IsOneOf(x, ny, nz, regionID1, regionID2)) caseIndex |= 128;
            }

            return caseIndex;
        }

        // Convert an edge index to a vertex position (at its midpoint).
        // Returned values are in the range [0, 2].
        public static int4 GetMarchingCubesVertex(byte edgeIndex, byte caseIndex) {
            EdgeToVertexIndexTableEntry edgeToVertexIndex = MarchingCubesTables.EdgeToVertexIndexTable[edgeIndex];
            byte vertex1Index = edgeToVertexIndex.Index1;
            byte vertex2Index = edgeToVertexIndex.Index2;

            int4 vertex1 = MarchingCubesTables.VertexIndexToPositionTable[vertex1Index];
            int4 vertex2 = MarchingCubesTables.VertexIndexToPositionTable[vertex2Index];

            int4 position = vertex1 + vertex2;

            // Set the w component to indicate whether the edge on which the vertex lies points towards or away
            // from the region. This is used to calculate the normal of the vertex.
            byte vertex1Mask = (byte)(1 << vertex1Index);
            byte vertex2Mask = (byte)(1 << vertex2Index);

            int vertex1Val = (caseIndex & vertex1Mask) != 0 ? 1 : 0;
            int vertex2Val = (caseIndex & vertex2Mask) != 0 ? 1 : 0;
            position.w = vertex1Val - vertex2Val;

            return position;
        }

        /// <summary>
        /// Given a grid space vertex, return a vector that points along the edge that vertex lies on.
        /// </summary>
        /// <param name="gridSpaceVertex">Grid-space vertex.</param>
        /// <returns>The normal-aligned vector.</returns>
        public static int4 GetNormalAlignedVector(int4 gridSpaceVertex) {
            // This gives us a vector where the midpoint axis is 1 and the other two are 0.
            // The axis with a value of 1 indicates which axis the edge is parallel to.
            int4 normalAlignedVector = math.abs(gridSpaceVertex - 1) % 2;
            normalAlignedVector.w = 0;

            // Grid space vertex w component indicates normal direction.
            // +1 means normal points towards positive direction of the edge.
            // After this, slide vector is aligned to the vertex's normal.
            normalAlignedVector *= gridSpaceVertex.w;

            return normalAlignedVector;
        }
    }
}
