using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// REFACTORED: PlayerBodyAnimationDatabase now uses enum-based lookups for maximum performance.
/// Centralized database storing all player body animation clips for a specific item type.
/// Organized by player state (Ground/Water) and animation category (Locomotion/Actions).
/// </summary>
[CreateAssetMenu(fileName = "New Player Body Animation Database", menuName = "Animation Player Body Animation Database")]
public class PlayerBodyAnimationDatabase : ScriptableObject
{
    [Header("Item Information")]
    [Tooltip("The item type this player body animation set belongs to")]
    public ItemType itemType = ItemType.Unarmed;

    [Tooltip("Display name for this player body animation set")]
    public string displayName = "";

    [Header("Ground State Animations")]
    public GroundAnimationSet groundAnimations;

    [Header("Water State Animations")]
    public WaterAnimationSet waterAnimations;

    [Header("Vehicle State Animations")]
    public VehicleAnimationSet vehicleAnimations;

    [Header("Climbing State Animations")]
    public ClimbingAnimationSet climbingAnimations;

    [Header("Action Animations (Universal)")]
    [Tooltip("Player body action animations that work in both ground and water states")]
    public ActionAnimations actionAnimations;

    [Header("Debug Settings")]
    public bool enableDebugLogs = false;

    /// <summary>
    /// Get animations for a specific player state
    /// </summary>
    public StateAnimationSet GetAnimationsForState(PlayerStateType state)
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
    /// OPTIMIZED: Get a specific animation clip by enum
    /// </summary>
    public AnimationClip GetAnimation(PlayerAnimationType animationType)
    {
        return animationType.GetCategory() switch
        {
            AnimationCategory.Locomotion => GetLocomotionAnimation(animationType),
            AnimationCategory.Action => GetActionAnimation(animationType),
            _ => null
        };
    }

    /// <summary>
    /// OPTIMIZED: Get locomotion animation by enum
    /// </summary>
    public AnimationClip GetLocomotionAnimation(PlayerAnimationType animationType)
    {
        if (!animationType.IsLocomotion())
        {
            return null;
        }

        // Determine which state this locomotion animation belongs to
        PlayerStateType targetState = GetStateForLocomotionAnimation(animationType);
        var stateSet = GetAnimationsForState(targetState);

        return stateSet?.GetLocomotionAnimationByEnum(animationType);
    }

    /// <summary>
    /// OPTIMIZED: Get action animation by enum
    /// </summary>
    public AnimationClip GetActionAnimation(PlayerAnimationType animationType)
    {
        if (!animationType.IsAction())
        {
            return null;
        }

        return actionAnimations?.GetActionAnimationByEnum(animationType);
    }

    /// <summary>
    /// Determine which player state a locomotion animation belongs to
    /// </summary>
    private PlayerStateType GetStateForLocomotionAnimation(PlayerAnimationType animationType)
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

            // Vehicle animations
            >= PlayerAnimationType.VehicleIdleStanding and <= PlayerAnimationType.VehicleIdleSitting
                => PlayerStateType.Vehicle,

            // Climbing animations
            >= PlayerAnimationType.Climb
                => PlayerStateType.Climbing,

            _ => PlayerStateType.Ground // Default fallback
        };
    }
    /// <summary>
    /// Check if animation exists by enum
    /// </summary>
    public bool HasAnimation(PlayerAnimationType animationType)
    {
        return GetAnimation(animationType) != null;
    }

    /// <summary>
    /// Check if animation exists with state validation
    /// </summary>
    public bool HasAnimation(PlayerStateType state, AnimationCategory category, PlayerAnimationType animationType)
    {
        return animationType.GetCategory() == category &&
               animationType.IsValidForState(state) &&
               HasAnimation(animationType);
    }

}

/// <summary>
/// REFACTORED: Base class for state-specific animation sets with enum support
/// </summary>
[Serializable]
public abstract class StateAnimationSet
{
    public abstract void ValidateAnimations(string contextName);

    // NEW: Enum-based methods for performance
    public abstract AnimationClip GetLocomotionAnimationByEnum(PlayerAnimationType animationType);
}

/// <summary>
/// REFACTORED: Animation set for Ground state with enum-based lookups
/// </summary>
[Serializable]
public class GroundAnimationSet : StateAnimationSet
{
    [Header("Locomotion Animations (Simplified for Upper Body)")]
    [Tooltip("Single idle animation (works for both standing and crouching)")]
    public AnimationClip idle;

    [Tooltip("Single walk animation (works for all directions and crouching)")]
    public AnimationClip walk;

    [Tooltip("Running forward animation")]
    public AnimationClip runForward;

