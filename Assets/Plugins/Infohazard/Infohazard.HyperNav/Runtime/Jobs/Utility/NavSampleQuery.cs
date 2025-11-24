// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Utility {
    public struct NavSampleQuery {
        /// <summary>
        /// Position to sample volumes (xyz) and radius (w).
        /// </summary>
        public float4 PositionWithRadiusForVolume;

        /// <summary>
        /// Position to sample surfaces (xyz) and radius (w).
        /// </summary>
        public float4 PositionWithRadiusForSurface;

        /// <summary>
        /// Type(s) of area to sample.
        /// </summary>
        public NavAreaTypes AreaTypeMask;

        /// <summary>
        /// Layers to sample. If zero, will sample all layers for compatibility.
        /// </summary>
        public NavLayerMask LayerMask;

        /// <summary>
        /// How to prioritize results when sampling in both volumes and surfaces.
        /// </summary>
        public NavSamplePriority Priority;

        /// <summary>
        /// If not zero, the up vector to use when sampling surfaces.
        /// The w component is the minimum dot product between the surface's up vector and this vector's xyz
        /// to consider a surface valid.
        /// </summary>
        public float4 UpVectorWithMinDotForSurface;

        /// <summary>
        /// Position to sample volumes.
        /// </summary>
        public float4 PositionForVolume {
            get => new(PositionWithRadiusForVolume.xyz, 1);
            set => PositionWithRadiusForVolume = new float4(value.xyz, PositionWithRadiusForVolume.w);
        }

        /// <summary>
        /// Position to sample surfaces.
        /// </summary>
        public float4 PositionForSurface {
            get => new(PositionWithRadiusForSurface.xyz, 1);
            set => PositionWithRadiusForSurface = new float4(value.xyz, PositionWithRadiusForSurface.w);
        }

        /// <summary>
        /// Vector to check when sampling surfaces. A valid surface must have an up vector that has a dot product
        /// with this vector's xyz greater than or equal to <see cref="MinUpVectorDotForSurface"/>.
        /// </summary>
        public float4 UpVectorForSurface {
            get => new(UpVectorWithMinDotForSurface.xyz, 0);
            set => UpVectorWithMinDotForSurface = new float4(value.xyz, UpVectorWithMinDotForSurface.w);
        }

        /// <summary>
        /// Radius of the sample for volumes.
        /// </summary>
        public float RadiusForVolume {
            get => PositionWithRadiusForVolume.w;
            set => PositionWithRadiusForVolume = new float4(PositionWithRadiusForVolume.xyz, value);
        }

        /// <summary>
        /// Radius of the sample for surfaces.
        /// </summary>
        public float RadiusForSurface {
            get => PositionWithRadiusForSurface.w;
            set => PositionWithRadiusForSurface = new float4(PositionWithRadiusForSurface.xyz, value);
        }

        /// <summary>
        /// Minimum dot product between the surface's up vector and the up vector for surfaces.
        /// </summary>
        public float MinUpVectorDotForSurface {
            get => UpVectorWithMinDotForSurface.w;
            set => UpVectorWithMinDotForSurface = new float4(UpVectorWithMinDotForSurface.xyz, value);
        }

        /// <summary>
        /// Create a new query.
        /// </summary>
        /// <param name="positionForVolume">Position to sample volumes.</param>
        /// <param name="radiusForVolume">Radius of the sample for volumes.</param>
        /// <param name="positionForSurface">Position to sample surfaces.</param>
        /// <param name="radiusForSurface">Radius of the sample for surfaces.</param>
        /// <param name="hitMask">Type(s) of area to sample.</param>
        /// <param name="layerMask">The layers to sample.</param>
        /// <param name="priority">How to prioritize results when sampling in both volumes and surfaces.</param>
        /// <param name="upVectorForSurface">Vector to check when sampling surfaces.</param>
        /// <param name="minUpVectorDotForSurface">Minimum dot product between the surface's up vector and the up vector for surfaces.</param>
        public NavSampleQuery(
            float3 positionForVolume,
            float3 positionForSurface,
            float radiusForVolume,
            float radiusForSurface,
            NavAreaTypes hitMask,
            uint layerMask = uint.MaxValue,
            NavSamplePriority priority = NavSamplePriority.Nearest,
            float3 upVectorForSurface = default,
            float minUpVectorDotForSurface = 0) {
            PositionWithRadiusForVolume = new float4(positionForVolume, radiusForVolume);
            PositionWithRadiusForSurface = new float4(positionForSurface, radiusForSurface);
            AreaTypeMask = hitMask;
            LayerMask = layerMask;
            Priority = priority;
            UpVectorWithMinDotForSurface = new float4(upVectorForSurface, minUpVectorDotForSurface);
        }

        /// <summary>
        /// Create a new query.
        /// </summary>
        /// <param name="position">Position to sample volumes and surfaces.</param>
        /// <param name="radius">Radius of the sample for volumes and surfaces.</param>
        /// <param name="hitMask">Type(s) of area to sample.</param>
        /// <param name="layerMask">The layers to sample.</param>
        /// <param name="priority">How to prioritize results when sampling in both volumes and surfaces.</param>
        public NavSampleQuery(
            float3 position,
            float radius,
            NavAreaTypes hitMask,
            uint layerMask = uint.MaxValue,
            NavSamplePriority priority = NavSamplePriority.Nearest) {
            PositionWithRadiusForVolume = new float4(position, radius);
            PositionWithRadiusForSurface = new float4(position, radius);
            AreaTypeMask = hitMask;
            LayerMask = layerMask;
            Priority = priority;
            UpVectorWithMinDotForSurface = float4.zero;
        }
    }

    /// <summary>
    /// How to prioritize results when sampling in both volumes and surfaces.
    /// </summary>
    public enum NavSamplePriority {
        Nearest, Surface, Volume,
    }
}
