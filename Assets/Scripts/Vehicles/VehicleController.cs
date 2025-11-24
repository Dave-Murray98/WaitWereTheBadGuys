using UnityEngine;
using System;
using Sirenix.OdinInspector;

/// <summary>
///  VehicleController with direct restoration support for save system
/// Now supports both interactive entry (via handler) and direct restoration (for saves)
/// </summary>
public abstract class VehicleController : MonoBehaviour, IVehicle
{
    [HideInInspector] public PlayerController playerController;

    [Header("Vehicle Identity")]
    [SerializeField] protected string vehicleID;
    [SerializeField] protected VehicleType vehicleType;

    [FoldoutGroup("Vehicle Configuration")]
    [SerializeField] protected Transform driverSeat;
    [FoldoutGroup("Vehicle Configuration")]
    [Tooltip("Whether the driver should sit or stand in the vehicle (ie what animation to play, sitting or standing)")]
    [SerializeField] protected bool isVehicleSeated;
    [FoldoutGroup("Vehicle Configuration")]
    [SerializeField] protected Vector3 exitOffset = new Vector3(2f, 0f, 0f);
    [FoldoutGroup("Vehicle Configuration")]
    [SerializeField] protected bool isOperational = true;

    [FoldoutGroup("References")]
    [SerializeField] protected VehicleInteractable vehicleInteractable;

    [FoldoutGroup("Steering Wheel")]
    [SerializeField] protected Transform steeringWheel;
    [FoldoutGroup("Steering Wheel")]
    [Tooltip("Local axis around which the steering wheel rotates (usually Vector3.up)")]
    [SerializeField] protected Vector3 steeringWheelRotationAxis = Vector3.up;
    [FoldoutGroup("Steering Wheel")]
    [Tooltip("Position of the left hand position on the steering wheel")]
    public Transform leftHandPosition;
    [FoldoutGroup("Steering Wheel")]
    [Tooltip("Position of the right hand position on the steering wheel")]
    public Transform rightHandPosition;
    [FoldoutGroup("Steering Wheel")]
    [Tooltip("Maximum rotation angle of the steering wheel in degrees (e.g., 30 means ±30°)")]
    [SerializeField] protected float maxSteeringWheelAngle = 50f;
    [FoldoutGroup("Steering Wheel")]
    [Tooltip("How fast the steering wheel rotates to match input (higher = faster response)")]
    [SerializeField] protected float steeringWheelRotationSpeed = 10f;
    [FoldoutGroup("Steering Wheel")]
    [Tooltip("Enable smooth interpolation for steering wheel rotation")]
    [SerializeField] protected bool useSmoothSteering = true;

    [FoldoutGroup("Vehicle Aiming Controls")]
    [Tooltip("Maximum vertical aim angle when in vehicle")]
    public float vehicleMaxVerticalAngle = 75f;

    [FoldoutGroup("Vehicle Aiming Controls")]
    [Tooltip("Minimum vertical aim angle when in vehicle")]
    public float vehicleMinVerticalAngle = -20f;

    [FoldoutGroup("Vehicle Aiming Controls")]
    [Tooltip("Maximum horizontal aim angle when in vehicle (left/right head rotation)")]
    public float vehicleMaxHorizontalAngle = 65f;

    [FoldoutGroup("Vehicle Aiming Controls")]
    [Tooltip("Minimum horizontal aim angle when in vehicle (left/right head rotation)")]
    public float vehicleMinHorizontalAngle = -65f;

