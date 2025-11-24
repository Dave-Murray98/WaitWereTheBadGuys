// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Runtime.InteropServices;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Baking.Volume;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct ProcessTriangleCollisionResultsJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<ColliderHit> Hits;

        public int HitCountPerItem;

        public bool UseStaticCheck;

        [ReadOnly]
        public NativeHashSet<int> StaticColliders;

        [WriteOnly]
        public NativeArray<bool> PartiallyFailingTriangles;

        [WriteOnly]
        public NativeArray<bool> CompletelyFailingTriangles;

        public void Execute(int index) {
            bool didHit = false;
            bool allHit = true;

            int hitStartIndex = index * HitCountPerItem;
            for (int i = 0; i < HitCountPerItem; i++) {
                int hitIndex = hitStartIndex + i;
                ColliderHit hit = Hits[hitIndex];

                if (hit.instanceID == 0 ||
                    (UseStaticCheck && !StaticColliders.Contains(hit.instanceID))) {
                    allHit = false;
                } else {
                    didHit = true;
                }

                if (didHit && !allHit) break;
            }

            if (allHit) {
                CompletelyFailingTriangles[index] = true;
            } else if (didHit) {
                PartiallyFailingTriangles[index] = true;
            }
        }
    }
}
