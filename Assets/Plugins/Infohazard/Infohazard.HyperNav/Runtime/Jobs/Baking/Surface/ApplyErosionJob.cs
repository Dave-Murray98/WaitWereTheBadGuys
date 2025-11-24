// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct ApplyErosionJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<int3> EdgeVertices;

        [NativeDisableParallelForRestriction]
        public NativeArray<float4> Vertices;
        
        [ReadOnly]
        public NativeArray<float4> VertexOutwardDirections;
        
        [ReadOnly]
        public NativeArray<RaycastHit> Hits;

        public float ErosionDistance;

        public int RaycastCountPerVertex;
        public int ResultsPerQuery;
        
        public bool UseStaticCheck;

        [ReadOnly]
        public NativeHashSet<int> StaticColliders;
        
        public void Execute(int index) {
            float fraction = 0;

            for (int i = 0; i < RaycastCountPerVertex; i++) {
                int queryIndex = index * RaycastCountPerVertex + i;
                if (IsHit(queryIndex)) continue;
                
                fraction = (RaycastCountPerVertex - i) / (float) RaycastCountPerVertex;
                break;
            }
            
            if (fraction == 0) return;
            
            float4 outward = VertexOutwardDirections[index];
            float4 offset = outward * -ErosionDistance * fraction;
            
            int vertexIndex = EdgeVertices[index].x;
            Vertices[vertexIndex] += offset;
        }

        private bool IsHit(int queryIndex) {
            for (int i = 0; i < ResultsPerQuery; i++) {
                RaycastHit hit = Hits[queryIndex * ResultsPerQuery + i];
                if (hit.colliderInstanceID == 0 ||
                    (UseStaticCheck && !StaticColliders.Contains(hit.colliderInstanceID))) {
                    continue;
                }

                return true;
            }
            
            return false;
        }
    }
}