    // OPTIMIZED: Enum-based lookup
    public override AnimationClip GetLocomotionAnimationByEnum(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.Idle or PlayerAnimationType.CrouchIdle => idle,

            // All walk variations use the same animation for upper body
            >= PlayerAnimationType.WalkForward and <= PlayerAnimationType.WalkBackwardRight or
            >= PlayerAnimationType.CrouchWalkForward and <= PlayerAnimationType.CrouchWalkBackwardRight => walk,

            PlayerAnimationType.RunForward => runForward,
            _ => null
        };
    }


    public override void ValidateAnimations(string contextName)
    {
        if (idle == null)
            Debug.LogWarning($"{contextName}: Missing idle animation");

        if (walk == null)
            Debug.LogWarning($"{contextName}: Missing walk animation");

        if (runForward == null)
            Debug.LogWarning($"{contextName}: Missing runForward animation");
    }
}

[Serializable]
public class VehicleAnimationSet : StateAnimationSet
{
    [Header("Vehicle Locomotion Animations")]
    [Tooltip("Standing idle animation for vehicles without seats (boats, etc.)")]
    public AnimationClip vehicleIdleStanding;

    [Tooltip("Sitting idle animation for vehicles with seats (cars, etc.)")]
    public AnimationClip vehicleIdleSitting;

    // UPDATED: Enum-based lookup with support for both standing and sitting
    public override AnimationClip GetLocomotionAnimationByEnum(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.VehicleIdleStanding => vehicleIdleStanding,
            PlayerAnimationType.VehicleIdleSitting => vehicleIdleSitting, // NEW: Support for sitting animation
            _ => vehicleIdleStanding // Default fallback to standing
        };
    }

    public override void ValidateAnimations(string contextName)
    {
        if (vehicleIdleStanding == null)
            Debug.LogWarning($"{contextName}: Missing vehicle standing idle animation");

        if (vehicleIdleSitting == null)
            Debug.LogWarning($"{contextName}: Missing vehicle sitting idle animation");
    }
}

[Serializable]
public class ClimbingAnimationSet : StateAnimationSet
{
    [Header("Climbing Animations")]
    [Tooltip("Ledge Climbing animation")]
    public AnimationClip ledgeClimb;

    // Only one climbing animation to return
    public override AnimationClip GetLocomotionAnimationByEnum(PlayerAnimationType animationType)
    {
        return ledgeClimb;
    }

    public override void ValidateAnimations(string contextName)
    {
        if (ledgeClimb == null)
            Debug.LogWarning($"{contextName}: Missing ledgeClimb animation");
    }
}

/// <summary>
/// REFACTORED: Animation set for Water state with enum-based lookups
/// </summary>
[Serializable]
public class WaterAnimationSet : StateAnimationSet
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
    public override AnimationClip GetLocomotionAnimationByEnum(PlayerAnimationType animationType)
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

    public override void ValidateAnimations(string contextName)
    {
        if (swimIdle == null)
            Debug.LogWarning($"{contextName}: Missing swim idle animation");

        if (swimForward == null)
            Debug.LogWarning($"{contextName}: Missing swim forward animation");
    }
}

/// <summary>
/// REFACTORED: Universal action animations with enum-based lookups
/// </summary>
[Serializable]
public class ActionAnimations
{
    [Header("Primary Actions")]
    [Tooltip("Primary action (shoot for weapons, consume for consumables, use for non-held input tools)")]
    public AnimationClip primaryAction;

    [Tooltip("Held Primary action start (for bows: drawing the bow, etc.)")]
    public AnimationClip heldPrimaryActionStart;

    [Tooltip("Held Primary action loop (for bows: holding the bow drawn, etc.)")]
    public AnimationClip heldPrimaryActionLoop;

    [Tooltip("Held Primary action end (for bows: releasing the bow, etc.)")]
    public AnimationClip heldPrimaryActionEnd;

    [Tooltip("Held Primary action cancel (for bows: cancelling the draw, etc.)")]
    public AnimationClip cancelHeldPrimaryAction;

    [Header("Secondary Actions")]
    [Tooltip("Secondary action (reload for weapons, secondary use for tools)")]
    public AnimationClip secondaryAction;

    [Tooltip("Held Secondary action start")]
    public AnimationClip heldSecondaryActionStart;

    [Tooltip("Held Secondary action loop")]
    public AnimationClip heldSecondaryActionLoop;

    [Tooltip("Held Secondary action end")]
    public AnimationClip heldSecondaryActionEnd;

    [Tooltip("Held Secondary action cancel")]
    public AnimationClip cancelHeldSecondaryAction;

    [Tooltip("Reload action (for weapons that need separate reload animation)")]
    public AnimationClip reloadAction;

    [Header("Melee Actions")]
    [Tooltip("Melee action (punch for unarmed, melee attack for tools)")]
    public AnimationClip meleeAction;

