// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct DetermineTrianglesToSplitJob : IJobParallelFor {
        public float MinNormalDotInTriangle;

        [ReadOnly]
        public NativeArray<int> TrianglesToCheck;

        [ReadOnly]
        public NativeArray<RaycastHit> Hits;

        public bool UseStaticCheck;

        [ReadOnly]
        public NativeHashSet<int> StaticColliders;

        [WriteOnly]
        public NativeArray<int> OutTrianglesToSplit;

        public int ResultsPerQuery;

        public void Execute(int index) {
            Span<RaycastHit> nearestHits = stackalloc RaycastHit[3];
            for (int j = 0; j < 3; j++) {
                nearestHits[j] = default;

                int firstHitIndex = (index * 3 + j) * ResultsPerQuery;
                float nearestHitDistance = float.MaxValue;
                for (int k = 0; k < ResultsPerQuery; k++) {
                    RaycastHit hit = Hits[firstHitIndex + k];
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

            bool allWalkable = true;
            for (int j = 0; j < 3; j++) {
                RaycastHit hit1 = nearestHits[j];
                RaycastHit hit2 = nearestHits[(j + 1) % 3];

                float dot = Vector3.Dot(hit1.normal, hit2.normal);
                if (dot > MinNormalDotInTriangle) continue;

                allWalkable = false;
                break;
            }

            if (!allWalkable) {
                OutTrianglesToSplit[index] = TrianglesToCheck[index];
            } else {
                OutTrianglesToSplit[index] = -1;
            }
        }
    }
}
