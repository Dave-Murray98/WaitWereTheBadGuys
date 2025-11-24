using UnityEngine;

/// <summary>
/// Interface for all movement controllers (Ground, Swimming, Vehicle).
/// Provides a unified API for PlayerController to manage different movement modes.
/// </summary>
public interface IMovementController
{
    /// <summary>
    /// Initialize the movement controller with player controller reference
    /// </summary>
    void Initialize(PlayerController playerController);

    /// <summary>
    /// Handle movement input from CoreMovement action map
    /// </summary>
    /// <param name="moveInput">2D movement input from Move action</param>
    /// <param name="isSpeedModified">Whether speed modifier (Sprint/SwimSpeed) is active</param>
    void HandleMovement(Vector2 moveInput, bool isSpeedModified);

    void HandleHorizontalRotation(float targetRotationY);

    /// <summary>
    /// Handle primary action (Jump/Surface/Accelerate depending on context)
    /// </summary>
    void HandlePrimaryAction();

    /// <summary>
    /// Handle primary action release (Stop Jump/Stop Surfacing/Release Accelerate depending on context)
    /// </summary>
    public void HandlePrimaryActionReleased();

    /// <summary>
    /// Handle secondary action (Crouch/Dive/Brake depending on context)
    /// </summary>
    void HandleSecondaryAction();

    /// <summary>
    /// Handle secondary action release (Stop Crouch/Stop Dive/Release Brake depending on context)
    /// </summary>
    void HandleSecondaryActionReleased();

    /// <summary>
    /// Get current velocity for state determination and other systems
    /// </summary>
    Vector3 GetVelocity();

    /// <summary>
    /// Whether this controller considers the player "grounded" (or equivalent stable state)
    /// </summary>
    bool IsGrounded { get; }

    /// <summary>
    /// Whether the player is currently moving with this controller
    /// </summary>
    bool IsMoving { get; }

    /// <summary>
    /// Whether speed modification is currently active (sprinting/fast swimming)
    /// </summary>
    bool IsSpeedModified { get; }

    /// <summary>
    /// Whether secondary action is currently active (crouching/diving)
    /// </summary>
    bool IsSecondaryActive { get; }

    /// <summary>
    /// The movement mode this controller handles
    /// </summary>
    MovementMode MovementMode { get; }

    /// <summary>
    /// Called when movement state changes - allows controller to react to transitions
    /// </summary>
    void OnMovementStateChanged(MovementState previousState, MovementState newState);

    /// <summary>
    /// Called when this controller becomes active
    /// </summary>
    void OnControllerActivated();

    /// <summary>
    /// Called when this controller becomes inactive
    /// </summary>
    void OnControllerDeactivated();

    /// <summary>
    /// Clean up resources and references
    /// </summary>
    void Cleanup();
}

/// <summary>
/// Movement states for the player - renamed from GroundMovementState to support all movement types.
/// Includes both ground-based and swimming states for comprehensive player movement tracking.
/// </summary>
public enum MovementState
{
    // Ground-based movement states
    Idle,
    Walking,
    Running,
    Crouching,
    Jumping,
    Falling,
    Landing,

    // Water-based movement states
    WaterEntry,      // Transitioning from ground to swimming
    SurfaceSwimming, // Swimming at or near water surface
    Swimming,        // Normal underwater swimming
    Diving,          // Actively diving deeper
    WaterExit        // Transitioning from swimming to ground
}


/// <summary>
/// Movement modes for different types of player control systems
/// </summary>
public enum MovementMode
{
    Ground,    // Ground-based movement (walking, running, jumping, crouching)
    Swimming,  // Water-based movement (swimming, diving, surfacing)
    Vehicle,    // Vehicle-based movement
    Climbing    // Climbing onto a ledge
}

/// <summary>
/// Helper class for movement state utilities and transitions
/// </summary>
public static class MovementStateUtilities
{
    /// <summary>
    /// Checks if a movement state is ground-based
    /// </summary>
    public static bool IsGroundState(MovementState state)
    {
        return state == MovementState.Idle ||
               state == MovementState.Walking ||
               state == MovementState.Running ||
               state == MovementState.Crouching ||
               state == MovementState.Jumping ||
               state == MovementState.Falling ||
               state == MovementState.Landing;
    }

    /// <summary>
    /// Checks if a movement state is water-based
    /// </summary>
    public static bool IsWaterState(MovementState state)
    {
        return state == MovementState.WaterEntry ||
               state == MovementState.SurfaceSwimming ||
               state == MovementState.Swimming ||
               state == MovementState.Diving ||
               state == MovementState.WaterExit;
    }

    /// <summary>
    /// Checks if a movement state is a transition state
    /// </summary>
    public static bool IsTransitionState(MovementState state)
    {
        return state == MovementState.WaterEntry ||
               state == MovementState.WaterExit ||
               state == MovementState.Landing;
    }

    /// <summary>
    /// Gets the appropriate movement mode for a given movement state
    /// </summary>
    public static MovementMode GetMovementModeForState(MovementState state)
    {
        if (IsGroundState(state))
            return MovementMode.Ground;
        else if (IsWaterState(state))
            return MovementMode.Swimming;
        else
            return MovementMode.Ground; // Default fallback
    }
}