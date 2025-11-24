using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: High-performance enum-based animation cache for instant O(1) lookup.
/// Eliminates string comparisons and provides type-safe animation access.
/// </summary>
public class PlayerBodyAnimationSetCache
{
    // Enum-based storage for maximum performance
    [ShowInInspector, ReadOnly]
    private Dictionary<PlayerAnimationType, AnimationClip> animationCache = new Dictionary<PlayerAnimationType, AnimationClip>();

    private Dictionary<PlayerAnimationType, bool> animationExistence = new Dictionary<PlayerAnimationType, bool>();

    // Cache metadata
    private ItemType cachedItemType;
    private PlayerBodyAnimationDatabase cachedDatabase;
    private bool isCacheValid = false;

    // Debug settings
    private bool enableDebugLogs = false;

    public PlayerBodyAnimationSetCache(bool enableDebug = false)
    {
        enableDebugLogs = enableDebug;
    }

    /// <summary>
    /// Load and cache animations for a specific item from its PlayerBodyAnimationDatabase
    /// </summary>
    public void CacheAnimationsForItem(ItemData itemData)
    {
        if (itemData == null)
        {
            Debug.LogError("[AnimationSetCache] Cannot cache animations - ItemData is null");
            ClearCache();
            return;
        }

        // Get the PLAYER BODY animation database from the item
        PlayerBodyAnimationDatabase database = GetPlayerBodyAnimationDatabaseFromItem(itemData);
        if (database == null)
        {
            DebugLog($"No PlayerBodyAnimationDatabase found for item: {itemData.itemName}");
            ClearCache();
            return;
        }

        // Check if we need to refresh the cache
        if (isCacheValid && cachedDatabase == database && cachedItemType == itemData.itemType)
        {
            DebugLog($"Animation cache already valid for {itemData.itemName}");
            return;
        }

        DebugLog($"Caching PLAYER BODY animations for item: {itemData.itemName} ({itemData.itemType})");

        // Clear existing cache
        ClearCache();

        // Cache new animations
        cachedItemType = itemData.itemType;
        cachedDatabase = database;

        CacheAnimationsFromDatabase(database);

        isCacheValid = true;
        DebugLog($"Player body animation cache loaded: {animationCache.Count} animations cached");
    }

    /// <summary>
    /// Get player body animation database from ItemData
    /// </summary>
    private PlayerBodyAnimationDatabase GetPlayerBodyAnimationDatabaseFromItem(ItemData itemData)
    {
        return itemData.GetPlayerBodyAnimationDatabase();
    }

    /// <summary>
    /// Cache all animations from the PlayerBodyAnimationDatabase using enums
    /// </summary>
    private void CacheAnimationsFromDatabase(PlayerBodyAnimationDatabase database)
    {
        // Cache Ground state animations
        CacheStateAnimations(database.groundAnimations, PlayerStateType.Ground);

        // Cache Water state animations
        CacheStateAnimations(database.waterAnimations, PlayerStateType.Water);

        // Cache action animations from ActionAnimations class (includes held actions)
        CacheActionAnimations(database.actionAnimations);
    }

    /// <summary>
    /// Cache animations for a specific state
    /// </summary>
    private void CacheStateAnimations(StateAnimationSet stateSet, PlayerStateType state)
    {
        if (stateSet == null) return;

        // Cache locomotion animations for this state
        CacheLocomotionAnimations(stateSet, state);
    }

    /// <summary>
    /// Cache all locomotion animations for a state using enum iteration
    /// </summary>
    private void CacheLocomotionAnimations(StateAnimationSet stateSet, PlayerStateType state)
    {
        // Get all locomotion animation types for the given state
        var locomotionTypes = GetLocomotionAnimationTypesForState(state);

        foreach (var animType in locomotionTypes)
        {
            // Use the new enum-based method
            AnimationClip clip = stateSet.GetLocomotionAnimationByEnum(animType);

            animationCache[animType] = clip;
            animationExistence[animType] = clip != null;

            if (clip != null)
            {
                DebugLog($"Cached locomotion animation: {state}.{animType} -> {clip.name}");
            }
        }
    }

