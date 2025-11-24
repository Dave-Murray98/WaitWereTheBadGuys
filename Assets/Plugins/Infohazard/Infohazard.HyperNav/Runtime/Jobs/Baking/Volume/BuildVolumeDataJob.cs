// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct BuildVolumeDataJob : IJob {
        public BakingNavVolumeMeshInfo Mesh;
        public NativeArray<CalculateInternalLinksJob.RegionOutput> LinkData;
        public NativeArray<UnsafeList<NativePlane>> BoundPlanes;

        public NativeReference<NativeNavVolumeData> OutputData;

        public void Execute() {
            // Count vertices and build mapping from old index -> new index.
            UnsafeHashMap<int, int> reducedIndexMap =
                CountVerticesAndBuildMapping(out int indexCount, out int vertexCount);

            // Create array for all vertices.
            UnsafeArray<float4> allVertices = CopyVerticesToArray(vertexCount, ref reducedIndexMap);

            // Allocate array for all vertex indices.
            UnsafeArray<int> allVertexIndices = new(indexCount, Allocator.Persistent);

            // Count bound planes.
            int boundPlaneCount = CountBoundPlanes();

            // Create array for all bound planes.
            UnsafeArray<NativePlane> allBoundPlanes = new(boundPlaneCount, Allocator.Persistent);

            // Count blocking indices and copy them to the new array.
            int blockingIndexCount = CopyBlockingIndices(ref reducedIndexMap, ref allVertexIndices);

            // Count internal links and their components.
            int linkCount = CountLinks(out int linkVertexCount, out int linkEdgeCount, out int linkTriangleCount);

            // Allocate arrays for internal links.
            UnsafeArray<NativeNavVolumeInternalLinkData> allLinks = new(linkCount, Allocator.Persistent);
            UnsafeArray<int> allLinkVertices = new(linkVertexCount, Allocator.Persistent);
            UnsafeArray<int2> allLinkEdges = new(linkEdgeCount, Allocator.Persistent);
            UnsafeArray<int3> allLinkTriangles = new(linkTriangleCount, Allocator.Persistent);

            // Create region data.
            UnsafeArray<NativeNavVolumeRegionData>
                regions = new(Mesh.RegionTriangleLists.Length - 1, Allocator.Persistent);
            CreateRegionData(blockingIndexCount, ref reducedIndexMap, ref regions, ref allVertexIndices,
                             ref allBoundPlanes, ref allLinkVertices, ref allLinkEdges, ref allLinkTriangles,
                             ref allLinks);

            reducedIndexMap.Dispose();

            OutputData.Value = new NativeNavVolumeData(
                -1,
                default,
                default,
                default,
                NavLayer.None,
                allVertices,
                regions,
                allVertexIndices,
                blockingIndexCount,
                allBoundPlanes,
                allLinks,
                default,
                allLinkVertices,
                allLinkEdges,
                allLinkTriangles);
        }

        private void CreateRegionData(int blockingIndexCount, ref UnsafeHashMap<int, int> reducedIndexMap,
                                      ref UnsafeArray<NativeNavVolumeRegionData> regions,
                                      ref UnsafeArray<int> allVertexIndices,
                                      ref UnsafeArray<NativePlane> allBoundPlanes,
                                      ref UnsafeArray<int> allLinkVertices,
                                      ref UnsafeArray<int2> allLinkEdges,
                                      ref UnsafeArray<int3> allLinkTriangles,
                                      ref UnsafeArray<NativeNavVolumeInternalLinkData> allLinks) {
            int addedIndexCount = blockingIndexCount;
            int addedBoundCount = 0;
            int addedLinkCount = 0;
            int addedLinkVertexCount = 0;
            int addedLinkEdgeCount = 0;
            int addedLinkTriangleCount = 0;

            for (int i = 1; i < Mesh.RegionTriangleLists.Length; i++) {
                int newRegionID = i - 1;

                // Add triangle indices.
                SerializableRange indexRange = CopyRegionIndices(i, ref reducedIndexMap, ref allVertexIndices,
                                                                 ref addedIndexCount, out NativeBounds bounds);

                // Add bound planes.
                SerializableRange planeRange =
                    CopyRegionBoundPlanes(newRegionID, ref allBoundPlanes, ref addedBoundCount);

                // Add internal links.
                SerializableRange linkRange = CopyRegionLinks(newRegionID, ref reducedIndexMap, ref allLinkVertices,
                                                              ref allLinkEdges, ref allLinkTriangles, ref allLinks,
                                                              ref addedLinkCount, ref addedLinkVertexCount,
                                                              ref addedLinkEdgeCount, ref addedLinkTriangleCount);

                regions[newRegionID] =
                    new NativeNavVolumeRegionData(newRegionID, bounds, indexRange, planeRange, linkRange, default);
            }
        }

        private SerializableRange CopyRegionLinks(int newRegionID, ref UnsafeHashMap<int, int> reducedIndexMap,
                                                  ref UnsafeArray<int> allLinkVertices,
                                                  ref UnsafeArray<int2> allLinkEdges,
                                                  ref UnsafeArray<int3> allLinkTriangles,
                                                  ref UnsafeArray<NativeNavVolumeInternalLinkData> allLinks,
                                                  ref int addedLinkCount, ref int addedLinkVertexCount,
                                                  ref int addedLinkEdgeCount, ref int addedLinkTriangleCount) {
            CalculateInternalLinksJob.RegionOutput regionLinkData = LinkData[newRegionID];
            SerializableRange linkRange = new(addedLinkCount, regionLinkData.InternalLinks.Length);

            for (int j = 0; j < regionLinkData.InternalLinks.Length; j++) {
                NativeNavVolumeInternalLinkData link = regionLinkData.InternalLinks[j];

                SerializableRange vertexRange = new(addedLinkVertexCount, link.VertexRange.Length);
                SerializableRange edgeRange = new(addedLinkEdgeCount, link.EdgeRange.Length);
                SerializableRange triangleRange = new(addedLinkTriangleCount, link.TriangleRange.Length);

                for (int k = 0; k < link.VertexRange.Length; k++) {
                    int oldVertexIndex = regionLinkData.InternalLinkVertices[link.VertexRange.Start + k];
                    int newVertexIndex = reducedIndexMap[oldVertexIndex];
                    allLinkVertices[addedLinkVertexCount++] = newVertexIndex;
                }

                for (int k = 0; k < link.EdgeRange.Length; k++) {
                    Edge oldEdge = regionLinkData.InternalLinkEdges[link.EdgeRange.Start + k];
                    int2 newEdge = new(reducedIndexMap[oldEdge.Vertex1], reducedIndexMap[oldEdge.Vertex2]);
                    allLinkEdges[addedLinkEdgeCount++] = newEdge;
                }

                for (int k = 0; k < link.TriangleRange.Length; k++) {
                    Triangle oldTriangle = regionLinkData.InternalLinkTriangles[link.TriangleRange.Start + k];
                    int3 newTriangle = new(reducedIndexMap[oldTriangle.Vertex1],
                                           reducedIndexMap[oldTriangle.Vertex2],
                                           reducedIndexMap[oldTriangle.Vertex3]);
                    allLinkTriangles[addedLinkTriangleCount++] = newTriangle;
                }

                NativeNavVolumeInternalLinkData copy = new(link.ToRegion, vertexRange, edgeRange, triangleRange);
                allLinks[addedLinkCount++] = copy;
            }

            return linkRange;
        }

        private SerializableRange CopyRegionBoundPlanes(int newRegionId,
                                                        ref UnsafeArray<NativePlane> allBoundPlanes,
                                                        ref int addedBoundCount) {
            UnsafeList<NativePlane> boundPlanes = BoundPlanes[newRegionId];
            SerializableRange planeRange = new(addedBoundCount, boundPlanes.Length);

            for (int j = 0; j < boundPlanes.Length; j++) {
                allBoundPlanes[addedBoundCount++] = boundPlanes[j];
            }

            return planeRange;
        }

        private SerializableRange CopyRegionIndices(int oldRegionID,
                                                    ref UnsafeHashMap<int, int> reducedIndexMap,
                                                    ref UnsafeArray<int> allVertexIndices, ref int addedIndexCount,
                                                    out NativeBounds bounds) {
            UnsafeList<int> triangleList = Mesh.RegionTriangleLists[oldRegionID];
            SerializableRange indexRange = new(addedIndexCount, 0);
            bounds = default;
            bool boundsSet = false;

            for (int j = 0; j < triangleList.Length; j++) {
                int index = triangleList[j];
                if (index < 0) continue;

                int reducedIndex = reducedIndexMap[index];

                allVertexIndices[addedIndexCount++] = reducedIndex;

                if (!boundsSet) {
                    bounds = new NativeBounds(Mesh.Vertices[index], float4.zero);
                    boundsSet = true;
                } else {
                    bounds.Encapsulate(Mesh.Vertices[index]);
                }
            }

            indexRange.Length = addedIndexCount - indexRange.Start;
            return indexRange;
        }

        private int CopyBlockingIndices(ref UnsafeHashMap<int, int> reducedIndexMap,
                                        ref UnsafeArray<int> allVertexIndices) {
            int blockingIndexCount = 0;
            UnsafeList<int> blockingVertexIndices = Mesh.RegionTriangleLists[0];
            for (int i = 0; i < blockingVertexIndices.Length; i++) {
                int index = blockingVertexIndices[i];
                if (index < 0) continue;

                int reducedIndex = reducedIndexMap[index];
                allVertexIndices[blockingIndexCount++] = reducedIndex;
            }

            return blockingIndexCount;
        }

        private int CountBoundPlanes() {
            int planeCount = 0;
            for (int i = 0; i < BoundPlanes.Length; i++) {
                planeCount += BoundPlanes[i].Length;
            }

            return planeCount;
        }

        private int CountLinks(out int linkVertexCount, out int linkEdgeCount, out int linkTriangleCount) {
            // Determine number of internal links and their components.
            int linkCount = 0;
            linkVertexCount = 0;
            linkEdgeCount = 0;
            linkTriangleCount = 0;
            for (int i = 1; i < Mesh.RegionTriangleLists.Length; i++) {
                CalculateInternalLinksJob.RegionOutput regionLinkData = LinkData[i - 1];
                int regionLinkCount = regionLinkData.InternalLinks.Length;

                linkCount += regionLinkCount;
                for (int j = 0; j < regionLinkCount; j++) {
                    NativeNavVolumeInternalLinkData link = regionLinkData.InternalLinks[j];
                    linkVertexCount += link.VertexRange.Length;
                    linkEdgeCount += link.EdgeRange.Length;
                    linkTriangleCount += link.TriangleRange.Length;
                }
            }

            return linkCount;
        }

        private UnsafeHashMap<int, int> CountVerticesAndBuildMapping(out int indexCount, out int vertexCount) {
            indexCount = 0;
            vertexCount = 0;
            UnsafeHashMap<int, int> reducedIndexMap = new(Mesh.Vertices.Length, Allocator.Temp);

            // Determine number of used vertices and non-negative indices.
            // Build mapping from old index -> new index.
            for (int i = 0; i < Mesh.RegionTriangleLists.Length; i++) {
                UnsafeList<int> triangleList = Mesh.RegionTriangleLists[i];
                for (int j = 0; j < triangleList.Length; j++) {
                    int index = triangleList[j];
                    if (index < 0) continue;

                    indexCount++;
                    if (reducedIndexMap.ContainsKey(index)) continue;
                    reducedIndexMap.Add(index, vertexCount++);
                }
            }

            if (indexCount % 3 != 0) throw new System.Exception("Triangle list is not a multiple of 3");
            return reducedIndexMap;
        }

        private UnsafeArray<float4> CopyVerticesToArray(int vertexCount, ref UnsafeHashMap<int, int> reducedIndexMap) {
            UnsafeArray<float4> allVertices = new(vertexCount, Allocator.Persistent);
            foreach (KVPair<int, int> pair in reducedIndexMap) {
                allVertices[pair.Value] = Mesh.Vertices[pair.Key];
            }

            return allVertices;
        }
    }
}
