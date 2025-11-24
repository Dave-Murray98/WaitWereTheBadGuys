using UnityEngine;

/// <summary>
/// FIXED: Enhanced ClothingSlot that properly stores equipped item data
/// Now stores ItemData reference to fix display issues and swap validation
/// </summary>
[System.Serializable]
public class ClothingSlot
{
    [Header("Slot Configuration")]
    [Tooltip("Which clothing layer this slot represents")]
    public ClothingLayer layer;

    [Tooltip("Display name for this slot in the UI")]
    public string displayName;

    [Header("Current State")]
    [Tooltip("ID of the currently equipped item (empty if none)")]
    public string equippedItemId = "";

    [Header("Equipped Item Data")]
    [Tooltip("ItemData of the currently equipped item")]
    [SerializeField] private ItemData equippedItemData;

    /// <summary>
    /// Checks if this slot is currently empty
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(equippedItemId);

    /// <summary>
    /// Checks if this slot has an item equipped
    /// </summary>
    public bool IsOccupied => !IsEmpty;

    /// <summary>
    /// Constructor for creating a clothing slot
    /// </summary>
    public ClothingSlot(ClothingLayer slotLayer)
    {
        layer = slotLayer;
        displayName = GetDefaultDisplayName(slotLayer);
        equippedItemId = "";
        equippedItemData = null;
    }

    /// <summary>
    /// Constructor with custom display name
    /// </summary>
    public ClothingSlot(ClothingLayer slotLayer, string customDisplayName)
    {
        layer = slotLayer;
        displayName = customDisplayName;
        equippedItemId = "";
        equippedItemData = null;
    }

    /// <summary>
    /// Checks if the specified clothing item can be equipped to this slot
    /// </summary>
    public bool CanEquip(ClothingData clothingData)
    {
        if (clothingData == null)
            return false;

        return System.Array.Exists(clothingData.validLayers, l => l == layer);
    }

    /// <summary>
    /// Checks if the specified inventory item can be equipped to this slot
    /// </summary>
    public bool CanEquip(InventoryItemData inventoryItem)
    {
        if (inventoryItem?.ItemData?.itemType != ItemType.Clothing)
            return false;

        var clothingData = inventoryItem.ItemData.ClothingData;
        return CanEquip(clothingData);
    }

    /// <summary>
    /// ENHANCED: Equips an item to this slot and stores the ItemData reference
    /// </summary>
    public void EquipItem(string itemId, ItemData itemData = null)
    {
        equippedItemId = itemId;

        // Store the ItemData reference for proper UI display and validation
        if (itemData != null)
        {
            equippedItemData = itemData;
        }
        else
        {
            // Try to find the ItemData if not provided
            equippedItemData = FindItemDataById(itemId);
        }

        if (equippedItemData == null)
        {
            Debug.LogWarning($"Could not find ItemData for equipped item: {itemId}");
        }
    }

    /// <summary>
    /// ENHANCED: Unequips the current item from this slot and clears data
    /// </summary>
    public string UnequipItem()
    {
        string previousItemId = equippedItemId;
        equippedItemId = "";
        equippedItemData = null;
        return previousItemId;
    }

    /// <summary>
    /// FIXED: Gets the currently equipped item data
    /// Now creates proper InventoryItemData from stored references
    /// </summary>
    public InventoryItemData GetEquippedItem()
    {
        if (IsEmpty || equippedItemData == null)
            return null;

        // Create InventoryItemData from the stored ItemData
        return new InventoryItemData(equippedItemId, equippedItemData, Vector2Int.zero);
    }

    /// <summary>
    /// Gets the clothing data of the currently equipped item
    /// </summary>
    public ClothingData GetEquippedClothingData()
    {
        if (equippedItemData == null)
            return null;

        return equippedItemData.ClothingData;
    }

    /// <summary>
    /// Gets the display name for the equipped item, or "Empty" if none
    /// </summary>
    public string GetEquippedItemDisplayName()
    {
        if (equippedItemData != null)
            return equippedItemData.itemName;

        return "Empty";
    }

