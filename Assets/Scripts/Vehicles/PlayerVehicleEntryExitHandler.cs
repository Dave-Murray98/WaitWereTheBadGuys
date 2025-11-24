using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ENHANCED: PlayerVehicleEntryExitHandler with restoration awareness
/// Now properly distinguishes between interactive entry (which needs collision timing)
/// and save restoration (which should be immediate and direct).
/// </summary>
public class PlayerVehicleEntryExitHandler : MonoBehaviour
{
    [Header("Transition Settings")]
    [SerializeField] private float maxTransitionTime = 3f;
    [SerializeField] private int physicsFramesToWait = 3;
    [SerializeField] private float collisionValidationDelay = 0.1f;

    public bool IsTransitioning => isTransitioning;

    [Header("UI")]
    [SerializeField] private GameObject transitionOverlay;
    [SerializeField] private bool showTransitionUI = true;

    [SerializeField] private TextMeshProUGUI vehicleTransitionUIText;
    [SerializeField] private string enteringVehicleMessage = "Entering Vehicle...";
    [SerializeField] private string exitingVehicleMessage = "Exiting Vehicle...";

    [Header("ENHANCED: Restoration Settings")]
    [SerializeField] private bool allowRestorationBypass = true;
    [SerializeField] private float restorationDetectionDelay = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDetailedLogs = false;

    // Component references
    private PlayerController playerController;
    private PlayerStateManager stateManager;
    private PlayerWaterDetector waterDetector;

    // Transition state
    [ShowInInspector, ReadOnly] private bool isTransitioning = false;
    [ShowInInspector, ReadOnly] private TransitionType currentTransition;
    [ShowInInspector, ReadOnly] private VehicleController targetVehicle;
    [ShowInInspector, ReadOnly] private PlayerStateType preTransitionState;

    // ENHANCED: Restoration detection
    [ShowInInspector, ReadOnly] private bool isRestorationInProgress = false;

    // Collision state tracking
    private LayerMask vehicleLayerMask;
    private bool collisionStateBeforeTransition;

    private enum TransitionType
    {
        None,
        EnteringVehicle,
        ExitingVehicle
    }

    private void Start()
    {
        FindComponentReferences();
        ValidateSetup();
        SetupTransitionUI();
        StartCoroutine(MonitorRestorationState());
    }

    #region Component Setup

    private void FindComponentReferences()
    {
        playerController = GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("[PlayerVehicleEntryExitHandler] No PlayerController found! This component must be on the player.");
            enabled = false;
            return;
        }

        stateManager = PlayerStateManager.Instance;
        waterDetector = playerController.waterDetector;

        if (playerController.vehicleMovementController != null)
        {
            vehicleLayerMask = playerController.vehicleMovementController.vehicleLayerMask;
        }

        if (vehicleTransitionUIText == null && transitionOverlay != null)
        {
            vehicleTransitionUIText = transitionOverlay.GetComponentInChildren<TextMeshProUGUI>();
        }

