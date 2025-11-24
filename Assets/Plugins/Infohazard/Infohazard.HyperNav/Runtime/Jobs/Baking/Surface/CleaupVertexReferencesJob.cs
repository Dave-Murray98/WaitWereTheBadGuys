// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct CleanupVertexTrianglesJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<int> TriangleIndices;

        public NativeArray<HybridIntList> VertexTriangles;

        public void Execute(int index) {
            HybridIntList vertexTriangles = VertexTriangles[index];

            for (int i = 0; i < vertexTriangles.Count; i++) {
                int triangleStart = vertexTriangles[i];
                if (TriangleIndices[triangleStart] != -1) continue;

                vertexTriangles.RemoveAtSwapBack(i);
                i--;
            }

            VertexTriangles[index] = vertexTriangles;
        }
    }
}
