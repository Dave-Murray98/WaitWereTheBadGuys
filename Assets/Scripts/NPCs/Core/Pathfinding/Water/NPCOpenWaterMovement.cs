using System;
using NWH.DWP2.WaterObjects;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Assertions.Must;

/// <summary>
/// OPTIMIZED open water movement with direct pathfinding and obstacle avoidance.
/// Designed for 20+ NPCs with performance optimizations:
/// - Cached component references
/// - Squared distance checks (no sqrt)
/// - Staggered raycast updates
/// - Reduced Update frequency for non-critical checks
/// </summary>
public class NPCOpenWaterMovement : MonoBehaviour
{
    private NPCWaterMovementController waterMovementcontroller;

    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 12f;
    [SerializeField] private float rotationSpeed = 4f;

    [Header("Distance Thresholds")]
    [SerializeField] private float arriveAtDestinationDistance = 2f;
    [SerializeField] private float slowDownDistance = 8f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleLayerMask = -1;
    [SerializeField] private float detectionDistance = 5f;
    [SerializeField] private float avoidanceForce = 10f;
    [SerializeField] private int raycastCount = 5;
    [SerializeField] private float raycastSpread = 45f;

    [Header("Performance Optimization")]
    [SerializeField, Tooltip("How often to update obstacle avoidance raycasts (seconds)")]
    private float obstacleCheckInterval = 0.15f;

    [Header("Physics")]
    [SerializeField] private float maxForce = 100f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showDebugRays = true;

    // OPTIMIZATION: Pre-calculated squared distances to avoid sqrt
    private float arriveDistanceSqr;
    private float slowDownDistanceSqr;

    // OPTIMIZATION: Cached direction and state calculations
    private Vector3 moveDirection;
    private Vector3 desiredVelocity;
    private Vector3 avoidanceDirection;
    private bool hasReachedDestination = false;
    private bool isActive = false;

    // OPTIMIZATION: Staggered obstacle checking
    private float obstacleCheckTimer = 0f;

    // OPTIMIZATION: Cached target position to avoid multiple property accesses
    private Vector3 cachedTargetPosition;
    private float targetCacheTimer = 0f;
    private const float TARGET_CACHE_DURATION = 0.1f;

    public enum MovementState
    {
        Moving,
        SlowingDown,
        Avoiding,
        Arrived
    }

    [SerializeField] private MovementState currentState = MovementState.Moving;

    // Events
    public System.Action OnDestinationReached;

    public void Initialize(NPCWaterMovementController controller)
    {
        this.waterMovementcontroller = controller;

        // OPTIMIZATION: Pre-calculate squared distances once
        arriveDistanceSqr = arriveAtDestinationDistance * arriveAtDestinationDistance;
        slowDownDistanceSqr = slowDownDistance * slowDownDistance;

        // OPTIMIZATION: Stagger obstacle checks across NPCs
        obstacleCheckTimer = -UnityEngine.Random.Range(0f, obstacleCheckInterval);

        currentState = MovementState.Moving;
    }

