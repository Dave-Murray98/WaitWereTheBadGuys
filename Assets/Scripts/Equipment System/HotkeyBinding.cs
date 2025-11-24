using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Data container for a single hotkey slot assignment in the equipment system.
/// Handles item assignment, stacking for consumables, and automatic cleanup.
/// Supports smart stacking where multiple identical consumables share a hotkey slot.
/// </summary>
[System.Serializable]
public class HotkeyBinding
{
    [Header("Slot Info")]
    public int slotNumber;      // 1-0 keys (1=slot 1, 0=slot 10)
    public bool isAssigned;     // Whether this slot has an item

    [Header("Item Reference")]
    public string itemId;       // Current active item ID from inventory
    public string itemDataName; // ItemData name for persistence and lookup

    [Header("Stack Management")]
    public List<string> stackedItemIds = new List<string>(); // All items of this type
    public int currentStackIndex = 0; // Which item in stack is currently active

    /// <summary>
    /// Creates a hotkey binding for the specified slot number.
    /// </summary>
    public HotkeyBinding(int slot)
    {
        slotNumber = slot;
        isAssigned = false;
        itemId = "";
        itemDataName = "";
        stackedItemIds = new List<string>();
        currentStackIndex = 0;
    }

    /// <summary>
    /// Copy constructor for scene transitions and data preservation.
    /// </summary>
    public HotkeyBinding(HotkeyBinding other)
    {
        slotNumber = other.slotNumber;
        isAssigned = other.isAssigned;
        itemId = other.itemId;
        itemDataName = other.itemDataName;
        stackedItemIds = new List<string>(other.stackedItemIds); // Deep copy list
        currentStackIndex = other.currentStackIndex;

        if (isAssigned)
        {
            Debug.Log($"[HotkeyBinding] Copy constructor: Slot {slotNumber} copied with item {itemDataName} (ID: {itemId})");
        }
    }

    /// <summary>
    /// Assigns an item to this hotkey slot, replacing any existing assignment.
    /// Automatically removes the item from other hotkey slots to ensure unique assignment.
    /// </summary>
    public void AssignItem(string newItemId, string newItemDataName)
    {
        // Clear any existing assignment
        ClearSlot();

        // Remove this item from other hotkey slots (unique assignment rule)
        RemoveItemFromOtherHotkeys(newItemId);

        // Assign the new item
        itemId = newItemId;
        itemDataName = newItemDataName;
        isAssigned = true;

        // Initialize stack with this item
        stackedItemIds.Add(newItemId);
        currentStackIndex = 0;

        // Find and stack identical consumables if this is a consumable
        FindAndStackIdenticalConsumables();
    }

