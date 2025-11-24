// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Infohazard.HyperNav.Settings;
using UnityEngine;

namespace Infohazard.HyperNav {
    /// <summary>
    /// A single NavLayer. May have the value 0 to 31, or -1 for invalid.
    /// </summary>
    [Serializable]
    public struct NavLayer : IEquatable<NavLayer> {
        [SerializeField]
        private int _index;

        public readonly int Index => _index;

        public static readonly NavLayer None = new(-1);

        public NavLayer(int index) {
            if (index is < -1 or > 31)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be between -1 and 31.");
            _index = index;
        }

        /// <summary>
        /// Get a mask that represents this single layer.
        /// </summary>
        /// <returns>A mask that represents this layer.</returns>
        public readonly NavLayerMask AsMask() {
            return new NavLayerMask(Index < 0 ? 0 : (uint) 1 << Index);
        }

        /// <summary>
        /// Get the name of this layer from the settings.
        /// </summary>
        /// <returns>The name of the layer, or an empty string if it is invalid.</returns>
        public readonly string GetName() {
            if (Index < 0) return string.Empty;

            HyperNavSettings settings = HyperNavSettings.Instance;
            if (!settings) {
                Debug.LogError("HyperNav settings not found. You must create it to use NavLayer.GetName.");
                return string.Empty;
            }

            return settings.GetLayerName(Index);
        }

        /// <summary>
        /// Get the NavLayer corresponding to the given name in the settings.
        /// </summary>
        /// <param name="name">The name of the layer.</param>
        /// <returns>The layer with that name, or None if invalid.</returns>
        public static NavLayer Get(string name) {
            HyperNavSettings settings = HyperNavSettings.Instance;
            if (!settings) {
                Debug.LogError("HyperNav settings not found. You must create it to use NavLayer.Get.");
                return None;
            }

            if (!settings.TryGetLayerIndex(name, out int layerIndex)) {
                Debug.LogError($"Layer name {name} not found in HyperNav settings.");
                return None;
            }

            return new NavLayer(layerIndex);
        }

        /// <inheritdoc/>
        public readonly override string ToString() {
            if (Index < 0) return "None";

            string name = GetName();
            if (!string.IsNullOrEmpty(name)) return name;

            return $"Layer {Index}";
        }

        /// <inheritdoc/>
        public readonly bool Equals(NavLayer other) {
            return Index == other.Index;
        }

        /// <inheritdoc/>
        public readonly override bool Equals(object obj) {
            return obj is NavLayer other && Equals(other);
        }

        /// <inheritdoc/>
        public readonly override int GetHashCode() {
            return Index;
        }

        /// <summary>
        /// Equality check.
        /// </summary>
        public static bool operator ==(NavLayer layer1, NavLayer layer2) {
            return layer1.Index == layer2.Index;
        }

        /// <summary>
        /// Inequality check.
        /// </summary>
        public static bool operator !=(NavLayer layer1, NavLayer layer2) {
            return layer1.Index != layer2.Index;
        }

        /// <summary>
        /// Try to get the NavLayer corresponding to the given name in the settings.
        /// </summary>
        /// <param name="name">The name of the layer.</param>
        /// <param name="layer">The layer with that name, or None if invalid.</param>
        /// <returns>Whether the layer exists.</returns>
        public static bool TryGet(string name, out NavLayer layer) {
            HyperNavSettings settings = HyperNavSettings.Instance;
            if (!settings) {
                Debug.LogError("HyperNav settings not found. You must create it to use NavLayer.Get.");
                layer = None;
                return false;
            }

            if (!settings.TryGetLayerIndex(name, out int layerIndex)) {
                Debug.LogError($"Layer name {name} not found in HyperNav settings.");
                layer = None;
                return false;
            }

            layer = new NavLayer(layerIndex);
            return true;
        }

        public static explicit operator NavLayer(int i) => new(i);
        public static implicit operator int(NavLayer i) => i.Index;
    }

    /// <summary>
    /// A mask of NavLayers with one bit for each possible layer.
    /// </summary>
    [Serializable]
    public struct NavLayerMask : IEnumerable<NavLayer>, IEquatable<NavLayerMask> {
        [SerializeField]
        private uint _value;

        public readonly uint Value => _value;

        /// <summary>
        /// Whether this mask is empty.
        /// </summary>
        public readonly bool IsEmpty => Value is 0;

        /// <summary>
        /// A mask that represents no layers.
        /// </summary>
        public static readonly NavLayerMask None = new(0);

        /// <summary>
        /// A mask that represents all possible layers.
        /// </summary>
        public static readonly NavLayerMask All = new(0xFFFFFFFF);

        public NavLayerMask(uint value) {
            _value = value;
        }

        /// <summary>
        /// Returns true if the mask contains the given layer index.
        /// </summary>
        /// <param name="layer">The index of the layer to check.</param>
        /// <returns>Whether the mask contains that value.</returns>
        public readonly bool Contains(int layer) {
            return (Value & (1 << layer)) != 0;
        }

        /// <summary>
        /// Non-allocating enumerator over this mask.
        /// </summary>
        public readonly Enumerator GetEnumerator() {
            return new Enumerator(Value);
        }

