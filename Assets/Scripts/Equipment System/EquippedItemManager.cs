using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// FIXED: Equipment manager with proper save/load restoration
/// 
/// KEY FIXES:
/// - Saves and restores current active slot correctly
/// - Properly activates equipped item visual after load
/// - Enhanced state tracking for save/load operations
/// - Better coordination with visual manager during restoration
/// </summary>
public class EquippedItemManager : MonoBehaviour
{
    public static EquippedItemManager Instance { get; private set; }

    [Header("Equipment Configuration")]
    [SerializeField] private bool enableStateRestrictions = true;
    [SerializeField] private bool showRestrictionFeedback = true;
    [SerializeField] private float scrollCooldown = 0.1f;

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip equipSound;
    [SerializeField] private AudioClip hotkeySound;
    [SerializeField] private AudioClip restrictedSound;

    [Header("Debug Settings")]
    public bool enableDebugLogs = false;

    // FIXED: Enhanced state tracking for save/load
    [SerializeField, ReadOnly] private EquipmentSaveData equipmentData;
    [SerializeField, ReadOnly] private int currentActiveSlot = 1; // Which slot is currently selected (1-10)
    [SerializeField, ReadOnly] private bool isCurrentSlotUsable = false; // Whether current slot's item is usable
    [SerializeField, ReadOnly] private bool hasEquippedItem = false; // Whether we actually have a usable item equipped

    // System references
    private PlayerInventoryManager inventoryManager;
    private PlayerStateManager playerStateManager;
    [Header("Visual System")]
    [SerializeField] private EquippedItemVisualManager visualManager;
    [SerializeField] private bool autoFindVisualManager = true;

    // Input control
    private float lastScrollTime = 0f;

    #region Events for UI Integration

    // Equipment state events
    public System.Action<EquippedItemData> OnItemEquipped;
    public System.Action OnItemUnequipped;
    public System.Action OnUnarmedActivated;

    // Slot selection events
    public System.Action<int, HotkeyBinding, bool> OnSlotSelected; // (slotNumber, binding, isUsable)
    public System.Action<int, HotkeyBinding> OnHotkeyAssigned;
    public System.Action<int> OnHotkeyCleared;

    // State restriction feedback events
    public System.Action<string, string> OnEquipmentRestricted;
    public System.Action<string> OnStateRestrictionMessage;

    // Action events
    public System.Action<ItemType, bool> OnItemActionPerformed;

    #endregion

    #region Public Properties

    /// <summary>Current equipped item data</summary>
    public EquippedItemData CurrentEquippedItem => equipmentData.equippedItem;

    /// <summary>Whether player has a usable item equipped (not unarmed)</summary>
    public bool HasEquippedItem => hasEquippedItem;

    /// <summary>Current active slot number (1-10)</summary>
    public int GetCurrentActiveSlot() => currentActiveSlot;

    /// <summary>Whether player is effectively unarmed (no usable item)</summary>
    public bool IsUnarmed => !hasEquippedItem;

    /// <summary>Get the ItemData of the currently equipped item (null if unarmed/restricted)</summary>
    public ItemData GetEquippedItemData() => hasEquippedItem ? equipmentData.equippedItem.GetItemData() : null;

    #endregion

    #region Initialization

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeEquipmentSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        FindSystemReferences();
        SetupEventSubscriptions();
        SetupInputHandling();

