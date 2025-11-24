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
    public struct GetRegionAdjacencyMapJob : IJob {
        public Fast3DArray Regions;
        public int RegionCount;
        public NativeParallelHashMap<int, UnsafeParallelHashSet<int>> RegionAdjacencyMap;

        public void Execute() {
            int3 voxelCounts = new(Regions.SizeX, Regions.SizeY, Regions.SizeZ);

            for (int x = 0; x < voxelCounts.x; x++) {
                for (int y = 0; y < voxelCounts.y; y++) {
                    for (int z = 0; z < voxelCounts.z; z++) {
                        int curRegion = Regions[x, y, z];
                        if (curRegion <= 0) continue;

                        if (!RegionAdjacencyMap.TryGetValue(curRegion, out UnsafeParallelHashSet<int> curAdjacent)) {
                            curAdjacent = new UnsafeParallelHashSet<int>(RegionCount, Allocator.Persistent);
                        }

                        int3 pos = new(x, y, z);
                        for (int i = 0; i < 3; i++) {
                            for (int sign = -1; sign <= 1; sign += 2) {
                                int3 n = pos;
                                n[i] += sign;
                                if (NativeMathUtility.IsOutOfBounds(voxelCounts, n)) continue;
                                int nRegion = Regions[n.x, n.y, n.z];
                                if (nRegion != curRegion && nRegion > 0) {
                                    curAdjacent.Add(nRegion);
                                }
                            }
                        }

                        RegionAdjacencyMap[curRegion] = curAdjacent;
                    }
                }
            }
        }
    }
}
