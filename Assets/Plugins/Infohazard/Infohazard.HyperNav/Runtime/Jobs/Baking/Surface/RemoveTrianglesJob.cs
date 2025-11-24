// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct RemoveTrianglesJob : IJob {
        [ReadOnly]
        public NativeHashSet<int> TrianglesToRemove;

        public BakingNavSurfaceMeshInfo Mesh;

        public void Execute() {
            foreach (int triStart in TrianglesToRemove) {
                Mesh.TriangleList[triStart + 0] = -1;
                Mesh.TriangleList[triStart + 1] = -1;
                Mesh.TriangleList[triStart + 2] = -1;
            }
        }
    }
}
