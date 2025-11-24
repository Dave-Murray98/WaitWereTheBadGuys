// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct GenerateVolumeBoxQueriesJob : IJobParallelFor {
        [NativeDisableParallelForRestriction]
        public NativeArray<OverlapBoxCommand> Queries;

        public PhysicsScene PhysicsScene;
        public bool UsePhysicsScene;
        public float4x4 VolumeTransform;
        public Quaternion VolumeRotation;
        public NativeBounds Bounds;
        public int3 VoxelCounts;
        public int QueryCountPerAxis;
        public float DistanceBetweenQueries;
        public float VoxelSize;
        public Vector3 HalfExtents;
        public int QueryLayerMask;
        public bool EnableMultiQuery;

        public void Execute(int index) {
            int x = index / (VoxelCounts.y * VoxelCounts.z);
            int y = (index / VoxelCounts.z) % VoxelCounts.y;
            int z = index % VoxelCounts.z;

            float4 voxelCorner = Bounds.Min + new float4(x, y, z, 0) * VoxelSize;
            float halfVoxelSize = 0.5f * VoxelSize;
            float4 voxelCenter = voxelCorner + new float4(halfVoxelSize, halfVoxelSize, halfVoxelSize, 0);

            int queryCountPerVoxel = QueryCountPerAxis * QueryCountPerAxis * QueryCountPerAxis;
            if (EnableMultiQuery) {
                for (int xInner = 0; xInner < QueryCountPerAxis; xInner++) {
                    for (int yInner = 0; yInner < QueryCountPerAxis; yInner++) {
                        for (int zInner = 0; zInner < QueryCountPerAxis; zInner++) {
                            float4 innerPos =
                                voxelCorner + new float4(xInner, yInner, zInner, 0) * DistanceBetweenQueries;

                            int queryIndex = index * queryCountPerVoxel +
                                             xInner * QueryCountPerAxis * QueryCountPerAxis +
                                             yInner * QueryCountPerAxis +
                                             zInner;

                            float4 worldPoint = math.mul(VolumeTransform, innerPos);
                            CreateQuery(queryIndex, worldPoint.xyz);
                        }
                    }
                }
            } else {
                float4 worldPoint = math.mul(VolumeTransform, voxelCenter);
                CreateQuery(index, worldPoint.xyz);
            }
        }

        private void CreateQuery(int queryIndex, Vector3 worldPoint) {
            if (UsePhysicsScene) {
                Queries[queryIndex] =
                    new OverlapBoxCommand(PhysicsScene, worldPoint, HalfExtents, VolumeRotation, new QueryParameters {
                        hitTriggers = QueryTriggerInteraction.Ignore,
                        layerMask = QueryLayerMask,
                    });
            } else {
                Queries[queryIndex] = new OverlapBoxCommand(worldPoint, HalfExtents, VolumeRotation, new QueryParameters {
                    hitTriggers = QueryTriggerInteraction.Ignore,
                    layerMask = QueryLayerMask,
                });
            }
        }
    }
}
