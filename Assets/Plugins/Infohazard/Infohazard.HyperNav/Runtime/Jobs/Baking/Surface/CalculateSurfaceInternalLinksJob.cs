// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct CalculateSurfaceInternalLinksJob : IJobParallelFor {
        [ReadOnly]
        public BakingNavSurfaceMeshInfo Mesh;

        [ReadOnly]
        public NativeArray<HybridIntList> VertexRegionMembership;

        public NativeArray<RegionOutput> OutputData;

        public struct RegionOutput {
            public UnsafeList<NativeNavSurfaceInternalLinkData> InternalLinks;
            public UnsafeList<int> InternalLinkVertices;
            public UnsafeList<Edge> InternalLinkEdges;
        }

        public void Execute(int index) {
            UnsafeList<NativeNavSurfaceInternalLinkData> internalLinks = new(8, Allocator.Persistent);
            UnsafeList<int> internalLinkVertices = new(32, Allocator.Persistent);
            UnsafeList<Edge> internalLinkEdges = new(16, Allocator.Persistent);

            UnsafeHashSet<int> checkedVertices = new(128, Allocator.Temp);

            for (int otherRegion = 0; otherRegion < Mesh.GroupTriangles.Length; otherRegion++) {
                if (otherRegion == index) continue;

                SerializableRange vertexRange = new(internalLinkVertices.Length, 0);
                SerializableRange edgeRange = new(internalLinkEdges.Length, 0);

                HybridIntList triStarts = Mesh.GroupTriangles[otherRegion];

                // Find shared edges and vertices
                checkedVertices.Clear();
                for (int i = 0; i < triStarts.Count; i++) {
                    int triStart = triStarts[i];
                    for (int j = 0; j < 3; j++) {
                        int e0 = Mesh.TriangleList[triStart + j];
                        if (e0 < 0) continue;
                        if (!checkedVertices.Add(e0)) continue;
                        if (!VertexRegionMembership[e0].Contains(index)) continue;

                        internalLinkVertices.Add(e0);
                        vertexRange.Length++;

                        // Shared edges
                        HybridIntList connections = Mesh.VertexConnections[e0];
                        for (int k = 0; k < connections.Count; k++) {
                            int e1 = connections[k];

                            if (e1 < 0) continue;

                            // Avoid duplicating edges.
                            if (e1 < e0) continue;

                            HybridIntList e1Membership = VertexRegionMembership[e1];
                            if (!e1Membership.Contains(otherRegion) || !e1Membership.Contains(index)) continue;

                            internalLinkEdges.Add(new Edge(e0, e1));
                            edgeRange.Length++;
                        }
                    }
                    
                }

                if (vertexRange.Length <= 0 && edgeRange.Length <= 0) continue;

                NativeNavSurfaceInternalLinkData linkData = new(otherRegion, vertexRange, edgeRange);
                internalLinks.Add(linkData);
            }

            checkedVertices.Dispose();

            OutputData[index] = new RegionOutput {
                InternalLinks = internalLinks,
                InternalLinkVertices = internalLinkVertices,
                InternalLinkEdges = internalLinkEdges,
            };
        }
    }
}