using UnityEngine;

/// <summary>
/// REFACTORED: Enum-based animation database for equipped item animations (not player body).
/// Contains all animations that play on the equipped item's animator.
/// Each item type has its own set of required animations.
/// </summary>
[CreateAssetMenu(fileName = "New Player Item Animation Database", menuName = "Animation System/Player Item Animation Database")]
public class PlayerItemAnimationDatabase : ScriptableObject
{
    [Header("Item Information")]
    [Tooltip("The item type this equipped item animation set belongs to")]
    public ItemType itemType = ItemType.Unarmed;

    [Tooltip("Display name for this equipped item animation set")]
    public string displayName = "";

    [TextArea(2, 3)]
    [Tooltip("Description of what animations this database contains")]
    public string description = "";

    [Header("Universal Animations")]
    [Tooltip("Idle animation - played during all locomotion when no actions are being performed")]
    public AnimationClip idle;

    [Header("Ranged Weapon Animations")]
    [SerializeField] private RangedWeaponAnimations rangedWeaponAnimations;

    [Header("Tool Animations")]
    [SerializeField] private ToolAnimations toolAnimations;

    [Header("Melee Weapon Animations")]
    [SerializeField] private MeleeWeaponAnimations meleeWeaponAnimations;

    [Header("Throwable Animations")]
    [SerializeField] private ThrowableAnimations throwableAnimations;

    [Header("Key Item Animations")]
    [SerializeField] private KeyItemAnimations keyItemAnimations;

    [Header("Consumable Animations")]
    [SerializeField] private ConsumableAnimations consumableAnimations;

    [Header("Bow Animations")]
    [SerializeField] private BowAnimations bowAnimations;

    [Header("Debug Settings")]
    public bool enableDebugLogs = false;

    /// <summary>
    /// OPTIMIZED: Get animation clip by enum for a specific action based on item type
    /// </summary>
    public AnimationClip GetItemAnimation(PlayerAnimationType animationType)
    {
        // First check if it's the universal idle
        if (animationType == PlayerAnimationType.Idle)
        {
            return idle;
        }

        // Then check item-specific animations based on item type
        return itemType switch
        {
            ItemType.RangedWeapon => rangedWeaponAnimations?.GetAnimationByEnum(animationType),
            ItemType.Tool => toolAnimations?.GetAnimationByEnum(animationType),
            ItemType.MeleeWeapon => meleeWeaponAnimations?.GetAnimationByEnum(animationType),
            ItemType.Throwable => throwableAnimations?.GetAnimationByEnum(animationType),
            ItemType.KeyItem => keyItemAnimations?.GetAnimationByEnum(animationType),
            ItemType.Consumable => consumableAnimations?.GetAnimationByEnum(animationType),
            ItemType.Bow => bowAnimations?.GetAnimationByEnum(animationType),
            ItemType.Unarmed => null, // Unarmed has no item animations
            _ => null
        };
    }

    /// <summary>
    /// Check if item has a specific animation by enum
    /// </summary>
    public bool HasItemAnimation(PlayerAnimationType animationType)
    {
        return GetItemAnimation(animationType) != null;
    }

    /// <summary>
    /// Get all available animation types for this item type as enums
    /// </summary>
    public PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        var animations = new System.Collections.Generic.List<PlayerAnimationType> { PlayerAnimationType.Idle };

        var typeSpecific = itemType switch
        {
            ItemType.RangedWeapon => rangedWeaponAnimations?.GetAvailableAnimationTypesAsEnums(),
            ItemType.Tool => toolAnimations?.GetAvailableAnimationTypesAsEnums(),
            ItemType.MeleeWeapon => meleeWeaponAnimations?.GetAvailableAnimationTypesAsEnums(),
            ItemType.Throwable => throwableAnimations?.GetAvailableAnimationTypesAsEnums(),
            ItemType.KeyItem => keyItemAnimations?.GetAvailableAnimationTypesAsEnums(),
            ItemType.Consumable => consumableAnimations?.GetAvailableAnimationTypesAsEnums(),
            ItemType.Bow => bowAnimations?.GetAvailableAnimationTypesAsEnums(),
            _ => new PlayerAnimationType[0]
        };

