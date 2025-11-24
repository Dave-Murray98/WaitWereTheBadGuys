using UnityEngine;

/// <summary>
/// OPTIMIZED transition movement for handling transitions between complex zones and open water.
/// Designed for 20+ NPCs with performance optimizations:
/// - Cached target positions
/// - Squared distance checks
/// - Reduced state update frequency
/// </summary>
public class NPCWaterZoneTransitionMovement : MonoBehaviour
{
    private NPCWaterMovementController waterMovementcontroller;

    [Header("Transition Settings")]
    [SerializeField] private float transitionSpeed = 6f;
    [SerializeField] private float transitionAcceleration = 12f;
    [SerializeField] private float rotationSpeed = 6f;

    [Header("Exit Handling")]
    [SerializeField] private float exitForce = 15f;
    [SerializeField] private float exitDuration = 1f;
    [SerializeField] private float minimumExitDistance = 3f;

    [Header("Physics")]
    [SerializeField] private float maxForce = 150f;
    [SerializeField] private float waterDrag = 4f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // OPTIMIZATION: Pre-calculated squared distances
    private float minimumExitDistanceSqr;

    // Private variables
    private bool isActive = false;
    private Vector3 transitionTarget;
    private Vector3 transitionStartPosition;
    private float transitionStartTime;
    private bool isExitingZone = false;
    private UnderwaterComplexZoneTrigger sourceZone;

    // OPTIMIZATION: Cached direction to avoid recalculating every frame
    private Vector3 cachedDirectionToTarget;
    private float directionCacheTimer = 0f;
    private const float DIRECTION_CACHE_DURATION = 0.1f;

    public enum TransitionState
    {
        MovingToTarget,
        ExitingZone,
        WaitingForClear
    }

    [SerializeField] private TransitionState currentState = TransitionState.MovingToTarget;

    // Events
    public System.Action OnTransitionComplete;
    public System.Action OnDestinationReached;

    public void Initialize(NPCWaterMovementController controller)
    {
        waterMovementcontroller = controller;

        // OPTIMIZATION: Pre-calculate squared distance
        minimumExitDistanceSqr = minimumExitDistance * minimumExitDistance;

        currentState = TransitionState.MovingToTarget;
    }

    private void Update()
    {
        if (!isActive) return;

        UpdateTransitionState();
        HandleRotation();

        // OPTIMIZATION: Only log every 30 frames (more frequent than others since transitions are shorter)
        if (showDebugInfo && Time.frameCount % 30 == 0)
        {
            DebugInfo();
        }
    }

    private void FixedUpdate()
    {
        if (!isActive) return;
        HandleTransitionMovement();
    }

    /// <summary>
    /// Start a transition to move towards a target position
    /// </summary>
    public void StartTransition(Vector3 targetPosition, bool exitingZone = false, UnderwaterComplexZoneTrigger exitingFromZone = null)
    {
        isActive = true;
        transitionTarget = targetPosition;
        transitionStartPosition = transform.position;
        transitionStartTime = Time.time;
        isExitingZone = exitingZone;
        sourceZone = exitingFromZone;

        currentState = exitingZone ? TransitionState.ExitingZone : TransitionState.MovingToTarget;

        // Reset cache
        cachedDirectionToTarget = Vector3.zero;
        directionCacheTimer = 0f;

        if (showDebugInfo)
        {
            string mode = exitingZone ? "EXIT ZONE" : "NORMAL";
            string targetType = waterMovementcontroller != null ? $" TargetType:{waterMovementcontroller.GetTargetType()}" : "";
            Debug.Log($"{gameObject.name} started transition in {mode} mode to {targetPosition}{targetType}");
        }
    }

