using UnityEngine;
using NWH.VehiclePhysics2;
using Sirenix.OdinInspector;

/// <summary>
///  Car controller that properly works with NWH Vehicle Physics 2
/// Now correctly uses NWH's separate throttle/brake system and input swapping
/// </summary>
[RequireComponent(typeof(NWH.VehiclePhysics2.VehicleController))]
public class CarController : VehicleController
{
    [Header("Car-Specific Settings")]
    [SerializeField] private NWH.VehiclePhysics2.VehicleController nwhVehicleController;
    [SerializeField] private NWH.VehiclePhysics2.Damage.DamageHandler damageHandler;

    [Header("Car Limits")]
    [SerializeField] private float maxThrottleInput = 1f;
    [SerializeField] private float maxSteeringInput = 1f;
    [SerializeField] private float maxBrakeInput = 1f;

    [Header("Engine Management")]
    [SerializeField] private bool autoStartEngine = true;

    //  Simple input values - let NWH handle the complexity
    private float currentThrottleInput = 0f;
    private float currentSteeringInput = 0f;
    private float currentBrakeInput = 0f;
    private float explicitBrakeInput = 0f; // Brake input from player, separate from movement-based braking

    // Track raw movement input for debugging
    private float rawMovementInput = 0f;


    #region Initialization

    protected override void Awake()
    {
        // Set vehicle type for car
        vehicleType = VehicleType.Car;

        base.Awake(); // This calls InitializeSteeringWheel()

        if (nwhVehicleController == null)
        {
            nwhVehicleController = GetComponent<NWH.VehiclePhysics2.VehicleController>();
        }

        if (damageHandler == null)
        {
            damageHandler = GetComponent<NWH.VehiclePhysics2.Damage.DamageHandler>();
        }

        ValidateCarSetup();
    }

    private void Start()
    {
        if (nwhVehicleController != null)
        {
            InitializeNWHController();
        }

        if (steeringWheel == null)
        {
            Debug.LogWarning($"[CarController] Steering wheel not found on {name}");
        }

        // turn on handbrake by default
        if (nwhVehicleController != null)
        {
            nwhVehicleController.input.Handbrake = 1f;
        }
    }

    private void ValidateCarSetup()
    {
        if (nwhVehicleController == null)
        {
            Debug.LogError($"[CarController] No NWH VehicleController found on {name}!");
            return;
        }

        DebugLog("Car setup validation complete");
    }

    private void InitializeNWHController()
    {
        //  Keep autoSetInput enabled but override specific inputs
        // This allows NWH's input swapping to work properly
        nwhVehicleController.input.autoSetInput = false;

        // Set initial input values
        nwhVehicleController.input.Throttle = 0f;
        nwhVehicleController.input.Steering = 0f;
        nwhVehicleController.input.Brakes = 0f;
        nwhVehicleController.input.Handbrake = 1f; // Start with handbrake on

        // Enable input swapping for reverse functionality
        nwhVehicleController.input.swapInputInReverse = true;

        DebugLog("NWH VehicleController initialized with proper input settings");
    }

    #endregion

    #region Engine Management

    public void StartEngine()
    {
        if (nwhVehicleController == null) return;

        var engine = nwhVehicleController.powertrain?.engine;
        if (engine != null)
        {
            engine.StartEngine();
            DebugLog("Car engine started");
        }
    }

    public void StopEngine()
    {
        if (nwhVehicleController == null) return;

        var engine = nwhVehicleController.powertrain?.engine;
        if (engine != null)
        {
            engine.StopEngine();
            DebugLog("Car engine stopped");
        }
    }

    public bool IsEngineRunning()
    {
        if (nwhVehicleController?.powertrain?.engine == null) return false;
        return nwhVehicleController.powertrain.engine.IsRunning;
    }

    #endregion

    #region  VehicleController Implementation

    protected override Vector3 GetVehicleVelocity()
    {
        if (nwhVehicleController != null && nwhVehicleController.vehicleRigidbody != null)
        {
            return nwhVehicleController.vehicleRigidbody.linearVelocity;
        }
        return Vector3.zero;
    }

    /// <summary>
    ///  Handle movement input properly for NWH
    /// Positive input = throttle, Negative input = brake (NWH will handle reverse via transmission)
    /// </summary>
    protected override void ApplyThrottleInput(float input)
    {
        rawMovementInput = Mathf.Clamp(input, -1f, 1f);

        //  Convert single movement input to separate throttle/brake inputs
        if (rawMovementInput > 0f)
        {
            // Forward input = throttle only
            currentThrottleInput = rawMovementInput * maxThrottleInput;
            currentBrakeInput = 0f;
        }
        else if (rawMovementInput < 0f)
        {
            // Reverse input = brake only (NWH will handle the reverse direction automatically)
            currentThrottleInput = 0f;
            currentBrakeInput = -rawMovementInput * maxBrakeInput; // Convert negative to positive brake
        }
        else
        {
            // No input = no throttle or brake
            currentThrottleInput = 0f;
            currentBrakeInput = 0f;
        }

        //        DebugLog($"Movement input: {rawMovementInput:F2} -> Throttle: {currentThrottleInput:F2}, AutoBrake: {currentBrakeInput:F2}");
    }

    protected override void ApplySteeringInput(float steering)
    {
        currentSteeringInput = Mathf.Clamp(steering, -maxSteeringInput, maxSteeringInput);
        //    DebugLog($"Steering input: {steering:F2} -> {currentSteeringInput:F2}");
    }

