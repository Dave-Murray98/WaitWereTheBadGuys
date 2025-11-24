using Sirenix.OdinInspector;
using UnityEngine;
using Infohazard.HyperNav;

/// <summary>
/// Completely rewritten coordinator that manages three movement modes:
/// - Complex Zone: For navigation within complex areas using HyperNav
/// - Transition: For moving between zones or exiting zones 
/// - Open Water: For direct movement in open areas
/// Now includes smart target validation to handle above-water targets
/// OPTIMIZED for 10-20+ NPCs with minimal performance impact
/// </summary>
public class NPCWaterMovementController : MonoBehaviour
{
    public NPCController npcController;

    [Header("Movement Components")]
    [SerializeField] private NPCWaterComplexZoneMovement complexZoneMovement;
    [SerializeField] private NPCWaterZoneTransitionMovement transitionMovement;
    [SerializeField] private NPCOpenWaterMovement openWaterMovement;
    [SerializeField] private NPCWaterExitDetector waterExitDetector;
    [SerializeField] private SplineNavAgent navAgent;
    [SerializeField] private AvoidanceAgent avoidanceAgent;

    [Header("Water Movement Settings")]
    public float waterDrag = 5f;

    [Header("Movement Mode")]
    [SerializeField] private MovementMode currentMovementMode = MovementMode.OpenWater;

    [Header("Zone Tracking")]
    [SerializeField, ReadOnly] private UnderwaterComplexZoneTrigger currentZone;
    [SerializeField] private bool isInComplexZone = false;

    [Header("Target Validation Settings")]
    [SerializeField, Tooltip("How far below the surface to place virtual targets for airborne targets")]
    private float submergedTargetDepth = 0.5f;

    [SerializeField, Tooltip("Distance to check for ground beneath target")]
    private float groundCheckDistance = 2f;

    [SerializeField, Tooltip("Layer mask for ground detection beneath targets")]
    private LayerMask groundLayerMask = 1;

    [SerializeField, Tooltip("How close to a water exit point before transitioning to ground")]
    private float exitTransitionDistance = 1.5f;

    [Header("Performance Optimization")]
    [SerializeField, Tooltip("How often to update target validation (seconds). Higher = better performance")]
    private float targetValidationInterval = 0.1f;

    [SerializeField, Tooltip("How often to check for land exit transitions (seconds)")]
    private float exitCheckInterval = 0.2f;

    [Header("Target State (Debug)")]
    [SerializeField, ReadOnly] private Vector3 actualTarget;
    [SerializeField, ReadOnly] private Vector3 effectiveTarget;
    [SerializeField, ReadOnly] private TargetType currentTargetType;
    [SerializeField, ReadOnly] private bool isApproachingLandExit = false;

    [Header("Surface Behaviour")]
    [SerializeField, Tooltip("The distance from the surface at which we will stop the NPC from being able to apply upwards movement.")]
    private float nearSurfaceDistance = 0.5f;
    private bool isNearSurface = false;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showTargetGizmos = true;

    // OPTIMIZATION: Update timers
    private float lastTargetValidationTime = 0f;
    private float lastExitCheckTime = 0f;
    private bool targetValidationDirty = true; // Forces immediate first check

    // OPTIMIZATION: Cached ground check results
    private bool lastGroundCheckResult = false;
    private Vector3 lastGroundCheckPosition;
    private const float groundCheckCacheDistance = 1f;

    public enum MovementMode
    {
        OpenWater,      // Direct movement with obstacle avoidance
        Transition,     // Transitioning between zones or exiting zones
        ComplexZone     // HyperNav pathfinding within complex areas
    }

    public enum TargetType
    {
        Underwater,     // Target is below water surface
        OnLand,         // Target is above water and has ground beneath it
        Airborne        // Target is above water but no ground beneath it
    }

    public bool IsNearSurface => isNearSurface;

    // Events
    public System.Action<MovementMode> OnMovementModeChanged;
    public System.Action OnDestinationReached;