    /// <summary>
    /// Stop the current transition
    /// </summary>
    public void StopTransition()
    {
        isActive = false;
        isExitingZone = false;
        sourceZone = null;

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} stopped transition");
        }
    }

    /// <summary>
    /// OPTIMIZATION: Cache direction to target to avoid recalculating every frame
    /// </summary>
    private Vector3 GetCachedDirectionToTarget()
    {
        directionCacheTimer += Time.deltaTime;

        if (directionCacheTimer >= DIRECTION_CACHE_DURATION || cachedDirectionToTarget == Vector3.zero)
        {
            directionCacheTimer = 0f;

            // Always use current effective target (it may update if actual target moves)
            if (waterMovementcontroller != null)
            {
                transitionTarget = waterMovementcontroller.GetEffectiveTarget();
            }

            cachedDirectionToTarget = (transitionTarget - transform.position).normalized;
        }

        return cachedDirectionToTarget;
    }

    private void UpdateTransitionState()
    {
        // OPTIMIZATION: Use squared distance to avoid sqrt
        Vector3 toTarget = transitionTarget - transform.position;
        float sqrDistanceToTarget = toTarget.sqrMagnitude;

        switch (currentState)
        {
            case TransitionState.MovingToTarget:
                // Arrival threshold: 2m = 4 sqr units
                if (sqrDistanceToTarget <= 4f)
                {
                    CompleteTransition();
                }
                break;

            case TransitionState.ExitingZone:
                // Check if we've been exiting for long enough OR moved far enough from start
                float exitTime = Time.time - transitionStartTime;
                Vector3 fromStart = transform.position - transitionStartPosition;
                float sqrDistanceFromStart = fromStart.sqrMagnitude;

                if (exitTime >= exitDuration || sqrDistanceFromStart >= minimumExitDistanceSqr)
                {
                    // Check if we're actually outside the zone
                    bool stillInZone = sourceZone != null && sourceZone.IsPositionInZone(transform.position);

                    if (!stillInZone)
                    {
                        // Successfully exited zone
                        currentState = TransitionState.WaitingForClear;
                    }
                    else if (sqrDistanceFromStart >= minimumExitDistanceSqr * 2.25f) // 1.5^2
                    {
                        // Force completion if we've moved far enough
                        currentState = TransitionState.WaitingForClear;
                        if (showDebugInfo)
                        {
                            Debug.Log($"{gameObject.name} forced zone exit completion due to distance");
                        }
                    }
                }
                break;

            case TransitionState.WaitingForClear:
                // Small delay to ensure we're completely clear
                if (Time.time - transitionStartTime >= exitDuration + 0.2f)
                {
                    CompleteTransition();
                }
                break;
        }
    }

    private void HandleTransitionMovement()
    {
        if (waterMovementcontroller.npcController.rb == null) return;

        Vector3 directionToTarget = GetCachedDirectionToTarget();
        Vector3 desiredVelocity = Vector3.zero;
        float forceMultiplier = transitionAcceleration;

        switch (currentState)
        {
            case TransitionState.MovingToTarget:
                desiredVelocity = directionToTarget * transitionSpeed;
                break;

            case TransitionState.ExitingZone:
                // Apply strong exit force in addition to normal movement
                Vector3 exitForceVector = directionToTarget * exitForce;

                // Use controller's upward movement control for exit force
                if (!waterMovementcontroller.ShouldAllowUpwardMovement() && exitForceVector.y > 0)
                {
                    exitForceVector.y = 0;
                }

                waterMovementcontroller.npcController.rb.AddForce(exitForceVector, ForceMode.Force);

                // Also apply normal movement but at higher speed
                desiredVelocity = directionToTarget * (transitionSpeed * 1.2f);
                forceMultiplier = transitionAcceleration * 1.5f;
                break;

            case TransitionState.WaitingForClear:
                // Continue gentle movement towards target
                desiredVelocity = directionToTarget * (transitionSpeed * 0.8f);
                break;
        }

        // Apply movement force
        // OPTIMIZATION: Check sqrMagnitude instead of magnitude
        if (desiredVelocity.sqrMagnitude > 0.0001f)
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
    }

    private void HandleRotation()
    {
        Vector3 directionToTarget = GetCachedDirectionToTarget();

        // OPTIMIZATION: Use sqrMagnitude to check if direction is significant
        if (directionToTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                                                rotationSpeed * Time.deltaTime);
        }
    }

    private void CompleteTransition()
    {
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} completed transition");
        }

        // OPTIMIZATION: Use squared distance for comparison
        Vector3 toTarget = transitionTarget - transform.position;
        float sqrDistanceToTarget = toTarget.sqrMagnitude;

        if (sqrDistanceToTarget <= 4f) // 2m = 4 sqr units
        {
            OnDestinationReached?.Invoke();
        }

        OnTransitionComplete?.Invoke();
        StopTransition();
    }

    private void DebugInfo()
    {
        if (!showDebugInfo) return;

        float distanceToTarget = Vector3.Distance(transform.position, transitionTarget);
        string zoneStatus = isExitingZone ? $" (Exiting from {sourceZone?.name ?? "Unknown"})" : "";
        string targetType = waterMovementcontroller != null ? $" TargetType:{waterMovementcontroller.GetTargetType()}" : "";

        Debug.Log($"[Transition] State: {currentState}, Distance to Target: {distanceToTarget:F2}, " +
                 $"Speed: {waterMovementcontroller.npcController.rb.linearVelocity.magnitude:F2}{zoneStatus}{targetType}");
    }

    // Public interface methods
    public bool IsActive() => isActive;
    public TransitionState GetCurrentState() => currentState;
    public Vector3 GetTransitionTarget() => transitionTarget;
    public bool IsExitingZone() => isExitingZone;

    public void SetTransitionSpeed(float newSpeed)
    {
        transitionSpeed = newSpeed;
    }

    // Visualization
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !isActive)
            return;

        // Draw line to transition target
        Gizmos.color = isExitingZone ? Color.red : Color.yellow;
        Gizmos.DrawLine(transform.position, transitionTarget);

        // Draw target position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transitionTarget, 1f);

        // If we have controller access, show actual vs effective target
        if (waterMovementcontroller != null)
        {
            Vector3 actualTarget = waterMovementcontroller.GetActualTarget();
            // OPTIMIZATION: Use sqrMagnitude for distance check
            if ((transitionTarget - actualTarget).sqrMagnitude > 0.25f) // 0.5^2
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(transitionTarget, actualTarget);
                Gizmos.DrawWireSphere(actualTarget, 0.6f);
            }
        }

        // Draw state indicator
        Vector3 statePos = transform.position + Vector3.up * 4f;
        switch (currentState)
        {
            case TransitionState.MovingToTarget:
                Gizmos.color = Color.blue;
                break;
            case TransitionState.ExitingZone:
                Gizmos.color = Color.red;
                break;
            case TransitionState.WaitingForClear:
                Gizmos.color = Color.yellow;
                break;
        }
        Gizmos.DrawWireCube(statePos, Vector3.one * 0.6f);

        // Draw minimum exit distance if exiting zone
        if (isExitingZone && sourceZone != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transitionStartPosition, minimumExitDistance);
        }

        // Draw movement direction
        if (waterMovementcontroller.npcController.rb != null)
        {
            Vector3 movementDir = waterMovementcontroller.npcController.rb.linearVelocity.normalized;
            // OPTIMIZATION: Use sqrMagnitude for check
            if (waterMovementcontroller.npcController.rb.linearVelocity.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, transform.position + movementDir * 3f);
            }
        }
    }
}