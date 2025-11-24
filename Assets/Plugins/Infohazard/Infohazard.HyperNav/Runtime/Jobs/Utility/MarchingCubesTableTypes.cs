// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Utility {
    [StructLayout(LayoutKind.Explicit, Size = TriTableEntry.Size * ItemCount)]
    public unsafe struct TriTable {
        public const int ItemCount = 256;

        [FieldOffset(0)] private TriTableEntry _firstEntry;

        public TriTableEntry this[int index] {
            readonly get {
                CheckIndex(index);
                fixed (TriTableEntry* array = &_firstEntry) {
                    return array[index];
                }
            }

            set {
                CheckIndex(index);
                fixed (TriTableEntry* array = &_firstEntry) {
                    array[index] = value;
                }
            }
        }

        public readonly ref readonly TriTableEntry GetRef(int index) {
            CheckIndex(index);
            fixed (TriTableEntry* array = &_firstEntry) {
                return ref array[index];
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(int index) {
            if (index is < 0 or >= ItemCount) {
                throw new IndexOutOfRangeException();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public unsafe struct TriTableEntry {
        public const int Size = 16;

        [FieldOffset(0)] public readonly byte Length;
        [FieldOffset(1)] private fixed byte _elements[15];

        public readonly byte this[int index] {
            get {
                CheckIndex(index);
                return _elements[index];
            }
        }

        public TriTableEntry(byte i0, byte i1, byte i2) {
            Length = 3;
            _elements[0] = i0;
            _elements[1] = i1;
            _elements[2] = i2;
        }

        public TriTableEntry(byte i0, byte i1, byte i2, byte i3, byte i4, byte i5) {
            Length = 6;
            _elements[0] = i0;
            _elements[1] = i1;
            _elements[2] = i2;
            _elements[3] = i3;
            _elements[4] = i4;
            _elements[5] = i5;
        }

        public TriTableEntry(byte i0, byte i1, byte i2, byte i3, byte i4, byte i5, byte i6, byte i7, byte i8) {
            Length = 9;
            _elements[0] = i0;
            _elements[1] = i1;
            _elements[2] = i2;
            _elements[3] = i3;
            _elements[4] = i4;
            _elements[5] = i5;
            _elements[6] = i6;
            _elements[7] = i7;
            _elements[8] = i8;
        }

        public TriTableEntry(byte i0, byte i1, byte i2, byte i3, byte i4, byte i5, byte i6, byte i7, byte i8, byte i9,
                             byte i10, byte i11) {
            Length = 12;
            _elements[0] = i0;
            _elements[1] = i1;
            _elements[2] = i2;
            _elements[3] = i3;
            _elements[4] = i4;
            _elements[5] = i5;
            _elements[6] = i6;
            _elements[7] = i7;
            _elements[8] = i8;
            _elements[9] = i9;
            _elements[10] = i10;
            _elements[11] = i11;
        }

        public TriTableEntry(byte i0, byte i1, byte i2, byte i3, byte i4, byte i5, byte i6, byte i7, byte i8, byte i9,
                             byte i10, byte i11, byte i12, byte i13, byte i14) {
            Length = 15;
            _elements[0] = i0;
            _elements[1] = i1;
            _elements[2] = i2;
            _elements[3] = i3;
            _elements[4] = i4;
            _elements[5] = i5;
            _elements[6] = i6;
            _elements[7] = i7;
            _elements[8] = i8;
            _elements[9] = i9;
            _elements[10] = i10;
            _elements[11] = i11;
            _elements[12] = i12;
            _elements[13] = i13;
            _elements[14] = i14;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private readonly void CheckIndex(int index) {
            if (index < 0 || index >= Length) {
                throw new IndexOutOfRangeException();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = EdgeToVertexIndexTableEntry.Size * ItemCount)]
    public unsafe struct EdgeToVertexIndexTable {
        public const int ItemCount = 12;

        [FieldOffset(0)] private EdgeToVertexIndexTableEntry _firstEntry;

        public EdgeToVertexIndexTableEntry this[int index] {
            readonly get {
                CheckIndex(index);
                fixed (EdgeToVertexIndexTableEntry* array = &_firstEntry) {
                    return array[index];
                }
            }

            set {
                CheckIndex(index);
                fixed (EdgeToVertexIndexTableEntry* array = &_firstEntry) {
                    array[index] = value;
                }
            }
        }

        public readonly ref readonly EdgeToVertexIndexTableEntry GetRef(int index) {
            CheckIndex(index);
            fixed (EdgeToVertexIndexTableEntry* array = &_firstEntry) {
                return ref array[index];
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(int index) {
            if (index is < 0 or >= ItemCount) {
                throw new IndexOutOfRangeException();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public unsafe struct EdgeToVertexIndexTableEntry {
        public const int Size = 2;

        [FieldOffset(0)] public readonly byte Index1;
        [FieldOffset(1)] public readonly byte Index2;

        public EdgeToVertexIndexTableEntry(byte index1, byte index2) {
            Index1 = index1;
            Index2 = index2;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = VertexIndexToPositionTableEntry.Size * ItemCount)]
    public unsafe struct VertexIndexToPositionTable {
        public const int ItemCount = 8;

        [FieldOffset(0)] private VertexIndexToPositionTableEntry _firstEntry;

        public VertexIndexToPositionTableEntry this[int index] {
            readonly get {
                CheckIndex(index);
                fixed (VertexIndexToPositionTableEntry* array = &_firstEntry) {
                    return array[index];
                }
            }

            set {
                CheckIndex(index);
                fixed (VertexIndexToPositionTableEntry* array = &_firstEntry) {
                    array[index] = value;
                }
            }
        }

        public readonly ref readonly VertexIndexToPositionTableEntry GetRef(int index) {
            CheckIndex(index);
            fixed (VertexIndexToPositionTableEntry* array = &_firstEntry) {
                return ref array[index];
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(int index) {
            if (index is < 0 or >= ItemCount) {
                throw new IndexOutOfRangeException();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public struct VertexIndexToPositionTableEntry {
        public const int Size = 3 * sizeof(int);

        [FieldOffset(0 * sizeof(int))] public readonly int x;
        [FieldOffset(1 * sizeof(int))] public readonly int y;
        [FieldOffset(2 * sizeof(int))] public readonly int z;

        public VertexIndexToPositionTableEntry(int x, int y, int z) {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static implicit operator int4(VertexIndexToPositionTableEntry entry) {
            return new int4(entry.x, entry.y, entry.z, 1);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = ItemCount)]
    public unsafe struct AcrossCenterEdges {
        public const int ItemCount = 12;

        [FieldOffset(0)] private byte _firstEntry;

        public byte this[int index] {
            readonly get {
                CheckIndex(index);
                fixed (byte* array = &_firstEntry) {
                    return array[index];
                }
            }

            set {
                CheckIndex(index);
                fixed (byte* array = &_firstEntry) {
                    array[index] = value;
                }
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(int index) {
            if (index is < 0 or >= ItemCount) {
                throw new IndexOutOfRangeException();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = ItemCount * DirectionToVerticesOnSideTableEntry.Count)]
    public unsafe struct DirectionToVerticesOnSideTable {
        public const int ItemCount = 3;

        [FieldOffset(0)] private DirectionToVerticesOnSideTableEntry _firstEntry;

        public DirectionToVerticesOnSideTableEntry this[int index] {
            readonly get {
                CheckIndex(index);
                fixed (DirectionToVerticesOnSideTableEntry* array = &_firstEntry) {
                    return array[index];
                }
            }

            set {
                CheckIndex(index);
                fixed (DirectionToVerticesOnSideTableEntry* array = &_firstEntry) {
                    array[index] = value;
                }
            }
        }

        public readonly ref readonly DirectionToVerticesOnSideTableEntry GetRef(int index) {
            CheckIndex(index);
            fixed (DirectionToVerticesOnSideTableEntry* array = &_firstEntry) {
                return ref array[index];
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(int index) {
            if (index is < 0 or >= ItemCount) {
                throw new IndexOutOfRangeException();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Count)]
    public unsafe struct DirectionToVerticesOnSideTableEntry {
        public const int Count = 4;

        [FieldOffset(0)] private fixed byte _entries[Count];

        public readonly byte this[int index] {
            get {
                CheckIndex(index);
                return _entries[index];
            }
        }

        public DirectionToVerticesOnSideTableEntry(byte i0, byte i1, byte i2, byte i3) {
            _entries[0] = i0;
            _entries[1] = i1;
            _entries[2] = i2;
            _entries[3] = i3;
        }

        public readonly bool CubeHasOnVoxelsThisSide(byte cube) {
            for (int i = 0; i < Count; i++) {
                if (CubeHasThisVoxelOn(cube, _entries[i])) return true;
            }

            return false;
        }

        private static bool CubeHasThisVoxelOn(byte cube, byte voxel) {
            return (cube & (1 << voxel)) != 0;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(int index) {
            if (index is < 0 or >= Count) {
                throw new IndexOutOfRangeException();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = ItemCount * sizeof(bool))]
    public unsafe struct CubesWithInternalCavityTable {
        public const int ItemCount = 256;

        [FieldOffset(0)] private bool _firstEntry;

        public bool this[int index] {
            readonly get {
                CheckIndex(index);
                fixed (bool* array = &_firstEntry) {
                    return array[index];
                }
            }
            set {
                CheckIndex(index);
                fixed (bool* array = &_firstEntry) {
                    array[index] = value;
                }
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(int index) {
            if (index is < 0 or >= ItemCount) {
                throw new IndexOutOfRangeException();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = ItemCount * CubeConcaveNeighborsTableEntry.Size)]
    public unsafe struct CubeConcaveNeighborsTable {
        public const int ItemCount = 256;

        [FieldOffset(0)] private CubeConcaveNeighborsTableEntry _firstEntry;

        public CubeConcaveNeighborsTableEntry this[int index] {
            readonly get {
                CheckIndex(index);
                fixed (CubeConcaveNeighborsTableEntry* array = &_firstEntry) {
                    return array[index];
                }
            }

            set {
                CheckIndex(index);
                fixed (CubeConcaveNeighborsTableEntry* array = &_firstEntry) {
                    array[index] = value;
                }
            }
        }

        public readonly ref readonly CubeConcaveNeighborsTableEntry GetRef(int index) {
            CheckIndex(index);
            fixed (CubeConcaveNeighborsTableEntry* array = &_firstEntry) {
                return ref array[index];
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(int index) {
            if (index is < 0 or >= ItemCount) {
                throw new IndexOutOfRangeException();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public unsafe struct CubeConcaveNeighborsTableEntry {
        public const int ItemCount = 3;
        public const int Size = ItemCount * CubeConcaveNeighborsTableDirectionEntry.Size;

        [FieldOffset(0)] private CubeConcaveNeighborsTableDirectionEntry _firstEntry;

        public CubeConcaveNeighborsTableDirectionEntry this[int index] {
            readonly get {
                CheckIndex(index);
                fixed (CubeConcaveNeighborsTableDirectionEntry* array = &_firstEntry) {
                    return array[index];
                }
            }

            set {
                CheckIndex(index);
                fixed (CubeConcaveNeighborsTableDirectionEntry* array = &_firstEntry) {
                    array[index] = value;
                }
            }
        }

        public readonly ref readonly CubeConcaveNeighborsTableDirectionEntry GetRef(int index) {
            CheckIndex(index);
            fixed (CubeConcaveNeighborsTableDirectionEntry* array = &_firstEntry) {
                return ref array[index];
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(int index) {
            if (index is < 0 or >= ItemCount) {
                throw new IndexOutOfRangeException();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public unsafe struct CubeConcaveNeighborsTableDirectionEntry : IEnumerable<int> {
        public const int MaxItemCount = 16;
        public const int Size = sizeof(byte) + MaxItemCount * sizeof(byte);

        public readonly byte Count => _count;

        [FieldOffset(0)] private byte _count;
        [FieldOffset(sizeof(byte))] private byte _firstEntry;

        public readonly byte this[int index] {
            get {
                CheckIndex(index);
                fixed (byte* array = &_firstEntry) {
                    return array[index];
                }
            }
        }

        public void Add(byte element) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_count >= MaxItemCount) {
                throw new InvalidOperationException("CubeConcaveNeighborsTableDirectionEntry is full.");
            }
#endif

            fixed (byte* array = &_firstEntry) {
                array[_count++] = element;
            }
        }

        public readonly bool Contains(byte element) {
            for (int i = 0; i < _count; i++) {
                if (this[i] == element) return true;
            }

            return false;
        }

        // Only implemented to support collection initializer syntax.
        readonly IEnumerator<int> IEnumerable<int>.GetEnumerator() {
            throw new System.NotImplementedException();
        }

        // Only implemented to support collection initializer syntax.
        readonly IEnumerator IEnumerable.GetEnumerator() {
            throw new System.NotImplementedException();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private readonly void CheckIndex(int index) {
            if (index < 0 || index >= Count) {
                throw new IndexOutOfRangeException();
            }
        }
    }
}