    public void Initialize(NPCController controller)
    {
        npcController = controller;

        InitializeComponents();
        SetupEventListeners();

        // Start in open water mode
        SwitchToMovementMode(MovementMode.OpenWater);

        // OPTIMIZATION: Stagger validation checks across NPCs
        lastTargetValidationTime = -Random.Range(0f, targetValidationInterval);
        lastExitCheckTime = -Random.Range(0f, exitCheckInterval);
    }

    private void InitializeComponents()
    {
        // Get components if not assigned
        if (complexZoneMovement == null)
            complexZoneMovement = GetComponent<NPCWaterComplexZoneMovement>();

        if (transitionMovement == null)
            transitionMovement = GetComponent<NPCWaterZoneTransitionMovement>();

        if (openWaterMovement == null)
            openWaterMovement = GetComponent<NPCOpenWaterMovement>();

        if (waterExitDetector == null)
            waterExitDetector = GetComponent<NPCWaterExitDetector>();

        if (navAgent == null)
            navAgent = GetComponent<SplineNavAgent>();

        if (avoidanceAgent == null)
            avoidanceAgent = GetComponent<AvoidanceAgent>();

        // Initialize components
        if (complexZoneMovement != null)
            complexZoneMovement.Initialize(this);

        if (transitionMovement != null)
            transitionMovement.Initialize(this);

        if (openWaterMovement != null)
            openWaterMovement.Initialize(this);

        // Validate required components
        if (complexZoneMovement == null)
            Debug.LogError($"{gameObject.name}: NPCWaterComplexZoneMovement component missing!");

        if (transitionMovement == null)
            Debug.LogError($"{gameObject.name}: NPCWaterZoneTransitionMovement component missing!");

        if (openWaterMovement == null)
            Debug.LogError($"{gameObject.name}: NPCOpenWaterMovement component missing!");

        if (navAgent == null)
            Debug.LogError($"{gameObject.name}: SplineNavAgent component missing!");
    }

    private void SetupEventListeners()
    {
        // Complex Zone Movement events
        if (complexZoneMovement != null)
        {
            complexZoneMovement.OnDestinationReached += HandleDestinationReached;
            complexZoneMovement.OnTransitionRequested += HandleTransitionRequested;
        }

        // Transition Movement events
        if (transitionMovement != null)
        {
            transitionMovement.OnDestinationReached += HandleDestinationReached;
            transitionMovement.OnTransitionComplete += HandleTransitionComplete;
        }

        // Open Water Movement events
        if (openWaterMovement != null)
        {
            openWaterMovement.OnDestinationReached += HandleDestinationReached;
        }
    }

    private void Update()
    {
        UpdateIsNearSurface();

        // OPTIMIZATION: Only update target validation at intervals
        if (Time.time - lastTargetValidationTime >= targetValidationInterval || targetValidationDirty)
        {
            lastTargetValidationTime = Time.time;
            UpdateTargetValidation();
            targetValidationDirty = false;
        }

        // OPTIMIZATION: Only check for land exit transitions at intervals
        if (Time.time - lastExitCheckTime >= exitCheckInterval)
        {
            lastExitCheckTime = Time.time;
            CheckForLandExitTransition();
        }
    }

    private void UpdateIsNearSurface()
    {
        isNearSurface = npcController.waterDetector.DepthInWater <=
            npcController.waterDetector.WaterEntryDepth + nearSurfaceDistance;
    }

    #region Target Validation System - OPTIMIZED

    /// <summary>
    /// Updates the effective target based on actual target position and water state
    /// OPTIMIZED: Only runs at specified intervals
    /// NOW INCLUDES: Water exit detection for land targets
    /// </summary>
    private void UpdateTargetValidation()
    {
        if (npcController.target == null)
        {
            Debug.Log("WaterMovementController: npcController.target is null, cannot validate target");
            effectiveTarget = transform.position;
            currentTargetType = TargetType.Underwater;
            return;
        }

        actualTarget = npcController.target.position;
        currentTargetType = DetermineTargetType(actualTarget);

        // Calculate effective target based on type
        switch (currentTargetType)
        {
            case TargetType.Underwater:
                // Target is underwater - use it directly
                effectiveTarget = actualTarget;
                break;

            case TargetType.OnLand:
                // Target is on land - check if we should use a water exit
                effectiveTarget = CalculateEffectiveTargetForLandTarget(actualTarget);
                break;

            case TargetType.Airborne:
                // Target is airborne - create submerged tracking point
                effectiveTarget = npcController.waterDetector.GetSubmergedPosition(actualTarget, submergedTargetDepth);
                break;
        }

        if (Time.frameCount % 120 == 0)
        {
            DebugLog($"{gameObject.name} Target validation - Type: {currentTargetType}, " +
                      $"Actual: {actualTarget}, Effective: {effectiveTarget}");
        }
    }

