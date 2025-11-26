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
public class MonsterUnderwaterMovement : MonoBehaviour
{
    [SerializeField] private SplineNavAgent agent;
    private UnderwaterMonsterController monsterController;

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
    [SerializeField] private bool showDebugInfo = true;

    // OPTIMIZATION: Pre-calculated squared distances
    private float arriveDistanceSqr;
    private float slowDownDistanceSqr;

    // Private variables
    private Vector3 moveDirection;
    private Vector3 desiredVelocity;
    private bool hasReachedDestination = false;
    private bool isActive = false;
    private float lastTargetCheck = 0f;


    // OPTIMIZATION: Cached target
    private Vector3 cachedTarget;
    private float targetCacheTimer = 0f;
    private const float TARGET_CACHE_DURATION = 0.1f;

    public enum MovementState
    {
        Moving,
        SlowingDown,
        Arrived
    }

    [SerializeField] private MovementState currentState = MovementState.Moving;

    // Events
    public Action OnDestinationReached;

    public void Initialize(UnderwaterMonsterController controller)
    {
        monsterController = controller;

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

        // OPTIMIZATION: Stagger target checks across NPCs
        lastTargetCheck = -UnityEngine.Random.Range(0f, targetCheckInterval);

        currentState = MovementState.Moving;
    }

    private void Update()
    {
        if (!isActive) return;

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
        isActive = true;
        hasReachedDestination = false;
        currentState = MovementState.Moving;

        // Reset caches
        cachedTarget = Vector3.zero;
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

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} deactivated Complex Zone Movement");
        }
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
            cachedTarget = monsterController.target.position;
        }

        return cachedTarget;
    }

    private void UpdateNavigation()
    {
        if (monsterController.target == null || !agent.enabled) return;

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
        if (monsterController.rb == null) return;

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
        Vector3 stoppingForce = -monsterController.rb.linearVelocity * deceleration;
        stoppingForce = Vector3.ClampMagnitude(stoppingForce, maxForce);
        monsterController.rb.AddForce(stoppingForce, ForceMode.Force);

        // OPTIMIZATION: Use sqrMagnitude to avoid sqrt
        if (monsterController.rb.linearVelocity.sqrMagnitude < 0.01f)
        {
            monsterController.rb.linearVelocity = Vector3.zero;
        }
    }

    private void ApplyMovementForce(float forceMultiplier)
    {
        Vector3 velocityDifference = desiredVelocity - monsterController.rb.linearVelocity;
        Vector3 force = velocityDifference * forceMultiplier;
        force = Vector3.ClampMagnitude(force, maxForce);

        monsterController.rb.AddForce(force, ForceMode.Force);
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

    // Public interface methods
    public bool HasReachedDestination() => hasReachedDestination;
    public MovementState GetCurrentState() => currentState;
    public void SetMaxSpeed(float newSpeed) => maxSpeed = newSpeed;


}