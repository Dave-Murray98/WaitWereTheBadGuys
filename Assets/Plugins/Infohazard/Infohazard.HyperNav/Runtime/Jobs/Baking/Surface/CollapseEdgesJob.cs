// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct CollapseEdgesJob : IJobParallelFor {
        [ReadOnly] public NativeArray<UnsafeList<CollapsibleEdgePair>> IslandCollapsibleEdgeLists;
        [ReadOnly] public NativeArray<int> VertexCountsPerIsland;

        // Assuming everything works right, each instance should only modify vertices in its own island.
        [NativeDisableParallelForRestriction]
        public BakingNavSurfaceMeshInfo Mesh;

        [NativeDisableParallelForRestriction]
        public NativeArray<float4x4> ErrorMatrices;

        [NativeDisableParallelForRestriction]
        public NativeArray<HybridIntList> VerticesOnEdge;

        [ReadOnly]
        public NativeArray<UnsafeHashMap<Edge, int2>> EdgeOccurrenceMaps;

        [ReadOnly]
        public NativeArray<int> IslandIndices;

        public NativeCancellationToken CancellationToken;

        public float DecimationThreshold;

        public float BoundaryVertexDisplacementThreshold;

        private static readonly ProfilerMarker ProfilerMarkerLoopIteration = new("CollapseEdgesJob - Loop Iteration");
        private static readonly ProfilerMarker ProfilerMarkerMergeVertices = new("CollapseEdgesJob - Merge Vertices");
        private static readonly ProfilerMarker ProfilerMarkerUpdateErrorMatrices = new("CollapseEdgesJob - Update Error Matrices");
        private static readonly ProfilerMarker ProfilerMarkerUpdateEdgeCost = new("CollapseEdgesJob - Update Edge Cost");
        private static readonly ProfilerMarker ProfilerMarkerIsEdgeCollapsible = new("CollapseEdgesJob - Is Edge Collapsible");
        private static readonly ProfilerMarker ProfilerMarkerFlipCheck = new("CollapseEdgesJob - DoesFlipTriangle");


        public void Execute(int index) {
            UnsafeList<CollapsibleEdgePair> initialEdges = IslandCollapsibleEdgeLists[index];

            UnsafeHeap<Edge> heap = new(initialEdges.Length, Allocator.Temp);
            UnsafeHashMap<Edge, EdgeInfo> edgeInfoDict = new(initialEdges.Length, Allocator.Temp);
            UnsafeHashMap<Edge, int2> edgeOccurrenceMap = EdgeOccurrenceMaps[index];
            UnsafeArray<bool> removed = new(Mesh.Vertices.Length, Allocator.Temp);
            UnsafeHashMap<int, UnsafeList<float4>> edgeVertexToPoints =
                new(VertexCountsPerIsland[index], Allocator.Temp);

            foreach (KVPair<Edge, int2> pair in edgeOccurrenceMap) {
                bool isBoundary = (pair.Value.x < 0) != (pair.Value.y < 0);
                if (!isBoundary) continue;
                
                int vertex1 = pair.Key.Vertex1;
                int vertex2 = pair.Key.Vertex2;

                if (!edgeVertexToPoints.ContainsKey(vertex1)) {
                    UnsafeList<float4> points = new(16, Allocator.Temp);
                    points.Add(Mesh.Vertices[vertex1]);
                    edgeVertexToPoints[vertex1] = points;
                }
                
                if (!edgeVertexToPoints.ContainsKey(vertex2)) {
                    UnsafeList<float4> points = new(16, Allocator.Temp);
                    points.Add(Mesh.Vertices[vertex2]);
                    edgeVertexToPoints[vertex2] = points;
                }
            }
            
            for (int i = 0; i < initialEdges.Length; i++) {
                CollapsibleEdgePair pair = initialEdges[i];

                Edge edge = new(pair.Vertex1, pair.Vertex2);
                heap.Add(edge, pair.Priority);
                edgeInfoDict[edge] = new EdgeInfo {
                    Priority = pair.Priority,
                };
            }

            int mergeCount = 0;

            UnsafeHashSet<Edge> checkedEdges = new(256, Allocator.Temp);

            while (heap.TryRemove(out Edge current, out float priority)) {
                if (CancellationToken.IsCancellationRequested) {
                    break;
                }

                using ProfilerMarker.AutoScope s = ProfilerMarkerLoopIteration.Auto();

                int vertex1 = current.Vertex1;
                int vertex2 = current.Vertex2;

                int vertex1Island = IslandIndices[vertex1];
                if (vertex1Island != index) {
                    Debug.LogError($"Encountered vertex1 {vertex1} from wrong island {vertex1Island}.");
                    continue;
                }

                int vertex2Island = IslandIndices[vertex2];
                if (vertex2Island != index) {
                    Debug.LogError($"Encountered vertex2 {vertex2} from wrong island {vertex2Island}.");
                    continue;
                }

                if (removed[vertex1] || removed[vertex2]) continue;

                // We may update the cost of an edge and re-add it to the heap, or determine that it is no longer valid.
                // It is cheaper to add it again and ignore the old one than to update it.
                if (!edgeInfoDict.TryGetValue(current, out EdgeInfo edgeInfo) || edgeInfo.Priority != priority) {
                    continue;
                }

                if (!IsEdgeCollapsible(current, edgeOccurrenceMap, edgeVertexToPoints, out float interp)) {
                    continue;
                }

                float4 midpoint = math.lerp(Mesh.Vertices[vertex1], Mesh.Vertices[vertex2], interp);
                Mesh.Vertices[vertex1] = midpoint;

                MergeVertices(vertex1, vertex2, ref edgeOccurrenceMap, ref edgeVertexToPoints);
                mergeCount++;
                removed[vertex2] = true;

                HybridIntList newConnections = Mesh.VertexConnections[vertex1];

                using (ProfilerMarkerUpdateErrorMatrices.Auto()) {
                    ErrorMatrices[vertex1] = CalculateEdgeCollapseErrorMatricesJob.CalculateErrorMatrix(Mesh, vertex1);

                    for (int i = 0; i < newConnections.Count; i++) {
                        int connection = newConnections[i];

                        ErrorMatrices[connection] =
                            CalculateEdgeCollapseErrorMatricesJob.CalculateErrorMatrix(Mesh, connection);
                    }
                }

                using (ProfilerMarkerUpdateEdgeCost.Auto()) {
                    checkedEdges.Clear();

                    int vertex1ConnectionCount = newConnections.Count;
                    for (int i = 0; i < newConnections.Count; i++) {
                        int connection = newConnections[i];

                        Edge edge = new(vertex1, connection);
                        int connectionConnectionsCount = Mesh.VertexConnections[connection].Count;

                        if (checkedEdges.Add(edge)) {
                            int edgeConnectionCount = vertex1ConnectionCount + connectionConnectionsCount;
                            float newPriority = -edgeConnectionCount;

                            edgeInfoDict[edge] = new EdgeInfo {
                                Priority = newPriority,
                            };

                            heap.Add(edge, newPriority);
                            //UpdateEdgeCost(edge, ref heap, ref edgeInfoDict, edgeOccurrenceMap);
                        }

                        HybridIntList connections2 = Mesh.VertexConnections[connection];
                        int connectionConnectionConnectionsCount = connections2.Count;
                        for (int j = 0; j < connections2.Count; j++) {
                            int connectionConnection = connections2[j];

                            if (connectionConnection == vertex1) continue;

                            Edge edge2 = new(connection, connectionConnection);

                            if (checkedEdges.Add(edge2)) {
                                int edgeConnectionCount = connectionConnectionsCount + connectionConnectionConnectionsCount;
                                float newPriority = -edgeConnectionCount;

                                edgeInfoDict[edge2] = new EdgeInfo {
                                    Priority = newPriority,
                                };

                                heap.Add(edge2, newPriority);

                                //UpdateEdgeCost(edge2, ref heap, ref edgeInfoDict, edgeOccurrenceMap);
                            }
                        }
                    }
                }
            }

            heap.Dispose();
            removed.Dispose();
        }

        private bool IsEdgeCollapsible(Edge edge, UnsafeHashMap<Edge, int2> edgeOccurrenceMap, 
                                       UnsafeHashMap<int, UnsafeList<float4>> edgeVertexToPoints, out float interp) {
            bool isEdgeCollapsible;
            bool vertex1IsOnEdge;
            bool vertex2IsOnEdge;

            using (ProfilerMarkerIsEdgeCollapsible.Auto()) {
                isEdgeCollapsible = IsEdgeCollapsibleIgnoringTriFlip(edge, edgeOccurrenceMap, edgeVertexToPoints, 
                                                                     out vertex1IsOnEdge, out vertex2IsOnEdge);
            }

            if (!isEdgeCollapsible) {
                interp = 0.0f;
                return false;
            }

            interp = 0.5f;

            if (vertex1IsOnEdge) {
                interp = 0.0f;
            } else if (vertex2IsOnEdge) {
                interp = 1.0f;
            }

            float4 midpoint = math.lerp(Mesh.Vertices[edge.Vertex1], Mesh.Vertices[edge.Vertex2], interp);
            float4x4 errorMatrix = ErrorMatrices[edge.Vertex1] + ErrorMatrices[edge.Vertex2];
            float cost = math.dot(midpoint, math.mul(errorMatrix, midpoint));

            if (cost > DecimationThreshold) {
                return false;
            }

            if (DoesCollapseFlipAnyTriangle(edge, interp, midpoint)) {
                return false;
            }

            return true;
        }

        private void MergeVertices(int vertex1, int vertex2, ref UnsafeHashMap<Edge, int2> edgeOccurrenceMap,
                                   ref UnsafeHashMap<int, UnsafeList<float4>> edgeVertexToPoints) {
            using ProfilerMarker.AutoScope s = ProfilerMarkerMergeVertices.Auto();

            // If we merge a vertex into an edge vertex, the resulting vertex is also on the edge.
            HybridIntList onEdge1 = VerticesOnEdge[vertex1];
            HybridIntList onEdge2 = VerticesOnEdge[vertex2];

            if (onEdge2.Count > 0) {
                if (onEdge1.Count > 0) {
                    int indexOfVertex2InOnEdge1 = onEdge1.IndexOf(vertex2);
                    if (indexOfVertex2InOnEdge1 < 0) {
                        Debug.DrawLine(Mesh.Vertices[vertex2].xyz, Mesh.Vertices[vertex1].xyz, Color.red, 10);
                        Debug.LogError($"vertex2 {vertex2} not found in vertex1 {vertex1} edge list with length {onEdge1.Count}.");
                        throw new Exception();
                    } else {
                        onEdge1.RemoveAtSwapBack(indexOfVertex2InOnEdge1);
                    }
                }

                for (int i = 0; i < onEdge2.Count; i++) {
                    int other = onEdge2[i];
                    if (other == vertex1) continue;

                    onEdge1.Add(other);

                    HybridIntList otherOnEdge = VerticesOnEdge[other];

                    int indexOfVertex2InOtherOnEdge = otherOnEdge.IndexOf(vertex2);
                    if (indexOfVertex2InOtherOnEdge < 0) {
                        Debug.LogError(
                            $"vertex2 {vertex2} not found in other {other} edge list with length {otherOnEdge.Count}.");
                        continue;
                    }

                    if (!otherOnEdge.Contains(vertex1)) {
                        otherOnEdge[indexOfVertex2InOtherOnEdge] = vertex1;
                    } else {
                        otherOnEdge.RemoveAtSwapBack(indexOfVertex2InOtherOnEdge);
                    }

                    // if (otherOnEdge.Count < 2) {
                    //     VerticesOnEdge[other] = default;
                    //     otherOnEdge.Dispose();
                    // } else {
                        VerticesOnEdge[other] = otherOnEdge;
                    //}
                }

                // if (onEdge1.Count < 2) {
                //     VerticesOnEdge[vertex1] = default;
                //     onEdge1.Dispose();
                // } else {
                    VerticesOnEdge[vertex1] = onEdge1;
                //}

                VerticesOnEdge[vertex2] = default;
                onEdge2.Dispose();
            }

            // Update the triangle list.
            HybridIntList triangles1 = Mesh.VertexTriangles[vertex1];
            HybridIntList triangles2 = Mesh.VertexTriangles[vertex2];

            int2 removedTriangles = new(-1, -1);
            int removedTriangleCount = 0;
            for (int i = 0; i < triangles2.Count; i++) {
                int triangleStart = triangles2[i];

                int indexOfVertex1 = -1;
                int indexOfVertex2 = -1;

                if (Mesh.TriangleList[triangleStart] == -1) {
                    continue;
                }

                for (int j = 0; j < 3; j++) {
                    int vertexIndex = Mesh.TriangleList[triangleStart + j];

                    if (vertexIndex == vertex1) indexOfVertex1 = j;
                    if (vertexIndex == vertex2) indexOfVertex2 = j;
                }

                if (indexOfVertex2 < 0) {
                    Debug.LogError("Vertex not found in triangle.");
                    continue;
                }

                if (indexOfVertex1 >= 0) {
                    // Triangle using both the merged vertices are removed.
                    Mesh.TriangleList[triangleStart + 0] = -1;
                    Mesh.TriangleList[triangleStart + 1] = -1;
                    Mesh.TriangleList[triangleStart + 2] = -1;

                    if (removedTriangleCount > 1) {
                        Debug.LogError("More than two triangles removed.");
                    } else {
                        removedTriangles[removedTriangleCount++] = triangleStart;
                    }
                } else {
                    // Triangles using vertex2 are updated to use vertex1.
                    Mesh.TriangleList[triangleStart + indexOfVertex2] = vertex1;

                    triangles1.Add(triangleStart);
                }
            }

            Mesh.VertexTriangles[vertex2] = default;
            Mesh.VertexTriangles[vertex1] = triangles1;
            triangles2.Dispose();

            HybridIntList connections1 = Mesh.VertexConnections[vertex1];
            HybridIntList connections2 = Mesh.VertexConnections[vertex2];

            // Update the occurrence map. The following changes are needed:
            // - Remove the edge between the merged vertices.
            // - Pairs of edges that connect vertex1 and vertex2 to a shared third vertex will have a shared triangle
            //   that has been removed. The edge connecting to vertex1 should have the shared triangle replaced by the
            //   unshared triangle that was previously on the edge connected to vertex2.
            // - Edges that connect vertex2 to a non-shared vertex should be updated to connect to vertex1 instead,
            //   with the same occurrences.
            edgeOccurrenceMap.Remove(new Edge(vertex1, vertex2));

            for (int i = 0; i < connections2.Count; i++) {
                int connVertex = connections2[i];
                if (connVertex == vertex1) continue;

                Edge v1ToConn = new(vertex1, connVertex);
                Edge v2ToConn = new(vertex2, connVertex);

                if (!edgeOccurrenceMap.TryGetValue(v2ToConn, out int2 e1Tris)) {
                    Debug.LogError($"Edge {v2ToConn} not found in occurrence map.");
                    continue;
                }

                // vertex 1 may or may not be connected to the vertex.
                if (edgeOccurrenceMap.TryGetValue(v1ToConn, out int2 e2Tris)) {
                    // If both merging vertices are connected to the current vertex, the occurrences should
                    // have a shared triangle. This triangle should be removed by the previous step.

                    int2 unsharedTriangles = new(-1, -1);
                    int sharedTriangle = -1;

                    if (e1Tris.x == e2Tris.x) {
                        sharedTriangle = e1Tris.x;
                        unsharedTriangles = new int2(e1Tris.y, e2Tris.y);
                    } else if (e1Tris.x == e2Tris.y) {
                        sharedTriangle = e1Tris.x;
                        unsharedTriangles = new int2(e1Tris.y, e2Tris.x);
                    } else if (e1Tris.y == e2Tris.x) {
                        sharedTriangle = e1Tris.y;
                        unsharedTriangles = new int2(e1Tris.x, e2Tris.y);
                    } else if (e1Tris.y == e2Tris.y) {
                        sharedTriangle = e1Tris.y;
                        unsharedTriangles = new int2(e1Tris.x, e2Tris.x);
                    } else {
                        Debug.LogError("No shared triangle found.");
                    }

                    if (sharedTriangle != removedTriangles.x && sharedTriangle != removedTriangles.y) {
                        Debug.LogError("Shared triangle not removed.");
                    }

                    edgeOccurrenceMap.Remove(v2ToConn);
                    edgeOccurrenceMap[v1ToConn] = unsharedTriangles;
                } else {
                    // If only vertex 2 is connected to the current vertex, the occurrences are unchanged.
                    // However, since vertex 2 is replaced by vertex 1, we need to remove the original edge and
                    // add a new one to the map.
                    edgeOccurrenceMap.Remove(v2ToConn);
                    edgeOccurrenceMap[v1ToConn] = e1Tris;
                }
            }

            // Update the connections list.
            // - Remove the connection from vertex1 to vertex2.
            // - Add connections from vertex1 to all connections of vertex2.
            // - Add connections from all the connections of vertex2 to vertex1.
            int indexOfVertex2InConnections1 = connections1.IndexOf(vertex2);
            if (indexOfVertex2InConnections1 >= 0) {
                connections1.RemoveAtSwapBack(indexOfVertex2InConnections1);
            } else {
                Debug.LogError($"Vertex {vertex2} not found in {vertex1} connections.");
            }

            for (int i = 0; i < connections2.Count; i++) {
                int connectedVertex = connections2[i];
                if (connectedVertex == vertex1) continue;

                HybridIntList connectionConnections = Mesh.VertexConnections[connectedVertex];
                int indexOfVertex2InConnectionConnections = connectionConnections.IndexOf(vertex2);
                if (indexOfVertex2InConnectionConnections < 0) {
                    Debug.LogError($"Vertex {vertex2} not found in connected vertex {connectedVertex} connections.");
                    continue;
                }

                if (!connections1.Contains(connectedVertex)) {
                    connections1.Add(connectedVertex);
                    connectionConnections[indexOfVertex2InConnectionConnections] = vertex1;
                } else {
                    connectionConnections.RemoveAtSwapBack(indexOfVertex2InConnectionConnections);
                }

                Mesh.VertexConnections[connectedVertex] = connectionConnections;
            }

            Mesh.VertexConnections[vertex2] = default;
            Mesh.VertexConnections[vertex1] = connections1;
            connections2.Dispose();
            
            // Update the edge vertex to points map.
            if (edgeVertexToPoints.TryGetValue(vertex2, out UnsafeList<float4> points2)) {
                if (edgeVertexToPoints.TryGetValue(vertex1, out UnsafeList<float4> points1)) {
                    points1.AddRange(points2);
                    edgeVertexToPoints[vertex1] = points1;
                    points2.Dispose();
                } else {
                    edgeVertexToPoints[vertex1] = points2;
                }

                edgeVertexToPoints.Remove(vertex2);
            }
        }

        private bool IsEdgeCollapsibleIgnoringTriFlip(Edge edge, UnsafeHashMap<Edge, int2> edgeOccurrenceMap,
                                                      UnsafeHashMap<int, UnsafeList<float4>> edgeVertexToPoints,
                                                      out bool vertex1IsOnEdge, out bool vertex2IsOnEdge) {

            using ProfilerMarker.AutoScope scope = ProfilerMarkerIsEdgeCollapsible.Auto();

            // The following checks are required to determine if an edge is collapsible:
            // 1. There must be exactly two other vertices that are connected to both of the given vertices,
            //    and the two given vertices must form a triangle with each of the other two vertices.
            // 2. None of the triangles containing one of the given vertices must flip its normal
            //    when that vertex is replaced by the midpoint.

            vertex1IsOnEdge = false;
            vertex2IsOnEdge = false;

            int vertex1 = edge.Vertex1;
            int vertex2 = edge.Vertex2;

            HybridIntList vertex1Edges = VerticesOnEdge[vertex1];
            HybridIntList vertex2Edges = VerticesOnEdge[vertex2];
            HybridIntList connections1 = Mesh.VertexConnections[vertex1];
            HybridIntList connections2 = Mesh.VertexConnections[vertex2];

            bool vertex1IsCorner = vertex1Edges.Count > 2 || connections1.Count == 2;
            bool vertex2IsCorner = vertex2Edges.Count > 2 || connections2.Count == 2;

            vertex1IsOnEdge = vertex1Edges.Count > 0;
            vertex2IsOnEdge = vertex2Edges.Count > 0;

            if (vertex1IsCorner && vertex2IsCorner) {
                return false;
            }

            if (!edgeOccurrenceMap.TryGetValue(edge, out int2 occurrences)) {
                Debug.LogError($"Edge ({vertex1}, {vertex2}) not found in EdgeOccurrenceMap.");
                return false;
            }

            bool isBoundary = (occurrences.x < 0) != (occurrences.y < 0);

            float maxDispSqr = BoundaryVertexDisplacementThreshold * BoundaryVertexDisplacementThreshold;
            if (isBoundary) {
                HybridIntList otherVertex1Edges = new(Allocator.Temp);
                HybridIntList otherVertex2Edges = new(Allocator.Temp);

                GetOtherEdgeVertices(vertex2, vertex1Edges, ref otherVertex1Edges);
                GetOtherEdgeVertices(vertex1, vertex2Edges, ref otherVertex2Edges);

                float disp1 = otherVertex1Edges.Count == 1
                    ? GetTriangleMaxVertexDisplacementSqr(otherVertex1Edges[0], vertex1, vertex2, edgeVertexToPoints)
                    : float.PositiveInfinity;

                float disp2 = otherVertex2Edges.Count == 1
                    ? GetTriangleMaxVertexDisplacementSqr(otherVertex2Edges[0], vertex2, vertex1, edgeVertexToPoints)
                    : float.PositiveInfinity;

                bool clip1 = disp1 < maxDispSqr;
                bool clip2 = disp2 < maxDispSqr;

                if (clip1 && clip2) {
                    vertex1IsOnEdge = disp1 > disp2;
                    vertex2IsOnEdge = !vertex1IsOnEdge;
                } else {
                    vertex1IsOnEdge = !clip1;
                    vertex2IsOnEdge = !clip2;
                }
            }

            if (vertex1IsOnEdge && vertex2IsOnEdge) {
                return false;
            }

            // Check 2
            int sharedConnections = 0;
            int requiredConnections = isBoundary ? 1 : 2;

            for (int i = 0; i < connections1.Count; i++) {
                int connection = connections1[i];
                if (connection != vertex2 && connections2.Contains(connection)) {
                    sharedConnections++;

                    if (sharedConnections > requiredConnections) {
                        return false;
                    }
                }
            }

            if (sharedConnections != requiredConnections) {
                return false;
            }

            return true;
        }

        public bool DoesCollapseFlipAnyTriangle(Edge edge, float interp, float4 midpoint) {
            int vertex1 = edge.Vertex1;
            int vertex2 = edge.Vertex2;

            using (ProfilerMarkerFlipCheck.Auto()) {
                if (interp > 0) {
                    HybridIntList tris1 = Mesh.VertexTriangles[vertex1];
                    for (int i = 0; i < tris1.Count; i++) {
                        int triStart = tris1[i];

                        if (triStart < 0) continue;

                        int a = Mesh.TriangleList[triStart + 0];
                        int b = Mesh.TriangleList[triStart + 1];
                        int c = Mesh.TriangleList[triStart + 2];

                        if (a == vertex2 || b == vertex2 || c == vertex2 || a < 0 || b < 0 || c < 0) {
                            continue;
                        }

                        if (DoesFlipTriangle(triStart, vertex1, midpoint)) {
                            return true;
                        }
                    }
                }

                if (interp < 1) {
                    HybridIntList tris2 = Mesh.VertexTriangles[vertex2];
                    for (int i = 0; i < tris2.Count; i++) {
                        int triStart = tris2[i];

                        if (triStart < 0) continue;

                        int a = Mesh.TriangleList[triStart + 0];
                        int b = Mesh.TriangleList[triStart + 1];
                        int c = Mesh.TriangleList[triStart + 2];

                        if (a == vertex1 || b == vertex1 || c == vertex1 || a < 0 || b < 0 || c < 0) {
                            continue;
                        }

                        if (DoesFlipTriangle(triStart, vertex2, midpoint)) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private float GetTriangleMaxVertexDisplacementSqr(int vertex1, int vertex2, int vertex3,
                                                          UnsafeHashMap<int, UnsafeList<float4>> edgeVertexToPoints) {
            
            if (!edgeVertexToPoints.TryGetValue(vertex2, out UnsafeList<float4> points)) {
                Debug.LogError($"Vertex {vertex2} not found in edgeVertexToPoints.");
                return float.PositiveInfinity;
            }
            
            if (points.Length == 0) {
                Debug.LogError($"Vertex {vertex2} has zero points in map.");
                return float.PositiveInfinity;
            }
            
            float maxDispSqr = 0.0f;
            float4 edgeStart = Mesh.Vertices[vertex1];
            float4 edgeVector = Mesh.Vertices[vertex3] - edgeStart;

            foreach (float4 point in points) {
                float4 edgeToPoint = point - edgeStart;
                float4 proj = math.project(edgeToPoint, edgeVector);
                float4 disp = edgeToPoint - proj;

                float dispSqr = math.lengthsq(disp);
                if (dispSqr > maxDispSqr) {
                    maxDispSqr = dispSqr;
                }
            }
            
            return maxDispSqr;
        }

        public static void GetOtherEdgeVertices(int otherVertex, in HybridIntList edges, ref HybridIntList outEdges) {
            for (int i = 0; i < edges.Count; i++) {
                int edge = edges[i];
                if (edge == otherVertex) continue;
                outEdges.Add(edge);
            }
        }

        private bool DoesFlipTriangle(int triangleStart, int changingVertex, float4 newVertexPosition) {
            int index1 = Mesh.TriangleList[triangleStart + 0];
            int index2 = Mesh.TriangleList[triangleStart + 1];
            int index3 = Mesh.TriangleList[triangleStart + 2];

            float4 normal = Mesh.TriangleNormals[triangleStart / 3];

            int2 oppositeEdge;
            if (index1 == changingVertex) {
                oppositeEdge = new int2(index2, index3);
            } else if (index2 == changingVertex) {
                oppositeEdge = new int2(index3, index1);
            } else if (index3 == changingVertex) {
                oppositeEdge = new int2(index1, index2);
            } else {
                Debug.LogError("Triangle does not contain changingVertex.");
                return true;
            }

            float4 v2 = Mesh.Vertices[oppositeEdge.x];
            float4 v3 = Mesh.Vertices[oppositeEdge.y];

            float4 newEdge1 = v2 - newVertexPosition;
            float4 newEdge2 = v3 - newVertexPosition;

            float3 cross = math.cross(newEdge1.xyz, newEdge2.xyz);

            float newAreaSqr = math.lengthsq(cross) * 0.25f;

            return newAreaSqr < 0.000001f || math.dot(normal.xyz, cross) < 0.0f;
        }

        private struct EdgeInfo {
            public float Priority;
        }
    }
}
