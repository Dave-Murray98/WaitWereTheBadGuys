// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Assertions;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct RebuildVertexNeighborsJob : IJob {
        [ReadOnly]
        public NativeArray<int> TriangleIndices;

        public NativeArray<HybridIntList> VertexTriangles;
        public NativeArray<HybridIntList> VertexConnections;

        public void Execute() {
            Assert.AreEqual(VertexTriangles.Length, VertexConnections.Length);

            for (int i = 0; i < VertexConnections.Length; i++) {
                HybridIntList triangles = VertexTriangles[i];
                HybridIntList connections = VertexConnections[i];

                triangles.Clear(false);
                connections.Clear(false);

                VertexTriangles[i] = triangles;
                VertexConnections[i] = connections;
            }

            for (int i = 0; i < TriangleIndices.Length; i += 3) {
                int index1 = TriangleIndices[i + 0];
                int index2 = TriangleIndices[i + 1];
                int index3 = TriangleIndices[i + 2];

                if (index1 == -1 || index2 == -1 || index3 == -1) {
                    continue;
                }

                AddTriangle(index1, index2, index3, i);
                AddTriangle(index2, index1, index3, i);
                AddTriangle(index3, index1, index2, i);
            }
        }

        private void AddTriangle(int vertex, int otherVertex1, int otherVertex2, int triangleIndex) {
            HybridIntList triangles = VertexTriangles[vertex];
            HybridIntList connections = VertexConnections[vertex];

            triangles.Add(triangleIndex);

            if (!connections.Contains(otherVertex1)) {
                connections.Add(otherVertex1);
            }

            if (!connections.Contains(otherVertex2)) {
                connections.Add(otherVertex2);
            }

            VertexTriangles[vertex] = triangles;
            VertexConnections[vertex] = connections;
        }
    }
}
