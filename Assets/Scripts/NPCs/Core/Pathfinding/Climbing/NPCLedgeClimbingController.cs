using UnityEngine;
using System.Collections;
using Pathfinding;
using Pathfinding.ECS;

/// <summary>
/// Handles ledge climbing with separate paths for ground and water ledges.
/// Ground ledges: Uses FollowerEntity + off-mesh link system
/// Water ledges: Pure manual force application, no FollowerEntity involvement
/// 
/// UPDATED: Now uses NPCLedgeClimbValidator for reliable success detection
/// </summary>
public class NPCLedgeClimbingController : MonoBehaviour, IOffMeshLinkHandler, IOffMeshLinkStateMachine
{
    [Header("References")]
    [SerializeField] private FollowerEntity followerEntity;
    private NPCLedgeClimbValidator climbValidator; // REMOVED [SerializeField] - get at runtime
    private NPCMovementStateMachine stateMachine;

    [Header("Base Forces - Climbing")]
    [SerializeField, Tooltip("Base upward force when climbing up")]
    private float baseClimbUpVerticalForce = 1000f;

    [SerializeField, Tooltip("Base forward force when climbing up")]
    private float baseClimbUpHorizontalForce = 400f;

    [Header("Base Forces - Dropping")]
    [SerializeField, Tooltip("Base downward force when dropping down")]
    private float baseDropDownVerticalForce = 400f;

    [SerializeField, Tooltip("Base forward force when dropping down")]
    private float baseDropDownHorizontalForce = 500f;

    [Header("Force Timing")]
    [SerializeField, Tooltip("Delay between applying vertical and horizontal forces")]
    private float horizontalForceDelay = 0.5f;

    [Header("Timeout Settings")]
    [SerializeField, Tooltip("Maximum time to allow for climb before forcing completion")]
    private float maxClimbTimeout = 5f;

    [SerializeField, Tooltip("How often to check for success/stuck state (seconds)")]
    private float validationCheckInterval = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private bool isTraversing = false;
    private ClimbableGroundLedge currentGroundLedge;
    private ClimbableWaterLedge currentWaterLedge;
    private LedgeType currentLedgeType;
    private bool isClimbingUp;

    public enum LedgeType
    {
        None,
        Ground,
        Water
    }

    public bool IsTraversing => isTraversing;
    public bool IsClimbingUp => isClimbingUp;
    public LedgeType CurrentLedgeType => currentLedgeType;

    //ledge queue system
    [Header("Queue Management")]
    [SerializeField] private bool useQueueSystem = true;
    [SerializeField] private float queueCheckInterval = 0.2f;

    private string currentLedgeID;
    private bool isInQueue = false;
    private bool hasQueueAccess = false;
    private float lastQueueCheck = 0f;

    private void Awake()
    {
        if (followerEntity == null)
            followerEntity = GetComponent<FollowerEntity>();

        if (stateMachine == null)
            stateMachine = GetComponent<NPCMovementStateMachine>();

        // NEW: Get or add validator
        if (climbValidator == null)
            climbValidator = GetComponent<NPCLedgeClimbValidator>();

        if (climbValidator == null)
        {
            climbValidator = gameObject.AddComponent<NPCLedgeClimbValidator>();
            DebugLog("Auto-added NPCLedgeClimbValidator component");
        }
    }

    private void OnEnable()
    {
        if (followerEntity != null)
        {
            followerEntity.onTraverseOffMeshLink = this;
            DebugLog("Registered with FollowerEntity");
        }
    }

    private void OnDisable()
    {
        // Clean up queue state when controller is disabled
        if (followerEntity != null)
        {
            followerEntity.onTraverseOffMeshLink = null;
        }

        // Release any active queue
        ReleaseLedgeQueue();
    }

    #region IOffMeshLinkHandler Implementation

