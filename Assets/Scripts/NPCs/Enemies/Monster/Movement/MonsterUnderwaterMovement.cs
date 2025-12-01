using System;
using System.Collections;
using Infohazard.HyperNav;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// OPTIMIZED complex zone movement for navigation within zones.
/// Designed for 20+ NPCs with performance optimizations:
/// - Staggered target checks
/// - Cached navigation queries
/// - Squared distance checks
/// - Reduced Update frequency
/// </summary>
public class MonsterUnderwaterMovement : MonoBehaviour
{
    [SerializeField] private SplineNavAgent agent;
    private UnderwaterMonsterController controller;

    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float acceleration = 300f;
    [SerializeField] private float deceleration = 500f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Target Checking")]
    [SerializeField] private float targetCheckInterval = 0.5f;

    [Header("Distance Thresholds")]
    [SerializeField] private float arriveAtDestinationDistance = 0.4f;
    [SerializeField] private float slowDownDistance = 1f;

    [Header("Physics")]
    [SerializeField] private float maxForce = 6000f;
    [SerializeField] private float waterDrag = 5f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // OPTIMIZATION: Pre-calculated squared distances
    private float arriveDistanceSqr;
    private float slowDownDistanceSqr;

    // Private variables
    private Vector3 moveDirection;
    private Vector3 desiredVelocity;
    [ShowInInspector] private bool hasReachedDestination = false;
    [SerializeField] private bool isActive = false;


    // OPTIMIZATION: Cached target
    private Vector3 cachedTarget;
    private float targetCacheTimer = 0f;
    private const float TARGET_CACHE_DURATION = 0.1f;

    public enum MovementState
    {
        Moving,
        SlowingDown,
        Arrived,
        Idle
    }

    [SerializeField] private MovementState currentState = MovementState.Moving;

    // Events
    public Action OnDestinationReached;

    private void Awake()
    {
        if (agent == null)
        {
            agent = GetComponent<SplineNavAgent>();
            if (agent == null)
            {
                Debug.LogError($"{gameObject.name}: SplineNavAgent component not found!");
                return;
            }
        }

        agent.PathFailed += OnPathFailed;

        // OPTIMIZATION: Pre-calculate squared distances
        arriveDistanceSqr = arriveAtDestinationDistance * arriveAtDestinationDistance;
        slowDownDistanceSqr = slowDownDistance * slowDownDistance;
    }

    public void Initialize(UnderwaterMonsterController controller)
    {
        this.controller = controller;

        currentState = MovementState.Moving;
    }

    private void Update()
    {
        if (!isActive)
            return;

        UpdateNavigation();
        UpdateMovementState();
        HandleRotation();
    }

    private void FixedUpdate()
    {
        if (!isActive) return;
        HandleMovement();
    }

    public void ActivateMovement()
    {
        DebugLog("Activated Movement");
        isActive = true;
        hasReachedDestination = false;

        // Reset caches
        cachedTarget = controller.targetPosition;
        targetCacheTimer = 0f;
        agent.enabled = true;

        agent.Destination = controller.targetPosition;
        currentState = MovementState.Moving;

    }

    public void DeactivateMovement()
    {
        isActive = false;

        agent.Stop(true);
        agent.enabled = false;

        hasReachedDestination = false;

        // Stop the monster
        StopMovement();

        DebugLog("Deactivated Movement");
    }

    /// <summary>
    /// OPTIMIZATION: Cache target and exit point briefly to avoid repeated property accesses
    /// </summary>
    private Vector3 GetCachedTarget()
    {
        targetCacheTimer += Time.deltaTime;

        if (targetCacheTimer >= TARGET_CACHE_DURATION)
        {
            targetCacheTimer = 0f;
            cachedTarget = controller.targetPosition;
        }

        return cachedTarget;
    }

    private void UpdateNavigation()
    {
        if (controller.targetPosition == Vector3.zero || !agent.enabled) return;

        // OPTIMIZATION: Use cached targets to reduce property accesses
        Vector3 cachedTargetPos = GetCachedTarget();

        // Target is inside zone - navigate normally to effective target
        agent.Destination = cachedTargetPos;

        // Get movement direction from NavAgent
        moveDirection = agent.DesiredVelocity.normalized;
    }

    private void UpdateMovementState()
    {
        // OPTIMIZATION: Cache remaining distance as it's accessed multiple times
        float remainingDistance = agent.RemainingDistance;

        // OPTIMIZATION: Pre-calculated squared distances for comparison
        float remainingDistanceSqr = remainingDistance * remainingDistance;

        // Normal state handling
        if (remainingDistanceSqr <= arriveDistanceSqr)
        {
            if (currentState != MovementState.Arrived)
            {
                currentState = MovementState.Arrived;
                OnArrivedAtDestination();
            }
        }
        else if (remainingDistanceSqr <= slowDownDistanceSqr)
        {
            currentState = MovementState.SlowingDown;
        }
        else
        {
            currentState = MovementState.Moving;
        }
    }

    private void HandleMovement()
    {
        if (controller.rb == null) return;

        switch (currentState)
        {
            case MovementState.Moving:
                MoveAtFullSpeed();
                break;

            case MovementState.SlowingDown:
                SlowDownMovement();
                break;

            case MovementState.Arrived:
                StopMovement();
                break;
        }
    }

    private void MoveAtFullSpeed()
    {
        desiredVelocity = moveDirection * maxSpeed;
        ApplyMovementForce(acceleration);
    }

    private void SlowDownMovement()
    {
        float remainingDistance = agent.RemainingDistance;
        float slowDownFactor = (remainingDistance - arriveAtDestinationDistance) /
                              (slowDownDistance - arriveAtDestinationDistance);
        slowDownFactor = Mathf.Clamp01(slowDownFactor);

        float targetSpeed = maxSpeed * slowDownFactor;
        desiredVelocity = moveDirection * targetSpeed;
        ApplyMovementForce(deceleration);
    }

    private void StopMovement()
    {
        DebugLog("Stopped Movement");
        controller.rb.linearVelocity = Vector3.zero;
        currentState = MovementState.Idle;
    }

    private void ApplyMovementForce(float forceMultiplier)
    {
        Vector3 velocityDifference = desiredVelocity - controller.rb.linearVelocity;
        Vector3 force = velocityDifference * forceMultiplier;
        force = Vector3.ClampMagnitude(force, maxForce);

        controller.rb.AddForce(force, ForceMode.Force);
    }

    private void HandleRotation()
    {
        // OPTIMIZATION: Use sqrMagnitude to check if direction is significant
        if (moveDirection.sqrMagnitude > 0.0001f && currentState != MovementState.Arrived)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                                                rotationSpeed * Time.deltaTime);
        }
    }

    private void OnArrivedAtDestination()
    {
        hasReachedDestination = true;

        DebugLog("Arrived at Destination");

        OnDestinationReached?.Invoke();
    }

    /// <summary>
    /// Called when the pathfinding fails to find a valid path.
    /// This will usually be if the target is unreachable (ie outside the navigation zone).
    /// </summary>
    private void OnPathFailed()
    {
        Debug.Log($"{gameObject.name} Path Failed!");
    }

    // Public interface methods
    public bool HasReachedDestination() => hasReachedDestination;
    public MovementState GetCurrentState() => currentState;
    public void SetMaxSpeed(float newSpeed) => maxSpeed = newSpeed;

    public float GetDistanceToTarget() => agent.RemainingDistance;

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MonsterUnderwaterMovement] {message}");
        }
    }

}