        DebugLog("EquippedItemManager initialized successfully");
    }

    private void InitializeEquipmentSystem()
    {
        equipmentData = new EquipmentSaveData();
        currentActiveSlot = 1; // Start with slot 1 selected
        isCurrentSlotUsable = false;
        hasEquippedItem = false;
        DebugLog("Equipment system initialized with slot 1 selected");
    }

    private void FindSystemReferences()
    {
        inventoryManager = PlayerInventoryManager.Instance;
        playerStateManager = PlayerStateManager.Instance ?? FindFirstObjectByType<PlayerStateManager>();

        if (inventoryManager == null)
            Debug.LogError("[EquippedItemManager] InventoryManager not found!");

        if (playerStateManager == null)
        {
            Debug.LogError("[EquippedItemManager] PlayerStateManager not found! State restrictions disabled.");
            enableStateRestrictions = false;
        }

        if (visualManager == null && autoFindVisualManager)
        {
            visualManager = FindFirstObjectByType<EquippedItemVisualManager>();
        }

        DebugLog($"System references - Inventory: {inventoryManager != null}, StateManager: {playerStateManager != null}, VisualManager: {visualManager != null}");
    }

    private void SetupEventSubscriptions()
    {
        if (GameManager.Instance != null)
            GameManager.OnManagersRefreshed += FindSystemReferences;

        if (InputManager.Instance != null)
            InputManager.OnInputManagerReady += OnInputManagerReady;

        if (inventoryManager != null)
        {
            inventoryManager.OnItemRemoved += OnInventoryItemRemoved;
            inventoryManager.OnItemAdded += OnInventoryItemAdded;
        }

        if (playerStateManager != null)
            playerStateManager.OnStateChanged += OnPlayerStateChanged;
    }

    private void SetupInputHandling()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnScrollWheelInput += HandleScrollInput;
            InputManager.Instance.OnHotkeyPressed += OnHotkeyPressed;
        }
    }

    private void OnInputManagerReady(InputManager inputManager) => SetupInputHandling();

    #endregion

    #region Input Handling

    private void Update()
    {
        HandleItemActions();

        // Fallback input handling
        if (InputManager.Instance == null)
            HandleFallbackHotkeyInput();
    }

    private void HandleScrollInput(Vector2 scrollDelta)
    {
        if (Time.time - lastScrollTime < scrollCooldown) return;
        if (GameManager.Instance?.uiManager?.isInventoryOpen == true) return;
        if (Mathf.Abs(scrollDelta.y) <= 0.1f) return;

        lastScrollTime = Time.time;
        CycleToNextSlot(scrollDelta.y > 0);
    }

    private void OnHotkeyPressed(int slotNumber) => SelectSlot(slotNumber);

    private void HandleFallbackHotkeyInput()
    {
        for (int i = 1; i <= 10; i++)
        {
            KeyCode key = i == 10 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + (i - 1));
            if (Input.GetKeyDown(key))
                SelectSlot(i);
        }
    }

    private void HandleItemActions()
    {
        if (GameManager.Instance?.uiManager?.isInventoryOpen == true) return;

        if (Input.GetMouseButtonDown(0)) PerformItemAction(true);  // Left click
        if (Input.GetMouseButtonDown(1)) PerformItemAction(false); // Right click
    }

    #endregion

    #region Core Slot Management

    /// <summary>
    /// Select a specific slot and handle equipment logic
    /// </summary>
    public void SelectSlot(int slotNumber)
    {
        if (!IsValidSlotNumber(slotNumber)) return;

        DebugLog($"Selecting slot {slotNumber}");

        // Update current active slot
        currentActiveSlot = slotNumber;

        // Get the binding for this slot
        var binding = equipmentData.GetHotkeyBinding(slotNumber);
        if (binding == null) return;

        // Determine if this slot has an item and if it's usable
        bool slotHasItem = false;
        bool slotIsUsable = false;
        ItemData itemData = null;

        if (binding.isAssigned)
        {
            // Validate item still exists in inventory
            if (inventoryManager != null)
            {
                var inventoryItem = inventoryManager.InventoryGridData.GetItem(binding.itemId);
                if (inventoryItem?.ItemData != null)
                {
                    itemData = inventoryItem.ItemData;
                    slotHasItem = true;

                    // Check if item can be used in current state
                    slotIsUsable = !enableStateRestrictions || CanEquipItemInCurrentState(itemData);
                }
                else
                {
                    // Item no longer exists - clean up binding
                    DebugLog($"Item in slot {slotNumber} no longer exists - clearing binding");
                    binding.ClearSlot();
                    OnHotkeyCleared?.Invoke(slotNumber);
                }
            }
        }

        // Update internal state properly
        isCurrentSlotUsable = slotIsUsable;
        hasEquippedItem = slotIsUsable && itemData != null;

        // FIXED: Update save data state tracking
        equipmentData.UpdateCurrentState(currentActiveSlot, hasEquippedItem);

        // Update equipped item data and visuals based on usability
        if (slotHasItem && slotIsUsable && itemData != null)
        {
            // Item exists and is usable - equip it
            equipmentData.equippedItem.EquipFromHotkey(binding.itemId, itemData, slotNumber);

            // Update visual system
            if (visualManager != null)
            {
                visualManager.EquipHotbarSlot(slotNumber, itemData);
            }

            DebugLog($"Equipped {itemData.itemName} from slot {slotNumber} (USABLE)");
            OnItemEquipped?.Invoke(equipmentData.equippedItem);
        }
        else
        {
            // Either no item, or item exists but is restricted - go unarmed
            equipmentData.equippedItem.Clear();

            // Update visual system to show unarmed
            if (visualManager != null)
            {
                visualManager.UnequipCurrentItem();
            }

            if (slotHasItem && !slotIsUsable && itemData != null)
            {
                DebugLog($"Slot {slotNumber} selected but acting as unarmed - item {itemData.itemName} is RESTRICTED");
                ShowRestrictionFeedback(itemData, "Cannot use in current state");
            }
            else
            {
                DebugLog($"Slot {slotNumber} selected but acting as unarmed (empty slot)");
            }

            OnItemUnequipped?.Invoke();
            OnUnarmedActivated?.Invoke();
        }

        // Fire slot selection event with correct usability info
        OnSlotSelected?.Invoke(slotNumber, binding, slotIsUsable);

        // Play appropriate sound
        if (hasEquippedItem)
        {
            PlayHotkeySound();
        }
        else if (slotHasItem && !slotIsUsable)
        {
            PlayRestrictedSound();
        }
        else
        {
            PlayHotkeySound(); // Still play sound for empty slot selection
        }
    }

    /// <summary>
    /// Cycle to the next slot in sequence (handles gaps properly)
    /// </summary>
    public void CycleToNextSlot(bool forward)
    {
        int nextSlot;

        if (forward)
        {
            nextSlot = currentActiveSlot + 1;
            if (nextSlot > 10) nextSlot = 1; // Wrap around
        }
        else
        {
            nextSlot = currentActiveSlot - 1;
            if (nextSlot < 1) nextSlot = 10; // Wrap around
        }

        DebugLog($"Cycling from slot {currentActiveSlot} to slot {nextSlot}");
        SelectSlot(nextSlot);
    }

    #endregion

    #region Equipment Management

    /// <summary>
    /// Assigns an item to a specific hotkey slot
    /// </summary>
    public bool AssignItemToHotkey(string itemId, int slotNumber)
    {
        if (!IsValidSlotNumber(slotNumber)) return false;
        if (!ValidateInventoryManager()) return false;

        var inventoryItem = inventoryManager.InventoryGridData.GetItem(itemId);
        if (inventoryItem?.ItemData == null)
        {
            DebugLog($"Cannot assign hotkey - item {itemId} not found");
            return false;
        }

        var binding = equipmentData.GetHotkeyBinding(slotNumber);
        if (binding == null) return false;

        binding.AssignItem(itemId, inventoryItem.ItemData.name);
        OnHotkeyAssigned?.Invoke(slotNumber, binding);

        // Add visual object for this slot if item has equipped prefab
        if (visualManager != null)
        {
            visualManager.AddHotbarSlotObject(slotNumber, inventoryItem.ItemData);
        }

        // If this is the current active slot, refresh selection
        if (slotNumber == currentActiveSlot)
        {
            SelectSlot(slotNumber);
        }

        DebugLog($"Assigned {inventoryItem.ItemData.itemName} to hotkey {slotNumber}");
        return true;
    }

    /// <summary>
    /// Unequips current item and returns to unarmed state
    /// </summary>
    public void UnequipCurrentItem()
    {
        if (!hasEquippedItem) return;

        string itemName = equipmentData.equippedItem.GetItemData()?.itemName ?? "Unknown";
        equipmentData.equippedItem.Clear();
        hasEquippedItem = false;

        // FIXED: Update save data state tracking
        equipmentData.UpdateCurrentState(currentActiveSlot, false);

        OnItemUnequipped?.Invoke();
        OnUnarmedActivated?.Invoke();

        // Fire slot selection event to update UI
        var binding = equipmentData.GetHotkeyBinding(currentActiveSlot);
        OnSlotSelected?.Invoke(currentActiveSlot, binding, false);

        DebugLog($"Unequipped {itemName}, slot {currentActiveSlot} now unarmed");
    }

    /// <summary>
    /// Called when hotbar is initially set up or completely rebuilt
    /// FIXED: Now properly builds the hotbar items dictionary and populates visuals
    /// </summary>
    public void RefreshAllEquippedItemPrefabs()
    {
        if (visualManager == null)
        {
            visualManager = FindFirstObjectByType<EquippedItemVisualManager>();
            if (visualManager == null) return;
        }

        DebugLog("Refreshing all hotbar visuals and animations");

        // FIXED: Actually build the dictionary of current hotbar items that have equipped prefabs
        Dictionary<int, ItemData> hotbarItems = new Dictionary<int, ItemData>();

        foreach (HotkeyBinding binding in equipmentData.hotkeyBindings)
        {
            if (binding.isAssigned)
            {
                ItemData itemData = binding.GetCurrentItemData();
                if (itemData != null && itemData.equippedItemPrefab != null) // Only include items with prefabs
                {
                    hotbarItems[binding.slotNumber] = itemData;
                    DebugLog($"Added to hotbar items: Slot {binding.slotNumber} = {itemData.itemName}");
                }
                else if (itemData != null)
                {
                    DebugLog($"Slot {binding.slotNumber} has item {itemData.itemName} but no equipped prefab");
                }
            }
        }

        // FIXED: Actually call PopulateHotbarEquippedItemPrefabs with the built dictionary
        if (visualManager != null)
        {
            visualManager.PopulateHotbarEquippedItemPrefabs(hotbarItems);
            DebugLog($"Populated hotbar visuals: {hotbarItems.Count} items with equipped prefabs");
        }
        else
        {
            DebugLog("Visual manager is null - cannot populate hotbar visuals");
        }
    }


    #endregion

    #region Item Actions

    /// <summary>
    /// Performs an action with the equipped item or unarmed combat
    /// </summary>
    public void PerformItemAction(bool isLeftClick)
    {
        // Handle unarmed actions (either empty slot or restricted item)
        if (!hasEquippedItem)
        {
            if (isLeftClick) PerformPunch();
            return; // Right-click does nothing when unarmed
        }

        var itemData = GetEquippedItemData();
        if (itemData == null) return;

        // Double-check state restrictions before action
        if (!CanEquipItemInCurrentState(itemData))
        {
            DebugLog($"Action blocked - {itemData.itemName} not valid for current state");
            ShowRestrictionFeedback(itemData, "Cannot use in current state");

            if (isLeftClick) PerformPunch(); // Fallback to punch
            return;
        }

        // Route to appropriate action handler based on item type
        switch (itemData.itemType)
        {
            case ItemType.RangedWeapon:
                if (isLeftClick) PerformWeaponAttack(itemData);
                else PerformWeaponAim(itemData);
                break;

            case ItemType.Consumable:
                if (isLeftClick) PerformPunch();
                else PerformConsumeItem(itemData);
                break;

            case ItemType.Tool:
                if (isLeftClick) PerformPunch();
                else PerformUseEquipment(itemData);
                break;

            case ItemType.KeyItem:
                if (isLeftClick) PerformPunch();
                else PerformUseKeyItem(itemData);
                break;

            case ItemType.Clothing:
                if (isLeftClick) PerformPunch();
                else PerformUseClothing(itemData);
                break;

            case ItemType.Ammo:
                if (isLeftClick) PerformPunch();
                break;
        }

        OnItemActionPerformed?.Invoke(itemData.itemType, isLeftClick);
    }

    // Action implementations
    private void PerformPunch() => DebugLog("Performing unarmed attack");
    private void PerformWeaponAttack(ItemData weapon) => DebugLog($"Attacking with {weapon.itemName}");
    private void PerformWeaponAim(ItemData weapon) => DebugLog($"Aiming {weapon.itemName}");
    private void PerformConsumeItem(ItemData consumable) => DebugLog($"Consuming {consumable.itemName}");
    private void PerformUseEquipment(ItemData equipment) => DebugLog($"Using equipment {equipment.itemName}");
    private void PerformUseKeyItem(ItemData keyItem) => DebugLog($"Using key item {keyItem.itemName}");
    private void PerformUseClothing(ItemData clothing) => DebugLog($"Using clothing {clothing.itemName}");

    #endregion

    #region State Management

    /// <summary>
    /// Checks if an item can be equipped in the current player state
    /// </summary>
    private bool CanEquipItemInCurrentState(ItemData itemData)
    {
        if (!enableStateRestrictions || playerStateManager == null || itemData == null)
            return true;

        return playerStateManager.CanEquipItem(itemData);
    }

    /// <summary>
    /// Shows restriction feedback to the player
    /// </summary>
    private void ShowRestrictionFeedback(ItemData itemData, string context)
    {
        if (!showRestrictionFeedback || itemData == null) return;

        string reason = GetRestrictionReason(itemData);
        string currentState = playerStateManager?.GetCurrentStateDisplayName() ?? "current state";

        OnEquipmentRestricted?.Invoke(itemData.itemName, reason);
        OnStateRestrictionMessage?.Invoke($"{context} in {currentState}");
        PlayRestrictedSound();
    }

    /// <summary>
    /// Gets a user-friendly reason for why an item is restricted
    /// </summary>
    private string GetRestrictionReason(ItemData itemData)
    {
        if (itemData == null) return "Unknown restriction";

        var usableStates = itemData.GetUsableStates();
        if (usableStates.Length == 0) return "This item cannot be used anywhere";

        var stateNames = new List<string>();
        foreach (var state in usableStates)
        {
            stateNames.Add(state switch
            {
                PlayerStateType.Ground => "on land",
                PlayerStateType.Water => "in water",
                PlayerStateType.Vehicle => "in vehicles",
                _ => state.ToString().ToLower()
            });
        }

        return $"Only usable {string.Join(" or ", stateNames)}";
    }

    /// <summary>
    /// Handles player state changes by validating current equipment
    /// </summary>
    private void OnPlayerStateChanged(PlayerStateType previousState, PlayerStateType newState)
    {
        DebugLog($"Player state changed: {previousState} -> {newState}");

        // Re-validate current slot
        SelectSlot(currentActiveSlot);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handle inventory item removal with proper stackable item support
    /// </summary>
    private void OnInventoryItemRemoved(string itemId)
    {

        //We'll use this to check if the item has no more stacks in the inventory and thus whether to remove the visual object
        bool slotStillHasItems = false;

        // Clean up hotkey assignments with improved stackable item logic
        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.RemoveItem(itemId))
            {
                // Check if this hotkey slot still has items assigned after removal
                slotStillHasItems = binding.isAssigned;

                // Only remove visual object if the slot is now completely empty
                if (!slotStillHasItems && visualManager != null)
                {
                    DebugLog($"Slot {binding.slotNumber} is now empty - removing visual object");
                    visualManager.RemoveHotbarSlotObject(binding.slotNumber);
                    OnHotkeyCleared?.Invoke(binding.slotNumber);
                }
                else if (slotStillHasItems)
                {
                    DebugLog($"Slot {binding.slotNumber} still has items - keeping visual object");
                    OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);
                }

                // If this affects the current slot, refresh selection
                if (binding.slotNumber == currentActiveSlot)
                {
                    SelectSlot(currentActiveSlot);
                }
            }
        }

        // Handle equipped item removal
        if (equipmentData.equippedItem.IsEquipped(itemId))
        {
            DebugLog($"Equipped item {itemId} removed from inventory - switching to unarmed");
            hasEquippedItem = false;
            equipmentData.equippedItem.Clear();
            equipmentData.UpdateCurrentState(currentActiveSlot, false);

            // Update visual
            if (visualManager != null)
            {
                // If the slot still has items, keep the visual; otherwise, unequip
                if (!slotStillHasItems)
                    visualManager.UnequipCurrentItem();
            }

            OnItemUnequipped?.Invoke();
            OnUnarmedActivated?.Invoke();
        }
    }

    private void OnInventoryItemAdded(InventoryItemData newItem)
    {
        if (newItem?.ItemData?.itemType != ItemType.Consumable) return;

        // Auto-stack consumables in existing hotkey slots
        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.TryAddToStack(newItem.ID, newItem.ItemData.name))
            {
                OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);

                // Update visual object
                if (visualManager != null)
                {
                    visualManager.UpdateHotbarSlotObject(binding.slotNumber, newItem.ItemData);
                }

                // If this affects the current slot, refresh selection
                if (binding.slotNumber == currentActiveSlot)
                {
                    SelectSlot(currentActiveSlot);
                }

                DebugLog($"Added {newItem.ItemData.itemName} to hotkey {binding.slotNumber} stack");
                break;
            }
        }
    }

    #endregion

    #region Public API

    /// <summary>Gets hotkey binding for a specific slot</summary>
    public HotkeyBinding GetHotkeyBinding(int slotNumber) => equipmentData.GetHotkeyBinding(slotNumber);

    /// <summary>Gets all hotkey bindings</summary>
    public List<HotkeyBinding> GetAllHotkeyBindings() => equipmentData.hotkeyBindings;

    /// <summary>Gets all hotkey bindings with their current validity status</summary>
    public List<(HotkeyBinding binding, bool isUsable)> GetAllHotkeyBindingsWithValidity()
    {
        var result = new List<(HotkeyBinding, bool)>();

        foreach (var binding in equipmentData.hotkeyBindings)
        {
            bool isUsable = true;

            if (binding.isAssigned && inventoryManager != null)
            {
                var item = inventoryManager.InventoryGridData.GetItem(binding.itemId);
                if (item?.ItemData != null)
                    isUsable = CanEquipItemInCurrentState(item.ItemData);
            }

            result.Add((binding, isUsable));
        }

        return result;
    }

    /// <summary>Gets current slot info for UI</summary>
    public (int slotNumber, bool hasItem, bool isUsable) GetCurrentSlotInfo()
    {
        var binding = equipmentData.GetHotkeyBinding(currentActiveSlot);
        return (currentActiveSlot, binding?.isAssigned ?? false, isCurrentSlotUsable);
    }

    /// <summary>Checks if current equipment is valid for current state</summary>
    public bool IsCurrentEquipmentValid()
    {
        return hasEquippedItem;
    }

    /// <summary>Forces validation of current equipment</summary>
    public void ValidateEquipmentForCurrentState()
    {
        SelectSlot(currentActiveSlot); // Re-validate current slot
    }

    #endregion

    #region FIXED: Save System Integration

    /// <summary>
    /// FIXED: Sets equipment data for save/load operations with proper state restoration
    /// </summary>
    public void SetEquipmentData(EquipmentSaveData newData)
    {
        if (newData == null || !newData.IsValid())
        {
            DebugLog("Invalid equipment data - clearing state");
            ClearEquipmentState();
            return;
        }

        DebugLog("=== RESTORING EQUIPMENT DATA ===");
        equipmentData = newData;

        // FIXED: Restore internal state tracking from save data
        currentActiveSlot = newData.currentActiveSlot;
        hasEquippedItem = newData.hasEquippedItem;

        // Debug what we're restoring
        var assignedCount = equipmentData.hotkeyBindings.FindAll(h => h.isAssigned).Count;
        DebugLog($"Restoring equipment: {assignedCount} hotkey assignments, active slot: {currentActiveSlot}, equipped: {hasEquippedItem}");

        // STEP 1: Rebuild all hotbar visuals from the loaded data
        RefreshAllEquippedItemPrefabs();

        // STEP 2: CRITICAL FIX - Restore the exact slot selection after visuals are ready
        RestoreSlotSelection();

        DebugLog("Equipment data restoration complete");
    }

    /// <summary>
    /// CRITICAL FIX: Properly restores the slot selection and activates equipped item
    /// </summary>
    private void RestoreSlotSelection()
    {
        DebugLog($"Restoring slot selection to slot {currentActiveSlot}");

        // Get the binding for the slot that should be active
        var activeBinding = equipmentData.GetHotkeyBinding(currentActiveSlot);

        if (activeBinding == null)
        {
            DebugLog($"No binding found for slot {currentActiveSlot} - defaulting to slot 1");
            currentActiveSlot = 1;
            activeBinding = equipmentData.GetHotkeyBinding(1);
        }

        // Determine if the restored slot should have an equipped item
        bool shouldHaveEquippedItem = false;
        ItemData itemData = null;

        if (activeBinding.isAssigned && inventoryManager != null)
        {
            var inventoryItem = inventoryManager.InventoryGridData.GetItem(activeBinding.itemId);
            if (inventoryItem?.ItemData != null)
            {
                itemData = inventoryItem.ItemData;
                shouldHaveEquippedItem = !enableStateRestrictions || CanEquipItemInCurrentState(itemData);
            }
        }

        // Update internal state to match what should be equipped
        isCurrentSlotUsable = shouldHaveEquippedItem;
        hasEquippedItem = shouldHaveEquippedItem;

        // CRITICAL FIX: Update save data to match current state
        equipmentData.UpdateCurrentState(currentActiveSlot, hasEquippedItem);

        if (shouldHaveEquippedItem && itemData != null)
        {
            // Restore equipped item data
            equipmentData.equippedItem.EquipFromHotkey(activeBinding.itemId, itemData, currentActiveSlot);

            // CRITICAL FIX: Activate the equipped item visual
            if (visualManager != null)
            {
                DebugLog($"Activating visual for restored equipped item: {itemData.itemName} in slot {currentActiveSlot}");
                visualManager.EquipHotbarSlot(currentActiveSlot, itemData);
            }

            DebugLog($"Restored equipped item: {itemData.itemName} from slot {currentActiveSlot}");
            OnItemEquipped?.Invoke(equipmentData.equippedItem);
        }
        else
        {
            // No item should be equipped - ensure we're unarmed
            equipmentData.equippedItem.Clear();

            if (visualManager != null)
            {
                visualManager.UnequipCurrentItem();
            }

            DebugLog($"Restored to unarmed state - slot {currentActiveSlot} selected but no usable item");
            OnItemUnequipped?.Invoke();
            OnUnarmedActivated?.Invoke();
        }

        // Fire slot selection event to update UI
        OnSlotSelected?.Invoke(currentActiveSlot, activeBinding, isCurrentSlotUsable);

        DebugLog($"Slot selection restoration complete - Active: {currentActiveSlot}, Equipped: {hasEquippedItem}");
    }

    /// <summary>
    /// Clears all equipment state
    /// </summary>
    public void ClearEquipmentState()
    {
        equipmentData.equippedItem.Clear();
        hasEquippedItem = false;
        currentActiveSlot = 1;
        isCurrentSlotUsable = false;

        // FIXED: Update save data state tracking
        equipmentData.UpdateCurrentState(1, false);

        // Clear all visual objects
        if (visualManager != null)
        {
            visualManager.ForceCleanup();
        }

        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.isAssigned)
            {
                int slotNumber = binding.slotNumber;
                binding.ClearSlot();
                OnHotkeyCleared?.Invoke(slotNumber);
            }
        }

        OnItemUnequipped?.Invoke();
        OnUnarmedActivated?.Invoke();
        DebugLog("Equipment state cleared - slot 1 selected, unarmed");
    }

    /// <summary>
    /// FIXED: Gets copy of equipment data for saving with current state
    /// </summary>
    public EquipmentSaveData GetEquipmentDataDirect()
    {
        // CRITICAL FIX: Update save data with current state before copying
        if (equipmentData != null)
        {
            equipmentData.UpdateCurrentState(currentActiveSlot, hasEquippedItem);
            DebugLog($"Updated save data before copy - Active slot: {currentActiveSlot}, Equipped: {hasEquippedItem}");
        }

        return new EquipmentSaveData(equipmentData);
    }

    #endregion

    #region Audio

    private void PlayEquipSound()
    {
        if (equipSound != null)
            AudioSource.PlayClipAtPoint(equipSound, Vector3.zero);
    }

    private void PlayHotkeySound()
    {
        if (hotkeySound != null)
            AudioSource.PlayClipAtPoint(hotkeySound, Vector3.zero);
    }

    private void PlayRestrictedSound()
    {
        if (restrictedSound != null)
            AudioSource.PlayClipAtPoint(restrictedSound, Vector3.zero);
    }

    #endregion

    #region Utility Methods

    private bool ValidateInventoryManager()
    {
        if (inventoryManager == null)
        {
            DebugLog("InventoryManager not available");
            return false;
        }
        return true;
    }

    private bool IsValidSlotNumber(int slotNumber) => slotNumber >= 1 && slotNumber <= 10;

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[EquippedItemManager] {message}");
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.OnManagersRefreshed -= FindSystemReferences;

        if (InputManager.Instance != null)
        {
            InputManager.OnInputManagerReady -= OnInputManagerReady;
            InputManager.Instance.OnScrollWheelInput -= HandleScrollInput;
            InputManager.Instance.OnHotkeyPressed -= OnHotkeyPressed;
        }

        if (inventoryManager != null)
        {
            inventoryManager.OnItemRemoved -= OnInventoryItemRemoved;
            inventoryManager.OnItemAdded -= OnInventoryItemAdded;
        }

        if (playerStateManager != null)
            playerStateManager.OnStateChanged -= OnPlayerStateChanged;
    }

    #endregion
}