    IOffMeshLinkStateMachine IOffMeshLinkHandler.GetOffMeshLinkStateMachine(AgentOffMeshLinkTraversalContext context)
    {
        DebugLog("GetOffMeshLinkStateMachine called");

        currentLedgeType = IdentifyLedgeType(context);

        switch (currentLedgeType)
        {
            case LedgeType.Ground:
                DebugLog($"✓ Found GROUND ledge: {currentGroundLedge.gameObject.name}");
                isClimbingUp = currentGroundLedge.IsClimbingUp(context.link.relativeStart);
                currentLedgeID = currentGroundLedge.LedgeID;

                // Queue system for ground ledges
                if (useQueueSystem)
                {
                    bool canUseNow = LedgeQueueManager.RequestLedgeUsage(currentLedgeID, stateMachine.npcController);

                    if (!canUseNow)
                    {
                        int queuePosition = LedgeQueueManager.GetQueuePosition(currentLedgeID, stateMachine.npcController);
                        float waitTime = LedgeQueueManager.GetEstimatedWaitTime(currentLedgeID, stateMachine.npcController);
                        DebugLog($"Added to GROUND ledge queue at position {queuePosition} (est. wait: {waitTime:F1}s)");
                        isInQueue = true;
                        hasQueueAccess = false;
                        // Return this to start the traversal coroutine, which will wait
                        return this;
                    }
                    else
                    {
                        DebugLog("Granted immediate access to ground ledge");
                        hasQueueAccess = true;
                        isInQueue = false;
                    }
                }

                if (followerEntity != null && !followerEntity.enabled)
                {
                    followerEntity.enabled = true;
                    DebugLog("Enabled FollowerEntity for ground ledge");
                }

                if (stateMachine != null)
                {
                    stateMachine.RequestClimbingState(currentGroundLedge, isClimbingUp, isWaterLedge: false);
                }

                return this;

            case LedgeType.Water:
                DebugLog($"✓ Found WATER ledge: {currentWaterLedge.gameObject.name}");
                currentLedgeID = currentWaterLedge.LedgeID;

                bool isOnGround = !stateMachine.npcController.waterDetector.IsInWater;

                if (!isOnGround)
                {
                    Debug.LogWarning($"{gameObject.name}: Water ledge from water - shouldn't happen via FollowerEntity!");
                    return null;
                }

                // Queue system for water ledges (DROP DOWN from ground)
                if (useQueueSystem)
                {
                    bool canUseNow = LedgeQueueManager.RequestLedgeUsage(currentLedgeID, stateMachine.npcController);

                    if (!canUseNow)
                    {
                        int queuePosition = LedgeQueueManager.GetQueuePosition(currentLedgeID, stateMachine.npcController);
                        float waitTime = LedgeQueueManager.GetEstimatedWaitTime(currentLedgeID, stateMachine.npcController);
                        DebugLog($"Added to WATER ledge DROP queue at position {queuePosition} (est. wait: {waitTime:F1}s)");
                        isInQueue = true;
                        hasQueueAccess = false;

                        // For water drops, we need to wait before jumping
                        if (stateMachine != null)
                        {
                            stateMachine.RequestClimbingState(currentWaterLedge, isClimbingUp: false, isWaterLedge: true);
                        }

                        // Start waiting coroutine
                        StartCoroutine(WaitForWaterDropAccess());

                        return null;
                    }
                    else
                    {
                        DebugLog("Granted immediate access to water drop ledge");
                        hasQueueAccess = true;
                        isInQueue = false;
                    }
                }

                DebugLog("Handling jump down into water");

                if (stateMachine != null)
                {
                    stateMachine.RequestClimbingState(currentWaterLedge, isClimbingUp: false, isWaterLedge: true);
                }

                HandleWaterLedgeJumpDown(currentWaterLedge);

                return null;

            default:
                DebugLog("Not a climbable ledge - using default traversal");
                return null;
        }
    }

    private IEnumerator WaitForWaterDropAccess()
    {
        DebugLog("Waiting for water drop queue access...");

        while (isInQueue && !hasQueueAccess)
        {
            if (Time.time - lastQueueCheck >= queueCheckInterval)
            {
                lastQueueCheck = Time.time;

                if (LedgeQueueManager.CanNPCUseLedgeNow(currentLedgeID, stateMachine.npcController))
                {
                    DebugLog("Queue access granted - executing water drop");
                    hasQueueAccess = true;
                    isInQueue = false;
                    break;
                }
            }

            yield return null;
        }

        // Now execute the water drop
        if (currentWaterLedge != null)
        {
            HandleWaterLedgeJumpDown(currentWaterLedge);
        }
    }

