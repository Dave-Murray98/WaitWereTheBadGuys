using UnityEngine;

/// <summary>
/// REFACTORED: Enum-based LocomotionAnimationDatabase for maximum performance.
/// Dedicated ScriptableObject for lower body locomotion animations.
/// Contains all movement animations that are independent of equipped items.
/// Used by LowerBodyAnimationController for consistent locomotion across all states.
/// </summary>
[CreateAssetMenu(fileName = "New Locomotion Animation Database", menuName = "Animation System/Locomotion Animation Database")]
public class LocomotionAnimationDatabase : ScriptableObject
{
    [Header("Database Information")]
    [Tooltip("Display name for this locomotion animation set")]
    public string displayName = "Default Locomotion Animations";

    [TextArea(2, 3)]
    [Tooltip("Description of this animation set")]
    public string description = "Contains all lower body locomotion animations independent of equipped items.";

    [Header("Ground State Locomotion")]
    public GroundLocomotionAnimations groundAnimations;

    [Header("Water State Locomotion")]
    public WaterLocomotionAnimations waterAnimations;

    [Header("Vehicle State Animations")]
    public VehicleLocomotionAnimations vehicleAnimations;

    [Header("Climbing State Locomotion")]
    public ClimbingLocomotionAnimations climbingAnimations;

    [Header("Debug Settings")]
    public bool enableDebugLogs = false;

    /// <summary>
    /// Get locomotion animations for a specific player state
    /// </summary>
    public LocomotionAnimationSet GetLocomotionAnimationsForState(PlayerStateType state)
    {
        return state switch
        {
            PlayerStateType.Ground => groundAnimations,
            PlayerStateType.Water => waterAnimations,
            PlayerStateType.Vehicle => vehicleAnimations,
            PlayerStateType.Climbing => climbingAnimations,
            _ => groundAnimations // Default fallback
        };
    }

    /// <summary>
    /// OPTIMIZED: Get a specific locomotion animation clip by enum
    /// </summary>
    public AnimationClip GetLocomotionAnimation(PlayerAnimationType animationType)
    {
        if (!animationType.IsLocomotion())
        {
            return null;
        }

        // Determine which state this animation belongs to and get it
        PlayerStateType targetState = GetStateForAnimation(animationType);
        var animationSet = GetLocomotionAnimationsForState(targetState);

        return animationSet?.GetAnimationByEnum(animationType);
    }

    /// <summary>
    /// Determine which player state an animation belongs to
    /// </summary>
    private PlayerStateType GetStateForAnimation(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            // Ground animations
            PlayerAnimationType.Idle or
            >= PlayerAnimationType.WalkForward and <= PlayerAnimationType.WalkBackwardRight or
            PlayerAnimationType.RunForward or
            >= PlayerAnimationType.CrouchIdle and <= PlayerAnimationType.CrouchWalkBackwardRight
                => PlayerStateType.Ground,

            // Water animations
            >= PlayerAnimationType.SwimIdle and <= PlayerAnimationType.SwimFastForward
                => PlayerStateType.Water,

            // Climbing animations
            >= PlayerAnimationType.Climb
                => PlayerStateType.Climbing,

            // Vehicle animations
            >= PlayerAnimationType.VehicleIdleStanding
                => PlayerStateType.Vehicle,

            _ => PlayerStateType.Ground // Default fallback
        };
    }

    /// <summary>
    /// Check if a locomotion animation exists by enum
    /// </summary>
    public bool HasLocomotionAnimation(PlayerAnimationType animationType)
    {
        return GetLocomotionAnimation(animationType) != null;
    }

    /// <summary>
    /// Get all available animation types for a specific state
    /// </summary>
    public PlayerAnimationType[] GetAvailableAnimationTypesForState(PlayerStateType state)
    {
        var animationSet = GetLocomotionAnimationsForState(state);
        return animationSet?.GetAvailableAnimationTypesAsEnums() ?? new PlayerAnimationType[0];
    }
}

/// <summary>
/// REFACTORED: Base class for locomotion animation sets with enum support
/// </summary>
[System.Serializable]
public abstract class LocomotionAnimationSet
{

