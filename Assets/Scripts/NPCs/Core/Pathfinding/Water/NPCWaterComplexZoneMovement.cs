using System;
using System.Collections;
using Infohazard.HyperNav;
using UnityEngine;

/// <summary>
/// OPTIMIZED complex zone movement for navigation within zones.
/// Designed for 20+ NPCs with performance optimizations:
/// - Staggered target checks
/// - Cached navigation queries
/// - Squared distance checks
/// - Reduced Update frequency
/// </summary>
public class NPCWaterComplexZoneMovement : MonoBehaviour
{
    [SerializeField] private SplineNavAgent agent;
    private NPCWaterMovementController waterMovementcontroller;

    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 15f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Distance Thresholds")]
    [SerializeField] private float arriveAtDestinationDistance = 0.4f;
    [SerializeField] private float slowDownDistance = 1f;

    [Header("Physics")]
    [SerializeField] private float maxForce = 100f;
    [SerializeField] private float waterDrag = 5f;

    [Header("Zone Exit Detection")]
    [SerializeField] private float exitRequestDistance = 3f;
    [SerializeField] private float targetCheckInterval = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // OPTIMIZATION: Pre-calculated squared distances
    private float arriveDistanceSqr;
    private float slowDownDistanceSqr;
    private float exitRequestDistanceSqr;

    // Private variables
    private Vector3 moveDirection;
    private Vector3 desiredVelocity;
    private bool hasReachedDestination = false;
    private bool isActive = false;
    private float lastTargetCheck = 0f;
    private bool targetIsOutsideZone = false;
    private bool hasRequestedTransition = false;

    // OPTIMIZATION: Cached target and exit point
    private Vector3 cachedEffectiveTarget;
    private Vector3 cachedExitPoint;
    private float targetCacheTimer = 0f;
    private const float TARGET_CACHE_DURATION = 0.1f;

    public enum MovementState
    {
        Moving,
        SlowingDown,
        Arrived,
        RequestingTransition
    }

    [SerializeField] private MovementState currentState = MovementState.Moving;

    // Events
    public System.Action OnDestinationReached;
    public System.Action<Vector3> OnTransitionRequested;

    public void Initialize(NPCWaterMovementController controller)
    {
        waterMovementcontroller = controller;

        if (agent == null)
        {
            agent = GetComponent<SplineNavAgent>();
            if (agent == null)
            {
                Debug.LogError($"{gameObject.name}: SplineNavAgent component not found!");
                return;
            }
        }

        // OPTIMIZATION: Pre-calculate squared distances
        arriveDistanceSqr = arriveAtDestinationDistance * arriveAtDestinationDistance;
        slowDownDistanceSqr = slowDownDistance * slowDownDistance;
        exitRequestDistanceSqr = exitRequestDistance * exitRequestDistance;

        // OPTIMIZATION: Stagger target checks across NPCs
        lastTargetCheck = -UnityEngine.Random.Range(0f, targetCheckInterval);

        currentState = MovementState.Moving;
    }