    #endregion

    #region Ledge Type Identification

    private LedgeType IdentifyLedgeType(AgentOffMeshLinkTraversalContext context)
    {
        if (context.link.link == null)
        {
            DebugLog("Link is null");
            return LedgeType.None;
        }

        var linkComponent = context.link.link.component;

        if (linkComponent is NodeLink2 nodeLink)
        {
            ClimbableWaterLedge waterLedge = ClimbableWaterLedge.GetWaterLedgeFromNodeLink(nodeLink);
            if (waterLedge != null)
            {
                currentWaterLedge = waterLedge;
                currentGroundLedge = null;
                return LedgeType.Water;
            }

            ClimbableGroundLedge groundLedge = ClimbableGroundLedge.GetLedgeFromNodeLink(nodeLink);
            if (groundLedge != null)
            {
                currentGroundLedge = groundLedge;
                currentWaterLedge = null;
                return LedgeType.Ground;
            }
        }

        return LedgeType.None;
    }

    #endregion

    #region Water Ledge Manual Handling

    private void HandleWaterLedgeJumpDown(ClimbableWaterLedge ledge)
    {
        if (stateMachine.npcController.rb == null)
        {
            Debug.LogError($"{gameObject.name}: No rigidbody for water ledge jump down!");
            return;
        }

        currentWaterLedge = ledge;
        currentLedgeType = LedgeType.Water;
        isClimbingUp = false;

        DebugLog($"Handling water ledge jump down: {ledge.gameObject.name}");

        StartCoroutine(ApplyWaterLedgeJumpForcesWithDelay(ledge));
    }

    private IEnumerator ApplyWaterLedgeJumpForcesWithDelay(ClimbableWaterLedge ledge)
    {
        Vector3 verticalForce = baseDropDownVerticalForce * ledge.CalculateVerticalForceMultiplier() * Vector3.up;
        stateMachine.npcController.rb.AddForce(verticalForce, ForceMode.Impulse);

        DebugLog($"Applied VERTICAL water jump force: {verticalForce}");

        yield return new WaitForSeconds(horizontalForceDelay);

        Vector3 dropDirection = ledge.GetDropDownDirection();
        Vector3 horizontalDirection = new Vector3(dropDirection.x, 0, dropDirection.z).normalized;
        Vector3 horizontalForce = baseDropDownHorizontalForce * ledge.CalculateHorizontalForceMultiplier() * horizontalDirection;

        stateMachine.npcController.rb.AddForce(horizontalForce, ForceMode.Impulse);

        DebugLog($"Applied HORIZONTAL water jump force: {horizontalForce}");
    }

    public void HandleWaterLedgeClimbUp(ClimbableWaterLedge ledge)
    {
        if (stateMachine.npcController.rb == null)
        {
            Debug.LogError($"{gameObject.name}: No rigidbody for water ledge climb!");
            return;
        }

        currentWaterLedge = ledge;
        currentLedgeType = LedgeType.Water;
        isClimbingUp = true;
        currentLedgeID = ledge.LedgeID; // Store ledge ID for queue release

        stateMachine.npcController.waterController.enabled = false; // Disable water movement

        DebugLog($"Handling water ledge climb up: {ledge.gameObject.name}");

        StartCoroutine(ApplyWaterLedgeClimbForcesWithDelay(ledge));
    }

    private IEnumerator ApplyWaterLedgeClimbForcesWithDelay(ClimbableWaterLedge ledge)
    {
        float verticalForceMultiplier = ledge.CalculateVerticalForceMultiplier();
        Vector3 verticalForce = baseClimbUpVerticalForce * verticalForceMultiplier * Vector3.up;
        stateMachine.npcController.rb.AddForce(verticalForce, ForceMode.Impulse);

        Debug.Log($"Applied VERTICAL water climb force: {verticalForce}, multiplier: {verticalForceMultiplier:F2}");

        yield return new WaitForSeconds(horizontalForceDelay);

        Vector3 climbDirection = ledge.GetClimbUpDirection();
        Vector3 horizontalDirection = new Vector3(climbDirection.x, 0, climbDirection.z).normalized;
        float horizontalForceMultiplier = ledge.CalculateHorizontalForceMultiplier();
        Vector3 horizontalForce = baseClimbUpHorizontalForce * horizontalForceMultiplier * horizontalDirection;

        stateMachine.npcController.rb.AddForce(horizontalForce, ForceMode.Impulse);

        Debug.Log($"Applied HORIZONTAL water climb force: {horizontalForce}, multiplier: {horizontalForceMultiplier:F2}");
    }

