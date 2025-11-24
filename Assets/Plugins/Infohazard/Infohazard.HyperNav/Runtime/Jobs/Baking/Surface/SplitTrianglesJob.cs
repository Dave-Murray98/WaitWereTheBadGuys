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
using UnityEngine.Serialization;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct SplitTrianglesJob : IJob {
        public BakingNavSurfaceMeshInfo Mesh;

        public NativeHashSet<int> TrianglesToSplit;

        public NativeList<int> OutNewTrianglesToTest;
        public NativeList<int> OutNewVerticesToTest;

        public void Execute() {
            UnsafeHashMap<Edge, int2> edgeOccurrenceMap = new(Mesh.Vertices.Length * 16, Allocator.Temp);
            for (int i = 0; i < Mesh.TriangleList.Length; i += 3) {
                if (Mesh.TriangleList[i] < 0) continue;

                for (int j = 0; j < 3; j++) {
                    int index1 = Mesh.TriangleList[i + j];
                    int index2 = Mesh.TriangleList[i + (j + 1) % 3];

                    Edge edge = new(index1, index2);
                    if (!edgeOccurrenceMap.TryGetValue(edge, out int2 curVal)) {
                        edgeOccurrenceMap.Add(edge, new int2(i, -1));
                    } else if (curVal.y < 0) {
                        edgeOccurrenceMap[edge] = new int2(curVal.x, i);
                    } else {
                        Debug.LogError(
                            $"Edge {index1}, {index2} has more than two neighbors {curVal.x}, {curVal.y}, {i}.");
                    }
                }
            }

            UnsafeHashMap<Edge, int> newVertices = new(TrianglesToSplit.Count * 3, Allocator.Temp);
            UnsafeHashSet<int> affectedTriangles = new(TrianglesToSplit.Count * 3, Allocator.Temp);

            foreach (int triStart in TrianglesToSplit) {
                if (Mesh.TriangleList[triStart] < 0) continue;

                for (int i = 0; i < 3; i++) {
                    int edgeIndex1 = Mesh.TriangleList[triStart + i];
                    int edgeIndex2 = Mesh.TriangleList[triStart + (i + 1) % 3];
                    Edge edge = new(edgeIndex1, edgeIndex2);

                    if (!newVertices.TryGetValue(edge, out int newIndex)) {
                        newIndex = Mesh.Vertices.Length;
                        newVertices.Add(edge, newIndex);

                        float4 vertex1 = Mesh.Vertices[edgeIndex1];
                        float4 vertex2 = Mesh.Vertices[edgeIndex2];
                        float4 newVertex = (vertex1 + vertex2) * 0.5f;
                        Mesh.Vertices.Add(newVertex);
                        Mesh.VertexConnections.Add(new HybridIntList(Allocator.Persistent));
                        Mesh.VertexTriangles.Add(new HybridIntList(Allocator.Persistent));

                        if (!edgeOccurrenceMap.TryGetValue(edge, out int2 edgeOccurrence)) {
                            Debug.LogError("Edge not found in edgeOccurrenceMap.");
                            continue;
                        }

                        int otherTriStart = edgeOccurrence.x == triStart ? edgeOccurrence.y : edgeOccurrence.x;

                        if (otherTriStart >= 0 &&
                            Mesh.TriangleList[otherTriStart] >= 0 &&
                            !TrianglesToSplit.Contains(otherTriStart)) {
                            affectedTriangles.Add(otherTriStart);
                        }
                    }
                }

                // Split the triangle into four triangles.
                int index1 = Mesh.TriangleList[triStart + 0];
                int index2 = Mesh.TriangleList[triStart + 1];
                int index3 = Mesh.TriangleList[triStart + 2];

                Edge edge1 = new(index1, index2);
                Edge edge2 = new(index2, index3);
                Edge edge3 = new(index3, index1);

                int splitIndex1 = newVertices[edge1];
                int splitIndex2 = newVertices[edge2];
                int splitIndex3 = newVertices[edge3];

                SplitTriangleOnAllEdges(triStart, index1, index2, index3, splitIndex1, splitIndex2, splitIndex3);
            }

            foreach (int triStart in affectedTriangles) {
                OutNewTrianglesToTest.Add(triStart);

                int index1 = Mesh.TriangleList[triStart + 0];
                int index2 = Mesh.TriangleList[triStart + 1];
                int index3 = Mesh.TriangleList[triStart + 2];

                bool edge1Split = newVertices.TryGetValue(new Edge(index1, index2), out int splitIndex1);
                bool edge2Split = newVertices.TryGetValue(new Edge(index2, index3), out int splitIndex2);
                bool edge3Split = newVertices.TryGetValue(new Edge(index3, index1), out int splitIndex3);

                if (edge1Split && edge2Split && edge3Split) {
                    SplitTriangleOnAllEdges(triStart, index1, index2, index3, splitIndex1, splitIndex2, splitIndex3);
                } else if (edge1Split && edge2Split) {
                    SplitTriangleOnTwoEdges(triStart, index2, index3, index1, splitIndex1, splitIndex2);
                } else if (edge2Split && edge3Split) {
                    SplitTriangleOnTwoEdges(triStart, index3, index1, index2, splitIndex2, splitIndex3);
                } else if (edge3Split && edge1Split) {
                    SplitTriangleOnTwoEdges(triStart, index1, index2, index3, splitIndex3, splitIndex1);
                } else if (edge1Split) {
                    SplitTriangleOnOneEdge(triStart, index3, index1, index2, splitIndex1);
                } else if (edge2Split) {
                    SplitTriangleOnOneEdge(triStart, index1, index2, index3, splitIndex2);
                } else if (edge3Split) {
                    SplitTriangleOnOneEdge(triStart, index2, index3, index1, splitIndex3);
                } else {
                    Debug.LogError("No edges split.");
                }
            }

            UnsafeArray<int> vertexIndexToQueryIndex = new(Mesh.Vertices.Length, Allocator.Temp);
            OutNewVerticesToTest.Length = 0;

            unsafe {
                UnsafeUtility.MemSet((void*)vertexIndexToQueryIndex.Pointer, 0xff, Mesh.Vertices.Length * sizeof(int));
            }

            foreach (int triStart in OutNewTrianglesToTest) {
                for (int i = 0; i < 3; i++) {
                    int vertexIndex = Mesh.TriangleList[triStart + i];
                    if (vertexIndexToQueryIndex[vertexIndex] >= 0) continue;

                    vertexIndexToQueryIndex[vertexIndex] = OutNewVerticesToTest.Length;
                    OutNewVerticesToTest.Add(vertexIndex);
                }
            }
        }

        private void SplitTriangleOnAllEdges(int triStart, int index1, int index2, int index3,
                                             int splitIndex1, int splitIndex2, int splitIndex3) {
            OutNewTrianglesToTest.Add(triStart);
            int triIndex = triStart / 3;
            float4 normal = Mesh.TriangleNormals[triIndex];

            // Replace original triangle with center triangle.
            Mesh.TriangleList[triStart + 0] = splitIndex1;
            Mesh.TriangleList[triStart + 1] = splitIndex2;
            Mesh.TriangleList[triStart + 2] = splitIndex3;

            // Add triangle between first vertex and center triangle.
            OutNewTrianglesToTest.Add(Mesh.TriangleList.Length);
            Mesh.TriangleList.Add(index1);
            Mesh.TriangleList.Add(splitIndex1);
            Mesh.TriangleList.Add(splitIndex3);
            Mesh.TriangleNormals.Add(normal);

            // Add triangle between second vertex and center triangle.
            OutNewTrianglesToTest.Add(Mesh.TriangleList.Length);
            Mesh.TriangleList.Add(index2);
            Mesh.TriangleList.Add(splitIndex2);
            Mesh.TriangleList.Add(splitIndex1);
            Mesh.TriangleNormals.Add(normal);

            // Add triangle between third vertex and center triangle.
            OutNewTrianglesToTest.Add(Mesh.TriangleList.Length);
            Mesh.TriangleList.Add(index3);
            Mesh.TriangleList.Add(splitIndex3);
            Mesh.TriangleList.Add(splitIndex2);
            Mesh.TriangleNormals.Add(normal);
        }

        private void SplitTriangleOnTwoEdges(int triStart, int indexBetweenSplitEdges, int index2, int index3,
                                             int splitIndex1, int splitIndex2) {
            OutNewTrianglesToTest.Add(triStart);
            int triIndex = triStart / 3;
            float4 normal = Mesh.TriangleNormals[triIndex];

            // Replace original triangle connecting vertex1 to the two split vertices.
            Mesh.TriangleList[triStart + 0] = splitIndex1;
            Mesh.TriangleList[triStart + 1] = indexBetweenSplitEdges;
            Mesh.TriangleList[triStart + 2] = splitIndex2;

            // After splitting off the first triangle, we're left with a quad.
            // Split the quad into two triangles, along the shortest diagonal.
            float d1 = math.distancesq(Mesh.Vertices[index2], Mesh.Vertices[splitIndex1]);
            float d2 = math.distancesq(Mesh.Vertices[index3], Mesh.Vertices[splitIndex2]);

            int3 tri1;
            int3 tri2;

            if (d1 > d2) {
                tri1 = new int3(splitIndex2, index2, index3);
                tri2 = new int3(splitIndex1, splitIndex2, index3);
            } else {
                tri1 = new int3(splitIndex1, splitIndex2, index2);
                tri2 = new int3(splitIndex1, index2, index3);
            }

            // Add the two new triangles.
            OutNewTrianglesToTest.Add(Mesh.TriangleList.Length);
            Mesh.TriangleList.Add(tri1.x);
            Mesh.TriangleList.Add(tri1.y);
            Mesh.TriangleList.Add(tri1.z);
            Mesh.TriangleNormals.Add(normal);

            OutNewTrianglesToTest.Add(Mesh.TriangleList.Length);
            Mesh.TriangleList.Add(tri2.x);
            Mesh.TriangleList.Add(tri2.y);
            Mesh.TriangleList.Add(tri2.z);
            Mesh.TriangleNormals.Add(normal);
        }

        private void SplitTriangleOnOneEdge(int triStart, int indexOppositeSplitEdge, int index2, int index3,
                                            int splitIndex) {
            OutNewTrianglesToTest.Add(triStart);
            int triIndex = triStart / 3;
            float4 normal = Mesh.TriangleNormals[triIndex];

            // Replace original triangle connecting vertex1, vertex2, and split index.
            Mesh.TriangleList[triStart + 0] = indexOppositeSplitEdge;
            Mesh.TriangleList[triStart + 1] = index2;
            Mesh.TriangleList[triStart + 2] = splitIndex;

            // Add second triangle connecting vertex1, split index, and vertex3.
            OutNewTrianglesToTest.Add(Mesh.TriangleList.Length);
            Mesh.TriangleList.Add(indexOppositeSplitEdge);
            Mesh.TriangleList.Add(splitIndex);
            Mesh.TriangleList.Add(index3);
            Mesh.TriangleNormals.Add(normal);
        }
    }
}