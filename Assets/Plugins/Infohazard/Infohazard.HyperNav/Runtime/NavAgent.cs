// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav {
    /// <summary>
    /// A script that can be used to calculate paths by any entity that needs to use HyperNav for navigation.
    /// </summary>
    /// <remarks>
    /// While a NavAgent is not necessary to use HyperNav, it makes pathfinding easier.
    /// The NavAgent does not impose any restrictions on how movement occurs, nor does it actually perform any movement.
    /// It simply provides a desired movement velocity, which other scripts on the object are responsible
    /// for using however they need.
    /// <p/>
    /// The agent can have one active path (the path it is currently following),
    /// but can have multiple pending paths (paths in the process of being calculated by a NavPathfinder).
    /// <p/>
    /// If you desire smoother movement then what the NavAgent provides, see <see cref="SplineNavAgent"/>.
    /// </remarks>
    public class NavAgent : MonoBehaviour {
        #region Serialized Fields

        [SerializeField]
        [Tooltip("The radius to search when finding the nearest NavVolume.")]
        private float _sampleRadius = 2;

        [SerializeField]
        [Tooltip("The types of areas that can be traversed.")]
        private NavAreaTypes _areaTypeMask = NavAreaTypes.All;

        [FormerlySerializedAs("_layerMask"), SerializeField]
        [Tooltip("The layers of areas that can be traversed.")]
        private NavLayerMask _areaLayerMask = NavLayerMask.All;

        [SerializeField]
        [Tooltip("How to prioritize results for the start position when sampling in both volumes and surfaces.")]
        private NavSamplePriority _startSamplingPriority = NavSamplePriority.Nearest;

        [SerializeField]
        [Tooltip("How to prioritize results for the destination position when sampling in both volumes and surfaces.")]
        private NavSamplePriority _destSamplingPriority = NavSamplePriority.Nearest;

        [SerializeField]
        [Tooltip(
            "The transform to use for sampling the agent's position in volumes. If null, the agent's transform is used.")]
        private Transform _volumeSamplingTransform;

        [SerializeField]
        [Tooltip(
            "The transform to use for sampling the agent's position on surfaces. If null, the agent's transform is used.")]
        private Transform _surfaceSamplingTransform;

        [SerializeField]
        [Tooltip(
            "If true, check that the surface up vector is within a certain dot product of the sampling up vector.")]
        [HelpBox(
            "If true, check that the surface up vector is within a certain dot product of the sampling up vector. " +
            "The reference vector will be the up vector of either SurfaceSamplingTransform if it is set, " +
            "or the agent's transform otherwise.")]
        private bool _checkSurfaceUpVector = false;

        [SerializeField]
        [Tooltip(
            "The minimum dot product between the surface up vector and the sampling up vector to consider a surface valid.")]
        [HelpBox(
            "The minimum dot product between the surface up vector and the sampling up vector to consider a surface valid. " +
            "This is used to ensure that the surface is not too steep or upside down. " +
            "0.7 is approximately 45 degrees, and -1 would allow any surface.")]
        private float _minSurfaceUpVectorDot = 0.7f; // 0.7 is approximately 45 degrees

        [Min(0)]
        [SerializeField]
        [Tooltip("Additional cost to changing from a surface to a volume.")]
        private float _costToChangeToVolume = 0;

        [Min(0)]
        [SerializeField]
        [Tooltip("Additional cost to changing from a volume to a surface.")]
        private float _costToChangeToSurface = 0;

        [Min(1)]
        [SerializeField]
        [Tooltip("Multiplier for the cost of traversing volume areas.")]
        private float _volumeCostMultiplier = 1;

        [Min(1)]
        [SerializeField]
        [Tooltip("Multiplier for the cost of traversing surface areas.")]
        private float _surfaceCostMultiplier = 1;

        [SerializeField]
        [Tooltip("How close the agent must get to a destination before it is considered to have arrived.")]
        private float _acceptance = 1;

        [SerializeField]
        [Tooltip("This should be set to the maximum acceleration of your agent (can be set dynamically as well).")]
        private float _accelerationEstimate = 0;

        [SerializeField]
        [Tooltip("Optional rigidbody to use for measuring velocity.")]
        private Rigidbody _rigidbody;

        [SerializeField]
        [Tooltip("The desired fraction of the maximum speed to travel at.")]
        [Range(0, 1)]
        private float _desiredSpeedRatio = 1;

        [SerializeField]
        [Tooltip("Whether to draw a debug line in the scene view showing the agent's current path.")]
        private bool _debugPath = true;

        [SerializeField]
        [Tooltip("Whether to keep following the current path while waiting for a new path to finish calculating.")]
        private bool _keepPathWhileCalculating = true;

        [SerializeField]
        [Tooltip("Whether to check during path following if the agent should skip to the next waypoint.")]
        private bool _checkSkippingWithPhysicsQuery = false;

        [SerializeField]
        [Tooltip("Whether to check during path following if the agent should go back to the previous waypoint.")]
        private bool _checkBacktrackWithPhysicsQuery = false;

        [SerializeField]
        [Tooltip("Layer mask to use for physics queries to check for obstacles when skipping waypoints.")]
        private LayerMask _skippingCheckLayerMask = -1;

        [SerializeField]
        [Tooltip("Collider to determine the query parameters for sweep testing.")]
        [HelpBox("A collider to determine the query parameters for sweep testing. " +
                 "The layer does not matter and it does not need to be enabled. " +
                 "However, it must be of type Box, Capsule, or Sphere, as Unity does not support Mesh casts.")]
        private Collider _skippingCheckCollider;

        [SerializeField]
        [Tooltip("Padding to reduce the size of the collider used for skipping waypoint checks.")]
        private float _skippingCheckColliderPadding = 0.1f;

        [SerializeField]
        [Tooltip("If true, only consider static colliders when checking for skipping waypoints.")]
        private bool _skippingCheckStaticOnly = false;

        [SerializeField]
        [Tooltip("If true, only consider static colliders when checking for backtracking waypoints.")]
        private bool _backtrackCheckStaticOnly = true;

        [SerializeField]
        [Tooltip(nameof(AvoidanceAgent) + " that this agent uses for avoidance (can be null).")]
        private AvoidanceAgent _avoidanceAgent;

        [SerializeField]
        [Tooltip(
            "If true, the IsActive state of the AvoidanceAgent is set based on whether there is a current valid path.")]
        private bool _controlAvoidanceIsActive = true;

        /// <summary>
        /// This is used to refer to the names of private fields in this class from a custom Editor.
        /// </summary>
        public static class PropNames {
            public const string SampleRadius = nameof(_sampleRadius);
            public const string AreaTypeMask = nameof(_areaTypeMask);
            public const string AreaLayerMask = nameof(_areaLayerMask);
            public const string StartSamplingPriority = nameof(_startSamplingPriority);
            public const string DestSamplingPriority = nameof(_destSamplingPriority);
            public const string VolumeSamplingTransform = nameof(_volumeSamplingTransform);
            public const string SurfaceSamplingTransform = nameof(_surfaceSamplingTransform);
            public const string CheckSurfaceUpVector = nameof(_checkSurfaceUpVector);
            public const string MinSurfaceUpVectorDot = nameof(_minSurfaceUpVectorDot);
            public const string CostToChangeToVolume = nameof(_costToChangeToVolume);
            public const string CostToChangeToSurface = nameof(_costToChangeToSurface);
            public const string VolumeCostMultiplier = nameof(_volumeCostMultiplier);
            public const string SurfaceCostMultiplier = nameof(_surfaceCostMultiplier);
            public const string Acceptance = nameof(_acceptance);
            public const string AccelerationEstimate = nameof(_accelerationEstimate);
            public const string Rigidbody = nameof(_rigidbody);
            public const string DesiredSpeedRatio = nameof(_desiredSpeedRatio);
            public const string DebugPath = nameof(_debugPath);
            public const string KeepPathWhileCalculating = nameof(_keepPathWhileCalculating);
            public const string AvoidanceAgent = nameof(_avoidanceAgent);
            public const string ControlAvoidanceIsActive = nameof(_controlAvoidanceIsActive);
            public const string CheckSkippingWithPhysicsQuery = nameof(_checkSkippingWithPhysicsQuery);
            public const string CheckBacktrackWithPhysicsQuery = nameof(_checkBacktrackWithPhysicsQuery);
            public const string SkippingCheckLayerMask = nameof(_skippingCheckLayerMask);
            public const string SkippingCheckCollider = nameof(_skippingCheckCollider);
            public const string SkippingCheckColliderPadding = nameof(_skippingCheckColliderPadding);
            public const string SkippingCheckStaticOnly = nameof(_skippingCheckStaticOnly);
            public const string BacktrackCheckStaticOnly = nameof(_backtrackCheckStaticOnly);
        }

        #endregion

        #region Private Fields

        // Pending path requests in order from oldest to newest.
        private readonly List<long> _pendingPathIDs = new List<long>();

        // Current destination of the agent.
        private NavSampleResult _destination;

        // Position at the previous frame, which is used for measuring velocity.
        private Vector3 _positionLastFrame;

        // The current path that the agent is following.
        private NavPath _currentPath;

        // Whether the agent has been initialized.
        private bool _isInitialized;

        private bool _isUpdatingPath;
        private long _currentPathID = -1;
        private int _currentPathIndex = -1;

        private static RaycastHit[] _colliderCastHits = new RaycastHit[64];

        #endregion

        #region Events

        /// <summary>
        /// Invoked when the agent finds a path to the destination.
        /// </summary>
        public event Action PathReady;

        /// <summary>
        /// Invoked when the agent fails to find a path to the destination.
        /// </summary>
        public event Action PathFailed;

        /// <summary>
        /// Invoked when the agent's path is updated.
        /// </summary>
        public event Action<NavPath> CurrentPathChanged;

        /// <summary>
        /// Invoked when the agent's path index changes.
        /// </summary>
        public event Action<int> PathIndexChanged;

        #endregion

        #region Serialized Field Accessor Properties

        /// <summary>
        /// How close the agent must get to a destination before it is considered to have arrived.
        /// </summary>
        /// <remarks>
        /// Note that setting acceptance too low may prevent the agent from ever stopping,
        /// but setting it to high can make the agent stop too far from the destination.
        /// </remarks>
        public float Acceptance {
            get => _acceptance;
            set => _acceptance = value;
        }

        /// <summary>
        /// This should be set to the maximum acceleration of your agent.
        /// </summary>
        /// <remarks>
        /// This is used to determine when the agent needs to start slowing down when approaching its destination.
        /// </remarks>
        public float AccelerationEstimate {
            get => _accelerationEstimate;
            set => _accelerationEstimate = value;
        }

        /// <summary>
        /// Optional rigidbody to use for measuring velocity.
        /// </summary>
        public Rigidbody Rigidbody {
            get => _rigidbody;
            set => _rigidbody = value;
        }

        /// <summary>
        /// The radius to search when finding the nearest NavVolume.
        /// </summary>
        public float SampleRadius {
            get => _sampleRadius;
            set => _sampleRadius = value;
        }

        /// <summary>
        /// The types of areas that the agent can traverse.
        /// </summary>
        public NavAreaTypes AreaTypeMask {
            get => _areaTypeMask;
            set => _areaTypeMask = value;
        }

        /// <summary>
        /// The layers of areas that the agent can traverse.
        /// </summary>
        public NavLayerMask AreaLayerMask {
            get => _areaLayerMask;
            set => _areaLayerMask = value;
        }

        /// <summary>
        /// How to prioritize results for the destination position when sampling in both volumes and surfaces.
        /// </summary>
        public NavSamplePriority StartSamplingPriority {
            get => _startSamplingPriority;
            set => _startSamplingPriority = value;
        }

        /// <summary>
        /// How to prioritize results for the destination position when sampling in both volumes and surfaces.
        /// </summary>
        public NavSamplePriority DestSamplingPriority {
            get => _destSamplingPriority;
            set => _destSamplingPriority = value;
        }

        /// <summary>
        /// The transform to use for sampling the agent's position in volumes.
        /// </summary>
        public Transform VolumeSamplingTransform {
            get => _volumeSamplingTransform;
            set => _volumeSamplingTransform = value;
        }

        /// <summary>
        /// The transform to use for sampling the agent's position on surfaces.
        /// </summary>
        public Transform SurfaceSamplingTransform {
            get => _surfaceSamplingTransform;
            set => _surfaceSamplingTransform = value;
        }

        /// <summary>
        /// Whether to check that the surface up vector is within a certain dot product of the sampling up vector.
        /// The reference vector will be the up vector of either <see cref="SurfaceSamplingTransform"/> if it is set,
        /// or the agent's transform otherwise.
        /// </summary>
        public bool CheckSurfaceUpVector {
            get => _checkSurfaceUpVector;
            set => _checkSurfaceUpVector = value;
        }

        /// <summary>
        /// The minimum dot product between the surface up vector and the sampling up vector to consider a surface valid.
        /// </summary>
        public float MinSurfaceUpVectorDot {
            get => _minSurfaceUpVectorDot;
            set => _minSurfaceUpVectorDot = value;
        }

        /// <summary>
        /// Additional cost to changing from a surface to a volume.
        /// </summary>
        public float CostToChangeToVolume {
            get => _costToChangeToVolume;
            set => _costToChangeToVolume = value;
        }

        /// <summary>
        /// Additional cost to changing from a volume to a surface.
        /// </summary>
        public float CostToChangeToSurface {
            get => _costToChangeToSurface;
            set => _costToChangeToSurface = value;
        }

        /// <summary>
        /// Multiplier for the cost of traversing volume areas.
        /// </summary>
        public float VolumeCostMultiplier {
            get => _volumeCostMultiplier;
            set => _volumeCostMultiplier = value;
        }

        /// <summary>
        /// Multiplier for the cost of traversing surface areas.
        /// </summary>
        public float SurfaceCostMultiplier {
            get => _surfaceCostMultiplier;
            set => _surfaceCostMultiplier = value;
        }

        /// <summary>
        /// Pathfinding parameters as a combined struct.
        /// </summary>
        public NavPathfindingParams PathfindingParams {
            get => new() {
                AreaTypeMask = _areaTypeMask,
                LayerMask = _areaLayerMask,
                CostToChangeToVolume = _costToChangeToVolume,
                CostToChangeToSurface = _costToChangeToSurface,
                VolumeCostMultiplier = _volumeCostMultiplier,
                SurfaceCostMultiplier = _surfaceCostMultiplier
            };
            set {
                _areaTypeMask = value.AreaTypeMask;
                _areaLayerMask = value.LayerMask;
                _costToChangeToVolume = value.CostToChangeToVolume;
                _costToChangeToSurface = value.CostToChangeToSurface;
                _volumeCostMultiplier = value.VolumeCostMultiplier;
                _surfaceCostMultiplier = value.SurfaceCostMultiplier;
            }
        }

        /// <summary>
        /// The desired fraction of the maximum speed to travel at.
        /// </summary>
        public float DesiredSpeedRatio {
            get => _desiredSpeedRatio;
            set => _desiredSpeedRatio = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Whether to draw a debug line in the scene view showing the agent's current path.
        /// </summary>
        public bool DebugPath {
            get => _debugPath;
            set => _debugPath = value;
        }

        /// <summary>
        /// Whether to keep following the current path while waiting for a new path to finish calculating.
        /// </summary>
        /// <remarks>
        /// If true, there can be two pending paths at the same time - the most and least recently requested ones.
        /// This ensures that even when the agent is receiving pathfinding requests faster than they can be calculated,
        /// they will still finish and the agent will not be deadlocked and unable to ever complete a path.
        /// </remarks>
        public bool KeepPathWhileCalculating {
            get => _keepPathWhileCalculating;
            set => _keepPathWhileCalculating = value;
        }

        /// <summary>
        /// <see cref="AvoidanceAgent"/> that this agent uses for avoidance (can be null).
        /// </summary>
        public AvoidanceAgent AvoidanceAgent {
            get => _avoidanceAgent;
            set => _avoidanceAgent = value;
        }

        /// <summary>
        /// If true, the <see cref="Infohazard.HyperNav.AvoidanceAgent.IsActive"/> state of the
        /// <see cref="AvoidanceAgent"/> is set based on whether there is a current valid path.
        /// </summary>
        public bool ControlAvoidanceIsActive {
            get => _controlAvoidanceIsActive;
            set => _controlAvoidanceIsActive = value;
        }

        /// <summary>
        /// Whether to check during path following if the agent should skip to the next waypoint.
        /// </summary>
        public bool CheckSkippingWithPhysicsQuery {
            get => _checkSkippingWithPhysicsQuery;
            set => _checkSkippingWithPhysicsQuery = value;
        }

        /// <summary>
        /// Whether to check during path following if the agent should go back to the previous waypoint.
        /// </summary>
        public bool CheckBacktrackWithPhysicsQuery {
            get => _checkBacktrackWithPhysicsQuery;
            set => _checkBacktrackWithPhysicsQuery = value;
        }

        /// <summary>
        /// Layer mask to use for physics queries to check for obstacles when skipping waypoints.
        /// </summary>
        public LayerMask SkippingCheckLayerMask {
            get => _skippingCheckLayerMask;
            set => _skippingCheckLayerMask = value;
        }

        /// <summary>
        /// Collider to determine the query parameters for sweep testing.
        /// </summary>
        /// <remarks>
        /// The layer does not matter and it does not need to be enabled.
        /// However, it must be of type Box, Capsule, or Sphere, as Unity does not support Mesh casts.
        /// </remarks>
        public Collider SkippingCheckCollider {
            get => _skippingCheckCollider;
            set => _skippingCheckCollider = value;
        }

        /// <summary>
        /// Padding to reduce the size of the collider used for skipping waypoint checks.
        /// </summary>
        public float SkippingCheckColliderPadding {
            get => _skippingCheckColliderPadding;
            set => _skippingCheckColliderPadding = value;
        }

        /// <summary>
        /// Whether to only consider static colliders when checking for skipping waypoints.
        /// </summary>
        public bool SkippingCheckStaticOnly {
            get => _skippingCheckStaticOnly;
            set => _skippingCheckStaticOnly = value;
        }

        /// <summary>
        /// Whether to only consider static colliders when checking for backtracking waypoints.
        /// </summary>
        public bool BacktrackCheckStaticOnly {
            get => _backtrackCheckStaticOnly;
            set => _backtrackCheckStaticOnly = value;
        }

        #endregion

        #region Additional Properties

        public Vector3 DesiredVelocity {
            get {
                if (CurrentPath == null) return Vector3.zero;
                if (_avoidanceAgent) return _avoidanceAgent.NormalizedAvoidanceVelocity;
                return CalculateDesiredNavigationVelocity();
            }
        }

        /// <summary>
        /// Whether a path is currently in the process of being calculated for this agent.
        /// </summary>
        public bool IsPathPending => _pendingPathIDs.Count > 0;

        // Which point in the path the agent is at.
        public int CurrentPathIndex {
            get => _currentPathIndex;
            protected set {
                if (_currentPathIndex == value) return;
                _currentPathIndex = value;
                PathIndexChanged?.Invoke(_currentPathIndex);
                OnPathIndexChanged(_currentPathIndex);
            }
        }

        /// <summary>
        /// The distance that it will take the agent to come to a stop from its current velocity,
        /// determined using the <see cref="AccelerationEstimate"/>.
        /// </summary>
        public virtual float StoppingDistance => MeasuredVelocity.sqrMagnitude / (2 * _accelerationEstimate);

        /// <summary>
        /// The current path waypoint that the agent is trying to move towards.
        /// </summary>
        /// <remarks>
        /// If there is no active path, will return the agent's current position.
        /// </remarks>
        public Vector3 NextWaypointPosition => NextWaypoint?.Position ?? transform.position;

        /// <summary>
        /// The position to use for sampling volumes.
        /// </summary>
        public Vector3 PositionForVolume =>
            _volumeSamplingTransform ? _volumeSamplingTransform.position : transform.position;

        /// <summary>
        /// The position to use for sampling surfaces.
        /// </summary>
        public Vector3 PositionForSurface =>
            _surfaceSamplingTransform ? _surfaceSamplingTransform.position : transform.position;

        public Vector3 ReferenceUpVectorForSurface =>
            _surfaceSamplingTransform ? _surfaceSamplingTransform.up : transform.up;

        public Transform TransformForNextWaypoint {
            get {
                NavWaypoint? next = NextWaypoint;
                if (!next.HasValue) return transform;

                NavWaypointType type = next.Value.Type;

                return type switch {
                    NavWaypointType.EnterSurface or NavWaypointType.InsideSurface or NavWaypointType.ExitSurface
                        when _surfaceSamplingTransform != null =>
                        _surfaceSamplingTransform,
                    NavWaypointType.EnterVolume or NavWaypointType.InsideVolume or NavWaypointType.ExitVolume
                        when _volumeSamplingTransform != null =>
                        _volumeSamplingTransform,
                    _ => transform,
                };
            }
        }

        public Vector3 CurrentPositionForNextWaypoint => TransformForNextWaypoint.position;

        public bool IsNavigatingOnSurface =>
            NextWaypoint is { IsSurface: true } && PreviousWaypoint is not { IsSurface: false };

        public bool IsNavigatingInVolume =>
            NextWaypoint is { IsVolume: true } && PreviousWaypoint is not { IsVolume: true };

        /// <summary>
        /// The current path waypoint that the agent is trying to move towards.
        /// </summary>
        public NavWaypoint? NextWaypoint {
            get {
                if (CurrentPath != null && CurrentPathIndex < CurrentPath.Waypoints.Count) {
                    return CurrentPath.Waypoints[CurrentPathIndex];
                }

                return null;
            }
        }

        /// <summary>
        /// The previous waypoint in the path.
        /// </summary>
        public NavWaypoint? PreviousWaypoint {
            get {
                if (CurrentPath != null && CurrentPathIndex > 0) {
                    return CurrentPath.Waypoints[CurrentPathIndex - 1];
                }

                return null;
            }
        }

        /// <summary>
        /// True if the agent has no active or pending path.
        /// </summary>
        public bool Arrived { get; private set; }

        /// <summary>
        /// The remaining distance to the destination.
        /// </summary>
        public float RemainingDistance { get; protected set; }

        /// <summary>
        /// The remaining distance to the next waypoint.
        /// </summary>
        public float RemainingDistanceToNextWaypoint { get; protected set; }

        /// <summary>
        /// The distance the agent has traveled along the path.
        /// </summary>
        public float DistanceFromStart { get; protected set; }

        /// <summary>
        /// Get or set the agent's destination (the position it is trying to get to).
        /// </summary>
        /// <remarks>
        /// If set within the <see cref="_acceptance"/> radius of the current position, will abort all movement.
        /// </remarks>
        public Vector3 Destination {
            get => _destination.Position.xyz;
            set => UpdatePath(value);
        }

        /// <summary>
        /// Velocity of the agent measured as delta position / delta time over the last frame,
        /// which is used to determine stopping distance.
        /// </summary>
        /// <remarks>
        /// This value is calculated in <see cref="UpdateMeasuredVelocity"/>.
        /// You can override that method to implement your own logic for calculating velocity.
        /// </remarks>
        public Vector3 MeasuredVelocity { get; protected set; }

        /// <summary>
        /// The current path that the agent is following.
        /// </summary>
        public NavPath CurrentPath {
            get => _currentPath;
            set {
                if (!_isInitialized || !enabled) {
                    Debug.LogError("NavAgent.CurrentPath cannot be set before OnEnable is called, or when disabled.");
                    return;
                }

                if (_currentPath == value) return;

                _currentPath?.Dispose();
                _currentPath = value;
                if (_avoidanceAgent != null && _controlAvoidanceIsActive) {
                    _avoidanceAgent.IsActive = value != null;
                }

                Arrived = value == null;
                _currentPathIndex = value == null ? -1 : 0;
                CurrentPathChanged?.Invoke(_currentPath);
                OnCurrentPathChanged(_currentPath);
                PathIndexChanged?.Invoke(_currentPathIndex);
                OnPathIndexChanged(_currentPathIndex);
            }
        }

        /// <summary>
        /// Maximum speed possible by this agent when avoiding obstacles.
        /// </summary>
        public float AvoidanceMaxSpeed {
            get => _avoidanceAgent ? _avoidanceAgent.MaxSpeed : 0;
            set {
                if (_avoidanceAgent) _avoidanceAgent.MaxSpeed = value;
            }
        }

        /// <summary>
        /// An optional condition to check if the agent can advance past a given waypoint in the path.
        /// </summary>
        /// <remarks>
        /// A multicast delegate is not supported.
        /// </remarks>
        public NavAgentWaypointPredicate AdvancePredicate { get; set; }

        /// <summary>
        /// An optional condition to check if the agent can backtrack to the previous waypoint in the path.
        /// Will only be used if a physics query determines the current waypoint is unreachable.
        /// </summary>
        public NavAgentWaypointPredicate BacktrackPredicate { get; set; }

        #endregion

        #region Unity Callbacks

        /// <summary>
        /// Sets the <see cref="AvoidanceAgent"/>.<see cref="Infohazard.HyperNav.AvoidanceAgent.InputVelocityFunc"/>.
        /// </summary>
        protected virtual void Awake() {
            if (_avoidanceAgent) _avoidanceAgent.InputVelocityFunc = CalculateDesiredNavigationVelocityTimesMaxSpeed;
        }

        /// <summary>
        /// Resets <see cref="MeasuredVelocity"/> and sets <see cref="Arrived"/> to true.
        /// </summary>
        protected virtual void OnEnable() {
            _positionLastFrame = transform.position;
            MeasuredVelocity = Vector3.zero;
            Arrived = true;
            _isInitialized = true;
        }

        /// <summary>
        /// Stops all pathfinding and cancels path requests.
        /// </summary>
        protected virtual void OnDisable() {
            Stop(true);
        }

        /// <summary>
        /// Updates measured velocity and current index in path.
        /// </summary>
        protected virtual void Update() {
            UpdateMeasuredVelocity();
            UpdatePathIndex(false);
        }

        /// <summary>
        /// Draws the current path as a sequence of debug lines if <see cref="DebugPath"/> is true.
        /// </summary>
        protected virtual void OnDrawGizmos() {
            Color c = Gizmos.color;

            if (!_debugPath || CurrentPath == null) return;

            Gizmos.color = Color.magenta;

            Vector3 currentPos = CurrentPositionForNextWaypoint;
            Vector3 next = NextWaypointPosition;
            NavWaypoint? prev = null;
            foreach (NavWaypoint waypoint in CurrentPath.Waypoints) {
                if (prev.HasValue) {
                    Gizmos.DrawLine(prev.Value.Position, waypoint.Position);
                }

                prev = waypoint;
            }

            Gizmos.DrawLine(currentPos, next);

            Gizmos.color = c;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Stop following the current path, and optionally cancel all path requests.
        /// </summary>
        /// <param name="abortPaths">Whether to cancel pending path requests.</param>
        public virtual void Stop(bool abortPaths) {
            _currentPath?.Dispose();
            _currentPath = null;
            CurrentPathIndex = -1;
            CurrentPathChanged?.Invoke(null);
            OnCurrentPathChanged(null);
            PathIndexChanged?.Invoke(_currentPathIndex);
            OnPathIndexChanged(_currentPathIndex);

            if (_avoidanceAgent) _avoidanceAgent.IsActive = false;
            Arrived = true;
            RemainingDistance = 0;
            RemainingDistanceToNextWaypoint = 0;
            DistanceFromStart = 0;

            if (abortPaths) {
                _destination = default;
                foreach (long pathID in _pendingPathIDs) {
                    // Don't log errors here to avoid error during shutdown.
                    NavPathfinder.MainInstance.CancelPath(pathID, false);
                }

                _pendingPathIDs.Clear();
            }
        }

        /// <summary>
        /// Request a new path between two <see cref="NavSampleResult"/>s.
        /// </summary>
        /// <remarks>
        /// This method is similar to setting <see cref="Destination"/>.
        /// However, you can use this if you want to supply your own query results.
        /// For example, you can run a single <see cref="NavSampleJob"/> for many pathfinding calls.
        /// </remarks>
        /// <param name="fromHit">Hit at the agent's current position.</param>
        /// <param name="toHit">Hit at the destination.</param>
        /// <returns>The result of the path update.</returns>
        public virtual UpdatePathResult SetDestinationByNavHit(NavSampleResult fromHit, NavSampleResult toHit) {
            _destination = toHit;
            return UpdatePath(fromHit, toHit);
        }

        /// <summary>
        /// Request a new path from the current position to the desired destination.
        /// This is equivalent to setting <see cref="Destination"/>, but returns a result.
        /// </summary>
        /// <remarks>
        /// It is usually not necessary to call this yourself, as it is called when setting <see cref="Destination"/>.
        /// However, if the agent gets stuck or pushed off course, you may wish to use this to get a new path.
        /// </remarks>
        /// <param name="destination">The position to move towards.</param>
        /// <returns>The result of the path update.</returns>
        public virtual UpdatePathResult UpdatePath(Vector3 destination) {
            if (!_isInitialized || !enabled) {
                Debug.LogError("NavAgent.Destination cannot be set before OnEnable is called, or when disabled.");
                return UpdatePathResult.Error;
            }

            // If we are within acceptance radius of destination, stop moving.
            if (Vector3.SqrMagnitude(destination - transform.position) < _acceptance * _acceptance) {
                Stop(true);
                return UpdatePathResult.Arrived;
            }

            if (!NavVolume.NativeDataMap.IsCreated && !NavSurface.NativeDataMap.IsCreated) {
                Debug.LogError("Trying to update NavAgent path without any areas loaded.");
                PathFailed?.Invoke();
                return UpdatePathResult.Failed;
            }

            Span<NavSampleQuery> samplePositions = stackalloc NavSampleQuery[2];
            Span<NavSampleResult> sampleResults = stackalloc NavSampleResult[2];

            samplePositions[0] = new NavSampleQuery(
                PositionForVolume,
                PositionForSurface,
                _sampleRadius,
                _sampleRadius,
                _areaTypeMask,
                _areaLayerMask,
                _startSamplingPriority,
                _checkSurfaceUpVector ? ReferenceUpVectorForSurface : Vector3.zero,
                _checkSurfaceUpVector ? _minSurfaceUpVectorDot : 0);
            samplePositions[1] = new NavSampleQuery(
                destination,
                _sampleRadius,
                _areaTypeMask,
                _areaLayerMask,
                _destSamplingPriority);

            NavSampleJob.SamplePositionsInAllAreas(samplePositions, sampleResults);

            NavSampleResult startHit = sampleResults[0];
            NavSampleResult endHit = sampleResults[1];

            _destination = endHit;

            // Ensure both current position and destination are within sample radius of a NavVolume.
            if (startHit.AreaID <= 0 || endHit.AreaID <= 0) {
                PathFailed?.Invoke();
                return UpdatePathResult.Failed;
            }

            return UpdatePath(startHit, endHit);
        }

        protected virtual UpdatePathResult UpdatePath(NavSampleResult startHit, NavSampleResult endHit) {
            // If KeepPathWhileCalculating is true, we can have two paths being calculated at the same time.
            int maxPendingIDs = _keepPathWhileCalculating ? 1 : 0;
            if (_pendingPathIDs.Count > maxPendingIDs) {
                // If there are too many pending paths, keep the oldest to ensure it gets to finished.
                // If the newest request was kept instead and requests came in faster than they could be calculated,
                // the agent would deadlock and never actually finish a path.
                for (int i = _pendingPathIDs.Count - 1; i >= maxPendingIDs; i--) {
                    NavPathfinder.MainInstance.CancelPath(_pendingPathIDs[i]);
                }

                _pendingPathIDs.RemoveRange(maxPendingIDs, _pendingPathIDs.Count - maxPendingIDs);
            }

            _isUpdatingPath = true;
            long id = NavPathfinder.MainInstance.FindPath(startHit, endHit, transform.position, Destination,
                                                          PathfindingParams, OnPathReady);
            _isUpdatingPath = false;

            // NavPathfinder returns a negative ID if pathfinding is not possible, such as if no volumes are loaded.
            if (id < 0) {
                PathFailed?.Invoke();
                return UpdatePathResult.Failed;
            }

            // If path completes synchronously, we OnPathReady has already been called and we can skip the rest.
            if (_currentPathID == id) return UpdatePathResult.Ready;

            _pendingPathIDs.Add(id);

            // If KeepPathWhileCalculating is false, the current path needs to be aborted.
            if (!_keepPathWhileCalculating) {
                CurrentPath = null;
                CurrentPathIndex = -1;
            }

            Arrived = false;
            return UpdatePathResult.Pending;
        }

        public virtual bool SamplePathPosition(NavAreaTypes areaTypes, float maxDistance,
                                               out NavSampleResult result, out bool didReachEnd) {
            float distance = 0;
            didReachEnd = false;
            result = NavSampleResult.Invalid;

            NavWaypoint? next = NextWaypoint;
            NavWaypoint? prev = PreviousWaypoint;
            if (!next.HasValue) {
                didReachEnd = true;
                return false;
            }

            if (!prev.HasValue && RemainingDistanceToNextWaypoint > maxDistance) {
                distance = maxDistance;
                return false;
            }

            if (prev.HasValue && (prev.Value.AreaType & areaTypes) != 0 && (next.Value.AreaType & areaTypes) != 0) {
                distance = 0;
                result = new NavSampleResult(
                    next.Value.AreaID,
                    next.Value.AreaType,
                    NavLayer.None,
                    next.Value.Region,
                    next.Value.Type != NavWaypointType.InsideVolume,
                    next.Value.Position.ToV4Pos(),
                    next.Value.Up.ToV4(),
                    distance);
                return true;
            }

            distance = 0;
            for (int i = CurrentPathIndex; i < _currentPath.Waypoints.Count; i++) {
                NavWaypoint waypoint = _currentPath.Waypoints[i];
                float distToWaypoint = waypoint.AccumulatedDistance -
                                       next.Value.AccumulatedDistance +
                                       RemainingDistanceToNextWaypoint;

                if (distToWaypoint > maxDistance) {
                    distance = maxDistance;

                    if (i > 0) {
                        NavWaypoint prevWaypoint = _currentPath.Waypoints[i - 1];
                        float distBetween = waypoint.AccumulatedDistance - prevWaypoint.AccumulatedDistance;
                        float interp = 1.0f - RemainingDistanceToNextWaypoint / distBetween;
                        Vector3 pos = Vector3.Lerp(prevWaypoint.Position, waypoint.Position, interp);
                        Vector3 up = Vector3.Lerp(prevWaypoint.Up, waypoint.Up, interp).normalized;

                        result = new NavSampleResult(
                            prevWaypoint.AreaID,
                            prevWaypoint.AreaType,
                            NavLayer.None,
                            prevWaypoint.Region,
                            prevWaypoint.Type != NavWaypointType.InsideVolume,
                            pos.ToV4Pos(),
                            up.ToV4(),
                            distance);
                    } else {
                        result = new NavSampleResult(
                            waypoint.AreaID,
                            waypoint.AreaType,
                            NavLayer.None,
                            waypoint.Region,
                            waypoint.Type != NavWaypointType.InsideVolume,
                            waypoint.Position.ToV4Pos(),
                            waypoint.Up.ToV4(),
                            distance);
                    }

                    return false;
                }

                distance = distToWaypoint;
                result = new NavSampleResult(waypoint.AreaID,
                                             waypoint.AreaType,
                                             NavLayer.None,
                                             waypoint.Region,
                                             waypoint.Type != NavWaypointType.InsideVolume,
                                             waypoint.Position.ToV4Pos(),
                                             waypoint.Up.ToV4(),
                                             distance);
                if ((waypoint.AreaType & areaTypes) != 0) return true;
            }

            didReachEnd = true;
            return false;
        }

        #endregion

        #region Internal Methods

        private Vector3 CalculateDesiredNavigationVelocityTimesMaxSpeed() =>
            CalculateDesiredNavigationVelocity() * AvoidanceMaxSpeed;

        /// <summary>
        /// Calculate the velocity the agent wants to move in, in the range [0, 1].
        /// </summary>
        protected virtual Vector3 CalculateDesiredNavigationVelocity() {
            // Always return no desired velocity if there is no path.
            if (CurrentPath == null) return Vector3.zero;

            // Get direction to the next path waypoint.
            Vector3 toNext = NextWaypointPosition - CurrentPositionForNextWaypoint;
            Vector3 result;

            if (_accelerationEstimate == 0) {
                // No acceleration estimate provided, so assume the agent can stop instantly.
                // That means it can just continue at full speed until reaching the target.
                result = toNext.normalized;
            } else {
                // Acceleration is limited, so if agent is within stopping distance, it needs to slow down.
                float stoppingDistance = StoppingDistance;
                if (toNext.sqrMagnitude > stoppingDistance * stoppingDistance) {
                    // Not within stopping distance, so proceed at full speed.
                    result = toNext.normalized;
                } else {
                    // Within stopping distance, so start slowing down to zero.
                    result = Vector3.zero;
                }
            }

            return result * _desiredSpeedRatio;
        }

        /// <summary>
        /// Update the value of <see cref="MeasuredVelocity"/>, which is used to determine <see cref="StoppingDistance"/>.
        /// </summary>
        protected virtual void UpdateMeasuredVelocity() {
            Vector3 currentPos = transform.position;

            if (_rigidbody && !_rigidbody.isKinematic) {
                MeasuredVelocity = _rigidbody.GetLinearVelocity();
            } else {
                MeasuredVelocity = (currentPos - _positionLastFrame) / Time.deltaTime;
            }

            _positionLastFrame = currentPos;
        }

        /// <summary>
        /// Update the current path index, which is used to determine <see cref="NextWaypointPosition"/>.
        /// </summary>
        protected virtual void UpdatePathIndex(bool isInitial) {
            if (CurrentPath == null) return;

            // If the agent has an active path and is within acceptance radius of next waypoint, move to the next waypoint.
            bool didAdvance = false;
            while (!isInitial && CurrentPathIndex < CurrentPath.Waypoints.Count && CanAdvanceWaypoint()) {
                CurrentPathIndex++;
                didAdvance = true;
            }

            while (!isInitial && !didAdvance && ShouldBacktrackWaypoint(out int backtrackPoint)) {
                CurrentPathIndex = backtrackPoint;
                return;
            }

            if (CurrentPathIndex >= CurrentPath.Waypoints.Count) {
                Stop(false);
            } else {
                NavWaypoint next = CurrentPath.Waypoints[CurrentPathIndex];
                float remainingDistanceToNext =
                    Vector3.Distance(CurrentPositionForNextWaypoint, NextWaypointPosition);
                float remainingDistance = remainingDistanceToNext +
                                          CurrentPath.Waypoints[^1].AccumulatedDistance -
                                          next.AccumulatedDistance;

                RemainingDistanceToNextWaypoint = remainingDistanceToNext;
                RemainingDistance = remainingDistance;
                DistanceFromStart = next.AccumulatedDistance - remainingDistanceToNext;
            }
        }

        protected virtual bool CanAdvanceWaypoint() {
            if (!NextWaypoint.HasValue) return false;
            NavWaypoint next = NextWaypoint.Value;

            if (Vector3.SqrMagnitude(CurrentPositionForNextWaypoint - NextWaypointPosition) <
                _acceptance * _acceptance) {
                return AdvancePredicate?.Invoke(next, CurrentPathIndex) ?? true;
            }

            if (CurrentPathIndex + 1 >= CurrentPath.Waypoints.Count) {
                return false;
            }

            if (!_checkSkippingWithPhysicsQuery || !_skippingCheckCollider) {
                return false;
            }

            NavWaypoint nextNext = CurrentPath.Waypoints[CurrentPathIndex + 1];

            if (nextNext.AreaType != next.AreaType) {
                return false;
            }

            Vector3 nextNextPos = nextNext.Position;
            Vector3 currentPos = CurrentPositionForNextWaypoint;
            Vector3 toNextNext = nextNextPos - currentPos;

            if (!_skippingCheckStaticOnly) {
                bool didHit = _skippingCheckCollider.ColliderCast(_skippingCheckColliderPadding, toNextNext, out _,
                                                                  toNextNext.magnitude, _skippingCheckLayerMask,
                                                                  QueryTriggerInteraction.Ignore);

                if (didHit) {
                    return false;
                }
            } else {
                int hits = _skippingCheckCollider.ColliderCastNonAlloc(_skippingCheckColliderPadding, toNextNext,
                                                                       _colliderCastHits, toNextNext.magnitude,
                                                                       _skippingCheckLayerMask,
                                                                       QueryTriggerInteraction.Ignore);

                for (int i = 0; i < hits; i++) {
                    if (_colliderCastHits[i].collider.gameObject.isStatic) {
                        return false;
                    }
                }
            }

            return AdvancePredicate?.Invoke(next, CurrentPathIndex) ?? true;
        }

        protected virtual bool ShouldBacktrackWaypoint(out int backtrackPoint) {
            backtrackPoint = _currentPathIndex;

            if (!NextWaypoint.HasValue || !_checkBacktrackWithPhysicsQuery) {
                return false;
            }

            NavWaypoint next = NextWaypoint.Value;

            if (Vector3.SqrMagnitude(CurrentPositionForNextWaypoint - NextWaypointPosition) <
                _acceptance * _acceptance) {
                return false;
            }

            if (CurrentPathIndex < 1) {
                return false;
            }

            if (!_skippingCheckCollider) {
                return false;
            }

            for (int i = _currentPathIndex; i >= 0; i--) {
                NavWaypoint waypoint = CurrentPath.Waypoints[i];
                if (waypoint.AreaType != next.AreaType) return false;

                Vector3 waypointPos = waypoint.Position;
                Vector3 currentPos = CurrentPositionForNextWaypoint;
                Vector3 toWaypoint = waypointPos - currentPos;

                if (!_backtrackCheckStaticOnly) {
                    bool didHit = Physics.Raycast(_skippingCheckCollider.transform.position, toWaypoint,
                                                  out RaycastHit hit,
                                                  toWaypoint.magnitude, _skippingCheckLayerMask,
                                                  QueryTriggerInteraction.Ignore);

                    if (didHit) continue;
                } else {
                    int hits = Physics.RaycastNonAlloc(_skippingCheckCollider.transform.position, toWaypoint,
                                                       _colliderCastHits, toWaypoint.magnitude, _skippingCheckLayerMask,
                                                       QueryTriggerInteraction.Ignore);

                    bool anyStatic = false;
                    for (int j = 0; j < hits; j++) {
                        if (_colliderCastHits[j].collider.gameObject.isStatic) {
                            anyStatic = true;
                            break;
                        }
                    }

                    if (anyStatic) continue;
                }

                if (i == _currentPathIndex) return false;
                if (BacktrackPredicate?.Invoke(waypoint, i) == false) continue;

                backtrackPoint = i;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Callback that is received when a pathfinding request completes, which should start moving along that path.
        /// </summary>
        /// <param name="id">The id of the path request.</param>
        /// <param name="path">The completed path, which is null if no path was found.</param>
        protected virtual void OnPathReady(long id, NavPath path) {
            int index = _pendingPathIDs.IndexOf(id);
            if (index < 0 && !_isUpdatingPath) return;

            if (index >= 0) {
                // Cancel any path requests that are older than the received path.
                for (int i = 0; i < index; i++) {
                    NavPathfinder.MainInstance.CancelPath(_pendingPathIDs[i]);
                }

                // Remove all older paths plus the just completed one.
                _pendingPathIDs.RemoveRange(0, index + 1);
            }

            // Start following the found path.
            CurrentPath = path;
            _currentPathID = id;

            UpdatePathIndex(true);

            if (path != null) {
                PathReady?.Invoke();
            } else {
                PathFailed?.Invoke();
            }
        }

        protected virtual void OnPathIndexChanged(int newIndex) { }

        protected virtual void OnCurrentPathChanged(NavPath newPath) { }

        #endregion
    }

    public delegate bool NavAgentWaypointPredicate(in NavWaypoint waypoint, int index);

    public enum UpdatePathResult {
        Error,
        Failed,
        Pending,
        Ready,
        Arrived
    }
}
