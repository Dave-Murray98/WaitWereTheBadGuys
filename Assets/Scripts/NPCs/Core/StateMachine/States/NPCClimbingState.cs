using UnityEngine;

/// <summary>
/// Climbing state that does NOT manage FollowerEntity lifecycle.
/// FollowerEntity management is handled by NPCLedgeClimbingController based on ledge type.
/// 
/// UPDATED: Now uses NPCLedgeClimbValidator for reliable water ledge success detection
/// </summary>
public class NPCClimbingState : NPCState
{
    private NPCLedgeClimbingController climbingController;
    private NPCLedgeClimbValidator climbValidator; // NEW
    private ClimbableGroundLedge currentGroundLedge;
    private ClimbableWaterLedge currentWaterLedge;
    private bool isClimbingUp;
    private LedgeType currentLedgeType;

    private float climbStartTime;
    private const float CLIMB_TIMEOUT = 5f;
    private const float MIN_TIME_IN_STATE = 1f; // Minimum time before checking completion

    private bool hasInitiatedExit = false;

    public override string StateName => "Climbing";

    public override NPCMovementStateMachine.MovementState StateType =>
        NPCMovementStateMachine.MovementState.Climbing;

    private enum LedgeType
    {
        Ground,
        Water
    }

    public override void Initialize(NPCMovementStateMachine stateMachine)
    {
        base.Initialize(stateMachine);

        climbingController = stateMachine.npcController.GetComponent<NPCLedgeClimbingController>();

        if (climbingController == null)
        {
            Debug.LogError($"{npcTransform.name}: NPCClimbingState requires NPCLedgeClimbingController!");
        }

        // NEW: Get validator
        climbValidator = stateMachine.npcController.GetComponent<NPCLedgeClimbValidator>();

        if (climbValidator == null)
        {
            Debug.LogError($"{npcTransform.name}: NPCClimbingState requires NPCLedgeClimbValidator!");
        }
    }

    public override void OnEnter()
    {
        DebugLog("Entering climbing state");

        if (climbingController == null)
        {
            Debug.LogError($"{npcTransform.name}: Cannot enter climbing - no controller!");
            return;
        }

        SetUpCoreComponents();
        SetupStatePhysics();

        climbStartTime = Time.time;
        hasInitiatedExit = false;

        DebugLog($"Climbing started - type: {currentLedgeType}, direction: {(isClimbingUp ? "UP" : "DOWN")}");
    }

    protected override void SetUpCoreComponents()
    {
        // Disable both ground and water controllers during climb
        if (stateMachine.npcController.groundController != null)
        {
            stateMachine.npcController.groundController.enabled = false;
        }

        if (stateMachine.npcController.waterController != null)
        {
            stateMachine.npcController.waterController.enabled = false;
        }

        DebugLog("Disabled ground and water controllers");
    }

    protected override void SetupStatePhysics()
    {
        stateMachine.npcController.rb.useGravity = true;

        stateMachine.npcController.rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        stateMachine.npcController.rb.linearDamping = stateMachine.npcController.groundController.groundDrag;
        stateMachine.npcController.rb.angularDamping = stateMachine.npcController.groundController.groundDrag;
    }

    public override void OnUpdate()
    {
        if (climbingController == null || hasInitiatedExit)
            return;

        float timeInClimbing = Time.time - climbStartTime;

        // Timeout check
        if (timeInClimbing > CLIMB_TIMEOUT)
        {
            Debug.LogWarning($"{npcTransform.name}: Climbing timeout reached ({CLIMB_TIMEOUT}s) for {currentLedgeType} ledge");
            ExitClimbingState("timeout");
            return;
        }

        // Don't check completion too early (let forces do their work)
        if (timeInClimbing < MIN_TIME_IN_STATE)
            return;

        // Check if still traversing
        bool isStillTraversing = IsStillTraversing();

        if (!isStillTraversing)
        {
            DebugLog($"{currentLedgeType} ledge traversal complete - exiting climbing state");
            ExitClimbingState("completed");
        }
    }

    /// <summary>
    /// UPDATED: Uses validator for water ledge success detection
    /// </summary>
    private bool IsStillTraversing()
    {
        if (currentLedgeType == LedgeType.Water)
        {
            return IsWaterLedgeStillTraversing();
        }

        // Ground ledges - check controller state
        if (climbingController.IsTraversing)
            return true;

        if (stateMachine.npcController.followerEntity != null)
        {
            if (stateMachine.npcController.followerEntity.isTraversingOffMeshLink)
                return true;
        }

        return false;
    }

    /// <summary>
    /// NEW: Clean water ledge traversal check using validator
    /// </summary>
    private bool IsWaterLedgeStillTraversing()
    {
        if (climbValidator == null)
        {
            Debug.LogWarning($"{npcTransform.name}: No validator - falling back to basic checks");
            return FallbackWaterLedgeCheck();
        }

        // Use validator for clean success detection
        if (isClimbingUp)
        {
            // Climbing up: check if grounded (simple and reliable)
            bool climbSuccessful = climbValidator.IsWaterLedgeClimbUpSuccessful();

            if (climbSuccessful && enableDebugLogs)
            {
                DebugLog("✓ Water climb UP successful - NPC is grounded");
            }

            return !climbSuccessful; // Still traversing if NOT successful
        }
        else
        {
            // Dropping down: check if in water (simple and reliable)
            bool dropSuccessful = climbValidator.IsWaterLedgeDropDownSuccessful();

            if (dropSuccessful && enableDebugLogs)
            {
                DebugLog("✓ Water DROP DOWN successful - NPC is in water");
            }

            return !dropSuccessful; // Still traversing if NOT successful
        }
    }

