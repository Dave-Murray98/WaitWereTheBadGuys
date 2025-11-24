// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    public struct CollapsibleEdgePair {
        public int Vertex1;
        public int Vertex2;
        public float Priority;
    }

    [BurstCompile]
    public struct CollectEdgesJob : IJobParallelFor {
        public NativeArray<UnsafeList<CollapsibleEdgePair>> IslandCollapsibleEdgeLists;

        [ReadOnly]
        public NativeArray<int> IslandIndices;

        [ReadOnly]
        public BakingNavSurfaceMeshInfo Mesh;

        public NativeCancellationToken CancellationToken;

        public void Execute(int islandIndex) {
            UnsafeList<CollapsibleEdgePair> outputList = new(Mesh.Vertices.Length, Allocator.Persistent);

            for (int vertex1 = 0; vertex1 < Mesh.Vertices.Length; vertex1++) {
                if (CancellationToken.IsCancellationRequested) {
                    outputList.Dispose();
                    return;
                }

                if (IslandIndices[vertex1] != islandIndex) {
                    continue;
                }

                HybridIntList vertexConnections = Mesh.VertexConnections[vertex1];

                for (int i = 0; i < vertexConnections.Count; i++) {
                    int vertex2 = vertexConnections[i];

                    if (vertex2 < vertex1) {
                        continue;
                    }

                    Edge edge = new(vertex1, vertex2);

                    int priority = -(vertexConnections.Count + Mesh.VertexConnections[vertex2].Count);

                    outputList.Add(new CollapsibleEdgePair {
                        Vertex1 = edge.Vertex1,
                        Vertex2 = edge.Vertex2,
                        Priority = priority,
                    });
                }
            }

            IslandCollapsibleEdgeLists[islandIndex] = outputList;
        }
    }
}