    /// <summary>
    /// Calculates the effective target for land targets
    /// Checks for water exits first, but only uses them if they provide a better path
    /// </summary>
    private Vector3 CalculateEffectiveTargetForLandTarget(Vector3 landTarget)
    {
        // Check if we have a water exit detector with a valid exit
        if (waterExitDetector != null && waterExitDetector.HasValidExit)
        {
            ClimbableWaterLedge bestExit = waterExitDetector.BestExitLedge;
            Vector3 exitPoint = waterExitDetector.BestExitEntryPoint;

            // Calculate distances
            float directDistanceToTarget = Vector3.Distance(transform.position, landTarget);
            float distanceToExit = Vector3.Distance(transform.position, exitPoint);
            float exitToTarget = Vector3.Distance(exitPoint, landTarget);
            float totalExitPathDistance = distanceToExit + exitToTarget;

            // CRITICAL: Only use water exit if:
            // 1. The target is above water (already confirmed)
            // 2. The exit is reasonably close to us
            // 3. Using the exit doesn't add too much extra distance
            // 4. The exit actually has a valid path to the target (from the detector's path calculation)

            const float maxExitDetour = 1.5f; // Allow up to 50% extra distance via exit
            const float maxExitDistance = 30f; // Don't consider exits that are too far away

            bool exitIsReasonablyClose = distanceToExit < maxExitDistance;
            bool exitIsNotMajorDetour = totalExitPathDistance < (directDistanceToTarget * maxExitDetour);

            if (exitIsReasonablyClose && exitIsNotMajorDetour)
            {
                // Check if the exit's pathfinding actually found a route to the target
                // The detector already calculated this, we just need to verify it exists
                if (waterExitDetector.ShouldUseWaterExit(landTarget))
                {
                    if (enableDebugLogs && Time.frameCount % 120 == 0)
                    {
                        DebugLog($"Using water exit: {bestExit.gameObject.name} " +
                                $"(direct: {directDistanceToTarget:F1}m, via exit: {totalExitPathDistance:F1}m)");
                    }
                    return exitPoint;
                }
            }
            else if (enableDebugLogs && Time.frameCount % 120 == 0)
            {
                if (!exitIsReasonablyClose)
                {
                    DebugLog($"Water exit too far: {distanceToExit:F1}m > {maxExitDistance}m");
                }
                if (!exitIsNotMajorDetour)
                {
                    DebugLog($"Water exit is major detour: {totalExitPathDistance:F1}m vs direct {directDistanceToTarget:F1}m");
                }
            }
        }

        // No valid water exit - use direct approach to shore
        if (enableDebugLogs && Time.frameCount % 120 == 0)
        {
            DebugLog($"No suitable water exit - swimming directly to shore");
        }

        return CalculateLandExitPoint(landTarget);
    }

    /// <summary>
    /// Determines what type of target we're dealing with
    /// OPTIMIZED: Ground checks are cached
    /// </summary>
    private TargetType DetermineTargetType(Vector3 targetPosition)
    {
        // Check if target is above water (uses shared cache in NPCWaterDetector)
        bool isAboveWater = npcController.waterDetector.IsPositionAboveWater(targetPosition);

        if (!isAboveWater)
        {
            return TargetType.Underwater;
        }

        // Target is above water - check if it has ground beneath it
        // OPTIMIZATION: Cache ground check results if position hasn't changed much
        bool hasGroundBeneath;
        if (Vector3.Distance(targetPosition, lastGroundCheckPosition) < groundCheckCacheDistance)
        {
            hasGroundBeneath = lastGroundCheckResult;
        }
        else
        {
            hasGroundBeneath = CheckForGroundBeneathPosition(targetPosition);
            lastGroundCheckResult = hasGroundBeneath;
            lastGroundCheckPosition = targetPosition;
        }

        return hasGroundBeneath ? TargetType.OnLand : TargetType.Airborne;
    }

