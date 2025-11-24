// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs;
using Infohazard.HyperNav.Jobs.Baking;
using Infohazard.HyperNav.Jobs.Baking.Volume;
using Infohazard.HyperNav.Jobs.Utility;
using Infohazard.HyperNav.Settings;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Infohazard.HyperNav {
    public enum NavVolumeBakeStep {
        CalculateBlockedVoxels,
        CalculateRegions,
        MakeAllRegionsConvex,
        CombineRegionsWherePossible,
        TriangulateRegions,
        DecimateRegions,
        PopulateData,
    }

    public struct NavVolumeBakeData {
        public NavVolume Volume;
        public NavVolumeSettings Settings;
        public Fast3DArray Voxels;
        public BakingNavVolumeMeshInfo MeshInfo;
        public int RegionCount;
        public NativeHashSet<int> StaticColliders;
    }

    public delegate void NavVolumeUpdateStepCallback(NavVolumeBakeStep step, NavBakeStepTiming timing,
                                                     in NavVolumeBakeData data);

    /// <summary>
    /// Utility functions for updating NavVolume data.
    /// </summary>
    public static class NavVolumeUpdate {
        private static readonly ProfilerMarker ProfilerMarkerReachableVoxelsJob =
            new("Wait for ReachableVoxelsJob");

        private static readonly ProfilerMarker ProfilerMarkerCalculateRegionsJob =
            new("Wait for CalculateRegionsJob");

        private static readonly ProfilerMarker ProfilerMarkerMakeRegionsConvexJob =
            new("Wait for MakeRegionsConvexJob");

        private static readonly ProfilerMarker ProfilerMarkerRebuildMeshConnectionDataJob =
            new("Run RebuildMeshConnectionDataJob");

        /// <summary>
        /// Generate the data for a NavVolume. Can be used at runtime to support dynamic baking.
        /// Note that runtime baking is a performance intensive operation and should occur during a loading screen if possible.
        /// </summary>
        /// <param name="volume">The NavVolume to update.</param>
        /// <param name="updateSerializedData">Whether to update the serialized data on the surface.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <param name="stepCallback">Optional callback that will be invoked at each step of the update process.</param>
        public static async UniTask GenerateVolumeData(NavVolume volume, bool updateSerializedData,
                                                       NativeCancellationToken cancellationToken,
                                                       NavVolumeUpdateStepCallback stepCallback = null) {
            NavVolumeBakeData data = new() {
                Volume = volume,
                Settings = volume.Settings.Clone(),
            };

            float3 fVoxelCounts = volume.Bounds.size / data.Settings.VoxelSize;
            int3 voxelCounts = new() {
                x = Mathf.FloorToInt(fVoxelCounts.x),
                y = Mathf.FloorToInt(fVoxelCounts.y),
                z = Mathf.FloorToInt(fVoxelCounts.z),
            };

            Fast3DArray voxels = new(voxelCounts.x, voxelCounts.y, voxelCounts.z);
            data.Voxels = voxels;

            if (data.Settings.StaticOnly) {
                data.StaticColliders = NavAreaBakingUtility.GetStaticCollidersForBaking();
            } else {
                // Needs to be allocated or we'll get errors when running a job.
                data.StaticColliders = new NativeHashSet<int>(0, Allocator.Persistent);
            }

            try {
                stepCallback?.Invoke(NavVolumeBakeStep.CalculateBlockedVoxels, NavBakeStepTiming.Before, data);
                await CalculateBlockedVoxels(data);
                cancellationToken.ThrowIfCancellationRequested();
                stepCallback?.Invoke(NavVolumeBakeStep.CalculateBlockedVoxels, NavBakeStepTiming.After, data);

                // Separate open voxels into regions.
                // At this point a region is a contiguous set of voxels,
                // and only non-contiguous islands become separate regions.
                stepCallback?.Invoke(NavVolumeBakeStep.CalculateRegions, NavBakeStepTiming.Before, data);
                data.RegionCount = await CalculateRegions(voxels);
                stepCallback?.Invoke(NavVolumeBakeStep.CalculateRegions, NavBakeStepTiming.After, data);
                cancellationToken.ThrowIfCancellationRequested();

                stepCallback?.Invoke(NavVolumeBakeStep.MakeAllRegionsConvex, NavBakeStepTiming.Before, data);
                data.RegionCount = await MakeAllRegionsConvex(volume, voxels, data.RegionCount, cancellationToken);
                stepCallback?.Invoke(NavVolumeBakeStep.MakeAllRegionsConvex, NavBakeStepTiming.After, data);
                cancellationToken.ThrowIfCancellationRequested();

                stepCallback?.Invoke(NavVolumeBakeStep.CombineRegionsWherePossible, NavBakeStepTiming.Before,
                                     data);
                data.RegionCount = await CombineRegionsWherePossible(voxels, data.RegionCount, cancellationToken);
                stepCallback?.Invoke(NavVolumeBakeStep.CombineRegionsWherePossible, NavBakeStepTiming.After,
                                     data);
                cancellationToken.ThrowIfCancellationRequested();

                BakingNavVolumeMeshInfo meshInfo = new(data.RegionCount, Allocator.Persistent);
                data.MeshInfo = meshInfo;

                stepCallback?.Invoke(NavVolumeBakeStep.TriangulateRegions, NavBakeStepTiming.Before, data);
                await TriangulateRegions(data);
                stepCallback?.Invoke(NavVolumeBakeStep.TriangulateRegions, NavBakeStepTiming.After, data);
                cancellationToken.ThrowIfCancellationRequested();

                stepCallback?.Invoke(NavVolumeBakeStep.DecimateRegions, NavBakeStepTiming.Before, data);
                await DecimateRegions(meshInfo, volume, cancellationToken);
                stepCallback?.Invoke(NavVolumeBakeStep.DecimateRegions, NavBakeStepTiming.After, data);
                cancellationToken.ThrowIfCancellationRequested();

                stepCallback?.Invoke(NavVolumeBakeStep.PopulateData, NavBakeStepTiming.Before, data);
                await PopulateData(meshInfo, volume, updateSerializedData);
                stepCallback?.Invoke(NavVolumeBakeStep.PopulateData, NavBakeStepTiming.After, data);
            } finally {
                if (!data.Voxels.IsNull) {
                    data.Voxels.Dispose();
                }

                if (data.MeshInfo.Vertices.IsCreated) {
                    data.MeshInfo.Dispose();
                }

                data.StaticColliders.Dispose();
            }
        }

        // Perform samples at every voxel to determine of an agent with radius agentRadius can fit in that position.
        private static async UniTask CalculateBlockedVoxels(NavVolumeBakeData bakeData) {
            NavVolume volume = bakeData.Volume;
            Fast3DArray voxels = bakeData.Voxels;

            int3 voxelCounts = new(voxels.SizeX, voxels.SizeY, voxels.SizeZ);
            int voxelCount = voxelCounts.x * voxelCounts.y * voxelCounts.z;

            PhysicsScene physicsScene = default;
            bool usePhysicsScene = false;

            physicsScene = volume.gameObject.scene.GetPhysicsScene();
            usePhysicsScene = physicsScene.IsValid();


            int queryCountPerAxis;

            if (bakeData.Settings.EnableMultiQuery) {
                queryCountPerAxis = Mathf.CeilToInt(bakeData.Settings.VoxelSize / bakeData.Settings.MaxAgentRadius) + 1;
            } else {
                queryCountPerAxis = 1;
            }

            long queryCountPerVoxel = queryCountPerAxis * queryCountPerAxis * queryCountPerAxis;
            long totalQueryCount = voxelCount * queryCountPerVoxel;
            int maxHitsPerQuery = bakeData.Settings.StaticOnly ? 8 : 1;
            long totalResultCount = totalQueryCount * maxHitsPerQuery;

            if (totalResultCount > int.MaxValue) {
                throw new InvalidOperationException(
                    "The total number of physics queries for baking this volume exceeds the maximum possible value." +
                    "Consider splitting the volume up into smaller chunks.");
            }

            NativeArray<OverlapSphereCommand> queries = new((int) totalQueryCount, Allocator.Persistent);
            NativeArray<ColliderHit> results = new((int) totalResultCount, Allocator.Persistent);

            float distancePer =
                bakeData.Settings.EnableMultiQuery ? bakeData.Settings.VoxelSize / (queryCountPerAxis - 1) : 1;

            GenerateVolumeSphereQueriesJob generateQueriesJob = new() {
                Queries = queries,
                PhysicsScene = physicsScene,
                UsePhysicsScene = usePhysicsScene,
                VolumeTransform = volume.transform.localToWorldMatrix,
                Bounds = new NativeBounds(volume.Bounds.center.ToV4Pos(), volume.Bounds.extents.ToV4()),
                VoxelCounts = voxelCounts,
                QueryCountPerAxis = queryCountPerAxis,
                DistanceBetweenQueries = distancePer,
                VoxelSize = bakeData.Settings.VoxelSize,
                QueryRadius = bakeData.Settings.MaxAgentRadius,
                QueryLayerMask = bakeData.Settings.BlockingLayers,
                EnableMultiQuery = bakeData.Settings.EnableMultiQuery,
            };

            int batchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(voxelCount);
            JobHandle genQueriesHandle = generateQueriesJob.Schedule(voxelCount, batchSize);
            JobHandle queryHandle =
                OverlapSphereCommand.ScheduleBatch(queries, results, batchSize, maxHitsPerQuery, genQueriesHandle);

            PopulateVolumeVoxelsFromHitsJob populateJob = new() {
                HitsArray = results,
                HitCountPerVoxel = (int) queryCountPerVoxel * maxHitsPerQuery,
                UseStaticCheck = bakeData.Settings.StaticOnly,
                Voxels = voxels,
                StaticColliders = bakeData.StaticColliders,
            };

            int populateBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(voxelCount);
            await populateJob.Schedule(voxelCount, populateBatchSize, queryHandle);

            queries.Dispose();
            results.Dispose();

            // If using start locations, set all positions that are not contiguous with a start location as invalid.
            if (bakeData.Volume.UseStartLocations) {
                NativeQueue<int3> queue = new(Allocator.TempJob);

                foreach (Vector3 startLocation in bakeData.Volume.StartLocations) {
                    Vector3 posInBounds = startLocation - volume.Bounds.min;
                    Vector3Int voxelPos = Vector3Int.RoundToInt(posInBounds / bakeData.Settings.VoxelSize);
                    queue.Enqueue(new int3(voxelPos.x, voxelPos.y, voxelPos.z));
                }

                ReachableVoxelsJob reachableJob = new() {
                    Voxels = voxels,
                    QueueToExplore = queue,
                };

                JobHandle handle2 = reachableJob.Schedule();

                for (int i = 0; i < 2; i++) {
                    await UniTask.Yield();
                    if (handle2.IsCompleted) break;
                }

                using (ProfilerMarkerReachableVoxelsJob.Auto()) {
                    handle2.Complete();
                }

                queue.Dispose();
            }
        }

        // Calculate the initial regions by finding all contiguous islands of voxels.
        private static async UniTask<int> CalculateRegions(Fast3DArray voxels) {
            NativeReference<int> regionCountPtr = new(1, Allocator.TempJob);

            CalculateRegionsJob job = new() {
                Voxels = voxels,
                RegionCount = regionCountPtr,
            };

            JobHandle handle = job.Schedule();

            for (int i = 0; i < 3; i++) {
                await UniTask.Yield();
                if (handle.IsCompleted) break;
            }

            using (ProfilerMarkerCalculateRegionsJob.Auto()) {
                handle.Complete();
            }

            int result = regionCountPtr.Value;

            regionCountPtr.Dispose();

            return result;
        }

        // Ensure that all regions are convex by splitting them at any concavities.
        private static async UniTask<int> MakeAllRegionsConvex(NavVolume volume, Fast3DArray regions, int regionCount,
                                                               NativeCancellationToken cancellationToken) {
            // Assume maximum possible region count is equal to total voxel count.
            // This is impossible, but the cost to allocate this queue once is not too bad.
            int maxRegionCount = regions.SizeX * regions.SizeY * regions.SizeZ;

            NativeList<int> regionQueue = new(maxRegionCount, Allocator.Persistent);
            for (int i = 1; i < regionCount; i++) {
                regionQueue.Add(i);
            }

            // Search all regions. When a region is split, search the two created regions.
            // Repeat this until no tasks remain.
            MakeRegionsConvexJob makeConvexJob = new() {
                Regions = regions,
                RegionQueue = regionQueue,
                VolumeTransform = volume.transform.localToWorldMatrix,
                VolumeBounds = new NativeBounds(volume.Bounds.center.ToV4Pos(), volume.Bounds.extents.ToV4()),
                CancellationToken = cancellationToken
            };

            try {
                while (regionQueue.Length > 0) {
                    NativeArray<int> createdRegionCounts = new(regionQueue.Length, Allocator.Persistent);
                    makeConvexJob.CreatedRegionCounts = createdRegionCounts;

                    int batchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(regionQueue.Length);
                    JobHandle makeConvexHandle = makeConvexJob.Schedule(regionQueue.Length, batchSize);

                    MakeRegionsConvexCleanupJob cleanupJob = new() {
                        Regions = regions,
                        OldRegionCount = regionCount,
                        RegionQueue = regionQueue,
                        CreatedRegionCounts = createdRegionCounts,
                    };

                    JobHandle cleanupHandle = cleanupJob.Schedule(makeConvexHandle);

                    await cleanupHandle;
                    regionCount += regionQueue.Length;
                    createdRegionCounts.Dispose();

                    cancellationToken.ThrowIfCancellationRequested();
                }
            } finally {
                regionQueue.Dispose();
            }

            return regionCount;
        }

        // Look at all pairs of regions and combine any paris where the two regions together would be convex.
        // This helps to reduce the total region count significantly, especially reducing the tiny regions.
        private static async UniTask<int> CombineRegionsWherePossible(Fast3DArray regions, int regionCount,
                                                                      NativeCancellationToken cancellationToken) {
            // Get map of all adjacent regions.
            // This only needs to be calculated once, after that we can just update it.
            NativeParallelHashMap<int, UnsafeParallelHashSet<int>>
                adjacencyMap = new(regionCount, Allocator.Persistent);

            NativeHashSet<int> removedRegions = default;
            NativeArray<RegionMergeCandidateList> mergeCandidateLists = default;
            NativeList<int2> regionPairsToMerge = default;

            try {
                GetRegionAdjacencyMapJob adjacencyJob = new() {
                    Regions = regions,
                    RegionCount = regionCount,
                    RegionAdjacencyMap = adjacencyMap,
                };

                await adjacencyJob.Schedule();

                cancellationToken.ThrowIfCancellationRequested();

                removedRegions = new NativeHashSet<int>(regionCount, Allocator.Persistent);
                mergeCandidateLists = new NativeArray<RegionMergeCandidateList>(regionCount - 1, Allocator.Persistent);
                regionPairsToMerge = new NativeList<int2>(regionCount / 2, Allocator.Persistent);

                bool combinedAnyRegion;
                do {
                    CalculateRegionMergeCandidatesJob calculateCandidatesJob = new() {
                        Regions = regions,
                        RegionAdjacency = adjacencyMap,
                        MergeCandidateLists = mergeCandidateLists,
                        CancellationToken = cancellationToken,
                    };

                    int calculationCount = regionCount - 1;
                    int batchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(calculationCount);
                    JobHandle handle = calculateCandidatesJob.Schedule(calculationCount, batchSize);

                    await handle;

                    cancellationToken.ThrowIfCancellationRequested();

                    regionPairsToMerge.Clear();

                    DetermineRegionPairsToMergeJob determinePairsJob = new() {
                        MergeCandidateLists = mergeCandidateLists,
                        RegionCount = regionCount,
                        RegionsAdjacency = adjacencyMap,
                        RegionPairsToMerge = regionPairsToMerge,
                        RemovedRegions = removedRegions,
                    };

                    // Not worth running this in a thread, it should be quite fast.
                    determinePairsJob.Run();

                    CombineRegionsJob combineRegionsJob = new() {
                        Regions = regions,
                        RegionsToMerge = regionPairsToMerge,
                    };

                    int combineBatchSize =
                        JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(regionPairsToMerge.Length);
                    JobHandle handle2 = combineRegionsJob.Schedule(regionPairsToMerge.Length, combineBatchSize);

                    await handle2;

                    cancellationToken.ThrowIfCancellationRequested();

                    combinedAnyRegion = regionPairsToMerge.Length > 0;
                } while (combinedAnyRegion);

                int removedRegionCount = removedRegions.Count;
                int newRegionCount = regionCount - removedRegionCount;

                // Shift all region indices down by number of lower regions that were removed.
                MapRegionsJob mapRegionsJob = new() {
                    Regions = regions,
                    OldRegionCount = regionCount,
                    RemovedRegions = removedRegions,
                };

                mapRegionsJob.Run();

                return newRegionCount;
            } finally {
                // Dispose of all the structures used for this process.
                foreach (KeyValue<int, UnsafeParallelHashSet<int>> pair in adjacencyMap) {
                    pair.Value.Dispose();
                }

                adjacencyMap.Dispose();
                removedRegions.Dispose();
                mergeCandidateLists.Dispose();
                regionPairsToMerge.Dispose();
            }
        }

        private static async UniTask TriangulateRegions(NavVolumeBakeData bakeData) {
            NativeArray<UnsafeList<int4>> vertices = new(bakeData.RegionCount, Allocator.Persistent);

            try {
                TriangulateRegionsJob triangulateRegionsJob = new() {
                    Regions = bakeData.Voxels,
                    Vertices = vertices,
                };

                int batchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(bakeData.RegionCount);
                JobHandle triangulateHandle = triangulateRegionsJob.Schedule(bakeData.RegionCount, batchSize);

                await triangulateHandle;

                VolumeTriangleMeshToIndicesJob triangleMeshToIndicesJob = new() {
                    Vertices = vertices,
                    Mesh = bakeData.MeshInfo,
                    VoxelSize = bakeData.Settings.VoxelSize,
                    VolumeBounds = new NativeBounds(bakeData.Volume.Bounds.center.ToV4Pos(),
                                                    bakeData.Volume.Bounds.extents.ToV4()),
                    VoxelCounts = new int3(bakeData.Voxels.SizeX, bakeData.Voxels.SizeY, bakeData.Voxels.SizeZ),
                };

                JobHandle triangleMeshToIndicesHandle = triangleMeshToIndicesJob.Schedule(triangulateHandle);

                await triangleMeshToIndicesHandle;
            } finally {
                for (int i = 0; i < vertices.Length; i++) {
                    if (vertices[i].IsCreated) {
                        vertices[i].Dispose();
                    }
                }

                vertices.Dispose();
            }
        }

        private static async UniTask DecimateRegions(BakingNavVolumeMeshInfo meshInfo, NavVolume volume,
                                                     NativeCancellationToken cancellationToken) {
            DecimateRegionsJob job = new() {
                Vertices = meshInfo.Vertices,
                VertexConnections = meshInfo.VertexConnections,
                VertexRegionMembership = meshInfo.VertexRegionMembership,
                TriangleIndicesPerRegion = meshInfo.TriangleIndicesPerRegion,
                RegionTriangleLists = meshInfo.RegionTriangleLists,
                VolumeTransform = volume.transform.localToWorldMatrix,
                CancellationToken = cancellationToken,
            };

            int batchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(meshInfo.RegionTriangleLists.Length);
            await job.Schedule(meshInfo.RegionTriangleLists.Length, batchSize);

            RebuildMeshConnectionDataJob rebuildMeshConnectionDataJob = new() {
                Mesh = meshInfo,
            };

            using (ProfilerMarkerRebuildMeshConnectionDataJob.Auto()) {
                rebuildMeshConnectionDataJob.Run();
            }
        }

        private static async UniTask PopulateData(BakingNavVolumeMeshInfo meshInfo, NavVolume volume,
                                                  bool updateSerializedData) {
            int regionCount = meshInfo.RegionTriangleLists.Length - 1;
            NativeArray<CalculateInternalLinksJob.RegionOutput> linkData = new(regionCount, Allocator.Persistent);
            NativeArray<UnsafeList<NativePlane>> boundPlaneData = new(regionCount, Allocator.Persistent);

            CalculateInternalLinksJob calculateInternalLinksJob = new() {
                Mesh = meshInfo,
                OutputData = linkData,
            };

            CalculateRegionBoundPlanesJob calculateRegionBoundPlanesJob = new() {
                Mesh = meshInfo,
                OutputData = boundPlaneData,
            };

            int batchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(regionCount);
            await calculateInternalLinksJob.Schedule(regionCount, batchSize);
            await calculateRegionBoundPlanesJob.Schedule(regionCount, batchSize);

            NativeReference<NativeNavVolumeData> volumeData = new(Allocator.Persistent);
            BuildVolumeDataJob buildVolumeDataJob = new() {
                Mesh = meshInfo,
                LinkData = linkData,
                BoundPlanes = boundPlaneData,
                OutputData = volumeData,
            };

            await buildVolumeDataJob.Schedule();

            NativeNavVolumeData data = volumeData.Value;

            for (int i = 0; i < regionCount; i++) {
                linkData[i].InternalLinks.Dispose();
                linkData[i].InternalLinkVertices.Dispose();
                linkData[i].InternalLinkEdges.Dispose();
                linkData[i].InternalLinkTriangles.Dispose();
                boundPlaneData[i].Dispose();
            }

            linkData.Dispose();
            boundPlaneData.Dispose();
            volumeData.Dispose();

            data = new NativeNavVolumeData(
                id: volume.InstanceID,
                transform: volume.transform.localToWorldMatrix,
                inverseTransform: volume.transform.worldToLocalMatrix,
                bounds: new NativeBounds(volume.Bounds.center.ToV4Pos(), volume.Bounds.extents.ToV4()),
                layer: volume.Layer,
                vertices: data.Vertices,
                regions: data.Regions,
                triangleIndices: data.TriangleIndices,
                blockingTriangleIndexCount: data.BlockingTriangleIndexCount,
                boundPlanes: data.BoundPlanes,
                internalLinks: data.InternalLinks,
                externalLinks: data.ExternalLinks,
                linkVertices: data.LinkVertices,
                linkEdges: data.LinkEdges,
                linkTriangles: data.LinkTriangles);


            NativeNavVolumeDataPointers pointers = volume.DataStructurePointers;
            NativeNavVolumeData oldData = volume.NativeData;

            volume.UpdateNativeData(data, default, updateSerializedData, updateSerializedData);
            oldData.Dispose();
            pointers.Dispose();
        }
    }
}
