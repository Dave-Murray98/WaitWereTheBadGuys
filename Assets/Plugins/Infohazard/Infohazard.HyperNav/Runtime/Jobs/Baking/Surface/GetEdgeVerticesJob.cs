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
    public struct GetEdgeVerticesJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<int> IslandIndices;
        
        [ReadOnly]
        public NativeArray<UnsafeHashMap<Edge, int2>> EdgeOccurrenceMaps;
        
        [ReadOnly]
        public BakingNavSurfaceMeshInfo Mesh;
        
        public NativeArray<int3> EdgeVertices;
        
        public void Execute(int index) {
            HybridIntList connections = Mesh.VertexConnections[index];
            int2 result = new(-1, -1);
            int boundEdgeCount = 0;
            EdgeVertices[index] = new int3(-1, result);
            
            int islandIndex = IslandIndices[index];
            if (islandIndex < 0) {
                return;
            }
            
            UnsafeHashMap<Edge, int2> edgeOccurrenceMap = EdgeOccurrenceMaps[islandIndex];

            for (int i = 0; i < connections.Count; i++) {
                int connectedVertex = connections[i];

                Edge edge = new(index, connectedVertex);
                if (!edgeOccurrenceMap.TryGetValue(edge, out int2 triIndices)) {
                    Debug.LogError($"Edge ({index}, {connectedVertex}) not found in edgeOccurrenceMap.");
                    continue;
                }

                if ((triIndices.x >= 0) == (triIndices.y >= 0)) continue;
                
                if (boundEdgeCount < 2) {
                    result[boundEdgeCount++] = connectedVertex;
                } else {
                    boundEdgeCount++;
                    break;
                }
            }

            if (boundEdgeCount == 2) {
                EdgeVertices[index] = new int3(index, result);
            }
        }
    }
}