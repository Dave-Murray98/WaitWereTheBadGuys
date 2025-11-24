using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ENHANCED: Complete clothing system state for saving/loading with ItemData references
/// Now stores ItemData names for equipped items to properly restore them
/// </summary>
[System.Serializable]
public class ClothingSaveData
{
    [Header("Clothing Slots")]
    public List<ClothingSlotSaveData> slots = new List<ClothingSlotSaveData>();

    public ClothingSaveData()
    {
        slots = new List<ClothingSlotSaveData>();
    }

    /// <summary>
    /// Add a clothing slot to the save data
    /// </summary>
    public void AddSlot(ClothingSlotSaveData slotData)
    {
        if (slotData != null)
        {
            slots.Add(slotData);
        }
    }

    /// <summary>
    /// Remove a slot by layer
    /// </summary>
    public bool RemoveSlot(ClothingLayer layer)
    {
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i].layer == layer)
            {
                slots.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get slot save data by layer
    /// </summary>
    public ClothingSlotSaveData GetSlot(ClothingLayer layer)
    {
        foreach (var slot in slots)
        {
            if (slot.layer == layer)
                return slot;
        }
        return null;
    }

    /// <summary>
    /// Clear all slot data
    /// </summary>
    public void Clear()
    {
        slots.Clear();
    }

    /// <summary>
    /// Get count of equipped items (non-empty slots)
    /// </summary>
    public int GetEquippedCount()
    {
        int count = 0;
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.equippedItemId))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Get total number of clothing slots
    /// </summary>
    public int SlotCount => slots.Count;

    /// <summary>
    /// Check if an item is equipped in any slot
    /// </summary>
    public bool IsItemEquipped(string itemId)
    {
        foreach (var slot in slots)
        {
            if (slot.equippedItemId == itemId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get the layer where an item is equipped, or null if not equipped
    /// </summary>
    public ClothingLayer? GetLayerForItem(string itemId)
    {
        foreach (var slot in slots)
        {
            if (slot.equippedItemId == itemId)
                return slot.layer;
        }
        return null;
    }

    /// <summary>
    /// Get all equipped item IDs
    /// </summary>
    public List<string> GetAllEquippedItemIds()
    {
        var equippedIds = new List<string>();
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.equippedItemId))
                equippedIds.Add(slot.equippedItemId);
        }
        return equippedIds;
    }

    /// <summary>
    /// ENHANCED: Get all equipped ItemData names for restoration
    /// </summary>
    public List<string> GetAllEquippedItemDataNames()
    {
        var itemDataNames = new List<string>();
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.equippedItemDataName))
                itemDataNames.Add(slot.equippedItemDataName);
        }
        return itemDataNames;
    }

    /// <summary>
    /// Validate that the save data is consistent
    /// </summary>
    public bool IsValid()
    {
        // Check for duplicate layers
        var seenLayers = new HashSet<ClothingLayer>();
        foreach (var slot in slots)
        {
            if (seenLayers.Contains(slot.layer))
                return false;
            seenLayers.Add(slot.layer);
        }

        // Check for duplicate equipped items
        var seenItems = new HashSet<string>();
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.equippedItemId))
            {
                if (seenItems.Contains(slot.equippedItemId))
                    return false;
                seenItems.Add(slot.equippedItemId);
            }
        }

        return true;
    }

    /// <summary>
    /// ENHANCED: Gets debug information about the clothing save data including ItemData names
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Clothing Save Data Debug Info ===");
        info.AppendLine($"Total Slots: {slots.Count}");
        info.AppendLine($"Equipped Items: {GetEquippedCount()}");

        foreach (var slot in slots)
        {
            if (string.IsNullOrEmpty(slot.equippedItemId))
            {
                info.AppendLine($"  {slot.layer}: Empty");
            }
            else
            {
                string itemDataInfo = !string.IsNullOrEmpty(slot.equippedItemDataName)
                    ? $" ({slot.equippedItemDataName})"
                    : " (No ItemData name)";
                info.AppendLine($"  {slot.layer}: {slot.equippedItemId}{itemDataInfo}");
            }
        }

        return info.ToString();
    }

    /// <summary>
    /// Merge data from another ClothingSaveData instance
    /// </summary>
    public void MergeFrom(ClothingSaveData other, bool overwriteExisting = true)
    {
        if (other == null) return;

        foreach (var otherSlot in other.slots)
        {
            var existingSlot = GetSlot(otherSlot.layer);
            if (existingSlot != null)
            {
                if (overwriteExisting)
                {
                    existingSlot.equippedItemId = otherSlot.equippedItemId;
                    existingSlot.equippedItemDataName = otherSlot.equippedItemDataName;
                }
            }
            else
            {
                AddSlot(new ClothingSlotSaveData
                {
                    layer = otherSlot.layer,
                    equippedItemId = otherSlot.equippedItemId,
                    equippedItemDataName = otherSlot.equippedItemDataName
                });
            }
        }
    }
}

