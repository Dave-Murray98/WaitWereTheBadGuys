// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;

namespace Infohazard.HyperNav.Jobs.Utility {
    public static class NativeUtility {
        public static void EnqueueChecked<T>(this ref UnsafeRingQueue<T> queue, T item) where T : unmanaged {
            bool result = queue.TryEnqueue(item);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!result) {
                throw new InvalidOperationException("Failed to enqueue value");
            }
#endif
        }
    }
}
