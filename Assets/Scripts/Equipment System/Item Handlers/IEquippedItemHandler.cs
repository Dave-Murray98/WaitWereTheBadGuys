using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Interface for all equipped item handlers.
/// Each item type (weapon, consumable, tool, etc.) implements this to handle type-specific behavior.
/// REFACTORED: Now supports both instant and held actions properly.
/// </summary>
public interface IEquippedItemHandler
{
    /// <summary>The item type this handler manages</summary>
    ItemType HandledItemType { get; }

    /// <summary>Whether this handler is currently active</summary>
    bool IsActive { get; }

    /// <summary>The currently handled item data</summary>
    ItemData CurrentItemData { get; }

    /// <summary>Called when this handler's item type is equipped</summary>
    void OnItemEquipped(ItemData itemData);

    /// <summary>Called when this handler's item is unequipped</summary>
    void OnItemUnequipped();

    /// <summary>Handle primary action input (left click, shoot, use, consume, etc.)</summary>
    void HandlePrimaryAction(InputContext context);

    /// <summary>Handle secondary action input (right click, aim, reload, etc.)</summary>
    void HandleSecondaryAction(InputContext context);

    /// <summary>Handle reload action input (R key)</summary>
    void HandleReloadAction(InputContext context);

    /// <summary>Handle cancel action input (for cancelling held actions)</summary>
    void HandleCancelAction(InputContext context);

    /// <summary>Check if a specific action can be performed in current state</summary>
    bool CanPerformAction(string actionType, PlayerStateType playerState);

    /// <summary>Update handler state (called every frame while active)</summary>
    void UpdateHandler(float deltaTime);

    /// <summary>Get debug information about this handler</summary>
    string GetDebugInfo();
}

/// <summary>
/// Input context passed to handlers containing all relevant input and state information
/// </summary>
[System.Serializable]
public struct InputContext
{
    [Header("Action State")]
    public bool isPressed;      // Action was just pressed this frame
    public bool isHeld;         // Action is currently being held
    public bool isReleased;     // Action was just released this frame

    [Header("Player State")]
    public PlayerStateType currentPlayerState;
    public bool isMoving;
    public bool isCrouching;
    public bool isRunning;

    [Header("Look Input")]
    public Vector2 lookInput;
    public Vector3 lookDirection;

    [Header("Context")]
    public float deltaTime;
    public bool canPerformActions; // Whether player can perform actions (not paused, not in UI, etc.)

    /// <summary>Create an input context from current game state</summary>
    public static InputContext Create(bool pressed, bool held, bool released, PlayerStateType playerState, bool canAct = true)
    {
        var inputManager = InputManager.Instance;
        var playerController = Object.FindFirstObjectByType<PlayerController>();

        return new InputContext
        {
            isPressed = pressed,
            isHeld = held,
            isReleased = released,
            currentPlayerState = playerState,
            isMoving = playerController?.IsMoving ?? false,
            isCrouching = playerController?.IsCrouching ?? false,
            isRunning = playerController?.IsSprinting ?? false,
            lookInput = inputManager?.LookInput ?? Vector2.zero,
            lookDirection = playerController?.transform.forward ?? Vector3.forward,
            deltaTime = Time.deltaTime,
            canPerformActions = canAct && !(GameManager.Instance?.isPaused ?? false)
        };
    }
}

/// <summary>
/// REFACTORED: Action types to distinguish between instant and held actions
/// </summary>
public enum ActionType
{
    Instant,    // Single animation, complete when done (melee, consume, reload)
    Held        // Start → Loop → End/Cancel pattern (bow draw, throwable aim)
}

/// <summary>
/// REFACTORED: Action state for tracking held actions properly
/// </summary>
public enum ActionState
{
    None,           // No action being performed
    Starting,       // Held action starting (playing start animation)
    Looping,        // Held action looping (playing loop animation)
    Ending,         // Held action ending (playing end animation)
    Cancelling,     // Held action being cancelled (playing cancel animation)
    Instant         // Instant action being performed
}

/// <summary>
/// REFACTORED: BaseEquippedItemHandler now uses enum-based animation system for maximum performance.
/// All string-based animation comparisons have been replaced with fast enum operations.
/// Combines held melee, held primary, and held secondary actions into one unified system.
/// FIXED: Animation completion handling for proper held action transitions.
/// </summary>
public abstract class BaseEquippedItemHandler : MonoBehaviour, IEquippedItemHandler
{
    [Header("Debugging")]
    [SerializeField] protected bool enableDebugLogs = true;

    [Header("Current State")]
    [SerializeField, ReadOnly] protected ItemData currentItemData;
    [SerializeField, ReadOnly] protected bool isActive = false;

    // Enhanced action state tracking for both instant and held actions
    [SerializeField, ReadOnly] protected ActionState currentActionState = ActionState.None;
    [SerializeField, ReadOnly] protected ActionType currentActionType = ActionType.Instant;
    [SerializeField, ReadOnly] protected PlayerAnimationType currentActionAnimation = PlayerAnimationType.Idle;
    [SerializeField, ReadOnly] protected float actionStartTime = 0f;

    [Header("UNIFIED: Held Action System")]
    [SerializeField, ReadOnly] protected HeldActionType currentHeldActionType = HeldActionType.None;
    [SerializeField, ReadOnly] protected float lastLoopCheckTime = 0f;
    [SerializeField] protected float loopCheckInterval = 0.05f; // Check every 50ms
    [SerializeField, ReadOnly] protected bool isReadyForAction = false; // Generic ready state

    [Header("Melee Configuration")]
    [SerializeField] protected float meleeDamage = 10f;
    [SerializeField, ReadOnly] protected bool isMeleeing = false;
    [SerializeField, ReadOnly] protected float currentMeleeChargeTimer = 0f;
    [SerializeField] protected float meleeChargeTime = 2f;

    [Header("Timeout Protection")]
    [SerializeField] protected float actionTimeoutDuration = 10f;

    // System references - cached for performance
    protected PlayerController playerController;
    protected PlayerAnimationManager animationManager;
    protected PlayerStateManager stateManager;
    protected EquippedItemManager equipmentManager;

    /// <summary>
    /// Enum to track which type of held action is currently active
    /// </summary>
    public enum HeldActionType
    {
        None,
        Melee,          // Held melee (all handlers can melee)
        Primary,        // Held primary action (bow draw, etc.)
        Secondary       // Held secondary action (throwable aim, tool use, etc.)
    }

    #region Animation to Action Mapping System

