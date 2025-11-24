using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Vehicle movement controller that implements IMovementController.
/// Handles player input and translates it to the currently occupied vehicle.
/// This controller becomes active when the player enters any vehicle.
/// </summary>
public class VehicleMovementController : MonoBehaviour, IMovementController
{
    [Header("Vehicle Physics Settings")]
    public LayerMask vehicleLayerMask;

    [Header("Vehicle Movement Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    // Interface properties
    public MovementMode MovementMode => MovementMode.Vehicle;
    public bool IsGrounded => currentVehicle?.IsOperational ?? false;
    public bool IsMoving => currentVehicle != null && currentVehicle.Velocity.magnitude > 0.1f;
    public bool IsSpeedModified => false; // Vehicle speed is handled by vehicle-specific systems
    public bool IsSecondaryActive => isBraking;

    // Component references
    private PlayerController playerController;

    // Current vehicle state
    [ShowInInspector] public VehicleController currentVehicle;
    private bool isBraking = false;

    // Input state
    private Vector2 movementInput = Vector2.zero;

    #region Initialization

    public void Initialize(PlayerController controller)
    {
        playerController = controller;
        DebugLog("VehicleMovementController initialized");
    }

    #endregion

    #region IMovementController Implementation

    public void HandleMovement(Vector2 moveInput, bool isSpeedModified)
    {
        if (currentVehicle == null) return;

        movementInput = moveInput;

        // Translate movement input to vehicle controls
        // Forward/backward input becomes throttle
        float throttle = movementInput.y;

        // Left/right input becomes steering
        float steering = movementInput.x;

        // Apply input to current vehicle
        currentVehicle.SetThrottleInput(throttle);
        currentVehicle.SetSteeringInput(steering);

        //        DebugLog($"Vehicle input - Throttle: {throttle:F2}, Steering: {steering:F2}, Brake: {isBraking}");
    }

    public void HandleHorizontalRotation(float targetRotationY)
    {
        // Vehicle handles its own rotation - player rotation is locked to vehicle
        // Don't apply any rotation when in vehicle mode
    }

    public void HandlePrimaryAction()
    {
        // Primary action in vehicle context could be horn, lights, etc.
        // For now, this is not used - expand as needed
        DebugLog("Vehicle primary action (not implemented)");
    }

    public void HandlePrimaryActionReleased()
    {
        // Handle primary action release if needed
    }

    public void HandleSecondaryAction()
    {
        // Secondary action is brake
        isBraking = true;
        if (currentVehicle != null)
        {
            currentVehicle.SetBrakeInput(1f);
        }
        DebugLog("Vehicle brake engaged");
    }

    public void HandleSecondaryActionReleased()
    {
        // Release brake
        isBraking = false;
        if (currentVehicle != null)
        {
            currentVehicle.SetBrakeInput(0f);
        }
        DebugLog("Vehicle brake released");
    }

    public Vector3 GetVelocity()
    {
        return currentVehicle?.Velocity ?? Vector3.zero;
    }

    #endregion

    #region Controller Lifecycle

    public void OnControllerActivated()
    {
        DebugLog("Vehicle movement controller activated");
    }

    public void OnControllerDeactivated()
    {
        DebugLog("Vehicle movement controller deactivated");

        // Clear vehicle reference and reset inputs
        if (currentVehicle != null)
        {
            currentVehicle.SetThrottleInput(0f);
            currentVehicle.SetSteeringInput(0f);
            currentVehicle.SetBrakeInput(0f);
        }

        ResetState();
    }

    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        DebugLog($"Vehicle movement state changed: {previousState} -> {newState}");
    }

    public void Cleanup()
    {
        ResetState();
        DebugLog("VehicleMovementController cleaned up");
    }

    #endregion

    #region Vehicle Management

    /// <summary>
    /// Set the current vehicle that the player is controlling
    /// Called by the vehicle entry system when player enters a vehicle
    /// </summary>
    public void SetCurrentVehicle(VehicleController vehicle)
    {
        if (currentVehicle != null)
        {
            // Clear inputs from previous vehicle
            currentVehicle.SetThrottleInput(0f);
            currentVehicle.SetSteeringInput(0f);
            currentVehicle.SetBrakeInput(0f);
        }

        currentVehicle = vehicle;
        DebugLog($"Current vehicle set to: {vehicle?.VehicleID ?? "null"}");
    }

    /// <summary>
    /// Clear the current vehicle reference
    /// Called when player exits vehicle
    /// </summary>
    public void ClearCurrentVehicle()
    {
        if (currentVehicle != null)
        {
            // Clear all inputs before releasing vehicle
            currentVehicle.SetThrottleInput(0f);
            currentVehicle.SetSteeringInput(0f);
            currentVehicle.SetBrakeInput(0f);
        }

        currentVehicle = null;
        ResetState();
        DebugLog("Current vehicle cleared");
    }

    /// <summary>
    /// Handle exit vehicle input - called by PlayerController
    /// </summary>
    public void HandleExitVehicleInput()
    {
        if (currentVehicle == null) return;

        // Check if we can safely exit the vehicle
        if (CanExitVehicle())
        {
            ExitCurrentVehicle();
        }
        else
        {
            DebugLog("Cannot exit vehicle at this time");
        }
    }

    /// <summary>
    /// Check if player can safely exit the current vehicle
    /// </summary>
    public bool CanExitVehicle()
    {
        DebugLog("Checking if player can exit vehicle");

        if (currentVehicle == null)
        {
            DebugLog("Cannot exit vehicle - current vehicle is null");
            return false;

        }

        // Check if exit position is safe
        Vector3 exitPosition = CalculateSafeExitPosition();
        if (exitPosition == Vector3.zero)
        {
            DebugLog("No safe exit position found");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Exit the current vehicle
    /// </summary>
    private void ExitCurrentVehicle()
    {
        if (currentVehicle == null || playerController == null)
        {
            DebugLog("Cannot exit vehicle - current vehicle or player controller is null");
            return;
        }

        DebugLog($"Exiting vehicle: {currentVehicle.VehicleID}");

        // Let the vehicle handle player exit
        currentVehicle.OnPlayerExit(playerController.gameObject);

        // Clear our vehicle reference
        ClearCurrentVehicle();
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// ADD TO VehicleMovementController - Calculate safe exit position
    /// </summary>
    private Vector3 CalculateSafeExitPosition()
    {
        DebugLog("Calculating safe exit position");

        if (currentVehicle == null)
        {
            DebugLog("Cannot calculate exit position - current vehicle is null");
            return Vector3.zero;
        }


        Vector3 vehiclePosition = currentVehicle.Transform.position;
        Vector3 exitOffset = currentVehicle.ExitOffset;

        // Try multiple exit positions around the vehicle
        Vector3[] exitOffsets = new Vector3[]
        {
            exitOffset,                                    // Right side (default)
            new Vector3(-exitOffset.x, exitOffset.y, exitOffset.z), // Left side
            new Vector3(0, exitOffset.y, exitOffset.z),             // Behind
            new Vector3(0, exitOffset.y, -exitOffset.z)             // Front
        };

        foreach (var offset in exitOffsets)
        {
            Vector3 testPosition = vehiclePosition + currentVehicle.Transform.TransformDirection(offset);

            // Check if position is clear of obstacles
            if (IsPositionSafe(testPosition))
            {
                // Ground the position
                if (Physics.Raycast(testPosition + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
                {
                    testPosition.y = hit.point.y;
                }

                return testPosition;
            }
        }

        return Vector3.zero; // No safe position found
    }

    /// <summary>
    /// ADD TO VehicleMovementController - Check if position is safe for player
    /// </summary>
    private bool IsPositionSafe(Vector3 position)
    {
        // Check for obstacles using a capsule cast (simulating player collider)
        // float playerRadius = 0.5f;
        // float playerHeight = 2f;

        // Vector3 bottom = position + Vector3.up * playerRadius;
        // Vector3 top = position + Vector3.up * (playerHeight - playerRadius);

        // // Check if space is clear
        // bool isBlocked = Physics.CheckCapsule(bottom, top, playerRadius);

        // return !isBlocked;

        return true;
    }

    private void ResetState()
    {
        movementInput = Vector2.zero;
        isBraking = false;
        currentVehicle = null;
    }

    public string GetVehicleStateInfo()
    {
        if (currentVehicle == null)
        {
            return "No vehicle assigned";
        }

        return $"Vehicle: {currentVehicle.VehicleID} ({currentVehicle.VehicleType}), " +
               $"Speed: {currentVehicle.Velocity.magnitude:F1}m/s, " +
               $"Operational: {currentVehicle.IsOperational}, " +
               $"Input: T{movementInput.y:F1}/S{movementInput.x:F1}, Brake: {isBraking}";
    }

    /// <summary>
    /// Force clean state for transitions and save/load operations
    /// </summary>
    public void ForceCleanState()
    {
        ResetState();
        DebugLog("Vehicle movement state force cleaned");
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[VehicleMovementController] {message}");
        }
    }

    #endregion
}