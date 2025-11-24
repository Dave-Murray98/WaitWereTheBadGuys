// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Infohazard.HyperNav.Jobs.Utility {
    /// <summary>
    /// This is a simple wrapper for unmanaged memory which bypasses Unity's safety checks.
    /// This allows arrays to be nested in other arrays (or in structs contained in arrays).
    /// Note that you must keep a reference to the original NativeArray, or Unity will detect a memory leak.
    /// </summary>
    /// <typeparam name="T">Element type of the array.</typeparam>
    public readonly struct UnsafeArray<T> : IDisposable where T : unmanaged {
        public static UnsafeArray<T> Null => new(IntPtr.Zero, 0);

        /// <summary>
        /// Length of the array.
        /// </summary>
        public readonly int Length;

        /// <summary>
        /// Pointer to the start of the array.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public readonly IntPtr Pointer;

        /// <summary>
        /// Allocator used to allocate the array, or None if it was created from a NativeArray.
        /// </summary>
        public readonly Allocator Allocator;

        /// <summary>
        /// Returns whether the pointer is null.
        /// </summary>
        public bool IsNull => Pointer == IntPtr.Zero;

        /// <summary>
        /// Returns the size of the memory block in bytes.
        /// </summary>
        public int MemorySize => Length * UnsafeUtility.SizeOf<T>();

        /// <summary>
        /// Get a reference to the element at the given index (can be used to set values as well).
        /// </summary>
        /// <param name="index">The index.</param>
        /// <exception cref="InvalidOperationException">(Dev Only) If underlying array is not set.</exception>
        /// <exception cref="IndexOutOfRangeException">(Dev Only) If index is outside the bounds of the array.</exception>
        public unsafe ref T this[int index] {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (Pointer == IntPtr.Zero) throw new InvalidOperationException();
                if (index < 0 || index >= Length) throw new IndexOutOfRangeException($"Index {index} out of range {Length}");
#endif

                return ref UnsafeUtility.ArrayElementAsRef<T>((void*) Pointer, index);
            }
        }

        /// <summary>
        /// Allocate a new array of the given length.
        /// </summary>
        /// <param name="length">Number of items to hold.</param>
        /// <param name="allocator">Allocator to use.</param>
        /// <param name="clearMemory">Whether to clear the memory.</param>
        /// <returns>The created array pointer.</returns>
        public unsafe UnsafeArray(int length, Allocator allocator, bool clearMemory = true) {
            int byteLength = length * UnsafeUtility.SizeOf<T>();
            void* ptr = UnsafeUtility.MallocTracked(byteLength, UnsafeUtility.AlignOf<T>(), allocator, 0);
            if (clearMemory) UnsafeUtility.MemClear(ptr, byteLength);

            Length = length;
            Pointer = (IntPtr) ptr;
            Allocator = allocator;
        }

        /// <summary>
        /// Wrap an existing pre-allocated memory block.
        /// </summary>
        /// <param name="ptr">Pointer to the memory block.</param>
        /// <param name="length">Length of the memory block.</param>
        /// <param name="allocator">Allocator to use for deallocation. None means this object won't free the memory.</param>
        public unsafe UnsafeArray(T* ptr, int length, Allocator allocator = Allocator.None) {
            Pointer = (IntPtr) ptr;
            Length = length;
            Allocator = allocator;
        }

        /// <summary>
        /// Wrap an existing pre-allocated memory block.
        /// </summary>
        /// <param name="ptr">Pointer to the memory block.</param>
        /// <param name="length">Length of the memory block.</param>
        /// <param name="allocator">Allocator to use for deallocation. None means this object won't free the memory.</param>
        public UnsafeArray(IntPtr ptr, int length, Allocator allocator = Allocator.None) {
            Pointer = ptr;
            Length = length;
            Allocator = allocator;
        }

        /// <summary>
        /// Free the memory if it has been allocated directly.
        /// </summary>
        /// <remarks>
        /// If this pointer is wrapping a NativeArray, this does nothing.
        /// </remarks>
        public unsafe void Dispose() {
            if (Allocator > Allocator.None && Pointer != IntPtr.Zero) {
                UnsafeUtility.FreeTracked((void*)Pointer, Allocator);
            }
        }

        /// <summary>
        /// Create a pointer to the given NativeArray.
        /// </summary>
        /// <param name="array">Array to create a pointer to.</param>
        /// <returns>The created pointer.</returns>
        public static unsafe UnsafeArray<T> ToPointer(in NativeArray<T> array) {
            if (!array.IsCreated) return Null;

            return new UnsafeArray<T>((IntPtr) array.GetUnsafePtr(), array.Length);
        }

        /// <summary>
        /// Get an enumerator for the array.
        /// </summary>
        public Enumerator GetEnumerator() => new(this);

        public struct Enumerator : IEnumerator<T> {
            private readonly UnsafeArray<T> _array;
            private int _index;

            public Enumerator(UnsafeArray<T> array) {
                _array = array;
                _index = -1;
            }

            public T Current => _array[_index];

            object System.Collections.IEnumerator.Current => Current;

            public void Dispose() { }

            public bool MoveNext() => ++_index < _array.Length;

            public void Reset() => _index = -1;
        }
    }
}
