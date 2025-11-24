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
    public unsafe struct CalculateRegionsJob : IJob {
        public Fast3DArray Voxels;
        public NativeReference<int> RegionCount;

        [NativeDisableUnsafePtrRestriction]
        private int3* _queueBuffer;
        private int _queueSize;

        public void Execute() {
            _queueSize = Voxels.SizeX * Voxels.SizeY * 2 +
                         Voxels.SizeX * Voxels.SizeZ * 2 +
                         Voxels.SizeY * Voxels.SizeZ * 2;

            _queueBuffer = (int3*) UnsafeUtility.MallocTracked(_queueSize * sizeof(int3), UnsafeUtility.AlignOf<int3>(),
                                                               Allocator.Temp, 0);

            int3 voxelCounts = new(Voxels.SizeX, Voxels.SizeY, Voxels.SizeZ);

            RegionCount.Value = 1;

            for (int x = 0; x < voxelCounts.x; x++) {
                for (int y = 0; y < voxelCounts.y; y++) {
                    for (int z = 0; z < voxelCounts.z; z++) {
                        if (Voxels[x, y, z] >= 0) continue; // Region is already set

                        // Create a new region and flood fill all contiguous voxels.
                        Voxels[x, y, z] = RegionCount.Value++;
                        ExpandVoxelRegion(voxelCounts, new int3(x, y, z));
                    }
                }
            }
        }

        // Flood fill all contiguous voxels.
        private void ExpandVoxelRegion(int3 voxelCounts, int3 startPos) {
            UnsafeRingQueue<int3> queue = new(_queueBuffer, _queueSize);
            queue.Enqueue(startPos);

            ExpandVoxelRegions(voxelCounts, queue);
        }

        // Flood fill all contiguous voxels.
        private void ExpandVoxelRegions(int3 voxelCounts, UnsafeRingQueue<int3> queue) {
            while (queue.TryDequeue(out int3 current)) {
                int region = Voxels[current.x, current.y, current.z];
                for (int i = 0; i < 3; i++) {
                    for (int sign = -1; sign <= 1; sign += 2) {
                        int3 neighbor = current;
                        neighbor[i] += sign;

                        if (NativeMathUtility.IsOutOfBounds(voxelCounts, neighbor)) continue;
                        if (Voxels[neighbor.x, neighbor.y, neighbor.z] >= 0) continue;

                        Voxels[neighbor.x, neighbor.y, neighbor.z] = region;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }
    }
}
