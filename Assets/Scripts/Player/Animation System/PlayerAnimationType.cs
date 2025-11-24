using UnityEngine;
using System;

/// <summary>
/// Main enum covering all possible player animations.
/// Replaces the string-based animation system for better performance and type safety.
/// </summary>
public enum PlayerAnimationType
{
    // === LOCOMOTION ANIMATIONS ===

    // Basic Locomotion
    Idle = 0,

    // Ground Walking (8-directional)
    WalkForward = 100,
    WalkBackward = 101,
    WalkLeft = 102,
    WalkRight = 103,
    WalkForwardLeft = 104,
    WalkForwardRight = 105,
    WalkBackwardLeft = 106,
    WalkBackwardRight = 107,

    // Ground Running
    RunForward = 120,

    // Crouching Locomotion
    CrouchIdle = 200,
    CrouchWalkForward = 201,
    CrouchWalkBackward = 202,
    CrouchWalkLeft = 203,
    CrouchWalkRight = 204,
    CrouchWalkForwardLeft = 205,
    CrouchWalkForwardRight = 206,
    CrouchWalkBackwardLeft = 207,
    CrouchWalkBackwardRight = 208,

    // Swimming Locomotion
    SwimIdle = 300,
    SwimForward = 301,
    SwimBackward = 302,
    SwimLeft = 303,
    SwimRight = 304,
    SwimFastForward = 305,

    // Vehicle Locomotion
    VehicleIdleStanding = 400,
    VehicleIdleSitting = 401,

    //Climbing Locomotion
    Climb = 500,

    // === ACTION ANIMATIONS ===

    // Basic Actions
    PrimaryAction = 1000,
    SecondaryAction = 1001,
    ReloadAction = 1002,
    MeleeAction = 1003,

    // Held Primary Actions (for Bows, etc.)
    HeldPrimaryActionStart = 1100,
    HeldPrimaryActionLoop = 1101,
    HeldPrimaryActionEnd = 1102,
    CancelHeldPrimaryAction = 1103,

    // Held Secondary Actions (for Throwables, Tools, etc.)
    HeldSecondaryActionStart = 1200,
    HeldSecondaryActionLoop = 1201,
    HeldSecondaryActionEnd = 1202,
    CancelHeldSecondaryAction = 1203,

    // Held Melee Actions
    HeldMeleeActionStart = 1300,
    HeldMeleeActionLoop = 1301,
    HeldMeleeActionEndLight = 1302,
    HeldMeleeActionEndHeavy = 1303,
    HeldMeleeActionCancel = 1304
}

/// <summary>
/// Category classification for animations - used for organization and filtering
/// </summary>
public enum AnimationCategory
{
    Locomotion = 0,
    Action = 1
}

/// <summary>
/// Extension methods and utilities for working with animation enums
/// </summary>
public static class PlayerAnimationTypeExtensions
{
    /// <summary>
    /// Get the animation category for a given animation type
    /// </summary>
    public static AnimationCategory GetCategory(this PlayerAnimationType animationType)
    {
        return (int)animationType switch
        {
            < 1000 => AnimationCategory.Locomotion,
            >= 1000 => AnimationCategory.Action
        };
    }

    /// <summary>
    /// Check if animation is a locomotion type
    /// </summary>
    public static bool IsLocomotion(this PlayerAnimationType animationType)
    {
        return animationType.GetCategory() == AnimationCategory.Locomotion;
    }

    /// <summary>
    /// Check if animation is an action type
    /// </summary>
    public static bool IsAction(this PlayerAnimationType animationType)
    {
        return animationType.GetCategory() == AnimationCategory.Action;
    }

