// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.Core;
using Unity.Profiling;
using UnityEngine;

namespace Infohazard.HyperNav {
    /// <summary>
    /// A script that can be used to calculate smooth paths by any entity that needs to use HyperNav for navigation.
    /// </summary>
    /// <remarks>
    /// A SplineNavAgent works just like (and is a subclass of) <see cref="NavAgent"/>.
    /// However, a SplineNavAgent feeds the path waypoints into a spline function to get a smoother path.
    /// The spline-based movement is only active during volume-based navigation;
    /// surface navigation will use the default point-based path.
    /// In order to follow the spline, the SplineNavAgent creates its <see cref="NavAgent.DesiredVelocity"/> based on two factors:
    /// <list type="bullet">
    /// <item>Tangent: The direction the spline is pointing nearest the agent.</item>
    /// <item>Alignment: The direction from the agent to the nearest point on the spline.</item>f
    /// </list>
    /// The agent will increase the influence of the alignment velocity the further it gets from the spline,
    /// in order to prevent drifting too far off.
    /// Additionally, the agent can slow down its desired tangent velocity on high-curvature regions,
    /// in order to take the curves more slowly.
    /// </remarks>
    public class SplineNavAgent : NavAgent {
        #region Serialized Fields

        [SerializeField]
        [Tooltip("Scale to apply to spline tangents (lower values make the spline less curvy).")]
        private float _tangentScale = 0.5f;

        [SerializeField]
        [Tooltip("Whether to shorten tangents by raycasting to ensure they don't penetrate blocked areas.")]
        private bool _raycastTangents = true;

        [SerializeField]
        [Tooltip("How many samples to take per segment of the spline when mapping the distance.")]
        private int _distanceSamplesPerSegment = 5;

        [SerializeField]
        [Tooltip("If DebugPath is enabled, how many points to use to draw the curve.")]
        private int _debugPointCount = 50;

        [Tooltip("At what distance from the spline the agent will have all its desired velocity devoted to returning.")]
        [SerializeField]
        private float _maxAlignmentVelocityDistance = 1.5f;

        [SerializeField]
        [Tooltip("Delta-T to use when sampling curvature (should be quite small).")]
        private float _curvatureSampleDistance = 0.01f;

        [SerializeField]
        [Tooltip("At what curvature value is the agent at its max curvature slowdown.")]
        private float _curvatureOfMaxSlowdown = 0.5f;

        [SerializeField]
        [Tooltip("The multiplier on desired tangent velocity when at the max curvature value.")]
        private float _maxCurvatureSlowdown = 0.5f;

        [SerializeField]
        [Tooltip("Whether to draw debug lines when projecting on the spline.")]
        private bool _debugProjectOnSpline = false;

        [SerializeField]
        [Tooltip("Distance in front of the agent to check to see if it needs to avoid level geometry.")]
        private float _blockedDetectionDistance = 0.1f;

        [SerializeField]
        [Tooltip("Distance behind the agent to check to see if it needs to avoid level geometry.")]
        private float _blockedDetectionBackDistance = 0.1f;

        [SerializeField]
        [Tooltip("How far the agent must be from the spline to check for blocking level geometry.")]
        private float _blockedDetectionMinSplineDistance = 0.2f;

        [SerializeField]
        [Tooltip("Whether to use physics queries to detect blocking level geometry.")]
        private bool _blockedDetectionPhysicsQuery = false;

        [SerializeField]
        [Tooltip("Rigidbody to use for physics queries to detect blocking level geometry.")]
        [ConditionalDraw(nameof(_blockedDetectionPhysicsQuery))]
        private Rigidbody _blockedDetectionPhysicsQueryRigidbody;

