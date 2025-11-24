using UnityEngine;
using Pathfinding;
using Sirenix.OdinInspector;
using Unity.VisualScripting;

/// <summary>
/// OPTIMIZED ground movement controller for NPCs using A* Pathfinding Project.
/// Designed for 20+ NPCs with minimal performance impact through:
/// - Staggered path updates
/// - Cached component references
/// - Squared distance checks (avoiding sqrt)
/// - Reduced Update frequency for non-critical checks
/// 
/// UPDATED: FollowerEntity lifecycle now managed by NPCMovementStateMachine
/// </summary>
public class NPCGroundMovementController : MonoBehaviour
{
    public NPCController npcController;
    [SerializeField] private NPCGroundObstacleDetector obstacleDetector;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = true;

    [Header("Ground Movement Settings")]
    public float groundDrag = 0f;

    [Header("Obstacle Jumping")]
    [SerializeField, Tooltip("Enable automatic jumping over obstacles")]
    private bool enableObstacleJumping = true;

    [SerializeField, Tooltip("Upward force applied when jumping")]
    private float jumpForce = 2000f;

    [SerializeField, Tooltip("Forward boost applied when jumping (helps clear obstacles)")]
    private float forwardJumpBoost = 500f;

    [SerializeField, Tooltip("Time that must pass between jumps")]
    private float jumpCooldown = 1f;

    // A* Pathfinding components - cached at initialization
    public FollowerEntity followerEntity;

    // Movement state
    private Vector3 currentVelocity = Vector3.zero;
    private bool hasReachedDestination = false;

    // OPTIMIZATION: Path update timing with staggering
    private float pathUpdateTimer = 0f;
    [SerializeField, Tooltip("How often to recalculate path (in seconds)")]
    private float pathUpdateInterval = 0.5f;

    // OPTIMIZATION: Cached arrival check - don't check every frame
    private float arrivalCheckTimer = 0f;
    private const float ARRIVAL_CHECK_INTERVAL = 0.1f;

    // Jump state tracking
    private float lastJumpTime = -999f;
    private bool isReadyToJump = true;

    // Events
    public event System.Action OnDestinationReached;

    // Public properties
    public bool HasReachedDestination => hasReachedDestination;
    public float CurrentSpeed => currentVelocity.magnitude;
    public Vector3 CurrentVelocity => currentVelocity;

    #region Initialization

    public void Initialize(NPCController controller)
    {
        npcController = controller;
        InitializeComponents();

        // OPTIMIZATION: Stagger initial path updates across NPCs to spread CPU load
        pathUpdateTimer = -UnityEngine.Random.Range(0f, pathUpdateInterval);
        arrivalCheckTimer = -UnityEngine.Random.Range(0f, ARRIVAL_CHECK_INTERVAL);
    }

    private void InitializeComponents()
    {
        if (followerEntity == null)
            followerEntity = GetComponent<FollowerEntity>();

        if (followerEntity == null)
        {
            Debug.LogError($"{gameObject.name}: FollowerEntity component missing!");
        }
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        if (!enabled) return;

        // OPTIMIZATION: Only update path at intervals, not every frame
        UpdatePathfinding();

        // OPTIMIZATION: Only check arrival at intervals, not every frame
        CheckArrivalAtDestination();

        // Handle obstacle jumping
        if (enableObstacleJumping)
        {
            UpdateJumpReadiness();
            CheckForObstacleJump();
        }
    }

    private void UpdatePathfinding()
    {
        pathUpdateTimer += Time.deltaTime;

        // Only update path when timer expires
        if (pathUpdateTimer >= pathUpdateInterval)
        {
            pathUpdateTimer = 0f;

            if (npcController.target != null && followerEntity != null)
            {
                followerEntity.destination = npcController.target.position;
            }
        }
    }

    private void CheckArrivalAtDestination()
    {
        arrivalCheckTimer += Time.deltaTime;

        // Only check arrival at intervals
        if (arrivalCheckTimer >= ARRIVAL_CHECK_INTERVAL)
        {
            arrivalCheckTimer = 0f;

            if (npcController.target == null || followerEntity == null) return;

            bool previousState = hasReachedDestination;

            // Check if reached destination
            if (followerEntity.reachedDestination)
            {
                hasReachedDestination = true;

                // Only fire event if this is a new arrival (state changed)
                if (!previousState)
                {
                    OnDestinationReached?.Invoke();
                }
            }
            else
            {
                hasReachedDestination = false;
            }
        }
    }

    #endregion

    #region Jumping

    /// <summary>
    /// Update jump readiness based on cooldown
    /// </summary>
    private void UpdateJumpReadiness()
    {
        float timeSinceLastJump = Time.time - lastJumpTime;
        isReadyToJump = timeSinceLastJump >= jumpCooldown;
    }

    /// <summary>
    /// Check if we should jump over an obstacle
    /// </summary>
    private void CheckForObstacleJump()
    {
        if (!isReadyToJump) return;
        if (obstacleDetector == null) return;
        if (!obstacleDetector.enabled) return;

        // Check if detector found a jumpable obstacle (not a wall)
        if (!obstacleDetector.HasObstacleAhead) return;
        if (obstacleDetector.IsTooTallToJump)
        {
            // Too tall - don't jump
            if (enableDebugLogs && Time.frameCount % 60 == 0)
            {
                DebugLog("Obstacle too tall to jump - skipping");
            }
            return;
        }

        // All conditions met - execute jump!
        ExecuteJump();
    }