    /// <summary>
    /// Check if animation is a held action type
    /// </summary>
    public static bool IsHeldAction(this PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.HeldPrimaryActionStart or
            PlayerAnimationType.HeldPrimaryActionLoop or
            PlayerAnimationType.HeldPrimaryActionEnd or
            PlayerAnimationType.CancelHeldPrimaryAction or
            PlayerAnimationType.HeldSecondaryActionStart or
            PlayerAnimationType.HeldSecondaryActionLoop or
            PlayerAnimationType.HeldSecondaryActionEnd or
            PlayerAnimationType.CancelHeldSecondaryAction or
            PlayerAnimationType.HeldMeleeActionStart or
            PlayerAnimationType.HeldMeleeActionLoop or
            PlayerAnimationType.HeldMeleeActionEndLight or
            PlayerAnimationType.HeldMeleeActionEndHeavy or
            PlayerAnimationType.HeldMeleeActionCancel => true,
            _ => false
        };
    }

    /// <summary>
    /// Check if animation is valid for a specific player state
    /// </summary>
    public static bool IsValidForState(this PlayerAnimationType animationType, PlayerStateType playerState)
    {
        return playerState switch
        {
            PlayerStateType.Ground => animationType switch
            {
                // Ground locomotion
                PlayerAnimationType.Idle or
                >= PlayerAnimationType.WalkForward and <= PlayerAnimationType.WalkBackwardRight or
                PlayerAnimationType.RunForward or
                >= PlayerAnimationType.CrouchIdle and <= PlayerAnimationType.CrouchWalkBackwardRight or
                // All actions are valid on ground
                >= PlayerAnimationType.PrimaryAction => true,
                _ => false
            },

            PlayerStateType.Water => animationType switch
            {
                // Water locomotion
                >= PlayerAnimationType.SwimIdle and <= PlayerAnimationType.SwimFastForward or
                // All actions are valid in water
                >= PlayerAnimationType.PrimaryAction => true,
                _ => false
            },

            PlayerStateType.Vehicle => animationType switch
            {
                // Vehicle locomotion - UPDATED: Both standing and sitting are valid
                PlayerAnimationType.VehicleIdleStanding or
                PlayerAnimationType.VehicleIdleSitting or
                // All actions are valid in vehicle
                >= PlayerAnimationType.PrimaryAction => true,
                _ => false
            },

            PlayerStateType.Climbing => animationType switch
            {
                // Climbing locomotion
                PlayerAnimationType.Climb => true,
                _ => false
            },

            _ => false
        };
    }

    /// <summary>
    /// Get the appropriate idle animation for a player state
    /// </summary>
    public static PlayerAnimationType GetIdleForState(PlayerStateType playerState, bool isCrouching = false, bool isVehicleSeated = false)
    {
        return playerState switch
        {
            PlayerStateType.Ground => isCrouching ? PlayerAnimationType.CrouchIdle : PlayerAnimationType.Idle,
            PlayerStateType.Water => PlayerAnimationType.SwimIdle,
            PlayerStateType.Vehicle => isVehicleSeated ? PlayerAnimationType.VehicleIdleSitting : PlayerAnimationType.VehicleIdleStanding, // NEW: Choose based on vehicle seat type
            PlayerStateType.Climbing => PlayerAnimationType.Climb,
            _ => PlayerAnimationType.Idle
        };
    }

    /// <summary>
    /// Convert enum to string for debugging purposes only
    /// </summary>
    public static string ToDebugString(this PlayerAnimationType animationType)
    {
        return animationType switch
        {
            // Locomotion
            PlayerAnimationType.Idle => "idle",
            PlayerAnimationType.WalkForward => "walkforward",
            PlayerAnimationType.WalkBackward => "walkbackward",
            PlayerAnimationType.WalkLeft => "walkleft",
            PlayerAnimationType.WalkRight => "walkright",
            PlayerAnimationType.WalkForwardLeft => "walkforwardleft",
            PlayerAnimationType.WalkForwardRight => "walkforwardright",
            PlayerAnimationType.WalkBackwardLeft => "walkbackwardleft",
            PlayerAnimationType.WalkBackwardRight => "walkbackwardright",
            PlayerAnimationType.RunForward => "runforward",
            PlayerAnimationType.CrouchIdle => "crouchidle",
            PlayerAnimationType.CrouchWalkForward => "crouchwalkforward",
            PlayerAnimationType.CrouchWalkBackward => "crouchwalkbackward",
            PlayerAnimationType.CrouchWalkLeft => "crouchwalkleft",
            PlayerAnimationType.CrouchWalkRight => "crouchwalkright",
            PlayerAnimationType.CrouchWalkForwardLeft => "crouchwalkforwardleft",
            PlayerAnimationType.CrouchWalkForwardRight => "crouchwalkforwardright",
            PlayerAnimationType.CrouchWalkBackwardLeft => "crouchwalkbackwardleft",
            PlayerAnimationType.CrouchWalkBackwardRight => "crouchwalkbackwardright",
            PlayerAnimationType.SwimIdle => "swimidle",
            PlayerAnimationType.SwimForward => "swimforward",
            PlayerAnimationType.SwimBackward => "swimbackward",
            PlayerAnimationType.SwimLeft => "swimleft",
            PlayerAnimationType.SwimRight => "swimright",
            PlayerAnimationType.SwimFastForward => "swimfastforward",
            PlayerAnimationType.VehicleIdleStanding => "vehicleidlestanding",
            PlayerAnimationType.VehicleIdleSitting => "vehicleidlesitting",
            PlayerAnimationType.Climb => "climb",

            // Actions
            PlayerAnimationType.PrimaryAction => "primaryaction",
            PlayerAnimationType.SecondaryAction => "secondaryaction",
            PlayerAnimationType.ReloadAction => "reloadaction",
            PlayerAnimationType.MeleeAction => "meleeaction",
            PlayerAnimationType.HeldPrimaryActionStart => "heldprimaryactionstart",
            PlayerAnimationType.HeldPrimaryActionLoop => "heldprimaryactionloop",
            PlayerAnimationType.HeldPrimaryActionEnd => "heldprimaryactionend",
            PlayerAnimationType.CancelHeldPrimaryAction => "cancelheldprimaryaction",
            PlayerAnimationType.HeldSecondaryActionStart => "heldsecondaryactionstart",
            PlayerAnimationType.HeldSecondaryActionLoop => "heldsecondaryactionloop",
            PlayerAnimationType.HeldSecondaryActionEnd => "heldsecondaryactionend",
            PlayerAnimationType.CancelHeldSecondaryAction => "cancelheldsecondaryaction",
            PlayerAnimationType.HeldMeleeActionStart => "heldmeleeactionstart",
            PlayerAnimationType.HeldMeleeActionLoop => "heldmeleeactionloop",
            PlayerAnimationType.HeldMeleeActionEndLight => "heldmeleeactionendlight",
            PlayerAnimationType.HeldMeleeActionEndHeavy => "heldmeleeactionendheavy",
            PlayerAnimationType.HeldMeleeActionCancel => "heldmeleeactioncancel",

            _ => animationType.ToString().ToLower()
        };
    }
}