    /// <summary>
    ///  Brake input adds to movement-based braking
    /// </summary>
    protected override void ApplyBrakeInput(float brake)
    {
        explicitBrakeInput = Mathf.Clamp01(brake);

        // Add explicit brake input to any movement-based braking
        float totalBrakeInput = Mathf.Clamp01(currentBrakeInput + explicitBrakeInput);
        currentBrakeInput = totalBrakeInput;

        DebugLog($"Explicit brake: {explicitBrakeInput:F2}, Total brake: {currentBrakeInput:F2}");
    }

    protected override void PerformVehicleEntryLogic(GameObject player)
    {
        DebugLog($"Player {player.name} entered car {VehicleID}");

        // Start engine when player enters if auto-start is enabled
        if (autoStartEngine && !IsEngineRunning())
        {
            StartEngine();
        }

        if (nwhVehicleController != null)
        {
            nwhVehicleController.input.Handbrake = 0f;
        }
    }

    protected override void PerformVehicleExitLogic(GameObject player)
    {
        DebugLog($"Player {player.name} exited car {VehicleID}");

        // Stop the car when player exits
        if (nwhVehicleController != null)
        {
            nwhVehicleController.input.Throttle = 0f;
            nwhVehicleController.input.Steering = 0f;
            nwhVehicleController.input.Brakes = 0f;
            nwhVehicleController.input.Handbrake = 1f; // Engage handbrake when exiting
        }

        // Reset input values
        currentThrottleInput = 0f;
        currentSteeringInput = 0f;
        currentBrakeInput = 0f;
        rawMovementInput = 0f;

        // Reset steering wheel to center
        ResetSteeringWheel();

        // turn off engine when player exits if auto-start is enabled
        if (autoStartEngine && IsEngineRunning())
        {
            StopEngine();
        }
    }

    #endregion

    #region  Update Logic

    protected override void Update()
    {
        // Call base Update for steering wheel rotation
        base.Update();

        if (nwhVehicleController == null) return;

        // Only process input if car has a driver
        if (HasDriver)
        {
            ApplyInputToNWHController();
        }
        else
        {
            // Ensure handbrake is engaged when no driver
            nwhVehicleController.input.Handbrake = 1f;
        }
    }

    /// <summary>
    ///  Simple direct application of inputs to NWH
    /// Let NWH handle all the complex reverse logic via input swapping
    /// </summary>
    private void ApplyInputToNWHController()
    {
        if (nwhVehicleController == null)
        {
            Debug.LogWarning($"[CarController] NWH VehicleController not found on {name}");
            return;
        }

        //  Direct application - NWH handles the rest
        nwhVehicleController.input.Throttle = currentThrottleInput;
        nwhVehicleController.input.Brakes = currentBrakeInput;
        nwhVehicleController.input.Steering = currentSteeringInput;

        nwhVehicleController.input.Handbrake = explicitBrakeInput > 0 ? 1f : 0f;

        // Update steering wheel rotation based on steering input
        UpdateSteeringWheelFromInput();

    }

    /// <summary>
    /// Update steering wheel rotation based on steering input
    /// </summary>
    private void UpdateSteeringWheelFromInput()
    {
        if (steeringWheel == null) return;

        float steeringAngle = currentSteeringInput * maxSteeringWheelAngle;
        targetSteeringAngle = steeringAngle;

        if (!useSmoothSteering)
        {
            currentSteeringAngle = targetSteeringAngle;
            ApplySteeringWheelRotation();
        }
    }

    #endregion

    #region Public API

    public NWH.VehiclePhysics2.VehicleController GetNWHController()
    {
        return nwhVehicleController;
    }

    [Button]
    public void RepairCar()
    {
        if (damageHandler != null)
        {
            damageHandler.Repair();
            DebugLog("Car repaired to full health");
        }
        else
        {
            DebugLog("No DamageHandler found to repair car");
        }
    }

    /// <summary>
    /// Get car debug information showing NWH input states
    /// </summary>
    public string GetCarDebugInfo()
    {
        if (nwhVehicleController == null) return "No NWH Controller";

        Vector3 velocity = GetVehicleVelocity();
        float currentSpeed = velocity.magnitude;

        string gearInfo = nwhVehicleController.powertrain?.transmission != null ?
            $"Gear: {nwhVehicleController.powertrain.transmission.Gear}" : "Gear: Unknown";

        return $"Car Debug - ID: {VehicleID}, Speed: {currentSpeed:F1}m/s, " +
               $"RawInput: {rawMovementInput:F2}, " +
               $"NWH-T: {nwhVehicleController.input.Throttle:F2}, " +
               $"NWH-B: {nwhVehicleController.input.Brakes:F2}, " +
               $"NWH-S: {nwhVehicleController.input.Steering:F2}, " +
               $"Swapped: {nwhVehicleController.input.IsInputSwapped}, " +
               $"{gearInfo}, Engine: {IsEngineRunning()}, Driver: {HasDriver}";
    }

    public override void StopAllInputs()
    {
        base.StopAllInputs();

        currentThrottleInput = 0f;
        currentSteeringInput = 0f;
        currentBrakeInput = 0f;
        rawMovementInput = 0f;

        if (nwhVehicleController != null)
        {
            nwhVehicleController.input.Throttle = 0f;
            nwhVehicleController.input.Steering = 0f;
            nwhVehicleController.input.Brakes = 0f;
            nwhVehicleController.input.Handbrake = 1f;
        }

        DebugLog("All car inputs and systems stopped");
    }

    #endregion

    #region Cleanup

    protected override void OnDestroy()
    {
        if (nwhVehicleController != null)
        {
            nwhVehicleController.input.Throttle = 0f;
            nwhVehicleController.input.Steering = 0f;
            nwhVehicleController.input.Brakes = 0f;
            nwhVehicleController.input.Handbrake = 1f;
        }

        base.OnDestroy();
    }

    #endregion
}