    /// <summary>
    /// Maps specific animation types to their parent action types.
    /// This allows the system to understand that HeldMeleeActionStart belongs to MeleeAction, etc.
    /// </summary>
    private static readonly Dictionary<PlayerAnimationType, PlayerAnimationType> animationToActionMap =
        new Dictionary<PlayerAnimationType, PlayerAnimationType>
    {
        // Held Primary Action mappings
        { PlayerAnimationType.HeldPrimaryActionStart, PlayerAnimationType.PrimaryAction },
        { PlayerAnimationType.HeldPrimaryActionLoop, PlayerAnimationType.PrimaryAction },
        { PlayerAnimationType.HeldPrimaryActionEnd, PlayerAnimationType.PrimaryAction },
        { PlayerAnimationType.CancelHeldPrimaryAction, PlayerAnimationType.PrimaryAction },
        
        // Held Secondary Action mappings
        { PlayerAnimationType.HeldSecondaryActionStart, PlayerAnimationType.SecondaryAction },
        { PlayerAnimationType.HeldSecondaryActionLoop, PlayerAnimationType.SecondaryAction },
        { PlayerAnimationType.HeldSecondaryActionEnd, PlayerAnimationType.SecondaryAction },
        { PlayerAnimationType.CancelHeldSecondaryAction, PlayerAnimationType.SecondaryAction },
        
        // Held Melee Action mappings
        { PlayerAnimationType.HeldMeleeActionStart, PlayerAnimationType.MeleeAction },
        { PlayerAnimationType.HeldMeleeActionLoop, PlayerAnimationType.MeleeAction },
        { PlayerAnimationType.HeldMeleeActionEndLight, PlayerAnimationType.MeleeAction },
        { PlayerAnimationType.HeldMeleeActionEndHeavy, PlayerAnimationType.MeleeAction },
        { PlayerAnimationType.HeldMeleeActionCancel, PlayerAnimationType.MeleeAction }
    };

    /// <summary>
    /// Gets the parent action type for a given animation.
    /// If the animation is already a base action or has no mapping, returns the animation itself.
    /// </summary>
    /// <param name="animation">The animation to get the parent action for</param>
    /// <returns>The parent action type</returns>
    private PlayerAnimationType GetParentActionFromAnimation(PlayerAnimationType animation)
    {
        if (animationToActionMap.TryGetValue(animation, out PlayerAnimationType parentAction))
        {
            return parentAction;
        }

        // If no mapping exists, the animation is likely already a base action
        return animation;
    }

    /// <summary>
    /// Gets the loop animation for a given base action type.
    /// </summary>
    /// <param name="baseAction">The base action to get the loop animation for</param>
    /// <returns>The corresponding loop animation, or the original action if no loop exists</returns>
    private PlayerAnimationType GetLoopAnimationForAction(PlayerAnimationType baseAction)
    {
        return baseAction switch
        {
            PlayerAnimationType.PrimaryAction => PlayerAnimationType.HeldPrimaryActionLoop,
            PlayerAnimationType.SecondaryAction => PlayerAnimationType.HeldSecondaryActionLoop,
            PlayerAnimationType.MeleeAction => PlayerAnimationType.HeldMeleeActionLoop,
            _ => baseAction // Return original if no loop animation exists
        };
    }

    /// <summary>
    /// Gets the start animation for a given base action type.
    /// </summary>
    /// <param name="baseAction">The base action to get the start animation for</param>
    /// <returns>The corresponding start animation, or the original action if no start exists</returns>
    private PlayerAnimationType GetStartAnimationForAction(PlayerAnimationType baseAction)
    {
        return baseAction switch
        {
            PlayerAnimationType.PrimaryAction => PlayerAnimationType.HeldPrimaryActionStart,
            PlayerAnimationType.SecondaryAction => PlayerAnimationType.HeldSecondaryActionStart,
            PlayerAnimationType.MeleeAction => PlayerAnimationType.HeldMeleeActionStart,
            _ => baseAction // Return original if no start animation exists
        };
    }

