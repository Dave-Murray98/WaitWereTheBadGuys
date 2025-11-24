// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Infohazard.HyperNav.Jobs.Utility {
    public struct HybridIntList : IDisposable {
        private FixedList64Bytes<int> _fixedPortion;
        private UnsafeList<int> _dynamicPortion;
        public readonly Allocator Allocator;

        public const int FixedCapacity = 15;

        public int Count => _fixedPortion.Length + _dynamicPortion.Length;

        public HybridIntList(Allocator allocator) {
            _fixedPortion = default;
            _dynamicPortion = default;
            Allocator = allocator;
        }

        public int this[int index] {
            get {
                CheckIndex(index, Count);
                if (index < FixedCapacity) return _fixedPortion[index];
                return _dynamicPortion[index - FixedCapacity];
            }
            set {
                CheckIndex(index, Count);
                if (index < FixedCapacity) _fixedPortion[index] = value;
                else _dynamicPortion[index - FixedCapacity] = value;
            }
        }

        public void CopyFrom(in HybridIntList other) {
            _fixedPortion = other._fixedPortion;

            if (!other._dynamicPortion.IsCreated || other._dynamicPortion.Length == 0) {
                _dynamicPortion.Clear();
                return;
            }

            if (!_dynamicPortion.IsCreated) {
                _dynamicPortion = new UnsafeList<int>(other._dynamicPortion.Length, Allocator);
            }

            _dynamicPortion.CopyFrom(other._dynamicPortion);
        }

        public void Add(int value) {
            if (_fixedPortion.Length < FixedCapacity) {
                _fixedPortion.Add(value);
            } else {
                if (!_dynamicPortion.IsCreated) {
                    _dynamicPortion = new UnsafeList<int>(FixedCapacity, Allocator);
                }

                _dynamicPortion.Add(value);
            }
        }

        public int IndexOf(int value) {
            int indexFixed = _fixedPortion.IndexOf(value);
            if (indexFixed >= 0) return indexFixed;

            if (!_dynamicPortion.IsCreated) return -1;

            int indexDynamic = _dynamicPortion.IndexOf(value);
            if (indexDynamic >= 0) return indexDynamic + FixedCapacity;

            return -1;
        }

        public bool Contains(int value) {
            return IndexOf(value) >= 0;
        }

        public void RemoveAt(int index) {
            CheckIndex(index, Count);

            if (index >= FixedCapacity) {
                _dynamicPortion.RemoveAt(index - FixedCapacity);
                return;
            }

            _fixedPortion.RemoveAt(index);

            if (_dynamicPortion.Length == 0) return;

            _fixedPortion.Add(_dynamicPortion[0]);
            _dynamicPortion.RemoveAt(0);
        }

        public void RemoveAtSwapBack(int index) {
            CheckIndex(index, Count);

            if (index >= FixedCapacity) {
                _dynamicPortion.RemoveAtSwapBack(index - FixedCapacity);
                return;
            }

            if (!_dynamicPortion.IsCreated || _dynamicPortion.Length == 0) {
                _fixedPortion.RemoveAtSwapBack(index);
                return;
            }

            _fixedPortion[index] = _dynamicPortion[^1];
            _dynamicPortion.Length--;
        }

        public void Clear(bool dispose) {
            _fixedPortion.Clear();

            if (_dynamicPortion.IsCreated) {
                if (dispose) {
                    _dynamicPortion.Dispose();
                    _dynamicPortion = default;
                } else {
                    _dynamicPortion.Clear();
                }
            }
        }

        public void Dispose() {
            if (_dynamicPortion.IsCreated) _dynamicPortion.Dispose();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndex(int index, int count) {
            if (index < 0 || index >= count) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for list of length {count}.");
            }
        }
    }
}