    /// <summary>
    /// Removes this item from any other hotkey slots to ensure unique assignment.
    /// </summary>
    private void RemoveItemFromOtherHotkeys(string itemIdToRemove)
    {
        if (EquippedItemManager.Instance == null) return;

        var allBindings = EquippedItemManager.Instance.GetAllHotkeyBindings();
        foreach (var binding in allBindings)
        {
            if (binding != this && binding.isAssigned)
            {
                if (binding.stackedItemIds.Contains(itemIdToRemove))
                {
                    bool wasCleared = false;

                    binding.RemoveItem(itemIdToRemove);
                    wasCleared = !binding.isAssigned;

                    // Trigger appropriate UI update event
                    if (wasCleared)
                    {
                        EquippedItemManager.Instance.OnHotkeyCleared?.Invoke(binding.slotNumber);
                    }
                    else
                    {
                        EquippedItemManager.Instance.OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Automatically finds and stacks all identical consumable items in the inventory.
    /// Only applies to consumable items for convenience stacking.
    /// </summary>
    private void FindAndStackIdenticalConsumables()
    {
        if (PlayerInventoryManager.Instance == null) return;

        var itemData = GetCurrentItemData();
        if (itemData == null || itemData.itemType != ItemType.Consumable) return;

        // Find all items in inventory with the same ItemData name
        var inventoryItems = PlayerInventoryManager.Instance.InventoryGridData.GetAllItems();

        foreach (var inventoryItem in inventoryItems)
        {
            // Skip if already in our stack
            if (stackedItemIds.Contains(inventoryItem.ID)) continue;

            // Add to stack if it's the exact same consumable type
            if (inventoryItem.ItemData != null &&
                inventoryItem.ItemData.name == itemDataName)
            {
                stackedItemIds.Add(inventoryItem.ID);
            }
        }

        Debug.Log($"Hotkey {slotNumber}: Found {stackedItemIds.Count} identical {itemDataName} consumables");
    }

    /// <summary>
    /// Removes an item from this slot's stack. If it's the active item, switches to next in stack.
    /// </summary>
    public bool RemoveItem(string itemIdToRemove)
    {
        //Debug.Log($"Hotkey {slotNumber}: Removing {itemIdToRemove} from {itemDataName} stack");

        bool removed = stackedItemIds.Remove(itemIdToRemove);

        if (removed)
        {
            if (itemId == itemIdToRemove)
            {
                if (stackedItemIds.Count > 0)
                {
                    // Move to next item in stack
                    currentStackIndex = Mathf.Clamp(currentStackIndex, 0, stackedItemIds.Count - 1);
                    itemId = stackedItemIds[currentStackIndex];

                    Debug.Log($"Hotkey {slotNumber}: Switched to next {itemDataName} in stack ({stackedItemIds.Count} remaining)");
                }
                else
                {
                    // No more items, clear slot
                    Debug.Log($"Hotkey {slotNumber}: No more {itemDataName} items, clearing slot");
                    ClearSlot();
                }
            }
            else
            {
                // Update current index if needed
                currentStackIndex = stackedItemIds.IndexOf(itemId);
            }
        }

        return removed;
    }

    /// <summary>
    /// Completely clears this hotkey slot.
    /// </summary>
    public void ClearSlot()
    {
        itemId = "";
        itemDataName = "";
        isAssigned = false;
        stackedItemIds.Clear();
        currentStackIndex = 0;
    }

    /// <summary>
    /// Adds a new item to this hotkey's stack if it's the same type (for dynamic stacking).
    /// Only works for consumables and matching item types.
    /// </summary>
    public bool TryAddToStack(string newItemId, string newItemDataName)
    {
        // Only add if this hotkey is assigned and it's the same item type
        if (!isAssigned || itemDataName != newItemDataName) return false;

        // Only stack consumables
        var itemData = GetCurrentItemData();
        if (itemData == null || itemData.itemType != ItemType.Consumable) return false;

        // Don't add if already in stack
        if (stackedItemIds.Contains(newItemId)) return false;

        // Add to stack
        stackedItemIds.Add(newItemId);
        Debug.Log($"Hotkey {slotNumber}: Added new {itemDataName} to stack ({stackedItemIds.Count} total)");

        return true;
    }

    /// <summary>
    /// Gets the ItemData for the currently assigned item with fallback to Resources loading.
    /// </summary>
    public ItemData GetCurrentItemData()
    {
        if (!isAssigned || string.IsNullOrEmpty(itemDataName))
        {
            return null;
        }

        // First try to get from current inventory item
        if (PlayerInventoryManager.Instance != null)
        {
            var inventoryItem = PlayerInventoryManager.Instance.InventoryGridData.GetItem(itemId);
            if (inventoryItem?.ItemData != null)
            {
                return inventoryItem.ItemData;
            }
        }

        // Fallback to Resources load
        return Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + itemDataName);
    }

    /// <summary>
    /// Checks if this slot has multiple items stacked.
    /// </summary>
    public bool HasMultipleItems => stackedItemIds.Count > 1;

    /// <summary>
    /// Gets stack info string for UI display (e.g., "2/3").
    /// </summary>
    public string GetStackInfo()
    {
        if (!isAssigned) return "";
        if (stackedItemIds.Count <= 1) return "";
        return $"{currentStackIndex + 1}/{stackedItemIds.Count}";
    }
}