    /// <summary>
    /// Cache all action animations using enum iteration
    /// </summary>
    private void CacheActionAnimations(ActionAnimations actionAnimations)
    {
        if (actionAnimations == null) return;

        // Get all action animation types
        var allActionTypes = GetAllActionAnimationTypes();

        foreach (var actionType in allActionTypes)
        {
            // Use the new enum-based method
            AnimationClip clip = actionAnimations.GetActionAnimationByEnum(actionType);

            // Actions are universal across both Ground and Water states
            animationCache[actionType] = clip;
            animationExistence[actionType] = clip != null;

            if (clip != null)
            {
                DebugLog($"Cached action animation: {actionType} -> {clip.name}");
            }
        }
    }

    /// <summary>
    /// Get locomotion animation types valid for a specific state
    /// </summary>
    private PlayerAnimationType[] GetLocomotionAnimationTypesForState(PlayerStateType state)
    {
        return state switch
        {
            PlayerStateType.Ground => new PlayerAnimationType[]
            {
                PlayerAnimationType.Idle,
                PlayerAnimationType.WalkForward, PlayerAnimationType.WalkBackward,
                PlayerAnimationType.WalkLeft, PlayerAnimationType.WalkRight,
                PlayerAnimationType.WalkForwardLeft, PlayerAnimationType.WalkForwardRight,
                PlayerAnimationType.WalkBackwardLeft, PlayerAnimationType.WalkBackwardRight,
                PlayerAnimationType.RunForward,
                PlayerAnimationType.CrouchIdle,
                PlayerAnimationType.CrouchWalkForward, PlayerAnimationType.CrouchWalkBackward,
                PlayerAnimationType.CrouchWalkLeft, PlayerAnimationType.CrouchWalkRight,
                PlayerAnimationType.CrouchWalkForwardLeft, PlayerAnimationType.CrouchWalkForwardRight,
                PlayerAnimationType.CrouchWalkBackwardLeft, PlayerAnimationType.CrouchWalkBackwardRight
            },

            PlayerStateType.Water => new PlayerAnimationType[]
            {
                PlayerAnimationType.SwimIdle, PlayerAnimationType.SwimForward,
                PlayerAnimationType.SwimBackward, PlayerAnimationType.SwimLeft,
                PlayerAnimationType.SwimRight, PlayerAnimationType.SwimFastForward
            },

            _ => new PlayerAnimationType[0]
        };
    }

    /// <summary>
    /// Get all action animation types
    /// </summary>
    private PlayerAnimationType[] GetAllActionAnimationTypes()
    {
        return new PlayerAnimationType[]
        {
            // Basic actions
            PlayerAnimationType.PrimaryAction, PlayerAnimationType.SecondaryAction,
            PlayerAnimationType.ReloadAction, PlayerAnimationType.MeleeAction,
            
            // Held melee actions
            PlayerAnimationType.HeldMeleeActionStart, PlayerAnimationType.HeldMeleeActionLoop,
            PlayerAnimationType.HeldMeleeActionEndLight, PlayerAnimationType.HeldMeleeActionEndHeavy,
            PlayerAnimationType.HeldMeleeActionCancel,
            
            // Primary held actions (for Bows)
            PlayerAnimationType.HeldPrimaryActionStart, PlayerAnimationType.HeldPrimaryActionLoop,
            PlayerAnimationType.HeldPrimaryActionEnd, PlayerAnimationType.CancelHeldPrimaryAction,
            
            // Secondary held actions (for Throwables and some Tools)
            PlayerAnimationType.HeldSecondaryActionStart, PlayerAnimationType.HeldSecondaryActionLoop,
            PlayerAnimationType.HeldSecondaryActionEnd, PlayerAnimationType.CancelHeldSecondaryAction
        };
    }

    /// <summary>
    /// OPTIMIZED: Get cached animation with O(1) enum lookup
    /// </summary>
    public AnimationClip GetAnimation(PlayerStateType state, AnimationCategory category, PlayerAnimationType animationType)
    {
        if (!isCacheValid)
        {
            DebugLog("Animation cache is not valid - returning null");
            return null;
        }

        // Validate animation is appropriate for the requested state
        if (!animationType.IsValidForState(state))
        {
            DebugLog($"Animation {animationType} is not valid for state {state}");
            return null;
        }

        // Validate category matches animation type
        if (animationType.GetCategory() != category)
        {
            DebugLog($"Animation {animationType} category mismatch - requested {category}, actual {animationType.GetCategory()}");
            return null;
        }

        DebugLog($"Requesting animation: {state}.{category}.{animationType}");

        // Direct enum lookup - much faster than hash calculation
        if (animationCache.TryGetValue(animationType, out AnimationClip clip))
        {
            if (clip != null)
            {
                DebugLog($"Found cached animation: {animationType} -> {clip.name}");
            }
            return clip;
        }

        DebugLog($"Animation not found in cache: {animationType}");
        return null;
    }

