// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Utility {
    /// <summary>
    /// A native-friendly representation of a navigation query result.
    /// </summary>
    public readonly struct NavSampleResult {
        public static readonly NavSampleResult Invalid =
            new(-1, NavAreaTypes.Nothing, NavLayer.None, -1, false, float4.zero, float4.zero, 0);

        /// <summary>
        /// ID of the volume that was hit.
        /// </summary>
        public readonly long AreaID;

        /// <summary>
        /// Type of area that was hit.
        /// </summary>
        public readonly NavAreaTypes Type;

        /// <summary>
        /// The layer of the area that was hit.
        /// </summary>
        public readonly NavLayer Layer;

        /// <summary>
        /// ID of the region that was hit.
        /// </summary>
        public readonly int Region;

        /// <summary>
        /// Whether the result point was on the edge of the region.
        /// </summary>
        [MarshalAs(UnmanagedType.U1)]
        public readonly bool IsOnEdge;

        /// <summary>
        /// Position of the hit.
        /// </summary>
        public readonly float4 Position;

        /// <summary>
        /// Up vector at the hit point. Zero for volumes.
        /// </summary>
        public readonly float4 Up;

        /// <summary>
        /// Distance from the sample position to the hit point.
        /// </summary>
        public readonly float Distance;

        /// <summary>
        /// Get the <see cref="NavVolume"/> instance that was hit. Not available in Burst.
        /// If the result type is not <see cref="NavAreaTypes.Volume"/>, this will return null.
        /// </summary>
        /// <remarks>
        /// This property performs a dictionary lookup, so the result should be cached.
        /// </remarks>
        [BurstDiscard]
        public NavVolume Volume =>
            Type == NavAreaTypes.Volume ? NavVolume.Instances.GetValueOrDefault(AreaID) : null;

        /// <summary>
        /// Get the <see cref="NavSurface"/> instance that was hit. Not available in Burst.
        /// If the result type is not <see cref="NavAreaTypes.Surface"/>, this will return null.
        /// </summary>
        /// <remarks>
        /// This property performs a dictionary lookup, so the result should be cached.
        /// </remarks>
        [BurstDiscard]
        public NavSurface Surface =>
            Type == NavAreaTypes.Surface ? NavSurface.Instances.GetValueOrDefault(AreaID) : null;

        /// <summary>
        /// Get the <see cref="NavAreaBase"/> instance that was hit. Not available in Burst.
        /// </summary>
        /// <remarks>
        /// This property performs a dictionary lookup, so the result should be cached.
        /// </remarks>
        [BurstDiscard]
        public NavAreaBase Area => Type switch {
            NavAreaTypes.Surface => Surface,
            NavAreaTypes.Volume => Volume,
            _ => null,
        };

        /// <summary>
        /// Initialize a new NativeNavHit with the given data.
        /// </summary>
        /// <param name="areaID">ID of the volume that was hit.</param>
        /// <param name="type">Type of area that was hit.</param>
        /// <param name="layer">Layer of the area that was hit.</param>
        /// <param name="region">ID of the region that was hit.</param>
        /// <param name="isOnEdge">Whether the result point was on the edge of the region.</param>
        /// <param name="position">Position of the hit.</param>
        /// <param name="up">Up vector at the hit point. Zero for volumes.</param>
        /// <param name="distance">Distance from the sample position to the hit point.</param>
        public NavSampleResult(long areaID,
                               NavAreaTypes type,
                               NavLayer layer,
                               int region,
                               bool isOnEdge,
                               float4 position,
                               float4 up,
                               float distance) {
            AreaID = areaID;
            Type = type;
            Layer = layer;
            Region = region;
            IsOnEdge = isOnEdge;
            Position = position;
            Up = up;
            Distance = distance;
        }
    }
}
