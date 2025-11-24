// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Runtime.InteropServices;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct ApplyShrinkwrapJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<SpherecastCommand> Queries;

        [ReadOnly]
        public NativeArray<RaycastHit> Hits;

        [ReadOnly]
        public NativeArray<int> VerticesToApply;

        [ReadOnly]
        public NativeArray<HybridIntList> VertexConnections;

        [NativeDisableParallelForRestriction]
        public NativeArray<float4> Vertices;

        public float4x4 InverseTransform;
        public int HitCountPerVertex;

        public bool UseStaticCheck;

        [ReadOnly]
        public NativeHashSet<int> StaticColliders;

        private bool GetNearestHit(int index, out RaycastHit hit) {
            SpherecastCommand query = Queries[index];

            float dist = query.distance;
            RaycastHit validHit = default;

            int hitStartIndex = index * HitCountPerVertex;
            for (int i = 0; i < HitCountPerVertex; i++) {
                RaycastHit tempHit = Hits[hitStartIndex + i];

                if (tempHit.colliderInstanceID == 0 ||
                    (UseStaticCheck && !StaticColliders.Contains(tempHit.colliderInstanceID))) {
                    continue;
                }

                if (tempHit.distance >= dist) continue;
                dist = tempHit.distance;
                validHit = tempHit;
            }

            hit = validHit;

            return validHit.colliderInstanceID != 0;
        }

        public void Execute(int index) {
            int vertexIndex = VerticesToApply[index];
            SpherecastCommand query = Queries[index];

            bool didHit = GetNearestHit(index, out RaycastHit hit);

            if (!didHit) {
                return;
            }

            float4 newPosWorldSpace = new(hit.point - query.direction * (query.radius * 0.05f), 1);
            float4 newPosLocalSpace = math.mul(InverseTransform, newPosWorldSpace);

            Vertices[vertexIndex] = newPosLocalSpace;
        }
    }
}
