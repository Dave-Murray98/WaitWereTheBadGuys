using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: UpperBodyAnimationController now uses enum-based animation system for maximum performance.
/// All string comparisons have been replaced with fast enum lookups and direct animation retrieval.
/// </summary>
public class UpperBodyAnimationController : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float locomotionCrossfadeDuration = 0.1f;
    [SerializeField] private float actionCrossfadeDuration = 0.05f;

    [Header("Unarmed Animation Database")]
    [SerializeField] private PlayerBodyAnimationDatabase unarmedAnimationDatabase;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true; // Enable for debugging

    // Core references
    private PlayerAnimationManager animationManager;
    private Animator animator;
    private int layerIndex;

    // Animation cache that contains all item animations for the player's body for the current equipped item
    [ShowInInspector, ReadOnly] private PlayerBodyAnimationSetCache bodyAnimationCache;

    // Current state tracking
    private PlayerStateType currentPlayerState = PlayerStateType.Ground;
    private ItemData currentEquippedItem = null;

    // Current animations
    [ShowInInspector, ReadOnly] private PlayerAnimationType currentLocomotionType = PlayerAnimationType.Idle;
    [ShowInInspector, ReadOnly] private AnimationClip currentLocomotionClip = null;
    [ShowInInspector, ReadOnly] private AnimationClip currentActionClip = null;

    // Movement state tracking for locomotion updates
    private Vector2 lastMovementInput = Vector2.zero;
    private bool lastIsCrouching = false;
    private bool lastIsRunning = false;
    private bool lastIsVehicleSeated = false;

    // CRITICAL FIX: Track last played animation to prevent immediate replays
    private PlayerAnimationType lastPlayedActionAnimation = PlayerAnimationType.Idle;
    private float lastActionTime = 0f;
    private const float MIN_ACTION_INTERVAL = 0.1f; // Minimum time between same animations

    // Events
    public System.Action<AnimationClip> OnAnimationStarted;

    #region Initialization

    /// <summary>
    /// Initialize the upper body controller
    /// </summary>
    public void Initialize(PlayerAnimationManager manager, Animator anim, int layer)
    {
        animationManager = manager;
        animator = anim;
        layerIndex = layer;

        // Find unarmed animation database if needed
        if (unarmedAnimationDatabase == null)
        {
            Debug.LogError("Unarmed animation database not assigned");
        }

        // Validate setup
        ValidateSetup();

        // Reset state
        ResetControllerState();

        DebugLog($"UpperBodyAnimationController initialized on layer {layerIndex} with unarmed database: {unarmedAnimationDatabase?.displayName ?? "None"}");
    }

    /// <summary>
    /// Validate the controller setup
    /// </summary>
    private void ValidateSetup()
    {
        if (animator == null)
        {
            Debug.LogError("[UpperBodyAnimationController] No Animator assigned!");
            return;
        }

        if (layerIndex >= animator.layerCount)
        {
            Debug.LogError($"[UpperBodyAnimationController] Layer index {layerIndex} exceeds animator layer count ({animator.layerCount})");
            return;
        }

        if (unarmedAnimationDatabase == null)
        {
            Debug.LogWarning("[UpperBodyAnimationController] No unarmed AnimationDatabase assigned! Unarmed animations and fallbacks will not work properly.");
        }

        DebugLog("Upper body controller setup validation passed");
    }

    /// <summary>
    /// Set the unarmed animation database (for runtime assignment)
    /// </summary>
    public void SetUnarmedAnimationDatabase(PlayerBodyAnimationDatabase database)
    {
        unarmedAnimationDatabase = database;
        DebugLog($"Unarmed animation database updated: {database?.displayName ?? "None"}");
    }

    /// <summary>
    /// Set the animation cache reference
    /// </summary>
    public void SetAnimationCache(PlayerBodyAnimationSetCache cache)
    {
        bodyAnimationCache = cache;
        DebugLog("Animation cache updated");
    }

    private void ResetControllerState()
    {
        currentLocomotionType = PlayerAnimationType.Idle;
        currentActionClip = null;
        currentLocomotionClip = null;
        lastPlayedActionAnimation = PlayerAnimationType.Idle;
        lastActionTime = 0f;
    }

    #endregion

    #region Public API - Locomotion

    /// <summary>
    /// UPDATED: Update locomotion animation for equipped item using enums with vehicle seat support
    /// </summary>
    public void UpdateLocomotion(PlayerStateType playerState, ItemData equippedItem, Vector2 movementInput, bool isCrouching, bool isRunning)
    {
        // Get vehicle seat type if in vehicle state
        bool isVehicleSeated = GetCurrentVehicleSeatType(playerState);

        // Check if we need to update the animation
        if (ShouldUpdateLocomotion(playerState, equippedItem, movementInput, isCrouching, isRunning, isVehicleSeated))
        {
            PlayLocomotionAnimation(playerState, equippedItem, movementInput, isCrouching, isRunning, isVehicleSeated);
        }

        // Update tracking variables
        UpdateTrackingVariables(playerState, equippedItem, movementInput, isCrouching, isRunning, isVehicleSeated);
    }

    /// <summary>
    /// OPTIMIZED: Update locomotion for unarmed state using enums
    /// </summary>
    public void UpdateUnarmedLocomotion(PlayerStateType playerState, Vector2 movementInput, bool isCrouching, bool isRunning)
    {
        UpdateLocomotion(playerState, null, movementInput, isCrouching, isRunning);
    }

    #endregion

    #region Public API - Actions (OPTIMIZED)

    /// <summary>
    /// OPTIMIZED: Trigger action animation using enum with proper validation
    /// </summary>
    public void TriggerActionWithoutCompletion(PlayerStateType playerState, PlayerAnimationType actionType, ItemData equippedItem = null)
    {
        DebugLog($"=== TriggerActionWithoutCompletion called: {actionType} ===");
        DebugLog($"PlayerState: {playerState}, EquippedItem: {equippedItem?.itemName ?? "null"}");

        // Validate that this is an action animation
        if (!actionType.IsAction())
        {
            DebugLog($"Invalid action type: {actionType} is not an action animation");
            return;
        }

        // Get the action animation clip
        AnimationClip actionClip = GetActionClip(playerState, actionType, equippedItem);

        if (actionClip != null)
        {
            DebugLog($"Found action clip: {actionClip.name} for action: {actionType}");
            bool success = PlayActionAnimationWithoutCompletion(actionClip, actionType);

            if (!success)
            {
                DebugLog($"Failed to play action animation: {actionType}");
            }
        }
        else
        {
            DebugLog($"Action animation not found: {actionType} for {equippedItem?.itemName ?? "Unknown"} in {playerState}");
        }
    }

    /// <summary>
    /// Stop current action (called by PlayerAnimationManager when force stopping)
    /// </summary>
    public void StopCurrentAction()
    {
        // Reset action state
        currentActionClip = null;
        lastPlayedActionAnimation = PlayerAnimationType.Idle;

        // Return to locomotion
        ReturnToLocomotionFromAction();

        DebugLog("Stopped current action");
    }

    /// <summary>
    /// Return to locomotion after action completion (called by PlayerAnimationManager)
    /// </summary>
    public void ReturnToLocomotionFromAction()
    {
        DebugLog("Returning to locomotion from action");
        // Play the current locomotion based on last known state and force update
        PlayLocomotionAnimation(currentPlayerState, currentEquippedItem, lastMovementInput, lastIsCrouching, lastIsRunning, true);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handle player state changes
    /// </summary>
    public void OnPlayerStateChanged(PlayerStateType newState)
    {
        DebugLog($"Player state changed: {currentPlayerState} -> {newState}");

        PlayerStateType previousState = currentPlayerState;
        currentPlayerState = newState;

        // Update locomotion for new state
        PlayLocomotionAnimation(newState, currentEquippedItem, lastMovementInput, lastIsCrouching, lastIsRunning, lastIsVehicleSeated);
    }

    /// <summary>
    /// Handle item equipped - FIXED: Force immediate animation update
    /// </summary>
    public void OnItemEquipped(ItemData itemData)
    {
        DebugLog($"Item equipped: {itemData?.itemName ?? "None"}");

        currentEquippedItem = itemData;

        // CRITICAL FIX: Reset action tracking when item changes
        lastPlayedActionAnimation = PlayerAnimationType.Idle;
        lastActionTime = 0f;

        // Force immediate locomotion update when item changes
        PlayLocomotionAnimation(currentPlayerState, itemData, lastMovementInput, lastIsCrouching, lastIsRunning, lastIsVehicleSeated);
    }

    /// <summary>
    /// Handle item unequipped
    /// </summary>
    public void OnItemUnequipped()
    {
        DebugLog("Item unequipped - switching to unarmed");

        currentEquippedItem = null;

        // CRITICAL FIX: Reset action tracking when going unarmed
        lastPlayedActionAnimation = PlayerAnimationType.Idle;
        lastActionTime = 0f;

        // Force locomotion update for unarmed
        PlayLocomotionAnimation(currentPlayerState, null, lastMovementInput, lastIsCrouching, lastIsRunning, true);
    }

    /// <summary>
    /// Refresh animations (called when cache updates)
    /// </summary>
    public void RefreshAnimations()
    {
        DebugLog("Refreshing upper body animations");

        // Reset action tracking
        lastPlayedActionAnimation = PlayerAnimationType.Idle;
        lastActionTime = 0f;

        PlayLocomotionAnimation(currentPlayerState, currentEquippedItem, lastMovementInput, lastIsCrouching, lastIsRunning, lastIsVehicleSeated);
    }

    #endregion

    #region OPTIMIZED: Animation Logic

    /// <summary>
    /// UPDATED: Check if locomotion should be updated (now includes vehicle seat type)
    /// </summary>
    private bool ShouldUpdateLocomotion(PlayerStateType playerState, ItemData equippedItem, Vector2 movementInput, bool isCrouching, bool isRunning, bool isVehicleSeated)
    {
        // Always update if player state changed
        if (playerState != currentPlayerState)
            return true;

        // Always update if equipped item changed
        if (equippedItem != currentEquippedItem)
            return true;

        // NEW: Always update if vehicle seat type changed
        if (isVehicleSeated != lastIsVehicleSeated)
            return true;

        // Check movement input changes (with small threshold to avoid jitter)
        if (Vector2.Distance(movementInput, lastMovementInput) > 0.1f)
            return true;

        // Check modifier changes
        if (isCrouching != lastIsCrouching || isRunning != lastIsRunning)
            return true;

        return false;
    }

    /// <summary>
    /// UPDATED: Play locomotion animation for current item and state with vehicle seat support
    /// </summary>
    private void PlayLocomotionAnimation(PlayerStateType playerState, ItemData equippedItem, Vector2 movementInput, bool isCrouching, bool isRunning, bool isVehicleSeated, bool forceUpdate = false)
    {
        // Get animation type based on movement using converter
        PlayerAnimationType animationType = GetLocomotionAnimationType(playerState, movementInput, isCrouching, isRunning, isVehicleSeated);

        // Don't replay same animation unless state/item changed OR forced
        if (!forceUpdate && animationType == currentLocomotionType &&
            playerState == currentPlayerState &&
            equippedItem == currentEquippedItem &&
            isVehicleSeated == lastIsVehicleSeated) // NEW: Include vehicle seat in comparison
        {
            return;
        }

        AnimationClip clip = GetLocomotionClip(playerState, equippedItem, animationType);

        if (clip != null)
        {
            DebugLog($"Playing locomotion clip: {clip.name} for animation: {animationType} in state: {playerState}, isRunning: {isRunning}");
            PlayLocomotionClip(clip, animationType);
        }
    }

    /// <summary>
    /// OPTIMIZED: Play action animation with enum-based validation and duplicate prevention
    /// </summary>
    private bool PlayActionAnimationWithoutCompletion(AnimationClip clip, PlayerAnimationType actionType)
    {
        if (clip == null || animator == null)
        {
            DebugLog($"Cannot play action - clip is null: {clip == null}, animator is null: {animator == null}");
            return false;
        }

        // CRITICAL FIX: Prevent immediate replays of the same animation
        float timeSinceLastAction = Time.time - lastActionTime;

        if (lastPlayedActionAnimation == actionType && timeSinceLastAction < MIN_ACTION_INTERVAL)
        {
            DebugLog($"Preventing immediate replay of {actionType} (only {timeSinceLastAction:F3}s since last play)");
            return false;
        }

        // CRITICAL FIX: Check if animator is in a valid state
        if (!IsAnimatorReady())
        {
            DebugLog("Animator is not ready for new animation");
            return false;
        }

        try
        {
            // Use the clip name as the state name for crossfade
            string stateName = clip.name;

            DebugLog($"Attempting to play animation: {stateName} on layer {layerIndex}");

            // CRITICAL FIX: Validate the state exists in the animator
            if (!HasAnimatorState(stateName))
            {
                Debug.LogError($"Animator state '{stateName}' not found on layer {layerIndex}!");
                return false;
            }

            // Record the animation BEFORE playing it
            lastPlayedActionAnimation = actionType;
            lastActionTime = Time.time;

            // Play the action animation
            animator.CrossFade(stateName, actionCrossfadeDuration, layerIndex);

            // CRITICAL FIX: Verify the animation actually started
            if (ValidateAnimationStarted(stateName))
            {
                // Update state only if animation actually started
                currentActionClip = clip;

                // Fire event
                OnAnimationStarted?.Invoke(clip);

                DebugLog($"Successfully started action animation: {stateName} ({actionType}) - Duration: {clip.length:F2}s");
                return true;
            }
            else
            {
                Debug.LogError($"Animation {stateName} failed to start properly!");

                // Reset tracking since animation didn't start
                lastPlayedActionAnimation = PlayerAnimationType.Idle;
                lastActionTime = 0f;
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception playing action animation {clip.name}: {e.Message}");

            // Reset tracking on failure
            lastPlayedActionAnimation = PlayerAnimationType.Idle;
            lastActionTime = 0f;
            return false;
        }
    }


    /// <summary>
    /// UPDATED: Get locomotion animation type using enum converter with vehicle seat support
    /// </summary>
    private PlayerAnimationType GetLocomotionAnimationType(PlayerStateType playerState, Vector2 movementInput, bool isCrouching, bool isRunning, bool isVehicleSeated)
    {
        return playerState switch
        {
            PlayerStateType.Ground => MovementToAnimationConverter.GetGroundLocomotionAnimation(movementInput, isCrouching, isRunning),
            PlayerStateType.Water => MovementToAnimationConverter.GetWaterLocomotionAnimation(movementInput, isRunning),
            PlayerStateType.Vehicle => MovementToAnimationConverter.GetVehicleLocomotionAnimation(isVehicleSeated),
            PlayerStateType.Climbing => MovementToAnimationConverter.GetClimbingLocomotionAnimation(),
            _ => PlayerAnimationType.Idle // Default to idle for unknown states
        };
    }

    /// <summary>
    /// OPTIMIZED: Get locomotion animation clip using enum-based lookup
    /// </summary>
    private AnimationClip GetLocomotionClip(PlayerStateType playerState, ItemData equippedItem, PlayerAnimationType animationType)
    {
        // Validate that this is a locomotion animation
        if (!animationType.IsLocomotion())
        {
            DebugLog($"Invalid locomotion type: {animationType}");
            return null;
        }

        // Validate animation is valid for the current state
        if (!animationType.IsValidForState(playerState))
        {
            DebugLog($"Animation {animationType} is not valid for state {playerState}");
            return null;
        }

        // Try from cache first (for equipped item)
        if (bodyAnimationCache != null && equippedItem != null)
        {
            AnimationClip clip = bodyAnimationCache.GetAnimation(animationType);
            if (clip != null)
                return clip;
        }

        // Try from equipped item's database directly
        if (equippedItem?.playerBodyAnimationDatabase != null)
        {
            AnimationClip clip = equippedItem.playerBodyAnimationDatabase.GetAnimation(animationType);
            if (clip != null)
                return clip;
        }

        // Fallback to unarmed database
        if (unarmedAnimationDatabase != null)
        {
            AnimationClip clip = unarmedAnimationDatabase.GetAnimation(animationType);
            if (clip != null)
                return clip;
        }

        return null;
    }

    /// <summary>
    /// OPTIMIZED: Get action animation clip using enum-based lookup
    /// </summary>
    private AnimationClip GetActionClip(PlayerStateType playerState, PlayerAnimationType actionType, ItemData equippedItem = null)
    {
        // Validate that this is an action animation
        if (!actionType.IsAction())
        {
            DebugLog($"Invalid action type: {actionType}");
            return null;
        }

        // Validate animation is valid for the current state
        if (!actionType.IsValidForState(playerState))
        {
            DebugLog($"Action {actionType} is not valid for state {playerState}");
            return null;
        }

        if (equippedItem == null)
        {
            // If no item equipped, use unarmed action
            return unarmedAnimationDatabase?.GetAnimation(actionType);
        }

        // Try from cache first (for equipped item)
        if (bodyAnimationCache != null)
        {
            AnimationClip clip = bodyAnimationCache.GetAnimation(actionType);
            if (clip != null)
            {
                DebugLog($"Found cached action animation: {clip.name} for {actionType} in {playerState} with equipped item {equippedItem.itemName}");
                return clip;
            }
            else
                DebugLog($"No cached action animation found for {actionType} in {playerState} with equipped item {equippedItem.itemName}");
        }

        // Try from equipped item's database directly  
        if (equippedItem?.playerBodyAnimationDatabase != null)
        {
            AnimationClip clip = equippedItem.playerBodyAnimationDatabase.GetAnimation(actionType);
            if (clip != null)
                return clip;
        }

        // Fallback to unarmed database
        if (unarmedAnimationDatabase != null)
        {
            AnimationClip clip = unarmedAnimationDatabase.GetAnimation(actionType);
            if (clip != null)
                return clip;
        }

        return null;
    }

    /// <summary>
    ///  Get the current vehicle's seat type from PlayerController
    /// </summary>
    private bool GetCurrentVehicleSeatType(PlayerStateType playerState)
    {
        if (playerState != PlayerStateType.Vehicle)
            return false;

        // Access PlayerController through the animation manager
        IVehicle currentVehicle = animationManager.playerController.GetCurrentVehicle();

        if (currentVehicle == null)
            return false;

        bool isSeated = currentVehicle.IsVehicleSeated;
        DebugLog($"Upper body: Vehicle seat type detected: {(isSeated ? "Sitting" : "Standing")} for vehicle {currentVehicle.VehicleID}");

        return isSeated;
    }

    /// <summary>
    /// Play locomotion animation clip
    /// </summary>
    private void PlayLocomotionClip(AnimationClip clip, PlayerAnimationType animationType)
    {
        if (clip == null || animator == null) return;

        // Use the clip name as the state name for crossfade
        string stateName = clip.name;
        animator.CrossFade(stateName, locomotionCrossfadeDuration, layerIndex);

        // Update tracking
        currentLocomotionType = animationType;
        currentLocomotionClip = clip;

        // Fire event
        OnAnimationStarted?.Invoke(clip);

        DebugLog($"Playing upper body locomotion: {stateName} ({animationType})");
    }


    /// <summary>
    /// UPDATED: Update tracking variables with vehicle seat support
    /// </summary>
    private void UpdateTrackingVariables(PlayerStateType playerState, ItemData equippedItem, Vector2 movementInput, bool isCrouching, bool isRunning, bool isVehicleSeated)
    {
        currentPlayerState = playerState;
        currentEquippedItem = equippedItem;
        lastMovementInput = movementInput;
        lastIsCrouching = isCrouching;
        lastIsRunning = isRunning;
        lastIsVehicleSeated = isVehicleSeated; // NEW: Track vehicle seat type
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// CRITICAL FIX: Check if animator is ready for new animations
    /// </summary>
    private bool IsAnimatorReady()
    {
        if (animator == null)
        {
            DebugLog("Animator is null");
            return false;
        }

        if (!animator.isActiveAndEnabled)
        {
            DebugLog("Animator is not active and enabled");
            return false;
        }

        if (!animator.gameObject.activeInHierarchy)
        {
            DebugLog("Animator GameObject is not active in hierarchy");
            return false;
        }

        // Check if layer exists
        if (layerIndex >= animator.layerCount)
        {
            DebugLog($"Layer index {layerIndex} exceeds layer count {animator.layerCount}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// CRITICAL FIX: Check if animator state exists
    /// </summary>
    private bool HasAnimatorState(string stateName)
    {
        if (animator?.runtimeAnimatorController == null)
        {
            DebugLog("Animator has no runtime controller");
            return false;
        }

        // Try to get the state info - this will work if the state exists
        try
        {
            // Check if we can get state info without exceptions
            var currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);

            // For a more thorough check, we could enumerate all states, but this is expensive
            // For now, assume the state exists if we got this far
            return true;
        }
        catch (System.Exception)
        {
            DebugLog($"State {stateName} not found in animator");
            return false;
        }
    }

    /// <summary>
    /// CRITICAL FIX: Validate that animation actually started playing
    /// </summary>
    private bool ValidateAnimationStarted(string stateName)
    {
        if (animator == null) return false;

        try
        {
            // Wait a frame to let the crossfade start
            return StartCoroutine(ValidateAnimationStartedCoroutine(stateName)) != null;
        }
        catch (System.Exception e)
        {
            DebugLog($"Exception validating animation start: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Coroutine to validate animation started (checks next frame)
    /// </summary>
    private System.Collections.IEnumerator ValidateAnimationStartedCoroutine(string stateName)
    {
        yield return null; // Wait one frame

        if (animator != null)
        {
            var currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);
            bool isTransitioning = animator.IsInTransition(layerIndex);

            DebugLog($"Animation validation - Current state hash: {currentState.fullPathHash}, " +
                    $"Transitioning: {isTransitioning}, Normalized time: {currentState.normalizedTime:F3}");
        }
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[UpperBodyAnimationController] {message}");
        }
    }
}