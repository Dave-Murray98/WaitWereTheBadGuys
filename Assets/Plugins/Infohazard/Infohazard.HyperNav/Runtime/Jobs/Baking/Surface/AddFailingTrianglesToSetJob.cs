// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct AddFailingTrianglesToSetJob : IJob {
        [ReadOnly]
        public NativeArray<bool> PartiallyFailingTriangles;
        
        [ReadOnly]
        public NativeArray<bool> CompletelyFailingTriangles;

        public NativeHashSet<int> ToRemoveTrianglesSet;
        
        public NativeHashSet<int> ToSplitTriangleSet;

        public NativeList<int> TrianglesToCheck;

        public bool CanSplitTriangles;

        public void Execute() {
            for (int i = 0; i < TrianglesToCheck.Length; i++) {
                if (CompletelyFailingTriangles[i]) {
                    ToRemoveTrianglesSet.Add(TrianglesToCheck[i]);
                } else if (PartiallyFailingTriangles[i]) {
                    if (CanSplitTriangles) {
                        ToSplitTriangleSet.Add(TrianglesToCheck[i]);
                    } else {
                        ToRemoveTrianglesSet.Add(TrianglesToCheck[i]);
                    }
                }
            }

            for (int i = 0; i < TrianglesToCheck.Length; i++) {
                int triStart = TrianglesToCheck[i];
                if (ToRemoveTrianglesSet.Contains(triStart) || ToSplitTriangleSet.Contains(triStart)) {
                    TrianglesToCheck.RemoveAtSwapBack(i);
                    i--;
                }
            }
        }
    }
}
