// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct UpdateNormalsJob : IJobParallelFor {
        [ReadOnly] public NativeList<float4> Vertices;
        [ReadOnly] public NativeList<int> TriangleIndices;

        [WriteOnly] public NativeArray<float4> Normals;
        
        public bool IgnoreZeroArea;

        public void Execute(int index) {
            int baseIndex = index * 3;
            int index1 = TriangleIndices[baseIndex + 0];
            int index2 = TriangleIndices[baseIndex + 1];
            int index3 = TriangleIndices[baseIndex + 2];

            if (index1 < 0 || index2 < 0 || index3 < 0) {
                return;
            }

            float4 vertex1 = Vertices[index1];
            float4 vertex2 = Vertices[index2];
            float4 vertex3 = Vertices[index3];

            float4 edge1 = vertex2 - vertex1;
            float4 edge2 = vertex3 - vertex1;

            float3 cross = math.cross(edge1.xyz, edge2.xyz);
            if (math.length(cross) < 0.0000001f) {
                if (!IgnoreZeroArea) {
                    Debug.LogError($"Triangle at index {index} has zero area.");
                    Debug.LogError($"Vertices: {vertex1.x}, {vertex1.y}, {vertex1.z} | {vertex2.x}, {vertex2.y}, {vertex2.z} | {vertex3.x}, {vertex3.y}, {vertex3.z}");
                }
                
                Normals[index] = float4.zero;
            } else {
                float3 normal = math.normalize(cross);
                Normals[index] = new float4(normal, 0);
            }
        }
    }
}
