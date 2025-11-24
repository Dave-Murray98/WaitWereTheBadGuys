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

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public unsafe struct DecimateRegionsJob : IJobParallelForBatch {
        [ReadOnly] public NativeList<float4> Vertices;
        [ReadOnly] public NativeList<HybridIntList> VertexConnections;
        [ReadOnly] public NativeList<IntList2> VertexRegionMembership;
        [ReadOnly] public NativeParallelHashMap<Triangle, TriangleRegionIndices> TriangleIndicesPerRegion;

        public NativeCancellationToken CancellationToken;

        public NativeArray<UnsafeList<int>> RegionTriangleLists;

        public float4x4 VolumeTransform;

        public void Execute(int index, int count) {
            int estTriCountPerRegion = TriangleIndicesPerRegion.Count() / RegionTriangleLists.Length;
            const int estFacesPerRegion = 24;
            int estTriCountPerFace = estTriCountPerRegion / estFacesPerRegion;
            int estSharpEdgeCountPerFace = (int) (4 * math.sqrt(estTriCountPerFace));
            int estSharpEdgeCount = estSharpEdgeCountPerFace * estFacesPerRegion;

            SingleThread thread = new() {
                // Parameters
                VolumeTransform = VolumeTransform,
                CancellationToken = CancellationToken,

                // Structures used to store temporary data.
                VertexOrder = new UnsafeList<int>(16, Allocator.Temp),
                FlatVertices = new UnsafeHashSet<int>(16, Allocator.Temp),
                ConcaveVertices = new UnsafeHashSet<int>(16, Allocator.Temp),
                SharpEdges = new UnsafeHashSet<Edge>(estSharpEdgeCount * 2, Allocator.Persistent),

                // Values from Mesh that are not copied.
                Vertices = new ReadOnlyArrayPointer<float4>(Vertices.GetUnsafeReadOnlyPtr(),
                                                            Vertices.Length),
                VertexRegionMembership =
                    new ReadOnlyArrayPointer<IntList2>(VertexRegionMembership.GetUnsafeReadOnlyPtr(),
                                                       VertexRegionMembership.Length),
                VertexConnections =
                    new UnsafeArray<UnsafeList<int>>(VertexConnections.Length, Allocator.Persistent, true),
                TriangleIndicesInCurrentRegion =
                    new UnsafeHashMap<Triangle, int>(estTriCountPerRegion * 2, Allocator.Temp),
            };

            try {
                for (int i = 0; i < count; i++) {
                    ExecuteInternal(index + i, ref thread);

                    if (CancellationToken.IsCancellationRequested) {
                        break;
                    }
                }
            } finally {
                thread.VertexOrder.Dispose();
                thread.FlatVertices.Dispose();
                thread.ConcaveVertices.Dispose();
                thread.SharpEdges.Dispose();
                thread.VertexConnections.Dispose();
                thread.TriangleIndicesInCurrentRegion.Dispose();
            }
        }

        private bool ExecuteInternal(int index, ref SingleThread thread) {
            thread.RegionIndex = index;
            thread.TriangleList = RegionTriangleLists[index];

            // Copy data for the thread.
            for (int i = 0; i < VertexConnections.Length; i++) {
                HybridIntList connections = VertexConnections[i];

                // Only copy lists for vertices in the region or connected to a vertex in the region.
                bool connectedToRegion = VertexRegionMembership[i].Contains(index);
                if (!connectedToRegion) {
                    for (int j = 0; j < connections.Count; j++) {
                        if (!VertexRegionMembership[connections[j]].Contains(index)) continue;
                        connectedToRegion = true;
                        break;
                    }
                }

                if (connectedToRegion) {
                    UnsafeList<int> existingList = thread.VertexConnections[i];
                    if (!existingList.IsCreated) {
                        existingList = new UnsafeList<int>(connections.Count, Allocator.Temp);
                    }

                    existingList.Clear();
                    for (int j = 0; j < connections.Count; j++) {
                        existingList.Add(connections[j]);
                    }

                    thread.VertexConnections[i] = existingList;
                }
            }

            thread.TriangleIndicesInCurrentRegion.Clear();
            foreach (KeyValue<Triangle, TriangleRegionIndices> pair in TriangleIndicesPerRegion) {
                if (!pair.Value.TryGetValue(index, out int triangleIndex)) continue;
                thread.TriangleIndicesInCurrentRegion[pair.Key] = triangleIndex;
            }

            // Need to reset this to default here. If this is not done and an exception occurs in the job,
            // it will crash Unity.
            RegionTriangleLists[index] = default;

            try {
                thread.BuildSharpEdgeData(TriangleIndicesPerRegion);
                return thread.Execute();
            } finally {
                // Copy index list (the memory is already modified but not the length).
                RegionTriangleLists[index] = thread.TriangleList;
            }
        }

        // Used to isolate the work of a single thread
        private struct SingleThread {
            // Parameters
            public float4x4 VolumeTransform;
            public int RegionIndex;
            public NativeCancellationToken CancellationToken;

            // Structures used to store temporary data.
            public UnsafeList<int> VertexOrder;
            public UnsafeHashSet<int> FlatVertices;
            public UnsafeHashSet<int> ConcaveVertices;
            public UnsafeHashSet<Edge> SharpEdges;

            // Values from Mesh that are not copied.
            public ReadOnlyArrayPointer<float4> Vertices;
            public ReadOnlyArrayPointer<IntList2> VertexRegionMembership;
            public UnsafeList<int> TriangleList;

            // Values from Mesh that are copied.
            public UnsafeArray<UnsafeList<int>> VertexConnections;
            public UnsafeHashMap<Triangle, int> TriangleIndicesInCurrentRegion;

            public void BuildSharpEdgeData(
                NativeParallelHashMap<Triangle, TriangleRegionIndices> triangleIndicesPerRegion) {
                for (int vertex = 0; vertex < VertexConnections.Length; vertex++) {
                    bool aInRegion = VertexRegionMembership[vertex].Contains(RegionIndex);

                    UnsafeList<int> connections = VertexConnections[vertex];
                    for (int j = 0; j < connections.Length; j++) {
                        int connectedVertex = connections[j];

                        if (connectedVertex <= vertex) continue;

                        bool bInRegion = VertexRegionMembership[connectedVertex].Contains(RegionIndex);
                        if (!aInRegion && !bInRegion) continue;

                        if (IsEdgeAngled(vertex, connectedVertex, triangleIndicesPerRegion)) {
                            SharpEdges.Add(new Edge(vertex, connectedVertex));
                        }
                    }
                }
            }

            public bool Execute() {
                // We must loop through vertices in their index order to ensure each thread gets the same results.
                for (int i = 0; i < Vertices.Length; i++) {
                    if (!VertexRegionMembership[i].Contains(RegionIndex)) continue;

                    bool result = RemoveVertexIfPossible(i);
                    if (!result) return false;
                }

                return true;
            }

            // Note: returns false only if the job is unable to complete due to an error.
            private bool RemoveVertexIfPossible(int vertexIndex) {
                // First, determine if the vertex lies on at least one sharp edge.
                // If there is one sharp edge, there should be at least one more.
                // When that is the case, both sharp edges will need to be known later on.
                int sharpEdgeCount = GetSharpEdgeCount(vertexIndex, out int firstSharpEdge, out int secondSharpEdge);

                if (vertexIndex == 9356 && sharpEdgeCount == 1) {
                    return false;
                }

                // A vertex can be removed if it has no sharp edges or exactly two.
                // Any other number of sharp edges, and it is on a corner and is needed.
                if (sharpEdgeCount != 0 && sharpEdgeCount != 2) return true;

                UnsafeList<int> vertexConnections = VertexConnections[vertexIndex];

                if (sharpEdgeCount == 0 ||
                    !AreVerticesConnectedInCurrentRegion(vertexIndex, firstSharpEdge) ||
                    !AreVerticesConnectedInCurrentRegion(vertexIndex, secondSharpEdge)) {
                    // Case 1 - no sharp edges: just remove all triangles that connect to the vertex.
                    // Build an edge loop that contains all the newly loose edges.
                    // Then fill in triangles using the ear clipping algorithm.

                    int firstConnectedVertex = -1;
                    for (int k = 0; k < vertexConnections.Length; k++) {
                        int v2 = vertexConnections[k];
                        if (!AreVerticesConnectedInCurrentRegion(vertexIndex, v2)) {
                            continue;
                        }

                        firstConnectedVertex = v2;
                        break;
                    }

                    if (firstConnectedVertex == -1) {
                        Debug.LogError($"No connected vertex found for index {vertexIndex}.");
                        return false;
                    }

                    if (!RemoveVertexTriangles(vertexIndex, firstConnectedVertex, -1)) return false;
                } else {
                    // Case 2 - 2 sharp edges: same as case 1, but ensure that the two sharp edges
                    // remain as edges in the newly created triangles. We do this by running ear clipping
                    // twice, once for each side of these edges.

                    if (!RemoveVertexTriangles(vertexIndex, firstSharpEdge, secondSharpEdge)) return false;
                    if (!RemoveVertexTriangles(vertexIndex, secondSharpEdge, firstSharpEdge)) return false;

                    // Register the new edge as sharp and remove the old sharp edges.
                    SharpEdges.Add(new Edge(firstSharpEdge, secondSharpEdge));
                    SharpEdges.Remove(new Edge(vertexIndex, firstSharpEdge));
                    SharpEdges.Remove(new Edge(vertexIndex, secondSharpEdge));
                }

                // Remove vertex connection data for removed vertex.
                for (int j = 0; j < vertexConnections.Length; j++) {
                    int connectedVertex = vertexConnections[j];
                    UnsafeList<int> connectedVertexConnections = VertexConnections[connectedVertex];
                    int index = connectedVertexConnections.IndexOf(vertexIndex);
                    if (index == -1) continue;

                    connectedVertexConnections.RemoveAtSwapBack(index);
                    VertexConnections[connectedVertex] = connectedVertexConnections;
                }

                VertexConnections[vertexIndex].Dispose();
                VertexConnections[vertexIndex] = default;

                return true;
            }

            private int GetSharpEdgeCount(int i, out int firstSharpEdge, out int secondSharpEdge) {
                int sharpEdgeCount = 0;
                firstSharpEdge = -1;
                secondSharpEdge = -1;

                UnsafeList<int> connections = VertexConnections[i];
                for (int j = 0; j < connections.Length; j++) {
                    int vertex2Index = connections[j];

                    if (!SharpEdges.Contains(new Edge(i, vertex2Index))) continue;

                    if (sharpEdgeCount == 0) {
                        firstSharpEdge = vertex2Index;
                    } else if (sharpEdgeCount == 1) {
                        secondSharpEdge = vertex2Index;
                    }

                    sharpEdgeCount++;
                }

                return sharpEdgeCount;
            }

            // Check whether the given region contains a triangle containing the two given vertices.
            private bool AreVerticesConnectedInCurrentRegion(int vertex1, int vertex2) {
                UnsafeList<int> vertex1Connections = VertexConnections[vertex1];
                UnsafeList<int> vertex2Connections = VertexConnections[vertex2];

                for (int i = 0; i < vertex1Connections.Length; i++) {
                    int otherVertex = vertex1Connections[i];
                    if (!vertex2Connections.Contains(otherVertex)) continue;

                    Triangle triangle = new(vertex1, vertex2, otherVertex);
                    if (TriangleIndicesInCurrentRegion.ContainsKey(triangle)) {
                        return true;
                    }
                }

                return false;
            }

            // Remove the triangles in the given region that include the given vertex.
            // This creates a hole, which is then filled using the Ear Clipping algorithm.
            // The result is that the mesh no longer has any triangles containing that vertex,
            // but the overall shape is unchanged.
            private bool RemoveVertexTriangles(int vertexIndex, int firstVertex, int lastVertex) {
                // Delete the triangles and get a list of the hole vertices.
                if (!RemoveTrianglesAndGetEdgeRing(vertexIndex, firstVertex, lastVertex, ref VertexOrder)) return false;

                if (VertexOrder.Length < 3) {
                    DrawDebugVertexOrder(vertexIndex, VertexOrder);
                    Debug.LogError(
                        $"RemoveTrianglesAndGetEdgeRing returned vertex order with count < 3 when removing {vertexIndex} in region {RegionIndex}.");
                    return false;
                }

                float4 v = Vertices[vertexIndex];
                float4 v1 = Vertices[VertexOrder[0]];
                float4 v2 = Vertices[VertexOrder[1]];

                // Get the normal of the new triangles we are creating.
                float3 normal = math.normalize(math.cross((v1 - v).xyz, (v2 - v).xyz));

                // Ear clipping algorithm:
                // While there are enough points to make a triangle, choose the sharpest point and make a triangle
                // with that point and its neighbors, then remove that point from the list and repeat.
                // Avoid getting into a situation where the only triangles we can create are slivers.
                while (VertexOrder.Length >= 3) {
                    // Find any vertices which, if clipped, would create a sliver triangle.
                    // These are any vertices with an angle close to 180 degrees.
                    FlatVertices.Clear();
                    ConcaveVertices.Clear();
                    for (int j = 0; j < VertexOrder.Length; j++) {
                        GetCurrentAndNeighboringVertexIndices(VertexOrder, j, out int curVertexIndex,
                                                              out int prevVertexIndex, out int nextVertexIndex);

                        float dot = GetDotAndCrossProduct(curVertexIndex, prevVertexIndex, nextVertexIndex,
                                                          out float3 cross);
                        if (dot < -0.99999) {
                            FlatVertices.Add(curVertexIndex);
                            continue;
                        }

                        float crossDot = math.dot(cross, normal);
                        if (crossDot < 0) {
                            ConcaveVertices.Add(curVertexIndex);
                        }
                    }

                    // Give priority to vertices with a flat neighbor.
                    // Only check vertices without a flat neighbor if none of those can be clipped.
                    // This ensures that we get rid of vertices with flat neighbors as soon as possible,
                    // to prevent being left with sliver triangles.
                    int bestVertex =
                        FindVertexToClip(ref VertexOrder, ref FlatVertices, true, ref ConcaveVertices, normal);

                    if (bestVertex == -1) {
                        bestVertex = FindVertexToClip(ref VertexOrder, ref FlatVertices, false, ref ConcaveVertices,
                                                      normal);
                    }

                    if (bestVertex == -1) {
                        DrawDebugVertexOrder(vertexIndex, VertexOrder);
                        Debug.LogError(
                            $"Did not find vertex to clip while removing vertex {vertexIndex} for region {RegionIndex}.");
                        return false;
                    }

                    // Convert index in vertex order list to index in all vertices.
                    GetCurrentAndNeighboringVertexIndices(VertexOrder, bestVertex, out int newV1,
                                                          out int newV2, out int newV3);

                    // Create new triangle and initialize it in the mesh data.
                    Triangle newTriangle = new(newV1, newV2, newV3);

                    // Add triangle's index to cached values.
                    TriangleIndicesInCurrentRegion[newTriangle] = TriangleList.Length;

                    // Add indices so triangle is part of mesh.
                    TriangleList.Add(newV1);
                    TriangleList.Add(newV2);
                    TriangleList.Add(newV3);

                    // Add new vertex connections.
                    ConnectVertices(ref newV1, newV2);
                    ConnectVertices(ref newV2, newV3);
                    ConnectVertices(ref newV3, newV1);

                    VertexOrder.RemoveAt(bestVertex);
                }

                return true;
            }

            private int FindVertexToClip(ref UnsafeList<int> vertexOrder,
                                         ref UnsafeHashSet<int> flatVertices, bool checkFlatNeighbors,
                                         ref UnsafeHashSet<int> concaveVertices, Vector3 normal) {
                // Find the angle with the sharpest point.
                // Do not consider any nearly-flat angles.
                int bestVertex = -1;
                float bestDot = -1;
                for (int j = 0; j < vertexOrder.Length; j++) {
                    GetCurrentAndNeighboringVertexIndices(vertexOrder, j, out int curVertexIndex,
                                                          out int prevVertexIndex, out int nextVertexIndex);

                    if (VertexHasFlatNeighbor(ref flatVertices, prevVertexIndex, nextVertexIndex) !=
                        checkFlatNeighbors) {
                        continue;
                    }

                    if (!CanClipVertex(ref flatVertices, ref concaveVertices, normal,
                                       prevVertexIndex, curVertexIndex, nextVertexIndex)) {
                        continue;
                    }

                    // Want to clip the vertex with the highest dot product (the sharpest angle).
                    float dot = GetDotAndCrossProduct(curVertexIndex, prevVertexIndex, nextVertexIndex, out _);
                    if (dot > bestDot) {
                        bestVertex = j;
                        bestDot = dot;
                    }
                }

                return bestVertex;
            }

            private static bool VertexHasFlatNeighbor(ref UnsafeHashSet<int> flatVertices,
                                                      int prevVertexIndex, int nextVertexIndex) {
                return flatVertices.Contains(prevVertexIndex) || flatVertices.Contains(nextVertexIndex);
            }

            private bool CanClipVertex(ref UnsafeHashSet<int> flatVertices, ref UnsafeHashSet<int> concaveVertices,
                                       Vector3 normal, int prevVertexIndex, int curVertexIndex, int nextVertexIndex) {
                float4 vCur = Vertices[curVertexIndex];
                float4 vNext = Vertices[nextVertexIndex];
                float4 vPrev = Vertices[prevVertexIndex];

                if (flatVertices.Contains(curVertexIndex)) return false;

                // Concave vertices may be inside the clipped triangle, making it invalid
                if (concaveVertices.Count <= 0) return true;
                if (concaveVertices.Contains(curVertexIndex)) return false;

                foreach (int concaveVertexIndex in concaveVertices) {
                    if (concaveVertexIndex == prevVertexIndex || concaveVertexIndex == nextVertexIndex) continue;
                    float4 concaveVertex = Vertices[concaveVertexIndex];

                    if (MathUtility.IsPointInsideBound(vNext.xyz, vPrev.xyz, normal, concaveVertex.xyz) &&
                        MathUtility.IsPointInsideBound(vPrev.xyz, vCur.xyz, normal, concaveVertex.xyz) &&
                        MathUtility.IsPointInsideBound(vCur.xyz, vNext.xyz, normal, concaveVertex.xyz)) {
                        return false;
                    }
                }

                return true;
            }

            // Remove the triangles connecting to a given vertex and return a list of vertices that make up the new hole.
            // Sets sharedData.VertexOrder
            // Uses sharedData.VertexListForIntersect
            private bool RemoveTrianglesAndGetEdgeRing(int vertexIndex, int firstVertex, int lastVertex,
                                                       ref UnsafeList<int> vertexOrder) {
                int currentConnectedVertex = firstVertex;

                vertexOrder.Clear();
                while (true) {
                    vertexOrder.Add(currentConnectedVertex);
                    if (currentConnectedVertex == lastVertex) break;

                    // Search for a triangle that contains both the current edge ring vertex and the removing vertex.
                    // Use the intersection of both vertices' connections to find candidates for the third vertex
                    // in this triangle.
                    UnsafeList<int> vertex1Connections = VertexConnections[vertexIndex];
                    UnsafeList<int> vertex2Connections = VertexConnections[currentConnectedVertex];
                    bool foundNext = false;

                    for (int i = 0; i < vertex1Connections.Length; i++) {
                        int otherVertex = vertex1Connections[i];
                        if ((otherVertex != firstVertex && vertexOrder.Contains(otherVertex)) ||
                            !vertex2Connections.Contains(otherVertex)) {
                            continue;
                        }

                        Triangle triangle = new(vertexIndex, currentConnectedVertex, otherVertex);

                        if (!TriangleIndicesInCurrentRegion.TryGetValue(triangle, out int triangleIndex)) {
                            continue;
                        }

                        if (vertexOrder.Length < 2 && otherVertex == lastVertex) {
                            Debug.LogError(
                                $"Found last vertex too early removing edge ring. Removing: {vertexIndex}, first: {firstVertex}, last: {lastVertex}, region: {RegionIndex}.");
                            vertexOrder.Add(otherVertex);
                            DrawDebugVertexOrder(vertexIndex, vertexOrder);
                            return false;
                        }

                        TriangleIndicesInCurrentRegion.Remove(triangle);

                        // Deleting elements here would invalidate other indices, so just set them to -1 and delete later.
                        TriangleList[triangleIndex + 0] = -1;
                        TriangleList[triangleIndex + 1] = -1;
                        TriangleList[triangleIndex + 2] = -1;

                        currentConnectedVertex = otherVertex;
                        foundNext = true;
                        break;
                    }

                    if (!foundNext) {
                        DrawDebugVertexOrder(vertexIndex, vertexOrder);
                        Debug.LogError(
                            $"Error finding next edge removing vertex {vertexIndex} in region {RegionIndex}.");
                        return false;
                    }

                    if (currentConnectedVertex == firstVertex) break;
                }

                return true;
            }

            // Used for debugging to draw an edge ring when removing a vertex fails.
            private void DrawDebugVertexOrder(int vertexIndex, in UnsafeList<int> vertexOrder) {
                DebugUtility.DrawDebugBounds(
                    new Bounds(math.mul(VolumeTransform, Vertices[vertexIndex]).xyz, Vector3.one * 0.2f), Color.green,
                    50);
                if (vertexOrder.Length <= 0) return;
                Debug.DrawLine(math.mul(VolumeTransform, Vertices[vertexIndex]).xyz,
                               math.mul(VolumeTransform, Vertices[vertexOrder[0]]).xyz,
                               Color.green, 50);
                for (int i = 0; i < vertexOrder.Length - 1; i++) {
                    Debug.DrawLine(math.mul(VolumeTransform, Vertices[vertexOrder[i]]).xyz,
                                   math.mul(VolumeTransform, Vertices[vertexOrder[i + 1]]).xyz,
                                   Color.green, 50);
                }
            }

            // Helper to get dot and cross product of a vertex in an edge ring.
            // Specifically, gets those products of the two edges the vertex is part of.
            private float GetDotAndCrossProduct(int curVertexIndex, int prevVertexIndex, int nextVertexIndex,
                                                out float3 crossProduct) {
                float3 curVertex = Vertices[curVertexIndex].xyz;
                float3 prevVertex = Vertices[prevVertexIndex].xyz;
                float3 nextVertex = Vertices[nextVertexIndex].xyz;

                float3 v1 = math.normalize(nextVertex - curVertex);
                float3 v2 = math.normalize(prevVertex - curVertex);

                float dot = math.dot(v1, v2);
                crossProduct = math.cross(v1, v2);
                return dot;
            }

            // Get the indices of the current, previous, and next vertices in the vertex order.
            private static void GetCurrentAndNeighboringVertexIndices(in UnsafeList<int> vertexOrder, int index,
                                                                      out int curVertexIndex, out int prevVertexIndex,
                                                                      out int nextVertexIndex) {
                curVertexIndex = vertexOrder[index];
                prevVertexIndex = vertexOrder[(index + vertexOrder.Length - 1) % vertexOrder.Length];
                nextVertexIndex = vertexOrder[(index + 1) % vertexOrder.Length];
            }

            // Mark the two vertices as connected if they are not already.
            private void ConnectVertices(ref int vertex1Index, int vertex2Index) {
                UnsafeList<int> vertex1Connections = VertexConnections[vertex1Index];
                UnsafeList<int> vertex2Connections = VertexConnections[vertex2Index];

                if (!vertex1Connections.Contains(vertex2Index)) {
                    vertex1Connections.Add(vertex2Index);
                    VertexConnections[vertex1Index] = vertex1Connections;
                }

                if (!vertex2Connections.Contains(vertex1Index)) {
                    vertex2Connections.Add(vertex1Index);
                    VertexConnections[vertex2Index] = vertex2Connections;
                }
            }

            // Check if the given edge connects two triangles at different angles
            private bool IsEdgeAngled(int vertex1Index, int vertex2Index,
                                      NativeParallelHashMap<Triangle, TriangleRegionIndices> triangleIndicesPerRegion) {
                float4 vertex1 = Vertices[vertex1Index];
                float4 vertex2 = Vertices[vertex2Index];

                UnsafeList<int> connections1 = VertexConnections[vertex1Index];
                UnsafeList<int> connections2 = VertexConnections[vertex2Index];

                for (int i = 0; i < connections1.Length; i++) {
                    int otherIndex1 = connections1[i];
                    if (!connections2.Contains(otherIndex1)) continue;

                    if (!triangleIndicesPerRegion.ContainsKey(
                            new Triangle(vertex1Index, vertex2Index, otherIndex1))) {
                        continue;
                    }

                    // Search for second triangle.
                    for (int j = i + 1; j < connections1.Length; j++) {
                        int otherIndex2 = connections1[j];
                        if (!connections2.Contains(otherIndex2) ||
                            !triangleIndicesPerRegion.ContainsKey(
                                new Triangle(vertex1Index, vertex2Index, otherIndex2))) {
                            continue;
                        }

                        if (otherIndex2 == otherIndex1 ||
                            otherIndex2 == vertex1Index ||
                            otherIndex2 == vertex2Index ||
                            otherIndex1 == vertex1Index ||
                            otherIndex1 == vertex2Index ||
                            vertex1Index == vertex2Index) {
                            Debug.LogError("Duplicate vertex found.");
                        }

                        float4 otherVertex1 = Vertices[otherIndex1];
                        float4 otherVertex2 = Vertices[otherIndex2];

                        // Calculate normals and compare dot product to determine if the edge is angled.
                        float3 normal1 =
                            math.normalize(math.cross((vertex2 - vertex1).xyz, (otherVertex1 - vertex1).xyz));
                        float3 normal2 =
                            math.normalize(math.cross((otherVertex2 - vertex1).xyz, (vertex2 - vertex1).xyz));

                        float dot = math.abs(math.dot(normal1, normal2));
                        if (dot < 0.95f) {
                            return true;
                        }
                    }
                }

                return false;
            }
        }
    }
}
