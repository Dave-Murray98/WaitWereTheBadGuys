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
    public struct GenerateShrinkwrapQueriesJob : IJobParallelFor {
        [WriteOnly] public NativeArray<SpherecastCommand> Queries;

        [ReadOnly] public BakingNavSurfaceMeshInfo Mesh;

        [ReadOnly]
        public NativeArray<int> VerticesToShrink;

        public PhysicsScene PhysicsScene;
        public bool UsePhysicsScene;
        public float4x4 Transform;
        public NativeBounds Bounds;
        public float VoxelSize;
        public int QueryLayerMask;

        public const float RaycastOffset = 0.01f;

        public void Execute(int index) {
            int vertexIndex = VerticesToShrink[index];
            float4 localSpaceVertex = Mesh.Vertices[vertexIndex];
            float4 worldSpaceVertex = math.mul(Transform, localSpaceVertex);

            float3 normal = float3.zero;

            HybridIntList vertexTriangles = Mesh.VertexTriangles[vertexIndex];
            for (int i = 0; i < vertexTriangles.Count; i++) {
                int triangleStart = vertexTriangles[i];

                int vertexIndex1 = Mesh.TriangleList[triangleStart + 0];
                int vertexIndex2 = Mesh.TriangleList[triangleStart + 1];
                int vertexIndex3 = Mesh.TriangleList[triangleStart + 2];

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (vertexIndex1 != vertexIndex && vertexIndex2 != vertexIndex && vertexIndex3 != vertexIndex) {
                    Debug.LogError("Vertex is not part of triangle.");
                    continue;
                }
#endif

                float4 vertex1 = Mesh.Vertices[vertexIndex1];
                float4 vertex2 = Mesh.Vertices[vertexIndex2];
                float4 vertex3 = Mesh.Vertices[vertexIndex3];

                float4 edge1 = vertex2 - vertex1;
                float4 edge2 = vertex3 - vertex1;

                float4 normalizedNormal = Mesh.TriangleNormals[triangleStart / 3];

                // Intentionally don't normalize it here.
                // Triangles with a greater area should have a greater influence on the normal.
                float3 triangleNormal = math.cross(edge1.xyz, edge2.xyz);
                if (math.dot(triangleNormal, normalizedNormal.xyz) < 0)
                    triangleNormal = -triangleNormal;

                normal += triangleNormal;
            }

            normal = math.normalize(normal);
            float4 normal4 = new(normal, 0);

            float forwardDistance = VoxelSize;
            float reverseDistance = VoxelSize * 0.2f;

            float4 normalWorld = math.mul(Transform, normal4);

            float radius = VoxelSize * 0.5f;

            float reverseDistanceWithOffset = reverseDistance + RaycastOffset + radius;
            float4 origin = worldSpaceVertex + normalWorld * reverseDistanceWithOffset;

            CreateQuery(index, origin.xyz, radius, -normalWorld.xyz, reverseDistanceWithOffset + forwardDistance);
        }

        private void CreateQuery(int queryIndex, Vector3 origin, float radius, Vector3 direction, float raycastDistance) {
            //Debug.DrawLine(origin, origin + direction * raycastDistance, Color.red, 10f);

            if (UsePhysicsScene) {
                Queries[queryIndex] =
                    new SpherecastCommand(PhysicsScene, origin, radius, direction, new QueryParameters {
                        hitTriggers = QueryTriggerInteraction.Ignore,
                        layerMask = QueryLayerMask,
                    }, raycastDistance);
            } else {
                Queries[queryIndex] = new SpherecastCommand(origin, radius, direction, new QueryParameters {
                    hitTriggers = QueryTriggerInteraction.Ignore,
                    layerMask = QueryLayerMask,
                }, raycastDistance);
            }
        }
    }
}