    [FoldoutGroup("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;

    // Current state
    [ShowInInspector] protected GameObject currentDriver;
    protected Transform originalDriverParent;
    protected bool hasDriver = false;

    // Input state
    protected float currentThrottle = 0f;
    protected float currentSteering = 0f;
    protected float currentBrake = 0f;

    // Steering wheel rotation state
    private Quaternion steeringWheelBaseRotation;
    protected float targetSteeringAngle = 0f;
    protected float currentSteeringAngle = 0f;

    // Events
    public event Action<VehicleController> OnVehicleDestroyed;
    public event Action<VehicleController, GameObject> OnPlayerEntered;
    public event Action<VehicleController, GameObject> OnPlayerExited;

    #region IVehicle Implementation

    public virtual string VehicleID
    {
        get
        {
            return vehicleID;
        }
    }

    public virtual VehicleType VehicleType => vehicleType;
    public virtual Transform Transform => transform;
    public virtual bool IsOperational => isOperational;
    public virtual bool HasDriver => hasDriver;
    public virtual Vector3 Velocity => GetVehicleVelocity();
    public virtual Transform DriverSeat => driverSeat;
    public virtual Vector3 ExitOffset => exitOffset;
    public bool IsVehicleSeated => isVehicleSeated;

    #region Vehicle Entry/Exit System

    /// <summary>
    /// INTERACTIVE: Player entry through handler (for normal gameplay)
    /// The collision timing and state management is handled by PlayerVehicleEntryExitHandler
    /// </summary>
    public virtual bool OnPlayerEnter(GameObject player)
    {
        return PerformVehicleEntry(player, false);
    }

    /// <summary>
    /// DIRECT: Player restoration for save system (bypasses handler)
    /// Used during save file restoration for immediate, direct entry
    /// </summary>
    public virtual bool RestorePlayerIntoVehicle(GameObject player)
    {
        DebugLog($"=== DIRECT VEHICLE RESTORATION for {VehicleID} ===");
        return PerformVehicleEntry(player, true);
    }

    /// <summary>
    /// UNIFIED: Core vehicle entry logic that handles both interactive and restoration scenarios
    /// </summary>
    private bool PerformVehicleEntry(GameObject player, bool isRestoration)
    {
        string entryType = isRestoration ? "RESTORATION" : "INTERACTIVE";
        DebugLog($"{entryType} vehicle entry starting for {VehicleID}");

        // Basic validation - same for both paths
        if (!IsOperational || HasDriver)
        {
            DebugLog($"Cannot enter vehicle - Operational: {IsOperational}, HasDriver: {HasDriver}");
            return false;
        }

        if (driverSeat == null)
        {
            Debug.LogError($"[VehicleController] Driver seat not assigned for vehicle {VehicleID}!");
            return false;
        }

        // Get PlayerController reference
        playerController = player.GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError($"[VehicleController] No PlayerController found on {player.name}!");
            return false;
        }

        // Store references
        currentDriver = player;
        hasDriver = true;
        originalDriverParent = player.transform.parent;

        // CRITICAL: Disable vehicle collision BEFORE positioning (restoration only)
        if (isRestoration)
        {
            DebugLog("Restoration: Disabling vehicle collision immediately");
            playerController.SetLayerCollision(playerController.vehicleMovementController.vehicleLayerMask, false);
        }

        // Position player in seat
        player.transform.SetParent(driverSeat);
        player.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        DebugLog($"Player positioned in driver seat - Local pos: {player.transform.localPosition}");

        // Enable hand IK for steering wheel
        if (leftHandPosition != null && rightHandPosition != null)
        {
            playerController.SetPlayerLeftHandAndRightHandIKEffectors(leftHandPosition, rightHandPosition, true, true);
            DebugLog("Hand IK enabled for steering wheel");
        }

        // Set current vehicle reference in PlayerController
        playerController.currentVehicle = this;

        // Update camera and aiming system (CRITICAL for both scenarios)
        UpdateCameraAndAimingForVehicle();

        // Perform vehicle-specific entry logic
        PerformVehicleEntryLogic(player);

        // Fire events
        OnPlayerEntered?.Invoke(this, player);

        DebugLog($"✅ {entryType} vehicle entry completed successfully for {VehicleID}");
        return true;
    }

    /// <summary>
    /// CRITICAL: Update camera and aiming systems for vehicle mode
    /// This ensures horizontal aiming works correctly after entry
    /// </summary>
    private void UpdateCameraAndAimingForVehicle()
    {
        if (playerController?.cameraController?.aimController == null)
        {
            Debug.LogWarning($"[VehicleController] Camera or aim controller not found - aiming may not work correctly");
            return;
        }

        DebugLog("Updating camera and aiming systems for vehicle mode");

        //  Force a small delay to ensure state is stable
        StartCoroutine(DelayedAimingConfiguration());
    }

    /// <summary>
    /// NEW: Delayed aiming configuration to avoid timing issues
    /// </summary>
    private System.Collections.IEnumerator DelayedAimingConfiguration()
    {
        // Wait for any restoration processes to complete
        yield return new WaitForSeconds(0.1f);

        if (playerController?.cameraController?.aimController != null)
        {
            // Update aim controller limits for vehicle horizontal aiming
            playerController.cameraController.aimController.UpdateAngleLimits(
                vehicleMinVerticalAngle,
                vehicleMaxVerticalAngle,
                true, // Enable horizontal aiming for vehicle
                vehicleMinHorizontalAngle,
                vehicleMaxHorizontalAngle
            );

            DebugLog($"Delayed vehicle aiming configured - Vertical: {vehicleMinVerticalAngle}° to {vehicleMaxVerticalAngle}°, " +
                    $"Horizontal: {vehicleMinHorizontalAngle}° to {vehicleMaxHorizontalAngle}°");
        }

        // Set steering wheel layer to first-person render layer
        SetSteeringWheelLayer(14); // firstPersonRenderLayer
    }

    /// <summary>
    ///  Player exit now focuses only on the vehicle mechanics
    /// The collision timing and state management is handled by PlayerVehicleEntryExitHandler
    /// </summary>
    public virtual void OnPlayerExit(GameObject player)
    {
        DebugLog($"Player {player.name} exiting vehicle {VehicleID}");

        if (currentDriver != player)
        {
            DebugLog("Player trying to exit is not the current driver");
            return;
        }

        // Calculate exit position
        Vector3 exitPosition = CalculateExitPosition();

        // Disable hand IK
        if (leftHandPosition != null && rightHandPosition != null && playerController != null)
        {
            playerController.SetPlayerLeftHandAndRightHandIKEffectors(leftHandPosition, rightHandPosition, false);
        }

        // Remove player from vehicle hierarchy
        player.transform.SetParent(originalDriverParent);
        player.transform.position = exitPosition;

        // Set steering wheel layer back to vehicle layer
        SetSteeringWheelLayer(12); // vehicleRenderLayer

        // Clear driver state
        currentDriver = null;
        hasDriver = false;
        originalDriverParent = null;
        playerController = null;

        // Reset vehicle inputs (this will also reset steering wheel)
        SetThrottleInput(0f);
        SetSteeringInput(0f);
        SetBrakeInput(0f);

        // Perform vehicle-specific exit logic
        PerformVehicleExitLogic(player);

        // Fire events
        OnPlayerExited?.Invoke(this, player);

        DebugLog($"✅ Player {player.name} exited vehicle {VehicleID}");
    }

    #endregion

    public virtual void SetThrottleInput(float throttle)
    {
        currentThrottle = Mathf.Clamp(throttle, -1f, 1f);
        ApplyThrottleInput(currentThrottle);
    }

    public virtual void SetSteeringInput(float steering)
    {
        currentSteering = Mathf.Clamp(steering, -1f, 1f);

        // Update steering wheel rotation when steering input changes
        UpdateSteeringWheelRotation(currentSteering);

        ApplySteeringInput(currentSteering);
    }

    public virtual void SetBrakeInput(float brake)
    {
        currentBrake = Mathf.Clamp01(brake);
        ApplyBrakeInput(currentBrake);
    }

    #endregion

    #region Steering Wheel Rotation System

    /// <summary>
    /// Initialize steering wheel rotation system
    /// Call this from Awake() or Start() in your vehicle controllers
    /// </summary>
    protected virtual void InitializeSteeringWheel()
    {
        if (steeringWheel != null)
        {
            // Store the original rotation as our base rotation
            steeringWheelBaseRotation = steeringWheel.localRotation;
            targetSteeringAngle = 0f;
            currentSteeringAngle = 0f;

            DebugLog($"Steering wheel initialized with base rotation: {steeringWheelBaseRotation.eulerAngles}");
        }
    }

    /// <summary>
    /// Updates steering wheel rotation based on steering input
    /// Called automatically when SetSteeringInput is used
    /// </summary>
    protected virtual void UpdateSteeringWheelRotation(float steeringInput)
    {
        if (steeringWheel == null) return;

        // Calculate target angle based on steering input
        // steeringInput ranges from -1 (left) to 1 (right)
        targetSteeringAngle = steeringInput * maxSteeringWheelAngle;

        // If not using smooth steering, apply rotation immediately
        if (!useSmoothSteering)
        {
            currentSteeringAngle = targetSteeringAngle;
            ApplySteeringWheelRotation();
        }
        // Smooth steering is handled in Update()
    }

    /// <summary>
    /// Apply the current steering angle to the steering wheel transform
    /// This rotates the wheel around its local Y-axis
    /// </summary>
    protected virtual void ApplySteeringWheelRotation()
    {
        if (steeringWheel == null) return;

        // Create rotation around the specified axis
        Quaternion steeringRotation = Quaternion.AngleAxis(currentSteeringAngle, steeringWheelRotationAxis);

        // Apply rotation to the base rotation
        steeringWheel.localRotation = steeringWheelBaseRotation * steeringRotation;
    }

    /// <summary>
    /// Update steering wheel rotation smoothly
    /// Call this from Update() in your vehicle controllers
    /// </summary>
    protected virtual void UpdateSteeringWheelSmooth()
    {
        if (steeringWheel == null || !useSmoothSteering) return;

        // Smoothly interpolate current angle toward target angle
        currentSteeringAngle = Mathf.LerpAngle(
            currentSteeringAngle,
            targetSteeringAngle,
            steeringWheelRotationSpeed * Time.deltaTime
        );

        // Apply the smooth rotation
        ApplySteeringWheelRotation();
    }

    /// <summary>
    /// Reset steering wheel to center position
    /// Useful when player exits vehicle or for initialization
    /// </summary>
    protected virtual void ResetSteeringWheel()
    {
        if (steeringWheel == null) return;

        targetSteeringAngle = 0f;

        if (useSmoothSteering)
        {
            // Let the smooth update handle returning to center
            DebugLog("Steering wheel returning to center (smooth)");
        }
        else
        {
            // Immediately snap to center
            currentSteeringAngle = 0f;
            ApplySteeringWheelRotation();
            DebugLog("Steering wheel reset to center (immediate)");
        }
    }

    /// <summary>
    /// Set steering wheel rotation settings at runtime
    /// </summary>
    public virtual void SetSteeringWheelSettings(float maxAngle, float rotationSpeed, bool useSmooth = true)
    {
        maxSteeringWheelAngle = Mathf.Max(0f, maxAngle);
        steeringWheelRotationSpeed = Mathf.Max(0.1f, rotationSpeed);
        useSmoothSteering = useSmooth;

        DebugLog($"Steering wheel settings updated - Max Angle: {maxSteeringWheelAngle}°, Speed: {steeringWheelRotationSpeed}, Smooth: {useSmoothSteering}");
    }

    public void SetSteeringWheelLayer(int newLayer)
    {
        if (steeringWheel == null) return;
        steeringWheel.gameObject.layer = newLayer;
        DebugLog($"Steering wheel layer set to: {newLayer}");
    }

    #endregion

    #region Abstract Methods - Must be implemented by specific vehicle types

    /// <summary>
    /// Get the current velocity of this specific vehicle type
    /// </summary>
    protected abstract Vector3 GetVehicleVelocity();

    /// <summary>
    /// Apply throttle input to the specific vehicle physics system
    /// </summary>
    protected abstract void ApplyThrottleInput(float throttle);

    /// <summary>
    /// Apply steering input to the specific vehicle physics system
    /// </summary>
    protected abstract void ApplySteeringInput(float steering);

    /// <summary>
    /// Apply brake input to the specific vehicle physics system
    /// </summary>
    protected abstract void ApplyBrakeInput(float brake);

    /// <summary>
    /// Perform vehicle-specific logic when player enters
    /// </summary>
    protected abstract void PerformVehicleEntryLogic(GameObject player);

    /// <summary>
    /// Perform vehicle-specific logic when player exits
    /// </summary>
    protected abstract void PerformVehicleExitLogic(GameObject player);

    #endregion

    #region Utility Methods

    protected virtual void Awake()
    {
        if (vehicleInteractable == null)
        {
            vehicleInteractable = GetComponentInChildren<VehicleInteractable>();
        }

        // Initialize steering wheel system
        InitializeSteeringWheel();

        ValidateSetup();
    }

    protected virtual void Update()
    {
        // Update steering wheel rotation if using smooth steering
        if (useSmoothSteering)
        {
            UpdateSteeringWheelSmooth();
        }
    }

    protected virtual void ValidateSetup()
    {
        if (driverSeat == null)
        {
            Debug.LogError($"[VehicleController] Driver seat not assigned for vehicle {name}!");
        }

        if (vehicleInteractable == null)
        {
            Debug.LogWarning($"[VehicleController] No VehicleInteractable found for vehicle {name}. " +
                           "Players won't be able to interact with this vehicle.");
        }

    }

    protected virtual Vector3 CalculateExitPosition()
    {
        // Calculate exit position with offset and collision checking
        Vector3 exitPos = transform.position + transform.TransformDirection(exitOffset);

        // Simple ground check - place player on ground
        if (Physics.Raycast(exitPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
        {
            exitPos.y = hit.point.y;
        }

        return exitPos;
    }

    /// <summary>
    /// Enhanced SetOperational method that can be called externally
    /// </summary>
    public virtual void SetOperational(bool operational)
    {
        bool wasOperational = isOperational;
        isOperational = operational;

        if (wasOperational != operational)
        {
            DebugLog($"Operational state changed: {wasOperational} -> {operational}");

            // If vehicle becomes non-operational while occupied, eject player
            if (!operational && HasDriver && currentDriver != null)
            {
                DebugLog("Vehicle became non-operational - ejecting player");
                OnPlayerExit(currentDriver);
            }
        }
    }

    /// <summary>
    /// Get the current driver GameObject (used by save system)
    /// </summary>
    public virtual GameObject GetCurrentDriver()
    {
        return currentDriver;
    }

    /// <summary>
    /// Force stop all vehicle inputs (used by save system)
    /// </summary>
    public virtual void StopAllInputs()
    {
        SetThrottleInput(0f);
        SetSteeringInput(0f);
        SetBrakeInput(0f);
        DebugLog("All vehicle inputs stopped");
    }

    #endregion

    #region Debug and Utility

    /// <summary>
    /// Get detailed vehicle debug info including steering wheel data
    /// </summary>
    public virtual string GetDetailedDebugInfo()
    {
        string steeringWheelInfo = steeringWheel != null ?
            $"Steering Wheel - Current: {currentSteeringAngle:F1}°, Target: {targetSteeringAngle:F1}°" :
            "No Steering Wheel";

        return $"Vehicle Debug Info:\n" +
               $"ID: {VehicleID}\n" +
               $"Type: {VehicleType}\n" +
               $"Operational: {IsOperational}\n" +
               $"Has Driver: {HasDriver}\n" +
               $"Current Driver: {(currentDriver != null ? currentDriver.name : "None")}\n" +
               $"Velocity: {Velocity.magnitude:F1}m/s\n" +
               $"Inputs - Throttle: {currentThrottle:F2}, Steering: {currentSteering:F2}, Brake: {currentBrake:F2}\n" +
               $"{steeringWheelInfo}";
    }

    protected virtual void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[{GetType().Name}:{VehicleID}] {message}");
        }
    }

    #endregion

    #region SceneVehicleStateManager Integration

    /// <summary>
    /// Apply saved state during restoration - called by SceneVehicleStateManager  
    /// </summary>
    public virtual void ApplyVehicleState(VehicleSaveData saveData)
    {
        if (saveData == null)
        {
            DebugLog("No save data to apply");
            return;
        }

        DebugLog($"{VehicleID}, position {transform.position} and rotation {transform.rotation.eulerAngles} before restoration");

        DebugLog($"Applying saved state to vehicle {VehicleID}: pos={saveData.position}, rot={saveData.rotation.eulerAngles}");

        // Stop any current inputs/movement
        StopAllInputs();

        // Clear physics momentum
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }


        // Apply position and rotation
        rb.Move(saveData.position, saveData.rotation);
        //Transform.SetPositionAndRotation(saveData.position, saveData.rotation);

        DebugLog($"{VehicleID}, position {transform.position} and rotation {transform.rotation.eulerAngles} applied after restoration");

        // Apply operational state  
        SetOperational(saveData.isOperational);

        DebugLog($"✅ State applied to vehicle {VehicleID}");
    }

    public virtual void SetVehicleID(string newID)
    {
        vehicleID = newID;
    }

    #endregion

    #region Cleanup

    protected virtual void OnDestroy()
    {
        // If vehicle is destroyed while player is inside, safely exit them first
        if (HasDriver && currentDriver != null)
        {
            OnPlayerExit(currentDriver);
        }

        OnVehicleDestroyed?.Invoke(this);
    }

    public GameObject GetSteeringWheel()
    {
        return steeringWheel != null ? steeringWheel.gameObject : null;
    }

    #endregion
}