    /// <summary>
    /// SIMPLIFIED: Get animation by enum directly (most common use case)
    /// </summary>
    public AnimationClip GetAnimation(PlayerAnimationType animationType)
    {
        if (!isCacheValid)
        {
            DebugLog("Animation cache is not valid - returning null");
            return null;
        }

        DebugLog($"Requesting animation: {animationType}");

        if (animationCache.TryGetValue(animationType, out AnimationClip clip))
        {
            if (clip != null)
            {
                DebugLog($"Found cached animation: {animationType} -> {clip.name}");
            }
            return clip;
        }

        DebugLog($"Animation not found in cache: {animationType}");
        return null;
    }

    /// <summary>
    /// OPTIMIZED: Check if animation exists with O(1) enum lookup
    /// </summary>
    public bool HasAnimation(PlayerAnimationType animationType)
    {
        if (!isCacheValid) return false;

        return animationExistence.TryGetValue(animationType, out bool exists) && exists;
    }

    /// <summary>
    /// Check if animation exists with state validation
    /// </summary>
    public bool HasAnimation(PlayerStateType state, AnimationCategory category, PlayerAnimationType animationType)
    {
        if (!isCacheValid) return false;
        if (!animationType.IsValidForState(state)) return false;
        if (animationType.GetCategory() != category) return false;

        return animationExistence.TryGetValue(animationType, out bool exists) && exists;
    }

    /// <summary>
    /// OPTIMIZED: Get locomotion animation with movement input conversion
    /// </summary>
    public AnimationClip GetLocomotionAnimation(PlayerStateType state, Vector2 input, bool isCrouching = false, bool isRunning = false)
    {
        PlayerAnimationType animationType = state switch
        {
            PlayerStateType.Ground => MovementToAnimationConverter.GetGroundLocomotionAnimation(input, isCrouching, isRunning),
            PlayerStateType.Water => MovementToAnimationConverter.GetWaterLocomotionAnimation(input, isRunning),
            _ => PlayerAnimationType.Idle
        };

        return GetAnimation(animationType);
    }

    /// <summary>
    /// Clear the animation cache
    /// </summary>
    public void ClearCache()
    {
        animationCache.Clear();
        animationExistence.Clear();
        cachedDatabase = null;
        isCacheValid = false;

        DebugLog("Animation cache cleared");
    }

    /// <summary>
    /// Get cache statistics for debugging
    /// </summary>
    public AnimationCacheStats GetCacheStats()
    {
        int existingCount = 0;
        foreach (var exists in animationExistence.Values)
        {
            if (exists) existingCount++;
        }

        return new AnimationCacheStats
        {
            isValid = isCacheValid,
            cachedItemType = cachedItemType,
            totalAnimations = animationCache.Count,
            existingAnimations = existingCount
        };
    }

    /// <summary>
    /// OPTIMIZED: Pre-cache animations for multiple items
    /// </summary>
    public static Dictionary<ItemType, PlayerBodyAnimationSetCache> PreCacheAnimationsForItems(ItemData[] items)
    {
        var cacheDict = new Dictionary<ItemType, PlayerBodyAnimationSetCache>();

        foreach (var item in items)
        {
            if (item == null) continue;

            if (!cacheDict.ContainsKey(item.itemType))
            {
                var cache = new PlayerBodyAnimationSetCache();
                cache.CacheAnimationsForItem(item);
                cacheDict[item.itemType] = cache;
            }
        }

        Debug.Log($"Pre-cached animations for {cacheDict.Count} item types");
        return cacheDict;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[AnimationSetCache] {message}");
        }
    }
}

/// <summary>
/// Statistics about the animation cache for debugging
/// </summary>
public struct AnimationCacheStats
{
    public bool isValid;
    public ItemType cachedItemType;
    public int totalAnimations;
    public int existingAnimations;

    public override string ToString()
    {
        return $"Cache Stats - Valid: {isValid}, Type: {cachedItemType}, " +
               $"Animations: {existingAnimations}/{totalAnimations}";
    }
}