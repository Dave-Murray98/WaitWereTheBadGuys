// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking {
    [BurstCompile]
    public struct ExternalLinkBakeJob : IJobParallelFor {
        public struct AreaMetadata {
            public NavAreaTypes AreaType;
            public float MaxExternalLinkDistanceToVolume;
            public float MaxExternalLinkDistanceToSurface;
            public NavLayerMask ExternalLinkTargetLayers;
            public float MaxSurfaceAngleDifferenceCos;
            public float2 MaxSurfaceVolumeApproachAngleCosTan;
            public float AgentHeight;
        }

        /// <summary>
        /// Map containing all loaded NavVolumes, keyed by their instance ID.
        /// </summary>
        [ReadOnly]
        public NativeParallelHashMap<long, NativeNavVolumeData> Volumes;

        /// <summary>
        /// Map containing all loaded NavSurfaces, keyed by their instance ID.
        /// </summary>
        [ReadOnly]
        public NativeParallelHashMap<long, NativeNavSurfaceData> Surfaces;

        /// <summary>
        /// Map containing metadata for link baking not included in the normal data structures.
        /// </summary>
        [ReadOnly]
        public NativeParallelHashMap<long, AreaMetadata> AreaMetadataMap;

        /// <summary>
        /// IDs in the <see cref="Volumes"/> and <see cref="Surfaces"/> dictionaries to update.
        /// </summary>
        [ReadOnly]
        public NativeArray<long> AreaInputs;

        // Output
        // Note: inner lists must be allocated and disposed by caller.
        public NativeArray<UnsafeList<NativeNavExternalLinkData>> LinkResults;

        // Note: inner arrays must be allocated and disposed by caller.
        public NativeArray<UnsafeArray<SerializableRange>> LinkRangeResults;

        public void Execute(int index) {
            long areaID = AreaInputs[index];
            if (areaID < 0) return;

            if (!AreaMetadataMap.TryGetValue(areaID, out AreaMetadata meta)) {
                Debug.LogError($"Metadata not found for input area {areaID}.");
                return;
            }

            float maxExternalLinkDistanceToVolume = meta.MaxExternalLinkDistanceToVolume;
            float maxExternalLinkDistanceToSurface = meta.MaxExternalLinkDistanceToSurface;
            float maxSurfaceAngleDifferenceCos = meta.MaxSurfaceAngleDifferenceCos;

            float4x4 areaTransform;
            float4x4 areaInverseTransform;
            int regionCount;
            NativeNavVolumeData volumeData = default;
            NativeNavSurfaceData surfaceData = default;

            if (meta.AreaType == NavAreaTypes.Volume) {
                if (!Volumes.TryGetValue(areaID, out volumeData)) return;
                areaTransform = volumeData.Transform;
                areaInverseTransform = volumeData.InverseTransform;
                regionCount = volumeData.Regions.Length;
            } else if (meta.AreaType == NavAreaTypes.Surface) {
                if (!Surfaces.TryGetValue(areaID, out surfaceData)) return;
                areaTransform = surfaceData.Transform;
                areaInverseTransform = surfaceData.InverseTransform;
                regionCount = surfaceData.Regions.Length;
            } else {
                return;
            }

            UnsafeArray<SerializableRange> linkRanges = LinkRangeResults[index];
            UnsafeList<NativeNavExternalLinkData> areaLinks = LinkResults[index];

            // Find external links for each region.
            for (int i = 0; i < regionCount; i++) {
                NativeNavVolumeRegionData volumeRegion = default;
                NativeNavSurfaceRegionData surfaceRegion = default;

                NativeBounds regionBounds = default;
                if (meta.AreaType == NavAreaTypes.Volume) {
                    volumeRegion = volumeData.Regions[i];
                    regionBounds = volumeRegion.Bounds;
                } else if (meta.AreaType == NavAreaTypes.Surface) {
                    surfaceRegion = surfaceData.Regions[i];
                    regionBounds = surfaceRegion.Bounds;
                }

                float regionRadius = math.length(regionBounds.Extents);
                float4 regionCenter = math.mul(areaTransform, regionBounds.Center);

                linkRanges[i] = new SerializableRange(areaLinks.Length, 0);

                foreach (KeyValue<long, NativeNavVolumeData> pair in Volumes) {
                    long otherID = pair.Key;
                    if (otherID == areaID) continue;
                    NativeNavVolumeData otherVolume = pair.Value;

                    if (!meta.ExternalLinkTargetLayers.Contains(otherVolume.Layer)) continue;

                    // Check if volumes are close enough to have external links.
                    float otherVolumeRadius = math.length(otherVolume.Bounds.Extents);
                    float4 otherVolumeCenter = math.mul(otherVolume.Transform, otherVolume.Bounds.Center);

                    // If region radius + MaxExternalLinkDistance doesn't overlap with other volume,
                    // cannot have any links.
                    float volumeDistance = math.distance(regionCenter, otherVolumeCenter);
                    if (volumeDistance > otherVolumeRadius + regionRadius + maxExternalLinkDistanceToVolume) continue;

                    for (int j = 0; j < otherVolume.Regions.Length; j++) {
                        NativeNavVolumeRegionData otherRegion = otherVolume.Regions[j];

                        float otherRegionRadius = math.length(otherRegion.Bounds.Extents);
                        float4 otherRegionCenter = math.mul(otherVolume.Transform, otherRegion.Bounds.Center);

                        // If region radius + MaxExternalLinkDistance doesn't overlap with other region,
                        // cannot have any links.
                        float regionDistance = math.distance(regionCenter, otherRegionCenter);
                        if (regionDistance > otherRegionRadius + regionRadius + maxExternalLinkDistanceToVolume)
                            continue;

                        // Check if self region has a link to other region.
                        float4 nearestOnSelf = default;
                        float4 pointForVolume = default;

                        if (meta.AreaType == NavAreaTypes.Volume) {
                            nearestOnSelf =
                                NativeMathUtility.GetNearestPointOnVolumeRegion(volumeData, volumeRegion.ID, otherRegionCenter);
                            pointForVolume = nearestOnSelf;
                        } else if (meta.AreaType == NavAreaTypes.Surface) {
                            nearestOnSelf =
                                NativeMathUtility.GetNearestPointOnSurfaceRegion(surfaceData, surfaceRegion.ID, otherRegionCenter);

                            // When calculating the nearest point in the volume, consider that for surface-based
                            // navigation, the reference point should be the agent's feet, whereas for volume-based
                            // navigation it should be the agent's center.
                            float4 worldUp = math.mul(surfaceData.Transform, surfaceRegion.UpVector);
                            float4 upOffset = worldUp * (meta.AgentHeight * 0.5f);
                            pointForVolume = nearestOnSelf + upOffset;
                        }

                        float4 nearestOnOther =
                            NativeMathUtility.GetNearestPointOnVolumeRegion(otherVolume, otherRegion.ID, pointForVolume);

                        float distSqr = math.distancesq(pointForVolume, nearestOnOther);
                        if (distSqr > maxExternalLinkDistanceToVolume * maxExternalLinkDistanceToVolume) continue;

                        if (meta.AreaType == NavAreaTypes.Surface &&
                            !TryFixApproachVector(otherVolume, otherRegion, surfaceData, surfaceRegion, pointForVolume,
                                meta, ref nearestOnOther)) {
                            continue;
                        }

                        // Add link to array.
                        float4 nearestOnSelfLocal = math.mul(areaInverseTransform, nearestOnSelf);
                        float4 nearestOnOtherLocal = math.mul(areaInverseTransform, nearestOnOther);
                        areaLinks.Add(new NativeNavExternalLinkData(otherID, NavAreaTypes.Volume, otherRegion.ID,
                            nearestOnSelfLocal, nearestOnOtherLocal, 0));

                        // Increase link count for this region by one.
                        linkRanges[i].Length++;
                    }
                }

                foreach (KeyValue<long, NativeNavSurfaceData> pair in Surfaces) {
                    long otherID = pair.Key;
                    if (otherID == areaID) continue;
                    NativeNavSurfaceData otherSurface = pair.Value;
                    if (!AreaMetadataMap.TryGetValue(otherID, out AreaMetadata otherMeta)) continue;

                    if (!meta.ExternalLinkTargetLayers.Contains(otherSurface.Layer)) continue;

                    // Check if volumes are close enough to have external links.
                    float otherSurfaceRadius = math.length(otherSurface.Bounds.Extents);
                    float4 otherSurfaceCenter = math.mul(otherSurface.Transform, otherSurface.Bounds.Center);

                    // If region radius + MaxExternalLinkDistance doesn't overlap with other volume,
                    // cannot have any links.
                    float surfaceDistance = math.distance(regionCenter, otherSurfaceCenter);
                    if (surfaceDistance > otherSurfaceRadius + regionRadius + maxExternalLinkDistanceToSurface)
                        continue;

                    for (int j = 0; j < otherSurface.Regions.Length; j++) {
                        NativeNavSurfaceRegionData otherRegion = otherSurface.Regions[j];

                        float otherRegionRadius = math.length(otherRegion.Bounds.Extents);
                        float4 otherRegionCenter = math.mul(otherSurface.Transform, otherRegion.Bounds.Center);

                        // If region radius + MaxExternalLinkDistance doesn't overlap with other region,
                        // cannot have any links.
                        float regionDistance = math.distance(regionCenter, otherRegionCenter);
                        if (regionDistance > otherRegionRadius + regionRadius + maxExternalLinkDistanceToSurface)
                            continue;

                        if (meta.AreaType == NavAreaTypes.Surface) {
                            float4 up = math.mul(surfaceData.Transform, surfaceRegion.UpVector);
                            float4 otherUp = math.mul(otherSurface.Transform, otherRegion.UpVector);
                            float angleCos = math.dot(up, otherUp);
                            if (angleCos < maxSurfaceAngleDifferenceCos) continue;
                        }

                        // Check if self region has a link to other region.
                        float4 nearestOnSelf = default;
                        if (meta.AreaType == NavAreaTypes.Volume) {
                            nearestOnSelf =
                                NativeMathUtility.GetNearestPointOnVolumeRegion(volumeData, volumeRegion.ID, otherRegionCenter);
                        } else if (meta.AreaType == NavAreaTypes.Surface) {
                            nearestOnSelf =
                                NativeMathUtility.GetNearestPointOnSurfaceRegion(surfaceData, surfaceRegion.ID, otherRegionCenter);
                        }

                        float4 nearestOnOther =
                            NativeMathUtility.GetNearestPointOnSurfaceRegion(otherSurface, otherRegion.ID, nearestOnSelf);

                        float4 pointForVolume = nearestOnOther;
                        if (meta.AreaType == NavAreaTypes.Volume) {
                            float4 worldUp = math.mul(otherSurface.Transform, otherRegion.UpVector);
                            float4 upOffset = worldUp * (otherMeta.AgentHeight * 0.5f);
                            pointForVolume = nearestOnOther + upOffset;
                        }

                        float distSqr = math.distancesq(nearestOnSelf, pointForVolume);
                        if (distSqr > maxExternalLinkDistanceToSurface * maxExternalLinkDistanceToSurface) continue;

                        if (meta.AreaType == NavAreaTypes.Volume &&
                            !TryFixApproachVector(volumeData, volumeRegion, otherSurface, otherRegion, pointForVolume,
                                otherMeta, ref nearestOnSelf)) {
                            continue;
                        }

                        // Add link to array.
                        float4 nearestOnSelfLocal = math.mul(areaInverseTransform, nearestOnSelf);
                        float4 nearestOnOtherLocal = math.mul(areaInverseTransform, nearestOnOther);
                        areaLinks.Add(new NativeNavExternalLinkData(otherID, NavAreaTypes.Surface, otherRegion.ID,
                            nearestOnSelfLocal, nearestOnOtherLocal, 0));

                        // Increase link count for this region by one.
                        linkRanges[i].Length++;
                    }
                }
            }

            LinkRangeResults[index] = linkRanges;
            LinkResults[index] = areaLinks;
        }

        private static bool TryFixApproachVector(
            NativeNavVolumeData volumeData,
            NativeNavVolumeRegionData volumeRegion,
            NativeNavSurfaceData surfaceData,
            NativeNavSurfaceRegionData surfaceRegion,
            float4 nearestOnSurface,
            AreaMetadata meta,
            ref float4 nearestOnVolume) {

            float4 down = -math.mul(surfaceData.Transform, surfaceRegion.UpVector);
            float4 approachVector = nearestOnSurface - nearestOnVolume;

            if (math.lengthsq(approachVector) < 0.0001f) return true;

            float approachAngleDot = math.dot(down, math.normalize(approachVector));

            if (approachAngleDot >= meta.MaxSurfaceVolumeApproachAngleCosTan.x) return true;

            float4 reject = NativeMathUtility.ProjectOnPlane(approachVector, down.xyz);
            float4 project = math.project(approachVector, down);

            float height = meta.MaxSurfaceVolumeApproachAngleCosTan.y * math.length(reject);
            float4 targetPoint = nearestOnVolume + project - down * (height * 1.1f);

            nearestOnVolume = NativeMathUtility.GetNearestPointOnVolumeRegion(volumeData, volumeRegion.ID, targetPoint);

            float4 newApproachVector = nearestOnSurface - nearestOnVolume;

            if (math.lengthsq(newApproachVector) < 0.0001f) return true;

            float newApproachAngleDot =
                math.dot(down, math.normalize(newApproachVector));

            return newApproachAngleDot >= meta.MaxSurfaceVolumeApproachAngleCosTan.x;
        }
    }
}
