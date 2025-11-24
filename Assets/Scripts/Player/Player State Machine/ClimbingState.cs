using RootMotion.FinalIK;
using Unity.VisualScripting;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// FIXED: Climbing state - player is climbing onto a ledge.
/// This is a transitional state where all player controls are disabled
/// and the player is moved automatically to the ledge position using PHYSICS FORCES.
/// CRITICAL: Does NOT make rigidbody kinematic - allows force-based movement.
/// </summary>
public class ClimbingState : PlayerState
{
    // Climbing-specific references
    private ClimbingMovementController climbingController;
    private bool hasSubscribedToClimbEvents = false;

    public ClimbingState(PlayerStateManager manager) : base(manager) { }

    [SerializeField] private float climbTimeOutLimit = 5f;
    private float currentClimbTimer = 0f;

    protected override void InitializeState()
    {
        // Create highly restrictive movement restrictions for climbing
        movementRestrictions = CreateClimbingRestrictions();
        DebugLog("Climbing state initialized - all controls disabled except climb progression");
    }

    /// <summary>
    /// Create movement restrictions for climbing (everything disabled)
    /// </summary>
    private MovementRestrictions CreateClimbingRestrictions()
    {
        return new MovementRestrictions
        {
            // Basic movement - all disabled
            canWalk = false,
            canRun = false,
            canJump = false,
            canCrouch = false,

            // Water movement - all disabled
            canSwim = false,
            canDive = false,
            canSurface = false,

            // Vehicle movement - all disabled
            canAccelerate = false,
            canBrake = false,
            canSteer = false,

            // Special actions - all disabled
            canInteract = false,
            canUseItems = false,
            canEquipItems = false
        };
    }

    public override void OnEnter()
    {
        base.OnEnter();

        DebugLog("Entering climbing state - disabling all player controls");

        // Update player physics
        UpdatePlayerPhysicsForState();

        UpdatePlayerComponentsForState();

        // Find and setup climbing controller
        SetupClimbingController();

    }

    protected override void UpdatePlayerPhysicsForState()
    {
        // Keep rigidbody dynamic so forces can be applied
        stateManager.playerController.SetRBIsKinematic(false);

        // Keep gravity disabled
        stateManager.playerController.SetRBUseGravity(false);

        stateManager.playerController.rb.linearDamping = stateManager.playerController.groundDrag;
        stateManager.playerController.rb.angularDamping = stateManager.playerController.groundDrag;

        // disable water object
        stateManager.playerController.swimmingMovementController.waterObject.enabled = false;
    }


    protected override void UpdatePlayerComponentsForState()
    {
        // Disable player swimming rotation and depth manager
        stateManager.playerController.swimmingBodyRotation.enabled = false;
        stateManager.playerController.swimmingDepthManager.enabled = false;

        UpdatePlayerIKComponentsForState();

    }

    public override void OnExit()
    {
        base.OnExit();

        DebugLog("Exiting climbing state - restoring player controls");

        // Clean up climbing controller events
        CleanupClimbingController();
    }

    /// <summary>
    /// Configure IK components for climbing pose
    /// </summary>
    protected override void UpdatePlayerIKComponentsForState()
    {
        // Disable grounder FBBIK during climbing to prevent interference
        if (stateManager.grounderFBBIK != null)
        {
            stateManager.grounderFBBIK.enabled = false;
            DebugLog("GrounderFBBIK disabled for climbing");
        }

        // Configure AimIK for climbing - use minimal spine movement
        if (stateManager.aimIK != null)
        {
            // Reduce spine bone influence during climb for more stable pose
            stateManager.aimIK.solver.bones[0].weight = 0.3f; // Lower spine - reduced
            stateManager.aimIK.solver.bones[1].weight = 0.5f; // Mid spine - moderate  
            stateManager.aimIK.solver.bones[2].weight = 0.7f; // Upper spine - moderate
            stateManager.aimIK.solver.bones[3].weight = 0.2f; // Neck - minimal

            DebugLog("AimIK configured for climbing pose");
        }
    }

