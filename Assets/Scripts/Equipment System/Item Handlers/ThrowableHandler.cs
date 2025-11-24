using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: ThrowableHandler implementation using enum-based animation system.
/// Primary action: Melee attack (through unified system)
/// Secondary action: Aim and throw (held action - start → loop → end)
/// Supports cancellable throws based on ThrowableData settings
/// </summary>
public class ThrowableHandler : BaseEquippedItemHandler
{
    [Header("Throwable Audio")]
    [SerializeField] private AudioClip aimStartSound;
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private AudioClip cancelSound;

    [Header("Throwable State")]
    [SerializeField, ReadOnly] private bool isAiming = false;
    [SerializeField, ReadOnly] private bool isReadyToThrow = false;

    // Components
    private AudioSource audioSource;
    private PlayerInventoryManager inventoryManager;

    // Quick access to throwable data
    private ThrowableData ThrowableData => currentItemData?.ThrowableData;

    // Events
    public System.Action<ItemData> OnThrowableEquipped;
    public System.Action OnThrowableUnequipped;
    public System.Action OnThrowableThrown;
    public System.Action OnAimingStarted;
    public System.Action OnAimingStopped;

    public override ItemType HandledItemType => ItemType.Throwable;

    #region Initialization

    protected override void Awake()
    {
        base.Awake();
        SetupComponents();
    }

