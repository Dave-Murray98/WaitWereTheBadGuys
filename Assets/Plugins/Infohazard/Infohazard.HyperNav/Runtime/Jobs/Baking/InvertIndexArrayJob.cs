// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Infohazard.HyperNav.Jobs.Baking {
    /// <summary>
    /// Given an array of indices,
    /// creates an array where the value at each index is the index of that value in the input array.
    /// OutputArray is expected to be at least as large as the maximum value in InputArray plus one.
    /// </summary>
    [BurstCompile]
    public struct InvertIndexArrayJob : IJob {
        [ReadOnly]
        public NativeArray<int> InputArray;

        public NativeArray<int> OutputArray;

        public unsafe void Execute() {
            UnsafeUtility.MemSet(OutputArray.GetUnsafePtr(), 0xFF, OutputArray.Length * sizeof(int));

            for (int i = 0; i < InputArray.Length; i++) {
                int value = InputArray[i];
                OutputArray[value] = i;
            }
        }
    }
}