    #endregion

    #region Update Method (queue handling)

    private void Update()
    {
        // Handle queue waiting for GROUND ledges
        if (isInQueue && !hasQueueAccess && useQueueSystem && !string.IsNullOrEmpty(currentLedgeID))
        {
            if (Time.time - lastQueueCheck >= queueCheckInterval)
            {
                lastQueueCheck = Time.time;

                if (LedgeQueueManager.CanNPCUseLedgeNow(currentLedgeID, stateMachine.npcController))
                {
                    DebugLog("Queue access granted - can start ground climb");
                    hasQueueAccess = true;
                    isInQueue = false;

                    // The traversal coroutine is already waiting, it will continue now
                }
                else if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    int queuePos = LedgeQueueManager.GetQueuePosition(currentLedgeID, stateMachine.npcController);
                    DebugLog($"Still waiting in queue (position: {queuePos})");
                }
            }
        }
    }

    #endregion

    #region Ground Ledge Physics-Based Traversal - UPDATED

    System.Collections.IEnumerable IOffMeshLinkStateMachine.OnTraverseOffMeshLink(AgentOffMeshLinkTraversalContext ctx)
    {
        DebugLog("========== GROUND LEDGE TRAVERSAL STARTED ==========");

        // NEW: Wait in queue if needed
        if (useQueueSystem)
        {
            while (isInQueue && !hasQueueAccess)
            {
                if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    int queuePos = LedgeQueueManager.GetQueuePosition(currentLedgeID, stateMachine.npcController);
                    DebugLog($"Waiting in ground ledge queue (position: {queuePos})");
                }
                yield return null;
            }

            DebugLog("Queue access granted - proceeding with ground climb");
        }

        isTraversing = true;

        var start = (Vector3)ctx.link.relativeStart;
        var end = (Vector3)ctx.link.relativeEnd;
        var dir = end - start;

        DebugLog($"From {start} to {end}, direction: {(isClimbingUp ? "UP" : "DOWN")}");

        ctx.DisableLocalAvoidance();

        if (currentGroundLedge == null)
        {
            Debug.LogError($"{gameObject.name}: No ground ledge reference!");
            isTraversing = false;
            ReleaseLedgeQueue();
            yield break;
        }

        // PHASE 1: Rotate to face the ledge
        DebugLog("Phase 1: Rotating to face ledge");
        float rotationTime = 0f;
        float maxRotationTime = currentGroundLedge.PreClimbDelay;

        while (rotationTime < maxRotationTime)
        {
            if (!ctx.MoveTowards(
                position: start,
                rotation: Quaternion.LookRotation(dir, ctx.movementPlane.up),
                gravity: true,
                slowdown: true).reached)
            {
                rotationTime += ctx.deltaTime;
                yield return null;
            }
            else
            {
                break;
            }
        }

        // PHASE 2: Apply VERTICAL force
        DebugLog("Phase 2: Applying VERTICAL force");
        ApplyClimbingVerticalForce(start, isClimbingUp);

        // PHASE 3: Wait, then apply HORIZONTAL force
        DebugLog($"Phase 3: Waiting {horizontalForceDelay}s before applying HORIZONTAL force");
        float delayTimer = 0f;

        while (delayTimer < horizontalForceDelay)
        {
            delayTimer += ctx.deltaTime;
            yield return null;
        }

        DebugLog("Phase 3b: Applying HORIZONTAL force");
        ApplyClimbingHorizontalForce(start, end, isClimbingUp);

        // PHASE 4: Monitor climb progress with NEW validation system
        DebugLog("Phase 4: Monitoring climb progress");

        float climbStartTime = Time.time;
        float lastValidationCheck = Time.time;
        bool climbSuccessful = false;

        while (Time.time - climbStartTime < maxClimbTimeout)
        {
            // Check for success at intervals
            if (Time.time - lastValidationCheck >= validationCheckInterval)
            {
                lastValidationCheck = Time.time;

                // NEW: Use validator for success check
                if (climbValidator != null && climbValidator.IsGroundLedgeClimbSuccessful(end))
                {
                    climbSuccessful = true;
                    DebugLog("✓ Climb successful via validator!");
                    break;
                }
            }

            yield return null;
        }

        // Handle result
        if (!climbSuccessful)
        {
            DebugLog($"⚠ Climb timeout reached ({maxClimbTimeout}s) - snapping to target");
            ctx.transform.Position = end;
        }
        else
        {
            DebugLog("✓ Climb completed successfully!");
        }

        // PHASE 5: Cleanup
        DebugLog("Phase 5: Finalizing");

        isTraversing = false;
        DebugLog("========== GROUND LEDGE TRAVERSAL COMPLETED ==========");
    }

    private void ApplyClimbingVerticalForce(Vector3 startPosition, bool climbingUp)
    {
        if (stateMachine.npcController.rb == null || currentGroundLedge == null)
        {
            Debug.LogError($"{gameObject.name}: Cannot apply vertical force - missing components!");
            return;
        }

        float verticalForce = climbingUp ? baseClimbUpVerticalForce : baseDropDownVerticalForce;

        float multiplier = 1f;

        if (climbingUp)
            multiplier = currentGroundLedge.CalculateVerticalForceMultiplier();

        Vector3 force = Vector3.up * (verticalForce * multiplier);

        stateMachine.npcController.rb.AddForce(force, ForceMode.Impulse);

        DebugLog($"Applied VERTICAL force: {force} (magnitude: {force.magnitude:F0}, multiplier: {multiplier:F2})");
    }

    private void ApplyClimbingHorizontalForce(Vector3 startPosition, Vector3 endPosition, bool climbingUp)
    {
        if (stateMachine.npcController.rb == null || currentGroundLedge == null)
        {
            Debug.LogError($"{gameObject.name}: Cannot apply horizontal force - missing components!");
            return;
        }

        float multiplier = currentGroundLedge.CalculateHorizontalForceMultiplier();
        float horizontalForce = climbingUp ? baseClimbUpHorizontalForce : baseDropDownHorizontalForce;

        Vector3 direction = endPosition - startPosition;
        Vector3 horizontalDirection = new Vector3(direction.x, 0, direction.z).normalized;
        Vector3 force = horizontalDirection * (horizontalForce * multiplier);

        stateMachine.npcController.rb.AddForce(force, ForceMode.Impulse);

        DebugLog($"Applied HORIZONTAL force: {force} (magnitude: {force.magnitude:F0}, multiplier: {multiplier:F2})");
    }

    #endregion

    #region IOffMeshLinkStateMachine Completion Handlers

    void IOffMeshLinkStateMachine.OnFinishTraversingOffMeshLink(AgentOffMeshLinkTraversalContext context)
    {
        DebugLog("Traversal finished successfully");
        isTraversing = false;

        // Release queue
        ReleaseLedgeQueue();

        currentGroundLedge = null;
        currentWaterLedge = null;
        currentLedgeType = LedgeType.None;
    }

    // Add this helper method
    private void ReleaseLedgeQueue()
    {
        if (useQueueSystem && !string.IsNullOrEmpty(currentLedgeID))
        {
            LedgeQueueManager.ReleaseLedgeUsage(currentLedgeID, stateMachine.npcController);
            DebugLog($"Released ledge queue for {currentLedgeID}");
            currentLedgeID = null;
            isInQueue = false;
            hasQueueAccess = false;
        }
    }

    void IOffMeshLinkStateMachine.OnAbortTraversingOffMeshLink()
    {
        Debug.LogWarning($"[LEDGE ABORT] Traversal aborted!");
        isTraversing = false;

        // Release queue
        ReleaseLedgeQueue();

        currentGroundLedge = null;
        currentWaterLedge = null;
        currentLedgeType = LedgeType.None;
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[LEDGE CONTROLLER-{gameObject.name}] {message}");
        }
    }



    #endregion
}