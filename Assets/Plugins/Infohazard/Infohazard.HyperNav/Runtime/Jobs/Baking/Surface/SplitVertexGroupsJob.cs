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
    public struct SplitVertexGroupsJob : IJob {
        public NativeArray<HybridIntList> GroupsForEachVertex;
        public NativeArray<HybridIntList> GroupCountsForEachVertex;

        public NativeArray<int> TriangleIndices;
        
        public NativeList<float4> Vertices;
        public NativeList<HybridIntList> VertexTriangles;
        public NativeList<HybridIntList> VertexConnections;
        
        public void Execute() {
            int originalVertexCount = Vertices.Length;
            for (int i = 0; i < originalVertexCount; i++) {
                HybridIntList groups = GroupsForEachVertex[i];
                HybridIntList groupCounts = GroupCountsForEachVertex[i];
                
                if (groupCounts.Count < 2) continue;

                int curIndexInGroups = groupCounts[0];
                for (int j = 1; j < groupCounts.Count; j++) {
                    int newVertexIndex = Vertices.Length;
                    Vertices.Add(Vertices[i]);
                    VertexTriangles.Add(new HybridIntList(Allocator.Persistent));
                    VertexConnections.Add(new HybridIntList(Allocator.Persistent));

                    for (int k = 0; k < groupCounts[j]; k++) {
                        int tri = groups[curIndexInGroups++];

                        if (TriangleIndices[tri + 0] == i) {
                            TriangleIndices[tri + 0] = newVertexIndex;
                        } else if (TriangleIndices[tri + 1] == i) {
                            TriangleIndices[tri + 1] = newVertexIndex;
                        } else if (TriangleIndices[tri + 2] == i) {
                            TriangleIndices[tri + 2] = newVertexIndex;
                        }
                    }
                }
            }
        }
    }
}