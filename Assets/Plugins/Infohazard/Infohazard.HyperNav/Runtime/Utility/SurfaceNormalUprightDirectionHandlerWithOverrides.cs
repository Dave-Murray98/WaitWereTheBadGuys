// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Cysharp.Threading.Tasks;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Baking.Surface;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav {
    /// <summary>
    /// Serves as an example of how to implement a custom upright direction handler for surfaces,
    /// including the related jobs.
    /// </summary>
    [CreateAssetMenu(menuName = "HyperNav/Upright Dir Handler with Overrides")]
    public class SurfaceUprightDirectionHandlerWithOverrides : ScriptableObject, ISurfaceUprightDirectionHandler {
        [SerializeField]
        private DefaultUprightMode _defaultUprightDirectionMode = DefaultUprightMode.HitNormal;

        [SerializeField]
        private Vector3 _defaultUprightDirection = Vector3.up;

        public async UniTask<NavSurfaceBakeData> CalculateUprightDirections(
            NavSurfaceBakeData bakeData,
            NativeArray<RaycastCommand> queries,
            int resultsPerQuery,
            NativeArray<RaycastHit> results,
            NativeCancellationToken cancellationToken) {
            Vector3 defaultUprightWorldDirection = _defaultUprightDirection;
            if (_defaultUprightDirectionMode == DefaultUprightMode.FixedSurfaceLocalDirection) {
                defaultUprightWorldDirection = bakeData.Surface.transform.TransformDirection(_defaultUprightDirection);
            }

            int triCount = bakeData.FilterData.TrianglesToCheck.Length;
            int originCount = triCount * ProcessGroundCheckResultsJob.OriginsPerTriangle;
            int resultsPerOrigin = ProcessGroundCheckResultsJob.QueriesPerOrigin * resultsPerQuery;

            // Get nearest hits for each vertex of each triangle.
            // This way, we don't have to check the collider for every hit, only the nearest ones.
            using NativeArray<RaycastHit> nearestHits = new(originCount, Allocator.TempJob);
            GetNearestHitsJob getNearestHitsJob = new() {
                UseStaticCheck = bakeData.Settings.StaticOnly,
                ResultsPerOrigin = resultsPerOrigin,
                StaticColliders = bakeData.StaticColliders,
                AllHits = results,
                NearestHits = nearestHits
            };

            await getNearestHitsJob.Schedule(originCount, 64);

            // Get surface overrides for each collider.
            // This must be done outside of the job system, because we are accessing GameObjects and MonoBehaviours.
            NativeArray<SurfaceOverrideItem> overrides = new(originCount, Allocator.Persistent);

            SurfaceOverrideItem defaultItem = new() {
                IsNormalDirection = _defaultUprightDirectionMode == DefaultUprightMode.HitNormal,
                UprightWorldDirection = defaultUprightWorldDirection.ToV4()
            };

            NativeHashMap<int, SurfaceOverrideItem> overridesMap = new(originCount, Allocator.Temp);
            for (int i = 0; i < nearestHits.Length; i++) {
                RaycastHit hit = nearestHits[i];
                int hitInstanceID = hit.colliderInstanceID;
                if (hitInstanceID == 0) continue;

                if (overridesMap.TryGetValue(hitInstanceID, out SurfaceOverrideItem item)) {
                    overrides[i] = item;
                    continue;
                }

                Collider collider = hit.collider;
                if (!collider.TryGetComponentInParent(out SurfaceUprightDirectionOverride cmp)) {
                    item = defaultItem;
                    if (_defaultUprightDirectionMode == DefaultUprightMode.ObjectLocalDirection) {
                        item.UprightWorldDirection =
                            collider.transform.TransformDirection(_defaultUprightDirection).ToV4();
                    }

                    overridesMap.Add(hitInstanceID, item);
                    overrides[i] = item;
                    continue;
                }

                item = new SurfaceOverrideItem {
                    IsNormalDirection = cmp.IsNormalDirection,
                    UprightWorldDirection = cmp.UprightWorldDirection.ToV4()
                };

                overridesMap.Add(hitInstanceID, item);
                overrides[i] = item;
            }

            overridesMap.Dispose();

            using NativeArray<bool> partiallyFailingTriangles = new(triCount, Allocator.Persistent);
            using NativeArray<bool> completelyFailingTriangles = new(triCount, Allocator.Persistent);

            // Calculate upright directions for each triangle.
            CalculateDirectionsJob calculateDirectionsJob = new() {
                ColliderSurfaceOverrides = overrides,
                NearestHits = nearestHits,
                TrianglesToCheck = bakeData.FilterData.TrianglesToCheck.AsArray(),
                TriangleUprightWorldDirections = bakeData.MeshInfo.TriangleUprightWorldDirections.AsArray(),
                PartiallyFailingTriangles = partiallyFailingTriangles,
                CompletelyFailingTriangles = completelyFailingTriangles,
                SlopeAngleLimitCos = Mathf.Cos(bakeData.Settings.SlopeAngleLimit * Mathf.Deg2Rad),
                MinNormalDotInTriangle =
                    Mathf.Cos(bakeData.Settings.MaxAngleBetweenUpDirectionsWithinTriangle * Mathf.Deg2Rad),
            };

            int triBatchSize = JobUtility.GetMinimumBatchSizeToAvoidUsingAllCores(triCount);
            JobHandle calcHandle = calculateDirectionsJob.Schedule(triCount, triBatchSize);

            AddFailingTrianglesToSetJob addCompleteToSetJob = new() {
                PartiallyFailingTriangles = partiallyFailingTriangles,
                CompletelyFailingTriangles = completelyFailingTriangles,
                ToRemoveTrianglesSet = bakeData.FilterData.TrianglesToRemove,
                ToSplitTriangleSet = bakeData.FilterData.TrianglesToSplit,
                TrianglesToCheck = bakeData.FilterData.TrianglesToCheck,
                CanSplitTriangles = bakeData.FilterData.Iteration < bakeData.Settings.MaxTriangleDivisions,
            };

            await addCompleteToSetJob.Schedule(calcHandle);

            overrides.Dispose();
            return bakeData;
        }

        private struct SurfaceOverrideItem {
            public float4 UprightWorldDirection;
            public bool IsNormalDirection;
        }

        [BurstCompile]
        private struct GetNearestHitsJob : IJobParallelFor {
            public bool UseStaticCheck;
            public int ResultsPerOrigin;

            [ReadOnly]
            public NativeHashSet<int> StaticColliders;

            [ReadOnly]
            public NativeArray<RaycastHit> AllHits;

            [WriteOnly]
            public NativeArray<RaycastHit> NearestHits;

            public void Execute(int index) {
                RaycastHit nearest = default;

                int firstHitIndex = index * ResultsPerOrigin;
                float nearestHitDistance = float.MaxValue;
                for (int i = 0; i < ResultsPerOrigin; i++) {
                    RaycastHit hit = AllHits[firstHitIndex + i];
                    if (hit.colliderInstanceID == 0 ||
                        (UseStaticCheck && !StaticColliders.Contains(hit.colliderInstanceID))) {
                        continue;
                    }

                    if (hit.distance < nearestHitDistance) {
                        nearest = hit;
                        nearestHitDistance = hit.distance;
                    }
                }

                NearestHits[index] = nearest;
            }
        }

        [BurstCompile]
        private struct CalculateDirectionsJob : IJobParallelFor {
            [ReadOnly]
            public NativeArray<SurfaceOverrideItem> ColliderSurfaceOverrides;

            [ReadOnly]
            public NativeArray<RaycastHit> NearestHits;

            [ReadOnly]
            public NativeArray<int> TrianglesToCheck;

            [WriteOnly, NativeDisableParallelForRestriction]
            public NativeArray<float4> TriangleUprightWorldDirections;

            [WriteOnly]
            public NativeArray<bool> PartiallyFailingTriangles;

            [WriteOnly]
            public NativeArray<bool> CompletelyFailingTriangles;

            public float SlopeAngleLimitCos;

            public float MinNormalDotInTriangle;

            public void Execute(int index) {
                int triStart = TrianglesToCheck[index];

                float4 uprightDirection = float4.zero;
                bool allWalkable = true;
                bool anyWalkable = false;

                int firstHitIndex = index * ProcessGroundCheckResultsJob.OriginsPerTriangle;

                for (int j = 0; j < ProcessGroundCheckResultsJob.OriginsPerTriangle; j++) {
                    bool isWalkable = false;
                    RaycastHit nearestHit = NearestHits[firstHitIndex + j];
                    SurfaceOverrideItem overrideItem = ColliderSurfaceOverrides[firstHitIndex + j];

                    if (overrideItem.IsNormalDirection) {
                        isWalkable = nearestHit.colliderInstanceID != 0;
                        uprightDirection += new float4(nearestHit.normal, 0);
                    } else {
                        uprightDirection = overrideItem.UprightWorldDirection;
                        isWalkable = nearestHit.colliderInstanceID != 0 &&
                                     math.dot(nearestHit.normal, uprightDirection.xyz) > SlopeAngleLimitCos;
                    }

                    allWalkable &= isWalkable;
                    anyWalkable |= isWalkable;

                    // Can only stop checking early if we have at least one walkable hit and at least one non-walkable hit.
                    if (!allWalkable && anyWalkable) {
                        break;
                    }
                }

                // If all walkable, check angles between the hits.
                if (allWalkable) {
                    RaycastHit centerHit = NearestHits[firstHitIndex + 3];
                    for (int j = 0; j < 3; j++) {
                        RaycastHit hit1 = NearestHits[firstHitIndex + j];
                        RaycastHit hit2 = NearestHits[firstHitIndex + (j + 1) % 3];

                        float edgeDot = Vector3.Dot(hit1.normal, hit2.normal);
                        float centerDot = Vector3.Dot(hit1.normal, centerHit.normal);
                        if (edgeDot >= MinNormalDotInTriangle && centerDot >= MinNormalDotInTriangle) continue;

                        allWalkable = false;
                        break;
                    }
                }

                if (!anyWalkable) {
                    CompletelyFailingTriangles[index] = true;
                } else if (!allWalkable) {
                    PartiallyFailingTriangles[index] = true;
                } else {
                    TriangleUprightWorldDirections[triStart / 3] = math.normalize(uprightDirection);
                }
            }
        }

        public enum DefaultUprightMode {
            FixedWorldDirection,
            FixedSurfaceLocalDirection,
            ObjectLocalDirection,
            HitNormal,
        }
    }
}
