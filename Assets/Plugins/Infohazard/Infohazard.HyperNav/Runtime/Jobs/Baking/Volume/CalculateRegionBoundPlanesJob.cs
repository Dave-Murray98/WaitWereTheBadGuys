// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct CalculateRegionBoundPlanesJob : IJobParallelFor {
        [ReadOnly]
        public BakingNavVolumeMeshInfo Mesh;

        public NativeArray<UnsafeList<NativePlane>> OutputData;

        public void Execute(int index) {
            int oldRegionID = index + 1;

            UnsafeList<int> triangleList = Mesh.RegionTriangleLists[oldRegionID];

            UnsafeList<NativePlane> boundPlanes = new(27, Allocator.Persistent);

            bool hasBounds = false;
            NativeBounds bounds = default;
            for (int i = 0; i < triangleList.Length; i++) {
                int v = triangleList[i];
                if (v < 0) continue;

                if (!hasBounds) {
                    hasBounds = true;
                    bounds = new NativeBounds(Mesh.Vertices[v], float4.zero);
                } else {
                    bounds.Encapsulate(Mesh.Vertices[v]);
                }
            }

            Span<bool> createdPlaneDirections = stackalloc bool[27];

            for (int i = 0; i < triangleList.Length; i += 3) {
                int v0 = triangleList[i + 0];
                int v1 = triangleList[i + 1];
                int v2 = triangleList[i + 2];

                if (v0 < 0 || v1 < 0 || v2 < 0) continue;

                float3 v0Pos = Mesh.Vertices[v0].xyz;
                float3 v1Pos = Mesh.Vertices[v1].xyz;
                float3 v2Pos = Mesh.Vertices[v2].xyz;

                float3 normal = math.normalize(math.cross(v1Pos - v0Pos, v2Pos - v0Pos));

                // Ensure the normal faces away from the center of the region.
                float3 toV0 = v0Pos - bounds.Center.xyz;
                if (math.dot(normal, toV0) < 0) {
                    normal = -normal;
                }

                int directionIndex = NativeMathUtility.GetDirectionIndex(normal);
                if (createdPlaneDirections[directionIndex]) continue;
                createdPlaneDirections[directionIndex] = true;

                NativePlane plane = new(normal, v0Pos);
                boundPlanes.Add(plane);
            }

            OutputData[index] = boundPlanes;
        }
    }
}
