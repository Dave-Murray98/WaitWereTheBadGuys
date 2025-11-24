// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

namespace Infohazard.HyperNav {

    /// <summary>
    /// Manages the baking process for a <see cref="NavVolume"/>. Cannot be reused.
    /// </summary>
    public class NavVolumeBakeHandler : NavAreaBaseBakeHandler {
        private int _regionCountAfterConvexify;

        private const int TotalSteps = (int) NavVolumeBakeStep.PopulateData + 1;

        private readonly NavVolume _volume;
        private readonly NavVolumeVisualizationMode _visualizationMode;

        /// <summary>
        /// Create a new bake handler for a volume.
        /// </summary>
        /// <param name="volume">Volume to bake.</param>
        /// <param name="sanityChecks">Whether to run sanity checks to catch baking issues.</param>
        /// <param name="updateSerializedData">Whether to update the serialized data after baking.</param>
        /// <param name="visualizationMode">The visualization mode to use during baking.</param>
        public NavVolumeBakeHandler(NavVolume volume, bool sanityChecks, bool updateSerializedData,
                                    NavVolumeVisualizationMode? visualizationMode = null) 
            : base(volume, sanityChecks, updateSerializedData) {
            _volume = volume;
            
            _visualizationMode = visualizationMode ?? volume.VisualizationMode;
        }

        protected override UniTask GenerateData(NativeCancellationToken token) {
            return NavVolumeUpdate.GenerateVolumeData(_volume, UpdateSerializedData, token, HandleBakeProgressUpdate);
        }

        private void HandleBakeProgressUpdate(NavVolumeBakeStep step, NavBakeStepTiming timing,
                                              in NavVolumeBakeData data) {
            string stepString = step.ToString().SplitCamelCase();
            if (timing == NavBakeStepTiming.Before) {
                UpdateBakeProgress($"{stepString} [{(int) step + 1}/{TotalSteps}]", (float) step / TotalSteps);
                Stopwatch.Restart();
            } else {
                LogStopwatch(stepString);

                if (step == NavVolumeBakeStep.MakeAllRegionsConvex) {
                  _regionCountAfterConvexify = data.RegionCount;
                } else if (step == NavVolumeBakeStep.CombineRegionsWherePossible) {
                    int newRegionCount = data.RegionCount;
                    int removedRegionCount = _regionCountAfterConvexify - newRegionCount;
                    Debug.Log(
                        $"Reduced region count by {removedRegionCount}, from {_regionCountAfterConvexify} to {newRegionCount}.");
                } else if (step == NavVolumeBakeStep.PopulateData) {
                    UpdateBakeProgress("Finalizing...", 1);
                }

                if (SanityChecks) {
                    RunSanityChecks(step, data);
                }

                UpdatePreviewMesh(step, data);
            }
        }

        private void RunSanityChecks(NavVolumeBakeStep step, NavVolumeBakeData data) {
            switch (step) {
                case NavVolumeBakeStep.CalculateRegions:
                    EnsureAllRegionsAreContiguous(data.Voxels);
                    break;
                case NavVolumeBakeStep.MakeAllRegionsConvex or NavVolumeBakeStep.CombineRegionsWherePossible:
                    EnsureAllRegionsAreContiguous(data.Voxels);
                    EnsureAllRegionsAreConvex(data.Voxels, data.RegionCount);
                    break;
            }
        }

        private void UpdatePreviewMesh(NavVolumeBakeStep step, NavVolumeBakeData data) {
            NavVolumeVisualizationMode visMode = _volume.VisualizationMode;
            NavVolumeBakeStep stepForMode = StepForVisMode(visMode);

            if (step > stepForMode) return;
            
            switch (step) {
                case NavVolumeBakeStep.CalculateBlockedVoxels:
                    NavAreaPreviewUtility.BuildVoxelPreviewMesh(_volume, data.Voxels);
                    break;
                case NavVolumeBakeStep.CalculateRegions:
                case NavVolumeBakeStep.MakeAllRegionsConvex:
                case NavVolumeBakeStep.CombineRegionsWherePossible:
                    NavAreaPreviewUtility.BuildRegionIDPreviewMesh(
                        _volume, data.Voxels, data.RegionCount, _volume.VisualizationSoloRegion);
                    break;
                case NavVolumeBakeStep.TriangulateRegions:
                case NavVolumeBakeStep.DecimateRegions:
                    NavAreaPreviewUtility.BuildTriangulationPreviewMesh(
                        _volume, data.MeshInfo.Vertices, data.MeshInfo.RegionTriangleLists,
                        _volume.VisualizationSoloRegion);
                    break;
                case NavVolumeBakeStep.PopulateData:
                    NavAreaPreviewUtility.RebuildPreviewMesh(_volume);
                    break;
            }
        }

