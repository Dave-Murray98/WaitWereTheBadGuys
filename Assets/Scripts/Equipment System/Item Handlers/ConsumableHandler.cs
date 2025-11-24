using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: ConsumableHandler implementation using enum-based animation system.
/// Primary action: Melee attack (through unified system)
/// Secondary action: Consume item (instant)
/// Removes consumed items from inventory automatically
/// </summary>
public class ConsumableHandler : BaseEquippedItemHandler
{
    [Header("Consumable Audio")]
    [SerializeField] private AudioClip consumeStartSound;
    [SerializeField] private AudioClip consumeCompleteSound;

    [Header("Consumable State")]
    [SerializeField, ReadOnly] private bool isConsuming = false;

    // Components
    private AudioSource audioSource;
    private PlayerInventoryManager inventoryManager;

    // Quick access to consumable data
    private ConsumableData ConsumableData => currentItemData?.ConsumableData;

    // Events
    public System.Action<ItemData> OnConsumableEquipped;
    public System.Action OnConsumableUnequipped;
    public System.Action<float> OnHealthRestored; // amount restored

    public override ItemType HandledItemType => ItemType.Consumable;

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
        if (itemData?.itemType != ItemType.Consumable)
        {
            Debug.LogError($"ConsumableHandler received invalid item: {itemData?.itemName ?? "null"}");
            return;
        }

        // Reset consumable state
        isConsuming = false;

