// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Threading;
using Unity.Burst.Intrinsics;
using Unity.Collections;

namespace Infohazard.HyperNav.Jobs.Utility {
    /// <summary>
    /// A thread-safe queue for use in Burst jobs.
    /// </summary>
    /// <typeparam name="T">Type of item.</typeparam>
    public readonly unsafe struct UnsafeConcurrentQueue<T> : IDisposable where T : unmanaged {
        public int Capacity => _items.Length;
        public int Count => _startCountMutex[1];

        private readonly UnsafeArray<T> _items;
        private readonly UnsafeArray<int> _startCountMutex;

        public UnsafeConcurrentQueue(int capacity, Allocator allocator) {
            _items = new UnsafeArray<T>(capacity, allocator);

            _startCountMutex = new UnsafeArray<int>(3, allocator);
            _startCountMutex[0] = 0;
            _startCountMutex[1] = 0;
            _startCountMutex[2] = 0;

            Common.Pause();
        }

        public void Dispose() {
            _items.Dispose();
            _startCountMutex.Dispose();
        }

        public bool TryPeek(out T item) {
            using Lock l = new((int*) _startCountMutex.Pointer + 2);

            int start = _startCountMutex[0];
            int count = _startCountMutex[1];

            if (count <= 0) {
                item = default;
                return false;
            }

            item = _items[start];
            return true;
        }

        public bool TryEnqueue(T item) {
            using Lock l = new((int*) _startCountMutex.Pointer + 2);

            int start = _startCountMutex[0];
            int count = _startCountMutex[1];

            if (count >= _items.Length) {
                return false;
            }

            _items[(start + count) % _items.Length] = item;
            _startCountMutex[1] = count + 1;

            return true;
        }

        public void EnqueueChecked(T item) {
            bool result = TryEnqueue(item);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!result) {
                throw new InvalidOperationException("Failed to enqueue value");
            }
#endif
        }

        public bool TryDequeue(out T item) {
            using Lock l = new((int*) _startCountMutex.Pointer + 2);

            int start = _startCountMutex[0];
            int count = _startCountMutex[1];

            if (count <= 0) {
                item = default;
                return false;
            }

            item = _items[start];
            _startCountMutex[0] = (start + 1) % _items.Length;
            _startCountMutex[1] = count - 1;

            return true;
        }

        public void Clear() {
            using Lock l = new((int*) _startCountMutex.Pointer + 2);

            _startCountMutex[0] = 0;
            _startCountMutex[1] = 0;
        }

        private readonly struct Lock : IDisposable {
            private readonly int* _mutex;

            public Lock(int* mutex) {
                _mutex = mutex;
                while (Interlocked.CompareExchange(ref *_mutex, 1, 0) != 0) {
                    Common.Pause();
                }
            }

            public void Dispose() {
                Interlocked.Exchange(ref *_mutex, 0);
            }
        }
    }
}