        private NavVolumeBakeStep StepForVisMode(NavVolumeVisualizationMode visMode) {
            return visMode switch {
                NavVolumeVisualizationMode.None => (NavVolumeBakeStep)(-1),
                NavVolumeVisualizationMode.Voxels => NavVolumeBakeStep.CalculateBlockedVoxels,
                NavVolumeVisualizationMode.InitialRegions => NavVolumeBakeStep.CalculateRegions,
                NavVolumeVisualizationMode.ConvexRegions => NavVolumeBakeStep.MakeAllRegionsConvex,
                NavVolumeVisualizationMode.CombinedRegions => NavVolumeBakeStep.CombineRegionsWherePossible,
                NavVolumeVisualizationMode.RegionTriangulation => NavVolumeBakeStep.TriangulateRegions,
                NavVolumeVisualizationMode.Decimation => NavVolumeBakeStep.DecimateRegions,
                _ => NavVolumeBakeStep.PopulateData
            };
        }

        private bool EnsureAllRegionsAreConvex(Fast3DArray regions, int regionCount) {
            HashSet<int> errorRegions = new();
            for (int i = 1; i < regionCount; i++) {
                if (!EnsureRegionIsConvex(regions, i)) {
                    errorRegions.Add(i);
                }
            }

            return errorRegions.Count == 0;
        }

        private bool EnsureRegionIsConvex(Fast3DArray regions, int region) {
            int3 voxelCounts = new(regions.SizeX, regions.SizeY, regions.SizeZ);

            // Check for internal concavities.
            for (int x = 0; x < voxelCounts.x - 1; x++) {
                for (int y = 0; y < voxelCounts.y - 1; y++) {
                    for (int z = 0; z < voxelCounts.z - 1; z++) {
                        byte cube = MarchingCubes.GetMarchingCubesIndex(regions, region, x, y, z);
                        if (MarchingCubesCavityTables.CubesWithInternalCavityTable[cube]) {
                            Debug.LogError(
                                $"Region {region} has internal concavity with cube {cube} at {x}, {y}, {z}! Step: {Progress.Operation}",
                                _volume);
                            return false;
                        }
                    }
                }
            }

            // Check for neighbor concavities.
            for (int x = 0; x < voxelCounts.x - 1; x++) {
                for (int y = 0; y < voxelCounts.y - 1; y++) {
                    for (int z = 0; z < voxelCounts.z - 1; z++) {
                        byte cube = MarchingCubes.GetMarchingCubesIndex(regions, region, x, y, z);
                        if (cube is 0 or 255) continue;

                        for (int dir = 0; dir < 3; dir++) {
                            int3 dirVector = MarchingCubesTables.GetPositiveDirection(dir);
                            int dx = dirVector.x;
                            int dy = dirVector.y;
                            int dz = dirVector.z;

                            CubeConcaveNeighborsTableDirectionEntry concaveNeighbors =
                                MarchingCubesCavityTables.CubeConcaveNeighborsTable[cube][dir];
                            if (concaveNeighbors.Count == 0) continue;

                            int nx = x + dx;
                            int ny = y + dy;
                            int nz = z + dz;

                            if (NativeMathUtility.IsOutOfBounds(voxelCounts, new int3(nx + 1, ny + 1, nz + 1)))
                                continue;

                            byte neighborCube = MarchingCubes.GetMarchingCubesIndex(regions, region, nx, ny, nz);
                            if (concaveNeighbors.Contains(neighborCube)) {
                                Debug.LogError(
                                    $"Region {region} has concavity with cube {cube} at {x}, {y}, {z} and neighbor {neighborCube} at {nx}, {ny}, {nz}! Step: {Progress.Operation}",
                                    _volume);
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        // Used for sanity checking to ensure regions don't have any isolated islands.
        private bool EnsureAllRegionsAreContiguous(Fast3DArray regions) {
            int3 voxelCounts = new(regions.SizeX, regions.SizeY, regions.SizeZ);
            Dictionary<int, HashSet<int3>> regionDict = new();
            HashSet<int> errorRegions = new();
            for (int x = 0; x < voxelCounts.x; x++) {
                for (int y = 0; y < voxelCounts.y; y++) {
                    for (int z = 0; z < voxelCounts.z; z++) {
                        int3 pos = new(x, y, z);
                        int region = regions[x, y, z];
                        if (region == 0) continue;
                        if (errorRegions.Contains(region)) continue;

                        if (regionDict.TryGetValue(region, out HashSet<int3> regionCont)) {
                            // When a voxel is found from an existing region but it's not in the filled set,
                            // the region must not be contiguous.
                            if (!regionCont.Contains(pos)) {
                                Debug.LogError(
                                    $"Region {region} is not contiguous at {pos}! Step: {Progress.Operation}", _volume);
                                errorRegions.Add(region);
                            }
                        } else {
                            // When a new region is found, flood fill the HashSet with all of the contiguous voxels.
                            regionCont = new HashSet<int3>();
                            regionDict[region] = regionCont;

                            Queue<int3> queue = new Queue<int3>();
                            queue.Enqueue(pos);
                            while (queue.Count > 0) {
                                int3 cur = queue.Dequeue();
                                if (NativeMathUtility.IsOutOfBounds(voxelCounts, cur) ||
                                    regions[cur.x, cur.y, cur.z] != region ||
                                    !regionCont.Add(cur)) continue;

                                for (int dir = 0; dir < 3; dir++) {
                                    for (int sign = -1; sign <= 1; sign += 2) {
                                        int3 neighbor = cur;
                                        neighbor[dir] += sign;
                                        queue.Enqueue(neighbor);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return errorRegions.Count == 0;
        }
    }
}
