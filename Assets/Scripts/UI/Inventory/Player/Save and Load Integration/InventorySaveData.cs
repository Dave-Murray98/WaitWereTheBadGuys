using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Complete inventory state for saving/loading
/// </summary>
[System.Serializable]
public class InventorySaveData
{
    [Header("Grid Configuration")]
    public int gridWidth;
    public int gridHeight;

    [Header("Inventory Items")]
    public List<InventoryItemSaveData> items = new List<InventoryItemSaveData>();

    [Header("Metadata")]
    public int nextItemId = 1; // Track the next available item ID

    public InventorySaveData()
    {
        items = new List<InventoryItemSaveData>();
    }

    public InventorySaveData(int width, int height)
    {
        gridWidth = width;
        gridHeight = height;
        items = new List<InventoryItemSaveData>();
        nextItemId = 1;
    }

    /// <summary>
    /// Add an item to the save data
    /// </summary>
    public void AddItem(InventoryItemSaveData itemData)
    {
        if (itemData != null)
        {
            items.Add(itemData);
        }
    }

    /// <summary>
    /// Remove an item from save data by ID
    /// </summary>
    public bool RemoveItem(string itemID)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i].itemID == itemID)
            {
                items.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get item save data by ID
    /// </summary>
    public InventoryItemSaveData GetItem(string itemID)
    {
        foreach (var item in items)
        {
            if (item.itemID == itemID)
                return item;
        }
        return null;
    }

    /// <summary>
    /// Clear all items
    /// </summary>
    public void Clear()
    {
        items.Clear();
        nextItemId = 1;
    }

    /// <summary>
    /// Get total number of items
    /// </summary>
    public int ItemCount => items.Count;

    /// <summary>
    /// Validate that the save data is consistent
    /// </summary>
    public bool IsValid()
    {
        // Check grid dimensions
        if (gridWidth <= 0 || gridHeight <= 0)
            return false;

        // Check for duplicate item IDs
        var seenIDs = new HashSet<string>();
        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.itemID) || seenIDs.Contains(item.itemID))
                return false;
            seenIDs.Add(item.itemID);
        }

        return true;
    }
}