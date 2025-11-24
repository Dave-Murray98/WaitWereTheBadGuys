using UnityEngine;

/// <summary>
/// Interactable component for vehicles with save/load state management
/// Now properly handles state restoration and event subscription management
/// </summary>
public class VehicleInteractable : InteractableBase
{
    [Header("Vehicle Settings")]
    [SerializeField] private VehicleController vehicleController;
    [SerializeField] private bool autoFindVehicleController = true;

    [Header("Interaction Configuration")]
    [SerializeField] private string enterPrompt = "Press E to enter vehicle";


    // State tracking
    private bool isSubscribedToExitEvent = false;

    protected override void Awake()
    {
        base.Awake();

        if (autoFindVehicleController && vehicleController == null)
        {
            vehicleController = GetComponent<VehicleController>();
            if (vehicleController == null)
            {
                vehicleController = GetComponentInParent<VehicleController>();
            }
        }

        // Set interaction properties
        interactionPrompt = enterPrompt;
        base.interactionRange = interactionRange;
    }

    protected override void Start()
    {
        base.Start();
        // Initialize state after all systems are loaded
        InitializeInteractableState();
    }

    /// <summary>
    /// Initialize or refresh the interactable state
    /// Called at start and after save/load operations
    /// </summary>
    private void InitializeInteractableState()
    {
        if (vehicleController == null)
        {
            DebugLog("No vehicle controller found during initialization");
            return;
        }

        // Check current vehicle state and update interactable accordingly
        bool vehicleHasDriver = vehicleController.HasDriver;
        bool shouldBeInteractable = vehicleController.IsOperational && !vehicleHasDriver;

        DebugLog($"Initializing interactable state - Vehicle has driver: {vehicleHasDriver}, Should be interactable: {shouldBeInteractable}");

        SetInteractable(shouldBeInteractable);

        // Manage event subscription based on vehicle state
        if (vehicleHasDriver && !isSubscribedToExitEvent)
        {
            SubscribeToVehicleEvents();
        }
        else if (!vehicleHasDriver && isSubscribedToExitEvent)
        {
            UnsubscribeFromVehicleEvents();
        }
    }

    /// <summary>
    /// Subscribe to vehicle events with state tracking
    /// </summary>
    private void SubscribeToVehicleEvents()
    {
        if (vehicleController != null && !isSubscribedToExitEvent)
        {
            vehicleController.OnPlayerExited += OnVehiclePlayerExited;
            isSubscribedToExitEvent = true;
            DebugLog("Subscribed to vehicle exit events");
        }
    }

    /// <summary>
    /// Unsubscribe from vehicle events with state tracking
    /// </summary>
    private void UnsubscribeFromVehicleEvents()
    {
        if (vehicleController != null && isSubscribedToExitEvent)
        {
            vehicleController.OnPlayerExited -= OnVehiclePlayerExited;
            isSubscribedToExitEvent = false;
            DebugLog("Unsubscribed from vehicle exit events");
        }
    }

    protected override bool PerformInteraction(GameObject player)
    {
        if (vehicleController == null)
        {
            DebugLog("No vehicle controller found!");
            return false;
        }

        // Get PlayerController from the player
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController == null)
        {
            DebugLog("No PlayerController found on player!");
            return false;
        }

        // Check if vehicle is operational
        if (!vehicleController.IsOperational)
        {
            DebugLog("Vehicle is not operational");
            return false;
        }

        // Check if vehicle already has a driver
        if (vehicleController.HasDriver)
        {
            DebugLog("Vehicle already has a driver");
            return false;
        }

        // Check if player is already in another vehicle
        if (playerController.IsInVehicle)
        {
            DebugLog("Player is already in another vehicle");
            return false;
        }

        // Try to enter the vehicle through PlayerController
        bool success = playerController.EnterVehicle(vehicleController);
        if (success)
        {
            DebugLog($"Player {player.name} entered vehicle {vehicleController.VehicleID}");

            // Disable the interactable while vehicle is in use
            SetInteractable(false);

            // Subscribe to vehicle exit events to re-enable interaction
            SubscribeToVehicleEvents();
        }

        return success;
    }

    /// <summary>
    /// Re-enable interaction when player exits vehicle
    /// </summary>
    private void OnVehiclePlayerExited(IVehicle vehicle, GameObject player)
    {
        DebugLog($"Vehicle player exited event received - Vehicle: {vehicle.VehicleID}, Player: {player.name}");

        SetInteractable(true);

        // Unsubscribe from the event since vehicle is now empty
        UnsubscribeFromVehicleEvents();

        DebugLog("Vehicle interaction re-enabled");
    }

    public override string GetInteractionPrompt()
    {
        if (vehicleController == null || !vehicleController.IsOperational)
        {
            return "Vehicle not available";
        }

        if (vehicleController.HasDriver)
        {
            return "Vehicle in use";
        }

        return enterPrompt;
    }

    /// <summary>
    /// Force refresh the interactable state
    /// Can be called by external systems after save/load operations
    /// </summary>
    public void RefreshInteractableState()
    {
        DebugLog("Force refreshing interactable state");
        InitializeInteractableState();
    }

    protected override object GetCustomSaveData()
    {
        // Save the current interactable state
        return new VehicleInteractableSaveData
        {
            canInteract = this.canInteract,
            isSubscribedToEvents = this.isSubscribedToExitEvent
        };
    }

    protected override void LoadCustomSaveData(object customData)
    {
        if (customData is VehicleInteractableSaveData saveData)
        {
            DebugLog($"Loading interactable save data - canInteract: {saveData.canInteract}");

            // Restore basic state
            SetInteractable(saveData.canInteract);

            // Force a state refresh after loading to ensure consistency
            // Use a coroutine to ensure all systems are ready
            StartCoroutine(DelayedStateRefresh());
        }
    }

    /// <summary>
    /// Delayed state refresh after loading to ensure all systems are ready
    /// </summary>
    private System.Collections.IEnumerator DelayedStateRefresh()
    {
        // Wait for vehicle and player systems to finish loading
        yield return new WaitForSeconds(0.5f);

        DebugLog("Performing delayed state refresh after load");
        InitializeInteractableState();

    }

    private void OnDestroy()
    {
        // Clean up event subscriptions
        UnsubscribeFromVehicleEvents();
    }

    /// <summary>
    /// Save data structure for vehicle interactable state
    /// </summary>
    [System.Serializable]
    private class VehicleInteractableSaveData
    {
        public bool canInteract;
        public bool isSubscribedToEvents;
    }

}