using UnityEngine;
using System;
using Sirenix.OdinInspector;
using RootMotion.FinalIK;

/// <summary>
/// PHASE 1 UPDATED: PlayerStateManager with deferred state validation and reference safety
/// Now prevents state transitions during scene loading and reference refresh periods
/// </summary>
public class PlayerStateManager : MonoBehaviour, IManager
{
    public static PlayerStateManager Instance { get; private set; }

    [Header("State Configuration")]
    public bool enableDebugLogs = true;
    [SerializeField] private float stateTransitionDelay = 0.05f;

    [Header("Transition Safety")]
    [SerializeField] private float referenceRefreshTimeout = 2f;
    [SerializeField] private float validationDeferDelay = 0.3f;

    [Header("Current State Info")]
    [ShowInInspector, ReadOnly] private PlayerStateType currentStateType = PlayerStateType.Ground;
    [ShowInInspector, ReadOnly] private PlayerStateType previousStateType = PlayerStateType.Ground;
    [ShowInInspector, ReadOnly] private bool isTransitioning = false;

    [Header("Safety Status")]
    [ShowInInspector, ReadOnly] private bool isInSceneTransition = false;
    [ShowInInspector, ReadOnly] private bool isRefreshingReferences = false;
    [ShowInInspector, ReadOnly] private bool hasDeferredValidation = false;

    [Header("Component References - Auto-Refreshed")]
    public PlayerController playerController;
    [ShowInInspector, ReadOnly] private PlayerWaterDetector waterDetector;
    public GrounderFBBIK grounderFBBIK;
    public AimIK aimIK;
    [ShowInInspector, ReadOnly] private bool referencesValid = false;

    // State instances
    private PlayerState currentState;
    private GroundState groundState;
    private WaterState waterState;
    private VehicleState vehicleState;
    private ClimbingState climbingState;

    // Events for external systems
    public event Action<PlayerStateType, PlayerStateType> OnStateChanged;
    public event Action<PlayerStateType> OnStateEntered;
    public event Action<PlayerStateType> OnStateExited;

    // IManager implementation state
    private bool isInitialized = false;
    private bool eventSubscriptionsActive = false;

    // Deferred validation tracking
    private bool pendingValidationRequest = false;
    private float lastReferenceRefreshTime = 0f;

    // Public properties
    public PlayerStateType CurrentStateType => currentStateType;
    public PlayerState CurrentState => currentState;
    public bool IsTransitioning => isTransitioning;
    public bool IsProperlyInitialized => isInitialized && referencesValid && !isInSceneTransition && !isRefreshingReferences;

    // Safety properties
    public bool IsInSceneTransition => isInSceneTransition;
    public bool CanPerformStateOperations => IsProperlyInitialized && !pendingValidationRequest;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DebugLog("PlayerStateManager singleton created");

            // Subscribe to scene loading events to track transitions
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadingStarted;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #region Scene Transition Safety

    /// <summary>
    /// Track when scene loading starts to prevent unsafe operations
    /// </summary>
    private void OnSceneLoadingStarted(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        DebugLog($"=== SCENE LOADING DETECTED: {scene.name} ===");
        isInSceneTransition = true;
        pendingValidationRequest = false;

        // Stop any ongoing state transitions
        if (isTransitioning)
        {
            DebugLog("Stopping ongoing state transition due to scene loading");
            StopAllCoroutines();
            isTransitioning = false;
        }

        // Mark references as invalid during scene transition
        referencesValid = false;
        DebugLog("Scene transition started - state operations suspended");
    }

