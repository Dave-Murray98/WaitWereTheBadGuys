// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking {
    public static class RemoveNegativeElementsJobs {
        [BurstCompile]
        public struct Int3 : IJob {
            public NativeList<int3> List;
            
            public void Execute() {
                int removedCount = 0;
                for (int i = 0; i < List.Length; i++) {
                    int3 item = List[i];
                    if (item.x < 0 || item.y < 0 || item.z < 0) {
                        removedCount++;
                    } else {
                        List[i - removedCount] = List[i];
                    }
                }
                
                List.Length -= removedCount;
            }
        }
    }
}