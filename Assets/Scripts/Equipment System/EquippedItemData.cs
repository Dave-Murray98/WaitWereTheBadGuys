using UnityEngine;

/// <summary>
/// Data for currently equipped item
/// FIXED: Added copy constructor for scene transitions
/// </summary>
[System.Serializable]
public class EquippedItemData
{
    [Header("Equipment State")]
    public bool isEquipped;
    public string equippedItemId;
    public string equippedItemDataName; // For persistence and fallback lookup

    [Header("Source Info")]
    public bool isEquippedFromHotkey;
    public int sourceHotkeySlot = -1;

    // Default constructor
    public EquippedItemData()
    {
        Clear();
    }

    // CRITICAL FIX: Copy constructor for scene transitions
    public EquippedItemData(EquippedItemData other)
    {
        isEquipped = other.isEquipped;
        equippedItemId = other.equippedItemId;
        equippedItemDataName = other.equippedItemDataName; // ← CRITICAL: Don't forget this!
        isEquippedFromHotkey = other.isEquippedFromHotkey;
        sourceHotkeySlot = other.sourceHotkeySlot;

        // Debug log to verify copy worked
        if (isEquipped)
        {
            Debug.Log($"[EquippedItemData] Copy constructor: Copied equipped item {equippedItemDataName} (ID: {equippedItemId})");
        }
    }

    /// <summary>
    /// Clear equipped item
    /// </summary>
    public void Clear()
    {
        isEquipped = false;
        equippedItemId = "";
        equippedItemDataName = ""; // ← Clear this too
        isEquippedFromHotkey = false;
        sourceHotkeySlot = -1;
    }

    /// <summary>
    /// Equip item from inventory
    /// </summary>
    public void EquipFromInventory(string itemId, ItemData itemData)
    {
        equippedItemId = itemId;
        equippedItemDataName = itemData.name; // ← Set the data name
        isEquipped = true;
        isEquippedFromHotkey = false;
        sourceHotkeySlot = -1;
    }

    /// <summary>
    /// Equip item from hotkey
    /// </summary>
    public void EquipFromHotkey(string itemId, ItemData itemData, int hotkeySlot)
    {
        equippedItemId = itemId;
        equippedItemDataName = itemData.name; // ← Set the data name
        isEquipped = true;
        isEquippedFromHotkey = true;
        sourceHotkeySlot = hotkeySlot;
    }

    /// <summary>
    /// Get the ItemData for the equipped item
    /// </summary>
    public ItemData GetItemData()
    {
        if (!isEquipped || string.IsNullOrEmpty(equippedItemDataName))
            return null;

        // First try to get from current inventory item
        if (PlayerInventoryManager.Instance != null)
        {
            var inventoryItem = PlayerInventoryManager.Instance.InventoryGridData.GetItem(equippedItemId);
            if (inventoryItem?.ItemData != null)
            {
                return inventoryItem.ItemData;
            }
        }

        // Fallback to Resources load using the saved data name
        return Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + equippedItemDataName);
    }

    /// <summary>
    /// Check if this specific item is equipped
    /// </summary>
    public bool IsEquipped(string itemId)
    {
        return isEquipped && equippedItemId == itemId;
    }
}