// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav {
    /// <summary>
    /// A script that enables using a NavAgent without writing any code.
    /// </summary>
    /// <remarks>
    /// It is completely optional to use this script, and for more advanced uses you will likely need to write your own
    /// code to handle the movement.
    /// </remarks>
    public class SimpleNavAgentMover : MonoBehaviour {
        #region Serialized Fields

        [SerializeField]
        [Tooltip("The NavAgent that calculates the path.")]
        private NavAgent _navAgent;

        [SerializeField]
        [Tooltip("How the agent's movement is handled.")]
        private MovementModeType _movementMode = MovementModeType.Transform;

        [SerializeField]
        [Tooltip("Rigidbody to use if movement is handled by Rigidbody.")]
        private Rigidbody _rigidbody;

        [SerializeField]
        [Tooltip("If set, the agent will move to this destination.")]
        private Transform _destinationTransform;

        [SerializeField]
        private float _sampleRadius = 0.5f;

        [SerializeField]
        [Tooltip("Enable automatically calculating a new path to the destination.")]
        private bool _enableRepathing = true;

        [SerializeField]
        [Tooltip("If enabled, a new path will be calculated at a set interval.")]
        private bool _repathAtInterval = false;

        [SerializeField]
        [Tooltip("If repathing is enabled, a new path to be calculated at this interval.")]
        private float _repathInterval = 0.5f;

        [SerializeField]
        [Tooltip("If enabled, a new path will be calculated when the destination moves.")]
        private bool _repathOnDestinationTransformMove = true;

        [SerializeField]
        [Tooltip("A new path will be calculated if the destination moves by this distance.")]
        private float _repathDistanceThreshold = 1.0f;

        [SerializeField]
        [Tooltip("Enable to repath when the agent reaches the end of its path.")]
        private bool _repathOnReachEnd = true;

        [SerializeField]
        [Tooltip("Distance from the end of the path to repath.")]
        private float _repathOnReachEndDistance = 1f;

        [SerializeField]
        [Tooltip("The maximum speed the agent can move in units/second when in a volume.")]
        private float _maxSpeedInVolume = 4;

        [SerializeField]
        [Tooltip("The acceleration of the agent in units/second^2 when in a volume.")]
        private float _accelerationInVolume = 6;

        [SerializeField]
        [Tooltip("The maximum speed the agent can move in units/second when on a surface.")]
        private float _maxSpeedOnSurface = 4;

        [SerializeField]
        [Tooltip("The acceleration of the agent in units/second^2 when on a surface.")]
        private float _accelerationOnSurface = 6;

        [SerializeField]
        [Tooltip("How the agent's rotation is determined.")]
        private RotateModeType _rotateMode = RotateModeType.FollowMovement;

        [SerializeField]
        [Tooltip("The speed at which the agent rotates in degrees/second.")]
        private float _rotationSpeed = 5;

        [SerializeField]
        [Tooltip("If enabled, rotation will be done with Slerp instead of RotateTowards.")]
        private bool _rotateBySlerp = true;

        [SerializeField]
        [Tooltip(
            "Once within this distance of a surface while flying, the agent will rotate to align with the surface normal.")]
        private float _distanceToRotateToSurfaceUp = 4;

        [SerializeField]
        [Tooltip("When moving on a surface, keep the agent aligned to the ground normal.")]
        private bool _keepAlignedToGroundNormal = true;

        [SerializeField]
        [Tooltip("When moving on a surface, keep the agent on the ground by raycasting and moving if necessary.")]
        private bool _keepOnGround = true;

        [SerializeField]
        [Tooltip("Layer mask to use for ground check.")]
        private LayerMask _groundCheckLayerMask = 1;

        [SerializeField]
        [Tooltip("Radius to use for ground check.")]
        private float _groundCheckQueryRadius = 0.5f;

        [SerializeField]
        [Tooltip("Distance to check for ground.")]
        private float _groundCheckDistance = 0.1f;

        [SerializeField]
        [Tooltip("When aligning to ground, ground normal must be within this angle of up vector.")]
        private float _groundCheckNormalAngle = 45;

        [SerializeField]
        [Tooltip("Desired vertical offset from the center of this agent to the ground.")]
        [HelpBox("Desired vertical offset from the center of this agent to the ground. " +
                 "For example, if the agent's origin is at the bottom, " +
                 "this should be zero, and if the origin is 0.9 units above the ground, this should be -0.9.")]
        private float _desiredOffsetToGround = -1;

        /// <summary>
        /// This is used to refer to the names of private fields in this class from a custom Editor.
        /// </summary>
        public static class PropNames {
            public const string NavAgent = nameof(_navAgent);
            public const string MovementMode = nameof(_movementMode);
            public const string Rigidbody = nameof(_rigidbody);
            public const string DestinationTransform = nameof(_destinationTransform);
            public const string SampleRadius = nameof(_sampleRadius);
            public const string EnableRepathing = nameof(_enableRepathing);
            public const string RepathAtInterval = nameof(_repathAtInterval);
            public const string RepathInterval = nameof(_repathInterval);
            public const string RepathOnDestinationTransformMove = nameof(_repathOnDestinationTransformMove);
            public const string RepathDistanceThreshold = nameof(_repathDistanceThreshold);
            public const string RepathOnReachEnd = nameof(_repathOnReachEnd);
            public const string RepathOnReachEndDistance = nameof(_repathOnReachEndDistance);
            public const string MaxSpeedInVolume = nameof(_maxSpeedInVolume);
            public const string AccelerationInVolume = nameof(_accelerationInVolume);
            public const string MaxSpeedOnSurface = nameof(_maxSpeedOnSurface);
            public const string AccelerationOnSurface = nameof(_accelerationOnSurface);
            public const string RotateMode = nameof(_rotateMode);
            public const string RotationSpeed = nameof(_rotationSpeed);
            public const string RotateBySlerp = nameof(_rotateBySlerp);
            public const string DistanceToRotateToSurfaceUp = nameof(_distanceToRotateToSurfaceUp);
            public const string KeepAlignedToGroundNormal = nameof(_keepAlignedToGroundNormal);
            public const string KeepOnGround = nameof(_keepOnGround);
            public const string GroundCheckLayerMask = nameof(_groundCheckLayerMask);
            public const string GroundCheckQueryRadius = nameof(_groundCheckQueryRadius);
            public const string GroundCheckDistance = nameof(_groundCheckDistance);
            public const string DesiredOffsetToGround = nameof(_desiredOffsetToGround);
        }

        #endregion

        #region Serialized Field Accessors

        /// <summary>
        /// The NavAgent that calculates the path.
        /// </summary>
        public NavAgent NavAgent {
            get => _navAgent;
            set {
                if (ReferenceEquals(_navAgent, value)) return;
                if (!value) throw new ArgumentNullException(nameof(value), "NavAgent cannot be null.");

                NavAgent oldAgent = _navAgent;
                _navAgent = value;

                if (_destinationTransform) {
                    UpdatePath();
                } else {
                    _navAgent.Destination = oldAgent.Destination;
                }

                oldAgent.Stop(true);
            }
        }

        /// <summary>
        /// If set, movement will be handled by this Rigidbody.
        /// </summary>
        public Rigidbody Rigidbody {
            get => _rigidbody;
            set => _rigidbody = value;
        }

        /// <summary>
        /// If set, the agent will move to this destination.
        /// </summary>
        public Transform DestinationTransform {
            get => _destinationTransform;
            set {
                if (ReferenceEquals(_destinationTransform, value)) return;
                _destinationTransform = value;
                if (value && _hasHadFirstUpdate) {
                    UpdatePath();
                }
            }
        }

        /// <summary>
        /// The radius to sample around the agent for volumes.
        /// </summary>
        public float SampleRadius {
            get => _sampleRadius;
            set => _sampleRadius = value;
        }

        /// <summary>
        /// Enable automatically calculating a new path to the destination.
        /// </summary>
        public bool EnableRepathing {
            get => _enableRepathing;
            set => _enableRepathing = value;
        }

        /// <summary>
        /// If enabled, a new path will be calculated at a set interval.
        /// </summary>
        public bool RepathAtInterval {
            get => _repathAtInterval;
            set => _repathAtInterval = value;
        }

        /// <summary>
        /// Interval to calculate a new path.
        /// </summary>
        public float RepathInterval {
            get => _repathInterval;
            set {
                _repathInterval = value;
                RepathTimer.Interval = _repathInterval;
            }
        }

        /// <summary>
        /// If enabled, a new path will be calculated when the destination moves.
        /// </summary>
        public bool RepathOnDestinationTransformMove {
            get => _repathOnDestinationTransformMove;
            set => _repathOnDestinationTransformMove = value;
        }

        /// <summary>
        /// Distance threshold to repath when the destination moves.
        /// </summary>
        public float RepathDistanceThreshold {
            get => _repathDistanceThreshold;
            set => _repathDistanceThreshold = value;
        }

        /// <summary>
        /// Enable to repath when the agent nears the end of its path.
        /// </summary>
        public bool RepathOnReachEnd {
            get => _repathOnReachEnd;
            set => _repathOnReachEnd = value;
        }

        /// <summary>
        /// Distance from the end of the path to repath.
        /// </summary>
        public float RepathOnReachEndDistance {
            get => _repathOnReachEndDistance;
            set => _repathOnReachEndDistance = value;
        }

        /// <summary>
        /// The maximum speed the agent can move in units/second when in a volume.
        /// </summary>
        public float MaxSpeedInVolume {
            get => _maxSpeedInVolume;
            set => _maxSpeedInVolume = value;
        }

        /// <summary>
        /// The acceleration of the agent in units/second^2 when in a volume.
        /// </summary>
        public float AccelerationInVolume {
            get => _accelerationInVolume;
            set => _accelerationInVolume = value;
        }

        /// <summary>
        /// The maximum speed the agent can move in units/second when on a surface.
        /// </summary>
        public float MaxSpeedOnSurface {
            get => _maxSpeedOnSurface;
            set => _maxSpeedOnSurface = value;
        }

        /// <summary>
        /// The acceleration of the agent in units/second^2 when on a surface.
        /// </summary>
        public float AccelerationOnSurface {
            get => _accelerationOnSurface;
            set => _accelerationOnSurface = value;
        }

        /// <summary>
        /// How the agent's rotation is determined.
        /// </summary>
        public RotateModeType RotateMode {
            get => _rotateMode;
            set => _rotateMode = value;
        }

        /// <summary>
        /// The speed at which the agent rotates in degrees/second.
        /// </summary>
        public float RotationSpeed {
            get => _rotationSpeed;
            set => _rotationSpeed = value;
        }

        /// <summary>
        /// If enabled, rotation will be done with Slerp instead of RotateTowards.
        /// </summary>
        public bool RotateBySlerp {
            get => _rotateBySlerp;
            set => _rotateBySlerp = value;
        }

        /// <summary>
        /// Once within this distance of a surface while flying, the agent will rotate to align with the surface normal.
        /// </summary>
        public float DistanceToRotateToSurfaceUp {
            get => _distanceToRotateToSurfaceUp;
            set => _distanceToRotateToSurfaceUp = value;
        }

        /// <summary>
        /// When moving on a surface, keep the agent aligned to the ground normal.
        /// </summary>
        public bool KeepAlignedToGroundNormal {
            get => _keepAlignedToGroundNormal;
            set => _keepAlignedToGroundNormal = value;
        }

        /// <summary>
        /// When moving on a surface, keep the agent on the ground by raycasting and moving if necessary.
        /// </summary>
        public bool KeepOnGround {
            get => _keepOnGround;
            set => _keepOnGround = value;
        }

        /// <summary>
        /// Layer mask to use for ground check.
        /// </summary>
        public LayerMask GroundCheckLayerMask {
            get => _groundCheckLayerMask;
            set => _groundCheckLayerMask = value;
        }

        /// <summary>
        /// Radius to use for ground check.
        /// </summary>
        public float GroundCheckQueryRadius {
            get => _groundCheckQueryRadius;
            set => _groundCheckQueryRadius = value;
        }

        /// <summary>
        /// Distance to check for ground.
        /// </summary>
        public float GroundCheckDistance {
            get => _groundCheckDistance;
            set => _groundCheckDistance = value;
        }

        /// <summary>
        /// When aligning to ground, ground normal must be within this angle of up vector.
        /// </summary>
        public float GroundCheckNormalAngle {
            get => _groundCheckNormalAngle;
            set => _groundCheckNormalAngle = value;
        }

        /// <summary>
        /// Desired vertical offset from the center of this agent to the ground.
        /// </summary>
        public float DesiredOffsetToGround {
            get => _desiredOffsetToGround;
            set => _desiredOffsetToGround = value;
        }

        #endregion

        #region Internal Fields

        /// <summary>
        /// Timer for repathing at intervals.
        /// </summary>
        protected PassiveTimer RepathTimer;

        /// <summary>
        /// Current velocity. Not used if a Rigidbody is used.
        /// </summary>
        protected Vector3 KinematicVelocity;

        /// <summary>
        /// Last destination set.
        /// </summary>
        protected Vector3 LastDestination;

        private bool _hasHadFirstUpdate;
        private bool _dataChanged;
        private bool _isGroundDetected;
        private Vector3 _groundNormal;

        #endregion

        public Vector3 Velocity =>
            _movementMode == MovementModeType.Rigidbody ? _rigidbody.GetLinearVelocity() : KinematicVelocity;

        public float MaxSpeed => _navAgent.IsNavigatingOnSurface ? _maxSpeedOnSurface : _maxSpeedInVolume;

        public float Acceleration => _navAgent.IsNavigatingOnSurface ? _accelerationOnSurface : _accelerationInVolume;

        #region Unity Events

        /// <summary>
        /// Reset state so next Update will calculate a new path.
        /// </summary>
        protected void OnEnable() {
            _hasHadFirstUpdate = false;

            if (_enableRepathing && _repathAtInterval) {
                RepathTimer = new PassiveTimer(_repathInterval);
            }

            ChangeNavAreaData.DataChanging += ChangeNavDataDataChanging;
            ChangeNavAreaData.DataChanged += ChangeNavDataDataChanged;
        }

        /// <summary>
        /// Unsubscribe from events.
        /// </summary>
        private void OnDisable() {
            ChangeNavAreaData.DataChanging -= ChangeNavDataDataChanging;
            ChangeNavAreaData.DataChanged -= ChangeNavDataDataChanged;
        }

        /// <summary>
        /// Update the path if needed, and move the agent if not using a Rigidbody.
        /// </summary>
        protected virtual void Update() {
            _navAgent.AccelerationEstimate = Acceleration;

            if (_destinationTransform &&
                (NavVolume.NativeDataMap.IsCreated || NavSurface.NativeDataMap.IsCreated) &&
                (!_hasHadFirstUpdate || _dataChanged || CheckRepath())) {
                _hasHadFirstUpdate = true;
                UpdatePath();
            }

            if (!_navAgent.Arrived && _movementMode == MovementModeType.Transform) {
                UpdateMovementTransform();
            } else {
                KinematicVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Move the agent if using a Rigidbody.
        /// </summary>
        protected virtual void FixedUpdate() {
            if (_movementMode == MovementModeType.Rigidbody) {
                if (!_navAgent.Arrived) {
                    UpdateMovementRigidbody();
                } else {
                    Vector3 deltaV = Vector3.ClampMagnitude(-_rigidbody.GetLinearVelocity(),
                                                            Acceleration * Time.fixedDeltaTime);
                    _rigidbody.AddForce(deltaV, ForceMode.VelocityChange);
                }
            }
        }

        /// <summary>
        /// Get references to components.
        /// </summary>
        protected virtual void Reset() {
            _navAgent = GetComponent<NavAgent>();

            if (TryGetComponent(out _rigidbody)) {
                _movementMode = MovementModeType.Rigidbody;
            } else {
                _movementMode = MovementModeType.Transform;
            }

            _keepOnGround = true;
            if (TryGetComponent(out CapsuleCollider capsule)) {
                _groundCheckQueryRadius = capsule.radius;
                _desiredOffsetToGround = -capsule.height * 0.5f + capsule.center.y;
                _groundCheckDistance = capsule.height * 0.05f;
            }
        }

        #endregion

        #region Internal Methods

        private void ChangeNavDataDataChanging() {
            if (!_destinationTransform) {
                LastDestination = _navAgent.Destination;
            }

            _navAgent.Stop(true);
        }

        private void ChangeNavDataDataChanged() {
            _dataChanged = true;
        }

        /// <summary>
        /// Calculate a new path to <see cref="DestinationTransform"/>.
        /// </summary>
        protected virtual void UpdatePath() {
            if (!_destinationTransform && !_dataChanged) return;

            _dataChanged = false;
            if (RepathTimer.IsInitialized) {
                RepathTimer.StartInterval();
            }

            if (_destinationTransform) {
                LastDestination = _destinationTransform.position;
            }

            _navAgent.Destination = LastDestination;
        }

        /// <summary>
        /// Check if a new path should be calculated.
        /// </summary>
        /// <returns>True if a new path should be calculated.</returns>
        protected virtual bool CheckRepath() {
            if (!_enableRepathing) return false;

            if (_repathAtInterval && RepathTimer.IsIntervalEnded) {
                return true;
            }

            if (_repathOnDestinationTransformMove &&
                (_destinationTransform.position - LastDestination).sqrMagnitude >
                _repathDistanceThreshold * _repathDistanceThreshold) {
                return true;
            }

            if (_repathOnReachEnd && _navAgent.RemainingDistance < _repathOnReachEndDistance) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Update the agent's position, rotation, and velocity based on the desired velocity.
        /// </summary>
        /// <remarks>
        /// Should only be called in Update and only if not using a Rigidbody.
        /// </remarks>
        protected virtual void UpdateMovementTransform() {
            Vector3 desiredVel = _navAgent.DesiredVelocity * MaxSpeed;
            Vector3 deltaV = Vector3.ClampMagnitude(desiredVel - KinematicVelocity, Acceleration * Time.deltaTime);

            transform.Translate((KinematicVelocity + deltaV * 0.5f) * Time.deltaTime, Space.World);

            KinematicVelocity += deltaV;

            _isGroundDetected = TryGetGroundCheckPosition(transform.position, transform.rotation,
                                                          out Vector3 newPosition, out _groundNormal);

            if (_rotateMode != RotateModeType.None) {
                transform.rotation = GetNewRotation(transform.rotation, KinematicVelocity, Time.deltaTime);
            }

            if (_keepOnGround && _isGroundDetected) {
                transform.position = newPosition;
            }
        }

        /// <summary>
        /// Update the Rigidbody's velocity and rotation based on the desired velocity.
        /// </summary>
        /// <remarks>
        /// Should only be called in FixedUpdate and only if using a Rigidbody.
        /// </remarks>
        protected virtual void UpdateMovementRigidbody() {
            Vector3 desiredVel = _navAgent.DesiredVelocity * MaxSpeed;
            Vector3 deltaV =
                Vector3.ClampMagnitude(desiredVel - _rigidbody.GetLinearVelocity(),
                                       Acceleration * Time.fixedDeltaTime);

            _rigidbody.AddForce(deltaV, ForceMode.VelocityChange);

            _isGroundDetected = TryGetGroundCheckPosition(_rigidbody.position, _rigidbody.rotation,
                                                          out Vector3 newPosition, out _groundNormal);

            if (_rotateMode != RotateModeType.None) {
                _rigidbody.MoveRotation(GetNewRotation(_rigidbody.rotation, _rigidbody.GetLinearVelocity(),
                                                       Time.fixedDeltaTime));
            }

            if (_keepOnGround && _isGroundDetected) {
                _rigidbody.MovePosition(newPosition);
            }
        }

        protected virtual bool TryGetGroundCheckPosition(Vector3 currentPosition, Quaternion currentRotation,
                                                         out Vector3 newPosition, out Vector3 groundNormal) {
            if (_navAgent.PreviousWaypoint is not
                { Type: NavWaypointType.EnterSurface or NavWaypointType.InsideSurface }) {
                newPosition = currentPosition;
                groundNormal = Vector3.zero;
                return false;
            }

            Vector3 up = currentRotation * Vector3.up;
            const float padding = 0.1f;
            Vector3 offsetToStart = up * (_desiredOffsetToGround + _groundCheckQueryRadius + padding);
            Vector3 startPos = currentPosition + offsetToStart;
            float distance = _groundCheckDistance + padding;

            bool result;
            RaycastHit hit;
            if (_sampleRadius > 0) {
                result = Physics.SphereCast(startPos, _groundCheckQueryRadius, -up, out hit, distance,
                                            _groundCheckLayerMask, QueryTriggerInteraction.Ignore);
            } else {
                result = Physics.Raycast(startPos, -up, out hit, distance, _groundCheckLayerMask,
                                         QueryTriggerInteraction.Ignore);
            }

            if (result && Vector3.Angle(hit.normal, up) < _groundCheckNormalAngle) {
                newPosition = currentPosition - up * (hit.distance - padding);
                groundNormal = hit.normal;
                return true;
            }

            newPosition = currentPosition;
            groundNormal = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Get the updated rotation based on the desired velocity and time delta.
        /// </summary>
        /// <param name="current">Current rotation.</param>
        /// <param name="velocity">Current velocity.</param>
        /// <param name="deltaTime">Time since last rotation update.</param>
        /// <returns>New rotation to use.</returns>
        protected virtual Quaternion GetNewRotation(Quaternion current, Vector3 velocity, float deltaTime) {
            if (velocity.sqrMagnitude < 0.0001f) return current;

            Vector3 vel = velocity.normalized;

            float delta = deltaTime * _rotationSpeed;

            Quaternion desired;
            if ((_navAgent.IsNavigatingOnSurface || _navAgent.Arrived) &&
                _keepAlignedToGroundNormal &&
                _isGroundDetected) {
                desired = MathUtility.YZRotation(_groundNormal, vel);
            } else if (_navAgent.IsNavigatingOnSurface &&
                       _navAgent.SamplePathPosition(NavAreaTypes.Surface, _distanceToRotateToSurfaceUp,
                                                    out NavSampleResult result, out _)) {
                desired = MathUtility.YZRotation(result.Up.xyz, vel);
            } else {
                Vector3 up = _rotateMode == RotateModeType.FollowMovementWithWorldUp
                    ? Vector3.up
                    : current * Vector3.up;
                desired = MathUtility.ZYRotation(vel, up);
            }

            return InterpolateRotation(current, desired, delta);
        }

        /// <summary>
        /// Interpolate between two rotations using either RotateTowards or Slerp.
        /// </summary>
        /// <param name="from">Source rotation.</param>
        /// <param name="to">Target rotation.</param>
        /// <param name="delta">Delta value (meaning depends on interpolation method).</param>
        /// <returns>Interpolated rotation.</returns>
        protected virtual Quaternion InterpolateRotation(Quaternion from, Quaternion to, float delta) {
            if (_rotateBySlerp) {
                return Quaternion.Slerp(from, to, delta);
            }

            return Quaternion.RotateTowards(from, to, delta);
        }

        #endregion

        /// <summary>
        /// Used to specify the modes for rotating the agent.
        /// </summary>
        public enum RotateModeType {
            FollowMovement,
            FollowMovementWithWorldUp,
            None,
        }

        public enum MovementModeType {
            Transform,
            Rigidbody,
        }
    }
}
