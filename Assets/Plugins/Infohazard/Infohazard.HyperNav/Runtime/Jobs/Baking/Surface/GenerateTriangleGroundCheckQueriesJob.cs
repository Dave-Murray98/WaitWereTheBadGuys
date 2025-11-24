// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct GenerateTriangleGroundCheckQueriesJob : IJobParallelFor {
        [ReadOnly]
        public BakingNavSurfaceMeshInfo Mesh;

        public float4x4 Transform;
        public float VoxelSize;
        public PhysicsScene PhysicsScene;
        public bool UsePhysicsScene;
        public int QueryLayerMask;

        [ReadOnly]
        public NativeArray<int> TrianglesToCheck;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<RaycastCommand> Queries;

        public void Execute(int index) {
            int triStart = TrianglesToCheck[index];

            int index1 = Mesh.TriangleList[triStart + 0];
            int index2 = Mesh.TriangleList[triStart + 1];
            int index3 = Mesh.TriangleList[triStart + 2];

            float4 vertex1 = Mesh.Vertices[index1];
            float4 vertex2 = Mesh.Vertices[index2];
            float4 vertex3 = Mesh.Vertices[index3];
            float4 center = (vertex1 + vertex2 + vertex3) / 3f;

            float4 normal = Mesh.TriangleNormals[triStart / 3];
            float4 worldNormal = math.mul(Transform, normal);

            float4 worldVertex1 = math.mul(Transform, vertex1);
            float4 worldVertex2 = math.mul(Transform, vertex2);
            float4 worldVertex3 = math.mul(Transform, vertex3);
            float4 worldCenter = math.mul(Transform, center);

            float3 offset = worldNormal.xyz * VoxelSize * 0.2f;
            float radius = VoxelSize * 0.05f;
            Quaternion rotation = Quaternion.LookRotation(-worldNormal.xyz);
            
            Vector3 v1 = rotation * new Vector3(radius, 0, 0);
            Vector3 v2 = rotation * new Vector3(0, radius, 0);

            CreateQueries(index * 16 + 0, worldVertex1.xyz + offset, -worldNormal.xyz, VoxelSize * 2f, v1, v2);
            CreateQueries(index * 16 + 4, worldVertex2.xyz + offset, -worldNormal.xyz, VoxelSize * 2f, v1, v2);
            CreateQueries(index * 16 + 8, worldVertex3.xyz + offset, -worldNormal.xyz, VoxelSize * 2f, v1, v2);
            CreateQueries(index * 16 + 12, worldCenter.xyz + offset, -worldNormal.xyz, VoxelSize * 2f, v1 * 0.1f,
                v2 * 0.1f);
        }

        private void CreateQueries(int queryIndex, Vector3 origin, Vector3 direction, float raycastDistance, Vector3 v1,
                                   Vector3 v2) {
            
            CreateQuery(queryIndex + 0, origin + v1, direction, raycastDistance);
            CreateQuery(queryIndex + 1, origin - v1, direction, raycastDistance);
            CreateQuery(queryIndex + 2, origin + v2, direction, raycastDistance);
            CreateQuery(queryIndex + 3, origin - v2, direction, raycastDistance);
        }

        private void CreateQuery(int queryIndex, Vector3 origin, Vector3 direction, float raycastDistance) {
            QueryParameters queryParameters = new() {
                hitTriggers = QueryTriggerInteraction.Ignore,
                layerMask = QueryLayerMask,
            };

            if (UsePhysicsScene) {
                Queries[queryIndex] =
                    new RaycastCommand(PhysicsScene, origin, direction, queryParameters, raycastDistance);
            } else {
                Queries[queryIndex] = new RaycastCommand(origin, direction, queryParameters, raycastDistance);
            }
        }
    }
}
