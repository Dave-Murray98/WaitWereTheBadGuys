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

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct MapRegionsJob : IJob {
        public Fast3DArray Regions;
        public int OldRegionCount;
        public NativeHashSet<int> RemovedRegions;

        private UnsafeHashMap<int, int> _regionMap;

        public void Execute() {
            _regionMap = new UnsafeHashMap<int, int>(OldRegionCount - RemovedRegions.Count, Allocator.Temp);

            int newRegionCount = 0;
            for (int i = 0; i < OldRegionCount; i++) {
                if (RemovedRegions.Contains(i)) continue;
                _regionMap[i] = newRegionCount++;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (newRegionCount != _regionMap.Count) {
                throw new Exception("Region map count does not match new region count!");
            }
#endif

            int3 voxelCounts = new(Regions.SizeX, Regions.SizeY, Regions.SizeZ);
            for (int x = 0; x < voxelCounts.x; x++) {
                for (int y = 0; y < voxelCounts.y; y++) {
                    for (int z = 0; z < voxelCounts.z; z++) {
                        int region = Regions[x, y, z];
                        if (region < 0) continue;

                        if (!_regionMap.TryGetValue(region, out int newRegion)) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            Debug.LogError($"Region {region} found in grid but not region map!");
#endif
                            continue;
                        }

                        Regions[x, y, z] = newRegion;
                    }
                }
            }
        }
    }
}
