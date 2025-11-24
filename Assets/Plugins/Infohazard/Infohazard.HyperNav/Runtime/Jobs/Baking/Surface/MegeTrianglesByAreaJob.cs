// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct MergeTrianglesByAreaJob : IJob {
        public BakingNavSurfaceMeshInfo Mesh;

        public float AreaThresholdSqr;

        public NativeList<int> TrianglesToCheck;
        public NativeList<int> VerticesToCheck;

        public bool CheckOldNormals;
        public NativeArray<float4> OldNormals;

        public int MaxMerges;

        public void Execute() {
            UnsafeArray<bool> removedVertices = new(Mesh.Vertices.Length, Allocator.Temp, true);

            int merges = 0;
            for (int i = 0; i < TrianglesToCheck.Length; i++) {
                int triStart = TrianglesToCheck[i];
                bool merged = CheckTriangle(triStart, ref removedVertices, merges);
                if (merged) merges++;
                if (MaxMerges > 0 && merges >= MaxMerges) break;
            }

            for (int i = 0; i < TrianglesToCheck.Length; i++) {
                int triStart = TrianglesToCheck[i];
                if (Mesh.TriangleList[triStart + 0] >= 0) continue;
                TrianglesToCheck.RemoveAtSwapBack(i);
                i--;
            }

            for (int i = 0; i < VerticesToCheck.Length; i++) {
                int vertexIndex = VerticesToCheck[i];
                if (!removedVertices[vertexIndex]) continue;
                VerticesToCheck.RemoveAtSwapBack(i);
                i--;
            }
        }

        private bool IsEdgeCollapsible(int index1, int index2) {
            int sharedConnections = 0;
            int sharedTriangles = 0;
            
            HybridIntList connections1 = Mesh.VertexConnections[index1];
            HybridIntList connections2 = Mesh.VertexConnections[index2];
            
            for (int i = 0; i < connections1.Count; i++) {
                int connection = connections1[i];
                if (connections2.Contains(connection)) {
                    sharedConnections++;
                }
            }
            
            HybridIntList tris1 = Mesh.VertexTriangles[index1];
            HybridIntList tris2 = Mesh.VertexTriangles[index2];
            
            for (int i = 0; i < tris1.Count; i++) {
                int tri = tris1[i];
                if (tris2.Contains(tri)) {
                    sharedTriangles++;
                }
            }

            return sharedConnections <= 2 && sharedTriangles == sharedConnections;
        }

        private bool CheckTriangle(int triStart, ref UnsafeArray<bool> removedVertices, int mergeNum) {
            if (!ShouldRemoveTriangle(triStart)) {
                return false;
            }

            if (mergeNum == 13 && MaxMerges > 0) {
                Debug.Log("");
            }

            if (TriangleHasConnections(triStart)) {
                // float4 center = (vertex1 + vertex2 + vertex3) / 3;
                // Mesh.Vertices[index1] = center;
                //
                // MergeVertices(index2, index1);
                //
                // if (Mesh.VertexConnections[index1].Contains(index3)) {
                //     MergeVertices(index3, index1);
                // }
                //
                // removedVertices[index2] = true;
                // removedVertices[index3] = true;
                int edgeToCollapse = -1;
                float shortestEdgeSqr = float.MaxValue;
                for (int i = 0; i < 3; i++) {
                    int edgeIndex1 = Mesh.TriangleList[triStart + i];
                    int edgeIndex2 = Mesh.TriangleList[triStart + (i + 1) % 3];
                    
                    float distSqr = math.lengthsq(Mesh.Vertices[edgeIndex1] - Mesh.Vertices[edgeIndex2]);
                    if (distSqr >= shortestEdgeSqr) continue;

                    if (!IsEdgeCollapsible(edgeIndex1, edgeIndex2)) continue;
                    
                    shortestEdgeSqr = distSqr;
                    edgeToCollapse = i;
                }

                if (edgeToCollapse >= 0) {
                    int edgeIndex1 = Mesh.TriangleList[triStart + edgeToCollapse];
                    int edgeIndex2 = Mesh.TriangleList[triStart + (edgeToCollapse + 1) % 3];
                    float4 center = (Mesh.Vertices[edgeIndex1] + Mesh.Vertices[edgeIndex2]) * 0.5f;
                    Mesh.Vertices[edgeIndex1] = center;
                        
                    MergeVertices(edgeIndex2, edgeIndex1);
                    removedVertices[edgeIndex2] = true;
                }
            } else {
                for (int i = 0; i < 3; i++) {
                    int vertexIndex = Mesh.TriangleList[triStart + i];
                    Mesh.TriangleList[triStart + i] = -1;
                    
                    removedVertices[vertexIndex] = true;
                    
                    HybridIntList connections = Mesh.VertexConnections[vertexIndex];
                    if (connections.Count != 2) {
                        Debug.LogError($"Unexpected vertex count {connections.Count} for vertex {vertexIndex}.");
                    }
                    
                    connections.Dispose();
                    Mesh.VertexConnections[vertexIndex] = default;
                    
                    HybridIntList tris = Mesh.VertexTriangles[vertexIndex];
                    tris.Dispose();
                    Mesh.VertexTriangles[vertexIndex] = default;
                }
            }
            
            return true;
        }

        private bool ShouldRemoveTriangle(int triStart) {
            int index1 = Mesh.TriangleList[triStart + 0];
            int index2 = Mesh.TriangleList[triStart + 1];
            int index3 = Mesh.TriangleList[triStart + 2];

            if (index1 < 0 || index2 < 0 || index3 < 0) return false;

            // If normal flips from a shrinkwrap op, we always want to merge the triangle together.
            if (CheckOldNormals) {
                float4 normal = Mesh.TriangleNormals[triStart / 3];
                float4 oldNormal = OldNormals[triStart / 3];

                if (math.dot(normal, oldNormal) < 0) return true;
            }

            float4 vertex1 = Mesh.Vertices[index1];
            float4 vertex2 = Mesh.Vertices[index2];
            float4 vertex3 = Mesh.Vertices[index3];

            float4 edge1 = vertex2 - vertex1;
            float4 edge2 = vertex3 - vertex1;

            float3 cross = math.cross(edge1.xyz, edge2.xyz);
            float areaSqr = math.lengthsq(cross) * 0.25f;
            
            return areaSqr < AreaThresholdSqr;
        }

        private bool TriangleHasConnections(int triStart) {
            for (int i = 0; i < 3; i++) {
                int vertexIndex = Mesh.TriangleList[triStart + i];
                if (Mesh.VertexConnections[vertexIndex].Count > 1) return true;
            }
            
            return false;
        }

        private void MergeVertices(int vertexBeingRemoved, int targetVertex) {
            HybridIntList trisToMerge = Mesh.VertexTriangles[vertexBeingRemoved];
            HybridIntList destTriList = Mesh.VertexTriangles[targetVertex];

            HybridIntList connectionsToMerge = Mesh.VertexConnections[vertexBeingRemoved];
            HybridIntList destConnectionList = Mesh.VertexConnections[targetVertex];

            for (int i = 0; i < trisToMerge.Count; i++) {
                int otherTri = trisToMerge[i];

                int otherIndex1 = Mesh.TriangleList[otherTri + 0];
                int otherIndex2 = Mesh.TriangleList[otherTri + 1];
                int otherIndex3 = Mesh.TriangleList[otherTri + 2];

                if (otherIndex1 < 0 || otherIndex2 < 0 || otherIndex3 < 0) continue;

                int indexInDestination = destTriList.IndexOf(otherTri);
                if (indexInDestination >= 0) {
                    destTriList.RemoveAtSwapBack(indexInDestination);

                    int oppositeIndex = -1;
                    if (otherIndex1 != vertexBeingRemoved && otherIndex1 != targetVertex) {
                        oppositeIndex = otherIndex1;
                    } else if (otherIndex2 != vertexBeingRemoved && otherIndex2 != targetVertex) {
                        oppositeIndex = otherIndex2;
                    } else if (otherIndex3 != vertexBeingRemoved && otherIndex3 != targetVertex) {
                        oppositeIndex = otherIndex3;
                    } else {
                        Debug.LogError(
                            $"Triangle vertex mismatch: {otherIndex1}, {otherIndex2}, {otherIndex3} does not contain vertex that is not {vertexBeingRemoved} or {targetVertex}.");
                    }

                    HybridIntList oppositeTriList = Mesh.VertexTriangles[oppositeIndex];
                    oppositeTriList.RemoveAtSwapBack(oppositeTriList.IndexOf(otherTri));
                    Mesh.VertexTriangles[oppositeIndex] = oppositeTriList;
                    
                    // If opposite vertex no longer shares any triangles with the target or removed vertex,
                    // we need to mark them as no longer connected as well.

                    bool sharesAny = false;
                    for (int j = 0; j < oppositeTriList.Count; j++) {
                        int oppositeTri = oppositeTriList[j];
                        if (trisToMerge.Contains(oppositeTri) || destTriList.Contains(oppositeTri)) {
                            sharesAny = true;
                            break;
                        }
                    }
                    
                    if (!sharesAny) {
                        HybridIntList oppositeConnections = Mesh.VertexConnections[oppositeIndex];
                        int indexOfTarget = oppositeConnections.IndexOf(targetVertex);

                        if (indexOfTarget >= 0) {
                            oppositeConnections.RemoveAtSwapBack(indexOfTarget);
                            Mesh.VertexConnections[oppositeIndex] = oppositeConnections;
                        
                            destConnectionList.RemoveAtSwapBack(destConnectionList.IndexOf(oppositeIndex));
                        }
                        
                        int indexOfRemoved = oppositeConnections.IndexOf(vertexBeingRemoved);
                        if (indexOfRemoved >= 0) {
                            oppositeConnections.RemoveAtSwapBack(indexOfRemoved);
                            Mesh.VertexConnections[oppositeIndex] = oppositeConnections;
                            
                            connectionsToMerge.RemoveAtSwapBack(connectionsToMerge.IndexOf(oppositeIndex));
                        }
                    }

                    Mesh.TriangleList[otherTri + 0] = -1;
                    Mesh.TriangleList[otherTri + 1] = -1;
                    Mesh.TriangleList[otherTri + 2] = -1;
                } else {
                    if (otherIndex1 == vertexBeingRemoved) {
                        Mesh.TriangleList[otherTri + 0] = targetVertex;
                    } else if (otherIndex2 == vertexBeingRemoved) {
                        Mesh.TriangleList[otherTri + 1] = targetVertex;
                    } else if (otherIndex3 == vertexBeingRemoved) {
                        Mesh.TriangleList[otherTri + 2] = targetVertex;
                    } else {
                        Debug.LogError($"Triangle vertex mismatch: {otherIndex1}, {otherIndex2}, {otherIndex3} does not contain {targetVertex}.");
                    }

                    destTriList.Add(otherTri);
                }
            }

            Mesh.VertexTriangles[targetVertex] = destTriList;

            trisToMerge.Dispose();
            Mesh.VertexTriangles[vertexBeingRemoved] = default;

            destConnectionList.RemoveAtSwapBack(destConnectionList.IndexOf(vertexBeingRemoved));

            for (int j = 0; j < connectionsToMerge.Count; j++) {
                int otherVertex = connectionsToMerge[j];
                if (otherVertex == targetVertex) continue;

                if (!destConnectionList.Contains(otherVertex)) {
                    destConnectionList.Add(otherVertex);
                }

                HybridIntList otherConnectionList = Mesh.VertexConnections[otherVertex];
                int indexOfRemoved = otherConnectionList.IndexOf(vertexBeingRemoved);
                if (otherConnectionList.Contains(targetVertex)) {
                    otherConnectionList.RemoveAtSwapBack(indexOfRemoved);
                } else {
                    otherConnectionList[indexOfRemoved] = targetVertex;
                }

                Mesh.VertexConnections[otherVertex] = otherConnectionList;
            }

            Mesh.VertexConnections[targetVertex] = destConnectionList;

            connectionsToMerge.Dispose();
            Mesh.VertexConnections[vertexBeingRemoved] = default;
        }
    }
}
