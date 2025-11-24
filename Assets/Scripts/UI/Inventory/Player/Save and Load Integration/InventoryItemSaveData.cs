using UnityEngine;

/// <summary>
/// Save data for an individual inventory item
/// </summary>
[System.Serializable]
public class InventoryItemSaveData
{
    [Header("Item Identity")]
    public string itemID;
    public string itemDataName; // Name of the ItemData ScriptableObject

    [Header("Grid Position")]
    public Vector2Int gridPosition;
    public int currentRotation;

    [Header("Item State")]
    public int stackCount = 1; // For future stacking support

    public InventoryItemSaveData()
    {
        // Default constructor
    }

    public InventoryItemSaveData(string id, string dataName, Vector2Int position, int rotation)
    {
        itemID = id;
        itemDataName = dataName;
        gridPosition = position;
        currentRotation = rotation;
        stackCount = 1;
    }

    /// <summary>
    /// Create save data from a GridItem
    /// </summary>
    public static InventoryItemSaveData FromGridItem(GridItem gridItem)
    {
        if (gridItem?.itemData == null)
        {
            Debug.LogError("Cannot create InventoryItemSaveData - GridItem or ItemData is null");
            return null;
        }

        return new InventoryItemSaveData(
            gridItem.ID,
            gridItem.itemData.name, // Use ScriptableObject name
            gridItem.GridPosition,
            gridItem.currentRotation
        );
    }

    /// <summary>
    /// Create a GridItem from this save data
    /// </summary>
    public GridItem ToGridItem()
    {
        // Find the ItemData by name
        ItemData itemData = FindItemDataByName(SaveManager.Instance.itemDataPath + itemDataName);
        if (itemData == null)
        {
            Debug.LogError($"Could not find ItemData with name: {itemDataName}");
            return null;
        }
        Debug.Log("ToGridItem called for item: " + itemDataName);
        // Create the GridItem
        GridItem gridItem = new GridItem(itemID, itemData, gridPosition);
        gridItem.SetRotation(currentRotation);

        return gridItem;
    }

    /// <summary>
    /// Find ItemData ScriptableObject by name
    /// </summary>
    private ItemData FindItemDataByName(string dataName)
    {
        // First, try to load from Resources
        ItemData itemData = Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + dataName);
        if (itemData != null)
            return itemData;

        // If not found in Resources, search all loaded ItemData assets
        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        foreach (var data in allItemData)
        {
            if (data.name == dataName)
                return data;
        }

        Debug.LogWarning($"ItemData '{dataName}' not found. Make sure it exists in Resources folder or is loaded in memory.");
        return null;
    }

    /// <summary>
    /// Validate that this item save data is valid
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(itemID) &&
               !string.IsNullOrEmpty(itemDataName) &&
               stackCount > 0;
    }

    /// <summary>
    /// Get a debug string representation
    /// </summary>
    public override string ToString()
    {
        return $"Item[{itemID}] {itemDataName} at {gridPosition} rot:{currentRotation}";
    }
}