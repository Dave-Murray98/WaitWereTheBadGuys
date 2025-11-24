// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav {
    public class NavSurfaceData : ScriptableObject, INavAreaData<NativeNavSurfaceData, NativeNavSurfaceDataPointers> {
        [SerializeField]
        [NonReorderable]
        private Vector3[] _vertices;

        [SerializeField]
        [NonReorderable]
        private NavSurfaceRegionData[] _regions;

        [SerializeField]
        [NonReorderable]
        private int[] _triangleIndices;

        [SerializeField]
        [NonReorderable]
        private NavSurfaceInternalLinkData[] _internalLinks;

        [SerializeField]
        [NonReorderable]
        private int[] _linkVertices;

        [SerializeField]
        [NonReorderable]
        private Edge[] _linkEdges;

        [SerializeField]
        private ulong _version;

        /// <summary>
        /// The vertex positions of all of the surface's regions, in local space.
        /// </summary>
        public IReadOnlyList<Vector3> Vertices => _vertices;

        /// <summary>
        /// The regions of the surface.
        /// </summary>
        public IReadOnlyList<NavSurfaceRegionData> Regions => _regions;

        /// <summary>
        /// The vertex indices of triangles.
        /// </summary>
        public IReadOnlyList<int> TriangleIndices => _triangleIndices;

        /// <summary>
        /// The internal links of all of the surface's regions.
        /// </summary>
        public IReadOnlyList<NavSurfaceInternalLinkData> InternalLinks => _internalLinks;

        /// <summary>
        /// The shared vertices of all of the surface's internal links.
        /// </summary>
        public IReadOnlyList<int> LinkVertices => _linkVertices;

        /// <summary>
        /// The shared edges of all of the surface's internal links.
        /// </summary>
        public IReadOnlyList<Edge> LinkEdges => _linkEdges;

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
        /// <param name="vertices">The vertex positions of all of the surface's regions, in local space.</param>
        /// <param name="regions">The regions of the surface.</param>
        /// <param name="triangleIndices">The vertex indices of triangles.</param>
        /// <param name="internalLinks">The internal links of all of the surface's regions.</param>
        /// <param name="linkVertices">The shared vertices of all of the surface's internal links.</param>
        /// <param name="linkEdges">The shared edges of all of the surface's internal links.</param>
        public void UpdateData(Vector3[] vertices, NavSurfaceRegionData[] regions, int[] triangleIndices,
                               NavSurfaceInternalLinkData[] internalLinks, int[] linkVertices, Edge[] linkEdges) {
            _vertices = vertices;
            _regions = regions;
            _triangleIndices = triangleIndices;
            _internalLinks = internalLinks;
            _linkVertices = linkVertices;
            _linkEdges = linkEdges;
            _version++;
        }

        /// <summary>
        /// Clear the properties of this NavSurfaceData.
        /// </summary>
        public void Clear() {
            _vertices = Array.Empty<Vector3>();
            _regions = Array.Empty<NavSurfaceRegionData>();
            _triangleIndices = Array.Empty<int>();
            _internalLinks = Array.Empty<NavSurfaceInternalLinkData>();
            _linkVertices = Array.Empty<int>();
            _linkEdges = Array.Empty<Edge>();
            _version++;
        }

        /// <summary>
        /// Update this surface data from the given native data.
        /// </summary>
        public void UpdateFromNativeData(in NativeNavSurfaceData data) {
            _vertices = new Vector3[data.Vertices.Length];
            for (int i = 0; i < data.Vertices.Length; i++) {
                _vertices[i] = data.Vertices[i].xyz;
            }

            _regions = new NavSurfaceRegionData[data.Regions.Length];
            for (int i = 0; i < data.Regions.Length; i++) {
                _regions[i] = new NavSurfaceRegionData(data.Regions[i]);
            }

            _triangleIndices = new int[data.TriangleIndices.Length];
            for (int i = 0; i < data.TriangleIndices.Length; i++) {
                _triangleIndices[i] = data.TriangleIndices[i];
            }

            _internalLinks = new NavSurfaceInternalLinkData[data.InternalLinks.Length];
            for (int i = 0; i < data.InternalLinks.Length; i++) {
                _internalLinks[i] = new NavSurfaceInternalLinkData(data.InternalLinks[i]);
            }

            _linkVertices = new int[data.LinkVertices.Length];
            for (int i = 0; i < data.LinkVertices.Length; i++) {
                _linkVertices[i] = data.LinkVertices[i];
            }

            _linkEdges = new Edge[data.LinkEdges.Length];
            for (int i = 0; i < data.LinkEdges.Length; i++) {
                _linkEdges[i] = new Edge(data.LinkEdges[i]);
            }

            _version++;
        }

        /// <summary>
        /// Convert this NavSurfaceData to the native format so that it can be used by jobs.
        /// </summary>
        /// <param name="area">Area that owns the data.</param>
        /// <param name="data">Created native data.</param>
        /// <param name="pointers">Created data structure references (must be kept in order to deallocate).</param>
        public bool ToNativeData(
            INavArea area,
            out NativeNavSurfaceData data,
            out NativeNavSurfaceDataPointers pointers) {
            pointers = default;

            if (_vertices is not { Length: > 0 } ||
                _regions is not { Length: > 0 } ||
                _triangleIndices is not { Length: > 0 }) {
                data = default;
                return false;
            }

            pointers.PositionsData = new NativeArray<float4>(_vertices.Length, Allocator.Persistent);
            for (int i = 0; i < _vertices.Length; i++) {
                pointers.PositionsData[i] = _vertices[i].ToV4Pos();
            }

            pointers.RegionsData = new NativeArray<NativeNavSurfaceRegionData>(_regions.Length, Allocator.Persistent);
            for (int i = 0; i < _regions.Length; i++) {
                if (area.ExternalLinkRanges?.Count > i) {
                    pointers.RegionsData[i] = _regions[i].ToNativeData(area.ExternalLinkRanges[i]);
                } else {
                    pointers.RegionsData[i] = _regions[i].ToNativeData(default);
                }
            }

            pointers.TriIndices = new NativeArray<int>(_triangleIndices, Allocator.Persistent);

            if (_internalLinks is { Length: > 0 }) {
                pointers.InternalLinksData =
                    new NativeArray<NativeNavSurfaceInternalLinkData>(_internalLinks.Length, Allocator.Persistent);
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

            Transform transform = area.Transform;
            Bounds bounds = area.Bounds;
            data = new NativeNavSurfaceData(
                area.InstanceID,
                transform.localToWorldMatrix,
                transform.worldToLocalMatrix,
                new NativeBounds(bounds.center.ToV4Pos(), bounds.extents.ToV4()),
                area.Layer,
                UnsafeArray<float4>.ToPointer(pointers.PositionsData),
                UnsafeArray<NativeNavSurfaceRegionData>.ToPointer(pointers.RegionsData),
                UnsafeArray<int>.ToPointer(pointers.TriIndices),
                UnsafeArray<NativeNavSurfaceInternalLinkData>.ToPointer(pointers.InternalLinksData),
                UnsafeArray<NativeNavExternalLinkData>.ToPointer(pointers.ExternalLinksData),
                UnsafeArray<int>.ToPointer(pointers.LinkVerticesData),
                UnsafeArray<int2>.ToPointer(pointers.LinkEdgesData));
            return true;
        }
    }

    /// <summary>
    /// The serialized data representing a single region in a NavSurface.
    /// </summary>
    [Serializable]
    public class NavSurfaceRegionData {
        [SerializeField]
        private int _id;

        [SerializeField]
        private Bounds _bounds;

        [SerializeField]
        private SerializableRange _triangleIndexRange;

        [SerializeField]
        private SerializableRange _internalLinkRange;

        [SerializeField]
        private Vector3 _upDirection;

        [SerializeField]
        private Vector3 _normalVector;

        [SerializeField]
        private int _islandIndex;

        /// <summary>
        /// The ID of the region.
        /// </summary>
        public int ID => _id;

        /// <summary>
        /// The bounds of the region in local space of the surface.
        /// </summary>
        public Bounds Bounds => _bounds;

        /// <summary>
        ///  Range of the region's triangle indices in the surface's index array.
        /// </summary>
        public SerializableRange TriangleIndexRange => _triangleIndexRange;

        /// <summary>
        /// Range of links between this region and other regions in surface's internal link array.
        /// </summary>
        public SerializableRange InternalLinkRange => _internalLinkRange;

        /// <summary>
        /// The upward direction for the region.
        /// </summary>
        public Vector3 UpDirection => _upDirection;

        /// <summary>
        /// The normal vector for the region, which may be different from the up direction depending on the
        /// upward direction mode. If the normal vector is not set, it defaults to the up direction.
        /// </summary>
        public Vector3 NormalVector => _normalVector != Vector3.zero ? _normalVector : _upDirection;

        /// <summary>
        /// The index of the island this region belongs to.
        /// Surface regions in the same island are connected.
        /// </summary>
        public int IslandIndex => _islandIndex;

        /// <summary>
        /// Default constructor for serialization.
        /// </summary>
        public NavSurfaceRegionData() { }

        /// <summary>
        /// Create a new NavRegionData with the given properties.
        /// </summary>
        /// <param name="id">ID of the region.</param>
        /// <param name="bounds">Bounds of the region.</param>
        /// <param name="triangleIndexRange">Range of the region's triangle indices.</param>
        /// <param name="internalLinkRange">Range of links between this region and other regions.</param>
        /// <param name="upDirection">The upward direction for the region.</param>
        /// <param name="normalVector">The normal vector for the region.</param>
        /// <param name="islandIndex">Index of the island this region belongs to.</param>
        public NavSurfaceRegionData(int id, Bounds bounds, SerializableRange triangleIndexRange,
                                    SerializableRange internalLinkRange, Vector3 upDirection, Vector3 normalVector,
                                    int islandIndex) {
            _id = id;
            _bounds = bounds;
            _triangleIndexRange = triangleIndexRange;
            _internalLinkRange = internalLinkRange;
            _upDirection = upDirection;
            _normalVector = normalVector;
            _islandIndex = islandIndex;
        }

        /// <summary>
        /// Create a new NavRegionData with the given native data.
        /// </summary>
        /// <param name="data">Native data to copy.</param>
        public NavSurfaceRegionData(in NativeNavSurfaceRegionData data) {
            _id = data.ID;
            _bounds = new Bounds(data.Bounds.Center.xyz, data.Bounds.Size.xyz);
            _triangleIndexRange = data.TriangleIndexRange;
            _internalLinkRange = data.InternalLinkRange;
            _upDirection = data.UpVector.xyz;
            _normalVector = data.NormalVector.xyz;
            _islandIndex = data.IslandIndex;
        }

        /// <summary>
        /// Convert to a native representation.
        /// </summary>
        /// <param name="externalLinkRange">External links,
        /// which are included in the native data but not the serialized data.</param>
        /// <returns>Created native data.</returns>
        public NativeNavSurfaceRegionData ToNativeData(SerializableRange externalLinkRange) {
            return new NativeNavSurfaceRegionData(ID, new NativeBounds(Bounds.center.ToV4Pos(), Bounds.extents.ToV4()),
                                                  TriangleIndexRange, InternalLinkRange, externalLinkRange,
                                                  UpDirection.ToV4(), NormalVector.ToV4(), IslandIndex);
        }
    }

    /// <summary>
    /// A connection from one region to another region in the same surface.
    /// </summary>
    [Serializable]
    public class NavSurfaceInternalLinkData {
        [SerializeField]
        private int _connectedRegionID;

        [SerializeField]
        private SerializableRange _vertexRange;

        [SerializeField]
        private SerializableRange _edgeRange;

        /// <summary>
        /// The ID of the connected region.
        /// </summary>
        public int ConnectedRegionID => _connectedRegionID;

        /// <summary>
        /// Range of the link's indices in the surface's <see cref="NavSurfaceData.LinkVertices"/> array.
        /// </summary>
        public SerializableRange VertexRange => _vertexRange;

        /// <summary>
        /// Range of the link's indices in the surface's <see cref="NavSurfaceData.LinkEdges"/> array.
        /// </summary>
        public SerializableRange EdgeRange => _edgeRange;

        /// <summary>
        /// Default constructor for serialization.
        /// </summary>
        public NavSurfaceInternalLinkData() { }

        /// <summary>
        /// Create a new NavSurfaceInternalLinkData with the given properties.
        /// </summary>
        /// <param name="connectedRegionID">ID of the connected region.</param>
        /// <param name="vertexRange">Range of the link's indices.</param>
        /// <param name="edgeRange">Range of the link's indices.</param>
        public NavSurfaceInternalLinkData(int connectedRegionID, SerializableRange vertexRange,
                                          SerializableRange edgeRange) {
            _connectedRegionID = connectedRegionID;
            _vertexRange = vertexRange;
            _edgeRange = edgeRange;
        }

        /// <summary>
        /// Create a new NavSurfaceInternalLinkData with the given native data.
        /// </summary>
        /// <param name="data">Native data to copy.</param>
        public NavSurfaceInternalLinkData(in NativeNavSurfaceInternalLinkData data) {
            _connectedRegionID = data.ToRegion;
            _vertexRange = data.VertexRange;
            _edgeRange = data.EdgeRange;
        }

        /// <summary>
        /// Convert to a native representation.
        /// </summary>
        /// <returns>Created native data.</returns>
        public NativeNavSurfaceInternalLinkData ToNativeData() {
            return new NativeNavSurfaceInternalLinkData(ConnectedRegionID, VertexRange, EdgeRange);
        }
    }

    /// <summary>
    /// References to the NativeArrays allocated for a <see cref="NativeNavSurfaceData"/>.
    /// </summary>
    /// <remarks>
    /// In the NativeNavSurfaceData itself, these arrays are kept as pointers,
    /// which cannot be used to deallocate the arrays under Unity's safe memory system.
    /// In order to play nicely with that system, the original references must be kept and disposed.
    /// </remarks>
    public struct NativeNavSurfaceDataPointers : IDisposable, INativeNavAreaDataPointers {
        public NativeArray<NativeNavSurfaceRegionData> RegionsData;
        public NativeArray<float4> PositionsData;
        public NativeArray<int> TriIndices;
        public NativeArray<NativeNavSurfaceInternalLinkData> InternalLinksData;
        public NativeArray<NativeNavExternalLinkData> ExternalLinksData;
        public NativeArray<int> LinkVerticesData;
        public NativeArray<int2> LinkEdgesData;

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
            InternalLinksData.Dispose();
            ExternalLinksData.Dispose();
            LinkVerticesData.Dispose();
            LinkEdgesData.Dispose();
        }
    }
}