    /// <summary>
    /// Checks if there's ground beneath a position (for land exit detection)
    /// OPTIMIZED: Results are cached per position
    /// </summary>
    private bool CheckForGroundBeneathPosition(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 0.5f;
        return Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, groundLayerMask);
    }

    /// <summary>
    /// Calculates the effective target position based on target type
    /// </summary>
    private Vector3 CalculateEffectiveTarget(Vector3 targetPosition, TargetType targetType)
    {
        switch (targetType)
        {
            case TargetType.Underwater:
                // Target is underwater - use it directly
                return targetPosition;

            case TargetType.OnLand:
                // Target is on land - create exit point at water surface
                return CalculateLandExitPoint(targetPosition);

            case TargetType.Airborne:
                // Target is airborne - create submerged tracking point
                return npcController.waterDetector.GetSubmergedPosition(targetPosition, submergedTargetDepth);

            default:
                return targetPosition;
        }
    }

    /// <summary>
    /// Calculates an appropriate water exit point for land targets
    /// </summary>
    private Vector3 CalculateLandExitPoint(Vector3 landTargetPosition)
    {
        // Get water surface height at target location (uses shared cache)
        float waterHeight = npcController.waterDetector.GetWaterSurfaceHeightAt(landTargetPosition);

        // Place exit point just below the surface
        float exitY = waterHeight - (npcController.waterDetector.WaterEntryDepth * 0.5f);

        // Keep the horizontal position of the actual target
        return new Vector3(landTargetPosition.x, exitY, landTargetPosition.z);
    }

    /// <summary>
    /// Checks if NPC is close enough to a land exit point to transition to ground mode
    /// OPTIMIZED: Only runs at specified intervals
    /// </summary>
    private void CheckForLandExitTransition()
    {
        // Only relevant when target is on land and we're not already transitioning
        if (currentTargetType != TargetType.OnLand)
        {
            isApproachingLandExit = false;
            return;
        }

        // OPTIMIZATION: Use squared distance to avoid sqrt
        float sqrDistanceToExit = (transform.position - effectiveTarget).sqrMagnitude;
        float sqrExitDistance = exitTransitionDistance * exitTransitionDistance;
        isApproachingLandExit = sqrDistanceToExit <= sqrExitDistance;

        // If we're very close and near the surface, we might be ready to exit water
        if (isApproachingLandExit && isNearSurface)
        {
            // Check if we're getting grounded
            if (npcController.groundDetector.IsGrounded)
            {
                DebugLog($"{gameObject.name} detected ground contact at water exit - ready for state transition");
            }
        }
    }

    /// <summary>
    /// Check if we're currently navigating toward a water exit
    /// </summary>
    public bool IsNavigatingToWaterExit()
    {
        if (currentTargetType != TargetType.OnLand)
            return false;

        if (waterExitDetector == null || !waterExitDetector.HasValidExit)
            return false;

        // Check if our effective target matches the water exit entry point
        Vector3 exitPoint = waterExitDetector.BestExitEntryPoint;
        float distanceToEffectiveTarget = Vector3.Distance(effectiveTarget, exitPoint);

        return distanceToEffectiveTarget < 0.5f; // Effective target is the water exit
    }

    /// <summary>
    /// Gets the effective target position that movement modes should use
    /// This abstracts away the complexity of above-water target handling
    /// </summary>
    public Vector3 GetEffectiveTarget()
    {
        return effectiveTarget;
    }

    /// <summary>
    /// Gets the actual target position (for visualization/debugging)
    /// </summary>
    public Vector3 GetActualTarget()
    {
        return actualTarget;
    }

    /// <summary>
    /// Gets the current target type
    /// </summary>
    public TargetType GetTargetType()
    {
        return currentTargetType;
    }

    /// <summary>
    /// Checks if we should allow upward movement (for movement modes to use)
    /// </summary>
    public bool ShouldAllowUpwardMovement()
    {
        // Allow upward movement if:
        // 1. We're approaching a land exit point (need to surface)
        // 2. We're not near the surface (plenty of room to move up)
        return isApproachingLandExit || !isNearSurface;
    }

    /// <summary>
    /// Forces an immediate target validation update (call when target changes)
    /// </summary>
    public void ForceTargetValidation()
    {
        targetValidationDirty = true;
    }

    #endregion

    #region Zone Trigger Events

    /// <summary>
    /// Called when NPC enters a complex zone trigger
    /// </summary>
    public void OnEnteredComplexZone(UnderwaterComplexZoneTrigger zoneTrigger)
    {
        if (npcController.climbingController.IsClimbingUp && npcController.climbingController.CurrentLedgeType == NPCLedgeClimbingController.LedgeType.Water)
        {
            // Ignore zone entry while climbing up out of water
            return;
        }

        currentZone = zoneTrigger;
        isInComplexZone = true;

        DebugLog($"{gameObject.name} entered complex zone: {zoneTrigger.name}");

        // If we're not already in a transition, switch to complex zone movement
        if (currentMovementMode != MovementMode.Transition)
        {
            SwitchToComplexZoneMovement();
        }
    }

    /// <summary>
    /// Called when NPC exits a complex zone trigger
    /// </summary>
    public void OnExitedComplexZone(UnderwaterComplexZoneTrigger zoneTrigger)
    {
        // Only process if exiting our current zone
        if (currentZone != zoneTrigger) return;


        currentZone = null;
        isInComplexZone = false;

        if (npcController.climbingController.IsClimbingUp && npcController.climbingController.CurrentLedgeType == NPCLedgeClimbingController.LedgeType.Water)
        {
            // Ignore zone exit while climbing up out of water
            return;
        }

        DebugLog($"{gameObject.name} exited complex zone: {zoneTrigger.name}");

        // Switch to open water if we're not in transition mode
        if (currentMovementMode != MovementMode.Transition)
        {
            SwitchToOpenWaterMovement();
        }
    }

    /// <summary>
    /// Force clear zone state (called when exiting water via climb)
    /// </summary>
    public void ForceClearZoneState()
    {
        if (currentZone != null || isInComplexZone)
        {
            DebugLog($"{gameObject.name} forcing zone state clear");

            // Deactivate current movement mode to prevent issues
            DeactivateCurrentMovementMode();

            currentZone = null;
            isInComplexZone = false;

            // Disable NavAgent if it's active
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.enabled = false;
            }
        }
    }

    #endregion

    #region Event Handlers

    private void HandleDestinationReached()
    {

        {
            DebugLog($"{gameObject.name} reached destination using {currentMovementMode} movement");
        }

        OnDestinationReached?.Invoke();
    }

    /// <summary>
    /// Called when complex zone movement requests a transition (target is outside zone)
    /// </summary>
    private void HandleTransitionRequested(Vector3 finalTarget)
    {

        DebugLog($"{gameObject.name} transition requested to {finalTarget}");

        // Use effective target for transition (handles above-water targets)
        Vector3 exitPoint = GetExitPointTowardsTarget();
        StartZoneExitTransition(GetEffectiveTarget(), exitPoint);
    }

    /// <summary>
    /// Called when transition movement completes
    /// </summary>
    private void HandleTransitionComplete()
    {

        DebugLog($"{gameObject.name} transition completed");

        // Determine next movement mode based on current zone status
        if (isInComplexZone)
        {
            SwitchToComplexZoneMovement();
        }
        else
        {
            SwitchToOpenWaterMovement();
        }
    }

    #endregion

    #region Movement Mode Management

    /// <summary>
    /// Switch to complex zone movement (HyperNav pathfinding)
    /// </summary>
    public void SwitchToComplexZoneMovement()
    {
        if (currentMovementMode == MovementMode.ComplexZone) return;

        DebugLog($"{gameObject.name} switching to Complex Zone Movement");

        // Enable NavAgent for complex movement
        if (navAgent != null)
        {
            navAgent.enabled = true;
        }

        SwitchToMovementMode(MovementMode.ComplexZone);
    }

    /// <summary>
    /// Switch to open water movement (direct movement)
    /// </summary>
    public void SwitchToOpenWaterMovement()
    {
        if (currentMovementMode == MovementMode.OpenWater) return;

        DebugLog($"{gameObject.name} switching to Open Water Movement");

        // Disable NavAgent for open water movement
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        SwitchToMovementMode(MovementMode.OpenWater);
    }

    /// <summary>
    /// Switch to transition movement
    /// </summary>
    public void SwitchToTransitionMovement()
    {
        if (currentMovementMode == MovementMode.Transition) return;

        DebugLog($"{gameObject.name} switching to Transition Movement");


        // Disable NavAgent during transitions
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        SwitchToMovementMode(MovementMode.Transition);
    }

    private void SwitchToMovementMode(MovementMode newMode)
    {
        // Deactivate current movement mode
        DeactivateCurrentMovementMode();

        // Update current mode
        MovementMode previousMode = currentMovementMode;
        currentMovementMode = newMode;

        // Activate new movement mode
        ActivateCurrentMovementMode();


        {
            DebugLog($"{gameObject.name} switched from {previousMode} to {newMode}");
        }

        // Notify listeners
        OnMovementModeChanged?.Invoke(currentMovementMode);
    }

    private void DeactivateCurrentMovementMode()
    {
        switch (currentMovementMode)
        {
            case MovementMode.ComplexZone:
                if (complexZoneMovement != null)
                {
                    complexZoneMovement.DeactivateMovement();
                    complexZoneMovement.enabled = false;
                }
                break;

            case MovementMode.Transition:
                if (transitionMovement != null)
                {
                    transitionMovement.StopTransition();
                    transitionMovement.enabled = false;
                }
                break;

            case MovementMode.OpenWater:
                if (openWaterMovement != null)
                {
                    openWaterMovement.DeactivateMovement();
                    openWaterMovement.enabled = false;
                }
                break;
        }
    }

    private void ActivateCurrentMovementMode()
    {
        switch (currentMovementMode)
        {
            case MovementMode.ComplexZone:
                if (complexZoneMovement != null)
                {
                    complexZoneMovement.enabled = true;
                    complexZoneMovement.ActivateMovement();
                }
                break;

            case MovementMode.Transition:
                if (transitionMovement != null)
                {
                    transitionMovement.enabled = true;
                    // Transition activation is handled separately in StartZoneExitTransition
                }
                break;

            case MovementMode.OpenWater:
                if (openWaterMovement != null)
                {
                    openWaterMovement.enabled = true;
                    openWaterMovement.ActivateMovement();
                }
                break;
        }
    }

    #endregion

    #region Transition Management

    /// <summary>
    /// Start a transition to exit the current zone
    /// </summary>
    private void StartZoneExitTransition(Vector3 finalTarget, Vector3 exitPoint)
    {
        // Reset complex zone movement transition request
        if (complexZoneMovement != null)
        {
            complexZoneMovement.ResetTransitionRequest();
        }

        // Switch to transition mode
        SwitchToTransitionMovement();

        // Start the transition movement with effective target
        if (transitionMovement != null)
        {
            transitionMovement.StartTransition(finalTarget, exitingZone: true, currentZone);
        }

        DebugLog($"{gameObject.name} started zone exit transition to {finalTarget}");

    }

    /// <summary>
    /// Start a general transition to a target (not necessarily exiting a zone)
    /// </summary>
    public void StartTransitionTo(Vector3 targetPosition)
    {
        SwitchToTransitionMovement();

        if (transitionMovement != null)
        {
            // Use effective target for transitions
            transitionMovement.StartTransition(GetEffectiveTarget(), exitingZone: false);
        }
        DebugLog($"{gameObject.name} started transition to {targetPosition}");

    }

    #endregion

    #region Zone Helper Methods

    /// <summary>
    /// Check if current target is within the current complex zone
    /// </summary>
    public bool IsTargetInCurrentZone()
    {
        if (npcController.target == null || currentZone == null)
            return false;

        // Use effective target for zone checks
        return currentZone.IsPositionInZone(GetEffectiveTarget());
    }

    /// <summary>
    /// Get the best exit point from current zone towards the target
    /// </summary>
    public Vector3 GetExitPointTowardsTarget()
    {
        if (currentZone == null || npcController.target == null)
            return transform.position;

        // Use effective target for exit point calculation
        return currentZone.GetExitPointTowards(GetEffectiveTarget());
    }

    /// <summary>
    /// Get current zone
    /// </summary>
    public UnderwaterComplexZoneTrigger GetCurrentZone() => currentZone;

    #endregion

    #region Public Interface

    public MovementMode GetCurrentMovementMode() => currentMovementMode;
    public bool IsInComplexZone() => isInComplexZone;

    /// <summary>
    /// Check if NPC has reached its destination
    /// </summary>
    public bool HasReachedDestination()
    {
        switch (currentMovementMode)
        {
            case MovementMode.ComplexZone:
                return complexZoneMovement != null && complexZoneMovement.HasReachedDestination();

            case MovementMode.Transition:
                return false; // Transitions don't count as reaching destination

            case MovementMode.OpenWater:
                return openWaterMovement != null && openWaterMovement.HasReachedDestination();

            default:
                return false;
        }
    }

    /// <summary>
    /// Set maximum movement speed for all movement modes
    /// </summary>
    public void SetMaxSpeed(float newSpeed)
    {
        if (complexZoneMovement != null)
            complexZoneMovement.SetMaxSpeed(newSpeed);

        if (transitionMovement != null)
            transitionMovement.SetTransitionSpeed(newSpeed);

        if (openWaterMovement != null)
            openWaterMovement.SetMaxSpeed(newSpeed);
    }

    /// <summary>
    /// Get detailed movement information for debugging
    /// </summary>
    public string GetMovementInfo()
    {
        string zoneInfo = isInComplexZone ? $" (Zone: {currentZone?.name ?? "Unknown"})" : " (Open Water)";
        string navAgentInfo = navAgent != null ? (navAgent.enabled ? " NavAgent:ON" : " NavAgent:OFF") : " NavAgent:Missing";
        string targetInfo = $" Target:{currentTargetType}";

        // ADD THIS:
        string waterExitInfo = "";
        if (waterExitDetector != null && waterExitDetector.HasValidExit)
        {
            waterExitInfo = $" WaterExit:{waterExitDetector.BestExitLedge.gameObject.name}@{waterExitDetector.BestExitDistance:F1}m";
            if (IsNavigatingToWaterExit())
            {
                waterExitInfo += " [NAVIGATING]";
            }
        }

        switch (currentMovementMode)
        {
            case MovementMode.ComplexZone:
                if (complexZoneMovement != null)
                {
                    return $"Complex Zone - State: {complexZoneMovement.GetCurrentState()}{zoneInfo}{navAgentInfo}{targetInfo}{waterExitInfo}";
                }
                break;

            case MovementMode.Transition:
                if (transitionMovement != null)
                {
                    return $"Transition - State: {transitionMovement.GetCurrentState()}{zoneInfo}{navAgentInfo}{targetInfo}{waterExitInfo}";
                }
                break;

            case MovementMode.OpenWater:
                if (openWaterMovement != null)
                {
                    return $"Open Water - State: {openWaterMovement.GetCurrentState()}{zoneInfo}{navAgentInfo}{targetInfo}{waterExitInfo}";
                }
                break;
        }

        return "Unknown";
    }

    /// <summary>
    /// Force switch to specific movement mode (for testing/debugging)
    /// </summary>
    public void ForceMovementMode(MovementMode mode)
    {
        switch (mode)
        {
            case MovementMode.ComplexZone:
                SwitchToComplexZoneMovement();
                break;
            case MovementMode.Transition:
                SwitchToTransitionMovement();
                break;
            case MovementMode.OpenWater:
                SwitchToOpenWaterMovement();
                break;
        }

        DebugLog($"{gameObject.name} forced to {mode} movement mode");

    }

    /// <summary>
    /// Check if currently in transition mode
    /// </summary>
    public bool IsInTransition() => currentMovementMode == MovementMode.Transition;

    #endregion

    #region Cleanup

    private void OnEnable()
    {
        //Debug.Log("EMABLING NPCWaterMovementController");
        navAgent.enabled = true;
        avoidanceAgent.enabled = true;
        openWaterMovement.enabled = true;
        complexZoneMovement.enabled = true;
        transitionMovement.enabled = true;
    }

    private void OnDisable()
    {
        //Debug.Log("DISABLING NPCWaterMovementController");
        navAgent.enabled = false;
        avoidanceAgent.enabled = false;
        openWaterMovement.enabled = false;
        complexZoneMovement.enabled = false;
        transitionMovement.enabled = false;
    }

    private void OnDestroy()
    {
        // Clean up event listeners
        if (complexZoneMovement != null)
        {
            complexZoneMovement.OnDestinationReached -= HandleDestinationReached;
            complexZoneMovement.OnTransitionRequested -= HandleTransitionRequested;
        }

        if (transitionMovement != null)
        {
            transitionMovement.OnDestinationReached -= HandleDestinationReached;
            transitionMovement.OnTransitionComplete -= HandleTransitionComplete;
        }

        if (openWaterMovement != null)
        {
            openWaterMovement.OnDestinationReached -= HandleDestinationReached;
        }
    }

    #endregion

    #region Debug Visualization

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"{gameObject.name}: {message}");
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || enabled == false) return;

        if (npcController.target == null) return;

        // Draw line to actual target
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, actualTarget);
        Gizmos.DrawWireSphere(actualTarget, 0.5f);

        // Draw line to effective target with color based on target type
        Color effectiveColor = currentTargetType switch
        {
            TargetType.Underwater => Color.cyan,
            TargetType.OnLand => Color.green,
            TargetType.Airborne => Color.yellow,
            _ => Color.white
        };

        Gizmos.color = effectiveColor;
        Gizmos.DrawLine(transform.position, effectiveTarget);
        Gizmos.DrawWireSphere(effectiveTarget, 0.8f);

        // Draw target type indicator
        if (showTargetGizmos)
        {
            Vector3 targetIndicator = effectiveTarget + Vector3.up * 1.5f;
            Gizmos.color = effectiveColor;
            Gizmos.DrawWireCube(targetIndicator, Vector3.one * 0.5f);
        }

        // Draw movement mode indicator above NPC
        Vector3 modeIndicatorPos = transform.position + Vector3.up * 3f;
        Color lineColor = currentMovementMode switch
        {
            MovementMode.ComplexZone => Color.magenta,
            MovementMode.Transition => Color.yellow,
            MovementMode.OpenWater => Color.cyan,
            _ => Color.white
        };

        Gizmos.color = lineColor;
        Gizmos.DrawWireCube(modeIndicatorPos, Vector3.one * 0.6f);

        // Draw land exit approach indicator
        if (isApproachingLandExit)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.4f);
        }

        // Zone-specific visualization
        if (isInComplexZone && currentZone != null)
        {
            // Draw line to zone center
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, currentZone.transform.position);

            // Show target location relative to zone
            if (npcController.target != null)
            {
                bool targetInZone = IsTargetInCurrentZone();
                Gizmos.color = targetInZone ? Color.green : Color.red;
                Gizmos.DrawWireCube(effectiveTarget + Vector3.up * 2f, Vector3.one * 0.4f);

                // Show exit point if target is outside zone
                if (!targetInZone)
                {
                    Vector3 exitPoint = GetExitPointTowardsTarget();
                    Gizmos.color = new Color(1f, 0.5f, 0f);
                    Gizmos.DrawWireSphere(exitPoint, 1.2f);
                    Gizmos.DrawLine(transform.position, exitPoint);
                }
            }
        }

        // NavAgent status indicator
        if (navAgent != null)
        {
            Vector3 navIndicatorPos = transform.position + Vector3.up * 4f;
            Gizmos.color = navAgent.enabled ? Color.green : Color.red;
            Gizmos.DrawWireSphere(navIndicatorPos, 0.25f);
        }

        // Movement-specific visualizations
        switch (currentMovementMode)
        {
            case MovementMode.Transition:
                if (transitionMovement != null && transitionMovement.IsActive())
                {
                    Vector3 transitionTarget = transitionMovement.GetTransitionTarget();
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(transform.position, transitionTarget);
                    Gizmos.DrawWireSphere(transitionTarget, 0.8f);
                }
                break;
        }
    }

    #endregion
}