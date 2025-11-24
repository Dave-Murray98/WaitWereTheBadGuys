// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct CalculateSplitVertexGroupsJob : IJobParallelFor {
        public float MinDotThreshold;

        [ReadOnly]
        public BakingNavSurfaceMeshInfo Mesh;

        public NativeArray<HybridIntList> GroupsForEachVertex;
        public NativeArray<HybridIntList> GroupCountsForEachVertex;

        public void Execute(int index) {
            HybridIntList groups = new(Allocator.Persistent);
            HybridIntList groupCounts = new(Allocator.Persistent);
            
            HybridIntList tris = Mesh.VertexTriangles[index];
            HybridIntList trisCopy = new(Allocator.Temp);
            trisCopy.CopyFrom(tris);

            while (trisCopy.Count > 0) {
                int curTri = trisCopy[0];
                float4 curUp = Mesh.TriangleUprightWorldDirections[curTri / 3];
                groups.Add(curTri);
                groupCounts.Add(1);

                for (int i = 1; i < trisCopy.Count; i++) {
                    int otherTri = trisCopy[i];
                    float4 otherUp = Mesh.TriangleUprightWorldDirections[otherTri / 3];

                    if (math.dot(curUp, otherUp) < MinDotThreshold) continue;
                    
                    groups.Add(otherTri);
                    groupCounts[^1]++;
                    trisCopy.RemoveAtSwapBack(i);
                    i--;
                }
                
                trisCopy.RemoveAtSwapBack(0);
            }
            
            GroupsForEachVertex[index] = groups;
            GroupCountsForEachVertex[index] = groupCounts;
        }
    }
}
