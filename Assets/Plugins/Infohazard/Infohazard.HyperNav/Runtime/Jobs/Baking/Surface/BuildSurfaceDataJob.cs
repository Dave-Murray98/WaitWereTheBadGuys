// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct BuildSurfaceDataJob : IJob {
        public BakingNavSurfaceMeshInfo Mesh;
        public NativeArray<CalculateSurfaceInternalLinksJob.RegionOutput> LinkData;
        public NativeArray<int> VertexIslandIndices;
        public float4x4 InverseTransform;

        public NativeReference<NativeNavSurfaceData> OutputData;

        public void Execute() {
            // Count vertices and build mapping from old index -> new index.
            UnsafeArray<int> reducedIndexMap = CountVerticesAndBuildMapping(out int indexCount, out int vertexCount);

            // Create array for all vertices.
            UnsafeArray<float4> allVertices = CopyVerticesToArray(vertexCount, ref reducedIndexMap);

            // Allocate array for all vertex indices.
            UnsafeArray<int> allVertexIndices = new(indexCount, Allocator.Persistent);

            // Count internal links and their components.
            int linkCount = CountLinks(out int linkVertexCount, out int linkEdgeCount);

            // Allocate arrays for internal links.
            UnsafeArray<NativeNavSurfaceInternalLinkData> allLinks = new(linkCount, Allocator.Persistent);
            UnsafeArray<int> allLinkVertices = new(linkVertexCount, Allocator.Persistent);
            UnsafeArray<int2> allLinkEdges = new(linkEdgeCount, Allocator.Persistent);

            // Create region data.
            UnsafeArray<NativeNavSurfaceRegionData>
                regions = new(Mesh.GroupTriangles.Length, Allocator.Persistent);
            CreateRegionData(ref reducedIndexMap, ref regions, ref allVertexIndices,
                             ref allLinkVertices, ref allLinkEdges, ref allLinks);

            reducedIndexMap.Dispose();

            OutputData.Value = new NativeNavSurfaceData(
                -1,
                default,
                default,
                default,
                NavLayer.None,
                allVertices,
                regions,
                allVertexIndices,
                allLinks,
                default,
                allLinkVertices,
                allLinkEdges);
        }

        private void CreateRegionData(
            ref UnsafeArray<int> reducedIndexMap,
            ref UnsafeArray<NativeNavSurfaceRegionData> regions,
            ref UnsafeArray<int> allVertexIndices,
            ref UnsafeArray<int> allLinkVertices,
            ref UnsafeArray<int2> allLinkEdges,
            ref UnsafeArray<NativeNavSurfaceInternalLinkData> allLinks) {

            int addedIndexCount = 0;
            int addedLinkCount = 0;
            int addedLinkVertexCount = 0;
            int addedLinkEdgeCount = 0;

            for (int i = 0; i < Mesh.GroupTriangles.Length; i++) {
                // Add triangle indices.
                SerializableRange indexRange = CopyRegionIndices(i, ref reducedIndexMap, ref allVertexIndices,
                                                                 ref addedIndexCount, out NativeBounds bounds);

                // Add internal links.
                SerializableRange linkRange = CopyRegionLinks(i, ref reducedIndexMap, ref allLinkVertices,
                    ref allLinkEdges, ref allLinks, ref addedLinkCount, ref addedLinkVertexCount,
                    ref addedLinkEdgeCount);

                float4 upDirection = float4.zero;
                float4 normalVector = float4.zero;
                HybridIntList tris = Mesh.GroupTriangles[i];
                for (int j = 0; j < tris.Count; j++) {
                    int triIndex = tris[j] / 3;
                    float4 triUp = Mesh.TriangleUprightWorldDirections[triIndex];
                    float4 triNormal = Mesh.TriangleNormals[triIndex];
                    upDirection += triUp;
                    normalVector += triNormal;
                }

                upDirection = math.mul(InverseTransform, upDirection);
                upDirection = math.normalize(upDirection);

                normalVector = math.normalize(normalVector);

                int islandIndex = VertexIslandIndices[Mesh.TriangleList[tris[0]]];

                regions[i] =
                    new NativeNavSurfaceRegionData(i, bounds, indexRange, linkRange, default, upDirection, normalVector,
                                                   islandIndex);
            }
        }

        private SerializableRange CopyRegionLinks(int newRegionID, ref UnsafeArray<int> reducedIndexMap,
                                                  ref UnsafeArray<int> allLinkVertices,
                                                  ref UnsafeArray<int2> allLinkEdges,
                                                  ref UnsafeArray<NativeNavSurfaceInternalLinkData> allLinks,
                                                  ref int addedLinkCount,
                                                  ref int addedLinkVertexCount,
                                                  ref int addedLinkEdgeCount) {
            CalculateSurfaceInternalLinksJob.RegionOutput regionLinkData = LinkData[newRegionID];
            SerializableRange linkRange = new(addedLinkCount, regionLinkData.InternalLinks.Length);

            for (int j = 0; j < regionLinkData.InternalLinks.Length; j++) {
                NativeNavSurfaceInternalLinkData link = regionLinkData.InternalLinks[j];

                SerializableRange vertexRange = new(addedLinkVertexCount, link.VertexRange.Length);
                SerializableRange edgeRange = new(addedLinkEdgeCount, link.EdgeRange.Length);

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

                NativeNavSurfaceInternalLinkData copy = new(link.ToRegion, vertexRange, edgeRange);
                allLinks[addedLinkCount++] = copy;
            }

            return linkRange;
        }

        private SerializableRange CopyRegionIndices(int oldRegionID,
                                                    ref UnsafeArray<int> reducedIndexMap,
                                                    ref UnsafeArray<int> allVertexIndices,
                                                    ref int addedIndexCount,
                                                    out NativeBounds bounds) {
            HybridIntList trianglesForRegion = Mesh.GroupTriangles[oldRegionID];
            SerializableRange indexRange = new(addedIndexCount, 0);
            bounds = default;
            bool boundsSet = false;

            for (int i = 0; i < trianglesForRegion.Count; i++) {
                int triStart = trianglesForRegion[i];

                for (int j = 0; j < 3; j++) {
                    int index = Mesh.TriangleList[triStart + j];
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
            }

            indexRange.Length = addedIndexCount - indexRange.Start;
            return indexRange;
        }

        private int CountLinks(out int linkVertexCount, out int linkEdgeCount) {
            // Determine number of internal links and their components.
            int linkCount = 0;
            linkVertexCount = 0;
            linkEdgeCount = 0;
            for (int i = 0; i < Mesh.GroupTriangles.Length; i++) {
                CalculateSurfaceInternalLinksJob.RegionOutput regionLinkData = LinkData[i];
                int regionLinkCount = regionLinkData.InternalLinks.Length;

                linkCount += regionLinkCount;
                for (int j = 0; j < regionLinkCount; j++) {
                    NativeNavSurfaceInternalLinkData link = regionLinkData.InternalLinks[j];
                    linkVertexCount += link.VertexRange.Length;
                    linkEdgeCount += link.EdgeRange.Length;
                }
            }

            return linkCount;
        }

        private unsafe UnsafeArray<int> CountVerticesAndBuildMapping(out int indexCount, out int vertexCount) {
            indexCount = 0;
            vertexCount = 0;
            UnsafeArray<int> reducedIndexMap = new(Mesh.Vertices.Length, Allocator.Temp);

            // Set all values to -1.
            UnsafeUtility.MemSet((void*)reducedIndexMap.Pointer, 0xFF, reducedIndexMap.Length * sizeof(int));

            // Determine number of used vertices and non-negative indices.
            // Build mapping from old index -> new index.
            for (int i = 0; i < Mesh.TriangleList.Length; i++) {
                int index = Mesh.TriangleList[i];
                if (index < 0) continue;

                indexCount++;

                if (reducedIndexMap[index] >= 0) continue;
                reducedIndexMap[index] = vertexCount++;
            }

            if (indexCount % 3 != 0) {
                Debug.LogError("Triangle list length is not a multiple of 3.");
            }

            return reducedIndexMap;
        }

        private UnsafeArray<float4> CopyVerticesToArray(int vertexCount, ref UnsafeArray<int> reducedIndexMap) {
            UnsafeArray<float4> allVertices = new(vertexCount, Allocator.Persistent);
            for (int i = 0; i < Mesh.Vertices.Length; i++) {
                int reducedIndex = reducedIndexMap[i];
                if (reducedIndex < 0) continue;

                allVertices[reducedIndex] = Mesh.Vertices[i];
            }

            return allVertices;
        }
    }
}
