// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct SurfaceTriangleMeshToIndicesJob : IJob {
        [ReadOnly] public NativeList<int4> BlockedAndWalkableVertices;
        [ReadOnly] public NativeList<int4> WalkableVertices;
        [ReadOnly] public NativeList<float4> InNormals;
        public int3 VoxelCounts;
        public BakingNavSurfaceMeshInfo Mesh;
        public float VoxelSize;
        public NativeBounds VolumeBounds;

        public void Execute() {
            if (WalkableVertices.Length == 0) return;

            int mapSize = (VoxelCounts.x + 1) * (VoxelCounts.y + 1) * (VoxelCounts.z + 1) * 4;

            UnsafeArray<bool2> verticesPresentInBlockedAndWalkable = new(mapSize, Allocator.Temp, true);
            UnsafeArray<int2> vertexIndices = new(mapSize, Allocator.Temp, true);

            try {
                for (int i = 0; i < BlockedAndWalkableVertices.Length; i++) {
                    int4 vertex = BlockedAndWalkableVertices[i];
                    if (!TryGetIndexInMap(vertex, out int indexInIndicesArray)) {
                        continue;
                    }

                    bool2 curValues = verticesPresentInBlockedAndWalkable[indexInIndicesArray];

                    if (vertex.w > 0) {
                        curValues.y = true;
                    } else {
                        curValues.x = true;
                    }

                    verticesPresentInBlockedAndWalkable[indexInIndicesArray] = curValues;
                }

                for (int triStart = 0; triStart < WalkableVertices.Length - 2; triStart += 3) {
                    int4 vertex1 = WalkableVertices[triStart + 0];
                    int4 vertex2 = WalkableVertices[triStart + 1];
                    int4 vertex3 = WalkableVertices[triStart + 2];

                    float4 normal = InNormals[triStart / 3];

                    if (!TryGetIndexInMap(vertex1, out int indexInIndicesArray1) ||
                        !TryGetIndexInMap(vertex2, out int indexInIndicesArray2) ||
                        !TryGetIndexInMap(vertex3, out int indexInIndicesArray3) ||
                        !ExistsInBlockedAndWalkable(vertex1, indexInIndicesArray1,
                                                    verticesPresentInBlockedAndWalkable) ||
                        !ExistsInBlockedAndWalkable(vertex2, indexInIndicesArray2,
                                                    verticesPresentInBlockedAndWalkable) ||
                        !ExistsInBlockedAndWalkable(vertex3, indexInIndicesArray3,
                                                    verticesPresentInBlockedAndWalkable)) {
                        continue;
                    }

                    // Add vertices to the mesh.
                    int vertex1Index = AddVertex(vertex1, indexInIndicesArray1, ref vertexIndices);
                    int vertex2Index = AddVertex(vertex2, indexInIndicesArray2, ref vertexIndices);
                    int vertex3Index = AddVertex(vertex3, indexInIndicesArray3, ref vertexIndices);

                    if (vertex1Index == vertex2Index || vertex1Index == vertex3Index || vertex2Index == vertex3Index) {
                        Debug.LogError(
                            $"Two of the vertices resolved to the same index: ({vertex1.x}, {vertex1.y}, {vertex1.z}), ({vertex2.x}, {vertex2.y}, {vertex2.z}), ({vertex3.x}, {vertex3.y}, {vertex3.z}).");
                    }

                    // Create triangle.
                    AddTriangle(vertex1Index, vertex2Index, vertex3Index, normal);
                }
            } finally {
                verticesPresentInBlockedAndWalkable.Dispose();
                vertexIndices.Dispose();
            }
        }

        private bool ExistsInBlockedAndWalkable(int4 vertex, int indexInIndicesArray, in UnsafeArray<bool2> map) {
            bool2 curValues = map[indexInIndicesArray];
            return vertex.w > 0 ? curValues.y : curValues.x;
        }

        // Add a vertex to the mesh and initialize needed data structures.
        private int AddVertex(int4 vertex, int indexInIndicesArray, ref UnsafeArray<int2> vertexIndices) {
            int2 curValues = vertexIndices[indexInIndicesArray];

            ref int curValue = ref vertex.w > 0 ? ref curValues.y : ref curValues.x;

            if (curValue > 0) return curValue - 1;

            int vertexIndex = Mesh.Vertices.Length;

            Mesh.Vertices.Add(new float4(VolumeBounds.Min.xyz + ((float3) vertex.xyz * 0.5f * VoxelSize), 1));
            Mesh.VertexConnections.Add(new HybridIntList(Allocator.Persistent));
            Mesh.VertexTriangles.Add(new HybridIntList(Allocator.Persistent));

            curValue = vertexIndex + 1;
            vertexIndices[indexInIndicesArray] = curValues;

            return vertexIndex;
        }

        private bool TryGetIndexInMap(int4 vertex, out int indexInIndicesArray) {
            int4 vertexBase = vertex / 2;
            int3 countsPlusOne = VoxelCounts + 1;
            indexInIndicesArray = 4 * (vertexBase.x * countsPlusOne.y * countsPlusOne.z +
                                       vertexBase.y * countsPlusOne.z + vertexBase.z);

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
                Debug.LogError($"Invalid vertex pos: {vertex.xyz}.");
                return false;
            }

            indexInIndicesArray += subIndex;
            return true;
        }

        // Add a triangle to a region, set up connected vertices
        private void AddTriangle(int vertex1Index, int vertex2Index, int vertex3Index, float4 normal) {
            int firstIndex = Mesh.TriangleList.Length;

            Mesh.TriangleList.Add(vertex1Index);
            Mesh.TriangleList.Add(vertex2Index);
            Mesh.TriangleList.Add(vertex3Index);

            Mesh.TriangleNormals.Add(normal);

            ConnectVertices(vertex1Index, vertex2Index);
            ConnectVertices(vertex2Index, vertex3Index);
            ConnectVertices(vertex3Index, vertex1Index);

            AddTriangleToVertex(vertex1Index, firstIndex);
            AddTriangleToVertex(vertex2Index, firstIndex);
            AddTriangleToVertex(vertex3Index, firstIndex);
        }

        private void AddTriangleToVertex(int vertexIndex, int triangleIndex) {
            HybridIntList vertexTriangles = Mesh.VertexTriangles[vertexIndex];
            vertexTriangles.Add(triangleIndex);
            Mesh.VertexTriangles[vertexIndex] = vertexTriangles;
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
