using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure data representation of the tetris-style inventory grid without any visual dependencies.
/// Handles collision detection, item placement validation, and grid state management.
/// Can persist across scenes and be serialized/deserialized without requiring active UI.
/// </summary>
[System.Serializable]
public class InventoryGridData
{
    [SerializeField] private string[,] grid;
    [SerializeField] private Dictionary<string, InventoryItemData> items;
    [SerializeField] private int gridWidth;
    [SerializeField] private int gridHeight;

    public int Width => gridWidth;
    public int Height => gridHeight;
    public int ItemCount => items?.Count ?? 0;

    public InventoryGridData(int width, int height)
    {
        gridWidth = width;
        gridHeight = height;
        grid = new string[width, height];
        items = new Dictionary<string, InventoryItemData>();

        // Initialize grid with null values
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = null;
            }
        }
    }

    /// <summary>
    /// Tests if an item can be placed at the specified position with its current rotation.
    /// Checks grid boundaries and collisions with other items.
    /// </summary>
    public bool IsValidPosition(Vector2Int position, InventoryItemData item)
    {
        return IsValidPosition(position, item, item.currentRotation);
    }

    /// <summary>
    /// Tests if an item can be placed at the specified position with a specific rotation.
    /// Used for rotation validation before applying changes.
    /// </summary>
    public bool IsValidPosition(Vector2Int position, InventoryItemData item, int rotation)
    {
        var occupiedPositions = item.GetOccupiedPositionsAt(position, rotation);

        foreach (var pos in occupiedPositions)
        {
            // Check grid boundaries
            if (pos.x < 0 || pos.x >= gridWidth || pos.y < 0 || pos.y >= gridHeight)
            {
                //  Debug.LogWarning($"Item {item.ID} cannot be placed at {position} with rotation {rotation} (out of bounds)");
                return false;
            }

            // Check collision with other items (ignore self)
            if (grid[pos.x, pos.y] != null && grid[pos.x, pos.y] != item.ID)
            {
                //                Debug.LogWarning($"Item {item.ID} cannot be placed at {position} with rotation {rotation} (collision with {grid[pos.x, pos.y]} at {pos})");
                return false;
            }
        }

        //Debug.Log($"Item {item.ID} can be placed at {position} with rotation {rotation}");
        return true;
    }

    /// <summary>
    /// Places an item in the grid at its current position.
    /// Updates both the spatial grid and item dictionary.
    /// </summary>
    public bool PlaceItem(InventoryItemData item)
    {
        if (!IsValidPosition(item.GridPosition, item))
            return false;

        // Remove if already exists (for repositioning)
        if (items.ContainsKey(item.ID))
            RemoveItem(item.ID);

        // Place in grid
        var occupiedPositions = item.GetOccupiedPositions();
        foreach (var pos in occupiedPositions)
        {
            grid[pos.x, pos.y] = item.ID;
        }

        items[item.ID] = item;
        return true;
    }

    /// <summary>
    /// Removes an item from the grid by ID, freeing up its occupied cells.
    /// </summary>
    public bool RemoveItem(string itemID)
    {
        if (!items.ContainsKey(itemID))
        {
            DebugLog($"RemoveItem failed: Item {itemID} not found in inventory");
            return false;
        }

        var item = items[itemID];
        var occupiedPositions = item.GetOccupiedPositions();

        // Clear grid cells
        foreach (var pos in occupiedPositions)
        {
            if (pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight)
            {
                grid[pos.x, pos.y] = null;
            }
        }

        items.Remove(itemID);

        DebugLog($"Item {itemID} removed from inventory grid");

        return true;
    }

    /// <summary>
    /// Gets the item occupying the specified grid cell, or null if empty.
    /// </summary>
    public InventoryItemData GetItemAt(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
            return null;

        string itemID = grid[x, y];
        if (itemID == null)
            return null;

        return items.ContainsKey(itemID) ? items[itemID] : null;
    }

    /// <summary>
    /// Gets an item by its unique ID.
    /// </summary>
    public InventoryItemData GetItem(string itemID)
    {
        if (items.ContainsKey(itemID))
        {
            return items[itemID];
        }
        return null;
    }

    /// <summary>
    /// Returns a list of all items in the inventory.
    /// </summary>
    public List<InventoryItemData> GetAllItems()
    {
        return new List<InventoryItemData>(items.Values);
    }

    /// <summary>
    /// Checks if a specific grid cell is occupied by any item.
    /// </summary>
    public bool IsOccupied(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
            return true;

        return grid[x, y] != null;
    }

    /// <summary>
    /// Clears all items from the grid.
    /// </summary>
    public void Clear()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                grid[x, y] = null;
            }
        }
        items.Clear();
    }

    /// <summary>
    /// Finds the first valid position where an item can be placed.
    /// Searches left-to-right, top-to-bottom starting from the specified position.
    /// </summary>
    public Vector2Int? FindValidPositionForItem(InventoryItemData item, int startX = 0, int startY = 0)
    {
        for (int y = startY; y < gridHeight; y++)
        {
            for (int x = (y == startY ? startX : 0); x < gridWidth; x++)
            {
                Vector2Int testPos = new Vector2Int(x, y);
                if (IsValidPosition(testPos, item))
                {
                    return testPos;
                }
            }
        }
        return null;
    }


    #region Debug

    private void DebugLog(string message)
    {
        if (PlayerInventoryManager.Instance.enableDebugLogs)
        {
            Debug.Log($"[InventoryGridData] {message}");
        }
    }

    #endregion
}
