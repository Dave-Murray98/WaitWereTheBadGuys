// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs;
using Infohazard.HyperNav.Jobs.Baking;
using Infohazard.HyperNav.Jobs.Utility;
using Infohazard.HyperNav.Settings;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Infohazard.HyperNav {
    /// <summary>
    /// Utility for updating external links for nav areas.
    /// </summary>
    public static class NavAreaExternalLinkUpdate {
        private static readonly HashSet<JobHandle> ActiveLinkJobs = new();

        static NavAreaExternalLinkUpdate() {
            ChangeNavAreaData.DataChanging += ChangeNavDataDataChanging;
        }

        private static void ChangeNavDataDataChanging() {
            foreach (JobHandle handle in ActiveLinkJobs) {
                handle.Complete();
            }

            ActiveLinkJobs.Clear();
        }

        /// <summary>
        /// Generate external links for the given areas. Can be used at runtime to support dynamically created links.
        /// </summary>
        /// <param name="areas"> Areas to generate external links for.
        /// Regardless of this parameter, all loaded areas will be considered as destinations for external links. </param>
        /// <param name="updateSerializedData"> Whether to update serialized data for the areas.
        /// If this is false, GC allocations will be avoided, but the data will not be saved.</param>
        /// <param name="keepLinksToUnloadedScenes">If true, links to areas in unloaded scenes will be kept.</param>
        /// <param name="maxCompletionFrames">Maximum number of frames to wait for job completion. -1 for no limit.</param>
        /// <exception cref="OperationCanceledException">Thrown if the job was cancelled due to data changing.</exception>
        public static async UniTask GenerateExternalLinks(IReadOnlyList<INavArea> areas, bool updateSerializedData,
                                                          bool keepLinksToUnloadedScenes, int maxCompletionFrames = 3) {
            Allocator allocator = maxCompletionFrames is < 0 or > 3
                ? Allocator.Persistent
                : Allocator.TempJob;

            NativeArray<long> inputs = new(areas.Count, allocator);
            NativeArray<UnsafeList<NativeNavExternalLinkData>> results = new(areas.Count, allocator);
            NativeArray<UnsafeArray<SerializableRange>> rangeResults = new(areas.Count, allocator);

#if UNITY_EDITOR
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
#endif

            for (int i = 0; i < areas.Count; i++) {
                INavArea area = areas[i];

#if UNITY_EDITOR
                if (prefabStage == null && PrefabStageUtility.GetPrefabStage(area.Transform.gameObject) != null) {
                    Debug.LogWarning(
                        $"Area {area.Transform.name} is in a prefab stage. " +
                        "External links will not be generated for this area.");
                    inputs[i] = -1;
                    continue;
                } else if (prefabStage != null && area.Transform.gameObject.scene != prefabStage.scene) {
                    Debug.LogWarning(
                        $"Area {area.Transform.name} is in a scene that is not the current prefab stage. " +
                        "External links will not be generated for this area.");
                    inputs[i] = -1;
                    continue;
                }
#endif
                inputs[i] = area.InstanceID;

                rangeResults[i] =
                    new UnsafeArray<SerializableRange>(area.Data.RegionCount, allocator);

                results[i] =
                    new UnsafeList<NativeNavExternalLinkData>(area.Data.RegionCount, allocator);
            }

            NativeParallelHashMap<long, NativeNavVolumeData> volumes = NavVolume.NativeDataMap;
            NativeParallelHashMap<long, NativeNavSurfaceData> surfaces = NavSurface.NativeDataMap;

            IReadOnlyDictionary<long, NavVolume> managedVolumes = NavVolume.Instances;
            IReadOnlyDictionary<long, NavSurface> managedSurfaces = NavSurface.Instances;

            bool allocatedVolumes = false;
            bool allocatedSurfaces = false;

#if UNITY_EDITOR
            if (prefabStage != null) {
                Dictionary<long, NavVolume> tempVolumes = new();
                Dictionary<long, NavSurface> tempSurfaces = new();

                foreach (NavAreaBase area in prefabStage.prefabContentsRoot.GetComponentsInChildren<NavAreaBase>()) {
                    area.Register();
                    if (!area.IsNativeDataCreated) continue;

                    if (area is NavVolume volume) {
                        tempVolumes[volume.InstanceID] = volume;
                    } else if (area is NavSurface surface) {
                        tempSurfaces[surface.InstanceID] = surface;
                    }
                }

                managedVolumes = tempVolumes;
                managedSurfaces = tempSurfaces;

                volumes = new NativeParallelHashMap<long, NativeNavVolumeData>(tempVolumes.Count, allocator);
                surfaces = new NativeParallelHashMap<long, NativeNavSurfaceData>(tempSurfaces.Count, allocator);
                allocatedVolumes = true;
                allocatedSurfaces = true;

                foreach (NavVolume volume in tempVolumes.Values) {
                    volumes[volume.InstanceID] = volume.NativeData;
                }

                foreach (NavSurface surface in tempSurfaces.Values) {
                    surfaces[surface.InstanceID] = surface.NativeData;
                }
            }
#endif

            // If the data maps are not created, create temporary ones.
            // Otherwise, we'll get an error when scheduling the job.
            if (!volumes.IsCreated) {
                volumes = new NativeParallelHashMap<long, NativeNavVolumeData>(0, allocator);
                allocatedVolumes = true;
            }

            if (!surfaces.IsCreated) {
                surfaces = new NativeParallelHashMap<long, NativeNavSurfaceData>(0, allocator);
                allocatedSurfaces = true;
            }

            NativeParallelHashMap<long, ExternalLinkBakeJob.AreaMetadata> metadata =
                new(managedVolumes.Count + managedSurfaces.Count, allocator);

            foreach (NavVolume volume in managedVolumes.Values) {
                NavVolumeSettings settings = volume.Settings;
                metadata[volume.InstanceID] = new ExternalLinkBakeJob.AreaMetadata {
                    AreaType = NavAreaTypes.Volume,
                    MaxExternalLinkDistanceToVolume = settings.MaxExternalLinkDistanceToVolume,
                    MaxExternalLinkDistanceToSurface = settings.MaxExternalLinkDistanceToSurface,
                    ExternalLinkTargetLayers = settings.ExternalLinkTargetLayers,
                };
            }

            foreach (NavSurface surface in managedSurfaces.Values) {
                NavSurfaceSettings settings = surface.Settings;
                metadata[surface.InstanceID] = new ExternalLinkBakeJob.AreaMetadata {
                    AreaType = NavAreaTypes.Surface,
                    MaxExternalLinkDistanceToVolume = settings.MaxExternalLinkDistanceToVolume,
                    MaxExternalLinkDistanceToSurface = settings.MaxExternalLinkDistanceToSurface,
                    ExternalLinkTargetLayers = settings.ExternalLinkTargetLayers,
                    MaxSurfaceAngleDifferenceCos =
                        Mathf.Cos(Mathf.Deg2Rad * surface.Settings.MaxAngleBetweenUpDirectionsBetweenTriangles),
                    MaxSurfaceVolumeApproachAngleCosTan = new float2(
                        Mathf.Cos(Mathf.Deg2Rad * surface.Settings.MaxSurfaceVolumeApproachAngle),
                        Mathf.Tan(Mathf.Deg2Rad * surface.Settings.MaxSurfaceVolumeApproachAngle)),
                    AgentHeight = surface.Settings.MaxAgentHeight,
                };
            }


            ExternalLinkBakeJob job = new() {
                Volumes = volumes,
                Surfaces = surfaces,
                AreaInputs = inputs,
                LinkResults = results,
                LinkRangeResults = rangeResults,
                AreaMetadataMap = metadata
            };

            int threadCount = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(areas.Count);
            JobHandle handle = job.Schedule(areas.Count, threadCount);
            ActiveLinkJobs.Add(handle);

            for (int i = 0; maxCompletionFrames < 0 || i < maxCompletionFrames; i++) {
                await UniTask.Yield();
                if (handle.IsCompleted) break;
            }

            // If job was already removed from ActiveJobs, that means it was cancelled.
            try {
                if (ActiveLinkJobs.Remove(handle)) {
                    handle.Complete();
                    FinalizeExternalLinkBakeJob(areas, job, updateSerializedData, keepLinksToUnloadedScenes);
                } else {
                    throw new OperationCanceledException();
                }
            } finally {
                DisposeExternalLinkBakeJob(job);

                if (allocatedVolumes) {
                    volumes.Dispose();
                }

                if (allocatedSurfaces) {
                    surfaces.Dispose();
                }

                metadata.Dispose();
            }
        }

        private static void FinalizeExternalLinkBakeJob(
            IReadOnlyList<INavArea> areas, ExternalLinkBakeJob job, bool updateSerializedData,
            bool keepLinksToUnloadedScenes) {
            UnsafeArray<UnsafeArray<UnsafeList<NativeNavExternalLinkData>>> manualLinks = GetManualExternalLinks(job);

            using ChangeNavAreaData change = ChangeNavAreaData.Instance();

            for (int i = 0; i < job.AreaInputs.Length; i++) {
                if (job.AreaInputs[i] == -1) continue;
                INavArea area = areas[i];

                UnsafeList<NativeNavExternalLinkData> newLinksFromJob = job.LinkResults[i];

                // Update the regions with the new link ranges.
                UnsafeArray<SerializableRange> newLinkRanges = job.LinkRangeResults[i];
                UnsafeArray<UnsafeList<NativeNavExternalLinkData>> manualLinksForArea = manualLinks[i];

                area.UpdateNativeExternalLinks(updateSerializedData, keepLinksToUnloadedScenes, newLinksFromJob,
                    newLinkRanges, manualLinksForArea);
            }
        }

        private static UnsafeArray<UnsafeArray<UnsafeList<NativeNavExternalLinkData>>> GetManualExternalLinks(
            in ExternalLinkBakeJob job) {
            // Sample positions of manual links to determine which volumes and regions they are in.
            IReadOnlyList<ManualNavLink> manualLinks = ManualNavLink.AllLinks;
            Span<NavSampleQuery> queries = stackalloc NavSampleQuery[manualLinks.Count * 2];
            Span<NavSampleResult> results = stackalloc NavSampleResult[manualLinks.Count * 2];

            for (int i = 0; i < manualLinks.Count; i++) {
                ManualNavLink link = manualLinks[i];
                queries[i * 2] = new NavSampleQuery(link.WorldStartPoint, link.SampleRadius, link.StartTypes);
                queries[i * 2 + 1] = new NavSampleQuery(link.WorldEndPoint, link.SampleRadius, link.EndTypes);
            }

            NavSampleJob.SamplePositionsInAllAreas(queries, results);

            // Build up map of volume index -> region index -> manual links.
            UnsafeArray<UnsafeArray<UnsafeList<NativeNavExternalLinkData>>> manualLinkData =
                new(job.AreaInputs.Length, Allocator.Temp);

            for (int i = 0; i < manualLinks.Count; i++) {
                ManualNavLink link = manualLinks[i];
                NavSampleResult startResult = results[i * 2];
                NavSampleResult endResult = results[i * 2 + 1];

                if (startResult.AreaID == 0 || endResult.AreaID == 0) continue;

                AddExternalLink(ref manualLinkData, job, startResult, endResult, link.InstanceID);

                if (link.IsBidirectional) {
                    AddExternalLink(ref manualLinkData, job, endResult, startResult, link.InstanceID);
                }
            }

            return manualLinkData;
        }

        private static void AddExternalLink(
            ref UnsafeArray<UnsafeArray<UnsafeList<NativeNavExternalLinkData>>> manualLinkData,
            in ExternalLinkBakeJob job, NavSampleResult fromHit, NavSampleResult toHit, long linkId) {

            float4x4 inverseTransform;
            int regionCount;

            if (NavVolume.Instances.TryGetValue(fromHit.AreaID, out NavVolume fromVolume)) {
                inverseTransform = fromVolume.NativeData.InverseTransform;
                regionCount = fromVolume.NativeData.Regions.Length;
            } else if (NavSurface.Instances.TryGetValue(fromHit.AreaID, out NavSurface fromSurface)) {
                inverseTransform = fromSurface.NativeData.InverseTransform;
                regionCount = fromSurface.NativeData.Regions.Length;
            } else {
                return;
            }

            int volumeIndex = -1;
            for (int j = 0; j < job.AreaInputs.Length; j++) {
                if (job.AreaInputs[j] != fromHit.AreaID) continue;
                volumeIndex = j;
                break;
            }

            if (volumeIndex == -1) return;
            ref UnsafeArray<UnsafeList<NativeNavExternalLinkData>> linksForVolume =
                ref manualLinkData[volumeIndex];

            if (linksForVolume.IsNull) {
                linksForVolume = new UnsafeArray<UnsafeList<NativeNavExternalLinkData>>(regionCount, Allocator.Temp);
            }

            ref UnsafeList<NativeNavExternalLinkData> linksForRegion = ref linksForVolume[fromHit.Region];
            if (!linksForRegion.IsCreated) {
                linksForRegion = new UnsafeList<NativeNavExternalLinkData>(4, Allocator.Temp);
            }

            float4 fromPositionLocal = math.mul(inverseTransform, fromHit.Position);
            float4 toPositionLocal = math.mul(inverseTransform, toHit.Position);
            linksForRegion.Add(new NativeNavExternalLinkData(toHit.AreaID, toHit.Type, toHit.Region, fromPositionLocal,
                toPositionLocal, linkId));
        }

        private static void DisposeExternalLinkBakeJob(ExternalLinkBakeJob job) {
            for (int i = 0; i < job.LinkResults.Length; i++) {
                job.LinkResults[i].Dispose();
            }

            for (int i = 0; i < job.LinkRangeResults.Length; i++) {
                job.LinkRangeResults[i].Dispose();
            }

            job.AreaInputs.Dispose();
            job.LinkResults.Dispose();
            job.LinkRangeResults.Dispose();
        }
    }
}