    /// <summary>
    /// Setup climbing controller and subscribe to events
    /// </summary>
    private void SetupClimbingController()
    {
        if (stateManager.playerController != null)
        {
            climbingController = stateManager.playerController.climbingMovementController;

            if (climbingController != null && !hasSubscribedToClimbEvents)
            {
                // Subscribe to climbing events
                climbingController.OnClimbStarted += OnClimbAnimationStarted;
                climbingController.OnClimbCompleted += OnClimbAnimationCompleted;
                hasSubscribedToClimbEvents = true;

                DebugLog("Subscribed to force-based climbing controller events");
            }
            else if (climbingController == null)
            {
                Debug.LogError("[ClimbingState] No ClimbingMovementController found on player!");
            }
        }
    }

    /// <summary>
    /// Clean up climbing controller event subscriptions
    /// </summary>
    private void CleanupClimbingController()
    {
        if (climbingController != null && hasSubscribedToClimbEvents)
        {
            climbingController.OnClimbStarted -= OnClimbAnimationStarted;
            climbingController.OnClimbCompleted -= OnClimbAnimationCompleted;
            hasSubscribedToClimbEvents = false;

            DebugLog("Unsubscribed from climbing controller events");
        }
    }

    /// <summary>
    /// Handle climb animation started
    /// </summary>
    private void OnClimbAnimationStarted()
    {
        DebugLog("Force-based climb animation started");
        // Could trigger additional effects here (particles, audio, etc.)
    }

    /// <summary>
    /// Handle climb animation completed - this will trigger state transition back to ground
    /// </summary>
    private void OnClimbAnimationCompleted()
    {
        DebugLog("Force-based climb animation completed - requesting transition to ground state");

        // Request transition back to ground state
        // The PlayerStateManager will handle the actual transition
        stateManager.ChangeToState(PlayerStateType.Ground);
    }

    /// <summary>
    /// Start the climbing sequence (called externally when climb should begin)
    /// </summary>
    public bool StartClimbing()
    {
        DebugLog("Request to start force-based climbing sequence");

        // Reset climb timer
        currentClimbTimer = 0f;

        if (climbingController == null)
        {
            Debug.LogError("[ClimbingState] Cannot start climbing - no climbing controller");
            return false;
        }

        if (!climbingController.CanStartClimbing())
        {
            DebugLog("Cannot start climbing - conditions not met");
            return false;
        }

        DebugLog("Starting force-based climbing sequence");
        return climbingController.StartClimbing();
    }

    #region PlayerState Abstract Implementation

    public override bool CanUseItem(ItemData itemData)
    {
        // No items can be used during climbing
        return false;
    }

    public override bool CanEquipItem(ItemData itemData)
    {
        // No items can be equipped during climbing
        return false;
    }

    public override string GetDisplayName()
    {
        return "Climbing";
    }

    public override string GetDebugInfo()
    {
        var baseInfo = base.GetDebugInfo();
        var climbInfo = new System.Text.StringBuilder();

        climbInfo.AppendLine(baseInfo);
        climbInfo.AppendLine("FIXED Climbing State: All controls and items disabled, physics forces enabled");
        climbInfo.AppendLine($"Has Climbing Controller: {climbingController != null}");
        climbInfo.AppendLine($"Events Subscribed: {hasSubscribedToClimbEvents}");

        if (climbingController != null)
        {
            climbInfo.AppendLine($"Can Start Climbing: {climbingController.CanStartClimbing()}");
            climbInfo.AppendLine($"Is Climbing: {climbingController.IsMoving}");

            // Get detailed climbing info
            if (climbingController.IsMoving)
            {
                climbInfo.AppendLine("=== CLIMBING CONTROLLER DEBUG ===");
                climbInfo.AppendLine(climbingController.GetDebugInfo());
            }
        }

        return climbInfo.ToString();
    }

    #endregion

    #region State Lifecycle

    public override void OnUpdate()
    {
        base.OnUpdate();

        // Monitor climbing progress
        if (climbingController != null && IsActive)
        {
            // Could add climbing progress monitoring here if needed
            // For now, we rely on the climbing controller's completion event

            // Debug info every few frames
            if (climbingController.IsMoving && Time.frameCount % 60 == 0)
            {
                DebugLog($"Climbing in progress - Velocity: {climbingController.GetVelocity().magnitude:F2}m/s");
            }

            // Increment climb timer
            currentClimbTimer += Time.deltaTime;

            if (currentClimbTimer >= climbTimeOutLimit)
            {
                DebugLog($"Climbing duration exceeded (likely stuck) - requesting transition to ground state");
                stateManager.ChangeToState(PlayerStateType.Ground);
            }
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        CleanupClimbingController();
    }

    #endregion
}