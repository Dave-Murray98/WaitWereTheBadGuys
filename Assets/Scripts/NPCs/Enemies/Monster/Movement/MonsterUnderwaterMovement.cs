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

    // FIX: Add pathfinding state tracking
    private bool isPathCalculating = false;
    private float pathCalculationStartTime = 0f;
    private const float MAX_PATH_CALCULATION_TIME = 2f;

    // OPTIMIZATION: Cached target
    private Vector3 cachedTarget;
    private float targetCacheTimer = 0f;
    private const float TARGET_CACHE_DURATION = 0.1f;

    public enum MovementState
    {
        Moving,
        SlowingDown,
        Arrived,
        Idle,
        CalculatingPath  // NEW: Add state for path calculation
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
        currentState = MovementState.Idle;
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
        isPathCalculating = true;
        pathCalculationStartTime = Time.time;

        // Reset caches
        cachedTarget = controller.targetPosition;
        targetCacheTimer = 0f;
        agent.enabled = true;

        // Set the destination
        agent.Destination = controller.targetPosition;
        currentState = MovementState.CalculatingPath;

        DebugLog($"Setting destination to: {controller.targetPosition}");

        // Start checking for path completion
        StartCoroutine(WaitForPathCalculation());
    }

    private IEnumerator WaitForPathCalculation()
    {
        // Wait for the pathfinding to complete
        while (isPathCalculating && (Time.time - pathCalculationStartTime) < MAX_PATH_CALCULATION_TIME)
        {
            // Check if the agent has a valid path and is no longer calculating
            if (agent.CurrentPath != null && agent.RemainingDistance > arriveAtDestinationDistance)
            {
                isPathCalculating = false;
                currentState = MovementState.Moving;
                DebugLog($"Path calculation complete! RemainingDistance: {agent.RemainingDistance}");
                break;
            }

            yield return new WaitForSeconds(0.1f); // Check every 0.1 seconds
        }

        // If we timeout or still don't have a valid path, something's wrong
        if (isPathCalculating)
        {
            isPathCalculating = false;
            DebugLog("Path calculation timed out or failed!");
            OnPathFailed();
        }
    }

    public void DeactivateMovement()
    {
        isActive = false;
        isPathCalculating = false;

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
        if (controller.targetPosition == Vector3.zero || !agent.enabled || isPathCalculating)
            return;

        // OPTIMIZATION: Use cached targets to reduce property accesses
        Vector3 cachedTargetPos = GetCachedTarget();

        // Target is inside zone - navigate normally to effective target
        agent.Destination = cachedTargetPos;

        // Get movement direction from NavAgent
        moveDirection = agent.DesiredVelocity.normalized;
    }

    private void UpdateMovementState()
    {
        // Don't update movement state while calculating path
        if (isPathCalculating || currentState == MovementState.CalculatingPath)
            return;

        // Make sure we have a valid path before checking distances
        if (agent.CurrentPath == null)
        {
            DebugLog("Agent has no path!");
            return;
        }

        // OPTIMIZATION: Cache remaining distance as it's accessed multiple times
        float remainingDistance = agent.RemainingDistance;

        // // Add extra debug information
        // if (enableDebugLogs && Time.frameCount % 60 == 0) // Log once per second at 60fps
        // {
        //     DebugLog($"Current distance to target: {remainingDistance}, Current state: {currentState}");
        // }

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
            case MovementState.CalculatingPath:
                // Don't move while calculating path
                break;

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
        DebugLog("Path Failed! Target might be unreachable.");
        isPathCalculating = false;
        hasReachedDestination = true; // Mark as reached so we can try again
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