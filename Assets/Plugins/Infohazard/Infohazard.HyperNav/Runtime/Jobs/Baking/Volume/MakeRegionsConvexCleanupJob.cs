// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct MakeRegionsConvexCleanupJob : IJob {
        [ReadOnly] public NativeArray<int> CreatedRegionCounts;
        public int OldRegionCount;

        public Fast3DArray Regions;

        [WriteOnly] public NativeList<int> RegionQueue;

        public void Execute() {
            RegionQueue.Clear();

            // Get total count of new regions.
            int totalCreatedRegionCount = 0;
            for (int i = 0; i < CreatedRegionCounts.Length; i++) {
                totalCreatedRegionCount += CreatedRegionCounts[i];
            }

            if (totalCreatedRegionCount == 0) return;

            // Build map of temporary ID to real region ID.
            int nextRegionIndex = OldRegionCount;
            UnsafeHashMap<int, int> newRegionIdMap = new(totalCreatedRegionCount, Allocator.Temp);
            for (int i = 0; i < CreatedRegionCounts.Length; i++) {
                int newCountForExecutionIndex = CreatedRegionCounts[i];
                int startIndex = MakeRegionsConvexJob.GetNewRegionStartIndex(i);
                for (int j = 0; j < newCountForExecutionIndex; j++) {
                    int createdIndex = startIndex + j;
                    int newRegionIndex = nextRegionIndex++;

                    newRegionIdMap[createdIndex] = newRegionIndex;
                }
            }

            // Update all new regions to use real region IDs.
            for (int x = 0; x < Regions.SizeX; x++) {
                for (int y = 0; y < Regions.SizeY; y++) {
                    for (int z = 0; z < Regions.SizeZ; z++) {
                        int regionIndex = Regions[x, y, z];
                        if (regionIndex < OldRegionCount) continue;

                        if (newRegionIdMap.TryGetValue(regionIndex, out int newRegionIndex)) {
                            Regions[x, y, z] = newRegionIndex;
                        }
                    }
                }
            }

            // Update region queue, indicating new regions need to be processed.
            for (int i = OldRegionCount; i < nextRegionIndex; i++) {
                RegionQueue.Add(i);
            }

            newRegionIdMap.Dispose();
        }
    }
}