        DebugLog("Component references found and setup");
    }

    private void ValidateSetup()
    {
        if (stateManager == null)
        {
            Debug.LogWarning("[PlayerVehicleEntryExitHandler] PlayerStateManager not found. Vehicle transitions may not work correctly.");
        }

        if (waterDetector == null)
        {
            Debug.LogWarning("[PlayerVehicleEntryExitHandler] PlayerWaterDetector not found. Water-to-vehicle transitions may not work correctly.");
        }

        if (vehicleLayerMask == 0)
        {
            Debug.LogWarning("[PlayerVehicleEntryExitHandler] Vehicle layer mask not set. Collision management may not work correctly.");
        }

        DebugLog("Setup validation complete");
    }

    private void SetupTransitionUI()
    {
        if (transitionOverlay != null)
        {
            transitionOverlay.SetActive(false);
        }
        else if (showTransitionUI)
        {
            Debug.LogWarning("[PlayerVehicleEntryExitHandler] Transition overlay not assigned but showTransitionUI is enabled.");
        }
    }

    /// <summary>
    /// ENHANCED: Monitor for restoration operations to avoid conflicts
    /// </summary>
    private IEnumerator MonitorRestorationState()
    {
        while (true)
        {
            yield return new WaitForSeconds(restorationDetectionDelay);

            bool wasInRestoration = isRestorationInProgress;
            isRestorationInProgress = SceneVehicleStateManager.Instance?.IsPerformingRestoration ?? false;

            if (wasInRestoration != isRestorationInProgress)
            {
                DebugLog($"Restoration state changed: {wasInRestoration} -> {isRestorationInProgress}");
            }
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Initiate vehicle entry process with proper state management
    /// ENHANCED: Now checks for restoration conflicts
    /// </summary>
    /// <param name="vehicle">The vehicle to enter</param>
    /// <returns>True if entry process started successfully</returns>
    public bool BeginVehicleEntry(VehicleController vehicle)
    {
        // ENHANCED: Check for restoration conflicts
        if (allowRestorationBypass && isRestorationInProgress)
        {
            DebugLog("Vehicle entry rejected - restoration in progress, using direct method instead");
            return false;
        }

        if (isTransitioning)
        {
            DebugLog("Vehicle entry rejected - already transitioning");
            return false;
        }

        if (vehicle == null || !vehicle.IsOperational || vehicle.HasDriver)
        {
            DebugLog($"Vehicle entry rejected - Vehicle valid: {vehicle != null}, Operational: {vehicle?.IsOperational ?? false}, Has driver: {vehicle?.HasDriver ?? true}");
            return false;
        }

        DebugLog($"=== BEGINNING INTERACTIVE VEHICLE ENTRY: {vehicle.VehicleID} ===");
        DebugLog($"Current player state: {stateManager?.CurrentStateType ?? PlayerStateType.Ground}");

        StartCoroutine(ProcessVehicleEntry(vehicle));
        return true;
    }

    /// <summary>
    /// Initiate vehicle exit process with proper state management
    /// ENHANCED: Now checks for restoration conflicts
    /// </summary>
    /// <returns>True if exit process started successfully</returns>
    public bool BeginVehicleExit()
    {
        // ENHANCED: Allow exit even during restoration for emergency cases
        if (isRestorationInProgress)
        {
            DebugLog("Vehicle exit during restoration - proceeding with caution");
        }

        if (isTransitioning)
        {
            DebugLog("Vehicle exit rejected - already transitioning");
            return false;
        }

        if (playerController.currentVehicle == null)
        {
            DebugLog("Vehicle exit rejected - player not in vehicle");
            return false;
        }

        DebugLog($"=== BEGINNING VEHICLE EXIT: {playerController.currentVehicle.VehicleID} ===");

        StartCoroutine(ProcessVehicleExit());
        return true;
    }

    /// <summary>
    /// ENHANCED: Check if the handler can currently process vehicle operations
    /// Now considers restoration state
    /// </summary>
    public bool CanProcessVehicleOperations()
    {
        if (allowRestorationBypass && isRestorationInProgress)
        {
            DebugLog("Vehicle operations blocked - restoration in progress");
            return false;
        }

        return !isTransitioning &&
               playerController != null &&
               stateManager != null &&
               stateManager.CanPerformStateOperations;
    }

    /// <summary>
    /// ENHANCED: Check if restoration bypass is active
    /// </summary>
    public bool IsRestorationBypassActive()
    {
        return allowRestorationBypass && isRestorationInProgress;
    }

    #endregion

    #region Vehicle Entry Process

    private IEnumerator ProcessVehicleEntry(VehicleController vehicle)
    {
        // Set transition state
        isTransitioning = true;
        currentTransition = TransitionType.EnteringVehicle;
        targetVehicle = vehicle;
        preTransitionState = stateManager?.CurrentStateType ?? PlayerStateType.Ground;

        DebugLog($"Starting INTERACTIVE vehicle entry process - Pre-transition state: {preTransitionState}");

        // Show transition UI
        ShowTransitionUI(true);

        // STEP 1: Inform state manager we're transitioning (prevents other state changes)
        if (stateManager != null)
        {
            // Force transition to vehicle state immediately to prevent water->ground->vehicle transitions
            DebugLog("Forcing immediate transition to vehicle state to prevent intermediate state changes");
            stateManager.ForceStateChange(PlayerStateType.Vehicle);
        }

        // STEP 2: Store current collision state and disable vehicle collision
        yield return StartCoroutine(UpdateCollisionSettings(false));

        // STEP 3: Wait for collision updates to take effect
        yield return StartCoroutine(WaitForCollisionUpdate());

        // STEP 4: Verify collision state is correct
        bool collisionUpdateSuccess = false;
        yield return StartCoroutine(VerifyCollisionState(false, (result) => collisionUpdateSuccess = result));

        if (!collisionUpdateSuccess)
        {
            DebugLog("❌ Collision update failed - aborting vehicle entry");
            yield return StartCoroutine(AbortVehicleEntry());
            yield break;
        }

        // STEP 5: Actually enter the vehicle using INTERACTIVE method
        bool entrySuccess = false;
        yield return StartCoroutine(PerformInteractiveVehicleEntry(vehicle, (result) => entrySuccess = result));

        if (!entrySuccess)
        {
            DebugLog("❌ Vehicle entry failed - aborting");
            yield return StartCoroutine(AbortVehicleEntry());
            yield break;
        }

        // STEP 6: Final state validation and cleanup
        yield return StartCoroutine(FinalizeVehicleEntry(vehicle));

        DebugLog("✅ INTERACTIVE vehicle entry process completed successfully");
    }

    private IEnumerator UpdateCollisionSettings(bool enableCollision)
    {
        DebugLog($"Updating collision settings - Enable collision: {enableCollision}");

        // Store current collision state before changing it
        if (!enableCollision)
        {
            collisionStateBeforeTransition = CheckCurrentCollisionState();
            DetailedLog($"Stored collision state before transition: {collisionStateBeforeTransition}");
        }

        // Update collision settings
        playerController.SetLayerCollision(vehicleLayerMask, enableCollision);

        DetailedLog($"Collision settings updated - Vehicle layer collision: {enableCollision}");

        // Small delay to ensure the setting takes effect
        yield return new WaitForFixedUpdate();
    }

    private IEnumerator WaitForCollisionUpdate()
    {
        DebugLog($"Waiting {physicsFramesToWait} physics frames for collision updates...");

        for (int i = 0; i < physicsFramesToWait; i++)
        {
            yield return new WaitForFixedUpdate();
            DetailedLog($"Physics frame {i + 1}/{physicsFramesToWait} completed");
        }

        // Additional small delay for safety
        yield return new WaitForSeconds(collisionValidationDelay);

        DebugLog("Collision update wait period completed");
    }

    private IEnumerator VerifyCollisionState(bool expectedState, System.Action<bool> onComplete)
    {
        DebugLog($"Verifying collision state - Expected: {expectedState}");

        float startTime = Time.time;
        bool stateVerified = false;

        while (Time.time - startTime < maxTransitionTime * 0.3f) // Use 30% of max transition time for verification
        {
            bool currentState = CheckCurrentCollisionState();

            if (currentState == expectedState)
            {
                stateVerified = true;
                DebugLog($"✅ Collision state verified - Current: {currentState}, Expected: {expectedState}");
                break;
            }

            DetailedLog($"Collision state not yet updated - Current: {currentState}, Expected: {expectedState}");
            yield return new WaitForFixedUpdate();
        }

        if (!stateVerified)
        {
            Debug.LogWarning($"[PlayerVehicleEntryExitHandler] ⚠️ Collision state verification failed after timeout");
        }

        onComplete?.Invoke(stateVerified);
    }

    /// <summary>
    /// ENHANCED: Perform interactive vehicle entry (as opposed to direct restoration)
    /// </summary>
    private IEnumerator PerformInteractiveVehicleEntry(VehicleController vehicle, System.Action<bool> onComplete)
    {
        DebugLog("Performing INTERACTIVE vehicle entry...");

        // Call the vehicle's INTERACTIVE entry logic (this should now work without collision issues)
        bool success = vehicle.OnPlayerEnter(playerController.gameObject);

        if (success)
        {
            DebugLog($"✅ Successfully entered vehicle via INTERACTIVE method: {vehicle.VehicleID}");
        }
        else
        {
            Debug.LogError($"[PlayerVehicleEntryExitHandler] ❌ INTERACTIVE vehicle entry failed for {vehicle.VehicleID}");
        }

        onComplete?.Invoke(success);
        yield return null;
    }

    private IEnumerator FinalizeVehicleEntry(VehicleController vehicle)
    {
        DebugLog("Finalizing INTERACTIVE vehicle entry...");

        // Wait a moment for all systems to settle
        yield return new WaitForSeconds(0.1f);

        // Force state validation to ensure we're in the correct state
        if (stateManager != null)
        {
            stateManager.ForceStateValidation();
        }

        // Ensure player is positioned correctly
        playerController.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        // Update VehicleMovementController state
        playerController.vehicleMovementController.SetCurrentVehicle(targetVehicle);

        DebugLog($"Player local position after finalization: {playerController.transform.localPosition}");

        // Hide transition UI
        ShowTransitionUI(false);

        // Clear transition state
        isTransitioning = false;
        currentTransition = TransitionType.None;
        targetVehicle = null;

        DebugLog($"INTERACTIVE vehicle entry finalized - Player now in {stateManager?.CurrentStateType ?? PlayerStateType.Vehicle}");
    }

    private IEnumerator AbortVehicleEntry()
    {
        DebugLog("⚠️ Aborting INTERACTIVE vehicle entry - restoring previous state");

        // Restore collision settings
        yield return StartCoroutine(UpdateCollisionSettings(collisionStateBeforeTransition));

        // Restore previous state if needed
        if (stateManager != null && preTransitionState != PlayerStateType.Vehicle)
        {
            DebugLog($"Restoring player state to: {preTransitionState}");
            stateManager.ForceStateChange(preTransitionState);
        }

        // Hide transition UI
        ShowTransitionUI(false);

        // Clear transition state
        isTransitioning = false;
        currentTransition = TransitionType.None;
        targetVehicle = null;

        DebugLog("INTERACTIVE vehicle entry abort completed");
    }

    #endregion

    #region Vehicle Exit Process

    private IEnumerator ProcessVehicleExit()
    {
        IVehicle exitingVehicle = playerController.currentVehicle;

        // Set transition state
        isTransitioning = true;
        currentTransition = TransitionType.ExitingVehicle;

        DebugLog($"Starting vehicle exit process from: {exitingVehicle.VehicleID}");

        // Show transition UI
        ShowTransitionUI(true);

        // STEP 1: Perform vehicle exit
        yield return StartCoroutine(PerformVehicleExit(exitingVehicle));

        // STEP 2: Re-enable collision with vehicles via PlayerController
        yield return StartCoroutine(UpdateCollisionSettings(true));

        // STEP 3: Wait for collision updates
        yield return StartCoroutine(WaitForCollisionUpdate());

        // STEP 4: Let state manager determine correct state
        yield return StartCoroutine(FinalizeVehicleExit());

        DebugLog("✅ Vehicle exit process completed successfully");
    }

    private IEnumerator PerformVehicleExit(IVehicle vehicle)
    {
        DebugLog("Performing actual vehicle exit...");

        // Call the vehicle's exit logic
        vehicle.OnPlayerExit(playerController.gameObject);

        DebugLog($"✅ Successfully exited vehicle: {vehicle.VehicleID}");

        yield return null;
    }

    private IEnumerator FinalizeVehicleExit()
    {
        DebugLog("Finalizing vehicle exit...");

        // Wait for systems to settle after vehicle references are cleared
        yield return new WaitForSeconds(0.1f);

        Debug.Log($"PlayerController.IsInVehicle after exit: {playerController.IsInVehicle}, currentVehicle after exit: {playerController.currentVehicle?.VehicleID ?? "null"}");

        // CRITICAL: Verify player is no longer considered "in vehicle" before state validation
        bool playerStillInVehicle = playerController.IsInVehicle;
        if (playerStillInVehicle)
        {
            Debug.LogError($"[PlayerVehicleEntryExitHandler] Player still shows IsInVehicle=true after exit - this may indicate a timing issue");
            Debug.LogError($"currentVehicle: {playerController.currentVehicle?.VehicleID ?? "null"}");

            // Force clear as emergency backup
            playerController.currentVehicle = null;
            if (playerController.vehicleMovementController != null)
            {
                playerController.vehicleMovementController.ClearCurrentVehicle();
            }
        }

        DebugLog($"Pre-validation check - IsInVehicle: {playerController.IsInVehicle}, currentVehicle: {playerController.currentVehicle?.VehicleID ?? "null"}");

        // CRITICAL: Force state validation to determine correct post-exit state
        if (stateManager != null)
        {
            DebugLog("Forcing state validation to determine correct post-exit state");
            stateManager.ForceStateValidation();

            // Log what state was determined
            DebugLog($"State after first validation: {stateManager.CurrentStateType}");
        }

        // Wait a bit more for state transition to complete
        yield return new WaitForSeconds(0.1f);

        // Verify the state changed correctly
        if (stateManager != null)
        {
            var currentState = stateManager.CurrentStateType;
            if (currentState == PlayerStateType.Vehicle)
            {
                Debug.LogWarning($"[PlayerVehicleEntryExitHandler] State still Vehicle after exit - checking conditions...");

                // Debug the state determination logic
                bool isInVehicle = playerController.IsInVehicle;
                bool isInWater = playerController.waterDetector?.IsInWaterState ?? false;

                Debug.LogWarning($"State validation debug - IsInVehicle: {isInVehicle}, IsInWater: {isInWater}");
                Debug.LogWarning($"PlayerController.currentVehicle: {playerController.currentVehicle?.VehicleID ?? "null"}");

                // Force a second validation
                DebugLog("Forcing second state validation...");
                yield return new WaitForSeconds(0.1f);
                stateManager.ForceStateValidation();

                // Log final state for debugging
                DebugLog($"Final state after second validation: {stateManager.CurrentStateType}");
            }
            else
            {
                DebugLog($"✅ State correctly changed to: {currentState}");
            }
        }

        // Hide transition UI
        ShowTransitionUI(false);

        // Clear transition state
        isTransitioning = false;
        currentTransition = TransitionType.None;

        DebugLog($"Vehicle exit finalized - Player final state: {stateManager?.CurrentStateType ?? PlayerStateType.Ground}");
    }

    #endregion

    #region Utility Methods

    private bool CheckCurrentCollisionState()
    {
        if (playerController == null) return true;

        var playerRb = playerController.GetComponent<Rigidbody>();
        if (playerRb == null) return true;

        // Check if vehicle layer is excluded (collision disabled)
        bool collisionDisabled = (playerRb.excludeLayers & vehicleLayerMask) != 0;
        bool collisionEnabled = !collisionDisabled;

        DetailedLog($"Current collision state - Vehicle layer collision enabled: {collisionEnabled}");

        return collisionEnabled;
    }

    private void ShowTransitionUI(bool show)
    {
        if (!showTransitionUI || transitionOverlay == null) return;

        if (vehicleTransitionUIText != null)
        {
            vehicleTransitionUIText.text = currentTransition == TransitionType.EnteringVehicle ? enteringVehicleMessage : exitingVehicleMessage;
        }
        transitionOverlay.SetActive(show);
        DebugLog($"Transition UI {(show ? "shown" : "hidden")}");
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerVehicleEntryExitHandler] {message}");
        }
    }

    private void DetailedLog(string message)
    {
        if (enableDebugLogs && showDetailedLogs)
        {
            Debug.Log($"[PlayerVehicleEntryExitHandler] {message}");
        }
    }

    [Button("Debug Current State")]
    public void DebugCurrentState()
    {
        Debug.Log("=== PLAYER VEHICLE HANDLER DEBUG ===");
        Debug.Log($"Is Transitioning: {isTransitioning}");
        Debug.Log($"Current Transition: {currentTransition}");
        Debug.Log($"Target Vehicle: {targetVehicle?.VehicleID ?? "None"}");
        Debug.Log($"Pre-transition State: {preTransitionState}");
        Debug.Log($"Current Player State: {stateManager?.CurrentStateType ?? PlayerStateType.Ground}");
        Debug.Log($"Player in Vehicle: {playerController?.IsInVehicle ?? false}");
        Debug.Log($"Current Vehicle: {playerController?.currentVehicle?.VehicleID ?? "None"}");
        Debug.Log($"Collision State: {CheckCurrentCollisionState()}");
        Debug.Log($"Can Process Operations: {CanProcessVehicleOperations()}");
        Debug.Log($"Restoration In Progress: {isRestorationInProgress}");
        Debug.Log($"Restoration Bypass Active: {IsRestorationBypassActive()}");
    }

    #endregion    
}