        if (typeSpecific != null)
        {
            animations.AddRange(typeSpecific);
        }

        return animations.ToArray();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = $"{itemType} Item Animations";
        }

        // Validate that we have animations for the specified item type
        switch (itemType)
        {
            case ItemType.RangedWeapon:
                if (rangedWeaponAnimations == null)
                    Debug.LogWarning($"{displayName}: RangedWeapon item type but no ranged weapon animations assigned!");
                break;

            case ItemType.Tool:
                if (toolAnimations == null)
                    Debug.LogWarning($"{displayName}: Tool item type but no tool animations assigned!");
                break;

            case ItemType.MeleeWeapon:
                if (meleeWeaponAnimations == null)
                    Debug.LogWarning($"{displayName}: MeleeWeapon item type but no melee weapon animations assigned!");
                break;

            case ItemType.Throwable:
                if (throwableAnimations == null)
                    Debug.LogWarning($"{displayName}: Throwable item type but no throwable animations assigned!");
                break;

            case ItemType.KeyItem:
                if (keyItemAnimations == null)
                    Debug.LogWarning($"{displayName}: KeyItem item type but no key item animations assigned!");
                break;

            case ItemType.Consumable:
                if (consumableAnimations == null)
                    Debug.LogWarning($"{displayName}: Consumable item type but no consumable animations assigned!");
                break;

            case ItemType.Bow:
                if (bowAnimations == null)
                    Debug.LogWarning($"{displayName}: Bow item type but no bow animations assigned!");
                break;
        }

        if (idle == null)
        {
            Debug.LogWarning($"{displayName}: Missing universal idle animation!");
        }
    }
}

/// <summary>
/// REFACTORED: Base class for item-specific animation sets with enum support
/// </summary>
[System.Serializable]
public abstract class ItemAnimationSet
{
    // NEW: Enum-based methods for performance
    public abstract AnimationClip GetAnimationByEnum(PlayerAnimationType animationType);
    public abstract PlayerAnimationType[] GetAvailableAnimationTypesAsEnums();
}

/// <summary>
/// REFACTORED: Ranged weapon specific animations with enum support
/// </summary>
[System.Serializable]
public class RangedWeaponAnimations : ItemAnimationSet
{
    [Header("Ranged Weapon Actions")]
    [Tooltip("Shooting animation")]
    public AnimationClip shoot;

    [Tooltip("Reloading animation")]
    public AnimationClip reload;

    // OPTIMIZED: Enum-based lookup
    public override AnimationClip GetAnimationByEnum(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.PrimaryAction => shoot,
            PlayerAnimationType.ReloadAction => reload,
            _ => null
        };
    }

    public override PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        return new PlayerAnimationType[] { PlayerAnimationType.PrimaryAction, PlayerAnimationType.ReloadAction };
    }
}

/// <summary>
/// REFACTORED: Tool specific animations with enum support
/// </summary>
[System.Serializable]
public class ToolAnimations : ItemAnimationSet
{
    [Header("Tool Actions")]
    [Tooltip("Use tool animation (for instant tools like placing C4)")]
    public AnimationClip useTool;

    [Tooltip("Active/loop animation (for held tools like blowtorch)")]
    public AnimationClip activeLoop;

    // OPTIMIZED: Enum-based lookup
    public override AnimationClip GetAnimationByEnum(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.PrimaryAction or PlayerAnimationType.SecondaryAction => useTool,

            PlayerAnimationType.HeldPrimaryActionStart or PlayerAnimationType.HeldPrimaryActionLoop or
            PlayerAnimationType.HeldPrimaryActionEnd or PlayerAnimationType.HeldSecondaryActionStart or
            PlayerAnimationType.HeldSecondaryActionLoop or PlayerAnimationType.HeldSecondaryActionEnd => activeLoop,
            _ => null
        };
    }

    public override PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        return new PlayerAnimationType[] { PlayerAnimationType.SecondaryAction, PlayerAnimationType.HeldSecondaryActionLoop };
    }
}

