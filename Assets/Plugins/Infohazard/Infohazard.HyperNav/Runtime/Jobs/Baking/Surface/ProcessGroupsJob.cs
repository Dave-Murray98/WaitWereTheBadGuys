// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct ProcessGroupsJob : IJob {
        [ReadOnly]
        public NativeArray<UnsafeList<HybridIntList>> InputGroupTrianglesPerIsland;

        public NativeList<HybridIntList> OutputGroupTriangles;

        public NativeArray<int> OutputGroupIDs;
        
        public void Execute() {
            int groupCount = 0;
            for (int i = 0; i < InputGroupTrianglesPerIsland.Length; i++) {
                UnsafeList<HybridIntList> islandGroups = InputGroupTrianglesPerIsland[i];
                
                for (int j = 0; j < islandGroups.Length; j++) {
                    HybridIntList group = islandGroups[j];
                    OutputGroupTriangles.Add(group);
                    for (int k = 0; k < group.Count; k++) {
                        OutputGroupIDs[group[k] / 3] = groupCount;
                    }
                    groupCount++;
                }
            }
        }
    }
}