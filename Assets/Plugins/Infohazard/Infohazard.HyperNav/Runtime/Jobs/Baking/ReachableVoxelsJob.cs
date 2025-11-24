// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking {
    [BurstCompile]
    public struct ReachableVoxelsJob : IJob {
        public NativeQueue<int3> QueueToExplore;
        public Fast3DArray Voxels;

        private UnsafeHashSet<int3> _reachableVoxels;

        public void Execute() {
            int3 voxelCounts = new(Voxels.SizeX, Voxels.SizeY, Voxels.SizeZ);
            _reachableVoxels = new UnsafeHashSet<int3>(voxelCounts.x * voxelCounts.y * voxelCounts.z, Allocator.Temp);

            while (QueueToExplore.TryDequeue(out int3 pos)) {
                if (NativeMathUtility.IsOutOfBounds(voxelCounts, pos) ||
                    Voxels[pos.x, pos.y, pos.z] >= 0 ||
                    !_reachableVoxels.Add(pos)) {
                    continue;
                }

                for (int i = 0; i < 3; i++) {
                    for (int sign = -1; sign <= 1; sign += 2) {
                        int3 neighbor = pos;
                        neighbor[i] += sign;

                        QueueToExplore.Enqueue(neighbor);
                    }
                }
            }

            for (int x = 0; x < Voxels.SizeX; x++) {
                for (int y = 0; y < Voxels.SizeY; y++) {
                    for (int z = 0; z < Voxels.SizeZ; z++) {
                        int3 pos = new(x, y, z);

                        if (Voxels[x, y, z] < 0 && !_reachableVoxels.Contains(pos)) {
                            Voxels[x, y, z] = 0;
                        }
                    }
                }
            }
        }
    }
}
