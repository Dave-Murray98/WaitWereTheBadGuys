// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct MakeRegionsConvexJob : IJobParallelFor {
        [ReadOnly] public NativeList<int> RegionQueue;
        public Fast3DArray Regions;
        [WriteOnly] public NativeArray<int> CreatedRegionCounts;
        public float4x4 VolumeTransform;
        public NativeBounds VolumeBounds;
        public NativeCancellationToken CancellationToken;

        public static int GetNewRegionStartIndex(int index) {
            return (index + 1) * 10_000;
        }

        public void Execute(int index) {
            int regionIndex = RegionQueue[index];
            int startRegionIndex = GetNewRegionStartIndex(index);
            int newRegionIndex = startRegionIndex;

            int totalVoxels = Regions.SizeX * Regions.SizeY * Regions.SizeZ;
            UnsafeArray<int3> queueBuffer = new(totalVoxels, Allocator.Temp);
            UnsafeHashSet<int3> contiguousWithCurrent = new(totalVoxels, Allocator.Temp);
            UnsafeHashSet<int3> contiguousWithNew = new(totalVoxels, Allocator.Temp);

            try {
                MakeRegionConvex(regionIndex, ref contiguousWithCurrent, ref contiguousWithNew, ref queueBuffer,
                                 ref newRegionIndex);
            } finally {
                CreatedRegionCounts[index] = newRegionIndex - startRegionIndex;
                contiguousWithCurrent.Dispose();
                contiguousWithNew.Dispose();
                queueBuffer.Dispose();
            }
        }

        private void MakeRegionConvex(int regionId, ref UnsafeHashSet<int3> contiguousWithCurrent,
                                      ref UnsafeHashSet<int3> contiguousWithNew,
                                      ref UnsafeArray<int3> queueBuffer, ref int newRegionIndex) {
            int3 voxelCounts = new(Regions.SizeX, Regions.SizeY, Regions.SizeZ);

            // Look for cubes with internal concavities and split at those points.
            for (int x = 0; x < voxelCounts.x - 1; x++) {
                for (int y = 0; y < voxelCounts.y - 1; y++) {
                    for (int z = 0; z < voxelCounts.z - 1; z++) {
                        int3 pos = new(x, y, z);
                        byte cube = MarchingCubes.GetMarchingCubesIndex(Regions, regionId, x, y, z);
                        if (MarchingCubesCavityTables.CubesWithInternalCavityTable[cube]) {
                            SplitRegionForInternalConcavity(regionId, ref contiguousWithCurrent, ref contiguousWithNew,
                                                            ref queueBuffer, ref newRegionIndex, pos);
                        }
                    }
                }
                
                if (CancellationToken.IsCancellationRequested) return;
            }

            // Look for cubes with neighbor concavities and split between them.
            for (int dir = 0; dir < 3; dir++) {
                int3 dirVector = MarchingCubesTables.GetPositiveDirection(dir);
                int dx = dirVector.x;
                int dy = dirVector.y;
                int dz = dirVector.z;

                for (int x = 0; x < voxelCounts.x - 1; x++) {
                    for (int y = 0; y < voxelCounts.y - 1; y++) {
                        for (int z = 0; z < voxelCounts.z - 1; z++) {
                            int3 selfPos = new(x, y, z);

                            byte selfCube = MarchingCubes.GetMarchingCubesIndex(Regions, regionId, x, y, z);
                            if (selfCube is 0 or 255) continue;

                            ref readonly CubeConcaveNeighborsTableDirectionEntry concaveNeighbors =
                                ref MarchingCubesCavityTables.CubeConcaveNeighborsTable.GetRef(selfCube).GetRef(dir);

                            if (concaveNeighbors.Count == 0) continue;

                            int nx = x + dx;
                            int ny = y + dy;
                            int nz = z + dz;

                            if (NativeMathUtility.IsOutOfBounds(voxelCounts, new int3(nx + 1, ny + 1, nz + 1))) {
                                continue;
                            }

                            byte neighborCube = MarchingCubes.GetMarchingCubesIndex(Regions, regionId, nx, ny, nz);
                            if (concaveNeighbors.Contains(neighborCube)) {
                                SplitRegionForNeighborConcavity(regionId, ref contiguousWithCurrent,
                                                                ref contiguousWithNew, ref queueBuffer,
                                                                ref newRegionIndex, selfPos, dir);
                            }
                        }
                    }
                
                    if (CancellationToken.IsCancellationRequested) return;
                }
            }
        }

        // Split a region between two neighbor marching cubes.
        private void SplitRegionForNeighborConcavity(int regionId,
                                                     ref UnsafeHashSet<int3> contiguousWithCurrent,
                                                     ref UnsafeHashSet<int3> contiguousWithNew,
                                                     ref UnsafeArray<int3> queueBuffer,
                                                     ref int newRegionIndex,
                                                     int3 pos, int dirIndex) {
            int3 voxelCounts = new(Regions.SizeX, Regions.SizeY, Regions.SizeZ);

            int3 zAxis = MarchingCubesTables.GetPositiveDirection(dirIndex);
            int3 xAxis = MarchingCubesTables.GetPositiveDirection((dirIndex + 1) % 3);
            int3 yAxis = MarchingCubesTables.GetPositiveDirection((dirIndex + 2) % 3);

            int vz = math.dot(pos, zAxis);

            int voxelsVx = math.dot(voxelCounts, xAxis);
            int voxelsVy = math.dot(voxelCounts, yAxis);
            int voxelsVz = math.dot(voxelCounts, zAxis);
            int3 virtualVoxelCounts = new(voxelsVx, voxelsVy, voxelsVz);

            // Two marching cubes means there are three voxels,
            // meaning two possible split locations.
            // Determine which one breaks fewer convex cubes and use that.
            int split1 = GetBrokenCubeCount(regionId, vz + 1, xAxis, yAxis, zAxis, new int2(voxelsVx, voxelsVy));
            int split2 = GetBrokenCubeCount(regionId, vz + 2, xAxis, yAxis, zAxis, new int2(voxelsVx, voxelsVy));

            if (split1 <= split2) {
                SplitRegion(regionId, ref contiguousWithCurrent, ref contiguousWithNew, ref queueBuffer,
                            ref newRegionIndex, xAxis, yAxis, zAxis, virtualVoxelCounts, pos + new int3(1, 1, 1));
            } else {
                SplitRegion(regionId, ref contiguousWithCurrent, ref contiguousWithNew, ref queueBuffer,
                            ref newRegionIndex, xAxis, yAxis, zAxis, virtualVoxelCounts,
                            pos + new int3(1, 1, 1) + zAxis);
            }
        }

        // Split a region to break up a single concave cube.
        private void SplitRegionForInternalConcavity(int regionId,
                                                     ref UnsafeHashSet<int3> contiguousWithCurrent,
                                                     ref UnsafeHashSet<int3> contiguousWithNew,
                                                     ref UnsafeArray<int3> queueBuffer,
                                                     ref int newRegionIndex,
                                                     int3 pos) {
            int3 voxelCounts = new(Regions.SizeX, Regions.SizeY, Regions.SizeZ);
            byte cube = MarchingCubes.GetMarchingCubesIndex(Regions, regionId, pos.x, pos.y, pos.z);

            int3 forward = new(0, 0, 1);
            int3 up = new(0, 1, 0);
            int3 right = new(1, 0, 0);

            // Find best split axis - must actually divide the cube.
            int xSplit = IsCubeBrokenOnAxis(cube, 0)
                ? GetBrokenCubeCount(regionId, pos.x + 1, forward, up, right, new int2(voxelCounts.z, voxelCounts.y))
                : int.MaxValue;

            int ySplit = IsCubeBrokenOnAxis(cube, 1)
                ? GetBrokenCubeCount(regionId, pos.y + 1, right, forward, up, new int2(voxelCounts.x, voxelCounts.z))
                : int.MaxValue;

            int zSplit = IsCubeBrokenOnAxis(cube, 2)
                ? GetBrokenCubeCount(regionId, pos.z + 1, right, up, forward, new int2(voxelCounts.x, voxelCounts.y))
                : int.MaxValue;

            // Split on best axis.
            if (xSplit <= ySplit && xSplit <= zSplit) {
                SplitRegion(regionId, ref contiguousWithCurrent, ref contiguousWithNew, ref queueBuffer,
                            ref newRegionIndex, forward, up, right,
                            new int3(voxelCounts.z, voxelCounts.y, voxelCounts.x), pos + new int3(1, 1, 1));
            } else if (ySplit <= xSplit && ySplit <= zSplit) {
                SplitRegion(regionId, ref contiguousWithCurrent, ref contiguousWithNew, ref queueBuffer,
                            ref newRegionIndex, right, forward, up,
                            new int3(voxelCounts.x, voxelCounts.z, voxelCounts.y), pos + new int3(1, 1, 1));
            } else {
                SplitRegion(regionId, ref contiguousWithCurrent, ref contiguousWithNew, ref queueBuffer,
                            ref newRegionIndex, right, up, forward,
                            new int3(voxelCounts.x, voxelCounts.y, voxelCounts.z), pos + new int3(1, 1, 1));
            }
        }

        // Split a region into two at the given point along the given axis.
        // Voxels further along the split axis than the split point will go into the new region.
        private void SplitRegion(int regionId, ref UnsafeHashSet<int3> contiguousWithCurrent,
                                 ref UnsafeHashSet<int3> contiguousWithNew,
                                 ref UnsafeArray<int3> queueBuffer,
                                 ref int newRegionIndex,
                                 int3 xAxis, int3 yAxis, int3 zAxis,
                                 int3 axisLimits, int3 startPos) {
            // Get initial contiguous regions staying on one side of the split axis.
            int3 startMinus1 = startPos - new int3(1, 1, 1);

            contiguousWithNew.Clear();
            contiguousWithCurrent.Clear();

            GetContiguousIfSplit(regionId, ref contiguousWithCurrent, ref queueBuffer, startMinus1, xAxis, yAxis,
                                 zAxis);
            GetContiguousIfSplit(regionId, ref contiguousWithNew, ref queueBuffer, startMinus1 + zAxis, xAxis, yAxis,
                                 -zAxis);

            // Voxels on side B of the axis but not contiguous to the current region B are added to region A.
            ExpandContiguousExcluding(regionId, ref contiguousWithCurrent, ref contiguousWithNew, ref queueBuffer);

            // Voxels on side A of the axis but not contiguous to the current region A are added to region B.
            ExpandContiguousExcluding(regionId, ref contiguousWithNew, ref contiguousWithCurrent, ref queueBuffer);

            int newRegion = newRegionIndex++;

            // Loop through all voxels and assign new region if should split.
            for (int vx = 0; vx < axisLimits.x; vx++) {
                for (int vy = 0; vy < axisLimits.y; vy++) {
                    for (int vz = 0; vz < axisLimits.z; vz++) {
                        int3 pos = xAxis * vx + yAxis * vy + zAxis * vz;
                        if (Regions[pos.x, pos.y, pos.z] != regionId) continue;

                        // Only split a voxel into the new region if it is part of contiguousWithNew.
                        bool contiguousNew = contiguousWithNew.Contains(pos);
                        if (contiguousNew) {
                            Regions[pos.x, pos.y, pos.z] = newRegion;
                        }
                    }
                }
            }
        }

        // Get the voxels that would be contiguous with the original region if it was split at the given position.
        // Use axis abstraction to enable this to work on x, y, or z axis.
        private unsafe void GetContiguousIfSplit(int regionId, ref UnsafeHashSet<int3> result,
                                                 ref UnsafeArray<int3> queueBuffer,
                                                 int3 start, int3 xAxis, int3 yAxis, int3 zAxis) {
            int3 voxelCounts = new(Regions.SizeX, Regions.SizeY, Regions.SizeZ);

            // The UnsafeRingQueue does not have a clear() method, so we reuse the memory but create a new
            // struct on top of it each time.
            UnsafeRingQueue<int3> queue = new((int3*) queueBuffer.Pointer, queueBuffer.Length);

            // Flood fill to find contiguous cubes.
            int z = math.dot(start, zAxis);
            queue.EnqueueChecked(start);
            queue.EnqueueChecked(start + xAxis);
            queue.EnqueueChecked(start + yAxis);
            queue.EnqueueChecked(start + xAxis + yAxis);

            while (queue.TryDequeue(out int3 cur)) {
                int cz = math.dot(cur, zAxis);

                // Don't cross the split axis.
                if (result.Contains(cur) || NativeMathUtility.IsOutOfBounds(voxelCounts, cur) || cz > z ||
                    Regions[cur.x, cur.y, cur.z] != regionId) {
                    continue;
                }

                result.Add(cur);

                // Enqueue neighbors.
                for (int i = 0; i < 3; i++) {
                    for (int sign = -1; sign <= 1; sign += 2) {
                        int3 n = cur;
                        n[i] += sign;
                        queue.EnqueueChecked(n);
                    }
                }
            }
        }

        // Expand contiguous voxels to cross the split axis,
        // but not into any that were contiguous with the actual split region.
        private unsafe void ExpandContiguousExcluding(int regionId, ref UnsafeHashSet<int3> toExpand,
                                                      ref UnsafeHashSet<int3> toExclude,
                                                      ref UnsafeArray<int3> queueBuffer) {
            int3 voxelCounts = new(Regions.SizeX, Regions.SizeY, Regions.SizeZ);

            // The UnsafeRingQueue does not have a clear() method, so we reuse the memory but create a new
            // struct on top of it each time.
            UnsafeRingQueue<int3> queue = new((int3*) queueBuffer.Pointer, queueBuffer.Length);

            foreach (int3 item in toExpand) {
                queue.EnqueueChecked(item);
            }

            while (queue.TryDequeue(out int3 cur)) {
                for (int i = 0; i < 3; i++) {
                    for (int sign = -1; sign <= 1; sign += 2) {
                        int3 n = cur;
                        n[i] += sign;
                        if (NativeMathUtility.IsOutOfBounds(voxelCounts, n) || Regions[n.x, n.y, n.z] != regionId ||
                            toExclude.Contains(n) || !toExpand.Add(n)) {
                            continue;
                        }

                        queue.EnqueueChecked(n);
                    }
                }
            }
        }

        // Get the number of convex cubes that would be broken
        // if the region was split on the given axis at the given point.
        // This approximation is useful to find the least disruptive place to split.
        private int GetBrokenCubeCount(int regionId, int vz, int3 xAxis, int3 yAxis, int3 zAxis, int2 axisLimits) {
            int count = 0;
            int3 zOffset = vz * zAxis;
            Span<int3> sideASamples = stackalloc int3[4];
            sideASamples[0] = -zAxis - xAxis - yAxis;
            sideASamples[1] = -zAxis - xAxis;
            sideASamples[2] = -zAxis - yAxis;
            sideASamples[3] = -zAxis;

            Span<int3> sideBSamples = stackalloc int3[4];
            sideBSamples[0] = xAxis + yAxis;
            sideBSamples[1] = xAxis;
            sideBSamples[2] = yAxis;
            sideBSamples[3] = int3.zero;

            // Define virtual x and y (both orthogonal to the split axis).
            // Loop through all x and y coordinates to find any cubes that would be split.
            int3 voxelCounts = new(Regions.SizeX, Regions.SizeY, Regions.SizeZ);
            for (int vx = 1; vx < axisLimits.x; vx++) {
                for (int vy = 1; vy < axisLimits.y; vy++) {
                    int3 pos = zOffset + vx * xAxis + vy * yAxis;

                    // For each side, check if there are "on" voxels on that side of the cube.

                    bool hasSideA = HasAnyVoxelsForRegion(sideASamples, pos, regionId);
                    bool hasSideB = HasAnyVoxelsForRegion(sideBSamples, pos, regionId);

                    if (hasSideA && hasSideB) {
                        // If has both sides, the cube will be split.
                        // If the cube is concave, this is good, otherwise it is bad.
                        byte cubeIndex =
                            MarchingCubes.GetMarchingCubesIndex(Regions, regionId, pos.x - 1, pos.y - 1, pos.z - 1);
                        if (MarchingCubesCavityTables.CubesWithInternalCavityTable[cubeIndex]) {
                            count--;
                        } else {
                            count++;
                        }
                    } else if (hasSideA || hasSideB) {
                        // If has one side, that means it would be splitting along an existing wall of faces,
                        // which is good as it is more likely to reduce concavities.
                        count--;
                    }
                }
            }

            return count;
        }

        // Check whether the region at the given pos plus any of the direction samples equals the given region id.
        private bool HasAnyVoxelsForRegion(in ReadOnlySpan<int3> samples, int3 basePos, int regionId) {
            int3 voxelCounts = new(Regions.SizeX, Regions.SizeY, Regions.SizeZ);
            for (int i = 0; i < samples.Length; i++) {
                int3 pos = basePos + samples[i];
                if (NativeMathUtility.IsOutOfBounds(voxelCounts, pos)) continue;
                int regionAtPos = Regions[pos.x, pos.y, pos.z];
                if (regionAtPos == regionId) return true;
            }

            return false;
        }

        // Check whether the given cube index has "on" voxels on both sides of the given axis.
        private static bool IsCubeBrokenOnAxis(byte cube, int axis) {
            ref readonly DirectionToVerticesOnSideTableEntry sideAVerts =
                ref MarchingCubesTables.DirectionToVerticesOnSideA.GetRef(axis);

            ref readonly DirectionToVerticesOnSideTableEntry sideBVerts =
                ref MarchingCubesTables.DirectionToVerticesOnSideB.GetRef(axis);

            bool hasSideA = sideAVerts.CubeHasOnVoxelsThisSide(cube);
            bool hasSideB = sideBVerts.CubeHasOnVoxelsThisSide(cube);

            return hasSideA && hasSideB;
        }
    }
}
