// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav.Jobs {
    [BurstCompile]
    public struct NavSampleJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<NavSampleQuery> Queries;

        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeParallelHashMap<long, NativeNavVolumeData> Volumes;

        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeParallelHashMap<long, NativeNavSurfaceData> Surfaces;

        [WriteOnly]
        public NativeArray<NavSampleResult> Results;

        public void Execute(int index) {
            NavSampleQuery query = Queries[index];
            SamplePositionInAllAreas(query, Surfaces, Volumes, out NavSampleResult result);
            Results[index] = result;
        }

        [BurstDiscard]
        public static unsafe void SamplePositionsInAllAreas(
            ReadOnlySpan<Vector3> positions,
            Span<NavSampleResult> results,
            float maxDistance,
            NavAreaTypes areaTypeMask,
            uint layerMask = uint.MaxValue,
            NavSamplePriority priority = NavSamplePriority.Nearest) {
            Span<NavSampleQuery> positionsWithRadii = stackalloc NavSampleQuery[positions.Length];
            for (int i = 0; i < positions.Length; i++) {
                positionsWithRadii[i] =
                    new NavSampleQuery(positions[i], maxDistance, areaTypeMask, layerMask, priority);
            }

            SamplePositionsInAllAreas(positionsWithRadii, results);
        }

        [BurstDiscard]
        public static unsafe void SamplePositionsInAllAreas(ReadOnlySpan<NavSampleQuery> positionsWithRadii,
                                                            Span<NavSampleResult> results) {
            fixed (NavSampleResult* resultsPtr = results)
            fixed (NavSampleQuery* inputPtr = positionsWithRadii) {
                NativeArray<NavSampleQuery> samplePositions =
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<NavSampleQuery>(
                        inputPtr, positionsWithRadii.Length, Allocator.None);

                NativeArray<NavSampleResult> sampleResults =
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<NavSampleResult>(
                        resultsPtr, results.Length, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle positionsSafety = AtomicSafetyHandle.Create();
                AtomicSafetyHandle resultsSafety = AtomicSafetyHandle.Create();
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref samplePositions, positionsSafety);
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref sampleResults, resultsSafety);
#endif

                NavSampleJob job = new() {
                    Queries = samplePositions,
                    Volumes = NavVolume.NativeDataMap,
                    Surfaces = NavSurface.NativeDataMap,
                    Results = sampleResults,
                };

                job.Schedule(samplePositions.Length, 1).Complete();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckDeallocateAndThrow(positionsSafety);
                AtomicSafetyHandle.CheckDeallocateAndThrow(resultsSafety);
                AtomicSafetyHandle.Release(positionsSafety);
                AtomicSafetyHandle.Release(resultsSafety);
#endif
            }
        }

        [BurstDiscard]
        public static bool SamplePositionInAllAreas(
            Vector3 position,
            out NavSampleResult hit,
            float maxDistance,
            NavAreaTypes areaTypeMask,
            uint layerMask = uint.MaxValue,
            NavSamplePriority priority = NavSamplePriority.Nearest,
            float3 upVectorForSurface = default,
            float minUpVectorDotForSurface = 0) {
            NavSampleQuery query = new(position, position, maxDistance, maxDistance, areaTypeMask, layerMask, priority,
                                       upVectorForSurface, minUpVectorDotForSurface);

            return SamplePositionInAllAreas(query, NavSurface.NativeDataMap, NavVolume.NativeDataMap, out hit);
        }

        [BurstCompile]
        private static bool SamplePositionInAllAreas(in NavSampleQuery query,
                                                     in NativeParallelHashMap<long, NativeNavSurfaceData> surfaces,
                                                     in NativeParallelHashMap<long, NativeNavVolumeData> volumes,
                                                     out NavSampleResult result) {
            if (query.Priority == NavSamplePriority.Surface) {
                return SamplePositionInSurfaces(query, surfaces, out result) ||
                       SamplePositionInVolumes(query, volumes, out result);
            } else if (query.Priority == NavSamplePriority.Volume) {
                return SamplePositionInVolumes(query, volumes, out result) ||
                       SamplePositionInSurfaces(query, surfaces, out result);
            } else {
                bool surfaceHit = SamplePositionInSurfaces(query, surfaces, out NavSampleResult surfaceResult);
                bool volumeHit = SamplePositionInVolumes(query, volumes, out NavSampleResult volumeResult);

                if (surfaceHit && volumeHit) {
                    result = surfaceResult.Distance < volumeResult.Distance ? surfaceResult : volumeResult;
                    return true;
                } else if (surfaceHit) {
                    result = surfaceResult;
                    return true;
                } else if (volumeHit) {
                    result = volumeResult;
                    return true;
                } else {
                    result = NavSampleResult.Invalid;
                    return false;
                }
            }
        }

        [BurstCompile]
        private static bool SamplePositionInSurfaces(in NavSampleQuery query,
                                                     in NativeParallelHashMap<long, NativeNavSurfaceData> surfaces,
                                                     out NavSampleResult result) {
            if ((query.AreaTypeMask & NavAreaTypes.Surface) == 0 ||
                query.RadiusForSurface <= 0 ||
                !surfaces.IsCreated) {
                result = NavSampleResult.Invalid;
                return false;
            }

            result = default;
            float bestDistance = float.PositiveInfinity;
            float4 worldPos = query.PositionForVolume;
            float4 posWithRadius = query.PositionWithRadiusForSurface;
            float4 upVectorWithMinDot = query.UpVectorWithMinDotForSurface;
            foreach (KeyValue<long, NativeNavSurfaceData> pair in surfaces) {
                NativeNavSurfaceData surface = pair.Value;

                if (!query.LayerMask.Contains(surface.Layer) ||
                    !SamplePositionInSurface(posWithRadius, upVectorWithMinDot, surface, out NavSampleResult tempHit)) {
                    continue;
                }

                if (!tempHit.IsOnEdge) {
                    result = tempHit;
                    return true;
                }

                float dist = math.distancesq(worldPos, tempHit.Position);
                if (dist < bestDistance) {
                    bestDistance = dist;
                    result = tempHit;
                }
            }

            return result.AreaID > 0;
        }

        [BurstCompile]
        public static bool SamplePositionInSurface(in float4 positionWithRadius,
                                                   in float4 upVectorWithMinDot,
                                                   in NativeNavSurfaceData surface,
                                                   out NavSampleResult result) {
            result = NavSampleResult.Invalid;

            float radius = positionWithRadius.w;
            float4 worldPos = new(positionWithRadius.xyz, 1);

            // Convert the position to inside the volume's coordinate space.
            // This makes the following math simpler and allows bounds checking without rotating the bounds.
            float4 localPos = math.mul(surface.InverseTransform, worldPos);

            // The point can be rejected if the the sample radius doesn't overlap the volume bounds.
            // Instead of doing a complex sphere vs box test, just create a bounds at the target with the sample radius.
            NativeBounds intersectBounds = new(localPos, new float4(radius, radius, radius, 0));
            if (!surface.Bounds.Intersects(intersectBounds)) return false;

            // Need to find which region has the closest point, which means looping through all regions.
            // Initialize closestDistance2 to be maxDistance ^ 2 so any point over that distance is ignored.
            float4 closestPoint = default;
            float4 upVector = default;
            float closestDistance2 = radius * radius;
            int closestRegion = -1;

            // Check regions within maxDistance range.
            for (int i = 0; i < surface.Regions.Length; i++) {
                NativeNavSurfaceRegionData region = surface.Regions[i];

                // Region can be skipped if the target bounds created earlier don't overlap it.
                if (!region.Bounds.Intersects(intersectBounds)) continue;

                // Check the up vector of the region against the query's up vector.
                if (!upVectorWithMinDot.Equals(float4.zero)) {
                    float dot = math.dot(region.UpVector.xyz, upVectorWithMinDot.xyz);
                    if (dot < upVectorWithMinDot.w) {
                        continue;
                    }
                }

                // Loop through all of the region's triangles to find which one has the nearest point.
                int triCount = region.TriangleIndexRange.Length / 3;
                for (int triIndex = 0; triIndex < triCount; triIndex++) {
                    // First index of the triangle in the mesh indices array.
                    int triStart = region.TriangleIndexRange.Start + triIndex * 3;

                    // Get triangle vertex indices.
                    int v1 = surface.TriangleIndices[triStart + 0];
                    int v2 = surface.TriangleIndices[triStart + 1];
                    int v3 = surface.TriangleIndices[triStart + 2];

                    // Get the triangle vertex positions.
                    float4 v1Pos = surface.Vertices[v1];
                    float4 v2Pos = surface.Vertices[v2];
                    float4 v3Pos = surface.Vertices[v3];

                    // Get the nearest point on the triangle, including its boundaries.
                    float4 nearestOnTriangle =
                        NativeMathUtility.GetNearestPointOnTriangleIncludingBounds(v1Pos, v2Pos, v3Pos, localPos);

                    // Check if that point is closer than the previous nearest point.
                    float dist2 = math.distancesq(nearestOnTriangle, localPos);
                    if (dist2 < closestDistance2) {
                        closestPoint = nearestOnTriangle;
                        upVector = region.UpVector;
                        closestDistance2 = dist2;
                        closestRegion = i;
                    }
                }
            }

            // If any hit was found in range, return it.
            if (closestRegion >= 0) {
                float4 worldUp = math.mul(surface.Transform, upVector);
                result = new NavSampleResult(
                    surface.ID,
                    NavAreaTypes.Surface,
                    surface.Layer,
                    closestRegion,
                    true,
                    math.mul(surface.Transform, closestPoint),
                    worldUp,
                    math.sqrt(closestDistance2));
                return true;
            }

            return false;
        }

        [BurstCompile]
        private static bool SamplePositionInVolumes(in NavSampleQuery query,
                                                    in NativeParallelHashMap<long, NativeNavVolumeData> volumes,
                                                    out NavSampleResult result) {
            if ((query.AreaTypeMask & NavAreaTypes.Volume) == 0 ||
                !volumes.IsCreated) {
                result = NavSampleResult.Invalid;
                return false;
            }

            result = default;
            float bestDistance = float.PositiveInfinity;
            float4 worldPos = query.PositionForVolume;
            float4 posWithRadius = query.PositionWithRadiusForVolume;
            foreach (KeyValue<long, NativeNavVolumeData> pair in volumes) {
                NativeNavVolumeData volume = pair.Value;

                if (!query.LayerMask.Contains(volume.Layer) ||
                    !SamplePositionInVolume(posWithRadius, volume, out NavSampleResult tempHit)) {
                    continue;
                }

                if (!tempHit.IsOnEdge) {
                    result = tempHit;
                    return true;
                }

                float dist = math.distancesq(worldPos, tempHit.Position);
                if (dist < bestDistance) {
                    bestDistance = dist;
                    result = tempHit;
                }
            }

            return result.AreaID > 0;
        }

        [BurstCompile]
        public static bool SamplePositionInVolume(in float4 positionWithRadius, in NativeNavVolumeData volume,
                                                  out NavSampleResult result) {
            result = NavSampleResult.Invalid;

            float radius = positionWithRadius.w;
            float4 worldPos = new(positionWithRadius.xyz, 1);

            // Convert the position to inside the volume's coordinate space.
            // This makes the following math simpler and allows bounds checking without rotating the bounds.
            float4 localPos = math.mul(volume.InverseTransform, worldPos);

            // Check if position is inside any regions.
            // If so, then the position is the same as the hit position and no complex math is needed.
            // There's no way a region can contain the point if the volume's bounds don't contain it.
            if (volume.Bounds.Contains(localPos)) {
                for (int i = 0; i < volume.Regions.Length; i++) {
                    ref readonly NativeNavVolumeRegionData region = ref volume.Regions[i];

                    // Quick reject: just check the bounds of the region.
                    if (!region.Bounds.Contains(localPos)) continue;

                    // If inside the bounds, need to check each face to see if point is on the inside.
                    // This works because regions are always convex.
                    bool isOutside = false;
                    for (int j = 0; j < region.BoundPlaneRange.Length; j++) {
                        NativePlane plane = volume.BoundPlanes[region.BoundPlaneRange.Start + j];
                        float dot = math.dot(-plane.Normal, localPos);

                        if (dot > plane.Distance) continue;

                        isOutside = true;
                        break;
                    }

                    // If a region is found that contains the target position, no more work is needed.
                    if (!isOutside) {
                        result = new NavSampleResult(
                            volume.ID,
                            NavAreaTypes.Volume,
                            volume.Layer,
                            i,
                            false,
                            worldPos,
                            float4.zero,
                            0);
                        return true;
                    }
                }
            }

            // A maxDistance of zero means the target point must be inside a region to hit,
            // so the query fails at this point.
            if (radius <= 0) return false;

            // The point can be rejected if the the sample radius doesn't overlap the volume bounds.
            // Instead of doing a complex sphere vs box test, just create a bounds at the target with the sample radius.
            NativeBounds intersectBounds = new(localPos, new float4(radius, radius, radius, 0));
            if (!volume.Bounds.Intersects(intersectBounds)) return false;

            // Need to find which region has the closest point, which means looping through all regions.
            // Initialize closestDistance2 to be maxDistance ^ 2 so any point over that distance is ignored.
            float4 closestPoint = default;
            float closestDistance2 = radius * radius;
            int closestRegion = -1;

            // Check regions within maxDistance range.
            for (int i = 0; i < volume.Regions.Length; i++) {
                NativeNavVolumeRegionData region = volume.Regions[i];

                // Region can be skipped if the target bounds created earlier don't overlap it.
                if (!region.Bounds.Intersects(intersectBounds)) continue;

                // Loop through all of the region's triangles to find which one has the nearest point.
                int triCount = region.TriangleIndexRange.Length / 3;
                for (int triIndex = 0; triIndex < triCount; triIndex++) {
                    // First index of the triangle in the mesh indices array.
                    int triStart = region.TriangleIndexRange.Start + triIndex * 3;

                    // Get triangle vertex indices.
                    int v1 = volume.TriangleIndices[triStart + 0];
                    int v2 = volume.TriangleIndices[triStart + 1];
                    int v3 = volume.TriangleIndices[triStart + 2];

                    // Get the triangle vertex positions.
                    float4 v1Pos = volume.Vertices[v1];
                    float4 v2Pos = volume.Vertices[v2];
                    float4 v3Pos = volume.Vertices[v3];

                    // Get the nearest point on the triangle, including its boundaries.
                    float4 nearestOnTriangle =
                        NativeMathUtility.GetNearestPointOnTriangleIncludingBounds(v1Pos, v2Pos, v3Pos, localPos);

                    // Check if that point is closer than the previous nearest point.
                    float dist2 = math.distancesq(nearestOnTriangle, localPos);
                    if (dist2 < closestDistance2) {
                        closestPoint = nearestOnTriangle;
                        closestDistance2 = dist2;
                        closestRegion = i;
                    }
                }
            }

            // If any hit was found in range, return it.
            if (closestRegion >= 0) {
                result = new NavSampleResult(
                    volume.ID,
                    NavAreaTypes.Volume,
                    volume.Layer,
                    closestRegion,
                    true,
                    math.mul(volume.Transform, closestPoint),
                    float4.zero,
                    math.sqrt(closestDistance2));
                return true;
            }

            return false;
        }
    }
}
