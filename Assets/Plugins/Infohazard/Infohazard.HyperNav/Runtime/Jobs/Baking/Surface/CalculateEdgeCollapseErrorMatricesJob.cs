// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct CalculateEdgeCollapseErrorMatricesJob : IJobParallelFor {
        [ReadOnly]
        public BakingNavSurfaceMeshInfo Mesh;
        
        [ReadOnly]
        public NativeArray<int> IslandIndices;
        
        [ReadOnly]
        public NativeArray<UnsafeHashMap<Edge, int2>> EdgeOccurrenceMaps;

        public NativeArray<float4x4> ErrorMatrices;
        public NativeArray<HybridIntList> VerticesOnEdge;

        public void Execute(int index) {
            float4x4 errorMatrix = CalculateErrorMatrix(Mesh, index);

            ErrorMatrices[index] = errorMatrix;
            VerticesOnEdge[index] = IsVertexOnEdge(index);
        }

        public static float4x4 CalculateErrorMatrix(in BakingNavSurfaceMeshInfo mesh, int index) {
            float4x4 errorMatrix = float4x4.zero;

            HybridIntList triangles = mesh.VertexTriangles[index];

            for (int i = 0; i < triangles.Count; i++) {
                int triangleStart = triangles[i];

                int index1 = mesh.TriangleList[triangleStart + 0];
                int index2 = mesh.TriangleList[triangleStart + 1];
                int index3 = mesh.TriangleList[triangleStart + 2];

                if (index1 == -1 || index2 == -1 || index3 == -1) {
                    continue;
                }

                float4 normal = mesh.TriangleNormals[triangleStart / 3];

                float4 p1 = mesh.Vertices[index1];
                float4 p2 = mesh.Vertices[index2];
                float4 p3 = mesh.Vertices[index3];

                float4x4 triangleMatrix = CalculateTriangleErrorMatrix(p1, p2, p3, normal);

                errorMatrix += triangleMatrix;
            }

            return errorMatrix;
        }

        public static float4x4 CalculateTriangleErrorMatrix(float4 p1, float4 p2, float4 p3, float4 normal) {
            float a = normal.x;
            float b = normal.y;
            float c = normal.z;
            float d = -math.dot(normal, p1);

            return new float4x4(
                a * a, a * b, a * c, a * d,
                a * b, b * b, b * c, b * d,
                a * c, b * c, c * c, c * d,
                a * d, b * d, c * d, d * d
            );
        }
        
        private HybridIntList IsVertexOnEdge(int vertex) {
            HybridIntList connections = Mesh.VertexConnections[vertex];
            HybridIntList result = new(Allocator.Persistent);
            
            int islandIndex = IslandIndices[vertex];
            if (islandIndex < 0) {
                return result;
            }
            
            UnsafeHashMap<Edge, int2> edgeOccurrenceMap = EdgeOccurrenceMaps[islandIndex];

            for (int i = 0; i < connections.Count; i++) {
                int connectedVertex = connections[i];

                Edge edge = new(vertex, connectedVertex);
                if (!edgeOccurrenceMap.TryGetValue(edge, out int2 triIndices)) {
                    Debug.LogError($"Edge ({vertex}, {connectedVertex}) not found in edgeOccurrenceMap.");
                    continue;
                }

                if ((triIndices.x >= 0) != (triIndices.y >= 0)) {
                    result.Add(connectedVertex);
                }
            }
            
            if (result.Count % 2 != 0) {
                Debug.LogError($"Vertex {vertex} has {result.Count} bound edges.");
            }

            return result;
        }
    }
}
