// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct VolumeTriangleMeshToIndicesJob : IJob {
        // Scaled x2 as the actual positions have half values.
        [ReadOnly] public NativeArray<UnsafeList<int4>> Vertices;

        public int3 VoxelCounts;
        public BakingNavVolumeMeshInfo Mesh;
        public float VoxelSize;
        public NativeBounds VolumeBounds;

        public unsafe void Execute() {
            UnsafeArray<int> vertexIndices =
                new((VoxelCounts.x + 1) * (VoxelCounts.y + 1) * (VoxelCounts.z + 1) * 4, Allocator.Temp, true);

            try {
                Mesh.RegionTriangleLists[0] = ProcessRegion(0, ref vertexIndices);

                // Don't share indices between blocking triangles and actual regions.
                UnsafeUtility.MemClear((void*) vertexIndices.Pointer, vertexIndices.MemorySize);

                // Regions are mostly independent, but they can share vertices.
                for (int regionIndex = 1; regionIndex < Vertices.Length; regionIndex++) {
                    Mesh.RegionTriangleLists[regionIndex] = ProcessRegion(regionIndex, ref vertexIndices);
                }
            } finally {
                vertexIndices.Dispose();
            }
        }

        private UnsafeList<int> ProcessRegion(int regionIndex, ref UnsafeArray<int> regionVertexIndices) {
            UnsafeList<int4> regionVertices = Vertices[regionIndex];

            if (regionVertices.Length == 0) return default;

            UnsafeList<int> triList = new(regionVertices.Length, Allocator.Persistent);

            for (int triIndex = 0; triIndex < regionVertices.Length - 2; triIndex += 3) {
                int4 vertex1 = regionVertices[triIndex + 0];
                int4 vertex2 = regionVertices[triIndex + 1];
                int4 vertex3 = regionVertices[triIndex + 2];

                // Add vertices to the mesh.
                int vertex1Index = AddVertex(vertex1, ref regionVertexIndices);
                int vertex2Index = AddVertex(vertex2, ref regionVertexIndices);
                int vertex3Index = AddVertex(vertex3, ref regionVertexIndices);

                if (vertex1Index == vertex2Index || vertex1Index == vertex3Index || vertex2Index == vertex3Index) {
                    Debug.LogError(
                        $"Two of the vertices resolved to the same index: ({vertex1.x}, {vertex1.y}, {vertex1.z}), ({vertex2.x}, {vertex2.y}, {vertex2.z}), ({vertex3.x}, {vertex3.y}, {vertex3.z}).");
                }

                // Create triangle.
                AddTriangle(ref triList, regionIndex, vertex1Index, vertex2Index, vertex3Index);
            }

            return triList;
        }

        // Add a vertex to the mesh and initialize needed data structures.
        private int AddVertex(int4 vertex, ref UnsafeArray<int> vertexIndices) {
            int4 vertexBase = vertex / 2;
            int3 countsPlusOne = VoxelCounts + 1;
            int indexInIndicesArray =
                4 * (vertexBase.x * countsPlusOne.y * countsPlusOne.z + vertexBase.y * countsPlusOne.z + vertexBase.z);

            int3 vertexOffsets = new(vertex.x % 2, vertex.y % 2, vertex.z % 2);
            int subIndex;
            if (vertexOffsets.Equals(new int3(0, 0, 0))) {
                subIndex = 0;
            } else if (vertexOffsets.Equals(new int3(0, 1, 1))) {
                subIndex = 1;
            } else if (vertexOffsets.Equals(new int3(1, 0, 1))) {
                subIndex = 2;
            } else if (vertexOffsets.Equals(new int3(1, 1, 0))) {
                subIndex = 3;
            } else {
                Debug.LogError($"Invalid vertex pos: {vertex}.");
                return -1;
            }

            indexInIndicesArray += subIndex;

            int curValue = vertexIndices[indexInIndicesArray];
            if (curValue > 0) return curValue - 1;

            int vertexIndex = Mesh.Vertices.Length;

            Mesh.Vertices.Add(new float4(VolumeBounds.Min.xyz + ((float3) vertex.xyz * 0.5f * VoxelSize), 1));
            Mesh.VertexConnections.Add(new HybridIntList(Allocator.Persistent));
            Mesh.VertexRegionMembership.Add(default);

            vertexIndices[indexInIndicesArray] = vertexIndex + 1;

            return vertexIndex;
        }

        // Add a triangle to a region, set up connected vertices
        private void AddTriangle(ref UnsafeList<int> triList, int region,
                                 int vertex1Index, int vertex2Index, int vertex3Index) {
            int firstIndex = triList.Length;

            triList.Add(vertex1Index);
            triList.Add(vertex2Index);
            triList.Add(vertex3Index);

            AddVertexToRegion(vertex1Index, region);
            AddVertexToRegion(vertex2Index, region);
            AddVertexToRegion(vertex3Index, region);

            ConnectVertices(vertex1Index, vertex2Index);
            ConnectVertices(vertex2Index, vertex3Index);
            ConnectVertices(vertex3Index, vertex1Index);

            Triangle triangle = new(vertex1Index, vertex2Index, vertex3Index);
            Mesh.TriangleIndicesPerRegion.TryGetValue(triangle, out TriangleRegionIndices indicesForTriangle);
            indicesForTriangle.SetValue(region, firstIndex);
            Mesh.TriangleIndicesPerRegion[triangle] = indicesForTriangle;
        }

        // Register the given vertex as being used by the given region.
        private void AddVertexToRegion(int vertex, int region) {
            IntList2 membershipForVertex = Mesh.VertexRegionMembership[vertex];
            if (membershipForVertex.Contains(region)) return;

            membershipForVertex.Add(region);
            Mesh.VertexRegionMembership[vertex] = membershipForVertex;
        }

        // Mark the two vertices as connected if they are not already.
        private void ConnectVertices(int vertex1Index, int vertex2Index) {
            HybridIntList vertex1Connections = Mesh.VertexConnections[vertex1Index];
            HybridIntList vertex2Connections = Mesh.VertexConnections[vertex2Index];

            if (!vertex1Connections.Contains(vertex2Index)) {
                vertex1Connections.Add(vertex2Index);
                Mesh.VertexConnections[vertex1Index] = vertex1Connections;
            }

            if (!vertex2Connections.Contains(vertex1Index)) {
                vertex2Connections.Add(vertex1Index);
                Mesh.VertexConnections[vertex2Index] = vertex2Connections;
            }
        }
    }
}
