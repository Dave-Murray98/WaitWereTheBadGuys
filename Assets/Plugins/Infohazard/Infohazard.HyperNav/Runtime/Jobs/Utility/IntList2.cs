// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Diagnostics;

namespace Infohazard.HyperNav.Jobs.Utility {
    /// <summary>
    /// Serves as a list of ints with a max size of 2, to avoid allocations.
    /// </summary>
    /// <remarks>
    /// Used to store which regions each vertex is part of, and which other vertices it's connected to.
    /// </remarks>
    public unsafe struct IntList2 {
        /// <summary>
        /// The number of elements in the list.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// The maximum number of elements the list can hold.
        /// </summary>
        public const int MaxCount = 2;

        private fixed int _data[MaxCount];

        /// <summary>
        /// Get or set the value at the given index.
        /// </summary>
        /// <param name="index">Index to get or set.</param>
        /// <exception cref="IndexOutOfRangeException">If the index is out of range.</exception>
        public int this[int index] {
            get {
                CheckIndex(index, Count);
                return _data[index];
            }

            set {
                CheckIndex(index, Count);
                _data[index] = value;
            }
        }

        /// <summary>
        /// Add a value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="IndexOutOfRangeException">If the list is full.</exception>
        public void Add(int value) {
            CheckIndex(Count, MaxCount);
            _data[Count++] = value;
        }

        /// <summary>
        /// Check if the list contains the given value.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>Whether the value was found.</returns>
        public bool Contains(int value) {
            for (int i = 0; i < Count; i++) {
                if (_data[i] == value) {
                    return true;
                }
            }

            return false;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndex(int index, int max) {
            if (index < 0 || index >= max) {
                throw new IndexOutOfRangeException();
            }
        }
    }
}
