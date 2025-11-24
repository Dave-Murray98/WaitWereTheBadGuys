// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Utility {
    public struct NativeNavSurfaceData : IDisposable, INativeNavAreaData {
        /// <summary>
        /// ID of the surface.
        /// </summary>
        public readonly long ID;

        /// <summary>
        /// Transform matrix of the surface.
        /// </summary>
        public readonly float4x4 Transform;

        float4x4 INativeNavAreaData.Transform => Transform;

        /// <summary>
        /// Inverse transform matrix of the surface.
        /// </summary>
        public readonly float4x4 InverseTransform;

        /// <summary>
        /// Bounds of the surface in local space.
        /// </summary>
        public readonly NativeBounds Bounds;

        /// <summary>
        /// The layer this area exists on.
        /// </summary>
        public readonly NavLayer Layer;

        /// <summary>
        /// The vertex positions of all of the surface's regions, in local space.
        /// </summary>
        public readonly UnsafeArray<float4> Vertices;

        /// <summary>
        /// The regions of the volume.
        /// </summary>
        public readonly UnsafeArray<NativeNavSurfaceRegionData> Regions;

        /// <summary>
        /// The vertex indices of triangles.
        /// </summary>
        public readonly UnsafeArray<int> TriangleIndices;

        /// <summary>
        /// The internal links of all of the volume's regions.
        /// </summary>
        public readonly UnsafeArray<NativeNavSurfaceInternalLinkData> InternalLinks;

        /// <summary>
        /// The external links of all of the volume's regions.
        /// </summary>
        public readonly UnsafeArray<NativeNavExternalLinkData> ExternalLinks;

        /// <summary>
        /// The shared vertices of all of the volume's internal links.
        /// </summary>
        public readonly UnsafeArray<int> LinkVertices;

        /// <summary>
        /// The shared edges of all of the volume's internal links.
        /// </summary>
        public readonly UnsafeArray<int2> LinkEdges;

        public bool IsCreated => !(Vertices.IsNull || Regions.IsNull || TriangleIndices.IsNull);

        public int InternalLinkCount => InternalLinks.Length;

        public int ExternalLinkCount => ExternalLinks.Length;

        public int RegionCount => Regions.Length;

        int INativeNavAreaData.Layer => Layer;

        public NativeNavExternalLinkData GetExternalLinkData(int index) {
            return ExternalLinks[index];
        }

        public SerializableRange GetExternalLinkRange(int index) {
            return Regions[index].ExternalLinkRange;
        }

        public NativeBounds GetRegionBounds(int index) {
            return Regions[index].Bounds;
        }

        public void UpdateExternalLinksInPlace(UnsafeArray<NativeNavExternalLinkData> links,
                                               ReadOnlySpan<SerializableRange> ranges) {

            ExternalLinks.Dispose();
            unsafe {
                fixed (NativeNavSurfaceData* thisPtr = &this) {
                    *thisPtr = new NativeNavSurfaceData(
                        ID,
                        Transform,
                        InverseTransform,
                        Bounds,
                        Layer,
                        Vertices,
                        Regions,
                        TriangleIndices,
                        InternalLinks,
                        links,
                        LinkVertices,
                        LinkEdges);
                }
            }

            for (int i = 0; i < Regions.Length; i++) {
                NativeNavSurfaceRegionData region = Regions[i];
                region = new NativeNavSurfaceRegionData(region.ID, region.Bounds, region.TriangleIndexRange,
                    region.InternalLinkRange, ranges[i], region.UpVector, region.NormalVector, region.IslandIndex);
                Regions[i] = region;
            }
        }

        /// <summary>
        /// Initialize a new NativeNavVolumeData with the given data.
        /// </summary>
        /// <param name="id">ID of the volume.</param>
        /// <param name="transform">Transform matrix of the volume.</param>
        /// <param name="inverseTransform">Inverse transform matrix of the volume.</param>
        /// <param name="bounds">Bounds of the volume in local space.</param>
        /// <param name="layer">The layer this area exists on.</param>
        /// <param name="vertices">The vertex positions of all of the volume's regions.</param>
        /// <param name="regions">The regions of the volume.</param>
        /// <param name="triangleIndices">The indices of triangles.</param>
        /// <param name="internalLinks">The internal links of all of the volume's regions.</param>
        /// <param name="externalLinks">The external links of all of the volume's regions.</param>
        /// <param name="linkVertices">The shared vertices of all of the volume's internal links.</param>
        /// <param name="linkEdges">The shared edges of all of the volume's internal links.</param>
        public NativeNavSurfaceData(long id,
                                    float4x4 transform,
                                    float4x4 inverseTransform,
                                    NativeBounds bounds,
                                    NavLayer layer,
                                    UnsafeArray<float4> vertices,
                                    UnsafeArray<NativeNavSurfaceRegionData> regions,
                                    UnsafeArray<int> triangleIndices,
                                    UnsafeArray<NativeNavSurfaceInternalLinkData> internalLinks,
                                    UnsafeArray<NativeNavExternalLinkData> externalLinks,
                                    UnsafeArray<int> linkVertices,
                                    UnsafeArray<int2> linkEdges) {
            ID = id;
            Transform = transform;
            InverseTransform = inverseTransform;
            Bounds = bounds;
            Layer = layer;
            Vertices = vertices;
            Regions = regions;
            TriangleIndices = triangleIndices;
            InternalLinks = internalLinks;
            ExternalLinks = externalLinks;
            LinkVertices = linkVertices;
            LinkEdges = linkEdges;
        }

        public void Dispose() {
            Vertices.Dispose();
            Regions.Dispose();
            TriangleIndices.Dispose();
            InternalLinks.Dispose();
            ExternalLinks.Dispose();
            LinkVertices.Dispose();
            LinkEdges.Dispose();
        }
    }

    public struct NativeNavSurfaceRegionData {
        /// <summary>
        /// The ID of the region.
        /// </summary>
        public readonly int ID;

        /// <summary>
        /// The bounds of the region in local space of the area.
        /// </summary>
        public readonly NativeBounds Bounds;

        /// <summary>
        /// The range of the region's triangles in the area's <see cref="NativeNavSurfaceData.TriangleIndices"/> list.
        /// </summary>
        public readonly SerializableRange TriangleIndexRange;

        /// <summary>
        /// The range of the region's internal links in the area's
        /// <see cref="NativeNavSurfaceData.InternalLinks"/> list.
        /// </summary>
        public readonly SerializableRange InternalLinkRange;

        /// <summary>
        /// The range of the region's externals link in the area's
        /// <see cref="NativeNavSurfaceData.ExternalLinks"/> list.
        /// </summary>
        public readonly SerializableRange ExternalLinkRange;

        /// <summary>
        /// The upward direction for the region.
        /// </summary>
        public readonly float4 UpVector;

        /// <summary>
        /// The normal vector for the region, which may be different from the up vector depending on the upwards direction mode.
        /// </summary>
        public readonly float4 NormalVector;

        /// <summary>
        /// The index of the island the region belongs to.
        /// </summary>
        public readonly int IslandIndex;

        /// <summary>
        /// Initialize a new NativeNavSurfaceRegionData with the given data.
        /// </summary>
        /// <param name="id">The ID of the region.</param>
        /// <param name="bounds">The bounds of the region in local space of the area.</param>
        /// <param name="triangleIndexRange">The range of the region's triangle indices.</param>
        /// <param name="internalLinkRange">The range of the region's internal links.</param>
        /// <param name="externalLinkRange">The range of the region's external links.</param>
        /// <param name="upVector">The upward direction for the region.</param>
        /// <param name="normalVector">The normal vector for the region.</param>
        /// <param name="islandIndex">The index of the island the region belongs to.</param>
        public NativeNavSurfaceRegionData(int id, NativeBounds bounds, SerializableRange triangleIndexRange,
                                          SerializableRange internalLinkRange, SerializableRange externalLinkRange,
                                          float4 upVector, float4 normalVector, int islandIndex) {
            ID = id;
            Bounds = bounds;
            TriangleIndexRange = triangleIndexRange;
            InternalLinkRange = internalLinkRange;
            ExternalLinkRange = externalLinkRange;
            UpVector = upVector;
            NormalVector = normalVector;
            IslandIndex = islandIndex;
        }
    }

    public struct NativeNavSurfaceInternalLinkData {
        /// <summary>
        /// The ID of the connected region.
        /// </summary>
        public readonly int ToRegion;

        /// <summary>
        /// The range of the link's vertices in the area's
        /// <see cref="NativeNavSurfaceData.LinkVertices"/> list.
        /// </summary>
        public readonly SerializableRange VertexRange;

        /// <summary>
        /// The range of the link's edges in the area's
        /// <see cref="NativeNavSurfaceData.LinkEdges"/> list.
        /// </summary>
        public readonly SerializableRange EdgeRange;

        /// <summary>
        /// Initialize a new NativeNavSurfaceInternalLinkData with the given data.
        /// </summary>
        /// <param name="toRegion">The ID of the connected region.</param>
        /// <param name="vertexRange">The range of the link's vertices.</param>
        /// <param name="edgeRange">The range of the link's edges.</param>
        public NativeNavSurfaceInternalLinkData(int toRegion, SerializableRange vertexRange,
                                                SerializableRange edgeRange) {
            ToRegion = toRegion;
            VertexRange = vertexRange;
            EdgeRange = edgeRange;
        }
    }
}