    [Tooltip("Melee action start")]
    public AnimationClip meleeActionStart;

    [Tooltip("Melee action light end")]
    public AnimationClip meleeActionEndLight;

    [Tooltip("Melee action heavy end")]
    public AnimationClip meleeActionEndHeavy;

    [Tooltip("Melee action cancel")]
    public AnimationClip meleeActionCancel;

    [Tooltip("Melee action loop")]
    public AnimationClip meleeActionLoop;

    /// <summary>
    /// OPTIMIZED: Get action animation by enum
    /// </summary>
    public AnimationClip GetActionAnimationByEnum(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.PrimaryAction => primaryAction,
            PlayerAnimationType.SecondaryAction => secondaryAction,
            PlayerAnimationType.ReloadAction => reloadAction,
            PlayerAnimationType.MeleeAction => meleeAction,
            PlayerAnimationType.HeldMeleeActionStart => meleeActionStart,
            PlayerAnimationType.HeldMeleeActionEndLight => meleeActionEndLight,
            PlayerAnimationType.HeldMeleeActionEndHeavy => meleeActionEndHeavy,
            PlayerAnimationType.HeldMeleeActionCancel => meleeActionCancel,
            PlayerAnimationType.HeldMeleeActionLoop => meleeActionLoop,
            PlayerAnimationType.HeldPrimaryActionStart => heldPrimaryActionStart,
            PlayerAnimationType.HeldPrimaryActionLoop => heldPrimaryActionLoop,
            PlayerAnimationType.HeldPrimaryActionEnd => heldPrimaryActionEnd,
            PlayerAnimationType.CancelHeldPrimaryAction => cancelHeldPrimaryAction,
            PlayerAnimationType.HeldSecondaryActionStart => heldSecondaryActionStart,
            PlayerAnimationType.HeldSecondaryActionLoop => heldSecondaryActionLoop,
            PlayerAnimationType.HeldSecondaryActionEnd => heldSecondaryActionEnd,
            PlayerAnimationType.CancelHeldSecondaryAction => cancelHeldSecondaryAction,
            _ => null
        };
    }

    /// <summary>
    /// Validate action animations
    /// </summary>
    public void ValidateAnimations(string contextName)
    {
        if (primaryAction == null)
            Debug.LogWarning($"{contextName}: Missing primaryAction animation");

        if (meleeAction == null)
            Debug.LogWarning($"{contextName}: Missing meleeAction animation");

        // Secondary and reload are optional depending on item type
    }

    /// <summary>
    /// Get all available action types as enums
    /// </summary>
    public PlayerAnimationType[] GetAvailableActionTypes()
    {
        var actionTypes = new List<PlayerAnimationType>
        {
            PlayerAnimationType.PrimaryAction,
            PlayerAnimationType.MeleeAction
        };

        if (secondaryAction != null) actionTypes.Add(PlayerAnimationType.SecondaryAction);
        if (reloadAction != null) actionTypes.Add(PlayerAnimationType.ReloadAction);

        // Add held actions if they exist
        if (heldPrimaryActionStart != null) actionTypes.Add(PlayerAnimationType.HeldPrimaryActionStart);
        if (heldPrimaryActionLoop != null) actionTypes.Add(PlayerAnimationType.HeldPrimaryActionLoop);
        if (heldPrimaryActionEnd != null) actionTypes.Add(PlayerAnimationType.HeldPrimaryActionEnd);
        if (cancelHeldPrimaryAction != null) actionTypes.Add(PlayerAnimationType.CancelHeldPrimaryAction);

        if (heldSecondaryActionStart != null) actionTypes.Add(PlayerAnimationType.HeldSecondaryActionStart);
        if (heldSecondaryActionLoop != null) actionTypes.Add(PlayerAnimationType.HeldSecondaryActionLoop);
        if (heldSecondaryActionEnd != null) actionTypes.Add(PlayerAnimationType.HeldSecondaryActionEnd);
        if (cancelHeldSecondaryAction != null) actionTypes.Add(PlayerAnimationType.CancelHeldSecondaryAction);

        // Add melee held actions
        if (meleeActionStart != null) actionTypes.Add(PlayerAnimationType.HeldMeleeActionStart);
        if (meleeActionLoop != null) actionTypes.Add(PlayerAnimationType.HeldMeleeActionLoop);
        if (meleeActionEndLight != null) actionTypes.Add(PlayerAnimationType.HeldMeleeActionEndLight);
        if (meleeActionEndHeavy != null) actionTypes.Add(PlayerAnimationType.HeldMeleeActionEndHeavy);
        if (meleeActionCancel != null) actionTypes.Add(PlayerAnimationType.HeldMeleeActionCancel);

        return actionTypes.ToArray();
    }
}