    /// <summary>
    /// Fallback check if validator is missing (shouldn't happen, but safety first)
    /// </summary>
    private bool FallbackWaterLedgeCheck()
    {
        float timeInState = Time.time - climbStartTime;
        if (timeInState < MIN_TIME_IN_STATE)
        {
            return true; // Too early to judge
        }

        bool isInWater = stateMachine.npcController.waterDetector.IsInWater;
        bool isGrounded = stateMachine.npcController.groundDetector.IsGrounded;

        if (isClimbingUp)
        {
            // Climbing up: done when OUT of water AND grounded
            return isInWater || !isGrounded;
        }
        else
        {
            // Dropping down: done when IN water
            return !isInWater;
        }
    }

    public void SetCurrentWaterLedge(ClimbableWaterLedge ledge, bool climbingUp)
    {
        currentWaterLedge = ledge;
        currentGroundLedge = null;
        isClimbingUp = climbingUp;
        currentLedgeType = LedgeType.Water;

        if (enableDebugLogs)
        {
            string direction = climbingUp ? "climbing up from water" : "dropping down into water";
            DebugLog($"Set to {direction} water ledge: {ledge.gameObject.name}");
        }
    }

    public void SetCurrentLedge(ClimbableGroundLedge ledge, bool climbingUp)
    {
        currentGroundLedge = ledge;
        currentWaterLedge = null;
        isClimbingUp = climbingUp;
        currentLedgeType = LedgeType.Ground;

        if (enableDebugLogs)
        {
            string direction = climbingUp ? "climbing up" : "dropping down";
            DebugLog($"Set to {direction} ground ledge: {ledge.gameObject.name}");
        }
    }

    private void ExitClimbingState(string reason)
    {
        if (hasInitiatedExit)
            return;

        hasInitiatedExit = true;
        DebugLog($"Exiting climbing state: {reason}");

        // If climbing up from water, ensure clean transition to ground
        if (currentLedgeType == LedgeType.Water && isClimbingUp)
        {
            // Clear water zone state to prevent water controller reactivation
            if (stateMachine.npcController.waterController != null)
            {
                stateMachine.npcController.waterController.ForceClearZoneState();
                DebugLog("Cleared water zone state before ground transition");
            }

            // Force water detector to recognize we're out of water
            if (stateMachine.npcController.waterDetector != null)
            {
                stateMachine.npcController.waterDetector.ForceWaterStateCheck();
            }
        }

        stateMachine.ForceStateToGround();
    }

    public override void OnExit()
    {
        DebugLog($"Cleaning up climbing state (ledge type: {currentLedgeType})");

        // Release queue for ANY ledge type
        string ledgeID = null;

        if (currentLedgeType == LedgeType.Ground && currentGroundLedge != null)
        {
            ledgeID = currentGroundLedge.LedgeID;
        }
        else if (currentLedgeType == LedgeType.Water && currentWaterLedge != null)
        {
            ledgeID = currentWaterLedge.LedgeID;
        }

        if (!string.IsNullOrEmpty(ledgeID))
        {
            LedgeQueueManager.ReleaseLedgeUsage(ledgeID, stateMachine.npcController);
            DebugLog($"Released queue for ledge: {ledgeID}");
        }

        currentGroundLedge = null;
        currentWaterLedge = null;
        hasInitiatedExit = false;
    }


    public override bool HasReachedDestination()
    {
        return false;
    }

    public override float GetCurrentSpeed()
    {
        return 0f;
    }

    public override void SetMaxSpeed(float speed)
    {
        // Climbing speed is controlled by ledge settings
    }

    public override string GetStateInfo()
    {
        if (climbingController != null)
        {
            string direction = isClimbingUp ? "UP" : "DOWN";
            string ledgeTypeName = currentLedgeType == LedgeType.Ground ? "GROUND" : "WATER";
            string ledgeName = currentLedgeType == LedgeType.Ground
                ? (currentGroundLedge != null ? currentGroundLedge.gameObject.name : "Unknown")
                : (currentWaterLedge != null ? currentWaterLedge.gameObject.name : "Unknown");

            bool isTraversing = IsStillTraversing();
            float timeInState = Time.time - climbStartTime;
            bool followerEntityEnabled = stateMachine.npcController.followerEntity?.enabled ?? false;

            string validationInfo = "";
            if (currentLedgeType == LedgeType.Water && climbValidator != null)
            {
                if (isClimbingUp)
                {
                    bool isGrounded = climbValidator.IsWaterLedgeClimbUpSuccessful();
                    validationInfo = $"\nGrounded: {isGrounded}";
                }
                else
                {
                    bool inWater = climbValidator.IsWaterLedgeDropDownSuccessful();
                    validationInfo = $"\nIn Water: {inWater}";
                }
            }

            return $"Climbing State Active\n" +
                   $"Ledge Type: {ledgeTypeName}\n" +
                   $"Direction: {direction}\n" +
                   $"Ledge: {ledgeName}\n" +
                   $"Still Traversing: {isTraversing}\n" +
                   $"Time in State: {timeInState:F2}s\n" +
                   $"Timeout: {CLIMB_TIMEOUT}s\n" +
                   $"FollowerEntity Enabled: {followerEntityEnabled}" +
                   validationInfo;
        }
        return "Climbing State - No controller available";
    }

    public override bool CanActivate()
    {
        return climbingController != null &&
               stateMachine.npcController.followerEntity != null &&
               climbValidator != null;
    }

    public void Cleanup()
    {
        currentGroundLedge = null;
        currentWaterLedge = null;
        hasInitiatedExit = false;
    }
}