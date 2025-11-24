using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Core clothing system manager handling equipment, stats, and UI coordination.
/// PHASE 1 ENHANCEMENT: Now properly removes items from inventory when equipped,
/// handles swapping with validation, and returns items to inventory when unequipped.
/// </summary>
public class ClothingManager : MonoBehaviour
{
    public static ClothingManager Instance { get; private set; }

    [Header("Clothing Slots Configuration")]
    [SerializeField] private ClothingSlot[] clothingSlots;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Events for UI synchronization (following InventoryManager pattern)
    public event Action<ClothingSlot, InventoryItemData> OnItemEquipped;
    public event Action<ClothingSlot, string> OnItemUnequipped;
    public event Action<string, float> OnClothingConditionChanged;
    public event Action OnClothingDataChanged;

    // NEW: Events for swap operations
    public event Action<ClothingSlot, string, string> OnItemSwapped; // slot, oldItemId, newItemId

    // Cached stats for performance
    private float cachedTotalDefense = 0f;
    private float cachedTotalWarmth = 0f;
    private float cachedTotalRainResistance = 0f;
    private bool statsCacheValid = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeClothingSlots();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Subscribe to inventory events to handle item removal
        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemRemoved += OnInventoryItemRemoved;
        }
    }

    private void OnDestroy()
    {
        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemRemoved -= OnInventoryItemRemoved;
        }
    }

    /// <summary>
    /// Initialize the clothing slots array with all available layers
    /// </summary>
    private void InitializeClothingSlots()
    {
        if (clothingSlots == null || clothingSlots.Length == 0)
        {
            DebugLog("Initializing default clothing slots");

            clothingSlots = new ClothingSlot[]
            {
                new ClothingSlot(ClothingLayer.HeadUpper),
                new ClothingSlot(ClothingLayer.HeadLower),
                new ClothingSlot(ClothingLayer.TorsoInner),
                new ClothingSlot(ClothingLayer.TorsoOuter),
                new ClothingSlot(ClothingLayer.LegsInner),
                new ClothingSlot(ClothingLayer.LegsOuter),
                new ClothingSlot(ClothingLayer.Hands),
                new ClothingSlot(ClothingLayer.Socks),
                new ClothingSlot(ClothingLayer.Shoes)
            };
        }

        InvalidateStatsCache();
        DebugLog($"Clothing system initialized with {clothingSlots.Length} slots");
    }

    /// <summary>
    /// ENHANCED: Equips an item from inventory to the specified clothing layer.
    /// Now properly removes item from inventory and handles swapping with validation.
    /// </summary>
    public bool EquipItemToLayer(string itemId, ClothingLayer targetLayer)
    {
        if (PlayerInventoryManager.Instance == null)
        {
            DebugLog("Cannot equip item - InventoryManager not found");
            return false;
        }

        // Get the item from inventory
        var item = PlayerInventoryManager.Instance.InventoryGridData.GetItem(itemId);
        if (item == null)
        {
            DebugLog($"Cannot equip item - item {itemId} not found in inventory");
            return false;
        }

        // Validate item type and compatibility
        if (!ValidateItemForEquipping(item, targetLayer))
        {
            return false;
        }

        var targetSlot = GetSlot(targetLayer);
        if (targetSlot == null)
        {
            DebugLog($"Cannot equip item - no slot found for layer {targetLayer}");
            return false;
        }

        // Handle different scenarios: empty slot vs occupied slot
        if (targetSlot.IsEmpty)
        {
            return EquipToEmptySlot(item, targetSlot);
        }
        else
        {
            return EquipWithSwap(item, targetSlot);
        }
    }

    /// <summary>
    /// NEW: Validates that an item can be equipped to the target layer
    /// </summary>
    private bool ValidateItemForEquipping(InventoryItemData item, ClothingLayer targetLayer)
    {
        if (item.ItemData?.itemType != ItemType.Clothing)
        {
            DebugLog($"Cannot equip item {item.ID} - not a clothing item");
            return false;
        }

        var clothingData = item.ItemData.ClothingData;
        if (clothingData == null)
        {
            DebugLog($"Cannot equip item {item.ID} - no clothing data");
            return false;
        }

        if (!clothingData.CanEquipToLayer(targetLayer))
        {
            DebugLog($"Cannot equip {item.ItemData.itemName} to layer {targetLayer} - not compatible");
            return false;
        }

        return true;
    }

    /// <summary>
    /// NEW: Equips item to an empty slot (simple case)
    /// </summary>
    private bool EquipToEmptySlot(InventoryItemData item, ClothingSlot targetSlot)
    {
        // Remove from inventory first
        if (!PlayerInventoryManager.Instance.RemoveItem(item.ID))
        {
            DebugLog($"Failed to remove item {item.ID} from inventory");
            return false;
        }

        // FIXED: Equip to slot with ItemData reference
        targetSlot.EquipItem(item.ID, item.ItemData);
        InvalidateStatsCache();

        // Fire events
        OnItemEquipped?.Invoke(targetSlot, item);
        OnClothingDataChanged?.Invoke();

        DebugLog($"Equipped {item.ItemData.itemName} to {targetSlot.layer} (empty slot)");
        return true;
    }

    /// <summary>
    /// ENHANCED: Equips item with swapping logic - validates inventory space first
    /// Added comprehensive pre-validation to prevent failures
    /// </summary>
    private bool EquipWithSwap(InventoryItemData newItem, ClothingSlot targetSlot)
    {
        string currentItemId = targetSlot.equippedItemId;

        DebugLog($"Attempting to swap {newItem.ItemData.itemName} with currently equipped item in {targetSlot.layer}");

        // FIXED: Get the currently equipped item data from the slot
        var currentItemData = targetSlot.GetEquippedItemData();
        if (currentItemData == null)
        {
            DebugLog($"Warning: Currently equipped item {currentItemId} has no ItemData - treating as empty slot");
            return EquipToEmptySlot(newItem, targetSlot);
        }

        // Pre-validation: Check all conditions before attempting swap
        if (!PreValidateSwapOperation(newItem, currentItemData, targetSlot))
        {
            return false;
        }

        // Perform the swap operation
        return ExecuteSwapOperation(newItem, currentItemData, targetSlot);
    }

    /// <summary>
    /// NEW: Comprehensive pre-validation for swap operations
    /// </summary>
    private bool PreValidateSwapOperation(InventoryItemData newItem, ItemData currentItemData, ClothingSlot targetSlot)
    {
        // DEBUG: Log the complete state before validation
        DebugSwapOperation(newItem.ID, targetSlot.layer);

        // 1. Verify new item exists in inventory
        var inventoryItem = PlayerInventoryManager.Instance.InventoryGridData.GetItem(newItem.ID);
        if (inventoryItem == null)
        {
            DebugLog($"Pre-validation failed: Item {newItem.ID} not found in inventory");
            return false;
        }

        // 2. Verify current item data is valid
        if (currentItemData == null)
        {
            DebugLog($"Pre-validation failed: Current equipped item has no ItemData");
            return false;
        }

        // 3. Check if inventory will have space for displaced item
        if (!ValidateSwapInventorySpace(newItem.ID, currentItemData))
        {
            DebugLog($"Pre-validation failed: Insufficient inventory space for {currentItemData.itemName}");
            return false;
        }

        // 4. Verify the new item can actually be equipped to this layer
        if (newItem.ItemData?.ClothingData == null || !newItem.ItemData.ClothingData.CanEquipToLayer(targetSlot.layer))
        {
            DebugLog($"Pre-validation failed: {newItem.ItemData?.itemName} cannot be equipped to {targetSlot.layer}");
            return false;
        }

        DebugLog($"Pre-validation passed for swap: {newItem.ItemData.itemName} <-> {currentItemData.itemName}");
        return true;
    }

    /// <summary>
    /// REFACTORED: Validates swap by checking if displaced item can fit in the same space
    /// Now implements "same-space swap" - displaced item goes to the exact position of the new item
    /// </summary>
    private bool ValidateSwapInventorySpace(string itemToRemoveId, ItemData itemToAdd)
    {
        var inventory = PlayerInventoryManager.Instance;

        // Get the item that we plan to remove from inventory (the one being equipped)
        var itemToRemove = inventory.InventoryGridData.GetItem(itemToRemoveId);
        if (itemToRemove == null)
        {
            DebugLog($"Item {itemToRemoveId} not found in inventory for swap validation");
            return false;
        }

        // Store the original position and rotation of the item being equipped
        Vector2Int originalPosition = itemToRemove.GridPosition;
        int originalRotation = itemToRemove.currentRotation;

        DebugLog($"Validating same-space swap: {itemToAdd.itemName} -> position {originalPosition} (rotation {originalRotation})");

        // Create a temporary test item for the displaced clothing at the original position
        var tempDisplacedItem = new InventoryItemData("temp_displaced", itemToAdd, originalPosition);

        // Try each possible rotation of the displaced item to see if it fits
        int maxRotations = TetrominoDefinitions.GetRotationCount(tempDisplacedItem.shapeType);

        for (int rotation = 0; rotation < maxRotations; rotation++)
        {
            tempDisplacedItem.SetRotation(rotation);

            // Temporarily remove the original item to test if displaced item fits in its space
            inventory.InventoryGridData.RemoveItem(itemToRemoveId);

            // Test if the displaced item can fit at the original position with this rotation
            bool canFitAtOriginalPosition = inventory.InventoryGridData.IsValidPosition(originalPosition, tempDisplacedItem);

            // Restore the original item
            itemToRemove.SetRotation(originalRotation); // Ensure original rotation is preserved
            inventory.InventoryGridData.PlaceItem(itemToRemove);

            if (canFitAtOriginalPosition)
            {
                DebugLog($"Same-space swap validated: {itemToAdd.itemName} can fit at position {originalPosition} with rotation {rotation}");
                return true;
            }
        }

        DebugLog($"Same-space swap failed: {itemToAdd.itemName} cannot fit at position {originalPosition} with any rotation");
        return false;
    }

    /// <summary>
    /// REFACTORED: Executes same-space swap operation
    /// The displaced item goes exactly where the new item was in inventory
    /// </summary>
    private bool ExecuteSwapOperation(InventoryItemData newItem, ItemData currentItemData, ClothingSlot targetSlot)
    {
        string newItemId = newItem.ID;
        string currentItemId = targetSlot.equippedItemId;

        DebugLog($"Executing same-space swap: {newItem.ItemData.itemName} (from inventory) <-> {currentItemData.itemName} (from {targetSlot.layer})");

        // Store the original position and rotation where the new item was located
        Vector2Int originalPosition = newItem.GridPosition;
        int originalRotation = newItem.currentRotation;

        DebugLog($"Original inventory position: {originalPosition}, rotation: {originalRotation}");

        // Verify the new item is actually in inventory before trying to remove it
        var inventoryItem = PlayerInventoryManager.Instance.InventoryGridData.GetItem(newItemId);
        if (inventoryItem == null)
        {
            DebugLog($"Same-space swap failed: Item {newItemId} not found in inventory");
            return false;
        }

        // Step 1: Remove new item from inventory
        DebugLog($"Step 1: Removing {newItemId} from inventory position {originalPosition}");
        if (!PlayerInventoryManager.Instance.RemoveItem(newItemId))
        {
            DebugLog($"Same-space swap failed: Could not remove {newItemId} from inventory");
            return false;
        }

        // Step 2: Unequip current item from slot
        DebugLog($"Step 2: Unequipping {currentItemId} from {targetSlot.layer}");
        string unequippedItemId = targetSlot.UnequipItem();
        if (unequippedItemId != currentItemId)
        {
            DebugLog($"Warning: Unequipped item ID mismatch. Expected: {currentItemId}, Got: {unequippedItemId}");
        }

        // Step 3: Equip new item to slot
        DebugLog($"Step 3: Equipping {newItemId} to {targetSlot.layer}");
        targetSlot.EquipItem(newItemId, newItem.ItemData);

        // Step 4: Place displaced item at the exact same position with optimal rotation
        DebugLog($"Step 4: Placing displaced item {currentItemData.itemName} at position {originalPosition}");
        if (PlaceDisplacedItemAtOriginalPosition(currentItemData, originalPosition))
        {
            // Success! Complete the swap
            InvalidateStatsCache();

            // Fire events
            OnItemSwapped?.Invoke(targetSlot, currentItemId, newItemId);
            OnItemEquipped?.Invoke(targetSlot, newItem);
            OnClothingDataChanged?.Invoke();

            DebugLog($"Same-space swap completed successfully: {newItem.ItemData.itemName} equipped to {targetSlot.layer}, {currentItemData.itemName} placed at {originalPosition}");
            return true;
        }
        else
        {
            // Rollback: swap failed to place displaced item at original position
            DebugLog($"Same-space swap rollback: Failed to place {currentItemData.itemName} at original position {originalPosition}");

            // Restore original equipped state
            targetSlot.UnequipItem();
            targetSlot.EquipItem(currentItemId, currentItemData);

            // Return new item to inventory at original position
            if (!PlayerInventoryManager.Instance.AddItem(newItem.ItemData, originalPosition))
            {
                // If exact position fails, try any position
                if (!PlayerInventoryManager.Instance.AddItem(newItem.ItemData))
                {
                    DebugLog($"CRITICAL: Failed to restore {newItem.ItemData.itemName} to inventory during rollback!");
                }
            }
            else
            {
                DebugLog($"Rollback successful: {newItem.ItemData.itemName} returned to original position {originalPosition}");
            }

            return false;
        }
    }

    /// <summary>
    /// NEW: Places the displaced item at the original position with the best fitting rotation
    /// </summary>
    private bool PlaceDisplacedItemAtOriginalPosition(ItemData displacedItemData, Vector2Int originalPosition)
    {
        var inventory = PlayerInventoryManager.Instance;

        // Try to add the displaced item back to inventory at the original position
        // The InventoryManager.AddItem method will handle rotation and placement validation
        if (inventory.AddItem(displacedItemData, originalPosition))
        {
            DebugLog($"Successfully placed displaced item {displacedItemData.itemName} at original position {originalPosition}");
            return true;
        }

        // If that fails, try without specifying position (let InventoryManager find best fit)
        if (inventory.AddItem(displacedItemData))
        {
            DebugLog($"Placed displaced item {displacedItemData.itemName} at alternative position (original position occupied)");
            return true;
        }

        DebugLog($"Failed to place displaced item {displacedItemData.itemName} anywhere in inventory");
        return false;
    }

    /// <summary>
    /// FIXED: Gets equipped item data by reconstructing it from ItemData
    /// Since equipped items aren't in inventory, we need to recreate the data
    /// </summary>
    private InventoryItemData GetEquippedItemData(string equippedItemId)
    {
        var slot = GetSlotForItem(equippedItemId);
        if (slot == null) return null;

        // Try to get the equipped item through the slot's method
        var equippedItem = slot.GetEquippedItem();
        if (equippedItem != null)
        {
            return equippedItem;
        }

        // If that fails, we need to reconstruct the data
        // This happens because equipped items are no longer in inventory
        // We need to find the ItemData some other way

        // For now, let's create a method to find ItemData by the equipped item ID
        // The equipped item ID should contain information about the original item
        var itemData = FindItemDataByEquippedId(equippedItemId);
        if (itemData != null)
        {
            // Create new InventoryItemData for the equipped item
            return new InventoryItemData(equippedItemId, itemData, Vector2Int.zero);
        }

        Debug.LogWarning($"Could not find ItemData for equipped item: {equippedItemId}");
        return null;
    }

    /// <summary>
    /// NEW: Find ItemData by equipped item ID
    /// This is a workaround for the current system limitation
    /// </summary>
    private ItemData FindItemDataByEquippedId(string equippedItemId)
    {
        // Try to extract the original item name from the equipped item ID
        // This assumes the equipped item ID is the same as the original inventory item ID

        // First, try to find it in the save manager's item data path
        string itemDataPath = SaveManager.Instance?.itemDataPath ?? "Data/Items/";

        // Load all ItemData assets and search for a match
        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();

        // For now, we'll try a simple approach - if the equipped item ID contains
        // information about the original item, we can use that
        // Otherwise, we might need to store more information about equipped items

        foreach (var itemData in allItemData)
        {
            // This is a basic approach - you might need to adjust this based on
            // how your item IDs are structured
            if (equippedItemId.Contains(itemData.name) ||
                itemData.name.Contains(equippedItemId.Replace("item_", "")))
            {
                return itemData;
            }
        }

        return null;
    }

    /// <summary>
    /// ENHANCED: Unequips an item from the specified layer and returns it to inventory
    /// Now properly validates inventory space and handles failures gracefully
    /// </summary>
    public bool UnequipItemFromLayer(ClothingLayer layer)
    {
        var slot = GetSlot(layer);
        if (slot == null || slot.IsEmpty)
        {
            DebugLog($"Cannot unequip from {layer} - slot empty or not found");
            return false;
        }

        string itemId = slot.equippedItemId;
        var equippedItem = slot.GetEquippedItem();

        if (equippedItem?.ItemData == null)
        {
            DebugLog($"Cannot unequip {itemId} - item data not found");
            return false;
        }

        // Check if inventory has space
        if (!PlayerInventoryManager.Instance.HasSpaceForItem(equippedItem.ItemData))
        {
            DebugLog($"Cannot unequip {equippedItem.ItemData.itemName} - inventory full");
            return false;
        }

        // Unequip from slot
        slot.UnequipItem();
        InvalidateStatsCache();

        // Add to inventory
        if (PlayerInventoryManager.Instance.AddItem(equippedItem.ItemData))
        {
            // Success
            OnItemUnequipped?.Invoke(slot, itemId);
            OnClothingDataChanged?.Invoke();

            DebugLog($"Unequipped {equippedItem.ItemData.itemName} from {layer} and returned to inventory");
            return true;
        }
        else
        {
            // Failed to add to inventory - restore to slot
            slot.EquipItem(itemId);
            InvalidateStatsCache();

            DebugLog($"Failed to return {equippedItem.ItemData.itemName} to inventory - restored to slot");
            return false;
        }
    }

    /// <summary>
    /// NEW: Attempts to unequip item and drop it in the world if inventory is full
    /// </summary>
    public bool UnequipAndDropItem(ClothingLayer layer)
    {
        var slot = GetSlot(layer);
        if (slot == null || slot.IsEmpty)
        {
            DebugLog($"Cannot unequip from {layer} - slot empty or not found");
            return false;
        }

        string itemId = slot.equippedItemId;
        var equippedItem = slot.GetEquippedItem();

        if (equippedItem?.ItemData == null)
        {
            DebugLog($"Cannot unequip {itemId} - item data not found");
            return false;
        }

        // Try to return to inventory first
        if (PlayerInventoryManager.Instance.HasSpaceForItem(equippedItem.ItemData))
        {
            return UnequipItemFromLayer(layer);
        }

        // If inventory is full, drop in world
        if (!equippedItem.ItemData.CanDrop)
        {
            DebugLog($"Cannot drop {equippedItem.ItemData.itemName} - it's a key item");
            return false;
        }

        // Unequip from slot
        slot.UnequipItem();
        InvalidateStatsCache();

        // Drop in world using ItemDropSystem
        if (ItemDropSystem.Instance != null && ItemDropSystem.Instance.DropItem(equippedItem.ItemData))
        {
            OnItemUnequipped?.Invoke(slot, itemId);
            OnClothingDataChanged?.Invoke();

            DebugLog($"Unequipped {equippedItem.ItemData.itemName} from {layer} and dropped in world (inventory full)");
            return true;
        }
        else
        {
            // Failed to drop - restore to slot
            slot.EquipItem(itemId);
            InvalidateStatsCache();

            DebugLog($"Failed to drop {equippedItem.ItemData.itemName} - restored to slot");
            return false;
        }
    }

    /// <summary>
    /// Gets the clothing slot for the specified layer
    /// </summary>
    public ClothingSlot GetSlot(ClothingLayer layer)
    {
        return System.Array.Find(clothingSlots, slot => slot.layer == layer);
    }

    /// <summary>
    /// Gets all clothing slots
    /// </summary>
    public ClothingSlot[] GetAllSlots()
    {
        return clothingSlots;
    }

    /// <summary>
    /// Gets all currently equipped items
    /// </summary>
    public List<InventoryItemData> GetEquippedItems()
    {
        var equippedItems = new List<InventoryItemData>();

        foreach (var slot in clothingSlots)
        {
            if (slot.IsOccupied)
            {
                var item = slot.GetEquippedItem();
                if (item != null)
                {
                    equippedItems.Add(item);
                }
            }
        }

        return equippedItems;
    }

    /// <summary>
    /// Checks if an item is currently equipped in any slot
    /// </summary>
    public bool IsItemEquipped(string itemId)
    {
        return System.Array.Exists(clothingSlots, slot => slot.equippedItemId == itemId);
    }

    /// <summary>
    /// Gets the slot where the specified item is equipped, or null if not equipped
    /// </summary>
    public ClothingSlot GetSlotForItem(string itemId)
    {
        return System.Array.Find(clothingSlots, slot => slot.equippedItemId == itemId);
    }

    #region Stats Calculation

    /// <summary>
    /// Gets total defense value from all equipped clothing
    /// </summary>
    public float GetTotalDefense()
    {
        if (!statsCacheValid)
            RecalculateStats();
        return cachedTotalDefense;
    }

    /// <summary>
    /// Gets total warmth value from all equipped clothing
    /// </summary>
    public float GetTotalWarmth()
    {
        if (!statsCacheValid)
            RecalculateStats();
        return cachedTotalWarmth;
    }

    /// <summary>
    /// Gets total rain resistance from all equipped clothing
    /// </summary>
    public float GetTotalRainResistance()
    {
        if (!statsCacheValid)
            RecalculateStats();
        return cachedTotalRainResistance;
    }

    /// <summary>
    /// Recalculates all clothing stats from equipped items
    /// </summary>
    private void RecalculateStats()
    {
        cachedTotalDefense = 0f;
        cachedTotalWarmth = 0f;
        cachedTotalRainResistance = 0f;

        foreach (var slot in clothingSlots)
        {
            var clothingData = slot.GetEquippedClothingData();
            if (clothingData != null)
            {
                cachedTotalDefense += clothingData.GetEffectiveDefense();
                cachedTotalWarmth += clothingData.GetEffectiveWarmth();
                cachedTotalRainResistance += clothingData.GetEffectiveRainResistance();
            }
        }

        statsCacheValid = true;
        DebugLog($"Recalculated stats - Defense: {cachedTotalDefense:F1}, Warmth: {cachedTotalWarmth:F1}, Rain: {cachedTotalRainResistance:F1}");
    }

    /// <summary>
    /// Invalidates the stats cache, forcing recalculation on next access
    /// </summary>
    private void InvalidateStatsCache()
    {
        statsCacheValid = false;
    }

    #endregion

    #region Damage and Condition

    /// <summary>
    /// Applies damage to equipped clothing when player takes damage
    /// </summary>
    public void OnPlayerTakeDamage(float damageAmount)
    {
        // Random chance to damage clothing
        float damageChance = 0.25f; // 25% chance per damage event

        foreach (var slot in clothingSlots)
        {
            if (slot.IsOccupied && UnityEngine.Random.value < damageChance)
            {
                var clothingData = slot.GetEquippedClothingData();
                if (clothingData != null)
                {
                    float conditionBefore = clothingData.currentCondition;
                    clothingData.TakeDamage();

                    if (clothingData.currentCondition != conditionBefore)
                    {
                        OnClothingConditionChanged?.Invoke(slot.equippedItemId, clothingData.currentCondition);
                        InvalidateStatsCache();

                        DebugLog($"Clothing {slot.equippedItemId} damaged - condition now {clothingData.currentCondition:F1}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Simple repair method for changing clothing condition
    /// </summary>
    public bool RepairClothingItem(string itemId, float repairAmount)
    {
        var slot = GetSlotForItem(itemId);
        if (slot == null)
        {
            DebugLog($"Cannot repair {itemId} - item not equipped");
            return false;
        }

        var clothingData = slot.GetEquippedClothingData();
        if (clothingData == null)
        {
            DebugLog($"Cannot repair {itemId} - no clothing data");
            return false;
        }

        float conditionBefore = clothingData.currentCondition;
        bool success = clothingData.RepairCondition(repairAmount);

        if (success && clothingData.currentCondition != conditionBefore)
        {
            OnClothingConditionChanged?.Invoke(itemId, clothingData.currentCondition);
            InvalidateStatsCache();

            DebugLog($"Repaired {itemId} - condition now {clothingData.currentCondition:F1}");
        }

        return success;
    }

    #endregion

    #region Data Management

    /// <summary>
    /// Sets clothing data directly (used by save system)
    /// </summary>
    public void SetClothingData(ClothingSlot[] newSlots)
    {
        clothingSlots = newSlots ?? new ClothingSlot[0];
        InvalidateStatsCache();
        OnClothingDataChanged?.Invoke();

        DebugLog($"Clothing data set with {clothingSlots.Length} slots");
    }

    /// <summary>
    /// Clears all equipped clothing
    /// </summary>
    public void ClearAllClothing()
    {
        foreach (var slot in clothingSlots)
        {
            if (slot.IsOccupied)
            {
                string itemId = slot.UnequipItem();
                OnItemUnequipped?.Invoke(slot, itemId);
            }
        }

        InvalidateStatsCache();
        OnClothingDataChanged?.Invoke();

        DebugLog("All clothing cleared");
    }

    /// <summary>
    /// Handles inventory item removal - unequip if currently equipped
    /// </summary>
    private void OnInventoryItemRemoved(string itemId)
    {
        var slot = GetSlotForItem(itemId);
        if (slot != null)
        {
            DebugLog($"Item {itemId} removed from inventory - unequipping from {slot.layer}");
            slot.UnequipItem();
            InvalidateStatsCache();
            OnItemUnequipped?.Invoke(slot, itemId);
            OnClothingDataChanged?.Invoke();
        }
    }

    #endregion

    #region Debug Methods

    [Button("Debug Clothing Stats")]
    private void DebugClothingStats()
    {
        DebugLog("=== CLOTHING STATS DEBUG ===");
        DebugLog($"Total Defense: {GetTotalDefense():F1}");
        DebugLog($"Total Warmth: {GetTotalWarmth():F1}");
        DebugLog($"Total Rain Resistance: {GetTotalRainResistance():F1}");

        DebugLog("\n=== EQUIPPED ITEMS ===");
        foreach (var slot in clothingSlots)
        {
            DebugLog(slot.GetDebugInfo());
        }
    }

    [Button("Debug Complete System State")]
    private void DebugCompleteSystemState()
    {
        DebugLog("=== COMPLETE CLOTHING SYSTEM DEBUG ===");

        // Debug all clothing slots
        foreach (var slot in clothingSlots)
        {
            DebugLog($"Slot {slot.layer}:");
            DebugLog($"  IsEmpty: {slot.IsEmpty}");
            DebugLog($"  EquippedItemId: {slot.equippedItemId}");

            var itemData = slot.GetEquippedItemData();
            if (itemData != null)
            {
                DebugLog($"  ItemData: {itemData.itemName}");
                DebugLog($"  ItemType: {itemData.itemType}");
            }
            else
            {
                DebugLog($"  ItemData: NULL");
            }

            var inventoryItem = slot.GetEquippedItem();
            if (inventoryItem != null)
            {
                DebugLog($"  InventoryItem: {inventoryItem.ItemData?.itemName}");
            }
            else
            {
                DebugLog($"  InventoryItem: NULL");
            }
        }

        // Debug inventory state
        if (PlayerInventoryManager.Instance != null)
        {
            var stats = PlayerInventoryManager.Instance.GetInventoryStats();
            DebugLog($"\nInventory Stats: {stats.itemCount} items, {stats.occupiedCells}/{stats.totalCells} cells");

            var allItems = PlayerInventoryManager.Instance.InventoryGridData.GetAllItems();
            DebugLog("Items in inventory:");
            foreach (var item in allItems)
            {
                DebugLog($"  {item.ID}: {item.ItemData?.itemName} at {item.GridPosition}");
            }
        }
    }

    /// <summary>
    /// NEW: Debug method specifically for swap operations
    /// </summary>
    public void DebugSwapOperation(string newItemId, ClothingLayer targetLayer)
    {
        DebugLog($"=== SWAP OPERATION DEBUG for {newItemId} -> {targetLayer} ===");

        // Check inventory item
        var inventoryItem = PlayerInventoryManager.Instance?.InventoryGridData.GetItem(newItemId);
        if (inventoryItem != null)
        {
            DebugLog($"New item found in inventory: {inventoryItem.ItemData?.itemName} at {inventoryItem.GridPosition}");
        }
        else
        {
            DebugLog($"ERROR: New item {newItemId} NOT found in inventory!");
        }

        // Check target slot
        var targetSlot = GetSlot(targetLayer);
        if (targetSlot != null)
        {
            DebugLog($"Target slot {targetLayer}:");
            DebugLog($"  IsEmpty: {targetSlot.IsEmpty}");
            DebugLog($"  EquippedItemId: {targetSlot.equippedItemId}");

            var currentItemData = targetSlot.GetEquippedItemData();
            if (currentItemData != null)
            {
                DebugLog($"  Current ItemData: {currentItemData.itemName}");
            }
            else
            {
                DebugLog($"  Current ItemData: NULL");
            }
        }
        else
        {
            DebugLog($"ERROR: Target slot {targetLayer} not found!");
        }

        // Check inventory space
        if (inventoryItem?.ItemData != null && targetSlot?.GetEquippedItemData() != null)
        {
            bool hasSpace = PlayerInventoryManager.Instance.HasSpaceForItem(targetSlot.GetEquippedItemData());
            DebugLog($"Inventory has space for displaced item: {hasSpace}");
        }
    }

    [Button("Clear All Clothing")]
    private void DebugClearAllClothing()
    {
        ClearAllClothing();
    }

    [Button("Damage Random Clothing")]
    private void DebugDamageRandomClothing()
    {
        OnPlayerTakeDamage(10f);
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ClothingManager] {message}");
        }
    }

    #endregion
}