        public new static class PropNames {
            public const string TangentScale = nameof(_tangentScale);
            public const string RaycastTangents = nameof(_raycastTangents);
            public const string DistanceSamplesPerSegment = nameof(_distanceSamplesPerSegment);
            public const string DebugPointCount = nameof(_debugPointCount);
            public const string MaxAlignmentVelocityDistance = nameof(_maxAlignmentVelocityDistance);
            public const string CurvatureSampleDistance = nameof(_curvatureSampleDistance);
            public const string CurvatureOfMaxSlowdown = nameof(_curvatureOfMaxSlowdown);
            public const string MaxCurvatureSlowdown = nameof(_maxCurvatureSlowdown);
            public const string DebugProjectOnSpline = nameof(_debugProjectOnSpline);
            public const string BlockedDetectionDistance = nameof(_blockedDetectionDistance);
            public const string BlockedDetectionBackDistance = nameof(_blockedDetectionBackDistance);
            public const string BlockedDetectionMinSplineDistance = nameof(_blockedDetectionMinSplineDistance);
            public const string BlockedDetectionPhysicsQuery = nameof(_blockedDetectionPhysicsQuery);
            public const string BlockedDetectionPhysicsQueryRigidbody = nameof(_blockedDetectionPhysicsQueryRigidbody);
        }

        #endregion

        #region Private Fields

        // Calculated spline that the agent is following.
        private SplinePath _splinePath;

        private float _pathDistanceAtSplineStart;

        // Whether the value of _splinePath is valid.

        // Parameter where the agent is nearest to along the spline.
        private float _projParam;

        // World-space position where the agent is nearest to along the spline.
        private Vector3 _projPosition;

        // Tangent at position where the agent is nearest to along the spline.
        private Vector3 _projTangent;

        // Measured curvature where the agent is nearest to along the spline.
        private float _projCurvature;

        // Current volume the agent is in.
        private NavVolume _projVolume;

        private static readonly ProfilerMarker ProjectMarker = new ProfilerMarker("SplinePath.ProjectPosition");

        #endregion

        #region Serialized Field Accessor Properties

        /// <summary>
        /// Scale to apply to spline tangents (lower values make the spline less curvy).
        /// </summary>
        public float TangentScale {
            get => _tangentScale;
            set => _tangentScale = value;
        }

        /// <summary>
        /// Whether to shorten tangents by raycasting to ensure they don't penetrate blocked areas.
        /// </summary>
        public bool RaycastTangents {
            get => _raycastTangents;
            set => _raycastTangents = value;
        }

        /// <summary>
        /// How many samples to take per segment of the spline when mapping the distance.
        /// </summary>
        public int DistanceSamplesPerSegment {
            get => _distanceSamplesPerSegment;
            set => _distanceSamplesPerSegment = value;
        }

        /// <summary>
        /// If <see cref="NavAgent.DebugPath"/> is enabled, how many points to use to draw the curve.
        /// </summary>
        public int DebugPointCount {
            get => _debugPointCount;
            set => _debugPointCount = value;
        }

        /// <summary>
        /// At what distance from the spline the agent will have all its desired velocity devoted to returning.
        /// </summary>
        public float MaxAlignmentVelocityDistance {
            get => _maxAlignmentVelocityDistance;
            set => _maxAlignmentVelocityDistance = value;
        }

        /// <summary>
        /// Delta-T to use when sampling curvature (should be quite small).
        /// </summary>
        public float CurvatureSampleDistance {
            get => _curvatureSampleDistance;
            set => _curvatureSampleDistance = value;
        }

        /// <summary>
        /// At what curvature value is the agent at its max curvature slowdown.
        /// </summary>
        public float CurvatureOfMaxSlowdown {
            get => _curvatureOfMaxSlowdown;
            set => _curvatureOfMaxSlowdown = value;
        }

        /// <summary>
        /// The multiplier on desired tangent velocity when at the max curvature value.
        /// </summary>
        public float MaxCurvatureSlowdown {
            get => _maxCurvatureSlowdown;
            set => _maxCurvatureSlowdown = value;
        }

        /// <summary>
        /// Whether to draw debug lines when projecting on the spline.
        /// </summary>
        public bool DebugProjectOnSpline {
            get => _debugProjectOnSpline;
            set => _debugProjectOnSpline = value;
        }

