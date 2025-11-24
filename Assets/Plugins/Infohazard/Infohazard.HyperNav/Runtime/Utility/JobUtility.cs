// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using UnityEngine;

namespace Infohazard.HyperNav {
    public static class JobUtility {
        /// <summary>
        /// Hacky way to ensure an IJobParallelFor job doesn't use all cores.
        /// </summary>
        /// <remarks>
        /// This is necessary because the Job system doesn't provide a way to limit the number of threads used.
        /// If all the cores are used, Unity's rendering and physics will be blocked by the job.
        /// </remarks>
        /// <param name="arrayLength">Number of elements in the IJobParallelFor.</param>
        /// <returns>Minimum batch size.</returns>
        public static int GetMinimumBatchSizeToAvoidUsingAllCores(int arrayLength) {
            int cpuCount = Environment.ProcessorCount;
            int cpusToUse = Mathf.Max(1, cpuCount - 2);
            return Mathf.Max(1, Mathf.CeilToInt(arrayLength / (float)cpusToUse));
        }
    }
}
