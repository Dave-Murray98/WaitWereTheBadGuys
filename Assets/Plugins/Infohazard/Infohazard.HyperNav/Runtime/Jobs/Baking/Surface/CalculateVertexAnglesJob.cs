// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    /// <summary>
    /// Used to calculate the offset from each vertex to check for collisions.
    /// For a flat or convex vertex, the offset is zero; however, for a concave vertex,
    /// the collision check must be offset in order to avoid the collision check hitting the ground.
    /// </summary>
    [BurstCompile]
    public struct CalculateVertexAnglesJob : IJobParallelFor {
        [ReadOnly]
        public BakingNavSurfaceMeshInfo Mesh;

        public NativeArray<float4> VertexAngleOffsets;
        public NativeArray<float4> VertexSharedWorldNormals;

        public float CollisionRadius;
        
        public float4x4 Transform;
        
        public void Execute(int index) {
            float minDot = 1;
            
            HybridIntList triangles = Mesh.VertexTriangles[index];
            
            Span<float4> triCenters = stackalloc float4[triangles.Count];
            for (int i = 0; i < triangles.Count; i++) {
                int triStart = triangles[i];
                float4 triCenter = Mesh.Vertices[Mesh.TriangleList[triStart + 0]] +
                                  Mesh.Vertices[Mesh.TriangleList[triStart + 1]] +
                                  Mesh.Vertices[Mesh.TriangleList[triStart + 2]];
                triCenters[i] = triCenter;
            }

            float4 combinedNormal = float4.zero;
            for (int i = 0; i < triangles.Count; i++) {
                int triStart = triangles[i];
                float4 normal = Mesh.TriangleNormals[triStart / 3];
                combinedNormal += normal;
                float4 triCenter = triCenters[i];
                
                for (int j = i + 1; j < triangles.Count; j++) {
                    int triStart2 = triangles[j];
                    float4 normal2 = Mesh.TriangleNormals[triStart2 / 3];
                    float4 triCenter2 = triCenters[j];
                    
                    // Check if concave. Convex vertices do not need an offset.
                    float4 centerDiff = triCenter2 - triCenter;
                    bool isConcave = math.dot(centerDiff, normal) < 0;
                    if (!isConcave) continue;
                    
                    float dot = math.dot(normal.xyz, normal2.xyz);
                    minDot = math.min(minDot, dot);
                }
            }
            
            combinedNormal = math.normalize(combinedNormal);
            VertexSharedWorldNormals[index] = math.mul(Transform, combinedNormal);

            if (minDot is >= 1 or <= -1) {
                VertexAngleOffsets[index] = 0;
                return;
            }
            
            float angle = math.acos(minDot);
            float internalAngle = math.PI - angle;
            float halfInternalAngle = internalAngle * 0.5f;
            float oppositeTriAngle = math.PI * 0.5f - halfInternalAngle;
            float hypotenuse = 1.0f / math.cos(oppositeTriAngle);
            float offsetDist = CollisionRadius * (hypotenuse - 1);
            
            VertexAngleOffsets[index] = combinedNormal * offsetDist;
        }
    }
}