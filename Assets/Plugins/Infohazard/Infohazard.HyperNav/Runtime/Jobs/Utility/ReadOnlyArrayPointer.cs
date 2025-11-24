// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;

namespace Infohazard.HyperNav.Jobs.Utility {
    /// <summary>
    /// Reference over a native array, which does not allow the memory to be modified.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    public unsafe struct ReadOnlyArrayPointer<T> where T : unmanaged {
        [NativeDisableUnsafePtrRestriction]
        private readonly T* _ptr;
        private readonly int _length;

        public ReadOnlyArrayPointer(T* ptr, int length) {
            _ptr = ptr;
            _length = length;
        }

        public readonly int Length => _length;

        public readonly T this[int index] {
            get {
                CheckIndex(index);
                return _ptr[index];
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private readonly void CheckIndex(int index) {
            if (index >= _length) {
                throw new IndexOutOfRangeException();
            }
        }
    }
}
