// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct CollectEdgeOccurrenceMapsJob : IJobParallelFor {
        public NativeArray<UnsafeHashMap<Edge, int2>> EdgeOccurrenceMaps;
        public NativeCancellationToken CancellationToken;

        [ReadOnly] public NativeArray<int> TriangleIndexList;
        [ReadOnly] public NativeArray<float4> Vertices;

        [ReadOnly] public NativeArray<int> IslandIndices;
        [ReadOnly] public NativeArray<int> VertexCountsPerIsland;

        public void Execute(int index) {
            UnsafeHashMap<Edge, int2> occurrenceMap = new(VertexCountsPerIsland[index] * 10, Allocator.Persistent);
            
            for (int i = 0; i < TriangleIndexList.Length; i += 3) {
                if (CancellationToken.IsCancellationRequested) {
                    occurrenceMap.Dispose();
                    return;
                }
                
                int a = TriangleIndexList[i + 0];
                int b = TriangleIndexList[i + 1];
                int c = TriangleIndexList[i + 2];
                
                if (a == -1 || IslandIndices[a] != index) continue;

                AddOccurrence(ref occurrenceMap, new Edge(a, b), i);
                AddOccurrence(ref occurrenceMap, new Edge(b, c), i);
                AddOccurrence(ref occurrenceMap, new Edge(c, a), i);
            }
            
            EdgeOccurrenceMaps[index] = occurrenceMap;
        }
        
        private bool AddOccurrence(ref UnsafeHashMap<Edge, int2> occurrenceMap, Edge edge, int triStart) {
            bool exists = occurrenceMap.TryGetValue(edge, out int2 value);
            
            if (exists) {
                if (value.x < 0) {
                    value.x = triStart;
                } else if (value.y < 0) {
                    value.y = triStart;
                } else {
                    Debug.LogError($"Triangle {triStart} cannot be added for edge ({edge.Vertex1}, {edge.Vertex2}) which already has two triangles {value}.");
                    Debug.LogError($"Positions: {Vertices[edge.Vertex1]} {Vertices[edge.Vertex2]}");
                    return false;
                }

                occurrenceMap[edge] = value;
            } else {
                occurrenceMap.Add(edge, new int2(triStart, -1));
            }

            return true;
        }
    }
}