    private void Update()
    {
        if (!isActive) return;

        CheckTargetLocation();
        UpdateNavigation();
        UpdateMovementState();
        HandleRotation();

        // OPTIMIZATION: Only log every 60 frames
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            DebugInfo();
        }
    }

    private void FixedUpdate()
    {
        if (!isActive) return;
        HandleMovement();
    }

    public void ActivateMovement()
    {
        isActive = true;
        hasReachedDestination = false;
        hasRequestedTransition = false;
        targetIsOutsideZone = false;
        currentState = MovementState.Moving;

        // Reset caches
        cachedEffectiveTarget = Vector3.zero;
        cachedExitPoint = Vector3.zero;
        targetCacheTimer = 0f;

        // Disable agent to clear any old pathfinding data, and re-enable it in the next frame
        agent.enabled = false;
        StartCoroutine(ReActivateAgentAfterFrame());

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} activated Complex Zone Movement");
        }
    }

    // Re-enable agent in the next frame (used to clear old pathfinding data before we start moving in the complex zone)
    private IEnumerator ReActivateAgentAfterFrame()
    {
        yield return null;
        agent.enabled = true;
    }

    public void DeactivateMovement()
    {
        isActive = false;
        hasRequestedTransition = false;

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} deactivated Complex Zone Movement");
        }
    }

    private void CheckTargetLocation()
    {
        // OPTIMIZATION: Only check periodically to avoid performance issues
        if (Time.time - lastTargetCheck < targetCheckInterval) return;

        lastTargetCheck = Time.time;

        if (waterMovementcontroller.npcController.target == null) return;

        bool wasOutside = targetIsOutsideZone;

        // Check if the EFFECTIVE target is outside the zone
        targetIsOutsideZone = !waterMovementcontroller.IsTargetInCurrentZone();

        if (targetIsOutsideZone && !wasOutside)
        {
            if (showDebugInfo)
            {
                Debug.Log($"{gameObject.name} detected target is outside zone (Type: {waterMovementcontroller.GetTargetType()})");
            }

            // Invalidate exit point cache when status changes
            cachedExitPoint = Vector3.zero;
        }
    }

    /// <summary>
    /// OPTIMIZATION: Cache target and exit point briefly to avoid repeated property accesses
    /// </summary>
    private Vector3 GetCachedEffectiveTarget()
    {
        targetCacheTimer += Time.deltaTime;

        if (targetCacheTimer >= TARGET_CACHE_DURATION)
        {
            targetCacheTimer = 0f;
            cachedEffectiveTarget = waterMovementcontroller.GetEffectiveTarget();
        }

        return cachedEffectiveTarget;
    }

    /// <summary>
    /// OPTIMIZATION: Cache exit point as it's expensive to calculate
    /// </summary>
    private Vector3 GetCachedExitPoint()
    {
        // Only recalculate if cache is invalid (Vector3.zero) or if target status changed
        if (cachedExitPoint == Vector3.zero && targetIsOutsideZone)
        {
            cachedExitPoint = waterMovementcontroller.GetExitPointTowardsTarget();
        }

        return cachedExitPoint;
    }

    private void UpdateNavigation()
    {
        if (waterMovementcontroller.npcController.target == null || !agent.enabled) return;

        // OPTIMIZATION: Use cached targets to reduce property accesses
        Vector3 effectiveTarget = GetCachedEffectiveTarget();

        if (targetIsOutsideZone)
        {
            // Target is outside zone - navigate to the best exit point
            Vector3 exitPoint = GetCachedExitPoint();
            agent.Destination = exitPoint;
        }
        else
        {
            // Target is inside zone - navigate normally to effective target
            agent.Destination = effectiveTarget;
        }

        // Get movement direction from NavAgent
        moveDirection = agent.DesiredVelocity.normalized;
    }

    private void UpdateMovementState()
    {
        // OPTIMIZATION: Cache remaining distance as it's accessed multiple times
        float remainingDistance = agent.RemainingDistance;

        if (targetIsOutsideZone && !hasRequestedTransition)
        {
            // OPTIMIZATION: Use squared distance to avoid sqrt
            Vector3 exitPoint = GetCachedExitPoint();
            Vector3 toExit = exitPoint - transform.position;
            float sqrDistanceToExit = toExit.sqrMagnitude;

            if (sqrDistanceToExit <= exitRequestDistanceSqr)
            {
                currentState = MovementState.RequestingTransition;
                hasRequestedTransition = true;

                if (showDebugInfo)
                {
                    Debug.Log($"{gameObject.name} requesting transition at distance {Mathf.Sqrt(sqrDistanceToExit):F2} from exit");
                }

                // Fire the event with the effective target
                OnTransitionRequested?.Invoke(GetCachedEffectiveTarget());
                return;
            }
        }

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
        if (waterMovementcontroller.npcController.rb == null) return;

        switch (currentState)
        {
            case MovementState.Moving:
                MoveAtFullSpeed();
                break;

            case MovementState.SlowingDown:
                SlowDownMovement();
                break;

            case MovementState.RequestingTransition:
                // Continue moving towards exit point while transition is being set up
                MoveAtFullSpeed();
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
        Vector3 stoppingForce = -waterMovementcontroller.npcController.rb.linearVelocity * deceleration;
        stoppingForce = Vector3.ClampMagnitude(stoppingForce, maxForce);
        waterMovementcontroller.npcController.rb.AddForce(stoppingForce, ForceMode.Force);

        // OPTIMIZATION: Use sqrMagnitude to avoid sqrt
        if (waterMovementcontroller.npcController.rb.linearVelocity.sqrMagnitude < 0.01f)
        {
            waterMovementcontroller.npcController.rb.linearVelocity = Vector3.zero;
        }
    }

    private void ApplyMovementForce(float forceMultiplier)
    {
        Vector3 velocityDifference = desiredVelocity - waterMovementcontroller.npcController.rb.linearVelocity;
        Vector3 force = velocityDifference * forceMultiplier;
        force = Vector3.ClampMagnitude(force, maxForce);

        // Use controller's unified upward movement control
        if (!waterMovementcontroller.ShouldAllowUpwardMovement() && force.y > 0)
        {
            force.y = 0;
        }

        waterMovementcontroller.npcController.rb.AddForce(force, ForceMode.Force);
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

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} has arrived at destination!");
        }

        OnDestinationReached?.Invoke();
    }

    private void DebugInfo()
    {
        if (!showDebugInfo) return;

        Debug.Log($"[Complex Zone] State: {currentState}, Distance: {agent.RemainingDistance:F2}, " +
                 $"Target Type: {waterMovementcontroller.GetTargetType()}, " +
                 $"Target Outside Zone: {targetIsOutsideZone}, Requested Transition: {hasRequestedTransition}");
    }

    // Public interface methods
    public bool HasReachedDestination() => hasReachedDestination;
    public MovementState GetCurrentState() => currentState;
    public void SetMaxSpeed(float newSpeed) => maxSpeed = newSpeed;
    public bool IsActive() => isActive;
    public bool IsTargetOutsideZone() => targetIsOutsideZone;
    public bool HasRequestedTransition() => hasRequestedTransition;

    /// <summary>
    /// Reset the transition request flag (called by coordinator after handling transition)
    /// </summary>
    public void ResetTransitionRequest()
    {
        hasRequestedTransition = false;
        if (currentState == MovementState.RequestingTransition)
        {
            currentState = MovementState.Moving;
        }

        // Invalidate exit point cache
        cachedExitPoint = Vector3.zero;

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} transition request reset");
        }
    }

    /// <summary>
    /// Set custom target check interval for this NPC (useful for less important NPCs)
    /// </summary>
    public void SetTargetCheckInterval(float interval)
    {
        targetCheckInterval = Mathf.Max(0.1f, interval); // Minimum 0.1s
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !isActive) return;

        if (waterMovementcontroller?.npcController?.target == null) return;

        Vector3 effectiveTarget = GetCachedEffectiveTarget();
        Vector3 actualTarget = waterMovementcontroller.GetActualTarget();

        // Draw line to effective target
        Gizmos.color = targetIsOutsideZone ? Color.red : Color.green;
        Gizmos.DrawLine(transform.position, effectiveTarget);
        Gizmos.DrawWireSphere(effectiveTarget, 1f);

        // If effective and actual differ, show both
        // OPTIMIZATION: Use sqrMagnitude for distance check
        if ((effectiveTarget - actualTarget).sqrMagnitude > 0.25f) // 0.5^2
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(effectiveTarget, actualTarget);
            Gizmos.DrawWireSphere(actualTarget, 0.5f);
        }

        // Draw exit point if target is outside zone
        if (targetIsOutsideZone)
        {
            Vector3 exitPoint = GetCachedExitPoint();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(exitPoint, exitRequestDistance);
            Gizmos.DrawLine(transform.position, exitPoint);
        }

        // Draw state indicator
        Vector3 statePos = transform.position + Vector3.up * 5f;
        switch (currentState)
        {
            case MovementState.Moving:
                Gizmos.color = Color.blue;
                break;
            case MovementState.SlowingDown:
                Gizmos.color = Color.yellow;
                break;
            case MovementState.RequestingTransition:
                Gizmos.color = Color.red;
                break;
            case MovementState.Arrived:
                Gizmos.color = Color.green;
                break;
        }
        Gizmos.DrawWireSphere(statePos, 0.4f);

        // Draw movement direction
        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + moveDirection * 3f);
        }

        // Draw NavAgent path if available
        if (agent != null && agent.enabled && agent.CurrentPath != null)
        {
            Gizmos.color = Color.magenta;
            var waypoints = agent.CurrentPath.Waypoints;
            Vector3 lastPos = transform.position;

            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 waypointPos = waypoints[i].Position;
                Gizmos.DrawLine(lastPos, waypointPos);
                Gizmos.DrawWireSphere(waypointPos, 0.15f);
                lastPos = waypointPos;
            }
        }
    }
}