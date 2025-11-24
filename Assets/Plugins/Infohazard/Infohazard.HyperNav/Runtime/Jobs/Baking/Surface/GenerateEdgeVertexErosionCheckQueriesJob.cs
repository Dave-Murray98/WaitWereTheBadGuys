// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct GenerateEdgeVertexErosionCheckQueriesJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<int3> EdgeVertices;

        [ReadOnly]
        public BakingNavSurfaceMeshInfo Mesh;

        [ReadOnly]
        public NativeArray<int> IslandIndices;

        [ReadOnly]
        public NativeArray<UnsafeHashMap<Edge, int2>> EdgeOccurrenceMaps;

        [WriteOnly]
        public NativeArray<float4> EdgeVertexOutwardDirections;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<RaycastCommand> Queries;

        public float ErosionDistance;

        public int RaycastCountPerVertex;

        public float ErosionCheckRange;
        
        public float4x4 Transform;
        public PhysicsScene PhysicsScene;
        public bool UsePhysicsScene;
        public int QueryLayerMask;

        public void Execute(int index) {
            int3 item = EdgeVertices[index];

            int vertexIndex = item.x;
            int other1 = item.y;
            int other2 = item.z;

            float4 vertex = Mesh.Vertices[vertexIndex];

            int islandIndex = IslandIndices[vertexIndex];
            UnsafeHashMap<Edge, int2> edgeOccurrenceMap = EdgeOccurrenceMaps[islandIndex];

            if (!GetEdgeOutwardDirection(vertexIndex, other1, ref edgeOccurrenceMap, out float4 outward1,
                    out float4 normal1) ||
                !GetEdgeOutwardDirection(vertexIndex, other2, ref edgeOccurrenceMap, out float4 outward2,
                    out float4 normal2)) {
                return;
            }

            float4 outward = math.normalize(outward1 + outward2);
            float4 normal = math.normalize(normal1 + normal2);
            EdgeVertexOutwardDirections[index] = outward;
            
            float4 worldVertex = math.mul(Transform, vertex);
            float4 worldOutward = math.mul(Transform, outward);
            float4 worldDirection = math.mul(Transform, -normal);
            
            //Debug.DrawLine(worldVertex.xyz, worldVertex.xyz + worldOutward.xyz * ErosionDistance, Color.red, 10);
            //Debug.DrawLine(worldVertex.xyz, worldVertex.xyz + normal.xyz, Color.blue, 10);
            
            QueryParameters queryParameters = new() {
                hitTriggers = QueryTriggerInteraction.Ignore,
                layerMask = QueryLayerMask,
            };

            int baseIndex = index * RaycastCountPerVertex;
            for (int i = 0; i < RaycastCountPerVertex; i++) {
                float fraction = (i + 1) / (float) (RaycastCountPerVertex + 1);
                float4 offset = worldOutward * ErosionDistance * fraction;
                float4 worldStart = worldVertex + offset;
                
                if (UsePhysicsScene) {
                    Queries[baseIndex + i] = new RaycastCommand(PhysicsScene, worldStart.xyz, worldDirection.xyz,
                        queryParameters, ErosionCheckRange);
                } else {
                    Queries[baseIndex + i] = new RaycastCommand(worldStart.xyz, worldDirection.xyz, queryParameters,
                        ErosionCheckRange);
                }
            }
        }

        private bool GetEdgeOutwardDirection(int index1, int index2, ref UnsafeHashMap<Edge, int2> edgeOccurrenceMap,
                                             out float4 outward, out float4 normal) {
            Edge edge = new(index1, index2);
            outward = float4.zero;
            normal = float4.zero;

            if (!edgeOccurrenceMap.TryGetValue(edge, out int2 triIndices)) {
                Debug.LogError($"Edge ({index1}, {index2}) not found in edgeOccurrenceMap.");
                return false;
            }

            int tri = triIndices.x >= 0 ? triIndices.x : triIndices.y;
            normal = Mesh.TriangleNormals[tri / 3];

            int i1 = -1;
            int i2 = -1;

            for (int i = 0; i < 3; i++) {
                int curIndex = tri + i;
                int vertex = Mesh.TriangleList[curIndex];
                if (vertex == index1) {
                    i1 = i;
                } else if (vertex == index2) {
                    i2 = i;
                }
            }

            if (i1 == -1 || i2 == -1 || i1 == i2) {
                Debug.LogError($"Edge ({index1}, {index2}) not found in triangle.");
                return false;
            }

            if (i1 == (i2 + 1) % 3) {
                int temp = index1;
                index1 = index2;
                index2 = temp;
            }

            float4 vertex1 = Mesh.Vertices[index1];
            float4 vertex2 = Mesh.Vertices[index2];

            float4 edgeVector = vertex2 - vertex1;
            float3 cross = math.cross(edgeVector.xyz, normal.xyz);

            outward = new float4(math.normalize(cross), 0);
            
            float4 midpoint = (vertex1 + vertex2) * 0.5f;
            //Debug.DrawLine(midpoint.xyz, midpoint.xyz + outward.xyz, Color.green, 10);
            
            return true;
        }
    }
}