    private void SetupComponents()
    {
        // Audio setup
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
        }
    }

    protected override void CacheSystemReferences()
    {
        base.CacheSystemReferences();

        if (inventoryManager == null)
            inventoryManager = PlayerInventoryManager.Instance;
    }

    #endregion

    #region Base Handler Implementation

    protected override void OnItemEquippedInternal(ItemData itemData)
    {
        if (itemData?.itemType != ItemType.Throwable)
        {
            Debug.LogError($"ThrowableHandler received invalid item: {itemData?.itemName ?? "null"}");
            return;
        }

        // Reset throwable state
        isAiming = false;
        isReadyToThrow = false;

        DebugLog($"Equipped throwable: {itemData.itemName}");
        OnThrowableEquipped?.Invoke(itemData);
    }

    protected override void OnItemUnequippedInternal()
    {
        base.OnItemUnequippedInternal();

        // Reset throwable state
        isAiming = false;
        isReadyToThrow = false;

        DebugLog("Unequipped throwable");
        OnThrowableUnequipped?.Invoke();
    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // Primary = Melee attack (using throwable as improvised weapon)
        if (context.isPressed)
        {
            HandleMeleeAction(context);
        }
    }

    protected override void HandleSecondaryActionInternal(InputContext context)
    {
        // Secondary = Aim and throw (held action)
        // This will be handled by the unified system since ShouldSecondaryActionBeHeld returns true
        DebugLog("Secondary action should be handled by unified held system");
    }

    protected override void HandleReloadActionInternal(InputContext context)
    {
        // Not applicable for throwables
        DebugLog("Reload not applicable for throwables");
    }

    protected override void HandleCancelActionInternal(InputContext context)
    {
        // Cancel throw if possible
        if (context.isPressed && CanCancelThrow())
        {
            CancelThrow();
        }
    }

    protected override bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                // Can melee if not aiming
                return !isAiming && currentActionState == ActionState.None;

            case PlayerAnimationType.SecondaryAction:
            case >= PlayerAnimationType.HeldSecondaryActionStart and <= PlayerAnimationType.CancelHeldSecondaryAction:
                return CanAimThrowable(playerState);

            default:
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        // Base class handles held action continuation
    }

    #endregion

    #region Unified System Configuration

    /// <summary>
    /// Primary action is instant (melee with throwable)
    /// </summary>
    protected override bool ShouldPrimaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Secondary action is held (aim and throw)
    /// </summary>
    protected override bool ShouldSecondaryActionBeHeld(InputContext context) => true;

    /// <summary>
    /// Override melee damage for throwable (weaker than dedicated weapons)
    /// </summary>
    protected override float GetMeleeDamage() => meleeDamage * 0.8f; // 80% of base damage

    /// <summary>
    /// Override to check if current action can be cancelled
    /// </summary>
    protected override bool CanCancelCurrentAction()
    {
        return base.CanCancelCurrentAction() && (ThrowableData?.canCancelThrow ?? false);
    }

    #endregion

    #region Held Action Events

    /// <summary>
    /// Called when starting to aim the throwable
    /// </summary>
    protected override void OnHeldActionStarting(HeldActionType heldType, PlayerAnimationType actionType, InputContext context)
    {
        base.OnHeldActionStarting(heldType, actionType, context);

        if (heldType == HeldActionType.Secondary)
        {
            isAiming = true;
            DebugLog($"Starting to aim throwable: {currentItemData.itemName}");

            PlaySound(aimStartSound);
            OnAimingStarted?.Invoke();
        }
    }

    /// <summary>
    /// Called when aim is complete and ready to throw
    /// </summary>
    protected override void OnSecondaryActionReady()
    {
        base.OnSecondaryActionReady();
        isReadyToThrow = true;
        DebugLog("Throwable aim complete - ready to throw");
    }

    /// <summary>
    /// Called when throwable is thrown (held action executed)
    /// </summary>
    protected override void ExecuteSecondaryAction(PlayerAnimationType actionType)
    {
        base.ExecuteSecondaryAction(actionType);
        DebugLog("Executing throw");

        PlaySound(throwSound);
        ThrowItem();
        OnThrowableThrown?.Invoke();
    }

    /// <summary>
    /// Called when secondary action completes
    /// </summary>
    protected override void OnSecondaryActionCompleted(PlayerAnimationType actionType)
    {
        base.OnSecondaryActionCompleted(actionType);

        // Reset throwable state
        isAiming = false;
        isReadyToThrow = false;
        DebugLog("Throw completed - ready for new actions");
    }

    /// <summary>
    /// Called when throw is cancelled
    /// </summary>
    protected override void OnHeldActionCancelled(PlayerAnimationType actionType)
    {
        base.OnHeldActionCancelled(actionType);

        if (currentHeldActionType == HeldActionType.Secondary)
        {
            isAiming = false;
            isReadyToThrow = false;
            DebugLog($"Throw cancelled: {currentItemData.itemName}");

            PlaySound(cancelSound);
            OnAimingStopped?.Invoke();
        }
    }

    #endregion

    #region Throwing System

    /// <summary>
    /// Execute the throw
    /// </summary>
    private void ThrowItem()
    {
        if (ThrowableData == null)
        {
            DebugLog("Cannot throw - no throwable data");
            return;
        }

        DebugLog($"Throwing {currentItemData.itemName} - Damage: {ThrowableData.damage}");

        // TODO: Implement throwable projectile:
        // - Spawn throwable physics object
        // - Apply throw force and trajectory
        // - Set damage and explosion radius
        // - Handle different throwable types (grenade, molotov, etc.)

        // For now, simulate the throw
        Vector3 throwDirection = Camera.main?.transform.forward ?? Vector3.forward;
        DebugLog($"Throwable trajectory: {throwDirection}");

        // Remove item from inventory (throwables are consumed)
        RemoveItemFromInventory();
    }

    /// <summary>
    /// Cancel the current throw
    /// </summary>
    private void CancelThrow()
    {
        if (!CanCancelThrow())
        {
            DebugLog("Cannot cancel throw");
            return;
        }

        DebugLog("Cancelling throw");
        CancelCurrentAction();
    }

    /// <summary>
    /// Remove thrown item from inventory
    /// </summary>
    private void RemoveItemFromInventory()
    {
        if (inventoryManager == null || currentItemData == null)
        {
            DebugLog("Cannot remove item - missing manager or item data");
            return;
        }

        // Try to find the equipped item
        InventoryItemData inventoryItem = null;

        // First try to get it from the equipped item manager
        if (EquippedItemManager.Instance?.CurrentEquippedItem != null)
        {
            string equippedItemId = EquippedItemManager.Instance.CurrentEquippedItem.equippedItemId;
            if (!string.IsNullOrEmpty(equippedItemId))
            {
                inventoryItem = inventoryManager.InventoryGridData.GetItem(equippedItemId);
            }
        }

        // Fallback: search by item data
        if (inventoryItem == null)
        {
            var allItems = inventoryManager.InventoryGridData.GetAllItems();
            foreach (var item in allItems)
            {
                if (item.ItemData == currentItemData)
                {
                    inventoryItem = item;
                    break;
                }
            }
        }

        // Remove the item
        if (inventoryItem != null)
        {
            inventoryManager.RemoveItem(inventoryItem.ID);
            //DebugLog($"Removed thrown item: {currentItemData.itemName}");
        }
        else
        {
            Debug.LogWarning($"Could not find inventory item to remove: {currentItemData.itemName}");
        }
    }

    #endregion

    #region State Validation

    /// <summary>
    /// Check if can aim throwable
    /// </summary>
    private bool CanAimThrowable(PlayerStateType playerState)
    {
        if (ThrowableData == null)
        {
            DebugLog("Cannot aim - no throwable data");
            return false;
        }

        // Allow aiming during Starting and Looping states for held actions
        if (currentActionState != ActionState.None &&
            currentActionState != ActionState.Starting &&
            currentActionState != ActionState.Looping)
        {
            DebugLog($"Cannot aim - incompatible action state: {currentActionState}");
            return false;
        }

        if (!currentItemData.CanUseInState(playerState))
        {
            DebugLog($"Cannot aim - item cannot be used in state: {playerState}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if can cancel throw
    /// </summary>
    private bool CanCancelThrow()
    {
        if (ThrowableData?.canCancelThrow != true)
        {
            return false;
        }

        return isAiming && (currentActionState == ActionState.Starting || currentActionState == ActionState.Looping);
    }

    #endregion

    #region Utility

    /// <summary>
    /// Play audio clip
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    #endregion

    #region Public API

    /// <summary>Check if currently aiming</summary>
    public bool IsAiming() => isAiming;

    /// <summary>Check if ready to throw</summary>
    public bool IsReadyToThrow() => isReadyToThrow && currentActionState == ActionState.Looping;

    /// <summary>Check if can cancel current throw</summary>
    public bool CanCancelCurrentThrow() => CanCancelThrow();

    /// <summary>Get current throwable data</summary>
    public ThrowableData GetCurrentThrowableData() => ThrowableData;

    /// <summary>Get throwable damage</summary>
    public float GetThrowableDamage() => ThrowableData?.damage ?? 0f;

    /// <summary>Force stop all throwable actions</summary>
    public void ForceStopAllActions()
    {
        if (isAiming)
        {
            isAiming = false;
            isReadyToThrow = false;
            OnAimingStopped?.Invoke();
        }

        if (currentActionState != ActionState.None)
            ForceCompleteAction();

        DebugLog("Force stopped all throwable actions");
    }

    /// <summary>Debug info</summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Action State: {currentActionState} ({currentActionAnimation}), " +
               $"Held Action: {currentHeldActionType}, " +
               $"Is Aiming: {isAiming}, Ready to Throw: {isReadyToThrow}, " +
               $"Can Cancel: {ThrowableData?.canCancelThrow ?? false}";
    }

    #endregion
}