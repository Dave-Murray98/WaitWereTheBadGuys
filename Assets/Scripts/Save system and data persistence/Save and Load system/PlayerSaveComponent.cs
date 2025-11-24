using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// PlayerSaveComponent with improved vehicle restoration coordination
/// Now properly coordinates with SceneVehicleStateManager to avoid position conflicts
/// and ensures proper restoration timing when loading saves with player in vehicle.
/// </summary>
public class PlayerSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private PlayerData playerData;
    [SerializeField] private PlayerWaterDetector waterDetector;
    [SerializeField] private PlayerStateManager playerStateManager;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    [Header("Validation Settings")]
    [SerializeField] private bool enableMovementValidation = true;
    [SerializeField] private float validationDelay = 0.3f;

    [Header("Vehicle Restoration Settings")]
    [SerializeField] private float vehicleRestorationCheckDelay = 0.5f;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "Player_Main";
        autoGenerateID = false;
        base.Awake();

        if (autoFindReferences)
        {
            FindPlayerReferences();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Now also finds PlayerStateManager reference
    /// </summary>
    private void FindPlayerReferences()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();

        if (playerManager == null)
            playerManager = GetComponent<PlayerManager>() ?? FindFirstObjectByType<PlayerManager>();

        if (waterDetector == null)
            waterDetector = GetComponent<PlayerWaterDetector>() ?? FindFirstObjectByType<PlayerWaterDetector>();

        if (playerStateManager == null)
            playerStateManager = GetComponent<PlayerStateManager>() ?? FindFirstObjectByType<PlayerStateManager>();

        if (playerData == null && GameManager.Instance != null)
            playerData = GameManager.Instance.playerData;

        DebugLog($"Auto-found references - Controller: {playerController != null}, Manager: {playerManager != null}, " +
                $"Data: {playerData != null}, WaterDetector: {waterDetector != null}, StateManager: {playerStateManager != null}");
    }

    /// <summary>
    /// Now validates PlayerStateManager reference
    /// </summary>
    private void ValidateReferences()
    {
        if (playerController == null)
            Debug.LogError($"[{name}] PlayerController reference missing! Position/abilities won't be saved.");

        if (playerManager == null)
            Debug.LogError($"[{name}] PlayerManager reference missing! Health won't be saved.");

        if (waterDetector == null)
            Debug.LogError($"[{name}] PlayerWaterDetector reference missing! Movement validation won't work properly.");

        if (playerStateManager == null)
            Debug.LogError($"[{name}] PlayerStateManager reference missing! State validation won't work properly.");

        if (playerData == null)
            Debug.LogWarning($"[{name}] PlayerData reference missing! Some default values unavailable.");
    }

    /// <summary>
    /// Modified to handle climbing state specially - saves as ground state at ledge position
    /// </summary>
    public override object GetDataToSave()
    {
        var saveData = new PlayerSaveData();

        // Extract position and abilities from PlayerController
        if (playerController != null)
        {
            // CLIMBING INTEGRATION: Handle climbing state specially
            Vector3 positionToSave = playerController.transform.position;
            MovementMode movementModeToSave = playerController.CurrentMovementMode;
            PlayerStateType playerStateToSave = playerStateManager?.CurrentStateType ?? PlayerStateType.Ground;

            // If player is currently climbing, save as ground state at ledge position
            if (playerStateToSave == PlayerStateType.Climbing)
            {
                DebugLog("Player is climbing - converting to ground state at ledge position for save");

                // Get the target ledge position from climbing controller
                positionToSave = GetClimbingTargetPosition();

                // Save as ground state instead of climbing
                movementModeToSave = MovementMode.Ground;
                playerStateToSave = PlayerStateType.Ground;

                DebugLog("Climbing state converted to ground state for save");
            }

            saveData.position = positionToSave;
            saveData.rotation = playerController.transform.eulerAngles;
            saveData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            saveData.canJump = playerController.canJump;
            saveData.canSprint = playerController.canSprint;
            saveData.canCrouch = playerController.canCrouch;

            // Use the potentially modified movement mode and player state
            saveData.savedMovementMode = movementModeToSave;
            saveData.savedPlayerState = playerStateToSave;
            saveData.wasInWater = waterDetector?.IsInWaterState ?? false;

            DebugLog($"Extracted position: {saveData.position}, movement mode: {saveData.savedMovementMode}, " +
                    $"player state: {saveData.savedPlayerState}, in water: {saveData.wasInWater}");
        }

        // Extract health data from PlayerManager
        if (playerManager != null)
        {
            saveData.currentHealth = playerManager.currentHealth;
            if (playerData != null)
            {
                saveData.maxHealth = playerData.maxHealth;
            }
            DebugLog($"Extracted health: {saveData.currentHealth}/{saveData.maxHealth}");
        }

        // Extract settings from PlayerData
        if (playerData != null)
        {
            saveData.lookSensitivity = playerData.lookSensitivity;
        }

        DebugLog($"Save data created: {saveData.GetMovementDebugInfo()}");
        return saveData;
    }

    /// <summary>
    /// Helper method to get the target ledge position from climbing controller
    /// </summary>
    private Vector3 GetClimbingTargetPosition()
    {
        if (playerController?.climbingMovementController == null)
        {
            DebugLog("No climbing movement controller found");
            return Vector3.zero;
        }

        return playerController.climbingMovementController.GetClimbTargetPositionForSave();

    }

    /// <summary>
    /// Handles new PlayerStateType in save data
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("Extracting player save data for persistence");

        if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Validate movement state consistency
            if (!playerSaveData.IsMovementStateConsistent())
            {
                Debug.LogWarning($"Movement state inconsistency detected: {playerSaveData.GetMovementDebugInfo()}");
            }

            DebugLog($"Extracted player save data: {playerSaveData.GetMovementDebugInfo()}");
            return playerSaveData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            PlayerSaveData extractedData = new()
            {
                currentHealth = persistentData.currentHealth,
                canJump = persistentData.canJump,
                canSprint = persistentData.canSprint,
                canCrouch = persistentData.canCrouch,
                position = Vector3.zero, // Persistent data doesn't contain position
                rotation = Vector3.zero
            };

            // Try to get additional data from dynamic storage
            PlayerSaveData fullPlayerData = persistentData.GetComponentData<PlayerSaveData>(SaveID);
            if (fullPlayerData != null)
            {
                extractedData.lookSensitivity = fullPlayerData.lookSensitivity;
                extractedData.masterVolume = fullPlayerData.masterVolume;
                extractedData.sfxVolume = fullPlayerData.sfxVolume;
                extractedData.musicVolume = fullPlayerData.musicVolume;
                extractedData.level = fullPlayerData.level;
                extractedData.experience = fullPlayerData.experience;
                extractedData.maxHealth = fullPlayerData.maxHealth;
                extractedData.currentScene = fullPlayerData.currentScene;

                // Extract both movement mode and player state
                extractedData.savedMovementMode = fullPlayerData.savedMovementMode;
                extractedData.savedPlayerState = fullPlayerData.savedPlayerState;
                extractedData.wasInWater = fullPlayerData.wasInWater;

                extractedData.MergeCustomDataFrom(fullPlayerData);
            }

            DebugLog($"Extracted from persistent data: {extractedData.GetMovementDebugInfo()}");
            return extractedData;
        }

        DebugLog($"Invalid save data type - got {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Handles new PlayerStateType in unified save extraction
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data");

        var playerSaveData = new PlayerSaveData
        {
            currentHealth = unifiedData.currentHealth,
            canJump = unifiedData.canJump,
            canSprint = unifiedData.canSprint,
            canCrouch = unifiedData.canCrouch,
            position = Vector3.zero, // Position comes from save files, not persistent data
            rotation = Vector3.zero
        };

        // Merge additional data from dynamic storage
        var additionalData = unifiedData.GetComponentData<PlayerSaveData>(SaveID);
        if (additionalData != null)
        {
            playerSaveData.lookSensitivity = additionalData.lookSensitivity;
            playerSaveData.masterVolume = additionalData.masterVolume;
            playerSaveData.sfxVolume = additionalData.sfxVolume;
            playerSaveData.musicVolume = additionalData.musicVolume;
            playerSaveData.level = additionalData.level;
            playerSaveData.experience = additionalData.experience;
            playerSaveData.maxHealth = additionalData.maxHealth;
            playerSaveData.currentScene = additionalData.currentScene;

            // Extract both movement mode and player state
            playerSaveData.savedMovementMode = additionalData.savedMovementMode;
            playerSaveData.savedPlayerState = additionalData.savedPlayerState;
            playerSaveData.wasInWater = additionalData.wasInWater;

            playerSaveData.MergeCustomDataFrom(additionalData);
        }

        DebugLog($"Modular extraction complete: {playerSaveData.GetMovementDebugInfo()}");
        return playerSaveData;
    }

    /// <summary>
    /// Creates default data with new PlayerStateType
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default player data for new game");

        var defaultData = new PlayerSaveData();

        // Set health from PlayerData if available
        if (playerData != null)
        {
            defaultData.currentHealth = playerData.maxHealth;
            defaultData.maxHealth = playerData.maxHealth;
            defaultData.lookSensitivity = playerData.lookSensitivity;
        }
        else
        {
            defaultData.currentHealth = 100f;
            defaultData.maxHealth = 100f;
            defaultData.lookSensitivity = 2f;
        }

        // Set default abilities and settings
        defaultData.canJump = true;
        defaultData.canSprint = true;
        defaultData.canCrouch = true;
        defaultData.masterVolume = 1f;
        defaultData.sfxVolume = 1f;
        defaultData.musicVolume = 1f;
        defaultData.level = 1;
        defaultData.experience = 0f;
        defaultData.position = Vector3.zero; // Spawn point will set position
        defaultData.rotation = Vector3.zero;
        defaultData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Set default state information
        defaultData.savedMovementMode = MovementMode.Ground;
        defaultData.savedPlayerState = PlayerStateType.Ground;
        defaultData.wasInWater = false;

        DebugLog($"Default player data created: {defaultData.GetMovementDebugInfo()}");
        return defaultData;
    }

    /// <summary>
    /// Contributes data with new PlayerStateType
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is PlayerSaveData playerData && unifiedData != null)
        {
            DebugLog("Contributing player data to unified save structure");

            // Store basic stats in main structure
            unifiedData.currentHealth = playerData.currentHealth;
            unifiedData.canJump = playerData.canJump;
            unifiedData.canSprint = playerData.canSprint;
            unifiedData.canCrouch = playerData.canCrouch;

            // Store complete data in dynamic storage for full preservation
            unifiedData.SetComponentData(SaveID, playerData);

            DebugLog($"Player data contributed: {playerData.GetMovementDebugInfo()}");
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected PlayerSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Context-aware data restoration with vehicle coordination
    /// Now properly coordinates with SceneVehicleStateManager to prevent conflicts
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not PlayerSaveData playerSaveData)
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== PLAYER DATA RESTORATION (Context: {context}) ===");
        DebugLog($"Received data: {playerSaveData.GetMovementDebugInfo()}");

        // Refresh references in case they changed after scene load
        if (autoFindReferences)
        {
            FindPlayerReferences();
        }

        // STEP 1: Always restore abilities and health first
        RestorePlayerStats(playerSaveData);

        // STEP 2: Handle position and movement based on context
        switch (context)
        {
            case RestoreContext.SaveFileLoad:
                DebugLog("Save file load - checking for vehicle restoration needs");
                HandleSaveFileLoadRestoration(playerSaveData);
                break;

            case RestoreContext.DoorwayTransition:
                DebugLog("Doorway transition - NOT restoring position, but setting movement context");
                // Don't restore position (doorway will set it)
                // Set movement mode but validate after positioning
                SetInitialMovementMode(playerSaveData);
                break;

            case RestoreContext.NewGame:
                DebugLog("New game - using default movement state");
                SetDefaultMovementState();
                break;
        }

        DebugLog($"Player data restoration complete for context: {context}");
    }

    /// <summary>
    /// Handle save file load restoration with vehicle coordination
    /// </summary>
    private void HandleSaveFileLoadRestoration(PlayerSaveData playerSaveData)
    {
        // Check if player was in a vehicle when saved
        bool wasInVehicle = playerSaveData.savedPlayerState == PlayerStateType.Vehicle;

        if (wasInVehicle)
        {
            DebugLog("Player was in vehicle - skipping position restore, SceneVehicleStateManager will handle it");

            // Set movement mode immediately for vehicle
            SetInitialMovementMode(playerSaveData);

            // Schedule a check to ensure vehicle restoration happens
            StartCoroutine(CheckVehicleRestorationCompletion(playerSaveData));
        }
        else
        {
            DebugLog("Player was on foot - restoring position and movement normally");
            RestorePlayerPosition(playerSaveData);
            RestoreMovementState(playerSaveData, RestoreContext.SaveFileLoad);

            // Schedule normal validation
            if (enableMovementValidation)
            {
                StartCoroutine(ScheduleMovementValidation(RestoreContext.SaveFileLoad));
            }
        }
    }

    /// <summary>
    /// Check that vehicle restoration completed successfully
    /// </summary>
    private System.Collections.IEnumerator CheckVehicleRestorationCompletion(PlayerSaveData playerSaveData)
    {
        DebugLog("Checking vehicle restoration completion...");

        // Wait for SceneVehicleStateManager to complete restoration
        yield return new WaitForSeconds(vehicleRestorationCheckDelay);

        // Verify player is in correct state
        bool isPlayerInVehicle = playerController != null && playerController.IsInVehicle;
        bool isStateCorrect = playerStateManager?.CurrentStateType == PlayerStateType.Vehicle;

        if (isPlayerInVehicle && isStateCorrect)
        {
            DebugLog("✅ Vehicle restoration completed successfully");

            // Ensure player position is exactly right in vehicle
            if (playerController.currentVehicle?.DriverSeat != null)
            {
                playerController.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                DebugLog($"Confirmed final player local position: {playerController.transform.localPosition}");
            }
        }
        else
        {
            Debug.LogWarning($"[PlayerSaveComponent] Vehicle restoration may have failed - InVehicle: {isPlayerInVehicle}, StateCorrect: {isStateCorrect}");

            // Fallback: restore position normally if vehicle restoration failed
            DebugLog("Falling back to normal position restoration");
            RestorePlayerPosition(playerSaveData);
            RestoreMovementState(playerSaveData, RestoreContext.SaveFileLoad);

            // Perform validation
            if (enableMovementValidation)
            {
                StartCoroutine(ScheduleMovementValidation(RestoreContext.SaveFileLoad));
            }
        }
    }

    /// <summary>
    /// Uses PlayerController's SetInitialMovementMode method
    /// </summary>
    private void SetInitialMovementMode(PlayerSaveData playerSaveData)
    {
        if (playerController != null)
        {
            // Set the movement mode but don't validate yet (positioning hasn't happened)
            playerController.SetInitialMovementMode(playerSaveData.savedMovementMode);
            DebugLog($"Set initial movement mode: {playerSaveData.savedMovementMode}");
        }
    }

    /// <summary>
    /// Sets default movement state for new games
    /// </summary>
    private void SetDefaultMovementState()
    {
        if (playerController != null)
        {
            playerController.SetInitialMovementMode(MovementMode.Ground);
            DebugLog("Set default movement state for new game");
        }
    }

    /// <summary>
    /// Restores movement state using new system
    /// </summary>
    private void RestoreMovementState(PlayerSaveData playerSaveData, RestoreContext context)
    {
        if (playerController == null) return;

        DebugLog($"Restoring movement state: {playerSaveData.savedMovementMode}");

        // Set the movement mode from save data
        playerController.SetInitialMovementMode(playerSaveData.savedMovementMode);

        // Log the restoration for debugging
        DebugLog($"Movement state restored - Mode: {playerSaveData.savedMovementMode}, " +
                $"PlayerState: {playerSaveData.savedPlayerState}, WasInWater: {playerSaveData.wasInWater}");
    }

    /// <summary>
    /// Uses PlayerStateManager for validation instead of PlayerController
    /// </summary>
    private System.Collections.IEnumerator ScheduleMovementValidation(RestoreContext context)
    {
        // Wait for positioning and physics to settle
        yield return new WaitForSecondsRealtime(validationDelay);

        DebugLog($"Performing scheduled movement validation for {context}");

        // Check if vehicle restoration is in progress
        if (SceneVehicleStateManager.Instance != null && SceneVehicleStateManager.Instance.IsPerformingRestoration)
        {
            DebugLog("Vehicle restoration in progress - skipping validation to avoid conflicts");
            yield break;
        }

        // Use PlayerStateManager for validation
        if (playerStateManager != null)
        {
            playerStateManager.ForceStateValidation();
        }
        else if (playerController != null)
        {
            // Fallback: trigger validation through PlayerController's state manager
            playerController.ValidatePlayerState();
        }
        else
        {
            Debug.LogWarning("No PlayerStateManager or PlayerController found for validation");
        }

        DebugLog("Scheduled movement validation complete");
    }

    /// <summary>
    /// Restores player stats, abilities, and health. Common to all restoration contexts.
    /// </summary>
    private void RestorePlayerStats(PlayerSaveData playerSaveData)
    {
        // Restore abilities to PlayerController
        if (playerController != null)
        {
            playerController.canJump = playerSaveData.canJump;
            playerController.canSprint = playerSaveData.canSprint;
            playerController.canCrouch = playerSaveData.canCrouch;
            DebugLog($"Restored abilities - Jump: {playerSaveData.canJump}, Sprint: {playerSaveData.canSprint}, Crouch: {playerSaveData.canCrouch}");
        }

        // Restore health to PlayerManager
        if (playerManager != null)
        {
            playerManager.currentHealth = playerSaveData.currentHealth;
            DebugLog($"Restored health: {playerSaveData.currentHealth}");

            // Trigger health UI update
            if (playerData != null)
            {
                GameEvents.TriggerPlayerHealthChanged(playerManager.currentHealth, playerData.maxHealth);
            }
        }

        // Restore settings to PlayerData
        if (playerData != null && playerSaveData.lookSensitivity > 0)
        {
            playerData.lookSensitivity = playerSaveData.lookSensitivity;
            DebugLog($"Restored look sensitivity: {playerSaveData.lookSensitivity}");
        }
    }

    /// <summary>
    /// Restores exact player position with vehicle awareness
    /// Now checks if player should be in a vehicle and skips position restore if so
    /// </summary>
    private void RestorePlayerPosition(PlayerSaveData playerSaveData)
    {
        if (playerController != null)
        {
            // Clean velocity before position change
            Rigidbody rb = playerController.rb;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                DebugLog("Cleared velocity before position restore");
            }

            // Restore position and rotation for non-vehicle states
            playerController.rb.Move(playerSaveData.position, Quaternion.Euler(playerSaveData.rotation));

            DebugLog($"Restored position: {playerSaveData.position}, rotation: {playerSaveData.rotation}");
        }
        else
        {
            DebugLog("PlayerController not found - position not restored");
        }
    }

    /// <summary>
    /// Called before save operations to ensure references are current.
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing player data for save");

        if (autoFindReferences)
        {
            FindPlayerReferences();
        }
    }

    /// <summary>
    /// Called after load operations to refresh UI systems.
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Player data load completed");

        if (GameManager.Instance?.uiManager != null)
        {
            GameManager.Instance.uiManager.RefreshReferences();
        }
    }

}