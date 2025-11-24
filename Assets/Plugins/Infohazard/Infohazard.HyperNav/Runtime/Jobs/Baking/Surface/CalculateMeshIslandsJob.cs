// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct CalculateMeshIslandsJob : IJob {
        [ReadOnly]
        public NativeArray<HybridIntList> VertexConnections;
        
        public NativeArray<int> IslandIndices;

        public NativeList<int> VertexCountsPerIsland;
        
        public unsafe void Execute() {
            int islandCount = 0;
            
            UnsafeList<int> stack = new(VertexConnections.Length, Allocator.Temp);
            
            // Fill with -1 to indicate not yet visited.
            UnsafeUtility.MemSet(IslandIndices.GetUnsafePtr(), 0xFF, IslandIndices.Length * sizeof(int));
            
            for (int i = 0; i < VertexConnections.Length; i++) {
                if (IslandIndices[i] >= 0 || VertexConnections[i].Count == 0) continue;
                
                int currentIsland = islandCount;
                IslandIndices[i] = currentIsland;
                islandCount++;
                int vertexCount = 1;
                
                stack.Clear();
                stack.Add(i);
                
                while (stack.Length > 0) {
                    int vertex = stack[^1];
                    stack.RemoveAt(stack.Length - 1);
                    HybridIntList connections = VertexConnections[vertex];
                    for (int j = 0; j < connections.Count; j++) {
                        int connection = connections[j];
                        if (IslandIndices[connection] >= 0) continue;
                        IslandIndices[connection] = currentIsland;
                        vertexCount++;
                        stack.Add(connection);
                    }
                }
                
                VertexCountsPerIsland.Add(vertexCount);
            }
            
            stack.Dispose();
        }
    }
}