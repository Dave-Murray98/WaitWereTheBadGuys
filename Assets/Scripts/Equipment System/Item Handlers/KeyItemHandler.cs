using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Clean KeyItemHandler implementation from scratch.
/// Primary action: Melee attack (through unified system)
/// Secondary action: Use key item (instant)
/// Key items are special quest/progression items that cannot be dropped
/// </summary>
public class KeyItemHandler : BaseEquippedItemHandler
{
    [Header("Key Item Audio")]
    [SerializeField] private AudioClip useKeyItemSound;
    [SerializeField] private AudioClip useCompleteSound;

    [Header("Key Item State")]
    [SerializeField, ReadOnly] private bool isUsingKeyItem = false;

    // Components
    private AudioSource audioSource;

    // Quick access to key item data
    private KeyItemData KeyItemData => currentItemData?.KeyItemData;

    // Events
    public System.Action<ItemData> OnKeyItemEquipped;
    public System.Action OnKeyItemUnequipped;
    public System.Action OnKeyItemUsed;

    public override ItemType HandledItemType => ItemType.KeyItem;

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

    #endregion

    #region Base Handler Implementation

    protected override void OnItemEquippedInternal(ItemData itemData)
    {
        if (itemData?.itemType != ItemType.KeyItem)
        {
            Debug.LogError($"KeyItemHandler received invalid item: {itemData?.itemName ?? "null"}");
            return;
        }

        // Reset key item state
        isUsingKeyItem = false;

        DebugLog($"Equipped key item: {itemData.itemName}");
        OnKeyItemEquipped?.Invoke(itemData);
    }

    protected override void OnItemUnequippedInternal()
    {
        base.OnItemUnequippedInternal();

        // Reset key item state
        isUsingKeyItem = false;

        DebugLog("Unequipped key item");
        OnKeyItemUnequipped?.Invoke();
    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // Primary = Melee attack (using key item as improvised weapon)
        if (context.isPressed)
        {
            HandleMeleeAction(context);
        }
    }

    protected override void HandleSecondaryActionInternal(InputContext context)
    {
        // Secondary = Use key item
        if (context.isPressed)
        {
            UseKeyItem();
        }
    }

    protected override void HandleReloadActionInternal(InputContext context)
    {
        // Not applicable for key items
        DebugLog("Reload not applicable for key items");
    }

    protected override bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                // Can melee if not using key item
                return !isUsingKeyItem && currentActionState == ActionState.None;

            case PlayerAnimationType.SecondaryAction:
                return CanUseKeyItem(playerState);

            default:
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        // Nothing specific needed for key items
    }

    #endregion

    #region Unified System Configuration

    /// <summary>
    /// Primary action is instant (melee with key item)
    /// </summary>
    protected override bool ShouldPrimaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Secondary action is instant (use key item)
    /// </summary>
    protected override bool ShouldSecondaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Override melee damage for key item (much weaker than weapons)
    /// </summary>
    protected override float GetMeleeDamage() => meleeDamage * 0.5f; // 50% of base damage

    #endregion

    #region Key Item Usage System

    /// <summary>
    /// Use the key item
    /// </summary>
    private void UseKeyItem()
    {
        if (!CanUseKeyItem(GetCurrentPlayerState()))
        {
            DebugLog("Cannot use key item");
            return;
        }

        isUsingKeyItem = true;
        DebugLog($"Using key item: {currentItemData.itemName}");

        // Play use sound
        PlaySound(useKeyItemSound);

        // Trigger key item animation
        TriggerInstantAction(PlayerAnimationType.SecondaryAction);
    }

    /// <summary>
    /// Complete consumption (called by animation completion)
    /// </summary>
    protected override void OnActionCompletedInternal(PlayerAnimationType actionType)
    {
        base.OnActionCompletedInternal(actionType);

        switch (actionType)
        {
            case PlayerAnimationType.SecondaryAction:
                CompleteKeyItemUse();
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
    /// Complete key item use and apply effects
    /// </summary>
    private void CompleteKeyItemUse()
    {
        if (!isUsingKeyItem || KeyItemData == null)
        {
            DebugLog("CompleteKeyItemUse called but not using key item or no data");
            return;
        }

        DebugLog($"Key item use completed: {currentItemData.itemName}");

        // Apply key item effects
        ApplyKeyItemEffects();

        // Play completion sound
        PlaySound(useCompleteSound);

        // Fire events
        OnKeyItemUsed?.Invoke();

        // Reset state
        isUsingKeyItem = false;
    }

    /// <summary>
    /// Apply key item effects
    /// </summary>
    private void ApplyKeyItemEffects()
    {
        if (KeyItemData == null) return;

        DebugLog($"Applying key item effects for: {currentItemData.itemName}");

        // TODO: Implement specific key item effects based on key item type:
        // Examples:
        // - Keys: unlock specific doors, chests, or containers
        // - Keycards: open electronic doors or access terminals
        // - ID Cards: gain access to restricted areas
        // - Codes/Passwords: activate computers or security systems
        // - Maps: reveal areas on the world map
        // - Documents: provide story information or quest progression
        // - Artifacts: trigger story events or cutscenes
        // - Tools: enable specific gameplay mechanics
        // - Tokens: represent quest progress or achievements

        // For now, just log the effect
        DebugLog($"Key item effect applied: {currentItemData.itemName}");

        // TODO: Add key item-specific logic here when quest/progression system is developed
        // This could involve:
        // - Checking for nearby interactable objects that require this key item
        // - Triggering quest events or story progression
        // - Unlocking new areas or gameplay features
        // - Providing information to the player (journal entries, map data)
        // - Activating or deactivating world objects
        // - Sending events to the quest system or story manager
    }

    #endregion

    #region State Validation

    /// <summary>
    /// Check if can use key item in current state
    /// </summary>
    private bool CanUseKeyItem(PlayerStateType playerState)
    {
        if (KeyItemData == null)
        {
            DebugLog("Cannot use key item - no key item data");
            return false;
        }

        if (isUsingKeyItem)
        {
            DebugLog("Cannot use key item - already using");
            return false;
        }

        if (currentActionState != ActionState.None)
        {
            DebugLog("Cannot use key item - already performing action");
            return false;
        }

        if (!currentItemData.CanUseInState(playerState))
        {
            DebugLog($"Cannot use key item - item cannot be used in state: {playerState}");
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

    /// <summary>Check if currently using key item</summary>
    public bool IsUsingKeyItem() => isUsingKeyItem;

    /// <summary>Get current key item data</summary>
    public KeyItemData GetCurrentKeyItemData() => KeyItemData;

    /// <summary>Check if key item is quest-related</summary>
    public bool IsQuestItem() => KeyItemData != null; // All key items are considered quest items

    /// <summary>Debug info</summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Action State: {currentActionState} ({currentActionAnimation}), " +
               $"Using Key Item: {isUsingKeyItem}";
    }

    #endregion
}