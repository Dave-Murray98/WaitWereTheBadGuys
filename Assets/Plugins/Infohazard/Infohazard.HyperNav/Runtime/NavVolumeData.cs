// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using System.Linq;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs;
using Infohazard.HyperNav.Jobs.Utility;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav {
    /// <summary>
    /// The baked data of a <see cref="NavVolume"/>, saved as an asset.
    /// </summary>
    public class NavVolumeData : ScriptableObject, INavAreaData<NativeNavVolumeData, NativeNavVolumeDataPointers> {
        [SerializeField]
        [NonReorderable]
        private Vector3[] _vertices;

        [SerializeField]
        [NonReorderable]
        private NavRegionData[] _regions;

        [SerializeField]
        [NonReorderable]
        private int[] _triangleIndices;

        [SerializeField]
        private int _blockingTriangleIndexCount;

        [SerializeField]
        [NonReorderable]
        private NativePlane[] _boundPlanes;

        [SerializeField]
        [NonReorderable]
        private NavInternalLinkData[] _internalLinks;

        [SerializeField]
        [NonReorderable]
        private int[] _linkVertices;

        [SerializeField]
        [NonReorderable]
        private Edge[] _linkEdges;

        [SerializeField]
        [NonReorderable]
        private Triangle[] _linkTriangles;

        [SerializeField]
        private ulong _version;

        /// <summary>
        /// The vertex positions of all of the volume's regions, in local space.
        /// </summary>
        public IReadOnlyList<Vector3> Vertices => _vertices;

        /// <summary>
        /// The regions of the volume.
        /// </summary>
        public IReadOnlyList<NavRegionData> Regions => _regions;

        /// <summary>
        /// The vertex indices of triangles.
        /// </summary>
        public IReadOnlyList<int> TriangleIndices => _triangleIndices;

        /// <summary>
        /// The number of indices in the <see cref="TriangleIndices"/> array that represent blocking triangles.
        /// </summary>
        public int BlockingTriangleIndexCount => _blockingTriangleIndexCount;

        /// <summary>
        /// The bound planes of all of the volume's regions.
        /// </summary>
        public IReadOnlyList<NativePlane> BoundPlanes => _boundPlanes;

        /// <summary>
        /// The internal links of all of the volume's regions.
        /// </summary>
        public IReadOnlyList<NavInternalLinkData> InternalLinks => _internalLinks;

        /// <summary>
        /// The shared vertices of all of the volume's internal links.
        /// </summary>
        public IReadOnlyList<int> LinkVertices => _linkVertices;

        /// <summary>
        /// The shared edges of all of the volume's internal links.
        /// </summary>
        public IReadOnlyList<Edge> LinkEdges => _linkEdges;

        /// <summary>
        /// The shared triangles of all of the volume's internal links.
        /// </summary>
        public IReadOnlyList<Triangle> LinkTriangles => _linkTriangles;

        /// <summary>
        /// The version of the data, which increments when it is baked.
        /// </summary>
        public ulong Version => _version;

        public bool IsBaked => this is {
            _vertices: { Length: > 0 },
            _regions: { Length: > 0 },
            _triangleIndices: { Length: > 0 },
        };

        public int RegionCount => _regions.Length;

        /// <summary>
        /// Update the data manually.
        /// </summary>
        /// <param name="vertices">The vertex positions of all of the volume's regions, in local space.</param>
        /// <param name="regions">The regions of the volume.</param>
        /// <param name="triangleIndices">The vertex indices of triangles.</param>
        /// <param name="blockingTriangleIndexCount">The number of indices that represent blocking triangles.</param>
        /// <param name="internalLinks">The internal links of all of the volume's regions.</param>
        /// <param name="boundPlanes">The bound planes of all of the volume's regions.</param>
        /// <param name="linkVertices">The shared vertices of all of the volume's internal links.</param>
        /// <param name="linkEdges">The shared edges of all of the volume's internal links.</param>
        /// <param name="linkTriangles">The shared triangles of all of the volume's internal links.</param>
        public void UpdateData(Vector3[] vertices, NavRegionData[] regions, int[] triangleIndices,
                               int blockingTriangleIndexCount, NavInternalLinkData[] internalLinks,
                               NativePlane[] boundPlanes, int[] linkVertices, Edge[] linkEdges,
                               Triangle[] linkTriangles) {
            _vertices = vertices;
            _regions = regions;
            _triangleIndices = triangleIndices;
            _blockingTriangleIndexCount = blockingTriangleIndexCount;
            _internalLinks = internalLinks;
            _boundPlanes = boundPlanes;
            _linkVertices = linkVertices;
            _linkEdges = linkEdges;
            _linkTriangles = linkTriangles;
            _version++;
        }

        /// <summary>
        /// Clear the properties of this NavVolumeData.
        /// </summary>
        public void Clear() {
            _vertices = Array.Empty<Vector3>();
            _regions = Array.Empty<NavRegionData>();
            _triangleIndices = Array.Empty<int>();
            _blockingTriangleIndexCount = 0;
            _boundPlanes = Array.Empty<NativePlane>();
            _internalLinks = Array.Empty<NavInternalLinkData>();
            _linkVertices = Array.Empty<int>();
            _linkEdges = Array.Empty<Edge>();
            _linkTriangles = Array.Empty<Triangle>();
            _version++;
        }

        /// <summary>
        /// Update this volume data from the given native data.
        /// </summary>
        public void UpdateFromNativeData(in NativeNavVolumeData data) {
            _vertices = new Vector3[data.Vertices.Length];
            for (int i = 0; i < data.Vertices.Length; i++) {
                _vertices[i] = data.Vertices[i].xyz;
            }

            _regions = new NavRegionData[data.Regions.Length];
            for (int i = 0; i < data.Regions.Length; i++) {
                _regions[i] = new NavRegionData(data.Regions[i]);
            }

            _triangleIndices = new int[data.TriangleIndices.Length];
            for (int i = 0; i < data.TriangleIndices.Length; i++) {
                _triangleIndices[i] = data.TriangleIndices[i];
            }

            _blockingTriangleIndexCount = data.BlockingTriangleIndexCount;

            _boundPlanes = new NativePlane[data.BoundPlanes.Length];
            for (int i = 0; i < data.BoundPlanes.Length; i++) {
                _boundPlanes[i] = data.BoundPlanes[i];
            }

            _internalLinks = new NavInternalLinkData[data.InternalLinks.Length];
            for (int i = 0; i < data.InternalLinks.Length; i++) {
                _internalLinks[i] = new NavInternalLinkData(data.InternalLinks[i]);
            }

            _linkVertices = new int[data.LinkVertices.Length];
            for (int i = 0; i < data.LinkVertices.Length; i++) {
                _linkVertices[i] = data.LinkVertices[i];
            }

            _linkEdges = new Edge[data.LinkEdges.Length];
            for (int i = 0; i < data.LinkEdges.Length; i++) {
                _linkEdges[i] = new Edge(data.LinkEdges[i]);
            }

            _linkTriangles = new Triangle[data.LinkTriangles.Length];
            for (int i = 0; i < data.LinkTriangles.Length; i++) {
                _linkTriangles[i] = new Triangle(data.LinkTriangles[i]);
            }

            _version++;
        }

        /// <summary>
        /// Convert this NavVolumeData to the native format so that it can be used by jobs.
        /// </summary>
        /// <param name="area">Area that owns the data.</param>
        /// <param name="data">Created native data.</param>
        /// <param name="pointers">Created data structure references (must be kept in order to deallocate).</param>
        public bool ToNativeData(
            INavArea area,
            out NativeNavVolumeData data,
            out NativeNavVolumeDataPointers pointers) {
            pointers = default;

            if (_vertices is not { Length: > 0 } ||
                _regions is not { Length: > 0 } ||
                _triangleIndices is not { Length: > 0 } ||
                _boundPlanes is not { Length: > 0 }) {
                data = default;
                return false;
            }

            pointers.PositionsData = new NativeArray<float4>(_vertices.Length, Allocator.Persistent);
            for (int i = 0; i < _vertices.Length; i++) {
                pointers.PositionsData[i] = _vertices[i].ToV4Pos();
            }

            pointers.RegionsData = new NativeArray<NativeNavVolumeRegionData>(_regions.Length, Allocator.Persistent);
            for (int i = 0; i < _regions.Length; i++) {
                if (area.ExternalLinkRanges?.Count > i) {
                    pointers.RegionsData[i] = _regions[i].ToNativeData(area.ExternalLinkRanges[i]);
                } else {
                    pointers.RegionsData[i] = _regions[i].ToNativeData(default);
                }
            }

            pointers.TriIndices = new NativeArray<int>(_triangleIndices, Allocator.Persistent);
            pointers.BoundPlanes = new NativeArray<NativePlane>(_boundPlanes.Length, Allocator.Persistent);
            for (int i = 0; i < _boundPlanes.Length; i++) {
                pointers.BoundPlanes[i] = _boundPlanes[i];
            }

            if (_internalLinks is { Length: > 0 }) {
                pointers.InternalLinksData =
                    new NativeArray<NativeNavVolumeInternalLinkData>(_internalLinks.Length, Allocator.Persistent);
                for (int i = 0; i < _internalLinks.Length; i++) {
                    pointers.InternalLinksData[i] = _internalLinks[i].ToNativeData();
                }
            }

            if (area.ExternalLinks is { Count: > 0 }) {
                pointers.ExternalLinksData =
                    new NativeArray<NativeNavExternalLinkData>(area.ExternalLinks.Count, Allocator.Persistent);
                for (int i = 0; i < area.ExternalLinks.Count; i++) {
                    pointers.ExternalLinksData[i] = area.ExternalLinks[i].ToNativeData();
                }
            }

            if (_linkVertices is { Length: > 0 }) {
                pointers.LinkVerticesData = new NativeArray<int>(_linkVertices, Allocator.Persistent);
            }

            if (_linkEdges is { Length: > 0 }) {
                pointers.LinkEdgesData = new NativeArray<int2>(_linkEdges.Length, Allocator.Persistent);
                for (int i = 0; i < _linkEdges.Length; i++) {
                    pointers.LinkEdgesData[i] = _linkEdges[i].ToInt2();
                }
            }

            if (_linkTriangles is { Length: > 0 }) {
                pointers.LinkTrianglesData = new NativeArray<int3>(_linkTriangles.Length, Allocator.Persistent);
                for (int i = 0; i < _linkTriangles.Length; i++) {
                    pointers.LinkTrianglesData[i] = _linkTriangles[i].ToInt3();
                }
            }

            Transform transform = area.Transform;
            Bounds bounds = area.Bounds;
            data = new NativeNavVolumeData(
                area.InstanceID,
                transform.localToWorldMatrix,
                transform.worldToLocalMatrix,
                new NativeBounds(bounds.center.ToV4Pos(), bounds.extents.ToV4()),
                area.Layer,
                UnsafeArray<float4>.ToPointer(pointers.PositionsData),
                UnsafeArray<NativeNavVolumeRegionData>.ToPointer(pointers.RegionsData),
                UnsafeArray<int>.ToPointer(pointers.TriIndices),
                _blockingTriangleIndexCount,
                UnsafeArray<NativePlane>.ToPointer(pointers.BoundPlanes),
                UnsafeArray<NativeNavVolumeInternalLinkData>.ToPointer(pointers.InternalLinksData),
                UnsafeArray<NativeNavExternalLinkData>.ToPointer(pointers.ExternalLinksData),
                UnsafeArray<int>.ToPointer(pointers.LinkVerticesData),
                UnsafeArray<int2>.ToPointer(pointers.LinkEdgesData),
                UnsafeArray<int3>.ToPointer(pointers.LinkTrianglesData));
            return true;
        }
    }

    /// <summary>
    /// The serialized data representing a single region in a NavVolume.
    /// </summary>
    [Serializable]
    public class NavRegionData {
        [SerializeField]
        private int _id;

        [SerializeField]
        private Bounds _bounds;

        [SerializeField]
        private SerializableRange _triangleIndexRange;

        [SerializeField]
        private SerializableRange _boundPlaneRange;

        [SerializeField]
        private SerializableRange _internalLinkRange;

        /// <summary>
        /// The ID of the region.
        /// </summary>
        public int ID => _id;

        /// <summary>
        /// The bounds of the region in local space of the volume.
        /// </summary>
        public Bounds Bounds => _bounds;

        /// <summary>
        ///  Range of the region's triangle indices in the volume's index array.
        /// </summary>
        public SerializableRange TriangleIndexRange => _triangleIndexRange;

        /// <summary>
        /// Range of bound planes of this region in the volume's bound planes array.
        /// </summary>
        public SerializableRange BoundPlaneRange => _boundPlaneRange;

        /// <summary>
        /// Range of links between this region and other regions in volume's internal link array.
        /// </summary>
        public SerializableRange InternalLinkRange => _internalLinkRange;

        /// <summary>
        /// Default constructor for serialization.
        /// </summary>
        public NavRegionData() { }

        /// <summary>
        /// Create a new NavRegionData with the given properties.
        /// </summary>
        /// <param name="id">ID of the region.</param>
        /// <param name="bounds">Bounds of the region.</param>
        /// <param name="triangleIndexRange">Range of the region's triangle indices.</param>
        /// <param name="boundPlaneRange">Range of bound planes of this region.</param>
        /// <param name="internalLinkRange">Range of links between this region and other regions.</param>
        public NavRegionData(int id, Bounds bounds, SerializableRange triangleIndexRange,
                             SerializableRange boundPlaneRange, SerializableRange internalLinkRange) {
            _id = id;
            _bounds = bounds;
            _triangleIndexRange = triangleIndexRange;
            _internalLinkRange = internalLinkRange;
            _boundPlaneRange = boundPlaneRange;
        }

        /// <summary>
        /// Create a new NavRegionData with the given native data.
        /// </summary>
        /// <param name="data">Native data to copy.</param>
        public NavRegionData(in NativeNavVolumeRegionData data) {
            _id = data.ID;
            _bounds = new Bounds(data.Bounds.Center.xyz, data.Bounds.Size.xyz);
            _triangleIndexRange = data.TriangleIndexRange;
            _boundPlaneRange = data.BoundPlaneRange;
            _internalLinkRange = data.InternalLinkRange;
        }

        /// <summary>
        /// Convert to a native representation.
        /// </summary>
        /// <param name="externalLinkRange">External links, which are included in the native data but not the serialized data.</param>
        /// <returns>Created native data.</returns>
        public NativeNavVolumeRegionData ToNativeData(SerializableRange externalLinkRange) {
            return new NativeNavVolumeRegionData(ID, new NativeBounds(Bounds.center.ToV4Pos(), Bounds.extents.ToV4()),
                                                 TriangleIndexRange, BoundPlaneRange, InternalLinkRange,
                                                 externalLinkRange);
        }
    }

    /// <summary>
    /// A connection from one region to another region in the same volume.
    /// </summary>
    [Serializable]
    public class NavInternalLinkData {
        [SerializeField]
        private int _connectedRegionID;

        [SerializeField]
        private SerializableRange _vertexRange;

        [SerializeField]
        private SerializableRange _edgeRange;

        [SerializeField]
        private SerializableRange _triangleRange;

        /// <summary>
        /// The ID of the connected region.
        /// </summary>
        public int ConnectedRegionID => _connectedRegionID;

        /// <summary>
        /// Range of the link's indices in the volume's <see cref="NavVolumeData.LinkVertices"/> array.
        /// </summary>
        public SerializableRange VertexRange => _vertexRange;

        /// <summary>
        /// Range of the link's indices in the volume's <see cref="NavVolumeData.LinkEdges"/> array.
        /// </summary>
        public SerializableRange EdgeRange => _edgeRange;

        /// <summary>
        /// Range of the link's indices in the volume's <see cref="NavVolumeData.LinkTriangles"/> array.
        /// </summary>
        public SerializableRange TriangleRange => _triangleRange;

        /// <summary>
        /// Default constructor for serialization.
        /// </summary>
        public NavInternalLinkData() { }

        /// <summary>
        /// Create a new NavInternalLinkData with the given properties.
        /// </summary>
        /// <param name="connectedRegionID">ID of the connected region.</param>
        /// <param name="vertexRange">Range of the link's indices.</param>
        /// <param name="edgeRange">Range of the link's indices.</param>
        /// <param name="triangleRange">Range of the link's indices.</param>
        public NavInternalLinkData(int connectedRegionID, SerializableRange vertexRange,
                                   SerializableRange edgeRange, SerializableRange triangleRange) {
            _connectedRegionID = connectedRegionID;
            _vertexRange = vertexRange;
            _edgeRange = edgeRange;
            _triangleRange = triangleRange;
        }

        /// <summary>
        /// Create a new NavInternalLinkData with the given native data.
        /// </summary>
        /// <param name="data">Native data to copy.</param>
        public NavInternalLinkData(in NativeNavVolumeInternalLinkData data) {
            _connectedRegionID = data.ToRegion;
            _vertexRange = data.VertexRange;
            _edgeRange = data.EdgeRange;
            _triangleRange = data.TriangleRange;
        }

        /// <summary>
        /// Convert to a native representation.
        /// </summary>
        /// <returns>Created native data.</returns>
        public NativeNavVolumeInternalLinkData ToNativeData() {
            return new NativeNavVolumeInternalLinkData(ConnectedRegionID, VertexRange, EdgeRange, TriangleRange);
        }
    }

    /// <summary>
    /// References to the NativeArrays allocated for a <see cref="NativeNavVolumeData"/>.
    /// </summary>
    /// <remarks>
    /// In the NativeNavVolumeData itself, these arrays are kept as pointers,
    /// which cannot be used to deallocate the arrays under Unity's safe memory system.
    /// In order to play nicely with that system, the original references must be kept and disposed.
    /// </remarks>
    public struct NativeNavVolumeDataPointers : IDisposable, INativeNavAreaDataPointers {
        public NativeArray<NativeNavVolumeRegionData> RegionsData;
        public NativeArray<float4> PositionsData;
        public NativeArray<int> TriIndices;
        public NativeArray<NativePlane> BoundPlanes;
        public NativeArray<NativeNavVolumeInternalLinkData> InternalLinksData;
        public NativeArray<NativeNavExternalLinkData> ExternalLinksData;
        public NativeArray<int> LinkVerticesData;
        public NativeArray<int2> LinkEdgesData;
        public NativeArray<int3> LinkTrianglesData;

        NativeArray<NativeNavExternalLinkData> INativeNavAreaDataPointers.ExternalLinksData {
            get => ExternalLinksData;
            set => ExternalLinksData = value;
        }

        /// <summary>
        /// Dispose all of the native array references.
        /// </summary>
        public void Dispose() {
            RegionsData.Dispose();
            PositionsData.Dispose();
            TriIndices.Dispose();
            BoundPlanes.Dispose();
            InternalLinksData.Dispose();
            ExternalLinksData.Dispose();
            LinkVerticesData.Dispose();
            LinkEdgesData.Dispose();
            LinkTrianglesData.Dispose();
        }
    }
}