    private void Update()
    {
        if (!isActive) return;

        UpdateMovementDirection();
        UpdateMovementState();
        HandleRotation();

        // OPTIMIZATION: Only log every 60 frames instead of checking every frame
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
        currentState = MovementState.Moving;

        // Reset cached values
        cachedTargetPosition = Vector3.zero;
        targetCacheTimer = 0f;

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} activated Open Water Movement");
        }
    }

    public void DeactivateMovement()
    {
        isActive = false;

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} deactivated Open Water Movement");
        }
    }

    private void UpdateMovementDirection()
    {
        if (waterMovementcontroller.npcController == null) return;

        // OPTIMIZATION: Cache target position for a short duration
        Vector3 targetPosition = GetCachedTargetPosition();

        // Calculate basic direction to target
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;

        // OPTIMIZATION: Only update obstacle avoidance at intervals
        obstacleCheckTimer += Time.deltaTime;
        if (obstacleCheckTimer >= obstacleCheckInterval)
        {
            obstacleCheckTimer = 0f;
            avoidanceDirection = CalculateObstacleAvoidance();
        }

        // Combine target direction with obstacle avoidance
        if (avoidanceDirection.sqrMagnitude > 0.001f) // OPTIMIZATION: Use sqrMagnitude
        {
            moveDirection = (directionToTarget + avoidanceDirection).normalized;
        }
        else
        {
            moveDirection = directionToTarget;
        }
    }

    /// <summary>
    /// OPTIMIZATION: Cache target position briefly to avoid repeated property accesses
    /// </summary>
    private Vector3 GetCachedTargetPosition()
    {
        targetCacheTimer += Time.deltaTime;

        if (targetCacheTimer >= TARGET_CACHE_DURATION)
        {
            targetCacheTimer = 0f;
            cachedTargetPosition = waterMovementcontroller.GetEffectiveTarget();
        }

        return cachedTargetPosition;
    }

    private Vector3 CalculateObstacleAvoidance()
    {
        Vector3 avoidance = Vector3.zero;
        Vector3 forward = transform.forward;

        // Cast multiple rays in a cone pattern
        for (int i = 0; i < raycastCount; i++)
        {
            float angle = 0f;
            if (raycastCount > 1)
            {
                // OPTIMIZATION: Avoid division in loop
                float t = (float)i / (raycastCount - 1);
                angle = Mathf.Lerp(-raycastSpread * 0.5f, raycastSpread * 0.5f, t);
            }

            Vector3 rayDirection = Quaternion.AngleAxis(angle, transform.up) * forward;

            if (Physics.Raycast(transform.position, rayDirection, out RaycastHit hit, detectionDistance, obstacleLayerMask))
            {
                // Calculate avoidance direction (away from obstacle)
                Vector3 obstacleAvoidDirection = transform.position - hit.point;
                obstacleAvoidDirection.Normalize();

                // Weight based on distance
                float distanceWeight = 1f - (hit.distance / detectionDistance);
                avoidance += obstacleAvoidDirection * distanceWeight * avoidanceForce;

                if (showDebugRays)
                {
                    Debug.DrawRay(transform.position, rayDirection * hit.distance, Color.red);
                }
            }
            else if (showDebugRays)
            {
                Debug.DrawRay(transform.position, rayDirection * detectionDistance, Color.green);
            }
        }

        return avoidance.normalized;
    }

    private void UpdateMovementState()
    {
        if (waterMovementcontroller.npcController.target == null) return;

        // OPTIMIZATION: Use squared distance to avoid sqrt
        Vector3 targetPosition = GetCachedTargetPosition();
        Vector3 toTarget = targetPosition - transform.position;
        float sqrDistanceToTarget = toTarget.sqrMagnitude;

        // Update state based on distance and obstacles
        if (sqrDistanceToTarget <= arriveDistanceSqr)
        {
            if (currentState != MovementState.Arrived)
            {
                currentState = MovementState.Arrived;
                OnArrivedAtDestination();
            }
        }
        else if (avoidanceDirection.sqrMagnitude > 0.001f) // OPTIMIZATION: sqrMagnitude check
        {
            currentState = MovementState.Avoiding;
        }
        else if (sqrDistanceToTarget <= slowDownDistanceSqr)
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
        if (waterMovementcontroller.npcController?.rb == null) return;

        switch (currentState)
        {
            case MovementState.Moving:
                MoveAtFullSpeed();
                break;

            case MovementState.SlowingDown:
                SlowDownMovement();
                break;

            case MovementState.Avoiding:
                HandleObstacleAvoidance();
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
        // OPTIMIZATION: Use squared distance and avoid unnecessary calculations
        Vector3 targetPosition = GetCachedTargetPosition();
        Vector3 toTarget = targetPosition - transform.position;
        float sqrDistanceToTarget = toTarget.sqrMagnitude;

        // Calculate slowdown factor using squared distances
        float sqrSlowRange = slowDownDistanceSqr - arriveDistanceSqr;
        float sqrCurrentRange = sqrDistanceToTarget - arriveDistanceSqr;
        float slowDownFactor = Mathf.Clamp01(sqrCurrentRange / sqrSlowRange);

        float targetSpeed = maxSpeed * slowDownFactor;
        desiredVelocity = moveDirection * targetSpeed;
        ApplyMovementForce(deceleration);
    }

    private void HandleObstacleAvoidance()
    {
        desiredVelocity = moveDirection * maxSpeed;
        ApplyMovementForce(acceleration);
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

        Vector3 targetPosition = GetCachedTargetPosition();
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        Debug.Log($"[Open Water] State: {currentState}, " +
                 $"Target Type: {waterMovementcontroller.GetTargetType()}, " +
                 $"Distance: {distanceToTarget:F2}, " +
                 $"Speed: {waterMovementcontroller?.npcController.rb?.linearVelocity.magnitude ?? 0:F2}, " +
                 $"Avoiding: {avoidanceDirection.sqrMagnitude > 0.001f}");
    }

    // Public interface methods
    public bool HasReachedDestination() => hasReachedDestination;
    public MovementState GetCurrentState() => currentState;
    public void SetMaxSpeed(float newSpeed) => maxSpeed = newSpeed;
    public bool IsActive() => isActive;

    /// <summary>
    /// Set custom obstacle check interval for this NPC (useful for less important NPCs)
    /// </summary>
    public void SetObstacleCheckInterval(float interval)
    {
        obstacleCheckInterval = Mathf.Max(0.05f, interval); // Minimum 0.05s
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !isActive) return;

        if (waterMovementcontroller?.npcController?.target == null) return;

        Vector3 effectiveTarget = waterMovementcontroller.GetEffectiveTarget();

        // Draw destination and slow down radii
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(effectiveTarget, arriveAtDestinationDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(effectiveTarget, slowDownDistance);

        // Draw detection range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);

        // Draw movement direction
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + moveDirection * 3f);

        // Draw avoidance direction if active
        if (avoidanceDirection.sqrMagnitude > 0.001f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + avoidanceDirection * 2f);
        }
    }
}