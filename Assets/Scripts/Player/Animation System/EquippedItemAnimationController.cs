using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: EquippedItemAnimationController now uses enum-based animation system for maximum performance.
/// The key optimization is using enum-based animation lookups and proper held action state management.
/// </summary>
public class EquippedItemAnimationController : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float defaultCrossfadeDuration = 0.1f;
    [SerializeField] private float actionCrossfadeDuration = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Core references
    private PlayerAnimationManager animationManager;
    private Animator itemAnimator;
    private PlayerItemAnimationDatabase currentItemAnimationDatabase;

    // Current state tracking
    private ItemData currentEquippedItem = null;
    [ShowInInspector, ReadOnly] private PlayerAnimationType currentAnimationType = PlayerAnimationType.Idle;
    [ShowInInspector, ReadOnly] private AnimationClip currentClip = null;
    [ShowInInspector, ReadOnly] private bool isPlayingAction = false;

    // CRITICAL FIX: Track held action state using enums
    [ShowInInspector, ReadOnly] private bool isInHeldAction = false;
    [ShowInInspector, ReadOnly] private PlayerAnimationType currentHeldActionType = PlayerAnimationType.Idle;

    // Idle state management
    private bool shouldPlayIdle = true;
    private bool isCurrentlyIdle = false;

    // Events
    public System.Action<AnimationClip> OnItemAnimationStarted;

    #region Initialization

    public void Initialize(PlayerAnimationManager manager)
    {
        animationManager = manager;
        DebugLog("EquippedItemAnimationController initialized");
    }

    #endregion

    #region Item Management

    public void SetEquippedItem(ItemData itemData, GameObject itemGameObject)
    {
        ClearCurrentItem();

        if (itemData == null || itemGameObject == null)
        {
            DebugLog("Cleared equipped item - no item or GameObject provided");
            return;
        }

        itemAnimator = itemGameObject.GetComponent<Animator>();
        if (itemAnimator == null)
        {
            DebugLog($"No Animator found on equipped item: {itemData.itemName}");
            return;
        }

        currentItemAnimationDatabase = GetItemAnimationDatabase(itemData);
        if (currentItemAnimationDatabase == null)
        {
            DebugLog($"No PlayerItemAnimationDatabase found for item: {itemData.itemName}");
            return;
        }

        currentEquippedItem = itemData;

        // Start with idle animation
        PlayIdleAnimation();

        DebugLog($"Set equipped item: {itemData.itemName} with animator and animation database");
    }

    public void ClearCurrentItem()
    {
        if (currentEquippedItem != null)
        {
            DebugLog($"Clearing equipped item: {currentEquippedItem.itemName}");
        }

        itemAnimator = null;
        currentItemAnimationDatabase = null;
        currentEquippedItem = null;
        currentAnimationType = PlayerAnimationType.Idle;
        currentClip = null;
        isPlayingAction = false;
        isCurrentlyIdle = false;
        shouldPlayIdle = true;

        // CRITICAL FIX: Reset held action state
        isInHeldAction = false;
        currentHeldActionType = PlayerAnimationType.Idle;
    }

    private PlayerItemAnimationDatabase GetItemAnimationDatabase(ItemData itemData)
    {
        return itemData.playerItemAnimationDatabase;
    }

    #endregion

    #region Animation Playback - OPTIMIZED with Enums

    /// <summary>
    /// OPTIMIZED: Trigger an action animation using enum with proper held action detection
    /// </summary>
    public void TriggerItemAction(PlayerAnimationType actionType)
    {
        if (!CanPlayAnimation())
        {
            DebugLog($"Cannot play item action {actionType} - no valid item setup");
            return;
        }

        AnimationClip actionClip = currentItemAnimationDatabase.GetItemAnimation(actionType);

        if (actionClip == null)
        {
            DebugLog($"No item animation found for action: {actionType} on item {currentEquippedItem.itemName}");
            return;
        }

        DebugLog($"Triggering item action: {actionType} -> {actionClip.name}");

        // CRITICAL FIX: Detect if this is a held action using enum
        if (IsHeldActionType(actionType))
        {
            HandleHeldAction(actionType, actionClip);
        }
        else
        {
            // Regular instant action
            PlayActionAnimation(actionClip, actionType);
        }
    }

    /// <summary>
    /// OPTIMIZED: Detect if an action type is part of a held action sequence using enum
    /// </summary>
    private bool IsHeldActionType(PlayerAnimationType actionType)
    {
        return actionType switch
        {
            >= PlayerAnimationType.HeldPrimaryActionStart and <= PlayerAnimationType.CancelHeldPrimaryAction or
            >= PlayerAnimationType.HeldSecondaryActionStart and <= PlayerAnimationType.CancelHeldSecondaryAction or
            >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel => true,
            _ => false
        };
    }

    /// <summary>
    /// OPTIMIZED: Handle held action sequences properly using enums
    /// </summary>
    private void HandleHeldAction(PlayerAnimationType actionType, AnimationClip actionClip)
    {
        if (IsHeldActionStart(actionType))
        {
            // Starting a held action
            DebugLog($"Starting held action sequence: {actionType}");
            isInHeldAction = true;
            currentHeldActionType = actionType;
            shouldPlayIdle = false;

            PlayActionAnimation(actionClip, actionType);
        }
        else if (IsHeldActionLoop(actionType))
        {
            // Loop part of held action - should keep looping until ended
            DebugLog($"Playing held action loop: {actionType}");
            shouldPlayIdle = false; // CRITICAL: Don't return to idle during loop

            PlayLoopingAnimation(actionClip, actionType);
        }
        else if (IsHeldActionEnd(actionType))
        {
            // Ending held action
            DebugLog($"Ending held action sequence: {actionType}");
            isInHeldAction = false;
            currentHeldActionType = PlayerAnimationType.Idle;

            PlayActionAnimation(actionClip, actionType);
        }
        else if (IsHeldActionCancel(actionType))
        {
            // Cancelling held action
            DebugLog($"Cancelling held action sequence: {actionType}");
            isInHeldAction = false;
            currentHeldActionType = PlayerAnimationType.Idle;

            PlayActionAnimation(actionClip, actionType);
        }
        else
        {
            // Unknown held action type, treat as regular action
            PlayActionAnimation(actionClip, actionType);
        }
    }

    /// <summary>
    /// OPTIMIZED: Check if action is a held action start using enum
    /// </summary>
    private bool IsHeldActionStart(PlayerAnimationType actionType)
    {
        return actionType == PlayerAnimationType.HeldPrimaryActionStart ||
               actionType == PlayerAnimationType.HeldSecondaryActionStart ||
               actionType == PlayerAnimationType.HeldMeleeActionStart;
    }

    /// <summary>
    /// OPTIMIZED: Check if action is a held action loop using enum
    /// </summary>
    private bool IsHeldActionLoop(PlayerAnimationType actionType)
    {
        return actionType == PlayerAnimationType.HeldPrimaryActionLoop ||
               actionType == PlayerAnimationType.HeldSecondaryActionLoop ||
               actionType == PlayerAnimationType.HeldMeleeActionLoop;
    }

    /// <summary>
    /// OPTIMIZED: Check if action is a held action end using enum
    /// </summary>
    private bool IsHeldActionEnd(PlayerAnimationType actionType)
    {
        return actionType == PlayerAnimationType.HeldPrimaryActionEnd ||
               actionType == PlayerAnimationType.HeldSecondaryActionEnd ||
               actionType == PlayerAnimationType.HeldMeleeActionEndLight ||
               actionType == PlayerAnimationType.HeldMeleeActionEndHeavy;
    }

    /// <summary>
    /// OPTIMIZED: Check if action is a held action cancel using enum
    /// </summary>
    private bool IsHeldActionCancel(PlayerAnimationType actionType)
    {
        return actionType == PlayerAnimationType.CancelHeldPrimaryAction ||
               actionType == PlayerAnimationType.CancelHeldSecondaryAction ||
               actionType == PlayerAnimationType.HeldMeleeActionCancel;
    }

    /// <summary>
    /// CRITICAL FIX: Play looping animation without completion callback using enum
    /// </summary>
    private void PlayLoopingAnimation(AnimationClip clip, PlayerAnimationType actionType)
    {
        if (itemAnimator == null || clip == null) return;

        try
        {
            string stateName = clip.name;
            itemAnimator.CrossFade(stateName, actionCrossfadeDuration);

            // Update state tracking
            currentAnimationType = actionType;
            currentClip = clip;
            isPlayingAction = true;
            isCurrentlyIdle = false;

            // Fire event
            OnItemAnimationStarted?.Invoke(clip);

            DebugLog($"Playing looping item animation: {stateName} ({actionType}) - Will loop until ended");

            // CRITICAL FIX: No completion callback for looping animations!
            // The animation will keep looping until manually stopped
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to play looping item animation {clip.name}: {e.Message}");
        }
    }

    public void ReturnToIdle()
    {
        if (!CanPlayAnimation())
        {
            return;
        }

        // CRITICAL FIX: Don't return to idle if we're in a held action
        if (isInHeldAction)
        {
            DebugLog("Not returning to idle - currently in held action");
            return;
        }

        shouldPlayIdle = true;
        PlayIdleAnimation();
    }

    /// <summary>
    /// OPTIMIZED: Start a looping action using enum (for held actions like bow draw)
    /// </summary>
    public void StartLoopingAction(PlayerAnimationType actionType)
    {
        if (!CanPlayAnimation())
        {
            DebugLog($"Cannot start looping action {actionType} - no valid item setup");
            return;
        }

        AnimationClip loopClip = currentItemAnimationDatabase.GetItemAnimation(actionType);

        if (loopClip == null)
        {
            DebugLog($"No looping animation found for action: {actionType}");
            return;
        }

        DebugLog($"Starting looping action: {actionType} -> {loopClip.name}");

        // Mark as held action
        isInHeldAction = true;
        currentHeldActionType = actionType;
        shouldPlayIdle = false;

        PlayLoopingAnimation(loopClip, actionType);
    }

    /// <summary>
    /// Stop looping animation properly
    /// </summary>
    public void StopLoopingAction()
    {
        if (isPlayingAction)
        {
            DebugLog("Stopping looping action");
            isPlayingAction = false;
        }

        // CRITICAL FIX: Reset held action state
        isInHeldAction = false;
        currentHeldActionType = PlayerAnimationType.Idle;

        ReturnToIdle();
    }

    #endregion

    #region Animation Helpers

    private void PlayIdleAnimation()
    {
        if (!CanPlayAnimation() || isCurrentlyIdle)
        {
            return;
        }

        // CRITICAL FIX: Don't play idle if we're in a held action
        if (isInHeldAction)
        {
            DebugLog("Not playing idle - currently in held action");
            return;
        }

        AnimationClip idleClip = currentItemAnimationDatabase.GetItemAnimation(PlayerAnimationType.Idle);

        if (idleClip == null)
        {
            DebugLog($"No idle animation found for item: {currentEquippedItem.itemName}");
            return;
        }

        PlayAnimationClip(idleClip, PlayerAnimationType.Idle, true);
        isCurrentlyIdle = true;

        DebugLog($"Playing idle animation: {idleClip.name}");
    }

    /// <summary>
    /// OPTIMIZED: Play action animation with proper held action handling using enum
    /// </summary>
    private void PlayActionAnimation(AnimationClip clip, PlayerAnimationType actionType)
    {
        shouldPlayIdle = false;
        isCurrentlyIdle = false;

        PlayAnimationClip(clip, actionType, false);

        // CRITICAL FIX: Only set up completion callback for non-looping actions
        if (!IsHeldActionType(actionType))
        {
            StartCoroutine(WaitForActionCompletion(clip.length, actionType));
        }
    }

    private void PlayAnimationClip(AnimationClip clip, PlayerAnimationType animationType, bool loop)
    {
        if (itemAnimator == null || clip == null)
        {
            DebugLog("Cannot play animation - missing animator or clip");
            return;
        }

        try
        {
            string stateName = clip.name;
            float crossfadeDuration = animationType == PlayerAnimationType.Idle ? defaultCrossfadeDuration : actionCrossfadeDuration;

            itemAnimator.CrossFade(stateName, crossfadeDuration);

            currentAnimationType = animationType;
            currentClip = clip;
            isPlayingAction = !loop || animationType != PlayerAnimationType.Idle;

            OnItemAnimationStarted?.Invoke(clip);

            DebugLog($"Playing item animation: {stateName} ({animationType}) - Loop: {loop}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to play item animation {clip.name}: {e.Message}");
        }
    }

    /// <summary>
    /// OPTIMIZED: Wait for action completion with held action awareness using enum
    /// </summary>
    private IEnumerator WaitForActionCompletion(float duration, PlayerAnimationType actionType)
    {
        isPlayingAction = true;

        yield return new WaitForSeconds(duration);

        // CRITICAL FIX: Check held action state before returning to idle
        if (shouldPlayIdle && currentEquippedItem != null && !isInHeldAction)
        {
            DebugLog($"Action {actionType} completed - returning to idle");
            isPlayingAction = false;
            PlayIdleAnimation();
        }
        else if (isInHeldAction)
        {
            DebugLog($"Action {actionType} completed but staying in held action state");
            isPlayingAction = false; // Allow next animation in sequence
        }
    }

    private bool CanPlayAnimation()
    {
        if (currentEquippedItem == null) return false;
        if (itemAnimator == null) return false;
        if (currentItemAnimationDatabase == null) return false;
        if (!itemAnimator.isActiveAndEnabled) return false;
        return true;
    }

    #endregion

    #region Public API

    public bool IsPlayingAction()
    {
        return isPlayingAction;
    }

    /// <summary>
    /// OPTIMIZED: Get current animation type as enum
    /// </summary>
    public PlayerAnimationType GetCurrentAnimationType()
    {
        return currentAnimationType;
    }

    public ItemData GetCurrentEquippedItem()
    {
        return currentEquippedItem;
    }

    /// <summary>
    /// OPTIMIZED: Check if animation exists using enum
    /// </summary>
    public bool HasAnimation(PlayerAnimationType animationType)
    {
        if (currentItemAnimationDatabase == null)
            return false;

        return currentItemAnimationDatabase.HasItemAnimation(animationType);
    }

    /// <summary>
    /// Check if currently in a held action
    /// </summary>
    public bool IsInHeldAction()
    {
        return isInHeldAction;
    }

    /// <summary>
    /// OPTIMIZED: Get current held action type as enum
    /// </summary>
    public PlayerAnimationType GetCurrentHeldActionType()
    {
        return currentHeldActionType;
    }

    public void ForceStopAllAnimations()
    {
        if (itemAnimator != null && itemAnimator.isActiveAndEnabled)
        {
            itemAnimator.enabled = false;
            itemAnimator.enabled = true;
        }

        isPlayingAction = false;
        isCurrentlyIdle = false;
        shouldPlayIdle = true;

        // CRITICAL FIX: Reset held action state
        isInHeldAction = false;
        currentHeldActionType = PlayerAnimationType.Idle;

        if (CanPlayAnimation())
        {
            PlayIdleAnimation();
        }

        DebugLog("Force stopped all item animations");
    }

    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Equipped Item Animation Controller Debug ===");
        info.AppendLine($"Current Item: {currentEquippedItem?.itemName ?? "None"}");
        info.AppendLine($"Has Animator: {itemAnimator != null}");
        info.AppendLine($"Has Animation DB: {currentItemAnimationDatabase != null}");
        info.AppendLine($"Current Animation: {currentAnimationType} ({currentAnimationType.ToDebugString()})");
        info.AppendLine($"Is Playing Action: {isPlayingAction}");
        info.AppendLine($"Is Currently Idle: {isCurrentlyIdle}");
        info.AppendLine($"Should Play Idle: {shouldPlayIdle}");
        info.AppendLine($"Is In Held Action: {isInHeldAction}");
        info.AppendLine($"Current Held Action: {currentHeldActionType} ({currentHeldActionType.ToDebugString()})");

        if (currentItemAnimationDatabase != null)
        {
            var availableAnims = currentItemAnimationDatabase.GetAvailableAnimationTypesAsEnums();
            var animNames = new string[availableAnims.Length];
            for (int i = 0; i < availableAnims.Length; i++)
            {
                animNames[i] = availableAnims[i].ToDebugString();
            }
            info.AppendLine($"Available Animations: {string.Join(", ", animNames)}");
        }

        return info.ToString();
    }

    /// <summary>
    /// OPTIMIZED: Convert string action type to enum for internal processing
    /// </summary>
    private PlayerAnimationType ConvertStringToAnimationType(string actionType)
    {
        return actionType.ToLower() switch
        {
            "idle" => PlayerAnimationType.Idle,
            "primaryaction" => PlayerAnimationType.PrimaryAction,
            "secondaryaction" => PlayerAnimationType.SecondaryAction,
            "reloadaction" => PlayerAnimationType.ReloadAction,
            "meleeaction" => PlayerAnimationType.MeleeAction,
            "heldprimaryactionstart" => PlayerAnimationType.HeldPrimaryActionStart,
            "heldprimaryactionloop" => PlayerAnimationType.HeldPrimaryActionLoop,
            "heldprimaryactionend" => PlayerAnimationType.HeldPrimaryActionEnd,
            "cancelheldprimaryaction" => PlayerAnimationType.CancelHeldPrimaryAction,
            "heldsecondaryactionstart" => PlayerAnimationType.HeldSecondaryActionStart,
            "heldsecondaryactionloop" => PlayerAnimationType.HeldSecondaryActionLoop,
            "heldsecondaryactionend" => PlayerAnimationType.HeldSecondaryActionEnd,
            "cancelheldsecondaryaction" => PlayerAnimationType.CancelHeldSecondaryAction,
            "heldmeleeactionstart" => PlayerAnimationType.HeldMeleeActionStart,
            "heldmeleeactionloop" => PlayerAnimationType.HeldMeleeActionLoop,
            "heldmeleeactionendlight" => PlayerAnimationType.HeldMeleeActionEndLight,
            "heldmeleeactionendheavy" => PlayerAnimationType.HeldMeleeActionEndHeavy,
            "heldmeleeactioncancel" => PlayerAnimationType.HeldMeleeActionCancel,
            _ => PlayerAnimationType.Idle // Use Idle as "not found" indicator
        };
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        // Handle continuous idle playback with held action awareness
        if (shouldPlayIdle && !isCurrentlyIdle && !isPlayingAction && !isInHeldAction && CanPlayAnimation())
        {
            PlayIdleAnimation();
        }
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EquippedItemAnimationController] {message}");
        }
    }
}