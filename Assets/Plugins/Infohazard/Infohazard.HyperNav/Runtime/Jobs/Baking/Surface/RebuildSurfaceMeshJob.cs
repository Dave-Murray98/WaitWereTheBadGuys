// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct RebuildSurfaceMeshJob : IJob {
        [ReadOnly]
        public BakingNavSurfaceMeshInfo OldMesh;
        
        public BakingNavSurfaceMeshInfo NewMesh;
        
        public unsafe void Execute() {
            UnsafeArray<int> vertexIndexMap = new(OldMesh.TriangleList.Length, Allocator.Temp);
            UnsafeUtility.MemSet((void*)vertexIndexMap.Pointer, 0xFF, vertexIndexMap.Length * sizeof(int));
            
            UnsafeArray<int> triangleIndexMap = new(OldMesh.TriangleList.Length / 3, Allocator.Temp);
            UnsafeUtility.MemSet((void*)triangleIndexMap.Pointer, 0xFF, triangleIndexMap.Length * sizeof(int));
            
            // Copy vertices and indices.
            for (int i = 0; i < OldMesh.TriangleList.Length; i += 3) {
                int firstIndex = OldMesh.TriangleList[i];
                if (firstIndex < 0) continue;

                for (int j = 0; j < 3; j++) {
                    int index = OldMesh.TriangleList[i + j];
                    if (index < 0) continue;
                    
                    int newIndex = vertexIndexMap[index];
                    if (newIndex == -1) {
                        newIndex = NewMesh.Vertices.Length;
                        vertexIndexMap[index] = newIndex;
                        NewMesh.Vertices.Add(OldMesh.Vertices[index]);
                        NewMesh.VertexConnections.Add(new HybridIntList(Allocator.Persistent));
                        NewMesh.VertexTriangles.Add(new HybridIntList(Allocator.Persistent));
                    }
                    
                    NewMesh.TriangleList.Add(newIndex);
                }
                
                NewMesh.TriangleNormals.Add(OldMesh.TriangleNormals[i / 3]);
                NewMesh.TriangleUprightWorldDirections.Add(OldMesh.TriangleUprightWorldDirections[i / 3]);
            }
        }
    }
}