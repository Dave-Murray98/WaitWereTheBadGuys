using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections;

/// <summary>
/// CLEANED: Scene Vehicle State Management System
/// Simplified vehicle discovery, state management, and restoration process
/// </summary>
public class SceneVehicleStateManager : MonoBehaviour, ISaveable
{
    public static SceneVehicleStateManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveID = "SceneVehicleStateManager";

    [Header("Restoration Settings")]
    [SerializeField] private float restorationDelay = 0.1f;
    [SerializeField] private float stateValidationDelay = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // Core state data
    private Dictionary<string, VehicleSaveData> vehicleStates = new Dictionary<string, VehicleSaveData>();
    private string playerOccupiedVehicleID = "";

    // Scene vehicles
    [ShowInInspector] private List<VehicleController> sceneVehicles = new List<VehicleController>();

    // Restoration state tracking
    private bool isPerformingRestoration = false;

    // ISaveable implementation
    public string SaveID => saveID;
    public SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DebugLog("SceneVehicleStateManager created");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StartCoroutine(InitializeAfterSceneLoad());
    }

    #region Initialization

    private IEnumerator InitializeAfterSceneLoad()
    {
        // Wait for scene to settle
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        DiscoverAndSetupVehicles();
        DebugLog($"Initialization complete - {sceneVehicles.Count} vehicles discovered");
    }

    private void DiscoverAndSetupVehicles()
    {
        DebugLog("=== DISCOVERING AND SETTING UP VEHICLES ===");

        // Find all vehicles in scene
        var vehicleControllers = FindObjectsByType<VehicleController>(FindObjectsSortMode.None).ToList();

        // Assign consistent IDs
        AssignConsistentVehicleIDs(vehicleControllers);
        sceneVehicles = vehicleControllers;

        // Subscribe to vehicle events for runtime tracking
        foreach (var vehicle in sceneVehicles)
        {
            vehicle.OnPlayerEntered += OnPlayerEnteredVehicle;
            vehicle.OnPlayerExited += OnPlayerExitedVehicle;
        }

        DebugLog($"Discovered {sceneVehicles.Count} vehicles:");
        foreach (var vehicle in sceneVehicles)
        {
            DebugLog($"  - {vehicle.VehicleID} ({vehicle.VehicleType}) at {vehicle.Transform.position}");
        }
    }

    /// <summary>
    /// Assign consistent IDs using Instance ID for deterministic ordering
    /// </summary>
    private void AssignConsistentVehicleIDs(List<VehicleController> vehicles)
    {
        var vehicleGroups = vehicles
            .GroupBy(v => v.Transform.gameObject.name.Replace(" ", "_"))
            .ToList();

        foreach (var group in vehicleGroups)
        {
            string baseName = group.Key;
            var vehiclesInGroup = group.OrderBy(v => v.GetInstanceID()).ToList();

            for (int i = 0; i < vehiclesInGroup.Count; i++)
            {
                var vehicle = vehiclesInGroup[i];
                string consistentID = $"{baseName}_{(i + 1):D2}";
                vehicle.SetVehicleID(consistentID);
                DebugLog($"Assigned ID '{consistentID}' to vehicle instance {vehicle.GetInstanceID()}");
            }
        }
    }

    #endregion

    #region Player-Vehicle Relationship Tracking

    private void OnPlayerEnteredVehicle(IVehicle vehicle, GameObject player)
    {
        if (player.GetComponent<PlayerController>() == null) return;

        playerOccupiedVehicleID = vehicle.VehicleID;
        DebugLog($"Player entered vehicle: {vehicle.VehicleID}");
    }

    private void OnPlayerExitedVehicle(IVehicle vehicle, GameObject player)
    {
        if (player.GetComponent<PlayerController>() == null) return;

        if (playerOccupiedVehicleID == vehicle.VehicleID)
        {
            playerOccupiedVehicleID = "";
            DebugLog($"Player exited vehicle: {vehicle.VehicleID}");
        }
    }

    #endregion

    #region ISaveable Implementation

    public object GetDataToSave()
    {
        DebugLog("Collecting vehicle states for save");

        // Ensure vehicles are discovered
        if (sceneVehicles.Count == 0)
        {
            DiscoverAndSetupVehicles();
        }

        // Collect current vehicle states
        vehicleStates.Clear();
        foreach (VehicleController vehicle in sceneVehicles)
        {
            var vehicleSaveData = new VehicleSaveData();
            vehicleSaveData.UpdateFromVehicle(vehicle);
            vehicleStates[vehicle.VehicleID] = vehicleSaveData;
        }

        // Get current player-vehicle relationship
        var playerController = FindFirstObjectByType<PlayerController>();
        if (playerController?.IsInVehicle == true)
        {
            var currentVehicle = playerController.GetCurrentVehicle();
            playerOccupiedVehicleID = currentVehicle?.VehicleID ?? "";
        }
        else
        {
            playerOccupiedVehicleID = "";
        }

        var saveData = new SceneVehicleStateSaveData
        {
            vehicleStates = vehicleStates.Values.ToList(),
            playerOccupiedVehicleID = playerOccupiedVehicleID
        };

        DebugLog($"Saved vehicle data - {vehicleStates.Count} vehicles, player in: {(string.IsNullOrEmpty(playerOccupiedVehicleID) ? "none" : playerOccupiedVehicleID)}");
        return saveData;
    }

    public object ExtractRelevantData(object saveContainer)
    {
        return saveContainer;
    }

    public void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        DebugLog($"LoadSaveDataWithContext called (Context: {context})");

        if (data is not SceneVehicleStateSaveData saveData)
        {
            DebugLog($"Invalid data type - expected SceneVehicleStateSaveData, got {data?.GetType()}");
            return;
        }

        vehicleStates = saveData.vehicleStates?.ToDictionary(v => v.vehicleID, v => v) ?? new Dictionary<string, VehicleSaveData>();

        // Context-aware player relationship handling
        switch (context)
        {
            case RestoreContext.SaveFileLoad:
                playerOccupiedVehicleID = saveData.playerOccupiedVehicleID ?? "";
                DebugLog($"Save file load - will restore player to vehicle: {(string.IsNullOrEmpty(playerOccupiedVehicleID) ? "none" : playerOccupiedVehicleID)}");
                break;

            case RestoreContext.DoorwayTransition:
                playerOccupiedVehicleID = "";
                DebugLog("Doorway transition - player will be on foot");
                break;

            case RestoreContext.NewGame:
                playerOccupiedVehicleID = "";
                vehicleStates.Clear();
                DebugLog("New game - clearing all states");
                break;
        }

        DebugLog($"Loaded state: {vehicleStates.Count} vehicles, target player vehicle: {(string.IsNullOrEmpty(playerOccupiedVehicleID) ? "none" : playerOccupiedVehicleID)}");
    }

    public void OnBeforeSave()
    {
        DebugLog("OnBeforeSave called - preparing vehicle data");
    }

    public void OnAfterLoad()
    {
        DebugLog("OnAfterLoad called - applying vehicle states to scene");

        // Ensure vehicles are discovered for restoration
        if (sceneVehicles.Count == 0)
        {
            DiscoverAndSetupVehicles();
        }

        StartCoroutine(ApplyLoadedStatesToScene());
    }

    #endregion

    #region State Application After Load

    /// <summary>
    /// Apply vehicle states with proper timing coordination
    /// </summary>
    private IEnumerator ApplyLoadedStatesToScene()
    {
        isPerformingRestoration = true;
        DebugLog($"=== STARTING VEHICLE RESTORATION PROCESS ===");

        // Wait for other systems to finish loading
        yield return new WaitForSeconds(restorationDelay);

        DebugLog($"Applying loaded states - {vehicleStates.Count} vehicles, player target: {(string.IsNullOrEmpty(playerOccupiedVehicleID) ? "none" : playerOccupiedVehicleID)}");

        // STEP 1: Apply states to all vehicles
        ApplyStatesToVehicles();

        // STEP 2: Handle player-vehicle relationship
        if (!string.IsNullOrEmpty(playerOccupiedVehicleID))
        {
            yield return StartCoroutine(RestorePlayerIntoVehicle());
        }

        // STEP 3: Refresh vehicle interactables
        RefreshVehicleInteractables();

        // STEP 4: Final state validation
        yield return new WaitForSeconds(stateValidationDelay);

        if (PlayerStateManager.Instance != null)
        {
            DebugLog("Performing final state validation after vehicle restoration");
            PlayerStateManager.Instance.ForceStateValidation();
        }

        isPerformingRestoration = false;
        DebugLog("=== VEHICLE RESTORATION PROCESS COMPLETED ===");
    }

    private void ApplyStatesToVehicles()
    {
        DebugLog($"=== APPLYING STATES TO {sceneVehicles.Count} VEHICLES ===");

        foreach (var vehicle in sceneVehicles)
        {
            if (vehicleStates.TryGetValue(vehicle.VehicleID, out var saveData))
            {
                DebugLog($"Applying state to vehicle: {vehicle.VehicleID} (includes damage data: {saveData.damageData != null})");

                // Use the enhanced method that includes damage restoration
                vehicle.ApplyVehicleStateWithDamage(saveData);
            }
        }
    }

    /// <summary>
    /// Direct vehicle restoration for save file loads
    /// </summary>
    private IEnumerator RestorePlayerIntoVehicle()
    {
        DebugLog($"=== RESTORING PLAYER INTO VEHICLE: {playerOccupiedVehicleID} ===");

        // Find the target vehicle
        var targetVehicle = sceneVehicles.FirstOrDefault(v => v.VehicleID == playerOccupiedVehicleID);
        if (targetVehicle == null)
        {
            DebugLog($"❌ Target vehicle {playerOccupiedVehicleID} not found!");
            playerOccupiedVehicleID = "";
            yield break;
        }

        // Find the player
        var playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null)
        {
            DebugLog("❌ PlayerController not found!");
            yield break;
        }

        // Verify vehicle is operational
        if (!targetVehicle.IsOperational)
        {
            DebugLog($"❌ Vehicle {playerOccupiedVehicleID} is not operational");
            playerOccupiedVehicleID = "";
            yield break;
        }

        // Wait for everything to settle
        yield return new WaitForEndOfFrame();

        // Perform direct restoration
        DebugLog($"🚗 Performing direct restoration into vehicle {playerOccupiedVehicleID}");
        bool success = targetVehicle.RestorePlayerIntoVehicle(playerController.gameObject);

        if (success)
        {
            DebugLog($"✅ Successfully restored player into vehicle {playerOccupiedVehicleID}");

            // Set player state immediately
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.ForceStateChange(PlayerStateType.Vehicle);
            }

            // Ensure player position is correct
            if (targetVehicle.DriverSeat != null)
            {
                playerController.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            // Start car engine if needed
            if (targetVehicle is CarController carController && !carController.IsEngineRunning())
            {
                carController.StartEngine();
                DebugLog("Started car engine after player restoration");
            }

            // Configure camera aiming
            yield return new WaitForSeconds(0.2f);
            ConfigureVehicleCameraAiming(targetVehicle, playerController);

            // Update vehicle movement controller
            playerController.vehicleMovementController.SetCurrentVehicle(targetVehicle);
        }
        else
        {
            DebugLog($"❌ Failed to restore player into vehicle {playerOccupiedVehicleID}");
            playerOccupiedVehicleID = "";
        }
    }

    /// <summary>
    /// Configure camera aiming for vehicle mode
    /// </summary>
    private void ConfigureVehicleCameraAiming(VehicleController vehicle, PlayerController player)
    {
        if (vehicle == null || player?.cameraController?.aimController == null)
            return;

        var aimController = player.cameraController.aimController;

        // Configure aiming limits for vehicle
        aimController.UpdateAngleLimits(
            vehicle.vehicleMinVerticalAngle,
            vehicle.vehicleMaxVerticalAngle,
            true, // Enable horizontal aiming
            vehicle.vehicleMinHorizontalAngle,
            vehicle.vehicleMaxHorizontalAngle
        );

        DebugLog($"✅ Vehicle camera aiming configured - Horizontal enabled: {aimController.IsHorizontalAimingEnabled}");
    }

    /// <summary>
    /// Refresh all vehicle interactables after restoration
    /// </summary>
    private void RefreshVehicleInteractables()
    {
        var vehicleInteractables = FindObjectsByType<VehicleInteractable>(FindObjectsSortMode.None);

        foreach (var interactable in vehicleInteractables)
        {
            interactable.RefreshInteractableState();
        }

        DebugLog($"Refreshed {vehicleInteractables.Length} vehicle interactables");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Check if vehicle restoration is currently in progress
    /// </summary>
    public bool IsPerformingRestoration => isPerformingRestoration;

    /// <summary>
    /// Get the vehicle ID that the player should be restored into
    /// </summary>
    public string GetPlayerTargetVehicleID() => playerOccupiedVehicleID;

    /// <summary>
    /// Check if a specific vehicle should contain the player
    /// </summary>
    public bool ShouldVehicleContainPlayer(string vehicleID)
    {
        return !string.IsNullOrEmpty(playerOccupiedVehicleID) && playerOccupiedVehicleID == vehicleID;
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        // Clean up event subscriptions
        foreach (var vehicle in sceneVehicles)
        {
            if (vehicle != null)
            {
                vehicle.OnPlayerEntered -= OnPlayerEnteredVehicle;
                vehicle.OnPlayerExited -= OnPlayerExitedVehicle;
            }
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneVehicleStateManager] {message}");
        }
    }

    #endregion
}

/// <summary>
/// Save data structure for vehicle states
/// </summary>
[System.Serializable]
public class SceneVehicleStateSaveData
{
    public List<VehicleSaveData> vehicleStates = new List<VehicleSaveData>();
    public string playerOccupiedVehicleID = "";
}