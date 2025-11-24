using UnityEngine;
using System;

/// <summary>
/// ENHANCED: Interface for all vehicle types with restoration support
/// Now includes both interactive entry (for gameplay) and direct restoration (for saves)
/// Each vehicle type implements this to translate player input into vehicle-specific movement commands.
/// </summary>
public interface IVehicle
{
    /// <summary>
    /// Unique identifier for this vehicle instance
    /// </summary>
    string VehicleID { get; }

    /// <summary>
    /// The type of vehicle (Boat, Car, etc.)
    /// </summary>
    VehicleType VehicleType { get; }

    /// <summary>
    /// Whether the player should play a seated or standing animation when riding in this vehicle
    /// </summary>
    bool IsVehicleSeated { get; }

    /// <summary>
    /// The transform of this vehicle (for positioning)
    /// </summary>
    Transform Transform { get; }

    /// <summary>
    /// Whether this vehicle is currently operational
    /// </summary>
    bool IsOperational { get; }

    /// <summary>
    /// Whether a player is currently driving this vehicle
    /// </summary>
    bool HasDriver { get; }

    /// <summary>
    /// Current velocity of the vehicle
    /// </summary>
    Vector3 Velocity { get; }

    /// <summary>
    /// The seat transform where the player should be positioned
    /// </summary>
    Transform DriverSeat { get; }

    /// <summary>
    /// Position offset from seat for player exit (configurable per vehicle)
    /// </summary>
    Vector3 ExitOffset { get; }

    /// <summary>
    /// INTERACTIVE: Called when player enters the vehicle through normal gameplay
    /// Uses PlayerVehicleEntryExitHandler for collision timing and state management
    /// </summary>
    /// <param name="player">The player entering the vehicle</param>
    /// <returns>True if entry was successful</returns>
    bool OnPlayerEnter(GameObject player);

    /// <summary>
    /// ENHANCED: DIRECT RESTORATION: Called when restoring player into vehicle from save files
    /// Bypasses collision timing and handler complexity for immediate, direct entry
    /// Used by SceneVehicleStateManager during save file restoration
    /// </summary>
    /// <param name="player">The player to restore into the vehicle</param>
    /// <returns>True if restoration was successful</returns>
    bool RestorePlayerIntoVehicle(GameObject player);

    /// <summary>
    /// Called when player exits the vehicle
    /// </summary>
    /// <param name="player">The player exiting the vehicle</param>
    void OnPlayerExit(GameObject player);

    /// <summary>
    /// Set movement input from player (-1 to 1 for forward/backward)
    /// </summary>
    /// <param name="throttle">Throttle input: 1 = forward, -1 = backward, 0 = neutral</param>
    void SetThrottleInput(float throttle);

    /// <summary>
    /// Set steering input from player (-1 to 1 for left/right)
    /// </summary>
    /// <param name="steering">Steering input: -1 = left, 1 = right, 0 = straight</param>
    void SetSteeringInput(float steering);

    /// <summary>
    /// Set brake input from player (0 to 1)
    /// </summary>
    /// <param name="brake">Brake intensity: 0 = no brake, 1 = full brake</param>
    void SetBrakeInput(float brake);

    /// <summary>
    /// Get the steering wheel GameObject for layer management
    /// </summary>
    GameObject GetSteeringWheel();

    /// <summary>
    /// Events for vehicle state changes
    /// </summary>
    event Action<VehicleController> OnVehicleDestroyed;
    event Action<VehicleController, GameObject> OnPlayerEntered;
    event Action<VehicleController, GameObject> OnPlayerExited;
}

/// <summary>
/// Types of vehicles supported by the system
/// </summary>
public enum VehicleType
{
    Boat,
    Car,
    Motorcycle
    // Add more types as needed
}