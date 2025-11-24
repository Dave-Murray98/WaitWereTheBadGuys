// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct CalculateRegionMergeCandidatesJob : IJobParallelFor {
        [ReadOnly] public Fast3DArray Regions;
        [ReadOnly] public NativeParallelHashMap<int, UnsafeParallelHashSet<int>> RegionAdjacency;
        
        public NativeCancellationToken CancellationToken;

        [WriteOnly] public NativeArray<RegionMergeCandidateList> MergeCandidateLists;

        public void Execute(int index) {
            int regionId = index + 1;

            RegionMergeCandidateList candidateList = default;
            if (!RegionAdjacency.TryGetValue(regionId, out UnsafeParallelHashSet<int> adjacent)) {
                MergeCandidateLists[index] = candidateList;
                return;
            }

            foreach (int otherRegion in adjacent) {
                if (otherRegion == regionId) {
                    Debug.LogError($"Region {regionId} reported as adjacent to itself.");
                    continue;
                }

                // We only want to make this calculation once for each pair of regions.
                // In order to ensure each thread does a similar amount of work,
                // we first check if the sum of the two region ids is even or odd.
                int idSum = regionId + otherRegion;
                bool largerIdDoesWork = idSum % 2 == 0;

                // The sum will be the same for the two threads processing this pair of regions.
                // If the sum is even, the thread with the larger region id will do the work.
                // If the sum is odd, the thread with the smaller region id will do the work.
                bool isLargerId = regionId > otherRegion;
                if (largerIdDoesWork && !isLargerId) {
                    continue;
                }

                if (!CanCombineRegions(regionId, otherRegion)) continue;

                candidateList.Add(otherRegion);

                // Once we've found the max number of candidates, we can stop.
                if (candidateList.Count >= RegionMergeCandidateList.MaxCount) {
                    break;
                }
                
                if (CancellationToken.IsCancellationRequested) {
                    break;
                }
            }

            MergeCandidateLists[index] = candidateList;
        }

        private bool CanCombineRegions(int region1, int region2) {
            int3 voxelCounts = new(Regions.SizeX, Regions.SizeY, Regions.SizeZ);

            // First check for any internal concavities.
            for (int x = 0; x < voxelCounts.x - 1; x++) {
                for (int y = 0; y < voxelCounts.y - 1; y++) {
                    for (int z = 0; z < voxelCounts.z - 1; z++) {
                        byte cube = MarchingCubes.GetMarchingCubesIndex(Regions, region1, region2, x, y, z);
                        if (MarchingCubesCavityTables.CubesWithInternalCavityTable[cube]) {
                            return false;
                        }
                    }
                }
            }

            // Next check for concavities created by neighbor relationships.
            for (int x = 0; x < voxelCounts.x - 1; x++) {
                for (int y = 0; y < voxelCounts.y - 1; y++) {
                    for (int z = 0; z < voxelCounts.z - 1; z++) {
                        byte selfCube = MarchingCubes.GetMarchingCubesIndex(Regions, region1, region2, x, y, z);
                        if (selfCube is 0 or 255) continue;

                        for (int dir = 0; dir < 3; dir++) {
                            int3 dirVector = MarchingCubesTables.GetPositiveDirection(dir);
                            int dx = dirVector.x;
                            int dy = dirVector.y;
                            int dz = dirVector.z;

                            int nx = x + dx;
                            int ny = y + dy;
                            int nz = z + dz;

                            if (NativeMathUtility.IsOutOfBounds(voxelCounts, new int3(nx + 1, ny + 1, nz + 1))) {
                                continue;
                            }

                            CubeConcaveNeighborsTableDirectionEntry concaveNeighbors =
                                MarchingCubesCavityTables.CubeConcaveNeighborsTable[selfCube][dir];
                            if (concaveNeighbors.Count == 0) continue;

                            byte neighborCube =
                                MarchingCubes.GetMarchingCubesIndex(Regions, region1, region2, nx, ny, nz);

                            if (concaveNeighbors.Contains(neighborCube)) {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }
    }
}
