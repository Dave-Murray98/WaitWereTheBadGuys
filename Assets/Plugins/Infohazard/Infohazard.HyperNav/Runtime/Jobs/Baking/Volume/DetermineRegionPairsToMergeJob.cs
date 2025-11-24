// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct DetermineRegionPairsToMergeJob : IJob {
        [ReadOnly] public NativeArray<RegionMergeCandidateList> MergeCandidateLists;

        public int RegionCount;

        public NativeParallelHashMap<int, UnsafeParallelHashSet<int>> RegionsAdjacency;

        [WriteOnly] public NativeList<int2> RegionPairsToMerge;

        [WriteOnly] public NativeHashSet<int> RemovedRegions;

        public void Execute() {
            using UnsafeHashSet<int> usedRegions = new(RegionCount, Allocator.Temp);

            for (int regionId = 1; regionId < RegionCount; regionId++) {
                if (usedRegions.Contains(regionId) ||
                    !RegionsAdjacency.TryGetValue(regionId, out UnsafeParallelHashSet<int> adjacent)) continue;

                RegionMergeCandidateList candidateList = MergeCandidateLists[regionId - 1];
                for (int j = 0; j < candidateList.Count; j++) {
                    int otherRegionId = candidateList[j];
                    if (usedRegions.Contains(otherRegionId)) continue;

                    usedRegions.Add(regionId);
                    usedRegions.Add(otherRegionId);
                    RegionPairsToMerge.Add(new int2(regionId, otherRegionId));

                    // At this point we are going to merge the two regions, so we need to update the adjacency map.
                    UnsafeParallelHashSet<int> otherAdjacency = RegionsAdjacency[otherRegionId];
                    otherAdjacency.UnionWith(adjacent);
                    otherAdjacency.Remove(regionId);
                    otherAdjacency.Remove(otherRegionId);
                    RegionsAdjacency[otherRegionId] = otherAdjacency;

                    foreach (int otherOtherRegionId in adjacent) {
                        if (otherOtherRegionId == otherRegionId) continue;

                        UnsafeParallelHashSet<int> otherOtherAdjacency = RegionsAdjacency[otherOtherRegionId];
                        otherOtherAdjacency.Remove(regionId);
                        otherOtherAdjacency.Add(otherRegionId);
                        RegionsAdjacency[otherOtherRegionId] = otherOtherAdjacency;
                    }

                    adjacent.Dispose();
                    RegionsAdjacency.Remove(regionId);
                    RemovedRegions.Add(regionId);

                    break;
                }
            }
        }
    }
}
