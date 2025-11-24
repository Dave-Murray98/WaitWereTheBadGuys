// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct FilterIslandMinAreaJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<int> IslandIndices;

        [ReadOnly]
        public NativeArray<float4> Vertices;

        [NativeDisableParallelForRestriction]
        public NativeArray<int> TriangleIndices;

        [NativeDisableParallelForRestriction]
        public NativeArray<HybridIntList> VertexTriangles;

        [NativeDisableParallelForRestriction]
        public NativeArray<HybridIntList> VertexConnections;

        public float MinArea;

        public void Execute(int index) {
            float totalArea = 0;

            for (int i = 0; i < TriangleIndices.Length; i += 3) {
                int vertex1 = TriangleIndices[i + 0];
                int vertex2 = TriangleIndices[i + 1];
                int vertex3 = TriangleIndices[i + 2];

                if (vertex1 < 0 || IslandIndices[vertex1] != index) {
                    continue;
                }

                float4 pos1 = Vertices[vertex1];
                float4 pos2 = Vertices[vertex2];
                float4 pos3 = Vertices[vertex3];

                float3 edge1 = pos2.xyz - pos1.xyz;
                float3 edge2 = pos3.xyz - pos1.xyz;

                float3 cross = math.cross(edge1, edge2);
                float area = math.length(cross) * 0.5f;

                totalArea += area;
            }

            if (totalArea >= MinArea) return;

            for (int i = 0; i < Vertices.Length; i++) {
                if (IslandIndices[i] != index) {
                    continue;
                }

                HybridIntList triangles = VertexTriangles[i];
                for (int j = 0; j < triangles.Count; j++) {
                    int triStart = triangles[j];

                    TriangleIndices[triStart + 0] = -1;
                    TriangleIndices[triStart + 1] = -1;
                    TriangleIndices[triStart + 2] = -1;
                }

                triangles.Dispose();
                VertexTriangles[i] = default;

                VertexConnections[i].Dispose();
                VertexConnections[i] = default;
            }
        }
    }
}