    // NEW: Enum-based methods for performance
    public abstract AnimationClip GetAnimationByEnum(PlayerAnimationType animationType);
    public abstract PlayerAnimationType[] GetAvailableAnimationTypesAsEnums();
}

/// <summary>
/// REFACTORED: Ground-based locomotion animations with enum-based lookups
/// </summary>
[System.Serializable]
public class GroundLocomotionAnimations : LocomotionAnimationSet
{
    [Header("Basic Locomotion")]
    [Tooltip("Standing still")]
    public AnimationClip idle;

    [Header("Walking Animations (8-directional)")]
    public AnimationClip walkForward;
    public AnimationClip walkBackward;
    public AnimationClip walkLeft;
    public AnimationClip walkRight;
    public AnimationClip walkForwardLeft;
    public AnimationClip walkForwardRight;
    public AnimationClip walkBackwardLeft;
    public AnimationClip walkBackwardRight;

    [Header("Running Animation")]
    [Tooltip("Running forward only")]
    public AnimationClip runForward;

    [Header("Crouching Locomotion")]
    public AnimationClip crouchIdle;
    public AnimationClip crouchWalkForward;
    public AnimationClip crouchWalkBackward;
    public AnimationClip crouchWalkLeft;
    public AnimationClip crouchWalkRight;
    public AnimationClip crouchWalkForwardLeft;
    public AnimationClip crouchWalkForwardRight;
    public AnimationClip crouchWalkBackwardLeft;
    public AnimationClip crouchWalkBackwardRight;

    // OPTIMIZED: Enum-based lookup
    public override AnimationClip GetAnimationByEnum(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.Idle => idle,
            PlayerAnimationType.WalkForward => walkForward,
            PlayerAnimationType.WalkBackward => walkBackward,
            PlayerAnimationType.WalkLeft => walkLeft,
            PlayerAnimationType.WalkRight => walkRight,
            PlayerAnimationType.WalkForwardLeft => walkForwardLeft,
            PlayerAnimationType.WalkForwardRight => walkForwardRight,
            PlayerAnimationType.WalkBackwardLeft => walkBackwardLeft,
            PlayerAnimationType.WalkBackwardRight => walkBackwardRight,
            PlayerAnimationType.RunForward => runForward,
            PlayerAnimationType.CrouchIdle => crouchIdle,
            PlayerAnimationType.CrouchWalkForward => crouchWalkForward,
            PlayerAnimationType.CrouchWalkBackward => crouchWalkBackward,
            PlayerAnimationType.CrouchWalkLeft => crouchWalkLeft,
            PlayerAnimationType.CrouchWalkRight => crouchWalkRight,
            PlayerAnimationType.CrouchWalkForwardLeft => crouchWalkForwardLeft,
            PlayerAnimationType.CrouchWalkForwardRight => crouchWalkForwardRight,
            PlayerAnimationType.CrouchWalkBackwardLeft => crouchWalkBackwardLeft,
            PlayerAnimationType.CrouchWalkBackwardRight => crouchWalkBackwardRight,
            _ => null
        };
    }

    public override PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        return new PlayerAnimationType[]
        {
            PlayerAnimationType.Idle, PlayerAnimationType.WalkForward, PlayerAnimationType.WalkBackward,
            PlayerAnimationType.WalkLeft, PlayerAnimationType.WalkRight, PlayerAnimationType.WalkForwardLeft,
            PlayerAnimationType.WalkForwardRight, PlayerAnimationType.WalkBackwardLeft, PlayerAnimationType.WalkBackwardRight,
            PlayerAnimationType.RunForward, PlayerAnimationType.CrouchIdle, PlayerAnimationType.CrouchWalkForward,
            PlayerAnimationType.CrouchWalkBackward, PlayerAnimationType.CrouchWalkLeft, PlayerAnimationType.CrouchWalkRight,
            PlayerAnimationType.CrouchWalkForwardLeft, PlayerAnimationType.CrouchWalkForwardRight,
            PlayerAnimationType.CrouchWalkBackwardLeft, PlayerAnimationType.CrouchWalkBackwardRight
        };
    }
}

