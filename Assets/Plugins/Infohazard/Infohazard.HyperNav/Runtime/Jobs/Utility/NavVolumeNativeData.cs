// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.Core;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Utility {
    /// <summary>
    /// The baked data of a <see cref="NavVolume"/>, converted to a form compatible with Burst.
    /// </summary>
    public readonly struct NativeNavVolumeData : IDisposable, INativeNavAreaData {
        /// <summary>
        /// ID of the volume.
        /// </summary>
        public readonly long ID;

        /// <summary>
        /// Transform matrix of the volume.
        /// </summary>
        public readonly float4x4 Transform;

        float4x4 INativeNavAreaData.Transform => Transform;

        /// <summary>
        /// Inverse transform matrix of the volume.
        /// </summary>
        public readonly float4x4 InverseTransform;

        /// <summary>
        /// Bounds of the volume in local space.
        /// </summary>
        public readonly NativeBounds Bounds;

        /// <summary>
        /// The layer this area exists on.
        /// </summary>
        public readonly NavLayer Layer;

        /// <summary>
        /// The vertex positions of all of the volume's regions, in local space.
        /// </summary>
        public readonly UnsafeArray<float4> Vertices;

        /// <summary>
        /// The regions of the volume.
        /// </summary>
        public readonly UnsafeArray<NativeNavVolumeRegionData> Regions;

        /// <summary>
        /// The vertex indices of triangles.
        /// </summary>
        public readonly UnsafeArray<int> TriangleIndices;

        /// <summary>
        /// The number of indices in the <see cref="TriangleIndices"/> array that represent blocking triangles.
        /// </summary>
        public readonly int BlockingTriangleIndexCount;

        /// <summary>
        /// The bound planes of all of the volume's regions.
        /// </summary>
        public readonly UnsafeArray<NativePlane> BoundPlanes;

        /// <summary>
        /// The internal links of all of the volume's regions.
        /// </summary>
        public readonly UnsafeArray<NativeNavVolumeInternalLinkData> InternalLinks;

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

        /// <summary>
        /// The shared triangles of all of the volume's internal links.
        /// </summary>
        public readonly UnsafeArray<int3> LinkTriangles;

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
                fixed (NativeNavVolumeData* thisPtr = &this) {
                    *thisPtr = new NativeNavVolumeData(
                        ID,
                        Transform,
                        InverseTransform,
                        Bounds,
                        Layer,
                        Vertices,
                        Regions,
                        TriangleIndices,
                        BlockingTriangleIndexCount,
                        BoundPlanes,
                        InternalLinks,
                        links,
                        LinkVertices,
                        LinkEdges,
                        LinkTriangles);
                }
            }

            for (int i = 0; i < Regions.Length; i++) {
                NativeNavVolumeRegionData region = Regions[i];
                region = new NativeNavVolumeRegionData(region.ID, region.Bounds, region.TriangleIndexRange,
                    region.BoundPlaneRange, region.InternalLinkRange, ranges[i]);
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
        /// <param name="blockingTriangleIndexCount">The number of indices in the <see cref="TriangleIndices"/> array that represent blocking triangles.</param>
        /// <param name="boundPlanes">The bound planes of all of the volume's regions.</param>
        /// <param name="internalLinks">The internal links of all of the volume's regions.</param>
        /// <param name="externalLinks">The external links of all of the volume's regions.</param>
        /// <param name="linkVertices">The shared vertices of all of the volume's internal links.</param>
        /// <param name="linkEdges">The shared edges of all of the volume's internal links.</param>
        /// <param name="linkTriangles">The shared triangles of all of the volume's internal links.</param>
        public NativeNavVolumeData(long id,
                                   float4x4 transform,
                                   float4x4 inverseTransform,
                                   NativeBounds bounds,
                                   NavLayer layer,
                                   UnsafeArray<float4> vertices,
                                   UnsafeArray<NativeNavVolumeRegionData> regions,
                                   UnsafeArray<int> triangleIndices,
                                   int blockingTriangleIndexCount,
                                   UnsafeArray<NativePlane> boundPlanes,
                                   UnsafeArray<NativeNavVolumeInternalLinkData> internalLinks,
                                   UnsafeArray<NativeNavExternalLinkData> externalLinks,
                                   UnsafeArray<int> linkVertices,
                                   UnsafeArray<int2> linkEdges,
                                   UnsafeArray<int3> linkTriangles) {
            ID = id;
            Transform = transform;
            InverseTransform = inverseTransform;
            Bounds = bounds;
            Layer = layer;
            Vertices = vertices;
            Regions = regions;
            TriangleIndices = triangleIndices;
            BlockingTriangleIndexCount = blockingTriangleIndexCount;
            BoundPlanes = boundPlanes;
            InternalLinks = internalLinks;
            ExternalLinks = externalLinks;
            LinkVertices = linkVertices;
            LinkEdges = linkEdges;
            LinkTriangles = linkTriangles;
        }

        public void Dispose() {
            Vertices.Dispose();
            Regions.Dispose();
            TriangleIndices.Dispose();
            BoundPlanes.Dispose();
            InternalLinks.Dispose();
            ExternalLinks.Dispose();
            LinkVertices.Dispose();
            LinkEdges.Dispose();
            LinkTriangles.Dispose();
        }
    }

    /// <summary>
    /// The native-friendly data representing a single region in a NavVolume.
    /// </summary>
    public readonly struct NativeNavVolumeRegionData {
        /// <summary>
        /// The ID of the region.
        /// </summary>
        public readonly int ID;

        /// <summary>
        /// The bounds of the region in local space of the volume.
        /// </summary>
        public readonly NativeBounds Bounds;

        /// <summary>
        /// The range of the region's triangles in the volume's <see cref="NativeNavVolumeData.TriangleIndices"/> list.
        /// </summary>
        public readonly SerializableRange TriangleIndexRange;

        /// <summary>
        /// The range of the region's bound planes in the volume's
        /// <see cref="NativeNavVolumeData.BoundPlanes"/> list.
        /// </summary>
        public readonly SerializableRange BoundPlaneRange;

        /// <summary>
        /// The range of the region's internal links in the volume's
        /// <see cref="NativeNavVolumeData.InternalLinks"/> list.
        /// </summary>
        public readonly SerializableRange InternalLinkRange;

        /// <summary>
        /// The range of the region's externals link in the volume's
        /// <see cref="NativeNavVolumeData.ExternalLinks"/> list.
        /// </summary>
        public readonly SerializableRange ExternalLinkRange;

        /// <summary>
        /// Initialize a new NativeNavRegionData with the given data.
        /// </summary>
        /// <param name="id">The ID of the region.</param>
        /// <param name="bounds">The bounds of the region in local space of the volume.</param>
        /// <param name="triangleIndexRange">The range of the region's triangle indices.</param>
        /// <param name="boundPlaneRange">The range of the region's bound planes.</param>
        /// <param name="internalLinkRange">The range of the region's internal links.</param>
        /// <param name="externalLinkRange">The range of the region's external links.</param>
        public NativeNavVolumeRegionData(int id, NativeBounds bounds, SerializableRange triangleIndexRange,
                                   SerializableRange boundPlaneRange, SerializableRange internalLinkRange,
                                   SerializableRange externalLinkRange) {
            ID = id;
            Bounds = bounds;
            TriangleIndexRange = triangleIndexRange;
            BoundPlaneRange = boundPlaneRange;
            InternalLinkRange = internalLinkRange;
            ExternalLinkRange = externalLinkRange;
        }
    }

    /// <summary>
    /// The native-friendly data representing a connection from one region to another region in the same volume.
    /// </summary>
    public readonly struct NativeNavVolumeInternalLinkData {
        /// <summary>
        /// The ID of the connected region.
        /// </summary>
        public readonly int ToRegion;

        /// <summary>
        /// The range of the link's vertices in the volume's
        /// <see cref="NativeNavVolumeData.LinkVertices"/> list.
        /// </summary>
        public readonly SerializableRange VertexRange;

        /// <summary>
        /// The range of the link's edges in the volume's
        /// <see cref="NativeNavVolumeData.LinkEdges"/> list.
        /// </summary>
        public readonly SerializableRange EdgeRange;

        /// <summary>
        /// The range of the link's triangles in the volume's
        /// <see cref="NativeNavVolumeData.LinkTriangles"/> list.
        /// </summary>
        public readonly SerializableRange TriangleRange;

        /// <summary>
        /// Initialize a new NativeNavInternalLinkData with the given data.
        /// </summary>
        /// <param name="toRegion">The ID of the connected region.</param>
        /// <param name="vertexRange">The range of the link's vertices.</param>
        /// <param name="edgeRange">The range of the link's edges.</param>
        /// <param name="triangleRange">The range of the link's triangles.</param>
        public NativeNavVolumeInternalLinkData(int toRegion, SerializableRange vertexRange, SerializableRange edgeRange,
                                         SerializableRange triangleRange) {
            ToRegion = toRegion;
            VertexRange = vertexRange;
            EdgeRange = edgeRange;
            TriangleRange = triangleRange;
        }
    }

    /// <summary>
    /// The native-friendly data representing a connection from one region to another region in another volume.
    /// </summary>
    public readonly struct NativeNavExternalLinkData {
        /// <summary>
        /// The ID of the connected area.
        /// </summary>
        public readonly long ToArea;

        /// <summary>
        /// The type of the connected area.
        /// </summary>
        public readonly NavAreaTypes ToAreaType;

        /// <summary>
        /// The ID of the connected region.
        /// </summary>
        public readonly int ToRegion;

        /// <summary>
        /// The position at which the connection originates (local space of the 'from' volume).
        /// </summary>
        public readonly float4 FromPosition;

        /// <summary>
        /// The position at which the connection ends (local space of the 'from' volume).
        /// </summary>
        public readonly float4 ToPosition;

        /// <summary>
        /// The distance from <see cref="FromPosition"/> to <see cref="ToPosition"/>.
        /// </summary>
        public readonly float InternalCost;

        /// <summary>
        /// The unique ID of the ManualNavLink that created this link, or 0.
        /// </summary>
        public readonly long ManualLinkID;

        /// <summary>
        /// Initialize a new NativeNavExternalLinkData with the given data.
        /// </summary>
        /// <param name="toArea">The ID of the connected volume.</param>
        /// <param name="toAreaType">The type of the connected volume.</param>
        /// <param name="toRegion">The ID of the connected region.</param>
        /// <param name="fromPosition">The position at which the connection originates.</param>
        /// <param name="toPosition">The position at which the connection ends.</param>
        /// <param name="manualLinkID">The unique ID of the ManualNavLink that created this link, or 0.</param>
        public NativeNavExternalLinkData(long toArea, NavAreaTypes toAreaType, int toRegion, float4 fromPosition,
                                         float4 toPosition, long manualLinkID) {
            ToArea = toArea;
            ToAreaType = toAreaType;
            ToRegion = toRegion;
            FromPosition = fromPosition;
            ToPosition = toPosition;
            InternalCost = math.distance(fromPosition, toPosition);
            ManualLinkID = manualLinkID;
        }
    }

    /// <summary>
    /// A native-friendly of a bounding box.
    /// </summary>
    public struct NativeBounds {
        /// <summary>
        /// Center of the bounds.
        /// </summary>
        public float4 Center;

        /// <summary>
        /// Extents of the bounds (half of its size).
        /// </summary>
        public float4 Extents;

        public readonly float4 Min => Center - Extents;

        public readonly float4 Max => Center + Extents;

        public readonly float4 Size => Extents * 2;

        /// <summary>
        /// Initialize a new NativeBounds with the given data.
        /// </summary>
        /// <param name="center">Center of the bounds.</param>
        /// <param name="extents">Extents of the bounds (half of its size).</param>
        public NativeBounds(float4 center, float4 extents) {
            Center = center;
            Extents = extents;
        }

        public void Encapsulate(float4 point) {
            float4 min = math.min(Min, point);
            float4 max = math.max(Max, point);

            Center = (min + max) * 0.5f;
            Extents = (max - min) * 0.5f;
        }

        public readonly bool Contains(float4 point) {
            return math.all((point >= Min) & (point <= Max));
        }

        public readonly bool Intersects(NativeBounds other) {
            return math.all((Min <= other.Max) & (Max >= other.Min));
        }
    }

    /// <summary>
    /// The state of a pathfinding request.
    /// </summary>
    public enum NavPathState {
        /// <summary>
        /// Path is still processing.
        /// </summary>
        Pending,

        /// <summary>
        /// A valid path was found.
        /// </summary>
        Success,

        /// <summary>
        /// Request finished processing and no valid path was found.
        /// </summary>
        Failure,
    }

    /// <summary>
    /// A structure used by the navigation job to return the waypoints of a path.
    /// </summary>
    public readonly struct NativeNavWaypoint {
        /// <summary>
        /// Position of the waypoint in world space.
        /// </summary>
        public readonly float4 Position;

        /// <summary>
        /// Upward direction of the waypoint.
        /// </summary>
        public readonly float4 Up;

        /// <summary>
        /// Type of the waypoint in relation to the containing volume.
        /// </summary>
        public readonly NavWaypointType Type;

        /// <summary>
        /// Identifier of the NavArea that contains this waypoint, or -1.
        /// </summary>
        public readonly long AreaID;

        /// <summary>
        /// Identifier of the region that contains this waypoint, or -1.
        /// </summary>
        public readonly int Region;

        /// <summary>
        /// Initialize a new NativeNavWaypoint with the given data.
        /// </summary>
        /// <param name="position">Position of the waypoint in world space.</param>
        /// <param name="up">Upward direction of the waypoint.</param>
        /// <param name="type">Type of the waypoint in relation to the containing volume.</param>
        /// <param name="areaID">Identifier of the NavVolume that contains this waypoint, or -1.</param>
        /// <param name="region">Identifier of the region that contains this waypoint, or -1.</param>
        public NativeNavWaypoint(float4 position, float4 up, NavWaypointType type, long areaID, int region) {
            Position = position;
            Up = up;
            Type = type;
            AreaID = areaID;
            Region = region;
        }
    }
}
