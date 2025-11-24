using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// CLEANED: Simplified state machine with proper save/load integration.
/// Removes confusion between initialization and restoration.
/// 
/// STATE PRIORITY (highest to lowest):
/// 1. Climbing - Cannot be interrupted, highest priority
/// 2. Water - When submerged
/// 3. Ground - When on solid ground
/// 4. Air - Fallback when neither grounded nor in water
/// </summary>
public class NPCMovementStateMachine : MonoBehaviour
{
    [HideInInspector] public NPCController npcController;

    [Header("Detection")]
    [SerializeField, Tooltip("Water detector component")]
    private NPCWaterDetector waterDetector;

    [Header("Current State")]
    [SerializeField, ReadOnly] private MovementState currentStateType = MovementState.Ground;
    [SerializeField, ReadOnly] private bool isProcessingTransition = false;

    [Header("Settings")]
    [SerializeField, Tooltip("Should this NPC be able to swim?")]
    private bool canSwim = true;

    [SerializeField, Tooltip("Delay after water detection before state change")]
    private float stateChangeDelay = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showStateIndicator = true;
    [SerializeField] private bool showDetailedTransitionLogs = false;

    // State management
    private Dictionary<MovementState, NPCState> states;
    private NPCState currentState;
    private Coroutine transitionCoroutine;

    // Initialization tracking
    private float lastWaterDetectionTime = 0f;
    private bool waterDetectorReady = false;
    private bool isInitialized = false;

    public enum MovementState
    {
        Ground,
        Water,
        Air,
        Climbing
    }

    // Events
    public event System.Action<MovementState> OnStateChanged;
    public event System.Action OnDestinationReached;

    // Public properties
    public MovementState CurrentStateType => currentStateType;
    public bool IsOnGround => currentStateType == MovementState.Ground;
    public bool IsInWater => currentStateType == MovementState.Water;
    public bool IsInAir => currentStateType == MovementState.Air;
    public bool IsClimbing => currentStateType == MovementState.Climbing;
    public bool CanSwim => canSwim;
    public bool EnableDebugLogs => enableDebugLogs;
    public bool IsTransitioning => isProcessingTransition;
    public bool IsInitialized => isInitialized;

    #region Initialization

    public void Initialize(NPCController controller)
    {
        npcController = controller;
        ValidateComponents();
        InitializeStates();
    }

    private void Start()
    {
        StartCoroutine(InitializeAfterFrame());
    }

    /// <summary>
    /// CLEANED: Simplified initialization - no restoration confusion
    /// </summary>
    private IEnumerator InitializeAfterFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();

        DebugLog("Beginning state machine initialization");

        SetupEventListeners();
        yield return StartCoroutine(WaitForWaterDetectorReady());
        yield return StartCoroutine(DetermineInitialState());

        waterDetectorReady = true;
        isInitialized = true;