/// <summary>
/// Helper class for converting movement input to animation types
/// </summary>
public static class MovementToAnimationConverter
{
    /// <summary>
    /// Convert movement input to appropriate ground locomotion animation
    /// </summary>
    public static PlayerAnimationType GetGroundLocomotionAnimation(Vector2 input, bool isCrouching, bool isRunning)
    {
        // No movement - idle
        if (input.magnitude < 0.1f)
        {
            return isCrouching ? PlayerAnimationType.CrouchIdle : PlayerAnimationType.Idle;
        }

        // Running (only forward, not while crouching)
        if (isRunning && !isCrouching && input.y > 0.5f && Mathf.Abs(input.x) < 0.3f)
        {
            return PlayerAnimationType.RunForward;
        }

        // Walking - get 8-directional movement
        var direction = GetMovementDirection(input);

        return isCrouching ? GetCrouchWalkAnimation(direction) : GetWalkAnimation(direction);
    }

    /// <summary>
    /// Convert movement input to appropriate water locomotion animation
    /// </summary>
    public static PlayerAnimationType GetWaterLocomotionAnimation(Vector2 input, bool isFastSwimming)
    {
        // No movement - floating
        if (input.magnitude < 0.1f)
        {
            return PlayerAnimationType.SwimIdle;
        }

        // Fast swimming (only forward)
        if (isFastSwimming && input.y > 0.5f && Mathf.Abs(input.x) < 0.3f)
        {
            return PlayerAnimationType.SwimFastForward;
        }

        // Normal swimming - 4-directional (simplified from 8-directional)
        var direction = GetSimplifiedSwimmingDirection(input);
        return GetSwimAnimation(direction);
    }

    /// <summary>
    /// Get vehicle locomotion animation based on vehicle seat type
    /// Since vehicles don't have movement input, we just need to determine standing vs sitting
    /// </summary>
    public static PlayerAnimationType GetVehicleLocomotionAnimation(bool isVehicleSeated)
    {
        return isVehicleSeated ? PlayerAnimationType.VehicleIdleSitting : PlayerAnimationType.VehicleIdleStanding;
    }