        /// <inheritdoc/>
        IEnumerator<NavLayer> IEnumerable<NavLayer>.GetEnumerator() {
            return new Enumerator(Value);
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() {
            return new Enumerator(Value);
        }

        /// <inheritdoc/>
        public readonly override string ToString() {
            return string.Join(", ", this.Select(l => l.ToString()));
        }

        /// <inheritdoc/>
        public readonly bool Equals(NavLayerMask other) {
            return Value == other.Value;
        }

        /// <inheritdoc/>
        public readonly override bool Equals(object obj) {
            return obj is NavLayerMask other && Equals(other);
        }

        /// <inheritdoc/>
        public readonly override int GetHashCode() {
            return (int) Value;
        }

        /// <summary>
        /// Get a mask representing the combination of two layers.
        /// </summary>
        public static NavLayerMask Combine(NavLayer layer1, NavLayer layer2) {
            return layer1.AsMask() | layer2.AsMask();
        }

        /// <summary>
        /// Get a mask representing the combination of three layers.
        /// </summary>
        public static NavLayerMask Combine(NavLayer layer1, NavLayer layer2, NavLayer layer3) {
            return layer1.AsMask() | layer2.AsMask() | layer3.AsMask();
        }

        /// <summary>
        /// Get a mask representing the combination of four layers.
        /// </summary>
        public static NavLayerMask Combine(NavLayer layer1, NavLayer layer2, NavLayer layer3, NavLayer layer4) {
            return layer1.AsMask() | layer2.AsMask() | layer3.AsMask() | layer4.AsMask();
        }

        /// <summary>
        /// Equality check.
        /// </summary>
        public static bool operator ==(NavLayerMask layer1, NavLayerMask layer2) {
            return layer1.Value == layer2.Value;
        }

        /// <summary>
        /// Inequality check.
        /// </summary>
        public static bool operator !=(NavLayerMask layer1, NavLayerMask layer2) {
            return layer1.Value != layer2.Value;
        }

        /// <summary>
        /// Convert from uint.
        /// </summary>
        public static implicit operator NavLayerMask(uint value) => new(value);

        /// <summary>
        /// Convert from int.
        /// </summary>
        public static implicit operator NavLayerMask(int value) => new((uint)value);

        /// <summary>
        /// Convert to uint.
        /// </summary>
        public static implicit operator uint(NavLayerMask i) => i.Value;

        /// <summary>
        /// Convert to int.
        /// </summary>
        public static implicit operator int(NavLayerMask i) => (int) i.Value;

        /// <summary>
        /// Apply bitwise OR operator to two NavLayerMasks.
        /// </summary>
        public static NavLayerMask operator |(NavLayerMask lhs, NavLayerMask rhs) {
            return new NavLayerMask(lhs.Value | rhs.Value);
        }

        /// <summary>
        /// Apply bitwise OR operator to a NavLayerMask and an unsigned integer.
        /// </summary>
        public static NavLayerMask operator |(NavLayerMask lhs, uint rhs) {
            return new NavLayerMask(lhs.Value | rhs);
        }

        /// <summary>
        /// Apply bitwise OR operator to an unsigned integer and a NavLayerMask.
        /// </summary>
        public static uint operator |(uint lhs, NavLayerMask rhs) {
            return lhs | rhs.Value;
        }

        /// <summary>
        /// Apply bitwise AND operator to two NavLayerMasks.
        /// </summary>
        public static NavLayerMask operator &(NavLayerMask lhs, NavLayerMask rhs) {
            return new NavLayerMask(lhs.Value & rhs.Value);
        }

        /// <summary>
        /// Apply bitwise AND operator to a NavLayerMask and an unsigned integer.
        /// </summary>
        public static NavLayerMask operator &(NavLayerMask lhs, uint rhs) {
            return new NavLayerMask(lhs.Value & rhs);
        }

        /// <summary>
        /// Apply bitwise AND operator to an unsigned integer and a NavLayerMask.
        /// </summary>
        public static uint operator &(uint lhs, NavLayerMask rhs) {
            return lhs & rhs.Value;
        }

        /// <summary>
        /// Apply bitwise XOR operator to two NavLayerMasks.
        /// </summary>
        public static NavLayerMask operator ^(NavLayerMask lhs, NavLayerMask rhs) {
            return new NavLayerMask(lhs.Value ^ rhs.Value);
        }

        /// <summary>
        /// Apply bitwise XOR operator to a NavLayerMask and an unsigned integer.
        /// </summary>
        public static NavLayerMask operator ^(NavLayerMask lhs, uint rhs) {
            return new NavLayerMask(lhs.Value ^ rhs);
        }

        /// <summary>
        /// Apply bitwise XOR operator to an unsigned integer and a NavLayerMask.
        /// </summary>
        public static uint operator ^(uint lhs, NavLayerMask rhs) {
            return lhs ^ rhs.Value;
        }

        /// <summary>
        /// Invert a NavLayerMask.
        /// </summary>
        public static NavLayerMask operator ~(NavLayerMask lhs) {
            return new NavLayerMask(~lhs.Value);
        }

        public struct Enumerator : IEnumerator<NavLayer> {
            private int _index;
            private readonly uint _mask;

            public Enumerator(uint mask) {
                _mask = mask;
                _index = -1;
            }

            public bool MoveNext() {
                while (_index < 31) {
                    _index++;
                    if ((_mask & (1 << _index)) != 0) return true;
                }

                return false;
            }

            public void Reset() {
                _index = -1;
            }

            public NavLayer Current => new(_index);

            object IEnumerator.Current => Current;

            public void Dispose() { }
        }
    }
}