/// <summary>
/// REFACTORED: Water-based locomotion animations with enum-based lookups
/// </summary>
[System.Serializable]
public class WaterLocomotionAnimations : LocomotionAnimationSet
{
    [Header("Swimming Locomotion")]
    [Tooltip("Floating/treading water")]
    public AnimationClip swimIdle;

    [Header("Swimming Animations (4-directional)")]
    public AnimationClip swimForward;
    public AnimationClip swimBackward;
    public AnimationClip swimLeft;
    public AnimationClip swimRight;

    [Header("Fast Swimming")]
    [Tooltip("Fast swimming forward only")]
    public AnimationClip swimFastForward;

    // OPTIMIZED: Enum-based lookup
    public override AnimationClip GetAnimationByEnum(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.SwimIdle => swimIdle,
            PlayerAnimationType.SwimForward => swimForward,
            PlayerAnimationType.SwimBackward => swimBackward,
            PlayerAnimationType.SwimLeft => swimLeft,
            PlayerAnimationType.SwimRight => swimRight,
            PlayerAnimationType.SwimFastForward => swimFastForward,
            _ => null
        };
    }


    public override PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        return new PlayerAnimationType[]
        {
            PlayerAnimationType.SwimIdle, PlayerAnimationType.SwimForward, PlayerAnimationType.SwimBackward,
            PlayerAnimationType.SwimLeft, PlayerAnimationType.SwimRight, PlayerAnimationType.SwimFastForward
        };
    }
}

[System.Serializable]
public class VehicleLocomotionAnimations : LocomotionAnimationSet
{
    [Header("Vehicle Locomotion")]
    [Tooltip("Standing idle animation for vehicles without seats (boats, etc.)")]
    public AnimationClip vehicleIdleStanding;

    [Tooltip("Sitting idle animation for vehicles with seats (cars, etc.)")]
    public AnimationClip vehicleIdleSitting;

    // UPDATED: Enum-based lookup with support for both standing and sitting
    public override AnimationClip GetAnimationByEnum(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.VehicleIdleStanding => vehicleIdleStanding,
            PlayerAnimationType.VehicleIdleSitting => vehicleIdleSitting, // NEW: Support for sitting animation
            _ => vehicleIdleStanding // Default fallback to standing
        };
    }

    // UPDATED: Return both vehicle animation types
    public override PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        return new PlayerAnimationType[]
        {
            PlayerAnimationType.VehicleIdleStanding,
            PlayerAnimationType.VehicleIdleSitting // NEW: Include sitting animation
        };
    }
}

[System.Serializable]
public class ClimbingLocomotionAnimations : LocomotionAnimationSet
{
    [Header("Climbing Locomotion")]
    [Tooltip("Only one climbing for now")]
    public AnimationClip climb;

    public override AnimationClip GetAnimationByEnum(PlayerAnimationType animationType)
    {
        return climb;
    }

    public override PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        return new PlayerAnimationType[]
        {
            PlayerAnimationType.Climb
        };
    }

}

/// <summary>
/// REFACTORED: Helper methods for locomotion animation database with enum support
/// </summary>
public static class LocomotionAnimationHelper
{
    /// <summary>
    /// OPTIMIZED: Get the appropriate animation type for ground movement using enums
    /// </summary>
    public static PlayerAnimationType GetGroundAnimationType(Vector2 input, bool isCrouching, bool isRunning)
    {
        return MovementToAnimationConverter.GetGroundLocomotionAnimation(input, isCrouching, isRunning);
    }

    /// <summary>
    /// OPTIMIZED: Get the appropriate animation type for water movement using enums
    /// </summary>
    public static PlayerAnimationType GetWaterAnimationType(Vector2 input, bool isFastSwimming)
    {
        return MovementToAnimationConverter.GetWaterLocomotionAnimation(input, isFastSwimming);
    }

    /// <summary>
    /// UPDATED: Get the appropriate animation type for vehicle movement with seat support
    /// </summary>
    public static PlayerAnimationType GetVehicleAnimationType(bool isVehicleSeated)
    {
        return MovementToAnimationConverter.GetVehicleLocomotionAnimation(isVehicleSeated);
    }

    public static PlayerAnimationType GetClimbingAnimationType()
    {
        return MovementToAnimationConverter.GetClimbingLocomotionAnimation();
    }
}