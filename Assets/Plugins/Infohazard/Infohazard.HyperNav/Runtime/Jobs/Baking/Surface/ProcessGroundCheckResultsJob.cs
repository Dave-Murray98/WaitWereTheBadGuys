// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct ProcessGroundCheckResultsJob : IJobParallelFor {
        public NavSurfaceUprightDirectionMode UprightDirectionMode;

        public int ResultsPerQuery;

        public float MinNormalDotInTriangle;

        public float SlopeAngleLimitCos;

        public bool UseStaticCheck;

        public float4 FixedUpWorldDirection;

        public const int QueriesPerOrigin = 4;
        public const int OriginsPerTriangle = 4;

        [ReadOnly]
        public NativeHashSet<int> StaticColliders;

        [ReadOnly]
        public NativeArray<RaycastHit> GroundCheckResults;

        [ReadOnly]
        public NativeArray<int> TrianglesToCheck;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<float4> TriangleUprightWorldDirections;

        [WriteOnly]
        public NativeArray<bool> PartiallyFailingTriangles;

        [WriteOnly]
        public NativeArray<bool> CompletelyFailingTriangles;

        public void Execute(int index) {
            int triStart = TrianglesToCheck[index];
            bool isNormal = UprightDirectionMode == NavSurfaceUprightDirectionMode.HitNormal;
            
            int resultsPerOrigin = QueriesPerOrigin * ResultsPerQuery;
            
            Span<RaycastHit> nearestHits = stackalloc RaycastHit[OriginsPerTriangle];
            for (int j = 0; j < OriginsPerTriangle; j++) {
                nearestHits[j] = default;

                int firstHitIndex = (index * OriginsPerTriangle + j) * resultsPerOrigin;
                float nearestHitDistance = float.MaxValue;
                for (int k = 0; k < resultsPerOrigin; k++) {
                    RaycastHit hit = GroundCheckResults[firstHitIndex + k];
                    if (hit.colliderInstanceID == 0 ||
                        (UseStaticCheck && !StaticColliders.Contains(hit.colliderInstanceID))) {
                        continue;
                    }

                    if (hit.distance < nearestHitDistance) {
                        nearestHits[j] = hit;
                        nearestHitDistance = hit.distance;
                    }
                }
            }

            float4 uprightDirection = float4.zero;

            bool allWalkable = true;
            bool anyWalkable = false;
            
            for (int j = 0; j < OriginsPerTriangle; j++) {
                bool isWalkable = false;
                RaycastHit nearestHit = nearestHits[j];

                if (isNormal) {
                    isWalkable = nearestHit.colliderInstanceID != 0;
                    uprightDirection += new float4(nearestHit.normal, 0);
                } else {
                    uprightDirection = FixedUpWorldDirection;
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
                RaycastHit centerHit = nearestHits[3];
                for (int j = 0; j < 3; j++) {
                    RaycastHit hit1 = nearestHits[j];
                    RaycastHit hit2 = nearestHits[(j + 1) % 3];

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
}
