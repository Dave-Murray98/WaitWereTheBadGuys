// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct CollectValidTrianglesJob : IJob {
        [ReadOnly]
        public NativeList<int> TriangleIndices;

        public NativeList<int> ValidTriangles;

        public void Execute() {
            for (int i = 0; i < TriangleIndices.Length; i += 3) {
                if (TriangleIndices[i] != -1) {
                    ValidTriangles.Add(i);
                }
            }
        }
    }
}
