// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav.Jobs.Utility {
    /// <summary>
    /// Serves as a Dictionary of ints with a max size of 2, to avoid allocations.
    /// This works because a triangle can be part of at most two regions.
    /// </summary>
    public unsafe struct TriangleRegionIndices {
        public int Count { get; private set; }

        public const int MaxCount = 2;

        private fixed int _data[MaxCount * 2];

        public bool TryGetValue(int region, out int index) {
            for (int i = 0; i < Count; i++) {
                if (_data[i * 2] != region) continue;

                index = _data[i * 2 + 1];
                return true;
            }

            index = -1;
            return false;
        }

        public void SetValue(int region, int index) {
            for (int i = 0; i < Count; i++) {
                if (_data[i * 2] != region) continue;

                _data[i * 2 + 1] = index;
                return;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS

            if (Count >= MaxCount) {
                throw new InvalidOperationException("TriangleRegionIndices is full.");
            }

#endif

            _data[Count * 2] = region;
            _data[Count * 2 + 1] = index;
            Count++;
        }
    }

    public struct BakingNavSurfaceMeshInfo : IDisposable {
        /// <summary>
        /// All vertices of the mesh.
        /// </summary>
        public NativeList<float4> Vertices;

        /// <summary>
        /// For each vertex index, which other vertex indices it is connected to via edges.
        /// </summary>
        public NativeList<HybridIntList> VertexConnections;

        /// <summary>
        /// For each vertex index, the first index of each triangle that uses that vertex.
        /// </summary>
        public NativeList<HybridIntList> VertexTriangles;

        /// <summary>
        /// The indices of all the triangle vertices in the mesh.
        /// </summary>
        public NativeList<int> TriangleList;

        /// <summary>
        /// For each triangle (three elements in TriangleList), the normal of the triangle.
        /// </summary>
        public NativeList<float4> TriangleNormals;

        /// <summary>
        /// For each triangle (three elements in TriangleList), the upright direction of the triangle.
        /// </summary>
        public NativeList<float4> TriangleUprightWorldDirections;
        
        public NativeList<int> TriangleGroupIDs;
        
        public NativeList<HybridIntList> GroupTriangles;

        public BakingNavSurfaceMeshInfo(Allocator allocator) {
            Vertices = new NativeList<float4>(256, allocator);
            VertexConnections = new NativeList<HybridIntList>(256, allocator);
            VertexTriangles = new NativeList<HybridIntList>(256, allocator);
            TriangleList = new NativeList<int>(256, allocator);
            TriangleNormals = new NativeList<float4>(256, allocator);
            TriangleUprightWorldDirections = new NativeList<float4>(256, Allocator.Persistent);
            TriangleGroupIDs = new NativeList<int>(256, Allocator.Persistent);
            GroupTriangles = new NativeList<HybridIntList>(256, allocator);
        }

        public void Dispose() {
            foreach (HybridIntList connList in VertexConnections) {
                connList.Dispose();
            }
            
            foreach (HybridIntList triList in VertexTriangles) {
                triList.Dispose();
            }
            
            foreach (HybridIntList groupList in GroupTriangles) {
                groupList.Dispose();
            }
            
            Vertices.Dispose();
            VertexConnections.Dispose();
            VertexTriangles.Dispose();
            TriangleList.Dispose();
            TriangleNormals.Dispose();
            TriangleUprightWorldDirections.Dispose();
            TriangleGroupIDs.Dispose();
            GroupTriangles.Dispose();
        }
    }

    public struct BakingNavVolumeMeshInfo : IDisposable {
        /// <summary>
        /// All vertices of the mesh.
        /// </summary>
        public NativeList<float4> Vertices;

        /// <summary>
        /// For each vertex index, which other vertex indices it is connected to via edges.
        /// </summary>
        public NativeList<HybridIntList> VertexConnections;

        /// <summary>
        /// For each vertex index, which regions it is a part of.
        /// </summary>
        public NativeList<IntList2> VertexRegionMembership;

        /// <summary>
        /// For each region, the indices of all the triangle vertices in that region.
        /// </summary>
        public NativeArray<UnsafeList<int>> RegionTriangleLists;

        /// <summary>
        /// For each triangle, for each region, what index that triangle's vertices start in that region.
        /// </summary>
        public NativeParallelHashMap<Triangle, TriangleRegionIndices> TriangleIndicesPerRegion;

        public BakingNavVolumeMeshInfo(int regionCount, Allocator allocator) {
            Vertices = new NativeList<float4>(256, allocator);
            VertexConnections = new NativeList<HybridIntList>(256, allocator);
            VertexRegionMembership = new NativeList<IntList2>(256, allocator);
            RegionTriangleLists = new NativeArray<UnsafeList<int>>(regionCount, allocator);
            TriangleIndicesPerRegion =
                new NativeParallelHashMap<Triangle, TriangleRegionIndices>(256, allocator);
        }

        public void Dispose() {
            for (int i = 0; i < RegionTriangleLists.Length; i++) {
                if (RegionTriangleLists[i].IsCreated)
                    RegionTriangleLists[i].Dispose();
            }

            for (int i = 0; i < VertexConnections.Length; i++) {
                VertexConnections[i].Dispose();
            }

            Vertices.Dispose();
            VertexConnections.Dispose();
            VertexRegionMembership.Dispose();
            RegionTriangleLists.Dispose();
            TriangleIndicesPerRegion.Dispose();
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = sizeof(int) + sizeof(int) * MaxCount)]
    public unsafe struct RegionMergeCandidateList {
        public const int MaxCount = 4;

        [FieldOffset(0)] private int _count;
        [FieldOffset(sizeof(int))] private int _firstElement;

        public int Count => _count;

        public ref int this[int index] {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS

                if (index < 0 || index >= _count) {
                    throw new IndexOutOfRangeException();
                }

#endif

                fixed (int* array = &_firstElement) {
                    return ref array[index];
                }
            }
        }

        public void Add(int value) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS

            if (_count >= MaxCount) {
                throw new InvalidOperationException("RegionMergeCandidateList is full.");
            }

#endif

            this[_count++] = value;
        }
    }
}
