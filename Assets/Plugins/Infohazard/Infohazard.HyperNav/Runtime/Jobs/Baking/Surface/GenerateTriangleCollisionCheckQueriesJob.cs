// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct GenerateTriangleCollisionCheckQueriesJob : IJobParallelFor {
        [ReadOnly] public BakingNavSurfaceMeshInfo Mesh;

        [ReadOnly] public NativeList<int> TrianglesToCheck;
        
        [ReadOnly] public NativeArray<float4> VertexAngleOffsets;
        
        [ReadOnly] public NativeArray<float4> VertexCombinedNormals;

        public float4x4 Transform;
        public PhysicsScene PhysicsScene;
        public bool UsePhysicsScene;
        public int QueryLayerMask;

        public float Height;
        public float Radius;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<OverlapCapsuleCommand> Queries;

        public void Execute(int index) {
            int triStart = TrianglesToCheck[index];

            float capsuleHeight = math.max(Height, Radius * 2 + 0.01f);
            float4 worldUp = Mesh.TriangleUprightWorldDirections[triStart / 3];

            float4 point2Offset = worldUp * (capsuleHeight - Radius * 1.9f);

            for (int i = 0; i < 3; i++) {
                int vertexIndex = Mesh.TriangleList[triStart + i];
                float4 vertex = Mesh.Vertices[vertexIndex] + VertexAngleOffsets[vertexIndex];
                float4 worldVertex = math.mul(Transform, vertex);
                
                float4 worldNormal = VertexCombinedNormals[vertexIndex];
                float4 point1Offset = worldNormal * (Radius * 1.1f);

                float4 point1 = worldVertex + point1Offset;
                float4 point2 = point1 + point2Offset;

                CreateQuery(index * 3 + i, point1.xyz, point2.xyz, Radius);
            }
        }

        private void CreateQuery(int queryIndex, Vector3 point1, Vector3 point2, float radius) {
            QueryParameters queryParameters = new() {
                hitTriggers = QueryTriggerInteraction.Ignore,
                layerMask = QueryLayerMask,
            };

            if (UsePhysicsScene) {
                Queries[queryIndex] =
                    new OverlapCapsuleCommand(PhysicsScene, point1, point2, radius, queryParameters);
            } else {
                Queries[queryIndex] =
                    new OverlapCapsuleCommand(point1, point2, radius, queryParameters);
            }
        }
    }
}