    /// <summary>
    /// Return the climbing animation (only a single climb animation for now)
    /// </summary>
    public static PlayerAnimationType GetClimbingLocomotionAnimation()
    {
        //for now there is only a single climb animation type
        return PlayerAnimationType.Climb;
    }

    /// <summary>
    /// Get 8-directional movement direction
    /// </summary>
    private static MovementDirection GetMovementDirection(Vector2 input)
    {
        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        return angle switch
        {
            >= 337.5f or < 22.5f => MovementDirection.Right,
            >= 22.5f and < 67.5f => MovementDirection.ForwardRight,
            >= 67.5f and < 112.5f => MovementDirection.Forward,
            >= 112.5f and < 157.5f => MovementDirection.ForwardLeft,
            >= 157.5f and < 202.5f => MovementDirection.Left,
            >= 202.5f and < 247.5f => MovementDirection.BackwardLeft,
            >= 247.5f and < 292.5f => MovementDirection.Backward,
            >= 292.5f and < 337.5f => MovementDirection.BackwardRight,
            _ => MovementDirection.Forward
        };
    }

    /// <summary>
    /// Get 4-directional swimming direction
    /// </summary>
    private static MovementDirection GetSimplifiedSwimmingDirection(Vector2 input)
    {
        // Determine primary axis for 4-directional movement
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            return input.x > 0 ? MovementDirection.Right : MovementDirection.Left;
        }
        else
        {
            return input.y > 0 ? MovementDirection.Forward : MovementDirection.Backward;
        }
    }

    /// <summary>
    /// Get walk animation for direction
    /// </summary>
    private static PlayerAnimationType GetWalkAnimation(MovementDirection direction)
    {
        return direction switch
        {
            MovementDirection.Forward => PlayerAnimationType.WalkForward,
            MovementDirection.Backward => PlayerAnimationType.WalkBackward,
            MovementDirection.Left => PlayerAnimationType.WalkLeft,
            MovementDirection.Right => PlayerAnimationType.WalkRight,
            MovementDirection.ForwardLeft => PlayerAnimationType.WalkForwardLeft,
            MovementDirection.ForwardRight => PlayerAnimationType.WalkForwardRight,
            MovementDirection.BackwardLeft => PlayerAnimationType.WalkBackwardLeft,
            MovementDirection.BackwardRight => PlayerAnimationType.WalkBackwardRight,
            _ => PlayerAnimationType.WalkForward
        };
    }

    /// <summary>
    /// Get crouch walk animation for direction
    /// </summary>
    private static PlayerAnimationType GetCrouchWalkAnimation(MovementDirection direction)
    {
        return direction switch
        {
            MovementDirection.Forward => PlayerAnimationType.CrouchWalkForward,
            MovementDirection.Backward => PlayerAnimationType.CrouchWalkBackward,
            MovementDirection.Left => PlayerAnimationType.CrouchWalkLeft,
            MovementDirection.Right => PlayerAnimationType.CrouchWalkRight,
            MovementDirection.ForwardLeft => PlayerAnimationType.CrouchWalkForwardLeft,
            MovementDirection.ForwardRight => PlayerAnimationType.CrouchWalkForwardRight,
            MovementDirection.BackwardLeft => PlayerAnimationType.CrouchWalkBackwardLeft,
            MovementDirection.BackwardRight => PlayerAnimationType.CrouchWalkBackwardRight,
            _ => PlayerAnimationType.CrouchWalkForward
        };
    }

    /// <summary>
    /// Get swim animation for direction
    /// </summary>
    private static PlayerAnimationType GetSwimAnimation(MovementDirection direction)
    {
        return direction switch
        {
            MovementDirection.Forward => PlayerAnimationType.SwimForward,
            MovementDirection.Backward => PlayerAnimationType.SwimBackward,
            MovementDirection.Left => PlayerAnimationType.SwimLeft,
            MovementDirection.Right => PlayerAnimationType.SwimRight,
            _ => PlayerAnimationType.SwimForward
        };
    }

    /// <summary>
    /// Internal enum for movement directions
    /// </summary>
    private enum MovementDirection
    {
        Forward,
        Backward,
        Left,
        Right,
        ForwardLeft,
        ForwardRight,
        BackwardLeft,
        BackwardRight
    }
}