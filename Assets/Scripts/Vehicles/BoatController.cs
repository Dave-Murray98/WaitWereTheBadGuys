using UnityEngine;
using NWH.DWP2.ShipController;
using NWH.DWP2.WaterObjects;

/// <summary>
/// Boat controller implementing IVehicle interface with Dynamic Water Physics 2 integration.
/// Handles boat-specific movement using DWP2's AdvancedShipController.
/// Translates simple player input (WASD) into boat throttle and steering controls.
/// 
/// CLEANED: Removed save system methods - now handled by VehicleSaveComponent
/// ENHANCED: Includes steering wheel rotation system
/// </summary>
[RequireComponent(typeof(AdvancedShipController))]
public class BoatController : VehicleController
{
    [Header("Boat-Specific Settings")]
    [SerializeField] private AdvancedShipController shipController;
    [SerializeField] private bool autoFindShipController = true;

    [Header("Input Response")]
    [SerializeField] private float throttleResponseSpeed = 2f;
    [SerializeField] private float steeringResponseSpeed = 3f;
    [SerializeField] private bool invertSteering = false;

    [Header("Boat Limits")]
    [SerializeField] private float maxThrottleInput = 1f;
    [SerializeField] private float maxSteeringInput = 1f;

    // Current input values
    private float currentThrottleInput = 0f;
    private float currentSteeringInput = 0f;

    #region Initialization

    protected override void Awake()
    {
        // Set vehicle type for boat
        vehicleType = VehicleType.Boat;

        base.Awake(); // This calls InitializeSteeringWheel()

        if (autoFindShipController && shipController == null)
        {
            shipController = GetComponent<AdvancedShipController>();
        }

        ValidateBoatSetup();
    }

    private void Start()
    {
        // Ensure boat starts with zero input
        if (shipController != null)
        {
            InitializeShipController();
        }

        if (steeringWheel == null)
        {
            Debug.LogWarning($"[BoatController] Steering wheel not found on {name}");
        }
    }

    private void ValidateBoatSetup()
    {
        if (shipController == null)
        {
            Debug.LogError($"[BoatController] AdvancedShipController not found on {name}! " +
                         "Boat will not function properly.");
            isOperational = false;
            return;
        }

        DebugLog("Boat setup validation complete");
    }

    private void InitializeShipController()
    {
        // Set initial ship controller state
        shipController.input.Throttle = 0f;
        shipController.input.Steering = 0f;

        // Configure ship controller settings if needed
        // You can adjust DWP2 settings here based on your requirements

        DebugLog("Ship controller initialized");
    }

    #endregion

    #region Vehicle Controller Implementation

    protected override Vector3 GetVehicleVelocity()
    {
        if (shipController != null && shipController.vehicleRigidbody != null)
        {
            return shipController.vehicleRigidbody.linearVelocity;
        }
        return Vector3.zero;
    }

    protected override void ApplyThrottleInput(float throttle)
    {
        // Set target throttle for smooth interpolation
        currentThrottleInput = Mathf.Clamp(throttle, -maxThrottleInput, maxThrottleInput);

        //        DebugLog($"Throttle input: {throttle:F2} -> Target: {currentThrottleInput:F2}");
    }

    protected override void ApplySteeringInput(float steering)
    {
        // Apply steering inversion if needed and clamp to limits
        float processedSteering = invertSteering ? -steering : steering;
        currentSteeringInput = Mathf.Clamp(processedSteering, -maxSteeringInput, maxSteeringInput);

        //     DebugLog($"Steering input: {steering:F2} -> Target: {currentSteeringInput:F2}");
    }

    protected override void ApplyBrakeInput(float brake)
    {
        // For boats, braking means setting throttle to 0 and applying reverse thrust
        if (brake > 0.1f)
        {
            // When braking, override throttle to 0 or slight reverse
            currentThrottleInput = 0f;

            // You could apply a slight reverse thrust for more realistic braking
            // targetThrottle = -0.3f * brake;
        }

        DebugLog($"Brake input: {brake:F2}");
    }

    protected override void PerformVehicleEntryLogic(GameObject player)
    {
        // Boat-specific logic when player enters
        DebugLog($"Player {player.name} entered boat {VehicleID}");

        // Could add boat-specific entry effects here:
        // - Start engine sound
        // - Enable boat UI
    }

    protected override void PerformVehicleExitLogic(GameObject player)
    {
        // Boat-specific logic when player exits
        DebugLog($"Player {player.name} exited boat {VehicleID}");

        // Stop the boat when player exits
        if (shipController != null)
        {
            shipController.input.Throttle = 0f;
            shipController.input.Steering = 0f;
        }

        currentThrottleInput = 0f;
        currentSteeringInput = 0f;

        // Reset steering wheel to center when player exits
        ResetSteeringWheel();

        // Could add boat-specific exit effects here:
        // - Stop engine sound
        // - Disable boat UI
        // - Reset camera settings
    }

    #endregion

    #region Update Logic

    protected override void Update()
    {
        // Call base Update for steering wheel rotation
        base.Update();

        if (shipController == null) return;

        // Only process input if boat has a driver
        if (HasDriver)
        {
            ApplyInputToShipController();
        }
    }

    private void ApplyInputToShipController()
    {
        if (shipController == null) return;

        // DWP2 uses Throttle for forward/backward movement
        shipController.input.Throttle = currentThrottleInput;

        // DWP2 uses Steering for left/right turning
        shipController.input.Steering = currentSteeringInput;

        if (currentBrake > 0.1f)
        {
            // When braking, override throttle to 0 or slight reverse
            shipController.input.Throttle = 0f;
        }

    }

    #endregion

    #region Public API

    /// <summary>
    /// Get boat-specific debug information including steering wheel
    /// </summary>
    public string GetBoatDebugInfo()
    {
        string baseInfo = $"Boat Debug - ID: {VehicleID}, " +
                         $"Throttle: {currentThrottleInput:F2}, " +
                         $"Steering: {currentSteeringInput:F2}, " +
                         $"Velocity: {Velocity.magnitude:F1}m/s, " +
                         $"HasDriver: {HasDriver}, Operational: {IsOperational}";

        return baseInfo;
    }

    /// <summary>
    /// Set boat-specific input response speeds
    /// </summary>
    public void SetInputResponseSpeeds(float throttleSpeed, float steeringSpeed)
    {
        throttleResponseSpeed = Mathf.Max(0.1f, throttleSpeed);
        steeringResponseSpeed = Mathf.Max(0.1f, steeringSpeed);
        DebugLog($"Input response speeds updated - Throttle: {throttleResponseSpeed}, Steering: {steeringResponseSpeed}");
    }

    /// <summary>
    /// Set steering inversion
    /// </summary>
    public void SetSteeringInverted(bool inverted)
    {
        invertSteering = inverted;
        DebugLog($"Steering inversion set to: {invertSteering}");
    }

    /// <summary>
    /// Force stop the boat (emergency stop)
    /// </summary>
    public void ForceStop()
    {
        currentThrottleInput = 0f;
        currentSteeringInput = 0f;

        if (shipController != null)
        {
            shipController.input.Throttle = 0f;
            shipController.input.Steering = 0f;
        }

        // Reset steering wheel
        ResetSteeringWheel();

        DebugLog("Boat force stopped");
    }

    #endregion

    #region Cleanup

    protected override void OnDestroy()
    {
        // Clean up boat-specific resources
        if (shipController != null)
        {
            shipController.input.Throttle = 0f;
            shipController.input.Steering = 0f;
        }

        base.OnDestroy();
    }

    #endregion
}