    /// <summary>
    /// Execute the jump by applying forces to rigidbody
    /// </summary>
    private void ExecuteJump()
    {
        if (npcController.rb == null) return;

        // Calculate jump vector
        Vector3 jumpVector = Vector3.up * jumpForce;

        // Add forward boost to help clear obstacle
        if (forwardJumpBoost > 0f)
        {
            Vector3 forwardDirection = CurrentVelocity.normalized;
            if (forwardDirection.sqrMagnitude > 0.01f)
            {
                jumpVector += forwardDirection * forwardJumpBoost;
            }
        }

        // Apply jump force
        npcController.rb.AddForce(jumpVector, ForceMode.Impulse);

        // Update state
        lastJumpTime = Time.time;
        isReadyToJump = false;

        if (enableDebugLogs)
        {
            DebugLog($"Jumped over obstacle! Height: {obstacleDetector.ObstacleHeight:F2}m, Force: {jumpVector}");
        }
    }

    #endregion

    #region Movement Control

    public void StopMovement()
    {
        if (followerEntity != null)
        {
            followerEntity.canMove = false;
        }
    }

    public void ResumeMovement()
    {
        if (followerEntity != null)
        {
            followerEntity.canMove = true;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Check if NPC is ready to jump
    /// </summary>
    public bool IsReadyToJump() => isReadyToJump;

    /// <summary>
    /// Manually trigger a jump (for testing)
    /// </summary>
    [Button("Test Jump")]
    public void TriggerTestJump()
    {
        if (npcController.rb != null)
        {
            Vector3 jumpVector = Vector3.up * jumpForce;
            jumpVector += transform.forward * forwardJumpBoost;
            npcController.rb.AddForce(jumpVector, ForceMode.Impulse);
            lastJumpTime = Time.time;
            DebugLog("Test jump triggered!");
        }
    }

    [Button]
    private void PushInRandomDirection(float pushForce)
    {
        if (npcController.rb == null) return;

        Vector3 randomDirection = new Vector3(
            UnityEngine.Random.Range(-1f, 1f),
            0f,
            UnityEngine.Random.Range(-1f, 1f)
        ).normalized;

        npcController.rb.AddForce(randomDirection * pushForce, ForceMode.Impulse);
    }

    public void SetMaxSpeed(float speed)
    {
        if (followerEntity != null)
        {
            followerEntity.maxSpeed = speed;
        }
    }

    /// <summary>
    /// Get current movement speed
    /// </summary>
    public float GetCurrentSpeed()
    {
        if (followerEntity != null)
        {
            return followerEntity.velocity.magnitude;
        }
        return 0f;
    }

    /// <summary>
    /// Force an immediate path update (useful when target changes dramatically)
    /// </summary>
    public void ForcePathUpdate()
    {
        pathUpdateTimer = pathUpdateInterval; // Will trigger on next Update
    }

    /// <summary>
    /// Set custom path update interval for this NPC (useful for less important NPCs)
    /// </summary>
    public void SetPathUpdateInterval(float interval)
    {
        pathUpdateInterval = Mathf.Max(0.1f, interval); // Minimum 0.1s
    }

    #endregion

    #region Enable/Disable - UPDATED

    private void OnEnable()
    {
        // NOTE: FollowerEntity lifecycle is now managed by NPCMovementStateMachine
        // We no longer enable/disable it here

        // Enable obstacle detector when ground controller is enabled
        if (obstacleDetector != null)
        {
            obstacleDetector.enabled = true;
        }

        if (enableDebugLogs)
        {
            DebugLog("Ground movement controller enabled");
        }
    }

    private void OnDisable()
    {
        // CRITICAL CHANGE: We no longer disable FollowerEntity here!
        // The state machine handles FollowerEntity lifecycle to prevent ECS structural change errors

        // Disable obstacle detector when ground controller is disabled
        if (obstacleDetector != null)
        {
            obstacleDetector.enabled = false;
            obstacleDetector.ClearObstacleData();
        }

        // Reset state when disabled
        hasReachedDestination = false;

        if (enableDebugLogs)
        {
            DebugLog("Ground movement controller disabled (FollowerEntity managed by state machine)");
        }
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCGroundMovement-{gameObject.name}] {message}");
        }
    }

    public string GetMovementInfo()
    {
        if (followerEntity == null)
        {
            return "Ground State - No FollowerEntity";
        }

        return $"Ground State Active\n" +
               $"Velocity: {followerEntity.velocity}\n" +
               $"Speed: {followerEntity.velocity.magnitude:F2} m/s\n" +
               $"Has Reached Destination: {hasReachedDestination}\n" +
               $"Path Update Interval: {pathUpdateInterval:F2}s\n" +
               $"Can Move: {followerEntity.canMove}\n" +
               $"FollowerEntity Enabled: {followerEntity.enabled}";
    }

    #endregion
}