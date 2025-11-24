using UnityEngine;

/// <summary>
/// Save data structure for vehicles with improved integration
/// for the SceneVehicleStateManager system. Now includes better support
/// for vehicle-specific data and relationship tracking.
/// </summary>
[System.Serializable]
public class VehicleSaveData
{
    [Header("Vehicle Identity")]
    public string vehicleID;
    public VehicleType vehicleType;

    [Header("Transform Data")]
    public Vector3 position;
    public Quaternion rotation;

    [Header("State Data")]
    public bool isOperational = true;
    public bool hasDriver = false; // Track if vehicle had a driver when saved

    [Header("Damage Data")]
    public VehicleDamageData damageData = new VehicleDamageData();

    [Header("Timestamps")]
    public string lastUsedTimestamp;
    public string saveTimestamp;

    [Header("Physics State")]
    public Vector3 lastKnownVelocity = Vector3.zero; // For physics restoration if needed
    public bool wasMoving = false; // Whether vehicle was in motion when saved

    [Header("Input State")]
    public float lastThrottleInput = 0f;
    public float lastSteeringInput = 0f;
    public float lastBrakeInput = 0f;

    #region Constructors

    public VehicleSaveData()
    {
        lastUsedTimestamp = System.DateTime.Now.ToString();
        saveTimestamp = System.DateTime.Now.ToString();
    }

    public VehicleSaveData(string id, VehicleType type, Vector3 pos, Quaternion rot, bool operational = true)
    {
        vehicleID = id;
        vehicleType = type;
        position = pos;
        rotation = rot;
        isOperational = operational;
        hasDriver = false;
        lastUsedTimestamp = System.DateTime.Now.ToString();
        saveTimestamp = System.DateTime.Now.ToString();
    }

    /// <summary>
    /// Enhanced constructor with full state tracking
    /// </summary>
    public VehicleSaveData(string id, VehicleType type, Vector3 pos, Quaternion rot, bool operational, bool driverPresent, Vector3 velocity = default, bool moving = false)
    {
        vehicleID = id;
        vehicleType = type;
        position = pos;
        rotation = rot;
        isOperational = operational;
        hasDriver = driverPresent;
        lastKnownVelocity = velocity;
        wasMoving = moving;
        lastUsedTimestamp = System.DateTime.Now.ToString();
        saveTimestamp = System.DateTime.Now.ToString();
    }

    #endregion

    #region Enhanced State Management

    /// <summary>
    /// Update the last used timestamp (called when vehicle is interacted with)
    /// </summary>
    public void UpdateLastUsedTime()
    {
        lastUsedTimestamp = System.DateTime.Now.ToString();
    }

    /// <summary>
    /// Set driver state with automatic timestamp update
    /// </summary>
    public void SetDriverState(bool driverPresent)
    {
        hasDriver = driverPresent;
        if (driverPresent)
        {
            UpdateLastUsedTime();
        }
    }

    /// <summary>
    /// Enhanced UpdateFromVehicle that includes damage data
    /// </summary>
    public void UpdateFromVehicle(VehicleController vehicle)
    {
        if (vehicle == null) return;

        // Call base implementation for position/rotation
        vehicleID = vehicle.VehicleID;
        vehicleType = vehicle.VehicleType;
        position = vehicle.Transform.position;
        rotation = vehicle.Transform.rotation;
        isOperational = vehicle.IsOperational;
        hasDriver = vehicle.HasDriver;
        lastKnownVelocity = vehicle.Velocity;
        wasMoving = vehicle.Velocity.magnitude > 0.1f;
        saveTimestamp = System.DateTime.Now.ToString();

        if (hasDriver)
        {
            UpdateLastUsedTime();
        }

        // Capture damage data if DamageHandler exists
        var damageHandler = vehicle.GetComponent<NWH.VehiclePhysics2.Damage.DamageHandler>();
        if (damageHandler != null)
        {
            damageData.UpdateFromDamageHandler(damageHandler, vehicle.GetComponent<NWH.VehiclePhysics2.VehicleController>());
        }
        else
        {
            // Clear damage data if no damage handler
            damageData = new VehicleDamageData();
        }

    }

    /// <summary>
    /// Update input state from VehicleController 
    /// </summary>
    private void UpdateInputStateFromController(VehicleController controller)
    {
        if (controller == null) return;

        // Access input state through protected fields 
        // For now, we'll store default values since inputs are typically reset anyway
        lastThrottleInput = 0f; // Vehicle inputs should be clear when saved
        lastSteeringInput = 0f;
        lastBrakeInput = 0f;
    }

    #endregion

}