// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Infohazard.HyperNav.Settings {
    /// <summary>
    /// Settings for baking NavSurfaces.
    /// </summary>
    [Serializable]
    public class NavSurfaceSettings : NavAreaBaseSettings<NavSurfaceSettings> {
        [SerializeField]
        [Min(0)]
        [Tooltip("The maximum height of agents walking on this volume.")]
        [HelpBox("This defines the maximum height of agents that will walk on the surface. " +
                 "The agents are assumed to have a capsule shape pointing along the upwards direction " +
                 "defined below.")]
        private float _maxAgentHeight = 2;

        [SerializeField]
        [Min(0)]
        [Tooltip("The minimum distance from edge of the surface to the edge of walkable ground.")]
        [HelpBox("This defines a minimum distance from the edge of the surface to the edge of actual walkable ground. " +
                 "Without this buffer, paths may go right up to the edge and cause the agent to walk right off.")]
        private float _erosionDistance = 0.25f;

        [SerializeField]
        [Tooltip("Layers to consider as walkable.")]
        [HelpBox("This defines which layers the agents are able to walk on. Some objects may block movement, " +
                 "but cannot actually be walked on. This field allows you to filter out such objects.")]
        private LayerMask _walkableLayers = 1;

        [SerializeField]
        [Tooltip("How to determine the 'upright' direction for walking on the surface.")]
        [HelpBox("The upright direction is used to determine the direction that is considered 'up' " +
                 "for agents walking on the surface.\n\n" +
                 "In situations with standard gravity, this can simply be the global upward direction." +
                 "However, if characters can walk on walls, for example, it should use the surface normal." +
                 "You can also use a custom handler to determine the upright direction.")]
        private NavSurfaceUprightDirectionMode _uprightDirectionMode = NavSurfaceUprightDirectionMode.HitNormal;

        [SerializeField]
        [Tooltip("Fixed direction to use as the upright direction.")]
        [HelpBox("When using a fixed upright direction mode, this defines that direction. " +
                 "Depending on the mode set above, this can be a world or local direction.")]
        private Vector3 _fixedUprightDirection = Vector3.up;

        [Tooltip("Angle limit between hit normal and upright direction.")] [SerializeField]
        [HelpBox("When using a fixed upright direction mode, this defines the maximum angle " +
                 "between the surface normal and the upright direction. If the angle is greater than this, " +
                 "the surface is considered too steep to walk on.")]
        private float _slopeAngleLimit = 45;

        [SerializeField]
        [Expandable(requiredInterfaces: typeof(ISurfaceUprightDirectionHandler))]
        [Tooltip("Custom handler to determine the upright direction.")]
        private Object _customUprightDirectionHandler;

        [SerializeField]
        [Tooltip("The ground normals of a triangle must be within this angle in degrees of each other.")]
        [HelpBox("This defines the maximum angle between the upright directions at the vertices of a triangle. " +
                 "If the angle is greater than this, meaning the triangle is too 'bent', it is discarded.")]
        private float _maxAngleBetweenUpDirectionsWithinTriangle = 30;

        [SerializeField]
        [Tooltip("The ground normals of neighboring triangles must be within this angle in degrees of each other.")]
        [HelpBox("This defines the maximum angle between the upright directions of neighboring triangles. " +
                 "If the angle is greater than this, the triangles are split up and not considered neighbors. " +
                 "For example, agents most likely cannot walk over a 90-degree angle, and this ensures that " +
                 "is reflected in the graph.\n\n" +
                 "In addition, this value determines the maximum angle between the upright directions of regions " +
                 "when calculating the external links between nearby surfaces.")]
        private float _maxAngleBetweenUpDirectionsBetweenTriangles = 30;

        [SerializeField]
        [Tooltip("Maximum number of times to divide a triangle lying on a corner to try triangles on flat surfaces.")]
        [HelpBox("When a triangle would be discarded due to the collision or ground checks, this allows it " +
                 "instead to be split into smaller triangles, which then have the checks applied to them.\n\n" +
                 "This can result in better baking performance than using a smaller voxel size, but also produces " +
                 "less clean and accurate results.")]
        private int _maxTriangleDivisions = 1;

        [SerializeField]
        [Tooltip("If greater than zero, islands with less surface area will be discarded.")]
        [HelpBox("This value defines a minimum value for the area of islands " +
                 "(continuous walkable areas in the surface). Any islands with an area lower than this, such as " +
                 "the faces of a small cube, will be discarded.")]
        private float _minIslandSurfaceArea = 0;

        [SerializeField]
        [Tooltip("Threshold for decimation - a higher threshold means more aggressive decimation.")]
        [HelpBox("This determines how aggressive the decimation operation during baking is. " +
                 "A higher value will result in a simpler mesh and better pathfinding performance, " +
                 "but can also result in a less accurate representation of the surface. " +
                 "There are no specific units for this value, but it does scale with the voxel size.")]
        private float _decimationThreshold = 0.5f;

        [SerializeField]
        [Tooltip("Threshold for removing bound vertices in decimation.")]
        [HelpBox("This determines how aggressively boundary vertices (those vertices which are at the edge of a " +
                 "walkable area) are removed during decimation. A higher value will result in a simpler mesh, " +
                 "but can also result in less precise edges, which may clip through solid geometry or pass " +
                 "over empty space.\n\n" +
                 "This value is multiplied by the voxel size to determine the distance that a vertex " +
                 "can be moved if it is being clipped.")]
        private float _boundaryTriangleClippingThreshold = 0.6f;

        [SerializeField]
        [Tooltip("The minimum area of a triangle as a fraction of the square of the voxel size.")]
        [HelpBox("This value applies a minimum area for non-connecting triangles after decimation. " +
                 "Any non-connecting triangles (triangles that only connect to a maximum of one other triangle) " +
                 "which have an area less than this value times the square voxel size are removed.")]
        private float _minBoundaryTriangleAreaFraction = 0.5f;

        [SerializeField]
        [Range(0, 90)]
        [Tooltip("The maximum angle in degrees between the surface normal and the approach direction from volumes.")]
        [HelpBox("The maximum angle between the surface normal and the approach direction from volumes to that surface , " +
                 "or that surface to volumes on external links. This avoids trying to approach a surface from " +
                 "the side, which could result in the agent getting stuck on the edge.")]
        private float _maxSurfaceVolumeApproachAngle = 60;

        /// <summary>
        /// This is used to refer to the names of private fields in this class from a custom Editor.
        /// </summary>
        public new static class PropNames {
            public const string MaxAgentHeight = nameof(_maxAgentHeight);
            public const string ErosionDistance = nameof(_erosionDistance);
            public const string WalkableLayers = nameof(_walkableLayers);
            public const string UprightDirectionMode = nameof(_uprightDirectionMode);
            public const string FixedUprightDirection = nameof(_fixedUprightDirection);
            public const string SlopeAngleLimit = nameof(_slopeAngleLimit);
            public const string CustomUprightDirectionHandler = nameof(_customUprightDirectionHandler);

            public const string MaxAngleBetweenUpDirectionsWithinTriangle =
                nameof(_maxAngleBetweenUpDirectionsWithinTriangle);

            public const string MaxAngleBetweenUpDirectionsBetweenTriangles =
                nameof(_maxAngleBetweenUpDirectionsBetweenTriangles);

            public const string MaxTriangleDivisions = nameof(_maxTriangleDivisions);
            public const string MinIslandSurfaceArea = nameof(_minIslandSurfaceArea);
            public const string DecimationThreshold = nameof(_decimationThreshold);
            public const string BoundaryTriangleClippingThreshold = nameof(_boundaryTriangleClippingThreshold);
            public const string MinTriangleAreaFraction = nameof(_minBoundaryTriangleAreaFraction);
            public const string MaxSurfaceVolumeApproachAngle = nameof(_maxSurfaceVolumeApproachAngle);
        }

        /// <summary>
        /// The maximum height of agents walking on this surface.
        /// </summary>
        public float MaxAgentHeight {
            get => _maxAgentHeight;
            set => _maxAgentHeight = value;
        }

        /// <summary>
        /// The minimum distance from edge of the surface to the edge of walkable ground.
        /// </summary>
        public float ErosionDistance {
            get => _erosionDistance;
            set => _erosionDistance = value;
        }

        /// <summary>
        /// Layers to consider as walkable.
        /// </summary>
        public LayerMask WalkableLayers {
            get => _walkableLayers;
            set => _walkableLayers = value;
        }

        /// <summary>
        /// How to determine the 'upright' direction for walking on the surface.
        /// </summary>
        public NavSurfaceUprightDirectionMode UprightDirectionMode {
            get => _uprightDirectionMode;
            set => _uprightDirectionMode = value;
        }

        /// <summary>
        /// Fixed direction to use as the upright direction.
        /// Only used if <see cref="UprightDirectionMode"/> is set to
        /// <see cref="NavSurfaceUprightDirectionMode.FixedWorldDirection"/> or
        /// <see cref="NavSurfaceUprightDirectionMode.FixedLocalDirection"/>.
        /// </summary>
        public Vector3 FixedUprightDirection {
            get => _fixedUprightDirection;
            set => _fixedUprightDirection = value;
        }

        /// <summary>
        /// Angle limit in degrees between hit normal and upright direction.
        /// Only used if <see cref="UprightDirectionMode"/> is set to
        /// <see cref="NavSurfaceUprightDirectionMode.FixedWorldDirection"/> or
        /// <see cref="NavSurfaceUprightDirectionMode.FixedLocalDirection"/>.
        /// </summary>
        public float SlopeAngleLimit {
            get => _slopeAngleLimit;
            set => _slopeAngleLimit = value;
        }

        /// <summary>
        /// Custom handler to determine the upright direction.
        /// Only used if <see cref="UprightDirectionMode"/> is set to
        /// <see cref="NavSurfaceUprightDirectionMode.Custom"/>.
        /// </summary>
        public ISurfaceUprightDirectionHandler CustomUprightDirectionHandler {
            get => _customUprightDirectionHandler as ISurfaceUprightDirectionHandler;
            set => _customUprightDirectionHandler = (Object)value;
        }

        /// <summary>
        /// The ground normals at all vertices of a triangle must be within this angle (in degrees) of each other.
        /// Otherwise, the triangle is discarded.
        /// </summary>
        public float MaxAngleBetweenUpDirectionsWithinTriangle {
            get => _maxAngleBetweenUpDirectionsWithinTriangle;
            set => _maxAngleBetweenUpDirectionsWithinTriangle = value;
        }

        /// <summary>
        /// The ground normals of triangles at a vertex must be within this angle (in degrees) of each other.
        /// Otherwise, the vertex will be split into groups.
        /// </summary>
        public float MaxAngleBetweenUpDirectionsBetweenTriangles {
            get => _maxAngleBetweenUpDirectionsBetweenTriangles;
            set => _maxAngleBetweenUpDirectionsBetweenTriangles = value;
        }

        /// <summary>
        /// Maximum number of times to divide a triangle lying on a corner to try triangles on flat surfaces.
        /// </summary>
        public int MaxTriangleDivisions {
            get => _maxTriangleDivisions;
            set => _maxTriangleDivisions = value;
        }

        /// <summary>
        /// If greater than zero, islands with less surface area will be discarded.
        /// </summary>
        public float MinIslandSurfaceArea {
            get => _minIslandSurfaceArea;
            set => _minIslandSurfaceArea = value;
        }

        /// <summary>
        /// Threshold for decimation - a higher threshold means more aggressive decimation.
        /// </summary>
        public float DecimationThreshold {
            get => _decimationThreshold;
            set => _decimationThreshold = value;
        }

        /// <summary>
        /// Threshold for removing bound vertices in decimation - a higher threshold means more aggressive decimation.
        /// </summary>
        public float BoundaryTriangleClippingThreshold {
            get => _boundaryTriangleClippingThreshold;
            set => _boundaryTriangleClippingThreshold = value;
        }

        /// <summary>
        /// The minimum area of a triangle as a fraction of the square of the voxel size.
        /// Smaller triangles are merged or discarded.
        /// </summary>
        public float MinTriangleAreaFraction {
            get => _minBoundaryTriangleAreaFraction;
            set => _minBoundaryTriangleAreaFraction = value;
        }

        /// <summary>
        /// The maximum angle in degrees between the surface normal and the approach direction
        /// from volumes to that surface, or that surface to volumes.
        /// </summary>
        public float MaxSurfaceVolumeApproachAngle {
            get => _maxSurfaceVolumeApproachAngle;
            set => _maxSurfaceVolumeApproachAngle = value;
        }

        /// <inheritdoc />
        public override NavSurfaceSettings Clone() {
            NavSurfaceSettings clone = base.Clone();
            clone.MaxAgentHeight = _maxAgentHeight;
            clone.ErosionDistance = _erosionDistance;
            clone.WalkableLayers = _walkableLayers;
            clone.UprightDirectionMode = _uprightDirectionMode;
            clone.FixedUprightDirection = _fixedUprightDirection;
            clone.SlopeAngleLimit = _slopeAngleLimit;
            clone.CustomUprightDirectionHandler = CustomUprightDirectionHandler;
            clone.MaxAngleBetweenUpDirectionsWithinTriangle = _maxAngleBetweenUpDirectionsWithinTriangle;
            clone.MaxAngleBetweenUpDirectionsBetweenTriangles = _maxAngleBetweenUpDirectionsBetweenTriangles;
            clone.MaxTriangleDivisions = _maxTriangleDivisions;
            clone.MinIslandSurfaceArea = _minIslandSurfaceArea;
            clone.DecimationThreshold = _decimationThreshold;
            clone.BoundaryTriangleClippingThreshold = _boundaryTriangleClippingThreshold;
            clone.MinTriangleAreaFraction = _minBoundaryTriangleAreaFraction;
            clone.MaxSurfaceVolumeApproachAngle = _maxSurfaceVolumeApproachAngle;
            return clone;
        }
    }
}
