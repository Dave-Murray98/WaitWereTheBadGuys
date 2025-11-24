// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Runtime.InteropServices;
using Infohazard.HyperNav.Jobs.Baking.Volume;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct PopulateSurfaceVoxelsFromHitsJob : IJobParallelFor {
        [ReadOnly] public NativeArray<ColliderHit> BlockingHitsArray;
        [ReadOnly] public NativeArray<ColliderHit> WalkableHitsArray;

        public int HitCountPerVoxel;

        public bool UseStaticCheck;

        [ReadOnly]
        public NativeHashSet<int> StaticColliders;

        public Fast3DArray Voxels;

        public const int ResultWalkable = 1;
        public const int ResultBlocked = 0;
        public const int ResultOpen = -1;

        public void Execute(int index) {
            int hitStartIndex = index * HitCountPerVoxel;

            bool isWalkable = false;
            bool isBlocked = false;
            for (int i = 0; i < HitCountPerVoxel; i++) {
                int hitIndex = hitStartIndex + i;
                if (!isWalkable) {
                    ColliderHit walkableHit = WalkableHitsArray[hitIndex];

                    if (walkableHit.instanceID != 0 &&
                        (!UseStaticCheck || StaticColliders.Contains(walkableHit.instanceID))) {
                        isWalkable = true;
                    }
                }

                ColliderHit blockingHit = BlockingHitsArray[hitIndex];

                if (blockingHit.instanceID != 0 &&
                    (!UseStaticCheck || StaticColliders.Contains(blockingHit.instanceID))) {
                    isBlocked = true;
                    break; // If we find a blocking hit, not need to keep searching.
                }
            }

            Voxels[index] = isWalkable
                ? ResultWalkable
                : isBlocked
                    ? ResultBlocked
                    : ResultOpen;
        }
    }
}
