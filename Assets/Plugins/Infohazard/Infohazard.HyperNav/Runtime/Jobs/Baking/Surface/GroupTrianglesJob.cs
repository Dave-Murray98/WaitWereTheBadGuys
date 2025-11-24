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

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct GroupTrianglesJob : IJobParallelFor {
        // Assuming everything works right, each instance should only modify vertices in its own island.
        [NativeDisableParallelForRestriction]
        public BakingNavSurfaceMeshInfo Mesh;

        [ReadOnly]
        public NativeArray<int> IslandIndices;

        public NativeArray<UnsafeList<HybridIntList>> GroupTriangles;

        public float MinNormalDot;

        public void Execute(int index) {
            UnsafeList<HybridIntList> groupsForIsland = new(256, Allocator.Persistent);

            UnsafeHeap<Group> heap = new(256, Allocator.Temp);

            // Add all triangles as groups to the heap, prioritizing the lowest area triangles.
            for (int i = 0; i < Mesh.TriangleList.Length; i += 3) {
                int index1 = Mesh.TriangleList[i + 0];
                int index2 = Mesh.TriangleList[i + 1];
                int index3 = Mesh.TriangleList[i + 2];

                if (index1 < 0 || index2 < 0 || index3 < 0 || IslandIndices[index1] != index) continue;

                float4 v1 = Mesh.Vertices[index1];
                float4 v2 = Mesh.Vertices[index2];
                float4 v3 = Mesh.Vertices[index3];

                float4 edge1 = v2 - v1;
                float4 edge2 = v3 - v1;
                float area = math.length(math.cross(edge1.xyz, edge2.xyz)) * 0.5f;

                Group group = new() {
                    Triangles = new UnsafeHashSet<int>(256, Allocator.Temp),
                    EdgeVertices = new UnsafeHashSet<int>(256, Allocator.Temp),
                    Normal = Mesh.TriangleNormals[i / 3],
                    Registered = false,
                    RootTriangle = i
                };

                group.Triangles.Add(i);

                group.EdgeVertices.Add(index1);
                group.EdgeVertices.Add(index2);
                group.EdgeVertices.Add(index3);

                // Use -area as the priority so that the lowest area triangles are prioritized.
                heap.Add(group, -area);
            }

            // For each triangle, track whether it's assigned to a group.
            // Once assigned a triangle cannot become part of another group.
            // Triangles in initial groups are not assigned until those groups are pulled from the heap.
            UnsafeArray<bool> assigned = new(Mesh.TriangleList.Length / 3, Allocator.Temp);

            while (heap.TryRemove(out Group curGroup, out float priority)) {
                // A group is registered once its root triangle is assigned to it.
                // Before that, the root triangle might be assigned to another group.
                if (!curGroup.Registered && assigned[curGroup.RootTriangle / 3]) continue;

                // Now that we've pulled the group from the heap, we can register it.
                curGroup.Registered = true;
                assigned[curGroup.RootTriangle / 3] = true;
                float area = -priority;

                UnsafeHashSet<int> newTriangleCandidates = new(256, Allocator.Temp);

                UnsafeHashMap<Edge, int2> occurrenceMap = new(256, Allocator.Temp);

                // Find all triangles that share a vertex with the current group,
                // and are within the normal dot threshold.
                CollectNewTriangleCandidates(curGroup, ref assigned, ref newTriangleCandidates, ref occurrenceMap);

                // Group cannot be expanded further.
                if (newTriangleCandidates.Count == 0) {
                    FinalizeGroup(curGroup, ref groupsForIsland);
                    continue;
                }

                // Add existing triangle edges to occurrence counts.
                AddCurrentTrianglesToEdgeOccurrences(curGroup, ref occurrenceMap);

                // Build map of how many 'steps' each candidate triangle is from the root triangle.
                // Triangles must be connected by a shared edge to be considered a step apart from each other.
                UnsafeHashMap<int, int> candidateStepsFromGroup =
                    BuildCandidateStepsFromGroupMap(newTriangleCandidates, curGroup, occurrenceMap);

                // Any triangle that cannot be reached by edge connections is discarded from the group.
                FilterConnectedTriangleCandidates(ref newTriangleCandidates, candidateStepsFromGroup, ref occurrenceMap);

                // Look for concave vertices.
                // As long as we find a concave corner, remove at least one of the two adjacent triangles from the set.
                // We consider the earlier vertex in the winding order of each edge where the edge has exactly one occurrence.
                while (newTriangleCandidates.Count > 0 ) {
                    int concaveVertex = FindConcaveVertex(occurrenceMap, out int2 edge1Faces, out int2 edge2Faces);

                    if (concaveVertex < 0) break;

                    int2 trisToRemove = GetTrisToRemove(edge1Faces, edge2Faces, curGroup);

                    if (trisToRemove.x < 0) break;

                    // If one triangle is more steps from the group than the other, remove the one with more steps.
                    if (trisToRemove.y >= 0) {
                        int tri1 = trisToRemove.x;
                        int tri2 = trisToRemove.y;

                        int steps1 = candidateStepsFromGroup[tri1];
                        int steps2 = candidateStepsFromGroup[tri2];

                        if (steps1 > steps2) {
                            trisToRemove = new int2(tri1, -1);
                        } else if (steps2 > steps1) {
                            trisToRemove = new int2(tri2, -1);
                        }
                    }

                    // Remove the triangles from the candidate list.
                    if (trisToRemove.y >= 0) {
                        newTriangleCandidates.Remove(trisToRemove.x);
                        newTriangleCandidates.Remove(trisToRemove.y);
                        RemoveOccurrences(ref occurrenceMap, trisToRemove.x);
                        RemoveOccurrences(ref occurrenceMap, trisToRemove.y);
                    } else {
                        newTriangleCandidates.Remove(trisToRemove.x);
                        RemoveOccurrences(ref occurrenceMap, trisToRemove.x);
                    }

                    // After removing triangles, we need to recalculate the steps map and re-filter,
                    // as some triangles may no longer be connected.
                    candidateStepsFromGroup.Dispose();
                    candidateStepsFromGroup =
                        BuildCandidateStepsFromGroupMap(newTriangleCandidates, curGroup, occurrenceMap);
                    FilterConnectedTriangleCandidates(ref newTriangleCandidates, candidateStepsFromGroup,
                        ref occurrenceMap);
                }

                if (newTriangleCandidates.Count == 0) {
                    FinalizeGroup(curGroup, ref groupsForIsland);
                    continue;
                }

                // Add the candidates to the group, and add their areas.
                foreach (int triangleStart in newTriangleCandidates) {
                    curGroup.Triangles.Add(triangleStart);
                    assigned[triangleStart / 3] = true;

                    // Calculate area
                    int index1 = Mesh.TriangleList[triangleStart + 0];
                    int index2 = Mesh.TriangleList[triangleStart + 1];
                    int index3 = Mesh.TriangleList[triangleStart + 2];

                    float4 v1 = Mesh.Vertices[index1];
                    float4 v2 = Mesh.Vertices[index2];
                    float4 v3 = Mesh.Vertices[index3];

                    float4 edge1 = v2 - v1;
                    float4 edge2 = v3 - v1;

                    float newArea = math.length(math.cross(edge1.xyz, edge2.xyz)) * 0.5f;
                    area += newArea;
                }

                curGroup.EdgeVertices.Clear();
                foreach (KVPair<Edge,int2> pair in occurrenceMap) {
                    if ((pair.Value.x >= 0) == (pair.Value.y >= 0)) continue;
                    curGroup.EdgeVertices.Add(pair.Key.Vertex1);
                    curGroup.EdgeVertices.Add(pair.Key.Vertex2);
                }

                heap.Add(curGroup, -area);
            }

            GroupTriangles[index] = groupsForIsland;
        }

        private static int2 GetTrisToRemove(int2 edge1Faces, int2 edge2Faces, Group curGroup) {
            int triIndex1 = edge1Faces.x >= 0 ? edge1Faces.x : edge1Faces.y;
            int triIndex2 = edge2Faces.x >= 0 ? edge2Faces.x : edge2Faces.y;

            bool c1 = curGroup.Triangles.Contains(triIndex1);
            bool c2 = curGroup.Triangles.Contains(triIndex2);

            if (c1 && c2) {
                Debug.LogError($"Unexpected error: both triangles {triIndex1} and {triIndex2} are already in the group.");
                return new int2(-1, -1);
            } else if (c1) {
                return new int2(triIndex2, -1);
            } else if (c2) {
                return new int2(triIndex1, -1);
            } else {
                return new int2(triIndex1, triIndex2);
            }
        }

        private int FindConcaveVertex(UnsafeHashMap<Edge, int2> occurrenceCounts, out int2 edge1Faces, out int2 edge2Faces) {
            edge1Faces = new(-1, -1);
            edge2Faces = new(-1, -1);
            int concaveVertex = -1;
            foreach (KVPair<Edge, int2> pair in occurrenceCounts) {
                if ((pair.Value.y >= 0) == (pair.Value.x >= 0)) continue;
                Edge edge = pair.Key;

                int vertex1 = edge.Vertex1;
                int vertex2 = edge.Vertex2;

                // Determine if the edge is CCW or CW.
                // If the edge if CCW, we need to reverse the vertices.
                HybridIntList conns1 = Mesh.VertexConnections[vertex1];
                HybridIntList conns2 = Mesh.VertexConnections[vertex2];

                int triIndex = pair.Value.x >= 0 ? pair.Value.x : pair.Value.y;

                int triVertex3 = -1;
                for (int i = 0; i < 3; i++) {
                    int triV = Mesh.TriangleList[triIndex + i];

                    if (triV != vertex1 && triV != vertex2) {
                        triVertex3 = triV;
                        break;
                    }
                }

                if (triVertex3 < 0) {
                    Debug.LogError("Could not find third vertex.");
                    break;
                }

                float4 triV1 = Mesh.Vertices[vertex1];
                float4 triV2 = Mesh.Vertices[vertex2];
                float4 triV3 = Mesh.Vertices[triVertex3];

                float4 triEdge1 = triV2 - triV1;
                float4 triEdge2 = triV3 - triV1;

                float3 triNormal = math.cross(triEdge1.xyz, triEdge2.xyz);
                float3 calcNormal = Mesh.TriangleNormals[triIndex / 3].xyz;
                if (math.dot(triNormal, calcNormal) < 0) {
                    (vertex1, vertex2) = (vertex2, vertex1);
                    (conns1, conns2) = (conns2, conns1);
                }

                // Find another boundary edge connecting to vertex1.
                // There should be exactly one.
                for (int i = 0; i < conns1.Count; i++) {
                    int conn = conns1[i];
                    if (conn == vertex2) continue;

                    Edge connEdge = new(vertex1, conn);
                    if (!occurrenceCounts.TryGetValue(connEdge, out int2 value) ||
                        ((value.x >= 0) == (value.y >= 0))) {
                        continue;
                    }

                    float4 v1 = Mesh.Vertices[vertex1];
                    float4 v2 = Mesh.Vertices[vertex2];
                    float4 connV = Mesh.Vertices[conn];

                    float4 edge1 = v1 - connV;
                    float4 edge2 = v2 - v1;

                    float3 normal = math.cross(edge1.xyz, edge2.xyz);
                    if (math.dot(normal, calcNormal) < 0) {
                        concaveVertex = vertex1;
                        edge1Faces = pair.Value;
                        edge2Faces = value;
                    }
                }
            }

            return concaveVertex;
        }

        private void FilterConnectedTriangleCandidates(ref UnsafeHashSet<int> newTriangleCandidates,
            UnsafeHashMap<int, int> candidateStepsFromRoot, ref UnsafeHashMap<Edge, int2> occurrenceCounts) {

            UnsafeHashSet<int> toRemove = new(newTriangleCandidates.Count, Allocator.Temp);

            foreach (int candidate in newTriangleCandidates) {
                if (!candidateStepsFromRoot.ContainsKey(candidate)) {
                    toRemove.Add(candidate);
                }
            }

            foreach (int triIndex in toRemove) {
                RemoveOccurrences(ref occurrenceCounts, triIndex);
                newTriangleCandidates.Remove(triIndex);
            }
        }

        private void CollectNewTriangleCandidates(
            Group curGroup,
            ref UnsafeArray<bool> assigned,
            ref UnsafeHashSet<int> newTriangleCandidates,
            ref UnsafeHashMap<Edge, int2> occurrenceCounts) {

            foreach (int edgeVertex in curGroup.EdgeVertices) {
                HybridIntList triangles = Mesh.VertexTriangles[edgeVertex];

                for (int i = 0; i < triangles.Count; i++) {
                    int triangleStart = triangles[i];

                    if (assigned[triangleStart / 3] ||
                        curGroup.Triangles.Contains(triangleStart) ||
                        newTriangleCandidates.Contains(triangleStart)) {
                        continue;
                    }

                    float4 normal = Mesh.TriangleNormals[triangleStart / 3];
                    float dot = math.dot(normal, curGroup.Normal);
                    if (dot < MinNormalDot) continue;

                    newTriangleCandidates.Add(triangleStart);

                    int index1 = Mesh.TriangleList[triangleStart + 0];
                    int index2 = Mesh.TriangleList[triangleStart + 1];
                    int index3 = Mesh.TriangleList[triangleStart + 2];

                    if (!TryAddOccurrences(new Edge(index1, index2), triangleStart, ref occurrenceCounts) ||
                        !TryAddOccurrences(new Edge(index2, index3), triangleStart, ref occurrenceCounts) ||
                        !TryAddOccurrences(new Edge(index3, index1), triangleStart, ref occurrenceCounts)) {
                        break;
                    }
                }
            }
        }

        private void AddCurrentTrianglesToEdgeOccurrences(Group curGroup, ref UnsafeHashMap<Edge, int2> occurrenceCounts) {
            foreach (int triangleStart in curGroup.Triangles) {
                int index1 = Mesh.TriangleList[triangleStart + 0];
                int index2 = Mesh.TriangleList[triangleStart + 1];
                int index3 = Mesh.TriangleList[triangleStart + 2];

                if (!TryAddOccurrences(new Edge(index1, index2), triangleStart, ref occurrenceCounts) ||
                    !TryAddOccurrences(new Edge(index2, index3), triangleStart, ref occurrenceCounts) ||
                    !TryAddOccurrences(new Edge(index3, index1), triangleStart, ref occurrenceCounts)) {
                    break;
                }
            }
        }

        private void FinalizeGroup(Group group, ref UnsafeList<HybridIntList> groupsForIsland) {
            HybridIntList groupList = new(Allocator.Persistent);
            foreach (int tri in group.Triangles) {
                groupList.Add(tri);
            }
            groupsForIsland.Add(groupList);
        }

        private UnsafeHashMap<int, int>  BuildCandidateStepsFromGroupMap(UnsafeHashSet<int> newTriangleCandidates,
            Group curGroup, UnsafeHashMap<Edge, int2> occurrenceCounts) {

            UnsafeHashMap<int, int> candidateStepsFromRoot = new(newTriangleCandidates.Count, Allocator.Temp);
            UnsafeRingQueue<int2> candidateQueue =
                new(newTriangleCandidates.Count + curGroup.Triangles.Count, Allocator.Temp);

            foreach (int curTriangle in curGroup.Triangles) {
                candidateQueue.Enqueue(new int2(curTriangle, 0));
            }

            while (candidateQueue.TryDequeue(out int2 cur)) {
                for (int i = 0; i < 3; i++) {
                    int vertex1 = Mesh.TriangleList[cur.x + i];
                    int vertex2 = Mesh.TriangleList[cur.x + (i + 1) % 3];
                    Edge edge = new(vertex1, vertex2);
                    int2 occurrences = occurrenceCounts[edge];

                    int neighbor = occurrences.x != cur.x ? occurrences.x : occurrences.y;
                    if (neighbor < 0) continue;

                    if (!newTriangleCandidates.Contains(neighbor)) continue;

                    int steps = cur.y + 1;
                    if (!candidateStepsFromRoot.TryAdd(neighbor, steps)) continue;

                    candidateQueue.Enqueue(new int2(neighbor, steps));
                }
            }

            candidateQueue.Dispose();
            return candidateStepsFromRoot;
        }

        private void RemoveOccurrences(ref UnsafeHashMap<Edge, int2> occurrenceCounts, int triIndex) {
            int vertex1 = Mesh.TriangleList[triIndex + 0];
            int vertex2 = Mesh.TriangleList[triIndex + 1];
            int vertex3 = Mesh.TriangleList[triIndex + 2];

            RemoveOccurrence(ref occurrenceCounts, new Edge(vertex1, vertex2), triIndex);
            RemoveOccurrence(ref occurrenceCounts, new Edge(vertex2, vertex3), triIndex);
            RemoveOccurrence(ref occurrenceCounts, new Edge(vertex3, vertex1), triIndex);
        }

        private void RemoveOccurrence(ref UnsafeHashMap<Edge, int2> occurrenceCounts, Edge edge, int triIndex) {
            if (occurrenceCounts.TryGetValue(edge, out int2 value)) {
                if (value.x == triIndex) {
                    value.x = -1;
                } else if (value.y == triIndex) {
                    value.y = -1;
                } else {
                    Debug.LogError($"Edge {edge} does not have the expected occurrence.");
                }

                occurrenceCounts[edge] = value;
            } else {
                Debug.LogError($"Edge {edge} does not have the expected occurrence.");
            }
        }

        private bool TryAddOccurrences(Edge edge, int triangleStart, ref UnsafeHashMap<Edge, int2> occurrenceCounts) {
            if (AddOccurrence(ref occurrenceCounts, edge, triangleStart)) return true;

            int2 tris = occurrenceCounts[edge];
            DrawTriangle(tris.x, Color.green);
            DrawTriangle(tris.y, Color.green);
            DrawTriangle(triangleStart, Color.yellow);
            Debug.DrawLine(Mesh.Vertices[edge.Vertex1].xyz, Mesh.Vertices[edge.Vertex2].xyz, Color.red, 10);
            Debug.LogError($"Edge {edge} already has two occurrences.");
            return false;
        }

        private bool AddOccurrence(ref UnsafeHashMap<Edge, int2> occurrenceCounts, Edge edge, int triIndex) {
            if (occurrenceCounts.TryGetValue(edge, out int2 value)) {
                if (value.x < 0) {
                    value.x = triIndex;
                } else if (value.y < 0) {
                    value.y = triIndex;
                } else {
                    Debug.LogError($"Triangle {triIndex} cannot be added for edge {edge} which already has two triangles {value}.");
                    return false;
                }

                occurrenceCounts[edge] = value;
            } else {
                occurrenceCounts.Add(edge, new int2(triIndex, -1));
            }

            return true;
        }

        private void DrawTriangle(int index, Color color) {
            float4 v1 = Mesh.Vertices[Mesh.TriangleList[index + 0]];
            float4 v2 = Mesh.Vertices[Mesh.TriangleList[index + 1]];
            float4 v3 = Mesh.Vertices[Mesh.TriangleList[index + 2]];

            Debug.DrawLine(v1.xyz, v2.xyz, color, 10);
            Debug.DrawLine(v2.xyz, v3.xyz, color, 10);
            Debug.DrawLine(v3.xyz, v1.xyz, color, 10);
        }

        private struct Group : IEquatable<Group> {
            public UnsafeHashSet<int> Triangles;
            public UnsafeHashSet<int> EdgeVertices;
            public float4 Normal;
            public bool Registered;
            public int RootTriangle;

            public bool Equals(Group other) {
                return RootTriangle == other.RootTriangle;
            }
        }
    }
}