/// <summary>
/// REFACTORED: Melee weapon specific animations with enum support
/// </summary>
[System.Serializable]
public class MeleeWeaponAnimations : ItemAnimationSet
{
    // Melee weapons only need idle (attacks are handled by player body animations)
    // But we can add weapon-specific animations here if needed

    // OPTIMIZED: Enum-based lookup
    public override AnimationClip GetAnimationByEnum(PlayerAnimationType animationType)
    {
        // Currently no specific animations for melee weapons beyond idle
        return null;
    }

    public override PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        return new PlayerAnimationType[0]; // Only idle for now
    }
}

/// <summary>
/// REFACTORED: Throwable specific animations with enum support
/// </summary>
[System.Serializable]
public class ThrowableAnimations : ItemAnimationSet
{
    // Throwables only need idle (throwing is handled by player body animations)
    // But we can add item-specific animations here if needed (like grenade pin pulling)

    // OPTIMIZED: Enum-based lookup
    public override AnimationClip GetAnimationByEnum(PlayerAnimationType animationType)
    {
        // Currently no specific animations for throwables beyond idle
        return null;
    }

    public override PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        return new PlayerAnimationType[0]; // Only idle for now
    }
}

/// <summary>
/// REFACTORED: Key item specific animations with enum support
/// </summary>
[System.Serializable]
public class KeyItemAnimations : ItemAnimationSet
{
    [Header("Key Item Actions")]
    [Tooltip("Use key item animation")]
    public AnimationClip useItem;

    // OPTIMIZED: Enum-based lookup
    public override AnimationClip GetAnimationByEnum(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.SecondaryAction => useItem,
            _ => null
        };
    }

    public override PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        return new PlayerAnimationType[] { PlayerAnimationType.SecondaryAction };
    }
}

/// <summary>
/// REFACTORED: Consumable specific animations with enum support
/// </summary>
[System.Serializable]
public class ConsumableAnimations : ItemAnimationSet
{
    [Header("Consumable Actions")]
    [Tooltip("Use/consume item animation")]
    public AnimationClip useItem;

    // OPTIMIZED: Enum-based lookup
    public override AnimationClip GetAnimationByEnum(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.SecondaryAction => useItem,
            _ => null
        };
    }

    public override PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        return new PlayerAnimationType[] { PlayerAnimationType.SecondaryAction };
    }
}

/// <summary>
/// REFACTORED: Bow specific animations with enum support
/// </summary>
[System.Serializable]
public class BowAnimations : ItemAnimationSet
{
    [Header("Bow Actions")]
    [Tooltip("Start drawing the bow")]
    public AnimationClip startDraw;

    [Tooltip("Holding the bow drawn (loop)")]
    public AnimationClip drawnLoop;

    [Tooltip("Shooting the bow")]
    public AnimationClip shoot;

    [Tooltip("Cancelling the draw")]
    public AnimationClip cancel;

    [Tooltip("Reloading/nocking an arrow")]
    public AnimationClip reload;

    // OPTIMIZED: Enum-based lookup
    public override AnimationClip GetAnimationByEnum(PlayerAnimationType animationType)
    {
        return animationType switch
        {
            PlayerAnimationType.HeldPrimaryActionStart => startDraw,
            PlayerAnimationType.HeldPrimaryActionLoop => drawnLoop,
            PlayerAnimationType.HeldPrimaryActionEnd => shoot,
            PlayerAnimationType.CancelHeldPrimaryAction => cancel,
            PlayerAnimationType.ReloadAction => reload,
            _ => null
        };
    }

    public override PlayerAnimationType[] GetAvailableAnimationTypesAsEnums()
    {
        return new PlayerAnimationType[]
        {
            PlayerAnimationType.HeldPrimaryActionStart, PlayerAnimationType.HeldPrimaryActionLoop,
            PlayerAnimationType.HeldPrimaryActionEnd, PlayerAnimationType.CancelHeldPrimaryAction,
            PlayerAnimationType.ReloadAction
        };
    }
}