        /// <summary>
        /// Distance in front of the agent to check to see if it needs to avoid level geometry.
        /// </summary>
        public float BlockedDetectionDistance {
            get => _blockedDetectionDistance;
            set => _blockedDetectionDistance = value;
        }

        /// <summary>
        /// Distance behind the agent to check to see if it needs to avoid level geometry.
        /// </summary>
        public float BlockedDetectionBackDistance {
            get => _blockedDetectionBackDistance;
            set => _blockedDetectionBackDistance = value;
        }

        /// <summary>
        /// How far the agent must be from the spline to check for blocking level geometry.
        /// </summary>
        public float BlockedDetectionMinSplineDistance {
            get => _blockedDetectionMinSplineDistance;
            set => _blockedDetectionMinSplineDistance = value;
        }

        /// <summary>
        /// Whether to use physics queries to detect blocking level geometry.
        /// </summary>
        public bool BlockedDetectionPhysicsQuery {
            get => _blockedDetectionPhysicsQuery;
            set => _blockedDetectionPhysicsQuery = value;
        }

        /// <summary>
        /// Rigidbody to use for physics queries to detect blocking level geometry.
        /// </summary>
        public Rigidbody BlockedDetectionPhysicsQueryRigidbody {
            get => _blockedDetectionPhysicsQueryRigidbody;
            set => _blockedDetectionPhysicsQueryRigidbody = value;
        }

        #endregion

        #region Additional Properties

        /// <summary>
        /// Whether the agent is currently traveling along a spline.
        /// </summary>
        public bool IsOnSpline { get; private set; }

        /// <summary>
        /// The spline that the agent is currently following.
        /// </summary>
        public SplinePath SplinePath => _splinePath;

        /// <summary>
        /// The spline parameter value the agent is nearest to on the spline.
        /// </summary>
        public float CurrentSplineParameter => _projParam;

        /// <summary>
        /// The distance along the spline the agent is nearest to.
        /// </summary>
        public float CurrentSplineDistance => _splinePath.GetDistance(_projParam);

        /// <summary>
        /// The length of the agent's current spline path.
        /// </summary>
        public float MaxSplineDistance => _splinePath.Length;

        /// <summary>
        /// The remaining distance along the path after the end of the current spline.
        /// </summary>
        public float RemainingDistanceAfterSpline { get; private set; }

        /// <summary>
        /// The index of the end of the current spline in the current path.
        /// </summary>
        public int PathIndexOfSplineEnd { get; private set; }

        #endregion

        #region Unity Callbacks

        /// <summary>
        /// Updates measured velocity and finds the nearest point on the spline.
        /// </summary>
        protected override void Update() {
            base.Update();

            if (!IsOnSpline) return;

            // This calculation could get a bit expensive, profiler marker here to keep an eye on it.
            using (ProjectMarker.Auto()) {
                _projParam = _splinePath.ProjectPosition(PositionForVolume, debug: _debugProjectOnSpline);
            }

            // Knowing the parameter on the spline, cache the position, tangent, and curvature.
            _projPosition = _splinePath.GetPosition(_projParam);
            _projTangent = _splinePath.GetTangent(_projParam).normalized;
            _projCurvature = GetCurvatureValue(_projParam);
            _projVolume = _splinePath.GetVolume(_projParam);

            // Proj param ranges from zero to 1.
            // Upon reaching the end, it should be exactly 1.
            if (_projParam >= 1.0f - (0.02f / (SplinePath.PointCount - 1))) {
                CurrentPathIndex = PathIndexOfSplineEnd + 1;
            } else {
                float distanceFromSplineStart = _splinePath.GetDistance(_projParam);
                RemainingDistanceToNextWaypoint = _splinePath.Length - distanceFromSplineStart;
                RemainingDistance = RemainingDistanceToNextWaypoint + RemainingDistanceAfterSpline;
                DistanceFromStart = _pathDistanceAtSplineStart + distanceFromSplineStart;
            }
        }

