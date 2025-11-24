// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct CalculateInternalLinksJob : IJobParallelFor {
        [ReadOnly]
        public BakingNavVolumeMeshInfo Mesh;

        public NativeArray<RegionOutput> OutputData;

        public struct RegionOutput {
            public UnsafeList<NativeNavVolumeInternalLinkData> InternalLinks;
            public UnsafeList<int> InternalLinkVertices;
            public UnsafeList<Edge> InternalLinkEdges;
            public UnsafeList<Triangle> InternalLinkTriangles;
        }

        public void Execute(int index) {
            int oldRegionID = index + 1;

            UnsafeList<NativeNavVolumeInternalLinkData> internalLinks = new(8, Allocator.Persistent);
            UnsafeList<int> internalLinkVertices = new(32, Allocator.Persistent);
            UnsafeList<Edge> internalLinkEdges = new(16, Allocator.Persistent);
            UnsafeList<Triangle> internalLinkTriangles = new(16, Allocator.Persistent);

            UnsafeHashSet<int> checkedVertices = new(128, Allocator.Temp);

            for (int otherOldRegionID = 1; otherOldRegionID < Mesh.RegionTriangleLists.Length; otherOldRegionID++) {
                if (otherOldRegionID == oldRegionID) continue;
                int otherNewRegionID = otherOldRegionID - 1;

                SerializableRange vertexRange = new(internalLinkVertices.Length, 0);
                SerializableRange edgeRange = new(internalLinkEdges.Length, 0);
                SerializableRange triangleRange = new(internalLinkTriangles.Length, 0);

                // Find shared triangles
                UnsafeList<int> triangleList = Mesh.RegionTriangleLists[otherOldRegionID];
                int triCount = triangleList.Length / 3;
                for (int i = 0; i < triCount; i++) {
                    int triStart = i * 3;

                    int v0 = triangleList[triStart + 0];
                    int v1 = triangleList[triStart + 1];
                    int v2 = triangleList[triStart + 2];

                    if (v0 < 0 || v1 < 0 || v2 < 0) continue;

                    Triangle tri = new(v0, v1, v2);
                    if (Mesh.TriangleIndicesPerRegion[tri].TryGetValue(oldRegionID, out _)) {
                        internalLinkTriangles.Add(tri);
                        triangleRange.Length++;
                    }
                }

                // Find shared edges and vertices
                checkedVertices.Clear();
                for (int i = 0; i < triangleList.Length; i++) {
                    int e0 = triangleList[i];
                    if (e0 < 0) continue;
                    if (!checkedVertices.Add(e0)) continue;
                    if (!Mesh.VertexRegionMembership[e0].Contains(oldRegionID)) continue;

                    internalLinkVertices.Add(e0);
                    vertexRange.Length++;

                    // Shared edges
                    HybridIntList connections = Mesh.VertexConnections[e0];
                    for (int j = 0; j < connections.Count; j++) {
                        int e1 = connections[j];

                        if (e1 < 0) continue;

                        // Avoid duplicating edges.
                        if (e1 < e0) continue;

                        IntList2 e1Membership = Mesh.VertexRegionMembership[e1];
                        if (!e1Membership.Contains(otherOldRegionID) || !e1Membership.Contains(oldRegionID)) continue;

                        internalLinkEdges.Add(new Edge(e0, e1));
                        edgeRange.Length++;
                    }
                }

                if (vertexRange.Length <= 0 && edgeRange.Length <= 0 && triangleRange.Length <= 0) continue;

                NativeNavVolumeInternalLinkData linkData = new(otherNewRegionID, vertexRange, edgeRange, triangleRange);
                internalLinks.Add(linkData);
            }

            checkedVertices.Dispose();

            OutputData[index] = new RegionOutput {
                InternalLinks = internalLinks,
                InternalLinkVertices = internalLinkVertices,
                InternalLinkEdges = internalLinkEdges,
                InternalLinkTriangles = internalLinkTriangles,
            };
        }
    }
}
