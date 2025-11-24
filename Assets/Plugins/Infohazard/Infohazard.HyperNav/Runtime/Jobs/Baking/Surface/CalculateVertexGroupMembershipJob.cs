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
    public struct CalculateVertexGroupMembershipJob : IJob {
        [ReadOnly]
        public BakingNavSurfaceMeshInfo Mesh;

        public NativeArray<HybridIntList> VertexRegionMembership;
        
        public void Execute() {
            for (int i = 0; i < Mesh.GroupTriangles.Length; i++) {
                HybridIntList triStarts = Mesh.GroupTriangles[i];
                for (int j = 0; j < triStarts.Count; j++) {
                    int triStart = triStarts[j];
                    for (int k = 0; k < 3; k++) {
                        int vertexIndex = Mesh.TriangleList[triStart + k];
                        if (vertexIndex < 0) continue;
                        
                        HybridIntList list = VertexRegionMembership[vertexIndex];
                        if (list.Allocator == default) {
                            list = new HybridIntList(Allocator.Persistent);
                        }

                        if (list.Contains(i)) continue;
                        list.Add(i);
                        VertexRegionMembership[vertexIndex] = list;
                    }
                }
            }
        }
    }
}