    /// <summary>
    /// Gets the condition percentage of the equipped item (0-1)
    /// </summary>
    public float GetEquippedItemCondition()
    {
        var clothingData = GetEquippedClothingData();
        return clothingData?.ConditionPercentage ?? 0f;
    }

    /// <summary>
    /// Checks if the equipped item is damaged (condition below 50%)
    /// </summary>
    public bool IsEquippedItemDamaged()
    {
        var clothingData = GetEquippedClothingData();
        return clothingData?.IsDamaged ?? false;
    }

    /// <summary>
    /// NEW: Gets the ItemData of the equipped item (for validation and display)
    /// </summary>
    public ItemData GetEquippedItemData()
    {
        return equippedItemData;
    }

    /// <summary>
    /// NEW: Helper method to find ItemData by item ID
    /// </summary>
    private ItemData FindItemDataById(string itemId)
    {
        // First try to find it in Resources
        string itemDataPath = SaveManager.Instance?.itemDataPath ?? "Data/Items/";

        // Load all ItemData assets
        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();

        foreach (var itemData in allItemData)
        {
            // Try different matching strategies
            if (itemData.name == itemId ||
                itemId.Contains(itemData.name) ||
                itemData.name.Contains(itemId.Replace("item_", "")))
            {
                return itemData;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets default display names for each clothing layer
    /// </summary>
    private string GetDefaultDisplayName(ClothingLayer slotLayer)
    {
        return slotLayer switch
        {
            ClothingLayer.HeadUpper => "Head (Upper)",
            ClothingLayer.HeadLower => "Head (Lower)",
            ClothingLayer.TorsoInner => "Torso (Inner)",
            ClothingLayer.TorsoOuter => "Torso (Outer)",
            ClothingLayer.LegsInner => "Legs (Inner)",
            ClothingLayer.LegsOuter => "Legs (Outer)",
            ClothingLayer.Hands => "Hands",
            ClothingLayer.Socks => "Socks",
            ClothingLayer.Shoes => "Shoes",
            _ => slotLayer.ToString()
        };
    }

    /// <summary>
    /// Gets a short display name for UI space constraints
    /// </summary>
    public string GetShortDisplayName()
    {
        return layer switch
        {
            ClothingLayer.HeadUpper => "Hat",
            ClothingLayer.HeadLower => "Scarf",
            ClothingLayer.TorsoInner => "Shirt",
            ClothingLayer.TorsoOuter => "Jacket",
            ClothingLayer.LegsInner => "Underwear",
            ClothingLayer.LegsOuter => "Pants",
            ClothingLayer.Hands => "Gloves",
            ClothingLayer.Socks => "Socks",
            ClothingLayer.Shoes => "Shoes",
            _ => displayName
        };
    }

    /// <summary>
    /// Creates a copy of this clothing slot
    /// </summary>
    public ClothingSlot CreateCopy()
    {
        var copy = new ClothingSlot(layer, displayName);
        copy.equippedItemId = this.equippedItemId;
        copy.equippedItemData = this.equippedItemData;
        return copy;
    }

    /// <summary>
    /// Validates that this slot's state is consistent
    /// </summary>
    public bool IsValid()
    {
        // If we have an equipped item, verify the data is consistent
        if (IsOccupied)
        {
            if (equippedItemData == null)
            {
                Debug.LogWarning($"Clothing slot {layer} has equipped item ID but no ItemData");
                return false;
            }

            // Verify the item can actually be equipped to this slot
            if (equippedItemData.itemType != ItemType.Clothing ||
                equippedItemData.ClothingData == null ||
                !equippedItemData.ClothingData.CanEquipToLayer(layer))
            {
                Debug.LogWarning($"Item {equippedItemId} cannot be equipped to slot {layer}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets debug information about this slot
    /// </summary>
    public string GetDebugInfo()
    {
        if (IsEmpty)
            return $"{layer}: Empty";

        string itemName = equippedItemData?.itemName ?? "UNKNOWN";
        var condition = GetEquippedItemCondition();
        return $"{layer}: {itemName} ({condition:P0} condition)";
    }
}