        DebugLog("State machine initialization complete");
    }

    private IEnumerator WaitForWaterDetectorReady()
    {
        if (waterDetector == null)
        {
            DebugLog("No water detector - using ground state");
            yield break;
        }

        int maxWaitFrames = 10;
        int frameCount = 0;

        while (frameCount < maxWaitFrames)
        {
            if (waterDetector.SceneHasWater)
            {
                DebugLog("Water detector ready");
                yield return new WaitForFixedUpdate();
                break;
            }
            frameCount++;
            yield return new WaitForFixedUpdate();
        }
    }

    private void ValidateComponents()
    {
        if (waterDetector == null)
            waterDetector = GetComponent<NPCWaterDetector>();

        if (waterDetector == null)
        {
            Debug.LogError($"{gameObject.name}: NPCWaterDetector required!");
        }
    }

    private void InitializeStates()
    {
        states = new Dictionary<MovementState, NPCState>();

        NPCGroundState groundState = new NPCGroundState();
        groundState.Initialize(this);
        states[MovementState.Ground] = groundState;

        NPCWaterState waterState = new NPCWaterState();
        waterState.Initialize(this);
        states[MovementState.Water] = waterState;

        NPCAirState airState = new NPCAirState();
        airState.Initialize(this);
        states[MovementState.Air] = airState;

        NPCClimbingState climbingState = new NPCClimbingState();
        climbingState.Initialize(this);
        states[MovementState.Climbing] = climbingState;

        DebugLog("States initialized (Ground, Water, Air, Climbing)");
    }

    private void SetupEventListeners()
    {
        if (waterDetector != null)
        {
            waterDetector.OnEnteredWater += HandleWaterEntered;
            waterDetector.OnExitedWater += HandleWaterExited;
        }
    }

    private IEnumerator DetermineInitialState()
    {
        if (waterDetector != null)
        {
            waterDetector.ForceWaterStateCheck();
            yield return new WaitForFixedUpdate();
        }

        MovementState initialState = DetermineAppropriateState();
        DebugLog($"Initial state: {initialState}");

        currentStateType = initialState;
        currentState = states[initialState];

        currentState.OnEnter();

        DebugLog($"Initial state set to {initialState}");
    }

    #endregion

    #region Save/Load Integration - CLEANED

    /// <summary>
    /// CLEANED: Called by NPCController when save/load starts
    /// Simply disables movement - no initialization confusion
    /// </summary>
    public void PrepareForRestoration()
    {
        DebugLog("=== SAVE/LOAD: Preparing for restoration ===");

        if (!isInitialized)
        {
            DebugLog("Not yet initialized - skipping preparation");
            return;
        }

        // Disable all movement systems
        DisableAllMovementSystems();

        DebugLog("Movement systems disabled for restoration");
    }

    /// <summary>
    /// CLEANED: Called by NPCController when save/load completes
    /// Re-enables and validates state
    /// </summary>
    public void CompleteRestoration()
    {
        DebugLog("=== SAVE/LOAD: Restoration complete ===");

        if (!isInitialized)
        {
            DebugLog("Not yet initialized - restoration will happen after init");
            return;
        }

        // Re-enable the state machine
        enabled = true;
        DebugLog("State machine re-enabled");

        // Re-enable movement systems
        EnableCurrentMovementSystems();

        // Validate we're in the correct state for the new position
        StartCoroutine(ValidateStateAfterRestoration());
    }

    /// <summary>
    /// Validates and corrects state after position restoration
    /// </summary>
    private IEnumerator ValidateStateAfterRestoration()
    {
        DebugLog("Validating state after restoration...");

        // Wait a moment for physics to settle at new position
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // Force check detectors at new position
        if (waterDetector != null)
        {
            waterDetector.ForceWaterStateCheck();
            yield return new WaitForFixedUpdate();
        }

        // Determine what state we should be in at this position
        MovementState appropriateState = DetermineAppropriateState();

        if (appropriateState != currentStateType)
        {
            DebugLog($"State correction needed: {currentStateType} -> {appropriateState}");
            ForceStateTransition(appropriateState, "post-restoration validation");
        }
        else
        {
            DebugLog($"State validated - {currentStateType} is correct");
        }
    }

    /// <summary>
    /// Disables all movement-related systems for restoration
    /// </summary>
    private void DisableAllMovementSystems()
    {
        // Disable the current state
        if (currentState != null)
        {
            currentState.OnExit();
        }

        // Disable FollowerEntity if present
        if (npcController.followerEntity != null)
        {
            npcController.followerEntity.enabled = false;
        }

        // Disable controllers
        if (npcController.groundController != null)
        {
            npcController.groundController.enabled = false;
        }

        if (npcController.waterController != null)
        {
            npcController.waterController.enabled = false;
        }

        // Disable this state machine
        enabled = false;

        DebugLog("All movement systems disabled");
    }

    /// <summary>
    /// Re-enables the current state's movement systems
    /// </summary>
    private void EnableCurrentMovementSystems()
    {
        if (currentState != null)
        {
            DebugLog($"Re-enabling movement systems for {currentStateType}");
            currentState.OnEnter();
        }
    }

    /// <summary>
    /// Forces an immediate state transition (used after restoration)
    /// </summary>
    private void ForceStateTransition(MovementState newState, string reason)
    {
        if (!states[newState].CanActivate())
        {
            DebugLog($"Cannot force transition to {newState} - requirements not met");
            return;
        }

        DebugLog($"Forcing state transition: {currentStateType} -> {newState} ({reason})");

        // Exit current state
        if (currentState != null)
        {
            currentState.OnExit();
        }

        // Update state
        currentStateType = newState;
        currentState = states[newState];

        // Enter new state
        currentState.OnEnter();

        OnStateChanged?.Invoke(currentStateType);
    }

    #endregion

    #region State Determination

    /// <summary>
    /// Determines appropriate state based on detectors
    /// </summary>
    private MovementState DetermineAppropriateState()
    {
        // Don't change climbing state here - it manages its own exit
        if (currentStateType == MovementState.Climbing)
        {
            return MovementState.Climbing;
        }

        // Check water (high priority)
        if (waterDetector != null && waterDetector.IsInWater && canSwim)
        {
            return MovementState.Water;
        }

        // Check ground
        if (npcController.groundDetector != null && npcController.groundDetector.IsGrounded)
        {
            return MovementState.Ground;
        }

        // Fallback to air
        return MovementState.Air;
    }

    /// <summary>
    /// Force transition to ground state (used when exiting climbing)
    /// </summary>
    public void ForceStateToGround()
    {
        DebugLog("Forcing transition to Ground state");

        // Cancel any existing transition
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
            isProcessingTransition = false;
        }

        // Execute immediate transition to ground
        ExecuteStateChange(MovementState.Ground, "climb completed");
    }

    #endregion

    #region Water Detection Event Handling

    private void HandleWaterEntered()
    {
        if (!waterDetectorReady || !isInitialized) return;
        if (currentStateType == MovementState.Climbing) return;

        lastWaterDetectionTime = Time.time;

        if (!canSwim)
        {
            DebugLog("Water detected but cannot swim");
            return;
        }

        if (showDetailedTransitionLogs)
        {
            DebugLog($"Water entered - current: {currentStateType}");
        }

        RequestStateChange(MovementState.Water, "water entered");
    }

    private void HandleWaterExited()
    {
        if (!waterDetectorReady || !isInitialized) return;
        if (currentStateType == MovementState.Climbing) return;

        lastWaterDetectionTime = Time.time;

        if (showDetailedTransitionLogs)
        {
            DebugLog($"Water exited - current: {currentStateType}");
        }

        MovementState targetState = DetermineAppropriateState();
        RequestStateChange(targetState, "water exited");
    }

    #endregion

    #region Climbing State Management

    /// <summary>
    /// Request climbing state
    /// </summary>
    public void RequestClimbingState(object ledge, bool isClimbingUp, bool isWaterLedge = false)
    {
        if (currentStateType == MovementState.Climbing)
        {
            DebugLog("Already climbing - ignoring request");
            return;
        }

        if (!states[MovementState.Climbing].CanActivate())
        {
            DebugLog("Cannot activate climbing state");
            return;
        }

        // Set ledge info on climbing state
        if (states[MovementState.Climbing] is NPCClimbingState climbState)
        {
            if (isWaterLedge)
            {
                if (ledge is ClimbableWaterLedge waterLedge)
                {
                    DebugLog($"Water ledge climbing requested: {waterLedge.gameObject.name}");
                    climbState.SetCurrentWaterLedge(waterLedge, isClimbingUp);
                }
                else
                {
                    Debug.LogError("Invalid water ledge type!");
                    return;
                }
            }
            else
            {
                if (ledge is ClimbableGroundLedge groundLedge)
                {
                    DebugLog($"Ground ledge climbing requested: {groundLedge.gameObject.name}");
                    climbState.SetCurrentLedge(groundLedge, isClimbingUp);
                }
                else
                {
                    Debug.LogError("Invalid ground ledge type!");
                    return;
                }
            }
        }

        // Cancel existing transition
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        // Direct transition to climbing
        ExecuteStateChange(MovementState.Climbing, "climbing requested");
    }

    #endregion

    #region State Transition Management

    private void RequestStateChange(MovementState newState, string reason)
    {
        // Never interrupt climbing
        if (currentStateType == MovementState.Climbing && newState != MovementState.Climbing)
        {
            if (showDetailedTransitionLogs)
            {
                DebugLog($"State change to {newState} blocked - currently climbing");
            }
            return;
        }

        // Already in requested state?
        if (newState == currentStateType && !isProcessingTransition)
        {
            if (showDetailedTransitionLogs)
            {
                DebugLog($"Already in {newState} - ignoring");
            }
            return;
        }

        // Can the state be activated?
        if (!states[newState].CanActivate())
        {
            DebugLog($"Cannot activate {newState} ({reason})");
            return;
        }

        if (showDetailedTransitionLogs)
        {
            DebugLog($"State change requested: {currentStateType} → {newState} ({reason})");
        }

        // Cancel existing transition
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(ProcessStateTransition(newState, reason));
    }

    private IEnumerator ProcessStateTransition(MovementState newState, string reason)
    {
        isProcessingTransition = true;

        if (showDetailedTransitionLogs)
        {
            DebugLog($"Starting transition: {currentStateType} → {newState}");
        }

        // Small delay for non-climbing transitions
        if (stateChangeDelay > 0f && newState != MovementState.Climbing)
        {
            yield return new WaitForSeconds(stateChangeDelay);
        }

        // Validate transition still needed (skip for climbing)
        if (newState != MovementState.Climbing)
        {
            MovementState appropriateState = DetermineAppropriateState();
            if (appropriateState != newState)
            {
                if (showDetailedTransitionLogs)
                {
                    DebugLog($"Transition no longer needed - cancelling");
                }
                isProcessingTransition = false;
                transitionCoroutine = null;
                yield break;
            }
        }

        ExecuteStateChange(newState, reason);

        isProcessingTransition = false;
        transitionCoroutine = null;
    }

    private void ExecuteStateChange(MovementState newState, string reason)
    {
        if (newState == currentStateType)
        {
            DebugLog($"Already in {newState}");
            return;
        }

        MovementState previousState = currentStateType;
        DebugLog($"Executing state change: {previousState} → {newState} ({reason})");

        // Exit current state
        if (currentState != null)
        {
            currentState.OnExit();
            if (showDetailedTransitionLogs)
            {
                DebugLog($"Exited {previousState}");
            }
        }

        // Update state
        currentStateType = newState;
        currentState = states[newState];

        // Enter new state
        currentState.OnEnter();
        if (showDetailedTransitionLogs)
        {
            DebugLog($"Entered {newState}");
        }

        DebugLog($"State transition completed: {previousState} → {newState}");

        OnStateChanged?.Invoke(currentStateType);
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        if (currentState != null && !isProcessingTransition && waterDetectorReady && isInitialized)
        {
            currentState.OnUpdate();
        }

        // Check for automatic state transitions
        if (waterDetectorReady && isInitialized && !isProcessingTransition)
        {
            CheckForAutomaticStateTransitions();
        }
    }

    private void CheckForAutomaticStateTransitions()
    {
        MovementState appropriateState = DetermineAppropriateState();

        if (appropriateState != currentStateType)
        {
            string reason = GetTransitionReason(appropriateState);
            RequestStateChange(appropriateState, reason);
        }
    }

    private string GetTransitionReason(MovementState targetState)
    {
        switch (targetState)
        {
            case MovementState.Air:
                return "lost ground and water contact";
            case MovementState.Ground:
                return "landed on ground";
            case MovementState.Water:
                return "entered water";
            case MovementState.Climbing:
                return "started climbing";
            default:
                return "automatic state check";
        }
    }

    #endregion

    #region Public API

    public bool HasReachedDestination()
    {
        if (currentState != null && !isProcessingTransition)
        {
            return currentState.HasReachedDestination();
        }
        return false;
    }

    public float GetCurrentSpeed()
    {
        if (currentState != null && !isProcessingTransition)
        {
            return currentState.GetCurrentSpeed();
        }
        return 0f;
    }

    public void SetMaxSpeed(float speed)
    {
        foreach (var state in states.Values)
        {
            state.SetMaxSpeed(speed);
        }
        DebugLog($"Max speed set to {speed}");
    }

    public void ForceState(MovementState forcedState)
    {
        if (currentStateType == MovementState.Climbing && forcedState != MovementState.Climbing)
        {
            Debug.LogWarning($"{gameObject.name}: Cannot force state while climbing!");
            return;
        }

        if (forcedState == MovementState.Water && !canSwim)
        {
            Debug.LogWarning($"{gameObject.name}: Cannot force water - NPC cannot swim!");
            return;
        }

        if (!states[forcedState].CanActivate())
        {
            Debug.LogWarning($"{gameObject.name}: Cannot force {forcedState} - requirements not met!");
            return;
        }

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
            isProcessingTransition = false;
        }

        DebugLog($"Forcing state to {forcedState}");
        ExecuteStateChange(forcedState, "forced");
    }

    public void SetSwimmingCapability(bool canSwimValue)
    {
        bool wasAbleToSwim = canSwim;
        canSwim = canSwimValue;

        if (states.ContainsKey(MovementState.Water) && states[MovementState.Water] is NPCWaterState waterState)
        {
            waterState.SetSwimmingCapability(canSwimValue);
        }

        if (!canSwim && currentStateType == MovementState.Water)
        {
            MovementState appropriateState = DetermineAppropriateState();
            ForceState(appropriateState);
        }
        else if (canSwim && !wasAbleToSwim && waterDetector.IsInWater)
        {
            RequestStateChange(MovementState.Water, "swimming enabled");
        }

        DebugLog($"Swimming capability: {wasAbleToSwim} → {canSwim}");
    }

    public string GetStateInfo()
    {
        string speedInfo = GetCurrentSpeed().ToString("F2");
        string waterInfo = waterDetector != null ? waterDetector.GetWaterStateInfo() : "No detector";
        string groundInfo = npcController.groundDetector != null ? $"Grounded: {npcController.groundDetector.IsGrounded}" : "No ground detector";
        string currentStateInfo = currentState != null ? currentState.GetStateInfo() : "No state";
        string transitionInfo = isProcessingTransition ? " (TRANSITIONING)" : "";
        string initInfo = isInitialized ? "" : " [INITIALIZING]";
        string climbingInfo = currentStateType == MovementState.Climbing ? " [CLIMBING]" : "";
        string followerEntityInfo = npcController.followerEntity != null ?
            $"FollowerEntity: {(npcController.followerEntity.enabled ? "ENABLED" : "DISABLED")}" :
            "FollowerEntity: MISSING";

        return $"Movement State Machine{initInfo}{climbingInfo}\n" +
               $"Current: {currentStateType}{transitionInfo}\n" +
               $"Speed: {speedInfo}\n" +
               $"Can Swim: {canSwim}\n" +
               $"Ground: {groundInfo}\n" +
               $"Water: {waterInfo}\n" +
               $"{followerEntityInfo}\n" +
               $"Reached Destination: {HasReachedDestination()}\n" +
               $"Details:\n{currentStateInfo}";
    }

    public void NotifyDestinationReached()
    {
        DebugLog($"Destination reached in {currentStateType}");
        OnDestinationReached?.Invoke();
    }

    public void RefreshStateFromDetectors()
    {
        if (!waterDetectorReady || waterDetector == null || !isInitialized) return;
        if (currentStateType == MovementState.Climbing) return;

        waterDetector.ForceWaterStateCheck();
        StartCoroutine(DelayedStateRefresh());
    }

    private IEnumerator DelayedStateRefresh()
    {
        yield return new WaitForFixedUpdate();

        MovementState desiredState = DetermineAppropriateState();

        if (desiredState != currentStateType)
        {
            DebugLog($"State refresh: correcting to {desiredState}");
            RequestStateChange(desiredState, "state refresh");
        }
        else
        {
            DebugLog("State refresh: current state correct");
        }
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCMovementStateMachine-{gameObject.name}] {message}");
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        if (waterDetector != null)
        {
            waterDetector.OnEnteredWater -= HandleWaterEntered;
            waterDetector.OnExitedWater -= HandleWaterExited;
        }

        if (states != null)
        {
            foreach (var state in states.Values)
            {
                if (state is NPCGroundState groundState)
                    groundState.Cleanup();
                else if (state is NPCWaterState waterState)
                    waterState.Cleanup();
                else if (state is NPCAirState airState)
                    airState.Cleanup();
                else if (state is NPCClimbingState climbingState)
                    climbingState.Cleanup();
            }
        }
    }

    #endregion
}