/// <summary>
/// ENHANCED: Save data for an individual clothing slot with ItemData reference
/// </summary>
[System.Serializable]
public class ClothingSlotSaveData
{
    [Header("Slot Identity")]
    public ClothingLayer layer;

    [Header("Equipped Item")]
    public string equippedItemId = "";

    [Header("ItemData Reference")]
    public string equippedItemDataName = ""; // NEW: Store ItemData name for restoration

    /// <summary>
    /// Checks if this slot is empty
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(equippedItemId);

    /// <summary>
    /// Checks if this slot has an item equipped
    /// </summary>
    public bool IsOccupied => !IsEmpty;

    /// <summary>
    /// ENHANCED: Create save data from a ClothingSlot with ItemData reference
    /// </summary>
    public static ClothingSlotSaveData FromClothingSlot(ClothingSlot clothingSlot)
    {
        if (clothingSlot == null)
        {
            Debug.LogError("Cannot create ClothingSlotSaveData - ClothingSlot is null");
            return null;
        }

        var saveData = new ClothingSlotSaveData
        {
            layer = clothingSlot.layer,
            equippedItemId = clothingSlot.equippedItemId
        };

        // ENHANCED: Store ItemData name if item is equipped
        if (!clothingSlot.IsEmpty)
        {
            var itemData = clothingSlot.GetEquippedItemData();
            if (itemData != null)
            {
                saveData.equippedItemDataName = itemData.name;
            }
        }

        return saveData;
    }

    /// <summary>
    /// ENHANCED: Apply this save data to a ClothingSlot with ItemData loading
    /// </summary>
    public void ApplyToClothingSlot(ClothingSlot clothingSlot)
    {
        if (clothingSlot == null)
        {
            Debug.LogError("Cannot apply save data - ClothingSlot is null");
            return;
        }

        if (clothingSlot.layer != layer)
        {
            Debug.LogError($"Cannot apply save data - layer mismatch. Expected {layer}, got {clothingSlot.layer}");
            return;
        }

        if (IsEmpty)
        {
            clothingSlot.UnequipItem();
        }
        else
        {
            // ENHANCED: Load ItemData and equip with it
            ItemData itemData = null;
            if (!string.IsNullOrEmpty(equippedItemDataName))
            {
                itemData = LoadItemDataByName(equippedItemDataName);
            }

            if (itemData != null)
            {
                clothingSlot.EquipItem(equippedItemId, itemData);
            }
            else
            {
                Debug.LogWarning($"Could not load ItemData '{equippedItemDataName}' for equipped item {equippedItemId}");
                // Fallback: equip without ItemData (may cause issues)
                clothingSlot.EquipItem(equippedItemId);
            }
        }
    }

    /// <summary>
    /// ENHANCED: Load ItemData by name for restoration
    /// </summary>
    private ItemData LoadItemDataByName(string itemDataName)
    {
        if (string.IsNullOrEmpty(itemDataName))
            return null;

        // Try to load from Resources
        string resourcePath = $"{SaveManager.Instance?.itemDataPath ?? "Data/Items/"}{itemDataName}";
        ItemData itemData = Resources.Load<ItemData>(resourcePath);

        if (itemData != null)
            return itemData;

        // Fallback: Search all ItemData assets
        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        foreach (var data in allItemData)
        {
            if (data.name == itemDataName)
                return data;
        }

        return null;
    }

    /// <summary>
    /// Validate that this slot save data is valid
    /// </summary>
    public bool IsValid()
    {
        // Layer must be a valid enum value
        if (!System.Enum.IsDefined(typeof(ClothingLayer), layer))
            return false;

        // If equipped item ID is not empty, it should be a valid format
        if (!string.IsNullOrEmpty(equippedItemId) && equippedItemId.Trim().Length == 0)
            return false;

        // ENHANCED: Check ItemData name consistency
        if (!string.IsNullOrEmpty(equippedItemId) && string.IsNullOrEmpty(equippedItemDataName))
        {
            Debug.LogWarning($"Equipped item {equippedItemId} has no ItemData name - may cause restoration issues");
        }

        return true;
    }

    /// <summary>
    /// ENHANCED: Get a debug string representation with ItemData info
    /// </summary>
    public override string ToString()
    {
        if (IsEmpty)
            return $"Slot[{layer}] = Empty";

        string itemDataInfo = !string.IsNullOrEmpty(equippedItemDataName)
            ? $" ({equippedItemDataName})"
            : "";
        return $"Slot[{layer}] = {equippedItemId}{itemDataInfo}";
    }

    /// <summary>
    /// ENHANCED: Creates a copy of this slot save data
    /// </summary>
    public ClothingSlotSaveData CreateCopy()
    {
        return new ClothingSlotSaveData
        {
            layer = this.layer,
            equippedItemId = this.equippedItemId,
            equippedItemDataName = this.equippedItemDataName
        };
    }
}