    /// <summary>
    /// Checks if an animation is a "start" animation in a held action sequence.
    /// </summary>
    /// <param name="animation">The animation to check</param>
    /// <returns>True if this is a start animation</returns>
    private bool IsStartAnimation(PlayerAnimationType animation)
    {
        return animation switch
        {
            PlayerAnimationType.HeldPrimaryActionStart or
            PlayerAnimationType.HeldSecondaryActionStart or
            PlayerAnimationType.HeldMeleeActionStart => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if an animation is a "loop" animation in a held action sequence.
    /// </summary>
    /// <param name="animation">The animation to check</param>
    /// <returns>True if this is a loop animation</returns>
    private bool IsLoopAnimation(PlayerAnimationType animation)
    {
        return animation switch
        {
            PlayerAnimationType.HeldPrimaryActionLoop or
            PlayerAnimationType.HeldSecondaryActionLoop or
            PlayerAnimationType.HeldMeleeActionLoop => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if an animation is an "end" animation in a held action sequence.
    /// </summary>
    /// <param name="animation">The animation to check</param>
    /// <returns>True if this is an end animation</returns>
    private bool IsEndAnimation(PlayerAnimationType animation)
    {
        return animation switch
        {
            PlayerAnimationType.HeldPrimaryActionEnd or
            PlayerAnimationType.HeldSecondaryActionEnd or
            PlayerAnimationType.HeldMeleeActionEndLight or
            PlayerAnimationType.HeldMeleeActionEndHeavy => true,
            _ => false
        };
    }

    #endregion

    #region IEquippedItemHandler Implementation

    public abstract ItemType HandledItemType { get; }
    public bool IsActive => isActive;
    public ItemData CurrentItemData => currentItemData;

    public virtual void OnItemEquipped(ItemData itemData)
    {
        if (itemData != null && itemData.itemType != HandledItemType)
        {
            Debug.LogError($"[{GetType().Name}] Item type mismatch! Expected {HandledItemType}, got {itemData.itemType}");
            return;
        }

        // CRITICAL: Reset action state when switching items
        ResetActionState();

        currentItemData = itemData;
        isActive = true;

        // Ensure system references are cached
        CacheSystemReferences();

        // FIXED: Subscribe to animation completion events
        SubscribeToAnimationEvents();

        // Force animation refresh when item changes
        ForceAnimationRefresh();

        // Handle item-specific equip logic
        OnItemEquippedInternal(itemData);

        DebugLog($"Handler activated for {itemData?.itemName ?? "unarmed"} ({HandledItemType})");
    }

    /// <summary>
    /// FIXED: Override OnItemUnequipped to properly unsubscribe from events
    /// </summary>
    public virtual void OnItemUnequipped()
    {
        if (!isActive)
        {
            DebugLog("OnItemUnequipped called but handler not active");
            return;
        }

        // CRITICAL FIX: Unsubscribe from animation events FIRST
        UnsubscribeFromAnimationEvents();

        // Handle item-specific unequip logic
        OnItemUnequippedInternal();

        DebugLog($"Handler deactivated for {currentItemData?.itemName ?? "Unknown"} ({HandledItemType})");

        // Reset all state AFTER unsubscribing
        ResetAllState();
    }

    public virtual void HandlePrimaryAction(InputContext context)
    {
        if (!CanHandleAction(context, PlayerAnimationType.PrimaryAction))
        {
            DebugLog("Primary action cannot be handled in current state");
            return;
        }

        // UNIFIED: Check if this should be a held action based on handler implementation
        if (ShouldPrimaryActionBeHeld(context))
        {
            HandleHeldAction(context, HeldActionType.Primary, PlayerAnimationType.PrimaryAction);
        }
        else
        {
            // Delegate to specific handler implementation for instant actions
            HandlePrimaryActionInternal(context);
        }
    }

    public virtual void HandleSecondaryAction(InputContext context)
    {
        if (!CanHandleAction(context, PlayerAnimationType.SecondaryAction))
        {
            DebugLog("Secondary action cannot be handled in current state");
            return;
        }

        // UNIFIED: Check if this should be a held action based on handler implementation
        if (ShouldSecondaryActionBeHeld(context))
        {
            HandleHeldAction(context, HeldActionType.Secondary, PlayerAnimationType.SecondaryAction);
        }
        else
        {
            // Delegate to specific handler implementation for instant actions
            HandleSecondaryActionInternal(context);
        }
    }

    public virtual void HandleReloadAction(InputContext context)
    {
        if (!CanHandleAction(context, PlayerAnimationType.ReloadAction)) return;

        // Delegate to specific handler implementation
        HandleReloadActionInternal(context);
    }

    public virtual void HandleCancelAction(InputContext context)
    {
        if (!CanHandleAction(context, PlayerAnimationType.Idle)) return;

        // Delegate to specific handler implementation
        HandleCancelActionInternal(context);
    }

    public virtual bool CanPerformAction(string actionType, PlayerStateType playerState)
    {
        // Convert string to enum for internal processing
        var animationType = ConvertStringToAnimationType(actionType);
        return CanPerformAction(animationType, playerState);
    }

    /// <summary>
    /// OPTIMIZED: Can perform action using enum-based validation
    /// </summary>
    public virtual bool CanPerformAction(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        // CRITICAL: Check if handler is active first
        if (!isActive)
        {
            return false;
        }

        // Check item state restrictions (if we have an item)
        if (currentItemData != null && !currentItemData.CanUseInState(playerState))
        {
            return false;
        }

        // Different logic for different action states
        switch (currentActionState)
        {
            case ActionState.None:
                // Can start any action when not performing any action
                return CanPerformActionInternal(actionType, playerState);

            case ActionState.Starting:
                // During start phase, only allow cancellation if it's a held action that supports cancel
                return actionType == PlayerAnimationType.Idle && CanCancelCurrentAction();

            case ActionState.Looping:
                // During loop phase, allow ending or cancelling held actions
                if (actionType == PlayerAnimationType.Idle && CanCancelCurrentAction())
                    return true;

                // UNIFIED: For held actions, can "continue" the same action (release to end)
                return IsCurrentActionType(actionType) && currentActionType == ActionType.Held;

            case ActionState.Ending:
            case ActionState.Cancelling:
                // During end/cancel phases, can't start new actions
                return false;

            case ActionState.Instant:
                // During instant actions, can't do anything else
                return false;

            default:
                return false;
        }
    }

    public virtual void UpdateHandler(float deltaTime)
    {
        if (!isActive) return;

        // Check for stuck actions and timeout
        CheckActionTimeout();

        // UNIFIED: Handle held action loop continuation
        HandleHeldActionContinuation();

        // Update handler-specific logic
        UpdateHandlerInternal(deltaTime);

        if (isMeleeing)
            currentMeleeChargeTimer += deltaTime;
    }

    public virtual string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Action State: {currentActionState}, " +
               $"Action Type: {currentActionType}, " +
               $"Action Animation: {currentActionAnimation}, " +
               $"Held Action Type: {currentHeldActionType}, " +
               $"Is Meleeing: {isMeleeing}, " +
               $"Melee Charge: {currentMeleeChargeTimer:F2}s, " +
               $"Is Ready: {isReadyForAction}, " +
               $"Action Time: {(currentActionState != ActionState.None ? Time.time - actionStartTime : 0):F2}s";
    }

    #endregion

    #region UNIFIED: Held Action System

    /// <summary>
    /// UNIFIED: Handle any type of held action (melee, primary, secondary)
    /// </summary>
    protected virtual void HandleHeldAction(InputContext context, HeldActionType heldType, PlayerAnimationType actionType)
    {
        if (context.isPressed)
        {
            StartHeldActionUnified(heldType, actionType, context);
        }
        else if (context.isReleased)
        {
            EndHeldActionUnified(heldType, context);
        }
    }

    /// <summary>
    /// UNIFIED: Start any type of held action
    /// </summary>
    protected virtual void StartHeldActionUnified(HeldActionType heldType, PlayerAnimationType actionType, InputContext context)
    {
        // Check if already performing this type of action
        if (currentHeldActionType == heldType && (currentActionState == ActionState.Starting || currentActionState == ActionState.Looping))
        {
            DebugLog($"Already performing {heldType} held action - ignoring start input");
            return;
        }

        if (currentActionState != ActionState.None)
        {
            DebugLog($"Cannot start {heldType} held action - already performing {currentActionState}");
            return;
        }

        if (!context.canPerformActions || !IsPlayerInValidState())
        {
            DebugLog($"Cannot start {heldType} held action in current state");
            return;
        }

        DebugLog($"Starting {heldType} held action: {actionType}");

        // Set held action state
        currentHeldActionType = heldType;
        lastLoopCheckTime = Time.time;
        isReadyForAction = false;

        // Set specific state for melee
        if (heldType == HeldActionType.Melee)
        {
            isMeleeing = true;
            currentMeleeChargeTimer = 0f;
        }

        // Call specific handler setup
        OnHeldActionStarting(heldType, actionType, context);

        // Start held action using the base class system
        StartHeldAction(actionType);
    }

    /// <summary>
    /// UNIFIED: End any type of held action
    /// </summary>
    protected virtual void EndHeldActionUnified(HeldActionType heldType, InputContext context)
    {
        DebugLog($"EndHeldAction called - type: {heldType}, current: {currentHeldActionType}, actionState: {currentActionState}");

        if (currentHeldActionType != heldType)
        {
            DebugLog($"Not currently performing {heldType} held action - ignoring end input");
            return;
        }

        if (currentActionState != ActionState.Starting && currentActionState != ActionState.Looping)
        {
            DebugLog($"Cannot end {heldType} held action - not in valid state (current: {currentActionState})");
            return;
        }

        DebugLog($"Ending {heldType} held action");

        // Call specific handler logic
        OnHeldActionEnding(heldType, context);

        // End the held action - this will trigger the appropriate ending animation
        EndHeldAction();
    }

    /// <summary>
    /// UNIFIED: Handle held action loop continuation with timer system
    /// </summary>
    protected virtual void HandleHeldActionContinuation()
    {
        if (currentActionState != ActionState.Looping || currentHeldActionType == HeldActionType.None)
            return;

        // Only check periodically to avoid spam
        if (Time.time - lastLoopCheckTime < loopCheckInterval)
            return;

        lastLoopCheckTime = Time.time;

        // Check if the held action should continue based on type
        bool shouldContinue = ShouldContinueHeldAction();

        DebugLog($"{currentHeldActionType} loop continuation check - Should continue: {shouldContinue}");

        if (!shouldContinue)
        {
            // Input was released - end the action
            DebugLog($"Input released during {currentHeldActionType} loop - ending held action");
            EndHeldAction();
        }
    }

    /// <summary>
    /// UNIFIED: Check if held action should continue based on current type
    /// </summary>
    protected virtual bool ShouldContinueHeldAction()
    {
        return currentHeldActionType switch
        {
            HeldActionType.Melee => ShouldContinueMelee(),
            HeldActionType.Primary => ShouldContinuePrimary(),
            HeldActionType.Secondary => ShouldContinueSecondary(),
            _ => false
        };
    }

    /// <summary>
    /// Check if melee should continue (both primary and secondary can trigger melee)
    /// </summary>
    protected virtual bool ShouldContinueMelee()
    {
        bool primaryHeld = InputManager.Instance?.PrimaryActionHeld == true;
        bool secondaryHeld = InputManager.Instance?.SecondaryActionHeld == true;
        return primaryHeld || secondaryHeld;
    }

    /// <summary>
    /// Check if primary held action should continue
    /// </summary>
    protected virtual bool ShouldContinuePrimary()
    {
        return InputManager.Instance?.PrimaryActionHeld == true;
    }

    /// <summary>
    /// Check if secondary held action should continue
    /// </summary>
    protected virtual bool ShouldContinueSecondary()
    {
        return InputManager.Instance?.SecondaryActionHeld == true;
    }

    #endregion

    #region UNIFIED: Melee System

    /// <summary>
    /// UNIFIED: All handlers can now use melee through this system
    /// </summary>
    public virtual void HandleMeleeAction(InputContext context)
    {
        HandleHeldAction(context, HeldActionType.Melee, PlayerAnimationType.MeleeAction);
    }

    /// <summary>
    /// Execute melee attack with damage calculation
    /// </summary>
    protected virtual void ExecuteMelee()
    {
        bool isHeavyMelee = currentMeleeChargeTimer >= meleeChargeTime;
        float damageMultiplier = isHeavyMelee ? 2.0f : 1.0f;
        float finalDamage = GetMeleeDamage() * damageMultiplier;

        DebugLog($"Executing melee - Type: {(isHeavyMelee ? "Heavy" : "Light")}, " +
                $"Damage: {finalDamage}, Charge Time: {currentMeleeChargeTimer:F2}s");

        // Apply melee effects
        ApplyMeleeEffects(finalDamage, isHeavyMelee);

        // Call handler-specific melee logic
        OnMeleeExecuted(finalDamage, isHeavyMelee);
    }

    /// <summary>
    /// Apply melee effects (override in specific handlers for custom logic)
    /// </summary>
    protected virtual void ApplyMeleeEffects(float damage, bool isHeavy)
    {
        // TODO: Implement actual melee damage application logic
        DebugLog($"Melee dealt {damage} damage (Heavy: {isHeavy})");
    }

    /// <summary>
    /// Get melee damage (can be overridden by weapon handlers)
    /// </summary>
    protected virtual float GetMeleeDamage() => meleeDamage;

    #endregion

    #region Abstract Methods for Handlers

    /// <summary>Handle item-specific equip logic</summary>
    protected abstract void OnItemEquippedInternal(ItemData itemData);

    /// <summary>Handle primary action for this item type</summary>
    protected abstract void HandlePrimaryActionInternal(InputContext context);

    /// <summary>Handle secondary action for this item type</summary>
    protected abstract void HandleSecondaryActionInternal(InputContext context);

    /// <summary>Handle reload action for this item type (may be no-op)</summary>
    protected abstract void HandleReloadActionInternal(InputContext context);

    /// <summary>Check if specific action can be performed by this handler</summary>
    protected abstract bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState);

    /// <summary>Update handler-specific state</summary>
    protected abstract void UpdateHandlerInternal(float deltaTime);

    #endregion

    #region Virtual Methods for Customization

    /// <summary>Should primary action be held for this handler?</summary>
    protected virtual bool ShouldPrimaryActionBeHeld(InputContext context) => false;

    /// <summary>Should secondary action be held for this handler?</summary>
    protected virtual bool ShouldSecondaryActionBeHeld(InputContext context) => false;

    /// <summary>Called when a held action is starting</summary>
    protected virtual void OnHeldActionStarting(HeldActionType heldType, PlayerAnimationType actionType, InputContext context) { }

    /// <summary>Called when a held action is ending</summary>
    protected virtual void OnHeldActionEnding(HeldActionType heldType, InputContext context) { }

    /// <summary>Called when melee is executed</summary>
    protected virtual void OnMeleeExecuted(float damage, bool isHeavy) { }

    /// <summary>Handle cancel action for this item type</summary>
    protected virtual void HandleCancelActionInternal(InputContext context)
    {
        // Default implementation: try to cancel current held action
        if (currentActionState == ActionState.Looping && CanCancelCurrentAction())
        {
            CancelCurrentAction();
        }
    }

    #endregion

    #region FIXED: Event-Based Animation System

    /// <summary>
    /// FIXED: Subscribe to animation completion events from PlayerAnimationManager
    /// </summary>
    private void SubscribeToAnimationEvents()
    {
        if (animationManager != null)
        {
            // Subscribe to the animation completion event
            animationManager.OnActionAnimationComplete += OnAnimationComplete;
            DebugLog("Subscribed to PlayerAnimationManager.OnActionAnimationComplete");
        }
        else
        {
            DebugLog("Cannot subscribe to animation events - no PlayerAnimationManager found");
        }
    }

    /// <summary>
    /// FIXED: Unsubscribe from animation completion events
    /// </summary>
    private void UnsubscribeFromAnimationEvents()
    {
        if (animationManager != null)
        {
            animationManager.OnActionAnimationComplete -= OnAnimationComplete;
            DebugLog("Unsubscribed from PlayerAnimationManager.OnActionAnimationComplete");
        }
    }

    /// <summary>
    /// OPTIMIZED: Handle animation completion from PlayerAnimationManager event using enums
    /// </summary>
    private void OnAnimationComplete(string completedActionType)
    {
        // Convert string to enum for internal processing
        var completedAnimationType = ConvertStringToAnimationType(completedActionType);
        OnAnimationComplete(completedAnimationType);
    }

    /// <summary>
    /// FIXED: Updated animation completion handler that properly handles held action sequences.
    /// </summary>
    protected virtual void OnAnimationComplete(PlayerAnimationType completedAnimation)
    {
        // Get the parent action for this animation
        PlayerAnimationType parentAction = GetParentActionFromAnimation(completedAnimation);

        DebugLog($"=== Animation completion received: {completedAnimation} (Parent: {parentAction}) ===");
        DebugLog($"Current action: {currentActionAnimation}, State: {currentActionState}, Type: {currentActionType}");

        // Check if this animation belongs to our current action
        if (parentAction == currentActionAnimation)
        {
            // Handle the specific animation completion
            HandleSpecificAnimationCompletion(completedAnimation);
        }
        else
        {
            DebugLog($"Animation completion {completedAnimation} doesn't match current action {currentActionAnimation}");
            // You might want to handle this case differently based on your needs
        }
    }

    /// <summary>
    /// Handles the completion of specific animations within a held action sequence.
    /// </summary>
    /// <param name="completedAnimation">The specific animation that just completed</param>
    private void HandleSpecificAnimationCompletion(PlayerAnimationType completedAnimation)
    {
        // Handle start animations
        if (IsStartAnimation(completedAnimation))
        {
            HandleStartAnimationCompletion(completedAnimation);
        }
        // Handle loop animations
        else if (IsLoopAnimation(completedAnimation))
        {
            HandleLoopAnimationCompletion(completedAnimation);
        }
        // Handle end animations
        else if (IsEndAnimation(completedAnimation))
        {
            HandleEndAnimationCompletion(completedAnimation);
        }
        // Handle other animations (instant actions, etc.)
        else
        {
            HandleInstantAnimationCompletion(completedAnimation);
        }
    }

    /// <summary>
    /// Handles completion of start animations in held action sequences.
    /// </summary>
    private void HandleStartAnimationCompletion(PlayerAnimationType completedAnimation)
    {
        DebugLog($"Start animation completed: {completedAnimation}");

        // Only transition to loop if we're in a held action and still starting
        if (currentActionType == ActionType.Held && currentActionState == ActionState.Starting)
        {
            // Check if the input is still being held
            if (IsInputStillHeld())
            {
                StartHeldActionLoop();
            }
            else
            {
                // Input was released during start animation, go directly to end
                DebugLog("Input released during start animation, transitioning to end");
                StartHeldActionEnd();
            }
        }
        else
        {
            DebugLog($"Start animation completed but action type is {currentActionType} and state is {currentActionState}");
        }
    }

    /// <summary>
    /// Handles completion of loop animations in held action sequences.
    /// </summary>
    private void HandleLoopAnimationCompletion(PlayerAnimationType completedAnimation)
    {
        DebugLog($"Loop animation completed: {completedAnimation}");

        // Check if we should continue looping or end the action
        if (currentActionType == ActionType.Held && currentActionState == ActionState.Looping)
        {
            if (IsInputStillHeld())
            {
                // Continue looping
                DebugLog("Continuing loop animation");
                PlayerAnimationType loopAnimation = GetLoopAnimationForAction(currentActionAnimation);
                TriggerAnimation(loopAnimation);
            }
            else
            {
                // Input was released, transition to end
                DebugLog("Input released, transitioning to end");
                StartHeldActionEnd();
            }
        }
    }

    /// <summary>
    /// Handles completion of end animations in held action sequences.
    /// </summary>
    private void HandleEndAnimationCompletion(PlayerAnimationType completedAnimation)
    {
        DebugLog($"End animation completed: {completedAnimation}");
        CompleteAction();
    }

    /// <summary>
    /// Handles completion of instant (non-held) animations.
    /// </summary>
    private void HandleInstantAnimationCompletion(PlayerAnimationType completedAnimation)
    {
        DebugLog($"Instant animation completed: {completedAnimation}");

        // For instant actions, complete immediately
        if (currentActionType == ActionType.Instant)
        {
            CompleteAction();
        }
    }

    /// <summary>
    /// Transitions from the starting phase to the looping phase of a held action.
    /// </summary>
    private void StartHeldActionLoop()
    {
        DebugLog($"Transitioning to held action loop for: {currentActionAnimation}");

        // Update state
        currentActionState = ActionState.Looping;

        // Trigger the looping animation
        PlayerAnimationType loopAnimation = GetLoopAnimationForAction(currentActionAnimation);
        TriggerAnimation(loopAnimation);

        DebugLog($"Started held action loop: {loopAnimation}");

        // Call the override method for specific weapon handlers
        OnHeldActionLoopStarted(currentActionAnimation);
    }

    /// <summary>
    /// Transitions to the ending phase of a held action.
    /// </summary>
    private void StartHeldActionEnd()
    {
        DebugLog($"Transitioning to held action end for: {currentActionAnimation}");

        // Update state
        currentActionState = ActionState.Ending;

        // Get the appropriate end animation
        PlayerAnimationType endAnimation = GetEndAnimationForAction(currentActionAnimation);
        TriggerAnimation(endAnimation);

        DebugLog($"Started held action end: {endAnimation}");

        // Call the override method for specific weapon handlers
        OnHeldActionEnding(currentActionAnimation);
    }

    /// <summary>
    /// Gets the appropriate end animation for a base action.
    /// Override this in specific weapon handlers if you need custom logic (like light vs heavy attacks).
    /// </summary>
    protected virtual PlayerAnimationType GetEndAnimationForAction(PlayerAnimationType baseAction)
    {
        return baseAction switch
        {
            PlayerAnimationType.PrimaryAction => PlayerAnimationType.HeldPrimaryActionEnd,
            PlayerAnimationType.SecondaryAction => PlayerAnimationType.HeldSecondaryActionEnd,
            PlayerAnimationType.MeleeAction => PlayerAnimationType.HeldMeleeActionEndLight, // Default to light
            _ => baseAction
        };
    }

    /// <summary>
    /// Override this method in specific weapon handlers to handle loop start events.
    /// </summary>
    protected virtual void OnHeldActionLoopStarted(PlayerAnimationType baseAction)
    {
        // Override in derived classes
    }

    /// <summary>
    /// Override this method in specific weapon handlers to handle action ending events.
    /// </summary>
    protected virtual void OnHeldActionEnding(PlayerAnimationType baseAction)
    {
        // Override in derived classes
    }

    /// <summary>
    /// Checks if the input for the current action is still being held.
    /// You'll need to implement this based on your input system.
    /// </summary>
    protected virtual bool IsInputStillHeld()
    {
        // This needs to be implemented based on your specific input system
        switch (currentActionAnimation)
        {
            case PlayerAnimationType.PrimaryAction:
                return InputManager.Instance?.PrimaryActionHeld == true;
            case PlayerAnimationType.SecondaryAction:
                return InputManager.Instance?.SecondaryActionHeld == true;
            case PlayerAnimationType.MeleeAction:
                // For melee, check both primary and secondary inputs
                return (InputManager.Instance?.PrimaryActionHeld == true) ||
                       (InputManager.Instance?.SecondaryActionHeld == true);
            default:
                return false;
        }
    }

    /// <summary>
    /// Complete the current action and reset state.
    /// </summary>
    private void CompleteAction()
    {
        if (currentActionState == ActionState.None) return;

        PlayerAnimationType completedAction = currentActionAnimation;

        // Execute the action BEFORE resetting state
        OnHeldActionExecuted(completedAction);

        // Call completion logic BEFORE resetting state
        OnActionCompletedInternal(completedAction);

        // THEN reset state - this must come AFTER the above calls
        ResetActionState();
    }

    /// <summary>
    /// OPTIMIZED: Trigger instant action with event-based completion using enums
    /// </summary>
    protected virtual void TriggerInstantAction(PlayerAnimationType actionType)
    {
        if (currentActionState != ActionState.None)
        {
            DebugLog($"Cannot start instant action {actionType} - already performing {currentActionAnimation} ({currentActionState})");
            return;
        }

        DebugLog($"Starting instant action: {actionType}");

        currentActionState = ActionState.Instant;
        currentActionType = ActionType.Instant;
        currentActionAnimation = actionType;
        actionStartTime = Time.time;

        // Trigger animation - completion handled by event
        TriggerAnimation(actionType);
    }

    /// <summary>
    /// OPTIMIZED: Start held action with event-based completion using enums
    /// </summary>
    protected virtual void StartHeldAction(PlayerAnimationType actionType)
    {
        if (currentActionState != ActionState.None)
        {
            DebugLog($"Cannot start held action {actionType} - already performing {currentActionAnimation} ({currentActionState})");
            return;
        }

        DebugLog($"Starting held action: {actionType}");

        currentActionState = ActionState.Starting;
        currentActionType = ActionType.Held;
        currentActionAnimation = actionType;
        actionStartTime = Time.time;

        // Trigger start animation - completion handled by event
        PlayerAnimationType startAnimationType = GetHeldActionAnimationType(actionType, "start");
        TriggerAnimation(startAnimationType);
    }

    /// <summary>
    /// OPTIMIZED: End held action with event-based completion using enums
    /// </summary>
    protected virtual void EndHeldAction()
    {
        DebugLog($"trying to end held action: {currentActionAnimation}");

        if (currentActionState != ActionState.Starting && currentActionState != ActionState.Looping)
        {
            DebugLog($"Cannot end held action - not in startable/loopable state (current: {currentActionState})");
            return;
        }

        DebugLog($"Ending held action: {currentActionAnimation}");

        currentActionState = ActionState.Ending;

        // Handle melee vs other actions differently
        if (currentHeldActionType == HeldActionType.Melee)
        {
            HandleEndMelee(currentMeleeChargeTimer < meleeChargeTime); // Check if light or heavy melee
        }
        else
        {
            HandleEndAction();
        }
    }

    protected virtual void HandleEndMelee(bool isLightMeleeAttack)
    {
        PlayerAnimationType endAnimationType = isLightMeleeAttack ?
            PlayerAnimationType.HeldMeleeActionEndLight :
            PlayerAnimationType.HeldMeleeActionEndHeavy;

        DebugLog($"Ending melee action: {currentActionAnimation} (Light: {isLightMeleeAttack})");

        if (animationManager != null)
            animationManager.TriggerAction(endAnimationType);
        else
            DebugLog("Cannot end held action - no animation manager");
    }

    protected virtual void HandleEndAction()
    {
        // Trigger end animation - completion handled by event
        PlayerAnimationType endAnimationType = GetHeldActionAnimationType(currentActionAnimation, "end");

        if (animationManager != null)
            animationManager.TriggerAction(endAnimationType);
        else
            DebugLog("Cannot end held action - no animation manager");
    }

    /// <summary>
    /// OPTIMIZED: Cancel held action with event-based completion using enums
    /// </summary>
    protected virtual void CancelCurrentAction()
    {
        if (currentActionState != ActionState.Starting && currentActionState != ActionState.Looping)
        {
            DebugLog($"Cannot cancel action - not in cancellable state (current: {currentActionState})");
            return;
        }

        DebugLog($"Cancelling held action: {currentActionAnimation}");

        currentActionState = ActionState.Cancelling;

        // Trigger cancel animation - completion handled by event
        PlayerAnimationType cancelAnimationType = GetHeldActionAnimationType(currentActionAnimation, "cancel");

        if (animationManager != null)
        {
            animationManager.TriggerAction(cancelAnimationType);
        }
        else
        {
            DebugLog("Cannot cancel held action - no animation manager");
        }
    }

    /// <summary>
    /// OPTIMIZED: Get animation name for held action phases using enums
    /// </summary>
    protected virtual PlayerAnimationType GetHeldActionAnimationType(PlayerAnimationType baseActionType, string phase)
    {
        return currentHeldActionType switch
        {
            HeldActionType.Melee => GetMeleeAnimationType(phase),
            HeldActionType.Primary => GetPrimaryAnimationType(phase),
            HeldActionType.Secondary => GetSecondaryAnimationType(phase),
            _ => phase.ToLower() switch
            {
                "start" => PlayerAnimationType.HeldPrimaryActionStart, // Default fallback
                "loop" => PlayerAnimationType.HeldPrimaryActionLoop,
                "end" => PlayerAnimationType.HeldPrimaryActionEnd,
                "cancel" => PlayerAnimationType.CancelHeldPrimaryAction,
                _ => baseActionType
            }
        };
    }

    /// <summary>
    /// Check if current action can be cancelled
    /// </summary>
    protected virtual bool CanCancelCurrentAction()
    {
        // Override in concrete handlers based on item-specific rules
        return currentActionType == ActionType.Held &&
               (currentActionState == ActionState.Starting || currentActionState == ActionState.Looping);
    }

    /// <summary>
    /// OPTIMIZED: Check if action type matches current action using enums
    /// </summary>
    protected virtual bool IsCurrentActionType(PlayerAnimationType actionType)
    {
        return IsHeldActionPhase(actionType, currentActionAnimation);
    }

    /// <summary>
    /// OPTIMIZED: Check if completed animation is a phase of the current held action using enums
    /// </summary>
    private bool IsHeldActionPhase(PlayerAnimationType completedAnimationType, PlayerAnimationType currentAnimationType)
    {
        // Check if both animations are part of the same held action family
        return (completedAnimationType, currentAnimationType) switch
        {
            // Primary held action family
            ( >= PlayerAnimationType.HeldPrimaryActionStart and <= PlayerAnimationType.CancelHeldPrimaryAction,
             >= PlayerAnimationType.HeldPrimaryActionStart and <= PlayerAnimationType.CancelHeldPrimaryAction) => true,

            // Secondary held action family
            ( >= PlayerAnimationType.HeldSecondaryActionStart and <= PlayerAnimationType.CancelHeldSecondaryAction,
             >= PlayerAnimationType.HeldSecondaryActionStart and <= PlayerAnimationType.CancelHeldSecondaryAction) => true,

            // Melee held action family
            ( >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel,
             >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel) => true,

            _ => false
        };
    }

    #endregion

    #region FIXED: Animation Event Handling

    /// <summary>
    /// OPTIMIZED: Trigger animation through animation manager using enums
    /// </summary>
    protected virtual void TriggerAnimation(PlayerAnimationType animationType)
    {
        if (animationManager == null)
        {
            DebugLog($"Cannot trigger animation {animationType} - no AnimationManager found");
            CompleteCurrentAction(); // Fail gracefully
            return;
        }

        DebugLog($"Triggering animation: {animationType}");

        try
        {
            // FIXED: No callback needed - use event system
            animationManager.TriggerAction(animationType);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to trigger animation {animationType}: {e.Message}");
            CompleteCurrentAction(); // Fail gracefully
        }
    }

    /// <summary>
    /// Handle instant action completion
    /// </summary>
    protected virtual void HandleInstantActionComplete(PlayerAnimationType actionType)
    {
        DebugLog($"Instant action completed: {actionType}");
        ResetActionState();
        OnActionCompletedInternal(actionType);
    }

    /// <summary>
    /// Handle held action start completion - transition to loop
    /// </summary>
    protected virtual void HandleHeldActionStartComplete()
    {
        if (currentActionState != ActionState.Starting)
        {
            DebugLog("HandleHeldActionStartComplete called but not in starting state");
            return;
        }

        DebugLog($"Held action start complete - transitioning to loop: {currentActionAnimation}");

        currentActionState = ActionState.Looping;

        // Start loop animation
        PlayerAnimationType loopAnimationType = GetHeldActionAnimationType(currentActionAnimation, "loop");
        TriggerAnimation(loopAnimationType);

        // Notify handler that action is ready
        OnHeldActionReady();
    }

    /// <summary>
    /// Handle held action loop completion - continue if still held
    /// </summary>
    protected virtual void HandleHeldActionLoopComplete()
    {
        if (currentActionState != ActionState.Looping)
        {
            DebugLog("HandleHeldActionLoopComplete called but not in looping state");
            return;
        }

        // UNIFIED: For handlers that don't get proper loop completion events, use timer system
        DebugLog($"{currentHeldActionType} loop animation completed - using timer-based continuation");

        // Check if action should continue (input still held)
        if (ShouldContinueHeldAction())
        {
            // Continue looping
            PlayerAnimationType loopAnimationType = GetHeldActionAnimationType(currentActionAnimation, "loop");
            TriggerAnimation(loopAnimationType);
        }
        else
        {
            // Input was released during loop - end action
            EndHeldAction();
        }
    }

    /// <summary>
    /// Handle held action end completion
    /// </summary>
    protected virtual void HandleHeldActionEndComplete(PlayerAnimationType actionType)
    {
        DebugLog($"Held action end completed: {actionType}");

        // FIXED: Execute the action BEFORE resetting state
        OnHeldActionExecuted(actionType);

        // CRITICAL FIX: Call OnActionCompletedInternal BEFORE resetting state
        // so that currentHeldActionType is still available for the completion logic
        OnActionCompletedInternal(actionType);

        // THEN reset state - this must come AFTER OnActionCompletedInternal
        ResetActionState();
    }

    /// <summary>
    /// Handle held action cancel completion
    /// </summary>
    protected virtual void HandleHeldActionCancelComplete(PlayerAnimationType actionType)
    {
        DebugLog($"Held action cancel completed: {actionType}");
        ResetActionState();
        OnHeldActionCancelled(actionType);
        OnActionCompletedInternal(actionType);
    }

    #endregion

    #region UNIFIED: Held Action Event Handlers

    /// <summary>
    /// UNIFIED: Override action completion to handle all held action types using enums
    /// </summary>
    protected virtual void OnActionCompletedInternal(PlayerAnimationType actionType)
    {
        DebugLog($"=== Action Completed: {actionType} (Type: {currentHeldActionType}) ===");

        // Handle completion based on held action type
        switch (currentHeldActionType)
        {
            case HeldActionType.Melee:
                if (actionType.IsAction() && IsHeldActionPhase(actionType, PlayerAnimationType.MeleeAction))
                {
                    // Reset melee state after completion
                    isMeleeing = false;
                    currentMeleeChargeTimer = 0f;
                    OnMeleeCompleted();
                }
                break;

            case HeldActionType.Primary:
                DebugLog($"Calling OnPrimaryActionCompleted for: {actionType}");
                OnPrimaryActionCompleted(actionType);
                break;

            case HeldActionType.Secondary:
                OnSecondaryActionCompleted(actionType);
                break;
        }

        DebugLog($"Action {actionType} completed (base handler)");
    }

    /// <summary>
    /// UNIFIED: Called when held action becomes ready
    /// </summary>
    protected virtual void OnHeldActionReady()
    {
        isReadyForAction = true;
        DebugLog($"{currentHeldActionType} held action ready");

        // Call type-specific ready logic
        switch (currentHeldActionType)
        {
            case HeldActionType.Melee:
                OnMeleeReady();
                break;
            case HeldActionType.Primary:
                OnPrimaryActionReady();
                break;
            case HeldActionType.Secondary:
                OnSecondaryActionReady();
                break;
        }
    }

    /// <summary>
    /// UNIFIED: Called when held action is executed
    /// </summary>
    protected virtual void OnHeldActionExecuted(PlayerAnimationType actionType)
    {
        DebugLog($"{currentHeldActionType} held action executed: {actionType}");

        // Execute based on type
        switch (currentHeldActionType)
        {
            case HeldActionType.Melee:
                ExecuteMelee();
                break;
            case HeldActionType.Primary:
                ExecutePrimaryAction(actionType);
                break;
            case HeldActionType.Secondary:
                ExecuteSecondaryAction(actionType);
                break;
        }
    }

    /// <summary>
    /// UNIFIED: Called when held action is cancelled
    /// </summary>
    protected virtual void OnHeldActionCancelled(PlayerAnimationType actionType)
    {
        DebugLog($"{currentHeldActionType} held action cancelled: {actionType}");

        // Reset states based on type
        switch (currentHeldActionType)
        {
            case HeldActionType.Melee:
                isMeleeing = false;
                currentMeleeChargeTimer = 0f;
                break;
        }

        currentHeldActionType = HeldActionType.None;
        isReadyForAction = false;
    }

    /// <summary>
    /// OPTIMIZED: Get melee animation types using enums
    /// </summary>
    protected virtual PlayerAnimationType GetMeleeAnimationType(string phase)
    {
        return phase.ToLower() switch
        {
            "start" => PlayerAnimationType.HeldMeleeActionStart,
            "loop" => PlayerAnimationType.HeldMeleeActionLoop,
            "end" => DetermineMeleeEndAnimationType(),
            "cancel" => PlayerAnimationType.HeldMeleeActionCancel,
            _ => PlayerAnimationType.MeleeAction
        };
    }

    /// <summary>
    /// OPTIMIZED: Get primary action animation types using enums (override in specific handlers)
    /// </summary>
    protected virtual PlayerAnimationType GetPrimaryAnimationType(string phase)
    {
        return phase.ToLower() switch
        {
            "start" => PlayerAnimationType.HeldPrimaryActionStart,
            "loop" => PlayerAnimationType.HeldPrimaryActionLoop,
            "end" => PlayerAnimationType.HeldPrimaryActionEnd,
            "cancel" => PlayerAnimationType.CancelHeldPrimaryAction,
            _ => PlayerAnimationType.PrimaryAction
        };
    }

    /// <summary>
    /// OPTIMIZED: Get secondary action animation types using enums (override in specific handlers)
    /// </summary>
    protected virtual PlayerAnimationType GetSecondaryAnimationType(string phase)
    {
        return phase.ToLower() switch
        {
            "start" => PlayerAnimationType.HeldSecondaryActionStart,
            "loop" => PlayerAnimationType.HeldSecondaryActionLoop,
            "end" => PlayerAnimationType.HeldSecondaryActionEnd,
            "cancel" => PlayerAnimationType.CancelHeldSecondaryAction,
            _ => PlayerAnimationType.SecondaryAction
        };
    }

    /// <summary>
    /// OPTIMIZED: Determine melee end animation based on charge time using enums
    /// </summary>
    protected virtual PlayerAnimationType DetermineMeleeEndAnimationType()
    {
        bool isLightAttack = currentMeleeChargeTimer < meleeChargeTime;
        return isLightAttack ?
            PlayerAnimationType.HeldMeleeActionEndLight :
            PlayerAnimationType.HeldMeleeActionEndHeavy;
    }

    #endregion

    #region Virtual Methods for Handlers to Override

    protected virtual void OnMeleeReady() { DebugLog("Melee ready"); }
    protected virtual void OnMeleeCompleted() { DebugLog("Melee completed"); }
    protected virtual void OnPrimaryActionReady() { DebugLog("Primary action ready"); }
    protected virtual void OnPrimaryActionCompleted(PlayerAnimationType actionType) { DebugLog($"Primary action completed: {actionType}"); }
    protected virtual void OnSecondaryActionReady() { DebugLog("Secondary action ready"); }
    protected virtual void OnSecondaryActionCompleted(PlayerAnimationType actionType) { DebugLog($"Secondary action completed: {actionType}"); }
    protected virtual void ExecutePrimaryAction(PlayerAnimationType actionType) { DebugLog($"Execute primary action: {actionType}"); }
    protected virtual void ExecuteSecondaryAction(PlayerAnimationType actionType) { DebugLog($"Execute secondary action: {actionType}"); }

    #endregion

    #region Action Timeout and Safety

    /// <summary>
    /// Check for stuck actions and force timeout
    /// </summary>
    private void CheckActionTimeout()
    {
        if (currentActionState == ActionState.None) return;

        float actionDuration = Time.time - actionStartTime;
        if (actionDuration > actionTimeoutDuration)
        {
            Debug.LogWarning($"[{GetType().Name}] Action {currentActionAnimation} timed out after {actionDuration:F2}s - forcing completion");
            ForceCompleteAction();
        }
    }

    /// <summary>
    /// Force complete action (for timeouts or errors)
    /// </summary>
    protected virtual void ForceCompleteAction()
    {
        if (currentActionState == ActionState.None) return;

        PlayerAnimationType timedOutAction = currentActionAnimation;

        ResetActionState();

        DebugLog($"Force completed action: {timedOutAction}");
        OnActionCompletedInternal(timedOutAction);
    }

    /// <summary>
    /// Complete current action immediately (useful for interruptions)
    /// </summary>
    protected virtual void CompleteCurrentAction()
    {
        if (currentActionState == ActionState.None) return;

        PlayerAnimationType actionToComplete = currentActionAnimation;

        ResetActionState();

        DebugLog($"Completed current action: {actionToComplete}");
        OnActionCompletedInternal(actionToComplete);
    }

    #endregion

    #region State Management

    /// <summary>
    /// Reset action state
    /// </summary>
    protected void ResetActionState()
    {
        currentActionState = ActionState.None;
        currentActionType = ActionType.Instant;
        currentActionAnimation = PlayerAnimationType.Idle;
        actionStartTime = 0f;

        // UNIFIED: Reset held action state
        currentHeldActionType = HeldActionType.None;
        isReadyForAction = false;
        isMeleeing = false;
        currentMeleeChargeTimer = 0f;
        lastLoopCheckTime = 0f;

        DebugLog("Action state reset");
    }

    /// <summary>
    /// Reset all state when deactivating handler
    /// </summary>
    private void ResetAllState()
    {
        currentItemData = null;
        isActive = false;
        ResetActionState();
        DebugLog("All handler state reset");
    }

    /// <summary>OPTIMIZED: Check if handler can process an action using enums</summary>
    protected virtual bool CanHandleAction(InputContext context, PlayerAnimationType actionType)
    {
        if (!isActive)
        {
            DebugLog($"Cannot handle {actionType} action - handler not active");
            return false;
        }

        if (!context.canPerformActions)
        {
            DebugLog($"Cannot handle {actionType} action - context cannot perform actions");
            return false;
        }

        if (!CanPerformAction(actionType, context.currentPlayerState))
        {
            return false;
        }

        return true;
    }

    /// <summary>Cache system references for performance</summary>
    protected virtual void CacheSystemReferences()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (animationManager == null)
            animationManager = FindFirstObjectByType<PlayerAnimationManager>();

        if (stateManager == null)
            stateManager = PlayerStateManager.Instance;

        if (equipmentManager == null)
            equipmentManager = EquippedItemManager.Instance;
    }

    /// <summary>Handle item-specific unequip logic</summary>
    protected virtual void OnItemUnequippedInternal()
    {
        // Stop any ongoing action
        if (currentActionState != ActionState.None)
        {
            ForceCompleteAction();
        }
    }

    /// <summary>Force animation refresh when item changes</summary>
    protected virtual void ForceAnimationRefresh()
    {
        if (animationManager != null)
        {
            animationManager.RefreshAnimationSystem();
            DebugLog("Forced animation refresh for item change");
        }
    }

    /// <summary>Check if player is in valid state for item usage</summary>
    protected bool IsPlayerInValidState()
    {
        var currentState = stateManager?.CurrentStateType ?? PlayerStateType.Ground;
        return currentItemData?.CanUseInState(currentState) ?? true;
    }

    /// <summary>Get current player state</summary>
    protected PlayerStateType GetCurrentPlayerState()
    {
        return stateManager?.CurrentStateType ?? PlayerStateType.Ground;
    }

    /// <summary>OPTIMIZED: Convert string action type to enum for internal processing</summary>
    protected PlayerAnimationType ConvertStringToAnimationType(string actionType)
    {
        return actionType.ToLower() switch
        {
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
            "cancel" => PlayerAnimationType.Idle, // Use Idle as cancel indicator
            _ => PlayerAnimationType.Idle // Use Idle as "not found" indicator
        };
    }

    /// <summary>Log debug messages if enabled</summary>
    protected void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[{GetType().Name}] {message}");
        }
    }

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        CacheSystemReferences();
    }

    protected virtual void Update()
    {
        if (isActive)
        {
            UpdateHandler(Time.deltaTime);
        }
    }

    protected virtual void OnDestroy()
    {
        if (isActive)
        {
            OnItemUnequipped();
        }
    }

    #endregion
}