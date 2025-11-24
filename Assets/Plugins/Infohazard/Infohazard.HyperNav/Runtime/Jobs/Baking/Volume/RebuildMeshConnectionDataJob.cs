// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct RebuildMeshConnectionDataJob : IJob {
        public BakingNavVolumeMeshInfo Mesh;

        public void Execute() {
            for (int i = 0; i < Mesh.Vertices.Length; i++) {
                HybridIntList connections = Mesh.VertexConnections[i];
                connections.Clear(false);
                Mesh.VertexConnections[i] = connections;

                Mesh.VertexRegionMembership[i] = default;
            }

            Mesh.TriangleIndicesPerRegion.Clear();

            for (int regionIndex = 0; regionIndex < Mesh.RegionTriangleLists.Length; regionIndex++) {
                UnsafeList<int> list = Mesh.RegionTriangleLists[regionIndex];

                int triCount = list.Length / 3;
                for (int i = 0; i < triCount; i++) {
                    int triStart = i * 3;

                    int v0 = list[triStart + 0];
                    int v1 = list[triStart + 1];
                    int v2 = list[triStart + 2];

                    if (v0 < 0 || v1 < 0 || v2 < 0) continue;

                    // Add vertex membership to current region.
                    AddMembership(v0, regionIndex);
                    AddMembership(v1, regionIndex);
                    AddMembership(v2, regionIndex);

                    // Add vertex connections to each other.
                    AddConnections(v0, v1, v2);
                    AddConnections(v1, v0, v2);
                    AddConnections(v2, v0, v1);

                    // Add triangle index.
                    Triangle tri = new(v0, v1, v2);
                    Mesh.TriangleIndicesPerRegion.TryGetValue(tri, out TriangleRegionIndices triIndices);
                    triIndices.SetValue(regionIndex, triStart);
                    Mesh.TriangleIndicesPerRegion[tri] = triIndices;
                }
            }
        }

        private void AddMembership(int vertex, int region) {
            IntList2 membership = Mesh.VertexRegionMembership[vertex];
            if (membership.Contains(region)) return;

            membership.Add(region);
            Mesh.VertexRegionMembership[vertex] = membership;
        }

        private void AddConnections(int fromVertex, int vertex1, int vertex2) {
            HybridIntList connections = Mesh.VertexConnections[fromVertex];

            bool has1 = connections.Contains(vertex1);
            bool has2 = connections.Contains(vertex2);

            if (has1 && has2) return;

            if (!has1) connections.Add(vertex1);
            if (!has2) connections.Add(vertex2);

            Mesh.VertexConnections[fromVertex] = connections;
        }
    }
}
