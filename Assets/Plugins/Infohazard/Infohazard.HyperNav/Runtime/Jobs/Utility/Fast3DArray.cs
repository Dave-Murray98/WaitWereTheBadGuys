// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Infohazard.HyperNav.Jobs.Utility {
    /// <summary>
    /// A data structure equivalent to a three-dimensional int array (int[,,]), but more efficient.
    /// </summary>
    /// <remarks>
    /// It does not perform bounds checks, and uses an unsafe pointer to the data.
    /// </remarks>
    public readonly unsafe struct Fast3DArray : IDisposable {
        /// <summary>
        /// Size of first dimension.
        /// </summary>
        public readonly int SizeX;

        /// <summary>
        /// Size of second dimension.
        /// </summary>
        public readonly int SizeY;

        /// <summary>
        /// Size of third dimension.
        /// </summary>
        public readonly int SizeZ;

        [NativeDisableUnsafePtrRestriction]
        public readonly int* Array;

        public readonly int Length;

        private readonly Allocator _allocator;

        public bool IsNull => Array == null;

        /// <summary>
        /// Construct a new Fast3DArray with the given dimensions.
        /// </summary>
        /// <param name="sizeX">Size of first dimension.</param>
        /// <param name="sizeY">Size of second dimension.</param>
        /// <param name="sizeZ">Size of third dimension.</param>
        /// <param name="allocator">Allocator to use for the array.</param>
        public Fast3DArray(int sizeX, int sizeY, int sizeZ, Allocator allocator = Allocator.Persistent) {
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            Length = sizeX * sizeY * sizeZ;
            _allocator = allocator;
            Array = (int*) UnsafeUtility.MallocTracked(Length * UnsafeUtility.SizeOf<int>(),
                                                       UnsafeUtility.AlignOf<int>(), allocator, 0);
        }

        /// <summary>
        /// Get or set the value at given coordinates.
        /// </summary>
        /// <param name="x">First coordinate.</param>
        /// <param name="y">Second coordinate.</param>
        /// <param name="z">Third coordinate.</param>
        public int this[int x, int y, int z] {
            get {
                CheckIndex(x, y, z);
                return Array[ToIndex(x, y, z)];
            }

            set {
                CheckIndex(x, y, z);
                Array[ToIndex(x, y, z)] = value;
            }
        }

        /// <summary>
        /// Get or set the value at the given index.
        /// </summary>
        public int this[int index] {
            get {
                CheckIndex(index);
                return Array[index];
            }

            set {
                CheckIndex(index);
                Array[index] = value;
            }
        }

        private int ToIndex(int x, int y, int z) {
            return x * SizeY * SizeZ + y * SizeZ + z;
        }

        /// <summary>
        /// Return true if the element at [x, y, z] is either option1 or option2.
        /// </summary>
        /// <param name="x">First coordinate.</param>
        /// <param name="y">Second coordinate.</param>
        /// <param name="z">Third coordinate.</param>
        /// <param name="option1">First option to check equality.</param>
        /// <param name="option2">Second option to check equality.</param>
        /// <returns>If the value at the given coordinates is equal to either option1 or option2.</returns>
        public bool IsOneOf(int x, int y, int z, int option1, int option2) {
            CheckIndex(x, y, z);
            int value = Array[ToIndex(x, y, z)];
            return value == option1 || value == option2;
        }

        /// <summary>
        /// Return true if the given item is contained at any position in the array.
        /// </summary>
        public bool Contains(int item) {
            for (int i = 0; i < Length; i++) {
                if (Array[i] == item) return true;
            }

            return false;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndex(int x, int y, int z) {
            if (x < 0 || x >= SizeX) throw new IndexOutOfRangeException("x");
            if (y < 0 || y >= SizeY) throw new IndexOutOfRangeException("y");
            if (z < 0 || z >= SizeZ) throw new IndexOutOfRangeException("z");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndex(int index) {
            if (index < 0 || index >= Length) throw new IndexOutOfRangeException("index");
        }

        public void Dispose() {
            UnsafeUtility.FreeTracked(Array, _allocator);
        }
    }
}
