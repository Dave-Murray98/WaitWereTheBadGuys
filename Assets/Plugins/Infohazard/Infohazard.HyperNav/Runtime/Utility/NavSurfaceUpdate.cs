// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Baking;
using Infohazard.HyperNav.Jobs.Baking.Surface;
using Infohazard.HyperNav.Jobs.Baking.Volume;
using Infohazard.HyperNav.Jobs.Utility;
using Infohazard.HyperNav.Settings;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Infohazard.HyperNav {
    public enum NavSurfaceBakeStep {
        CalculateBlockedAndWalkableVoxels,
        TriangulateVoxels,
        ShrinkwrapMesh,
        FilterUprightDirections,
        FilterCollisions,
        SplitOrRemoveTriangles,
        SplitVertices,
        ShrinkwrapMeshAfterSplit,
        DecimateTriangles,
        PruneSmallTriangles,
        ErodeEdges,
        CreateGroups,
        PopulateData,
    }

    public struct NavSurfaceBakeData {
        public NavSurface Surface;
        public NavSurfaceSettings Settings;
        public Fast3DArray Voxels;
        public BakingNavSurfaceMeshInfo MeshInfo;
        public NativeHashSet<int> StaticColliders;
        public NavSurfaceFilterData FilterData;
    }

    public struct NavSurfaceFilterData : IDisposable {
        public int Iteration;
        public NativeList<int> VerticesToCheck;
        public NativeList<int> TrianglesToCheck;
        public NativeHashSet<int> TrianglesToSplit;
        public NativeHashSet<int> TrianglesToRemove;

        public void Dispose() {
            VerticesToCheck.Dispose();
            TrianglesToCheck.Dispose();
            TrianglesToSplit.Dispose();
            TrianglesToRemove.Dispose();
        }
    }

    public delegate void NavSurfaceUpdateStepCallback(NavSurfaceBakeStep step, NavBakeStepTiming timing,
                                                      in NavSurfaceBakeData data);

    /// <summary>
    /// Utility class for updating NavSurface data.
    /// </summary>
    public static class NavSurfaceUpdate {
        public static int VisIndex;

        /// <summary>
        /// Generate surface data for a NavSurface. Can be used at runtime to support dynamic baking.
        /// Note that runtime baking is a performance intensive operation and should occur during a loading screen if possible.
        /// </summary>
        /// <param name="surface">The surface to update.</param>
        /// <param name="updateSerializedData">Whether to update the serialized data on the surface.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <param name="stepCallback">Optional callback that will be invoked at each step of the update process.</param>
        public static async UniTask GenerateSurfaceData(NavSurface surface, bool updateSerializedData,
                                                        NativeCancellationToken cancellationToken,
                                                        NavSurfaceUpdateStepCallback stepCallback = null) {
            NavSurfaceBakeData data = new() {
                Surface = surface,
                Settings = surface.Settings.Clone(),
            };

            float3 fVoxelCounts = surface.Bounds.size / data.Settings.VoxelSize;
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
                stepCallback?.Invoke(NavSurfaceBakeStep.CalculateBlockedAndWalkableVoxels, NavBakeStepTiming.Before,
                    data);
                await CalculateBlockedAndWalkableVoxels(data);
                cancellationToken.ThrowIfCancellationRequested();
                stepCallback?.Invoke(NavSurfaceBakeStep.CalculateBlockedAndWalkableVoxels, NavBakeStepTiming.After,
                    data);

                data.MeshInfo = new BakingNavSurfaceMeshInfo(Allocator.Persistent);

                stepCallback?.Invoke(NavSurfaceBakeStep.TriangulateVoxels, NavBakeStepTiming.Before, data);
                await TriangulateVoxels(data);
                cancellationToken.ThrowIfCancellationRequested();
                stepCallback?.Invoke(NavSurfaceBakeStep.TriangulateVoxels, NavBakeStepTiming.After, data);

                int filterIterations = Mathf.Max(1, data.Settings.MaxTriangleDivisions + 1);
                CreateFilterData(ref data);

                for (int i = 0; i < filterIterations; i++) {
                    data.FilterData.Iteration = i;

                    stepCallback?.Invoke(NavSurfaceBakeStep.ShrinkwrapMesh, NavBakeStepTiming.Before, data);
                    data.MeshInfo = await ShrinkwrapMesh(data);
                    cancellationToken.ThrowIfCancellationRequested();
                    stepCallback?.Invoke(NavSurfaceBakeStep.ShrinkwrapMesh, NavBakeStepTiming.After, data);

                    stepCallback?.Invoke(NavSurfaceBakeStep.FilterUprightDirections, NavBakeStepTiming.Before, data);
                    data = await FilterTriangleUprightDirections(data, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    stepCallback?.Invoke(NavSurfaceBakeStep.FilterUprightDirections, NavBakeStepTiming.After, data);

                    stepCallback?.Invoke(NavSurfaceBakeStep.FilterCollisions, NavBakeStepTiming.Before, data);
                    await FilterTriangleCollisions(data);
                    cancellationToken.ThrowIfCancellationRequested();
                    stepCallback?.Invoke(NavSurfaceBakeStep.FilterCollisions, NavBakeStepTiming.After, data);

                    stepCallback?.Invoke(NavSurfaceBakeStep.SplitOrRemoveTriangles, NavBakeStepTiming.Before, data);
                    await RemoveOrSplitFailingTriangles(data, filterIterations);
                    cancellationToken.ThrowIfCancellationRequested();
                    stepCallback?.Invoke(NavSurfaceBakeStep.SplitOrRemoveTriangles, NavBakeStepTiming.After, data);
                }

                stepCallback?.Invoke(NavSurfaceBakeStep.SplitVertices, NavBakeStepTiming.Before, data);
                await SplitVertexTrianglesOnUpDirection(data);
                cancellationToken.ThrowIfCancellationRequested();
                stepCallback?.Invoke(NavSurfaceBakeStep.SplitVertices, NavBakeStepTiming.After, data);

                ResetFilterData(ref data);

                RebuildVertexNeighborsJob rebuildNeighborsJob = new() {
                    TriangleIndices = data.MeshInfo.TriangleList.AsArray(),
                    VertexTriangles = data.MeshInfo.VertexTriangles.AsArray(),
                    VertexConnections = data.MeshInfo.VertexConnections.AsArray()
                };

                await rebuildNeighborsJob.Schedule();

                stepCallback?.Invoke(NavSurfaceBakeStep.ShrinkwrapMeshAfterSplit, NavBakeStepTiming.Before, data);
                data.MeshInfo = await ShrinkwrapMesh(data);
                cancellationToken.ThrowIfCancellationRequested();
                stepCallback?.Invoke(NavSurfaceBakeStep.ShrinkwrapMeshAfterSplit, NavBakeStepTiming.After, data);

                stepCallback?.Invoke(NavSurfaceBakeStep.DecimateTriangles, NavBakeStepTiming.Before, data);
                await DecimateSurfaceTriangles(data, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                stepCallback?.Invoke(NavSurfaceBakeStep.DecimateTriangles, NavBakeStepTiming.After, data);

                stepCallback?.Invoke(NavSurfaceBakeStep.PruneSmallTriangles, NavBakeStepTiming.Before, data);
                await PruneSmallTriangles(data, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                stepCallback?.Invoke(NavSurfaceBakeStep.PruneSmallTriangles, NavBakeStepTiming.After, data);

                stepCallback?.Invoke(NavSurfaceBakeStep.ErodeEdges, NavBakeStepTiming.Before, data);
                await ErodeEdges(data, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                stepCallback?.Invoke(NavSurfaceBakeStep.ErodeEdges, NavBakeStepTiming.After, data);

                stepCallback?.Invoke(NavSurfaceBakeStep.CreateGroups, NavBakeStepTiming.Before, data);
                data.MeshInfo = await CreateGroups(data.MeshInfo, surface);
                cancellationToken.ThrowIfCancellationRequested();
                stepCallback?.Invoke(NavSurfaceBakeStep.CreateGroups, NavBakeStepTiming.After, data);

                stepCallback?.Invoke(NavSurfaceBakeStep.PopulateData, NavBakeStepTiming.Before, data);
                await PopulateData(data, updateSerializedData);
                stepCallback?.Invoke(NavSurfaceBakeStep.PopulateData, NavBakeStepTiming.After, data);
            } catch (Exception ex) {
                Debug.LogException(ex);
            } finally {
                if (!data.Voxels.IsNull) {
                    data.Voxels.Dispose();
                }

                if (data.MeshInfo.Vertices.IsCreated) {
                    data.MeshInfo.Dispose();
                }

                data.StaticColliders.Dispose();

                data.FilterData.Dispose();
                data.FilterData = default;
            }
        }

        // Perform samples at every voxel to determine of an agent with radius agentRadius can fit in that position.
        private static async UniTask CalculateBlockedAndWalkableVoxels(NavSurfaceBakeData bakeData) {
            NavSurface surface = bakeData.Surface;
            Fast3DArray voxels = bakeData.Voxels;

            int3 voxelCounts = new(voxels.SizeX, voxels.SizeY, voxels.SizeZ);
            long voxelCount = voxelCounts.x * voxelCounts.y * voxelCounts.z;

            PhysicsScene physicsScene = surface.gameObject.scene.GetPhysicsScene();
            bool usePhysicsScene = physicsScene.IsValid();

            int maxHitsPerQuery = bakeData.Settings.StaticOnly ? 8 : 1;
            long totalResultCount = voxelCount * maxHitsPerQuery;

            if (totalResultCount > int.MaxValue) {
                throw new InvalidOperationException(
                    "The total number of physics queries for baking this volume exceeds the maximum possible value." +
                    "Consider splitting the volume up into smaller chunks.");
            }

            Vector3 halfExtents = Vector3.one * (bakeData.Settings.VoxelSize * 0.5f);

            NativeArray<OverlapBoxCommand> queriesBlocking = new((int)voxelCount, Allocator.Persistent);
            NativeArray<ColliderHit> resultsBlocking = new((int)totalResultCount, Allocator.Persistent);

            NativeArray<OverlapBoxCommand> queriesWalkable = new((int)voxelCount, Allocator.Persistent);
            NativeArray<ColliderHit> resultsWalkable = new((int)totalResultCount, Allocator.Persistent);

            GenerateVolumeBoxQueriesJob generateQueriesJobBlocking = new() {
                Queries = queriesBlocking,
                PhysicsScene = physicsScene,
                UsePhysicsScene = usePhysicsScene,
                VolumeTransform = surface.transform.localToWorldMatrix,
                VolumeRotation = surface.transform.rotation,
                Bounds = new NativeBounds(surface.Bounds.center.ToV4Pos(), surface.Bounds.extents.ToV4()),
                VoxelCounts = voxelCounts,
                QueryCountPerAxis = 1,
                DistanceBetweenQueries = 1,
                VoxelSize = bakeData.Settings.VoxelSize,
                HalfExtents = halfExtents,
                QueryLayerMask = bakeData.Settings.BlockingLayers,
                EnableMultiQuery = false,
            };

            GenerateVolumeBoxQueriesJob generateQueriesJobWalkable = generateQueriesJobBlocking;
            generateQueriesJobWalkable.Queries = queriesWalkable;
            generateQueriesJobWalkable.QueryLayerMask = bakeData.Settings.WalkableLayers;

            int batchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores((int)voxelCount);
            JobHandle genQueriesBlockingHandle = generateQueriesJobBlocking.Schedule((int)voxelCount, batchSize);

            // Walkable job waits for blocking job to finish, in order to avoid monopolizing CPU cores.
            JobHandle genQueriesWalkableHandle =
                generateQueriesJobWalkable.Schedule((int)voxelCount, batchSize, genQueriesBlockingHandle);

            JobHandle queryHandleBlocking = OverlapBoxCommand.ScheduleBatch(queriesBlocking, resultsBlocking, batchSize,
                maxHitsPerQuery, genQueriesWalkableHandle);

            JobHandle queryHandleWalkable = OverlapBoxCommand.ScheduleBatch(queriesWalkable, resultsWalkable, batchSize,
                maxHitsPerQuery, queryHandleBlocking);

            PopulateSurfaceVoxelsFromHitsJob populateJob = new() {
                BlockingHitsArray = resultsBlocking,
                WalkableHitsArray = resultsWalkable,
                HitCountPerVoxel = maxHitsPerQuery,
                UseStaticCheck = bakeData.Settings.StaticOnly,
                StaticColliders = bakeData.StaticColliders,
                Voxels = voxels,
            };

            int populateBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores((int)voxelCount);
            await populateJob.Schedule((int)voxelCount, populateBatchSize, queryHandleWalkable);

            queriesBlocking.Dispose();
            queriesWalkable.Dispose();
            resultsBlocking.Dispose();
            resultsWalkable.Dispose();

            // If using start locations, set all open positions that are not contiguous with a start location as blocked.
            if (bakeData.Surface.UseStartLocations) {
                NativeQueue<int3> queue = new(Allocator.TempJob);

                foreach (Vector3 startLocation in bakeData.Surface.StartLocations) {
                    Vector3 posInBounds = startLocation - surface.Bounds.min;
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

                handle2.Complete();
                queue.Dispose();
            }
        }

        private static async UniTask TriangulateVoxels(NavSurfaceBakeData bakeData) {
            NativeList<int4> walkableVertices = new(512, Allocator.Persistent);
            NativeList<int4> blockedAndWalkableVertices = new(512, Allocator.Persistent);
            NativeList<float4> walkableNormals = new(512, Allocator.Persistent);
            NativeList<float4> unusedBlockedNormals = new(512, Allocator.Persistent);

            TriangulateSurfaceJob triangulateWalkableJob = new() {
                Regions = bakeData.Voxels,
                Vertices = walkableVertices,
                Normals = walkableNormals,
                Value = PopulateSurfaceVoxelsFromHitsJob.ResultWalkable,
                AlternateValue = PopulateSurfaceVoxelsFromHitsJob.ResultWalkable,
            };

            TriangulateSurfaceJob triangulateBlockedAndWalkableJob = new() {
                Regions = bakeData.Voxels,
                Vertices = blockedAndWalkableVertices,
                Normals = unusedBlockedNormals,
                Value = PopulateSurfaceVoxelsFromHitsJob.ResultBlocked,
                AlternateValue = PopulateSurfaceVoxelsFromHitsJob.ResultWalkable,
            };

            JobHandle triangulateWalkableHandle = triangulateWalkableJob.Schedule();
            JobHandle triangulateBlockedAndWalkableHandle = triangulateBlockedAndWalkableJob.Schedule();

            SurfaceTriangleMeshToIndicesJob triangleMeshToIndicesJob = new() {
                BlockedAndWalkableVertices = blockedAndWalkableVertices,
                WalkableVertices = walkableVertices,
                InNormals = walkableNormals,
                Mesh = bakeData.MeshInfo,
                VoxelSize = bakeData.Settings.VoxelSize,
                VolumeBounds = new NativeBounds(bakeData.Surface.Bounds.center.ToV4Pos(),
                                                bakeData.Surface.Bounds.extents.ToV4()),
                VoxelCounts = new int3(bakeData.Voxels.SizeX, bakeData.Voxels.SizeY, bakeData.Voxels.SizeZ),
            };

            await triangulateWalkableHandle;
            await triangulateBlockedAndWalkableHandle;

            JobHandle triangleMeshToIndicesHandle = triangleMeshToIndicesJob.Schedule();

            await triangleMeshToIndicesHandle;

            walkableVertices.Dispose();
            blockedAndWalkableVertices.Dispose();
            walkableNormals.Dispose();
            unusedBlockedNormals.Dispose();
        }

        private static void CreateFilterData(ref NavSurfaceBakeData bakeData) {
            bakeData.FilterData = new NavSurfaceFilterData {
                VerticesToCheck = new NativeList<int>(bakeData.MeshInfo.Vertices.Length, Allocator.Persistent),
                TrianglesToCheck = new NativeList<int>(bakeData.MeshInfo.TriangleList.Length / 3, Allocator.Persistent),
                TrianglesToSplit =
                    new NativeHashSet<int>(bakeData.MeshInfo.TriangleList.Length / 3, Allocator.Persistent),
                TrianglesToRemove =
                    new NativeHashSet<int>(bakeData.MeshInfo.TriangleList.Length / 3, Allocator.Persistent),
            };

            ResetFilterData(ref bakeData);
        }

        private static void ResetFilterData(ref NavSurfaceBakeData bakeData) {
            bakeData.FilterData.VerticesToCheck.Clear();
            bakeData.FilterData.TrianglesToCheck.Clear();
            bakeData.FilterData.TrianglesToSplit.Clear();
            bakeData.FilterData.TrianglesToRemove.Clear();

            for (int i = 0; i < bakeData.MeshInfo.Vertices.Length; i++) {
                bakeData.FilterData.VerticesToCheck.Add(i);
            }

            for (int i = 0; i < bakeData.MeshInfo.TriangleList.Length; i += 3) {
                bakeData.FilterData.TrianglesToCheck.Add(i);
            }
        }

        private static async UniTask<BakingNavSurfaceMeshInfo> ShrinkwrapMesh(NavSurfaceBakeData bakeData) {
            NavSurface surface = bakeData.Surface;
            BakingNavSurfaceMeshInfo mesh = bakeData.MeshInfo;

            int resultsPerQuery = bakeData.Settings.StaticOnly ? 8 : 1;

            int vertexCount = bakeData.FilterData.VerticesToCheck.Length;

            using NativeArray<SpherecastCommand> vertexSpherecastCommands =
                new(vertexCount, Allocator.Persistent);

            using NativeArray<RaycastHit> vertexSpherecastResults =
                new(vertexCount * resultsPerQuery, Allocator.Persistent);

            PhysicsScene physicsScene = surface.gameObject.scene.GetPhysicsScene();
            bool usePhysicsScene = physicsScene.IsValid();
            NativeArray<int> verticesToCheckArray = bakeData.FilterData.VerticesToCheck.AsArray();

            GenerateShrinkwrapQueriesJob generateQueriesJob = new() {
                Queries = vertexSpherecastCommands,
                Mesh = mesh,
                PhysicsScene = physicsScene,
                UsePhysicsScene = usePhysicsScene,
                Transform = surface.transform.localToWorldMatrix,
                Bounds = new NativeBounds(surface.Bounds.center.ToV4Pos(), surface.Bounds.extents.ToV4()),
                VoxelSize = bakeData.Settings.VoxelSize,
                QueryLayerMask = bakeData.Settings.WalkableLayers,
                VerticesToShrink = verticesToCheckArray,
            };

            int vertexBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(vertexCount);
            JobHandle generateQueriesHandle = generateQueriesJob.Schedule(vertexCount, vertexBatchSize);
            JobHandle queryHandle = SpherecastCommand.ScheduleBatch(
                vertexSpherecastCommands,
                vertexSpherecastResults,
                vertexBatchSize,
                resultsPerQuery,
                generateQueriesHandle);

            await queryHandle;

            NativeArray<float4> verticesArray = mesh.Vertices.AsArray();

            ApplyShrinkwrapJob shrinkwrapJob = new() {
                Queries = vertexSpherecastCommands,
                Hits = vertexSpherecastResults,
                Vertices = verticesArray,
                InverseTransform = surface.transform.worldToLocalMatrix,
                HitCountPerVertex = resultsPerQuery,
                UseStaticCheck = bakeData.Settings.StaticOnly,
                StaticColliders = bakeData.StaticColliders,
                VerticesToApply = verticesToCheckArray,
                VertexConnections = mesh.VertexConnections.AsArray(),
            };

            await shrinkwrapJob.Schedule(vertexCount, vertexBatchSize);

            NativeList<float4> oldNormals = mesh.TriangleNormals;
            int fullTriCount = mesh.TriangleList.Length / 3;
            mesh.TriangleNormals = new NativeList<float4>(fullTriCount, Allocator.Persistent);
            mesh.TriangleNormals.Length = fullTriCount;

            UpdateNormalsJob updateNormalsJob = new() {
                Vertices = mesh.Vertices,
                TriangleIndices = mesh.TriangleList,
                Normals = mesh.TriangleNormals.AsArray(),
                IgnoreZeroArea = true
            };

            int fullTriBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(fullTriCount);
            await updateNormalsJob.Schedule(fullTriCount, fullTriBatchSize);

            MergeTrianglesByAreaJob mergeTrianglesByAreaJob = new() {
                Mesh = mesh,
                AreaThresholdSqr = 0.0000001f,
                TrianglesToCheck = bakeData.FilterData.TrianglesToCheck,
                VerticesToCheck = bakeData.FilterData.VerticesToCheck,
                CheckOldNormals = true,
                OldNormals = oldNormals.AsArray(),
                MaxMerges = bakeData.FilterData.Iteration == 1
                    ? VisIndex
                    : -1,
            };

            await mergeTrianglesByAreaJob.Schedule();

            // Update normals again after merging.
            updateNormalsJob = new UpdateNormalsJob {
                Vertices = mesh.Vertices,
                TriangleIndices = mesh.TriangleList,
                Normals = mesh.TriangleNormals.AsArray(),
            };

            await updateNormalsJob.Schedule(fullTriCount, fullTriBatchSize);

            oldNormals.Dispose();

            return mesh;
        }

        private static async UniTask<NavSurfaceBakeData> FilterTriangleUprightDirections(
            NavSurfaceBakeData bakeData, NativeCancellationToken cancellationToken) {
            NavSurface surface = bakeData.Surface;
            BakingNavSurfaceMeshInfo mesh = bakeData.MeshInfo;

            int triCount = bakeData.FilterData.TrianglesToCheck.Length;

            PhysicsScene physicsScene = surface.gameObject.scene.GetPhysicsScene();
            bool usePhysicsScene = physicsScene.IsValid();

            int commandCount = triCount * 4 * 4;
            int resultsPerQuery = bakeData.Settings.StaticOnly ? 8 : 1;

            using NativeArray<RaycastCommand> commands = new(commandCount, Allocator.Persistent);
            using NativeArray<RaycastHit> results = new(commandCount * resultsPerQuery, Allocator.Persistent);

            mesh.TriangleUprightWorldDirections.Length = mesh.TriangleList.Length / 3;

            GenerateTriangleGroundCheckQueriesJob job = new() {
                Mesh = mesh,
                Transform = surface.transform.localToWorldMatrix,
                VoxelSize = bakeData.Settings.VoxelSize,
                PhysicsScene = physicsScene,
                UsePhysicsScene = usePhysicsScene,
                QueryLayerMask = bakeData.Settings.WalkableLayers,
                TrianglesToCheck = bakeData.FilterData.TrianglesToCheck.AsArray(),
                Queries = commands,
            };

            int batchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(triCount);
            JobHandle genQueriesHandle = job.Schedule(triCount, batchSize);

            int queryBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(commandCount);
            JobHandle queryHandle = RaycastCommand.ScheduleBatch(
                commands,
                results,
                queryBatchSize,
                resultsPerQuery,
                genQueriesHandle);

            await queryHandle;

            if (bakeData.Settings.UprightDirectionMode == NavSurfaceUprightDirectionMode.Custom) {
                if (bakeData.Settings.CustomUprightDirectionHandler == null) {
                    Debug.LogError("UprightDirectionMode is set to Custom but CustomUprightDirectionHandler is null.");
                    bakeData.MeshInfo = mesh;
                    return bakeData;
                }

                bakeData.MeshInfo = mesh;
                bakeData = await bakeData.Settings.CustomUprightDirectionHandler.CalculateUprightDirections(
                    bakeData, commands, resultsPerQuery, results, cancellationToken);
            } else {
                await ProcessGroundCheckResultsWithFixedMode(bakeData, resultsPerQuery, results);
            }

            bakeData.MeshInfo = mesh;
            return bakeData;
        }

        private static async UniTask ProcessGroundCheckResultsWithFixedMode(
            NavSurfaceBakeData bakeData, int resultsPerQuery, NativeArray<RaycastHit> groundCheckResults) {
            NavSurface surface = bakeData.Surface;
            BakingNavSurfaceMeshInfo mesh = bakeData.MeshInfo;

            float4 fixedUpWorldDir = float4.zero;

            if (bakeData.Settings.UprightDirectionMode == NavSurfaceUprightDirectionMode.FixedWorldDirection) {
                fixedUpWorldDir = new float4(bakeData.Settings.FixedUprightDirection, 0);
            } else if (bakeData.Settings.UprightDirectionMode == NavSurfaceUprightDirectionMode.FixedLocalDirection) {
                fixedUpWorldDir =
                    new float4(surface.transform.TransformDirection(bakeData.Settings.FixedUprightDirection), 0);
            }

            int triCount = bakeData.FilterData.TrianglesToCheck.Length;
            using NativeArray<bool> partiallyFailingTriangles = new(triCount, Allocator.Persistent);
            using NativeArray<bool> completelyFailingTriangles = new(triCount, Allocator.Persistent);

            ProcessGroundCheckResultsJob processJob = new() {
                UprightDirectionMode = bakeData.Settings.UprightDirectionMode,
                ResultsPerQuery = resultsPerQuery,
                MinNormalDotInTriangle =
                    Mathf.Cos(bakeData.Settings.MaxAngleBetweenUpDirectionsWithinTriangle * Mathf.Deg2Rad),
                SlopeAngleLimitCos = Mathf.Cos(bakeData.Settings.SlopeAngleLimit * Mathf.Deg2Rad),
                UseStaticCheck = bakeData.Settings.StaticOnly,
                FixedUpWorldDirection = fixedUpWorldDir,
                StaticColliders = bakeData.StaticColliders,
                GroundCheckResults = groundCheckResults,
                TrianglesToCheck = bakeData.FilterData.TrianglesToCheck.AsArray(),
                TriangleUprightWorldDirections = mesh.TriangleUprightWorldDirections.AsArray(),
                PartiallyFailingTriangles = partiallyFailingTriangles,
                CompletelyFailingTriangles = completelyFailingTriangles,
            };

            int triBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(triCount);
            JobHandle processHandle = processJob.Schedule(triCount, triBatchSize);

            AddFailingTrianglesToSetJob addCompleteToSetJob = new() {
                PartiallyFailingTriangles = partiallyFailingTriangles,
                CompletelyFailingTriangles = completelyFailingTriangles,
                ToRemoveTrianglesSet = bakeData.FilterData.TrianglesToRemove,
                ToSplitTriangleSet = bakeData.FilterData.TrianglesToSplit,
                TrianglesToCheck = bakeData.FilterData.TrianglesToCheck,
                CanSplitTriangles = bakeData.FilterData.Iteration < bakeData.Settings.MaxTriangleDivisions,
            };

            await addCompleteToSetJob.Schedule(processHandle);
        }

        private static async UniTask FilterTriangleCollisions(NavSurfaceBakeData bakeData) {
            NavSurface surface = bakeData.Surface;
            BakingNavSurfaceMeshInfo mesh = bakeData.MeshInfo;

            using NativeArray<float4> vertexAngleOffsets = new(mesh.Vertices.Length, Allocator.Persistent);
            using NativeArray<float4> vertexNormals = new(mesh.Vertices.Length, Allocator.Persistent);
            CalculateVertexAnglesJob angleOffsetJob = new() {
                Mesh = mesh,
                VertexAngleOffsets = vertexAngleOffsets,
                VertexSharedWorldNormals = vertexNormals,
                CollisionRadius = bakeData.Settings.MaxAgentRadius,
                Transform = surface.transform.localToWorldMatrix,
            };

            int vertexCount = mesh.Vertices.Length;
            int vertexBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(vertexCount);
            JobHandle angleOffsetHandle = angleOffsetJob.Schedule(vertexCount, vertexBatchSize);

            PhysicsScene physicsScene = surface.gameObject.scene.GetPhysicsScene();
            bool usePhysicsScene = physicsScene.IsValid();

            int triCount = bakeData.FilterData.TrianglesToCheck.Length;
            int queryCount = triCount * 3;
            using NativeArray<OverlapCapsuleCommand> commands = new(queryCount, Allocator.Persistent);

            GenerateTriangleCollisionCheckQueriesJob job = new() {
                Mesh = mesh,
                Transform = surface.transform.localToWorldMatrix,
                PhysicsScene = physicsScene,
                UsePhysicsScene = usePhysicsScene,
                QueryLayerMask = bakeData.Settings.BlockingLayers,
                Height = bakeData.Settings.MaxAgentHeight,
                Radius = bakeData.Settings.MaxAgentRadius,
                Queries = commands,
                TrianglesToCheck = bakeData.FilterData.TrianglesToCheck,
                VertexAngleOffsets = vertexAngleOffsets,
                VertexCombinedNormals = vertexNormals
            };

            int batchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(triCount);
            JobHandle genQueriesHandle = job.Schedule(triCount, batchSize, angleOffsetHandle);

            int resultsPerQuery = bakeData.Settings.StaticOnly ? 8 : 1;
            using NativeArray<ColliderHit> results = new(queryCount * resultsPerQuery, Allocator.Persistent);

            int queryBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(commands.Length);
            JobHandle queryHandle = OverlapCapsuleCommand.ScheduleBatch(
                commands,
                results,
                queryBatchSize,
                resultsPerQuery,
                genQueriesHandle);

            using NativeArray<bool> partiallyFailingTriangles = new(triCount, Allocator.Persistent);
            using NativeArray<bool> completelyFailingTriangles = new(triCount, Allocator.Persistent);

            ProcessTriangleCollisionResultsJob processJob = new() {
                Hits = results,
                HitCountPerItem = resultsPerQuery * 3,
                UseStaticCheck = bakeData.Settings.StaticOnly,
                StaticColliders = bakeData.StaticColliders,
                PartiallyFailingTriangles = partiallyFailingTriangles,
                CompletelyFailingTriangles = completelyFailingTriangles,
            };

            JobHandle processHandle = processJob.Schedule(triCount, batchSize, queryHandle);

            AddFailingTrianglesToSetJob addCompleteToSetJob = new() {
                PartiallyFailingTriangles = partiallyFailingTriangles,
                CompletelyFailingTriangles = completelyFailingTriangles,
                ToRemoveTrianglesSet = bakeData.FilterData.TrianglesToRemove,
                ToSplitTriangleSet = bakeData.FilterData.TrianglesToSplit,
                TrianglesToCheck = bakeData.FilterData.TrianglesToCheck,
                CanSplitTriangles = bakeData.FilterData.Iteration < bakeData.Settings.MaxTriangleDivisions,
            };

            await addCompleteToSetJob.Schedule(processHandle);
        }

        private static async UniTask RemoveOrSplitFailingTriangles(NavSurfaceBakeData bakeData,
                                                                   int filterIterations) {

            if (bakeData.FilterData.TrianglesToRemove.Count > 0) {
                await RemoveFailingTriangles(bakeData.MeshInfo, bakeData.FilterData.TrianglesToRemove);
            }

            if (bakeData.FilterData.TrianglesToSplit.Count > 0) {
                await SplitFailingTriangles(bakeData.MeshInfo, bakeData.FilterData.TrianglesToSplit,
                    bakeData.FilterData.TrianglesToCheck, bakeData.FilterData.VerticesToCheck);
            }

            bakeData.FilterData.TrianglesToRemove.Clear();
            bakeData.FilterData.TrianglesToSplit.Clear();

            // Rebuild neighbor vertices / triangles.
            RebuildVertexNeighborsJob rebuildJob = new() {
                TriangleIndices = bakeData.MeshInfo.TriangleList.AsArray(),
                VertexTriangles = bakeData.MeshInfo.VertexTriangles.AsArray(),
                VertexConnections = bakeData.MeshInfo.VertexConnections.AsArray(),
            };

            await rebuildJob.Schedule();
        }

        private static async UniTask SplitFailingTriangles(BakingNavSurfaceMeshInfo mesh,
                                                           NativeHashSet<int> trianglesToSplit,
                                                           NativeList<int> trianglesToCheck,
                                                           NativeList<int> verticesToCheck) {
            trianglesToCheck.Clear();
            verticesToCheck.Clear();

            SplitTrianglesJob splitJob = new() {
                Mesh = mesh,
                TrianglesToSplit = trianglesToSplit,
                OutNewTrianglesToTest = trianglesToCheck,
                OutNewVerticesToTest = verticesToCheck,
            };

            await splitJob.Schedule();
        }

        private static async UniTask RemoveFailingTriangles(BakingNavSurfaceMeshInfo mesh,
                                                            NativeHashSet<int> trianglesToRemove) {
            RemoveTrianglesJob removeJob = new() {
                Mesh = mesh,
                TrianglesToRemove = trianglesToRemove
            };

            await removeJob.Schedule();
        }

        private static async UniTask SplitVertexTrianglesOnUpDirection(NavSurfaceBakeData data) {
            BakingNavSurfaceMeshInfo mesh = data.MeshInfo;

            using NativeArray<HybridIntList> vertexGroups = new(mesh.Vertices.Length, Allocator.Persistent);
            using NativeArray<HybridIntList> vertexGroupCounts = new(mesh.Vertices.Length, Allocator.Persistent);

            CalculateSplitVertexGroupsJob calculateJob = new() {
                GroupsForEachVertex = vertexGroups,
                GroupCountsForEachVertex = vertexGroupCounts,
                Mesh = mesh,
                MinDotThreshold = Mathf.Cos(data.Settings.MaxAngleBetweenUpDirectionsBetweenTriangles * Mathf.Deg2Rad),
            };

            int vertexCount = mesh.Vertices.Length;
            int vertexBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(vertexCount);
            await calculateJob.Schedule(vertexCount, vertexBatchSize);

            SplitVertexGroupsJob splitJob = new() {
                GroupsForEachVertex = vertexGroups,
                GroupCountsForEachVertex = vertexGroupCounts,
                TriangleIndices = mesh.TriangleList.AsArray(),
                Vertices = mesh.Vertices,
                VertexTriangles = mesh.VertexTriangles,
                VertexConnections = mesh.VertexConnections,
            };

            await splitJob.Schedule();

            // Rebuild neighbor vertices / triangles.
            RebuildVertexNeighborsJob rebuildJob = new() {
                TriangleIndices = mesh.TriangleList.AsArray(),
                VertexTriangles = mesh.VertexTriangles.AsArray(),
                VertexConnections = mesh.VertexConnections.AsArray(),
            };

            await rebuildJob.Schedule();

            foreach (HybridIntList list in vertexGroups) {
                list.Dispose();
            }

            foreach (HybridIntList list in vertexGroupCounts) {
                list.Dispose();
            }
        }

        private static async UniTask RemoveJaggedTriangles(BakingNavSurfaceMeshInfo mesh) {
            using NativeList<int> validTriangles = new(mesh.TriangleList.Length / 3, Allocator.Persistent);
            CollectValidTrianglesJob collectJob = new() {
                TriangleIndices = mesh.TriangleList,
                ValidTriangles = validTriangles,
            };

            await collectJob.Schedule();

            RemoveJaggedTrianglesJob removeJob = new() {
                ValidTriangles = validTriangles.AsArray(),
                TriangleIndices = mesh.TriangleList.AsArray(),
                VertexTriangles = mesh.VertexTriangles.AsArray(),
            };

            int removeBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(validTriangles.Length);
            await removeJob.Schedule(validTriangles.Length, removeBatchSize);

            // Rebuild neighbor vertices / triangles.
            RebuildVertexNeighborsJob rebuildJob = new() {
                TriangleIndices = mesh.TriangleList.AsArray(),
                VertexTriangles = mesh.VertexTriangles.AsArray(),
                VertexConnections = mesh.VertexConnections.AsArray(),
            };

            await rebuildJob.Schedule();
        }

        private static async UniTask DecimateSurfaceTriangles(NavSurfaceBakeData bakeData,
                                                              NativeCancellationToken cancellationToken) {

            BakingNavSurfaceMeshInfo mesh = bakeData.MeshInfo;
            int vertexCount = mesh.Vertices.Length;

            // Calculate islands of connected vertices.

            using NativeArray<int> islandIndices = new(vertexCount, Allocator.Persistent);
            using NativeList<int> vertexCountsPerIsland = new(16, Allocator.Persistent);

            CalculateMeshIslandsJob calculateIslandsJob = new() {
                VertexConnections = mesh.VertexConnections.AsArray(),
                IslandIndices = islandIndices,
                VertexCountsPerIsland = vertexCountsPerIsland,
            };

            await calculateIslandsJob.Schedule();

            int islandCount = vertexCountsPerIsland.Length;
            int islandBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(islandCount);

            if (bakeData.Settings.MinIslandSurfaceArea > 0) {
                FilterIslandMinAreaJob filterIslandMinAreaJob = new() {
                    IslandIndices = islandIndices,
                    Vertices = mesh.Vertices.AsArray(),
                    TriangleIndices = mesh.TriangleList.AsArray(),
                    VertexTriangles = mesh.VertexTriangles.AsArray(),
                    VertexConnections = mesh.VertexConnections.AsArray(),
                    MinArea = bakeData.Settings.MinIslandSurfaceArea,
                };

                await filterIslandMinAreaJob.Schedule(islandCount, islandBatchSize);
            }

            // Calculate the occurrence map (for each edge, which triangles contain it).
            using NativeArray<UnsafeHashMap<Edge, int2>> edgeOccurrenceMaps = new(islandCount, Allocator.Persistent);

            CollectEdgeOccurrenceMapsJob edgeOccurrenceMapsJob = new() {
                EdgeOccurrenceMaps = edgeOccurrenceMaps,
                TriangleIndexList = mesh.TriangleList.AsArray(),
                Vertices = mesh.Vertices.AsArray(),
                CancellationToken = cancellationToken,
                IslandIndices = islandIndices,
                VertexCountsPerIsland = vertexCountsPerIsland.AsArray(),
            };

            JobHandle edgeOccurrenceMapHandle = edgeOccurrenceMapsJob.Schedule(islandCount, islandBatchSize);

            // Calculate edge collapse error matrices.
            int vertexBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(vertexCount);

            using NativeArray<float4x4> errorMatrices = new(vertexCount, Allocator.Persistent);
            using NativeArray<HybridIntList> verticesOnEdge = new(vertexCount, Allocator.Persistent);

            CalculateEdgeCollapseErrorMatricesJob errorMatricesJob = new() {
                Mesh = mesh,
                ErrorMatrices = errorMatrices,
                VerticesOnEdge = verticesOnEdge,
                IslandIndices = islandIndices,
                EdgeOccurrenceMaps = edgeOccurrenceMaps,
            };

            JobHandle errorMatricesHandle =
                errorMatricesJob.Schedule(vertexCount, vertexBatchSize, edgeOccurrenceMapHandle);

            await errorMatricesHandle;

            // Collect edges that can be collapsed and calculate the cost of each.
            using NativeArray<UnsafeList<CollapsibleEdgePair>> islandCollapsibleEdgeLists =
                new(islandCount, Allocator.Persistent);

            float boundThreshold = bakeData.Settings.BoundaryTriangleClippingThreshold * bakeData.Settings.VoxelSize;
            float decimationThreshold = bakeData.Settings.DecimationThreshold * bakeData.Settings.VoxelSize;
            decimationThreshold *= decimationThreshold;

            CollectEdgesJob collectEdgesJob = new() {
                Mesh = mesh,
                IslandCollapsibleEdgeLists = islandCollapsibleEdgeLists,
                IslandIndices = islandIndices,
                CancellationToken = cancellationToken
            };

            JobHandle collectEdgesHandle = collectEdgesJob.Schedule(islandCount, islandBatchSize, errorMatricesHandle);

            // Collapse edges.
            CollapseEdgesJob collapseEdgesJob = new() {
                IslandCollapsibleEdgeLists = islandCollapsibleEdgeLists,
                VertexCountsPerIsland = vertexCountsPerIsland.AsArray(),
                Mesh = mesh,
                ErrorMatrices = errorMatrices,
                DecimationThreshold = decimationThreshold,
                BoundaryVertexDisplacementThreshold = boundThreshold,
                IslandIndices = islandIndices,
                VerticesOnEdge = verticesOnEdge,
                EdgeOccurrenceMaps = edgeOccurrenceMaps,
                CancellationToken = cancellationToken
            };

            await collapseEdgesJob.Schedule(islandCount, islandBatchSize, collectEdgesHandle);

            for (int i = 0; i < verticesOnEdge.Length; i++) {
                verticesOnEdge[i].Dispose();
            }

            for (int i = 0; i < islandCollapsibleEdgeLists.Length; i++) {
                islandCollapsibleEdgeLists[i].Dispose();
            }

            for (int i = 0; i < edgeOccurrenceMaps.Length; i++) {
                edgeOccurrenceMaps[i].Dispose();
            }
        }

        private static async UniTask PruneSmallTriangles(NavSurfaceBakeData data,
                                                         NativeCancellationToken cancellationToken) {
            BakingNavSurfaceMeshInfo mesh = data.MeshInfo;

            int vertexCount = mesh.Vertices.Length;

            using NativeArray<int> islandIndices = new(vertexCount, Allocator.Persistent);
            using NativeList<int> vertexCountsPerIsland = new(16, Allocator.Persistent);

            CalculateMeshIslandsJob calculateIslandsJob = new() {
                VertexConnections = mesh.VertexConnections.AsArray(),
                IslandIndices = islandIndices,
                VertexCountsPerIsland = vertexCountsPerIsland,
            };

            await calculateIslandsJob.Schedule();

            int islandCount = vertexCountsPerIsland.Length;
            int islandBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(islandCount);

            using NativeArray<UnsafeHashMap<Edge, int2>> edgeOccurrenceMaps = new(islandCount, Allocator.Persistent);

            CollectEdgeOccurrenceMapsJob edgeOccurrenceMapsJob = new() {
                EdgeOccurrenceMaps = edgeOccurrenceMaps,
                TriangleIndexList = mesh.TriangleList.AsArray(),
                Vertices = mesh.Vertices.AsArray(),
                CancellationToken = cancellationToken,
                IslandIndices = islandIndices,
                VertexCountsPerIsland = vertexCountsPerIsland.AsArray(),
            };

            await edgeOccurrenceMapsJob.Schedule(islandCount, islandBatchSize);

            float areaThreshold =
                data.Settings.MinTriangleAreaFraction * data.Settings.VoxelSize * data.Settings.VoxelSize;

            PruneRedundantTrianglesJob pruneJob = new() {
                Mesh = data.MeshInfo,
                IslandIndices = islandIndices,
                EdgeOccurrenceMaps = edgeOccurrenceMaps,
                CancellationToken = cancellationToken,
                AreaThreshold = areaThreshold,
            };

            await pruneJob.Schedule(islandCount, islandBatchSize);

            for (int i = 0; i < edgeOccurrenceMaps.Length; i++) {
                edgeOccurrenceMaps[i].Dispose();
            }
        }

        private static async UniTask ErodeEdges(NavSurfaceBakeData data, NativeCancellationToken cancellationToken) {
            if (data.Settings.ErosionDistance <= 0) return;

            BakingNavSurfaceMeshInfo mesh = data.MeshInfo;

            int vertexCount = mesh.Vertices.Length;
            int vertexBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(vertexCount);

            using NativeArray<int> islandIndices = new(vertexCount, Allocator.Persistent);
            using NativeList<int> vertexCountsPerIsland = new(16, Allocator.Persistent);

            RebuildVertexNeighborsJob test = new() {
                TriangleIndices = mesh.TriangleList.AsArray(),
                VertexTriangles = mesh.VertexTriangles.AsArray(),
                VertexConnections = mesh.VertexConnections.AsArray(),
            };

            await test.Schedule();

            CalculateMeshIslandsJob calculateIslandsJob = new() {
                VertexConnections = mesh.VertexConnections.AsArray(),
                IslandIndices = islandIndices,
                VertexCountsPerIsland = vertexCountsPerIsland,
            };

            await calculateIslandsJob.Schedule();

            int islandCount = vertexCountsPerIsland.Length;
            int islandBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(islandCount);

            using NativeArray<UnsafeHashMap<Edge, int2>> edgeOccurrenceMaps = new(islandCount, Allocator.Persistent);

            CollectEdgeOccurrenceMapsJob edgeOccurrenceMapsJob = new() {
                EdgeOccurrenceMaps = edgeOccurrenceMaps,
                TriangleIndexList = mesh.TriangleList.AsArray(),
                Vertices = mesh.Vertices.AsArray(),
                CancellationToken = cancellationToken,
                IslandIndices = islandIndices,
                VertexCountsPerIsland = vertexCountsPerIsland.AsArray(),
            };

            JobHandle occurrenceMapHandle = edgeOccurrenceMapsJob.Schedule(islandCount, islandBatchSize);

            using NativeList<int3> edgeVertices = new(mesh.Vertices.Length, Allocator.Persistent);
            edgeVertices.Resize(mesh.Vertices.Length, NativeArrayOptions.UninitializedMemory);

            GetEdgeVerticesJob edgeVerticesJob = new() {
                IslandIndices = islandIndices,
                EdgeOccurrenceMaps = edgeOccurrenceMaps,
                Mesh = mesh,
                EdgeVertices = edgeVertices.AsArray()
            };

            JobHandle edgeVerticesHandle = edgeVerticesJob.Schedule(vertexCount, vertexBatchSize, occurrenceMapHandle);

            RemoveNegativeElementsJobs.Int3 filterJob = new() {
                List = edgeVertices
            };

            await filterJob.Schedule(edgeVerticesHandle);

            using NativeArray<float4> edgeVectors = new(edgeVertices.Length, Allocator.Persistent);

            const int raycastCountPerVertex = 8;
            int edgeVertexCount = edgeVertices.Length;
            int raycastCount = edgeVertexCount * raycastCountPerVertex;

            int edgeVertexBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(edgeVertexCount);
            int resultsPerQuery = data.Settings.StaticOnly ? 8 : 1;
            int resultsCount = raycastCount * resultsPerQuery;

            using NativeArray<RaycastCommand> raycastCommands = new(raycastCount, Allocator.Persistent);
            using NativeArray<RaycastHit> raycastResults = new(resultsCount, Allocator.Persistent);

            using NativeArray<float4> outwardDirections = new(edgeVertexCount, Allocator.Persistent);

            PhysicsScene physicsScene = data.Surface.gameObject.scene.GetPhysicsScene();
            bool usePhysicsScene = physicsScene.IsValid();

            NativeArray<float4> verticesTemp = mesh.Vertices.AsArray();

            GenerateEdgeVertexErosionCheckQueriesJob genQueriesJob = new() {
                EdgeVertices = edgeVertices.AsArray(),
                Mesh = mesh,
                IslandIndices = islandIndices,
                EdgeOccurrenceMaps = edgeOccurrenceMaps,
                EdgeVertexOutwardDirections = outwardDirections,
                Queries = raycastCommands,
                ErosionDistance = data.Settings.ErosionDistance,
                RaycastCountPerVertex = raycastCountPerVertex,
                ErosionCheckRange = data.Settings.VoxelSize * 0.1f,
                Transform = data.Surface.Transform.localToWorldMatrix,
                PhysicsScene = physicsScene,
                UsePhysicsScene = usePhysicsScene,
                QueryLayerMask = data.Settings.WalkableLayers,
            };

            JobHandle genQueriesHandle =
                genQueriesJob.Schedule(edgeVertexCount, edgeVertexBatchSize, occurrenceMapHandle);

            int queryBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(raycastCount);
            JobHandle queryHandle = RaycastCommand.ScheduleBatch(
                raycastCommands,
                raycastResults,
                queryBatchSize,
                resultsPerQuery,
                genQueriesHandle);

            ApplyErosionJob applyJob = new() {
                EdgeVertices = edgeVertices.AsArray(),
                Vertices = verticesTemp,
                VertexOutwardDirections = outwardDirections,
                Hits = raycastResults,
                ErosionDistance = data.Settings.ErosionDistance,
                RaycastCountPerVertex = raycastCountPerVertex,
                ResultsPerQuery = resultsPerQuery,
                UseStaticCheck = data.Settings.StaticOnly,
                StaticColliders = data.StaticColliders,
            };

            await applyJob.Schedule(edgeVertexCount, edgeVertexBatchSize, queryHandle);

            foreach (UnsafeHashMap<Edge, int2> map in edgeOccurrenceMaps) {
                map.Dispose();
            }
        }

        private static async UniTask<BakingNavSurfaceMeshInfo> CreateGroups(BakingNavSurfaceMeshInfo mesh,
                                                                            NavSurface surface) {
            // 0. Rebuild indices.
            BakingNavSurfaceMeshInfo newMesh = new(Allocator.Persistent);

            RebuildSurfaceMeshJob rebuildSurfaceMeshJob = new() {
                OldMesh = mesh,
                NewMesh = newMesh,
            };

            await rebuildSurfaceMeshJob.Schedule();

            mesh.Dispose();
            mesh = newMesh;

            mesh.VertexConnections.Length = mesh.Vertices.Length;
            mesh.VertexTriangles.Length = mesh.Vertices.Length;

            // 1. Rebuild neighbor vertices / triangles.
            RebuildVertexNeighborsJob rebuildJob = new() {
                TriangleIndices = mesh.TriangleList.AsArray(),
                VertexTriangles = mesh.VertexTriangles.AsArray(),
                VertexConnections = mesh.VertexConnections.AsArray(),
            };

            await rebuildJob.Schedule();

            // 2. Calculate islands of connected vertices.
            using NativeArray<int> islandIndices = new(mesh.Vertices.Length, Allocator.Persistent);
            using NativeList<int> vertexCountsPerIsland = new(16, Allocator.Persistent);

            CalculateMeshIslandsJob calculateIslandsJob = new() {
                VertexConnections = mesh.VertexConnections.AsArray(),
                IslandIndices = islandIndices,
                VertexCountsPerIsland = vertexCountsPerIsland,
            };

            await calculateIslandsJob.Schedule();

            // 3. Create groups.
            mesh.TriangleGroupIDs.Length = mesh.TriangleList.Length / 3;
            int islandCount = vertexCountsPerIsland.Length;

            using NativeArray<UnsafeList<HybridIntList>> groupTriangles = new(islandCount, Allocator.Persistent);

            GroupTrianglesJob groupJob = new() {
                Mesh = mesh,
                IslandIndices = islandIndices,
                GroupTriangles = groupTriangles,
                MinNormalDot = 0.9f,
            };

            int batchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(islandCount);
            await groupJob.Schedule(islandCount, batchSize);

            // 4. Process groups into single list.
            ProcessGroupsJob processGroupsJob = new() {
                InputGroupTrianglesPerIsland = groupTriangles,
                OutputGroupTriangles = mesh.GroupTriangles,
                OutputGroupIDs = mesh.TriangleGroupIDs.AsArray(),
            };

            await processGroupsJob.Schedule();

            foreach (UnsafeList<HybridIntList> groupsForIsland in groupTriangles) {
                groupsForIsland.Dispose();
            }

            return mesh;
        }

        private static async UniTask PopulateData(NavSurfaceBakeData data, bool updateSerializedData) {
            BakingNavSurfaceMeshInfo mesh = data.MeshInfo;

            // Recalculate islands
            using NativeArray<int> islandIndices = new(mesh.Vertices.Length, Allocator.Persistent);
            using NativeList<int> vertexCountsPerIsland = new(16, Allocator.Persistent);

            CalculateMeshIslandsJob islandsJob = new() {
                VertexConnections = mesh.VertexConnections.AsArray(),
                IslandIndices = islandIndices,
                VertexCountsPerIsland = vertexCountsPerIsland,
            };

            JobHandle islandsHandle = islandsJob.Schedule();

            // Calculate vertex region membership
            using NativeArray<HybridIntList> vertexRegionMembership = new(mesh.Vertices.Length, Allocator.Persistent);

            CalculateVertexGroupMembershipJob membershipJob = new() {
                Mesh = mesh,
                VertexRegionMembership = vertexRegionMembership,
            };

            JobHandle membershipHandle = membershipJob.Schedule();

            // Calculate internal links
            int regionCount = mesh.GroupTriangles.Length;
            using NativeArray<CalculateSurfaceInternalLinksJob.RegionOutput> regionLinks =
                new(regionCount, Allocator.Persistent);

            CalculateSurfaceInternalLinksJob internalLinksJob = new() {
                Mesh = mesh,
                VertexRegionMembership = vertexRegionMembership,
                OutputData = regionLinks,
            };

            int regionBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(regionCount);
            JobHandle internalLinksHandle = internalLinksJob.Schedule(regionCount, regionBatchSize, membershipHandle);

            // Build data
            using NativeReference<NativeNavSurfaceData> dataRef = new(Allocator.Persistent);
            BuildSurfaceDataJob buildJob = new() {
                Mesh = mesh,
                LinkData = regionLinks,
                VertexIslandIndices = islandIndices,
                OutputData = dataRef,
                InverseTransform = data.Surface.transform.worldToLocalMatrix,
            };

            JobHandle dependency = JobHandle.CombineDependencies(islandsHandle, internalLinksHandle);
            await buildJob.Schedule(dependency);

            NativeNavSurfaceData nativeSurfaceData = dataRef.Value;

            for (int i = 0; i < regionCount; i++) {
                regionLinks[i].InternalLinks.Dispose();
                regionLinks[i].InternalLinkVertices.Dispose();
                regionLinks[i].InternalLinkEdges.Dispose();
            }

            nativeSurfaceData = new NativeNavSurfaceData(
                id: data.Surface.InstanceID,
                transform: data.Surface.transform.localToWorldMatrix,
                inverseTransform: data.Surface.transform.worldToLocalMatrix,
                bounds: new NativeBounds(data.Surface.Bounds.center.ToV4Pos(), data.Surface.Bounds.extents.ToV4()),
                layer:data.Surface.Layer,
                vertices: nativeSurfaceData.Vertices,
                regions: nativeSurfaceData.Regions,
                triangleIndices: nativeSurfaceData.TriangleIndices,
                internalLinks: nativeSurfaceData.InternalLinks,
                externalLinks: nativeSurfaceData.ExternalLinks,
                linkVertices: nativeSurfaceData.LinkVertices,
                linkEdges: nativeSurfaceData.LinkEdges);

            NativeNavSurfaceDataPointers pointers = data.Surface.DataStructurePointers;
            NativeNavSurfaceData oldData = data.Surface.NativeData;

            data.Surface.UpdateNativeData(nativeSurfaceData, default, updateSerializedData, updateSerializedData);
            oldData.Dispose();
            pointers.Dispose();
        }
    }
}