        DebugLog($"Equipped consumable: {itemData.itemName}");
        OnConsumableEquipped?.Invoke(itemData);
    }

    /// <summary>
    /// FIXED: Override OnItemUnequippedInternal to clean up consumption state
    /// </summary>
    protected override void OnItemUnequippedInternal()
    {
        DebugLog($"OnItemUnequippedInternal called - isConsuming: {isConsuming}");

        // CRITICAL FIX: Reset consumption state when item is unequipped
        if (isConsuming)
        {
            DebugLog("Item unequipped during consumption - resetting consumption state");
            isConsuming = false;
        }

        base.OnItemUnequippedInternal();
    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // Primary = Melee attack (using consumable as improvised weapon)
        if (context.isPressed)
        {
            HandleMeleeAction(context);
        }
    }

    protected override void HandleSecondaryActionInternal(InputContext context)
    {
        // Secondary = Consume item
        if (context.isPressed)
        {
            ConsumeItem();
        }
    }

    protected override void HandleReloadActionInternal(InputContext context)
    {
        // Not applicable for consumables
        DebugLog("Reload not applicable for consumables");
    }

    protected override bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                // Can melee if not consuming
                return !isConsuming && currentActionState == ActionState.None;

            case PlayerAnimationType.SecondaryAction:
                return CanConsume(playerState);

            default:
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        // Nothing specific needed for consumables
    }

    #endregion

    #region Unified System Configuration

    /// <summary>
    /// Primary action is instant (melee with consumable)
    /// </summary>
    protected override bool ShouldPrimaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Secondary action is instant (consume)
    /// </summary>
    protected override bool ShouldSecondaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Override melee damage for consumable (weaker than dedicated weapons)
    /// </summary>
    protected override float GetMeleeDamage() => meleeDamage * 0.7f; // 70% of base damage

    #endregion

    #region Consumption System

    /// <summary>
    /// Consume the item
    /// </summary>
    private void ConsumeItem()
    {
        if (!CanConsume(GetCurrentPlayerState()))
        {
            DebugLog("Cannot consume item");
            return;
        }

        isConsuming = true;
        DebugLog($"Starting consumption: {currentItemData.itemName}");

        // Play consume start sound
        PlaySound(consumeStartSound);

        // Trigger consume animation
        TriggerInstantAction(PlayerAnimationType.SecondaryAction);
    }

    /// <summary>
    /// FIXED: Override OnActionCompletedInternal with consumption state protection
    /// </summary>
    protected override void OnActionCompletedInternal(PlayerAnimationType actionType)
    {
        DebugLog($"OnActionCompletedInternal called for: {actionType}, isConsuming: {isConsuming}");

        // Call base implementation first
        base.OnActionCompletedInternal(actionType);

        switch (actionType)
        {
            case PlayerAnimationType.SecondaryAction:
                // CRITICAL FIX: Only complete consumption if we're actually in consuming state
                if (isConsuming)
                {
                    CompleteConsumption();
                }
                else
                {
                    DebugLog("SecondaryAction completed but not in consuming state - ignoring");
                }
                break;

            case PlayerAnimationType.MeleeAction:
            case PlayerAnimationType.PrimaryAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                // Melee attack with consumable completed
                DebugLog("Melee attack with consumable completed");
                break;
        }
    }

    /// <summary>
    /// FIXED: Complete consumption with guard against double execution
    /// </summary>
    private void CompleteConsumption()
    {
        if (!isConsuming || ConsumableData == null)
        {
            DebugLog("CompleteConsumption called but not consuming or no data");
            return;
        }

        // CRITICAL FIX: Prevent double completion during handler switching
        if (currentActionState == ActionState.None)
        {
            DebugLog("CompleteConsumption called but action state is None - likely duplicate call during handler switching");
            return;
        }

        DebugLog($"Consumption completed: {currentItemData.itemName}");

        // CRITICAL FIX: Set consuming to false IMMEDIATELY to prevent re-entry
        isConsuming = false;

        // Apply consumable effects
        ApplyConsumableEffects();

        // Play completion sound
        PlaySound(consumeCompleteSound);

        // Remove item from inventory
        RemoveItemFromInventory();
    }


    /// <summary>
    /// Apply consumable effects to player
    /// </summary>
    private void ApplyConsumableEffects()
    {
        if (ConsumableData == null) return;

        float healthRestore = ConsumableData.healthRestore;
        DebugLog($"Applying effects: Health +{healthRestore}");

        // Apply health restoration
        if (healthRestore > 0 && GameManager.Instance?.playerManager != null)
        {
            GameManager.Instance.playerManager.ModifyHealth(healthRestore);
            OnHealthRestored?.Invoke(healthRestore);
        }

        // TODO: Add other consumable effects:
        // - Hunger restoration
        // - Thirst restoration
        // - Status effects (buffs/debuffs)
        // - Temporary stat boosts
    }

    /// <summary>
    /// Remove consumed item from inventory
    /// </summary>
    private void RemoveItemFromInventory()
    {
        DebugLog($"Removing {currentItemData.itemName} from inventory");

        if (inventoryManager == null || currentItemData == null)
        {
            DebugLog("Cannot remove item - missing manager or item data");
            return;
        }

        // Try to find the equipped item
        InventoryItemData inventoryItem = null;

        // Try to get it from the equipped item manager
        if (EquippedItemManager.Instance?.CurrentEquippedItem != null)
        {
            string equippedItemId = EquippedItemManager.Instance.CurrentEquippedItem.equippedItemId;
            if (!string.IsNullOrEmpty(equippedItemId))
            {
                inventoryItem = inventoryManager.InventoryGridData.GetItem(equippedItemId);
            }
            else
            {
                DebugLog("Equipped item ID is null or empty");
            }
        }

        // Remove the item
        if (inventoryItem != null)
        {
            inventoryManager.RemoveItem(inventoryItem.ID);
            //            DebugLog($"Removed consumed item: {currentItemData.itemName}");
        }
        else
        {
            Debug.LogWarning($"Could not find inventory item to remove: {currentItemData.itemName}");
        }
    }

    #endregion

    #region State Validation

    /// <summary>
    /// Check if can consume in current state
    /// </summary>
    private bool CanConsume(PlayerStateType playerState)
    {
        if (ConsumableData == null)
        {
            DebugLog("Cannot consume - no consumable data");
            return false;
        }

        if (isConsuming)
        {
            DebugLog("Cannot consume - already consuming");
            return false;
        }

        if (currentActionState != ActionState.None)
        {
            DebugLog("Cannot consume - already performing action");
            return false;
        }

        if (!currentItemData.CanUseInState(playerState))
        {
            DebugLog($"Cannot consume - item cannot be used in state: {playerState}");
            return false;
        }

        return true;
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

    /// <summary>Check if currently consuming</summary>
    public bool IsConsuming() => isConsuming;

    /// <summary>Get current consumable data</summary>
    public ConsumableData GetCurrentConsumableData() => ConsumableData;

    /// <summary>Get health restore amount</summary>
    public float GetHealthRestoreAmount() => ConsumableData?.healthRestore ?? 0f;

    /// <summary>Debug info</summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Action State: {currentActionState} ({currentActionAnimation}), " +
               $"Is Consuming: {isConsuming}, " +
               $"Health Restore: {GetHealthRestoreAmount()}";
    }

    /// <summary>
    /// FIXED: Override ForceCompleteAction to prevent consumption during handler switches
    /// </summary>
    protected override void ForceCompleteAction()
    {
        DebugLog($"ForceCompleteAction called - isConsuming: {isConsuming}, actionState: {currentActionState}");

        // If we're in the middle of consuming, just reset the action state without consuming again
        if (isConsuming && currentActionState != ActionState.None)
        {
            DebugLog("Forcing completion during consumption - resetting state without re-consuming");
            isConsuming = false; // Reset consumption state
            ResetActionState(); // Reset action state
            return;
        }

        // For other actions, use normal force completion
        base.ForceCompleteAction();
    }

    #endregion
}