    /// <summary>
    /// Safe reference validation that only runs when appropriate
    /// </summary>
    private bool CanValidateState()
    {
        if (isInSceneTransition)
        {
            DebugLog("State validation blocked - in scene transition");
            return false;
        }

        if (isRefreshingReferences)
        {
            DebugLog("State validation blocked - refreshing references");
            return false;
        }

        if (!referencesValid)
        {
            DebugLog("State validation blocked - references invalid");
            return false;
        }

        if (isTransitioning)
        {
            DebugLog("State validation blocked - already transitioning");
            return false;
        }

        if (!isInitialized)
        {
            DebugLog("State validation blocked - not initialized");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Schedule validation for later when it's safe
    /// </summary>
    private void ScheduleDeferredValidation()
    {
        if (hasDeferredValidation)
        {
            DebugLog("Deferred validation already scheduled");
            return;
        }

        pendingValidationRequest = true;
        hasDeferredValidation = true;
        StartCoroutine(DeferredValidationCoroutine());
        DebugLog("Scheduled deferred state validation");
    }

    /// <summary>
    /// Coroutine that waits for safe conditions before validating
    /// </summary>
    private System.Collections.IEnumerator DeferredValidationCoroutine()
    {
        DebugLog("Starting deferred validation wait...");

        // Wait for initial delay
        yield return new WaitForSecondsRealtime(validationDeferDelay);

        // Wait until conditions are safe for validation
        float waitStart = Time.unscaledTime;
        while (!CanValidateState())
        {
            // Timeout protection
            if (Time.unscaledTime - waitStart > referenceRefreshTimeout)
            {
                Debug.LogWarning("[PlayerStateManager] Deferred validation timeout reached - forcing validation");
                break;
            }

            yield return new WaitForSecondsRealtime(0.1f);
        }

        // Perform the validation
        if (CanValidateState())
        {
            DebugLog("Executing deferred state validation");
            ValidateCurrentState();
        }
        else
        {
            Debug.LogWarning("[PlayerStateManager] Unable to execute deferred validation - conditions still unsafe");
        }

        // Clear deferred validation flags
        pendingValidationRequest = false;
        hasDeferredValidation = false;
        DebugLog("Deferred validation completed");
    }

    #endregion

    #region IManager Implementation

    public void Initialize()
    {
        DebugLog("=== INITIALIZING PLAYER STATE MANAGER ===");

        RefreshReferences();

        if (groundState == null)
        {
            CreateStateInstances();
        }

        SetInitialState();
        SubscribeToEvents();

        isInitialized = true;
        DebugLog("PlayerStateManager initialization complete");
    }

    /// <summary>
    /// Enhanced reference refresh with safety tracking
    /// </summary>
    public void RefreshReferences()
    {
        DebugLog("=== REFRESHING PLAYER STATE MANAGER REFERENCES ===");

        isRefreshingReferences = true;
        lastReferenceRefreshTime = Time.unscaledTime;

        UnsubscribeFromEvents();
        FindComponentReferences();
        ValidateReferences();

        if (isInitialized)
        {
            SubscribeToEvents();

            // End scene transition after reference refresh
            if (isInSceneTransition)
            {
                isInSceneTransition = false;
                DebugLog("Scene transition ended - state operations resumed");
            }

            StartCoroutine(DelayedStateValidation());
        }

        isRefreshingReferences = false;
        DebugLog($"References refreshed - Valid: {referencesValid}");
    }

    public void Cleanup()
    {
        DebugLog("=== CLEANING UP PLAYER STATE MANAGER ===");

        UnsubscribeFromEvents();

        if (currentState != null)
        {
            currentState.OnExit();
        }

        groundState?.OnDestroy();
        waterState?.OnDestroy();
        vehicleState?.OnDestroy();

        // Clean up scene loading subscription
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadingStarted;

        isInitialized = false;
        eventSubscriptionsActive = false;
        referencesValid = false;
        isInSceneTransition = false;
        isRefreshingReferences = false;

        DebugLog("PlayerStateManager cleanup complete");
    }

    #endregion

    #region Safe Component Access

    /// <summary>
    /// Safe method to access PlayerController with null checking
    /// </summary>
    private bool TryGetPlayerController(out PlayerController controller)
    {
        controller = playerController;

        if (controller == null)
        {
            DebugLog("PlayerController reference is null");
            return false;
        }

        // Additional Unity object validity check
        try
        {
            var _ = controller.transform; // This will throw if object is destroyed
            return true;
        }
        catch (System.Exception)
        {
            DebugLog("PlayerController reference points to destroyed object");
            playerController = null;
            controller = null;
            return false;
        }
    }

    /// <summary>
    /// Safe method to access WaterDetector with null checking
    /// </summary>
    private bool TryGetWaterDetector(out PlayerWaterDetector detector)
    {
        detector = waterDetector;

        if (detector == null)
        {
            return false;
        }

        try
        {
            var _ = detector.transform;
            return true;
        }
        catch (System.Exception)
        {
            waterDetector = null;
            detector = null;
            return false;
        }
    }

    #endregion

    private System.Collections.IEnumerator DelayedStateValidation()
    {
        yield return new WaitForSecondsRealtime(0.2f);

        if (referencesValid && !isInSceneTransition)
        {
            DebugLog("Performing delayed state validation after reference refresh");
            ValidateCurrentState();
        }
        else
        {
            DebugLog("Delayed validation skipped - conditions not safe");
        }
    }

    private void FindComponentReferences()
    {
        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
            DebugLog($"Found PlayerController: {playerController.name}");
        else
            Debug.LogWarning("[PlayerStateManager] PlayerController not found in scene!");

        waterDetector = playerController?.GetComponent<PlayerWaterDetector>();
        if (waterDetector != null)
            DebugLog($"Found PlayerWaterDetector: {waterDetector.name}");
        else
            Debug.LogWarning("[PlayerStateManager] PlayerWaterDetector not found in scene!");

        grounderFBBIK = playerController?.GetComponent<GrounderFBBIK>();
        if (grounderFBBIK != null)
            DebugLog($"Found GrounderBipedIK: {grounderFBBIK.name}");

        aimIK = playerController?.GetComponent<AimIK>();
        if (aimIK != null)
            DebugLog($"Found AimIK: {aimIK.name}");
        else
            Debug.LogWarning("[PlayerStateManager] AimIK not found in scene!");
    }

    private void ValidateReferences()
    {
        referencesValid = playerController != null && waterDetector != null;

        if (!referencesValid)
        {
            Debug.LogError($"[PlayerStateManager] Missing critical references - " +
                          $"PlayerController: {playerController != null}, " +
                          $"WaterDetector: {waterDetector != null}");
        }
        else
        {
            DebugLog("All critical references validated successfully");
        }
    }

    private void CreateStateInstances()
    {
        groundState = new GroundState(this);
        waterState = new WaterState(this);
        vehicleState = new VehicleState(this);
        climbingState = new ClimbingState(this);

        DebugLog("State instances created");
    }

    private void SetInitialState()
    {
        PlayerStateType initialState = DetermineInitialState();
        ChangeToState(initialState, true);
        DebugLog($"Initial state set to: {initialState}");
    }

    private void SubscribeToEvents()
    {
        DebugLog("=== SUBSCRIBING TO PLAYER STATE MANAGER EVENTS ===");

        if (eventSubscriptionsActive)
        {
            DebugLog("Events already subscribed, skipping");
            return;
        }

        if (TryGetWaterDetector(out var detector))
        {
            detector.OnWaterStateEntered += OnWaterStateEntered;
            detector.OnWaterStateExited += OnWaterStateExited;
            DebugLog("Subscribed to WaterDetector IMMEDIATE water state transition events");
        }
        else
        {
            Debug.LogWarning("[PlayerStateManager] WaterDetector not found - cannot subscribe to events");
        }

        if (TryGetPlayerController(out var controller))
        {
            controller.OnMovementModeChanged += OnMovementModeChanged;
            DebugLog("Subscribed to PlayerController events");
        }

        eventSubscriptionsActive = true;
        DebugLog("Event subscriptions complete");
    }

    private void UnsubscribeFromEvents()
    {
        if (!eventSubscriptionsActive)
        {
            return;
        }

        if (waterDetector != null)
        {
            waterDetector.OnWaterStateEntered -= OnWaterStateEntered;
            waterDetector.OnWaterStateExited -= OnWaterStateExited;
            DebugLog("Unsubscribed from WaterDetector events");
        }

        if (playerController != null)
        {
            playerController.OnMovementModeChanged -= OnMovementModeChanged;
            DebugLog("Unsubscribed from PlayerController events");
        }

        eventSubscriptionsActive = false;
        DebugLog("Event unsubscription complete");
    }

    private PlayerStateType DetermineInitialState()
    {
        if (TryGetWaterDetector(out var detector) && detector.IsInWaterState)
        {
            return PlayerStateType.Water;
        }

        return PlayerStateType.Ground;
    }

    private void Update()
    {
        // Enhanced safety checks
        if (!CanValidateState())
        {
            return;
        }

        currentState?.OnUpdate();
        ValidateCurrentState();
    }

    /// <summary>
    /// Safe state validation with deferred fallback
    /// </summary>
    private void ValidateCurrentState()
    {
        if (!CanValidateState())
        {
            // Schedule for later if we can't validate now
            if (!hasDeferredValidation && isInitialized)
            {
                ScheduleDeferredValidation();
            }
            return;
        }

        PlayerStateType requiredState = DetermineRequiredState();

        if (requiredState != currentStateType)
        {
            DebugLog($"State validation detected change needed: {currentStateType} -> {requiredState}");
            ChangeToState(requiredState);
        }
    }

    /// <summary>
    /// Safe state determination with reference checking
    /// </summary>
    private PlayerStateType DetermineRequiredState()
    {
        // Check vehicle state first
        if (TryGetPlayerController(out var controller01) && controller01.IsInVehicle)
        {
            return PlayerStateType.Vehicle;
        }

        //check if we're climbing next
        if (TryGetPlayerController(out var controller02) && controller02.IsClimbing)
        {
            return PlayerStateType.Climbing;
        }

        // Check water state
        if (TryGetWaterDetector(out var detector) && detector.IsInWaterState)
        {
            return PlayerStateType.Water;
        }

        return PlayerStateType.Ground;
    }

    public void ChangeToState(PlayerStateType newStateType, bool immediate = false)
    {
        // Additional safety check
        if (!CanPerformStateOperations && !immediate)
        {
            DebugLog($"State change blocked - unsafe conditions. Scheduling for later: {newStateType}");
            ScheduleDeferredValidation();
            return;
        }

        if (newStateType == currentStateType && !immediate)
        {
            return;
        }

        if (isTransitioning && !immediate)
        {
            DebugLog($"State transition already in progress, ignoring request to change to {newStateType}");
            return;
        }

        StartCoroutine(TransitionToState(newStateType, immediate));
    }

    private System.Collections.IEnumerator TransitionToState(PlayerStateType newStateType, bool immediate)
    {
        isTransitioning = true;
        previousStateType = currentStateType;

        DebugLog($"Starting state transition: {previousStateType} -> {newStateType}");

        // Exit current state
        if (currentState != null)
        {
            currentState.OnExit();
            OnStateExited?.Invoke(previousStateType);
        }

        if (!immediate && stateTransitionDelay > 0)
        {
            yield return new WaitForSeconds(stateTransitionDelay);
        }

        // Switch to new state
        currentStateType = newStateType;
        currentState = GetStateInstance(newStateType);

        // Enter new state
        if (currentState != null)
        {
            currentState.OnEnter();
            OnStateEntered?.Invoke(currentStateType);
        }

        OnStateChanged?.Invoke(previousStateType, currentStateType);

        isTransitioning = false;
        DebugLog($"State transition complete: {previousStateType} -> {currentStateType}");
    }

    private PlayerState GetStateInstance(PlayerStateType stateType)
    {
        return stateType switch
        {
            PlayerStateType.Ground => groundState,
            PlayerStateType.Water => waterState,
            PlayerStateType.Vehicle => vehicleState,
            PlayerStateType.Climbing => climbingState,
            _ => groundState
        };
    }

    #region Event Handlers

    private void OnWaterStateEntered()
    {
        if (!CanPerformStateOperations)
        {
            DebugLog("Water state entry blocked - unsafe conditions");
            return;
        }

        DebugLog("Immediate water state entry detected - transitioning to Water state");

        if (currentStateType != PlayerStateType.Vehicle)
        {
            ChangeToState(PlayerStateType.Water);
        }
    }

    private void OnWaterStateExited()
    {
        if (!CanPerformStateOperations)
        {
            DebugLog("Water state exit blocked - unsafe conditions");
            return;
        }

        DebugLog("Immediate water state exit detected, if not in vehicle or climbing will transition to Ground state");

        if (currentStateType != PlayerStateType.Vehicle && currentStateType != PlayerStateType.Climbing)
        {
            ChangeToState(PlayerStateType.Ground);
        }
        else if (currentStateType == PlayerStateType.Climbing)
        {
            DebugLog("Water state exit ignored - player is climbing");
        }
    }

    private void OnMovementModeChanged(MovementMode previousMode, MovementMode newMode)
    {
        if (!CanPerformStateOperations)
        {
            DebugLog($"Movement mode change blocked - unsafe conditions: {previousMode} -> {newMode}");
            return;
        }

        DebugLog($"Movement mode changed: {previousMode} -> {newMode}");

        PlayerStateType targetState = newMode switch
        {
            MovementMode.Swimming => PlayerStateType.Water,
            MovementMode.Vehicle => PlayerStateType.Vehicle,
            MovementMode.Climbing => PlayerStateType.Climbing,
            _ => PlayerStateType.Ground
        };

        if (targetState != currentStateType)
        {
            ChangeToState(targetState);
        }
    }

    #endregion

    #region Public API

    public bool CanUseItem(ItemData itemData)
    {
        if (currentState == null || itemData == null)
            return false;

        return currentState.CanUseItem(itemData);
    }

    public bool CanEquipItem(ItemData itemData)
    {
        if (currentState == null || itemData == null)
            return false;

        return currentState.CanEquipItem(itemData);
    }

    public MovementRestrictions GetMovementRestrictions()
    {
        return currentState?.GetMovementRestrictions() ?? new MovementRestrictions();
    }

    public string GetCurrentStateDisplayName()
    {
        return currentStateType switch
        {
            PlayerStateType.Ground => "On Land",
            PlayerStateType.Water => "In Water",
            PlayerStateType.Vehicle => "In Vehicle",
            PlayerStateType.Climbing => "Climbing",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Enhanced force validation with safety checks
    /// </summary>
    public void ForceStateValidation()
    {
        if (CanValidateState())
        {
            DebugLog("Forcing immediate state validation");
            ValidateCurrentState();
        }
        else
        {
            DebugLog("Cannot force validation - scheduling deferred validation instead");
            ScheduleDeferredValidation();
        }
    }

    public void ForceStateChange(PlayerStateType targetState)
    {
        ChangeToState(targetState, true);
        DebugLog($"Forced immediate state change to: {targetState}");
    }

    public string GetWaterDetectionDebugInfo()
    {
        if (TryGetWaterDetector(out var detector))
        {
            return detector.GetThreePointDetectionInfo();
        }
        return "No valid water detector reference";
    }

    /// <summary>
    /// Debug info including safety status
    /// </summary>
    public string GetSafetyDebugInfo()
    {
        return $"SceneTransition: {isInSceneTransition}, RefreshingRefs: {isRefreshingReferences}, " +
               $"RefsValid: {referencesValid}, CanOperate: {CanPerformStateOperations}, " +
               $"DeferredValidation: {hasDeferredValidation}, Transitioning: {isTransitioning}";
    }

    #endregion

    private void OnDestroy()
    {
        Cleanup();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerStateManager] {message}");
        }
    }
}