        /// <summary>
        /// Draws the current spline if <see cref="NavAgent.DebugPath"/> is true.
        /// </summary>
        /// <remarks>
        /// The spline will be drawn with <see cref="DebugPointCount"/> points.
        /// </remarks>
        protected override void OnDrawGizmos() {
            base.OnDrawGizmos();
            if (!DebugPath || !IsOnSpline) return;

            Vector3 lastPosition = Vector3.zero;

            Color color = Gizmos.color;

            for (int i = 0; i < _splinePath.PointCount; i++) {
                Vector3 point = _splinePath.GetControlPosition(i);
                Vector3 tan = _splinePath.GetControlTangent(i);

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(point, point + tan);
                Gizmos.DrawLine(point, point - tan);
            }

            float iMult = _splinePath.Length / (_debugPointCount - 1);
            for (int i = 0; i < _debugPointCount; i++) {
                float param = _splinePath.GetParameter(i * iMult);
                Vector3 curPosition = _splinePath.GetPosition(param);
                float curCurvature = GetCurvatureValue(param);
                Gizmos.color = Color.Lerp(Color.green, Color.red, curCurvature);

                if (i > 0) {
                    Gizmos.DrawLine(lastPosition, curPosition);
                }

                lastPosition = curPosition;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(PositionForVolume, _projPosition);

            Gizmos.color = color;
        }

        #endregion

        #region Internal Methods

        // Sample the curvature at a point along the spline,
        // and scale it to a range of 0 to 1 based on the _curvatureOfMaxSlowdown.
        private float GetCurvatureValue(float param) {
            float value = _splinePath.GetCurvature(param, _curvatureSampleDistance).magnitude;
            return Mathf.Clamp01(value / _curvatureOfMaxSlowdown);
        }


        /// <inheritdoc/>
        protected override Vector3 CalculateDesiredNavigationVelocity() {
            if (Arrived || !IsOnSpline) {
                return base.CalculateDesiredNavigationVelocity();
            }

            // Determine tangent velocity - by default this is the same as the tangent wherever the agent is.
            Vector3 tangentVelocity = _projTangent;

            // If agent is within stopping distance of the destination, slow to a stop.
            Vector3 posForVolume = PositionForVolume;
            if (AccelerationEstimate > 0) {
                Vector3 toDest = CurrentPath.Waypoints[PathIndexOfSplineEnd].Position - posForVolume;
                float stoppingDistance = StoppingDistance;
                if (toDest.sqrMagnitude < stoppingDistance * stoppingDistance) {
                    tangentVelocity = Vector3.zero;
                }
            }

            // Also slow down based on current curvature.
            tangentVelocity *= Mathf.Lerp(1, _maxCurvatureSlowdown, _projCurvature);

            Vector3 toSpline = _projPosition - posForVolume;

            // Determine aligning velocity, which points towards the nearest point on the spline.
            // Its magnitude scales from 0 to 1 based on distance / _maxAlignmentVelocityDistance.
            Vector3 aligningVelocity =
                Vector3.ClampMagnitude(toSpline / _maxAlignmentVelocityDistance, 1);

            if (_blockedDetectionMinSplineDistance > 0 && toSpline.sqrMagnitude >
                _blockedDetectionMinSplineDistance * _blockedDetectionMinSplineDistance) {
                Vector3 startOffset = -_projTangent * _blockedDetectionBackDistance;
                Vector3 endOffset = _projTangent * _blockedDetectionDistance;

                Vector3 lineStart = posForVolume + startOffset;
                Vector3 lineEnd = posForVolume + endOffset;
                if (DebugPath) Debug.DrawLine(lineStart, lineEnd, Color.magenta);
                if (_projVolume != null && _projVolume.Raycast(lineStart, lineEnd)) {
                    tangentVelocity = Vector3.zero;
                }

                if (_blockedDetectionPhysicsQuery && _blockedDetectionPhysicsQueryRigidbody != null &&
                    _blockedDetectionPhysicsQueryRigidbody.SweepTest(_projTangent, out RaycastHit hit,
                                                                     _blockedDetectionDistance,
                                                                     QueryTriggerInteraction.Ignore)) {
                    tangentVelocity = Vector3.zero;
                }
            }

            // In order to cap the desired velocity's magnitude at one,
            // must make sure tangentVelocity is not too great.

            // Since the vectors are roughly perpendicular, use the pythagorean theorem A^2 + B^2 = C^2,
            // where A = tangentVelocity.magnitude, B = aligningVelocity.magnitude, C = 1.

            // This means the sum of the square magnitudes of the two velocities must be <= 1.
            // Or, since B will not change, A^2 must be less than 1 - B^2.

            // (1 - B^2) is clamped here to avoid floating point imprecision leading to a tiny negative number,
            // which would cause the sqrt to return NaN.
            float tangentMaxSqrMag = Mathf.Clamp01(1.0f - aligningVelocity.sqrMagnitude);
            float tangentSqrMag = tangentVelocity.sqrMagnitude;

            // If A^2 > 1 - B^2, multiply it by the sqrt((1 - B^2) / A^2).
            // This makes A^2's magnitude exactly 1 - B^2, meaning the total magnitude is 1.
            if (tangentSqrMag > tangentMaxSqrMag) {
                tangentVelocity *= Mathf.Sqrt(tangentMaxSqrMag / tangentSqrMag);
            }

            // Can add the velocities now that their combined magnitude is <= 1.
            return (tangentVelocity + aligningVelocity) * DesiredSpeedRatio;
        }

        protected override void OnPathIndexChanged(int newIndex) {
            base.OnPathIndexChanged(newIndex);

            if (CurrentPath != null && newIndex >= 0 && newIndex < CurrentPath.Waypoints.Count) {
                UpdateSpline();
            } else {
                ClearSpline();
            }
        }

        protected override void OnCurrentPathChanged(NavPath newPath) {
            base.OnCurrentPathChanged(newPath);

            ClearSpline();
        }

        protected virtual void ClearSpline() {
            _splinePath.Dispose();
            _splinePath = default;
            _pathDistanceAtSplineStart = 0;
            PathIndexOfSplineEnd = -1;
            IsOnSpline = false;
        }

        protected virtual void UpdateSpline() {
            int firstPoint = Mathf.Max(0, CurrentPathIndex - 1);

            if (CurrentPath.Waypoints[firstPoint].Type is not
                (NavWaypointType.EnterVolume or NavWaypointType.InsideVolume)) {
                ClearSpline();
                return;
            }

            if (IsOnSpline) return;

            int lastPoint = firstPoint;
            while (lastPoint < CurrentPath.Waypoints.Count - 1 &&
                   CurrentPath.Waypoints[lastPoint + 1].AreaType == NavAreaTypes.Volume) {
                lastPoint++;
            }

            int segmentCount = lastPoint - firstPoint;
            if (lastPoint - firstPoint < 2) {
                ClearSpline();
            } else {
                int sampleCount = _distanceSamplesPerSegment * segmentCount;
                _splinePath.Initialize(CurrentPath, firstPoint, lastPoint, _tangentScale, sampleCount,
                                       _raycastTangents);

                if (_splinePath.PointCount == 0) {
                    ClearSpline();
                    return;
                }

                _pathDistanceAtSplineStart = CurrentPath.Waypoints[firstPoint].AccumulatedDistance;
                IsOnSpline = true;
                RemainingDistanceAfterSpline = CurrentPath.Waypoints[^1].AccumulatedDistance -
                                               CurrentPath.Waypoints[lastPoint].AccumulatedDistance;
                PathIndexOfSplineEnd = lastPoint;
                CurrentPathIndex = lastPoint;
            }
        }

        /// <inheritdoc/>
        public override void Stop(bool abortPaths) {
            base.Stop(abortPaths);

            ClearSpline();
        }

        /// <inheritdoc/>
        protected override void OnDisable() {
            base.OnDisable();

            ClearSpline();
        }

        #endregion
    }
}
