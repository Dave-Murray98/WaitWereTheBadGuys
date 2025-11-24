// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct CombineRegionsJob : IJobParallelFor {
        public Fast3DArray Regions;

        [ReadOnly]
        public NativeList<int2> RegionsToMerge;

        public void Execute(int index) {
            int2 pair = RegionsToMerge[index];
            int regionA = pair.x;
            int regionB = pair.y;

            for (int x = 0; x < Regions.SizeX; x++) {
                for (int y = 0; y < Regions.SizeY; y++) {
                    for (int z = 0; z < Regions.SizeZ; z++) {
                        if (Regions[x, y, z] == regionA) {
                            Regions[x, y, z] = regionB;
                        }
                    }
                }
            }
        }
    }
}
