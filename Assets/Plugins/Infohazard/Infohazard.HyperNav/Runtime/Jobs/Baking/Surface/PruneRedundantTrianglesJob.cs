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
    public struct PruneRedundantTrianglesJob : IJobParallelFor {
        // Assuming everything works right, each instance should only modify vertices in its own island.
        [NativeDisableParallelForRestriction]
        public BakingNavSurfaceMeshInfo Mesh;

        [ReadOnly]
        public NativeArray<int> IslandIndices;

        public NativeArray<UnsafeHashMap<Edge, int2>> EdgeOccurrenceMaps;

        public NativeCancellationToken CancellationToken;

        public float AreaThreshold;
        
        public void Execute(int index) {
            UnsafeRingQueue<int> trianglesToCheck = new(Mesh.TriangleList.Length / 3, Allocator.Temp);

            for (int i = 0; i < Mesh.TriangleList.Length; i += 3) {
                int vertexIndex = Mesh.TriangleList[i];
                if (vertexIndex == -1) continue;
                int islandIndex = IslandIndices[vertexIndex];
                if (islandIndex != index) continue;
                
                trianglesToCheck.Enqueue(i);
            }
            
            UnsafeHashMap<Edge, int2> edgeOccurrenceMap = EdgeOccurrenceMaps[index];

            while (trianglesToCheck.TryDequeue(out int triStart)) {
                if (CancellationToken.IsCancellationRequested) {
                    EdgeOccurrenceMaps[index] = edgeOccurrenceMap;
                    return;
                }

                if (!CanPrune(triStart, ref edgeOccurrenceMap, out int adjacentTri)) continue;
                Prune(triStart, adjacentTri, ref edgeOccurrenceMap);
                    
                if (adjacentTri != -1) {
                    trianglesToCheck.Enqueue(adjacentTri);
                }
            }
            
            // In case it gets reallocated for some reason we need to put it back.
            EdgeOccurrenceMaps[index] = edgeOccurrenceMap;
        }

        private bool CanPrune(int triStart, ref UnsafeHashMap<Edge, int2> edgeOccurrenceMap, out int adjacentTri) {
            adjacentTri = -1;
            
            if (Mesh.TriangleList[triStart] == -1) return false;
            
            for (int i = 0; i < 3; i++) {
                int edgeIndex1 = Mesh.TriangleList[triStart + i];
                int edgeIndex2 = Mesh.TriangleList[triStart + (i + 1) % 3];
                
                Edge edge = new(edgeIndex1, edgeIndex2);
                if (!edgeOccurrenceMap.TryGetValue(edge, out int2 occurrence)) {
                    Debug.LogError($"Edge {edge.ToInt2()} not found in map.");
                } else {
                    int otherTri = occurrence.x == triStart ? occurrence.y : occurrence.x;
                    if (otherTri == -1) continue;
                    
                    if (adjacentTri == -1) {
                        adjacentTri = otherTri;
                    } else {
                        return false;
                    }
                }
            }
            
            int index1 = Mesh.TriangleList[triStart + 0];
            int index2 = Mesh.TriangleList[triStart + 1];
            int index3 = Mesh.TriangleList[triStart + 2];
            
            float4 v1 = Mesh.Vertices[index1];
            float4 v2 = Mesh.Vertices[index2];
            float4 v3 = Mesh.Vertices[index3];
            
            float areaSqr = math.lengthsq(math.cross(v2.xyz - v1.xyz, v3.xyz - v1.xyz));
            return areaSqr < AreaThreshold;
        }

        private void Prune(int triStart, int adjacentTri, ref UnsafeHashMap<Edge, int2> edgeOccurrenceMap) {
            int adjacentIndex1 = -1;
            int adjacentIndex2 = -1;
            int adjacentIndex3 = -1;

            if (adjacentTri >= 0) {
                adjacentIndex1 = Mesh.TriangleList[adjacentTri + 0];
                adjacentIndex2 = Mesh.TriangleList[adjacentTri + 1];
                adjacentIndex3 = Mesh.TriangleList[adjacentTri + 2];
            }
            
            for (int i = 0; i < 3; i++) {
                int curIndex = Mesh.TriangleList[triStart + i];
                
                HybridIntList triangles = Mesh.VertexTriangles[curIndex];
                triangles.RemoveAtSwapBack(triangles.IndexOf(triStart));
                
                HybridIntList connections = Mesh.VertexConnections[curIndex];
                int nextIndex = Mesh.TriangleList[triStart + (i + 1) % 3];
                int prevIndex = Mesh.TriangleList[triStart + (i + 2) % 3];
                
                if (triangles.Count == 0) {
                    connections.Dispose();
                    triangles.Dispose();
                    connections = default;
                    triangles = default;
                } else {
                    if (prevIndex != adjacentIndex1 && prevIndex != adjacentIndex2 && prevIndex != adjacentIndex3) {
                        connections.RemoveAtSwapBack(connections.IndexOf(prevIndex));
                    }
                    
                    if (nextIndex != adjacentIndex1 && nextIndex != adjacentIndex2 && nextIndex != adjacentIndex3) {
                        connections.RemoveAtSwapBack(connections.IndexOf(nextIndex));
                    }
                }
                
                Mesh.VertexConnections[curIndex] = connections;
                Mesh.VertexTriangles[curIndex] = triangles;
                
                Edge edge = new(curIndex, nextIndex);
                if (!edgeOccurrenceMap.TryGetValue(edge, out int2 occurrence)) {
                    Debug.LogError($"Edge {edge.ToInt2()} not found in map.");
                } else {
                    if (occurrence.x == triStart) {
                        edgeOccurrenceMap[edge] = new int2(-1, occurrence.y);
                    } else {
                        edgeOccurrenceMap[edge] = new int2(occurrence.x, -1);
                    }
                }
            }
            
            Mesh.TriangleList[triStart + 0] = -1;
            Mesh.TriangleList[triStart + 1] = -1;
            Mesh.TriangleList[triStart + 2] = -1;
        }
    }
}