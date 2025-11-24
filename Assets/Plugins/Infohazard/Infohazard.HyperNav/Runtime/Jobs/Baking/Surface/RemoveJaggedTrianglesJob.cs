// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    /// <summary>
    /// Remove triangles that have a vertex that is not part of any other triangle.
    /// This cleans up the edges of the mesh, removing jagged edges.
    /// </summary>
    [BurstCompile]
    public struct RemoveJaggedTrianglesJob : IJobParallelFor {
        [ReadOnly] public NativeArray<int> ValidTriangles;

        [FormerlySerializedAs("TriangleList"),NativeDisableParallelForRestriction]
        public NativeArray<int> TriangleIndices;

        [ReadOnly]
        public NativeArray<HybridIntList> VertexTriangles;

        public void Execute(int index) {
            int triangleStart = ValidTriangles[index];

            int index1 = TriangleIndices[triangleStart + 0];
            int index2 = TriangleIndices[triangleStart + 1];
            int index3 = TriangleIndices[triangleStart + 2];

            HybridIntList vertex1Triangles = VertexTriangles[index1];
            HybridIntList vertex2Triangles = VertexTriangles[index2];
            HybridIntList vertex3Triangles = VertexTriangles[index3];

            if (vertex1Triangles.Count == 1 || vertex2Triangles.Count == 1 || vertex3Triangles.Count == 1) {
                TriangleIndices[triangleStart + 0] = -1;
                TriangleIndices[triangleStart + 1] = -1;
                TriangleIndices[triangleStart + 2] = -1;
            }
        }
    }
}
