// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Runtime.InteropServices;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct PopulateVolumeVoxelsFromHitsJob : IJobParallelFor {
        [ReadOnly] public NativeArray<ColliderHit> HitsArray;
        public int HitCountPerVoxel;

        public bool UseStaticCheck;

        [ReadOnly]
        public NativeHashSet<int> StaticColliders;

        public Fast3DArray Voxels;

        public void Execute(int index) {
            int hitStartIndex = index * HitCountPerVoxel;

            bool isBlocked = false;
            for (int i = 0; i < HitCountPerVoxel; i++) {
                int hitIndex = hitStartIndex + i;
                ColliderHit hit = HitsArray[hitIndex];

                if (hit.instanceID == 0) continue;
                if (UseStaticCheck && !StaticColliders.Contains(hit.instanceID)) continue;

                isBlocked = true;
                break;
            }

            Voxels[index] = isBlocked ? 0 : -1;
        }
    }
}
