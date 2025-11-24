using UnityEngine;
using System;


/// <summary>
/// Data representation of an inventory item without visual dependencies.
/// Stores position, rotation, and shape information for tetris-style placement.
/// Links to ItemData ScriptableObjects via name for persistence compatibility.
/// </summary>
[System.Serializable]
public class InventoryItemData
{
    [Header("Basic Properties")]
    public string ID;
    public Vector2Int GridPosition;

    [Header("Item Data Reference")]
    public string itemDataName; // Name of the ItemData ScriptableObject for persistence
    [NonSerialized] private ItemData _cachedItemData; // Runtime cache for performance

    [Header("Shape and State")]
    public TetrominoType shapeType;
    public int currentRotation = 0;
    public int stackCount = 1;

    // Cached shape data for performance
    private TetrominoData _currentShapeData;
    private bool _dataCached = false;

    public InventoryItemData(string id, ItemData itemData, Vector2Int gridPosition)
    {
        ID = id;
        itemDataName = itemData.name;
        _cachedItemData = itemData;
        shapeType = itemData.shapeType;
        GridPosition = gridPosition;
        currentRotation = 0;
        stackCount = 1;
        RefreshShapeData();
    }

    /// <summary>
    /// Gets the ItemData ScriptableObject, using cache or loading from Resources if needed.
    /// </summary>
    public ItemData ItemData
    {
        get
        {
            if (_cachedItemData == null)
            {
                _cachedItemData = FindItemDataByName(itemDataName);
            }
            return _cachedItemData;
        }
    }

    /// <summary>
    /// Gets the current tetris shape data with rotation applied.
    /// </summary>
    public TetrominoData CurrentShapeData
    {
        get
        {
            if (!_dataCached)
                RefreshShapeData();
            return _currentShapeData;
        }
    }

    /// <summary>
    /// Gets all grid positions occupied by this item at its current location and rotation.
    /// </summary>
    public Vector2Int[] GetOccupiedPositions()
    {
        var shapeData = CurrentShapeData;
        Vector2Int[] positions = new Vector2Int[shapeData.cells.Length];

        for (int i = 0; i < shapeData.cells.Length; i++)
        {
            positions[i] = GridPosition + shapeData.cells[i];
        }

        return positions;
    }

    /// <summary>
    /// Gets all grid positions this item would occupy at a specific location (for placement testing).
    /// </summary>
    public Vector2Int[] GetOccupiedPositionsAt(Vector2Int position)
    {
        var shapeData = CurrentShapeData;
        Vector2Int[] positions = new Vector2Int[shapeData.cells.Length];

        for (int i = 0; i < shapeData.cells.Length; i++)
        {
            positions[i] = position + shapeData.cells[i];
        }

        return positions;
    }

    /// <summary>
    /// Gets all grid positions this item would occupy at a specific location and rotation (for rotation testing).
    /// </summary>
    public Vector2Int[] GetOccupiedPositionsAt(Vector2Int position, int rotation)
    {
        var shapeData = TetrominoDefinitions.GetRotationState(shapeType, rotation);
        Vector2Int[] positions = new Vector2Int[shapeData.cells.Length];

        for (int i = 0; i < shapeData.cells.Length; i++)
        {
            positions[i] = position + shapeData.cells[i];
        }

        return positions;
    }

    /// <summary>
    /// Rotates the item clockwise to the next valid rotation state.
    /// </summary>
    public void RotateItem()
    {
        if (!CanRotate) return;

        int maxRotations = TetrominoDefinitions.GetRotationCount(shapeType);
        currentRotation = (currentRotation + 1) % maxRotations;
        RefreshShapeData();
    }

    /// <summary>
    /// Sets the item to a specific rotation state.
    /// </summary>
    public void SetRotation(int rotation)
    {
        int maxRotations = TetrominoDefinitions.GetRotationCount(shapeType);
        currentRotation = Mathf.Clamp(rotation, 0, maxRotations - 1);
        RefreshShapeData();
    }

    /// <summary>
    /// Updates the item's grid position.
    /// </summary>
    public void SetGridPosition(Vector2Int position)
    {
        GridPosition = position;
    }

    /// <summary>
    /// Checks if this item can be rotated based on its ItemData settings and shape definition.
    /// </summary>
    public bool CanRotate
    {
        get
        {
            var itemData = ItemData;
            if (itemData != null)
                return itemData.isRotatable && TetrominoDefinitions.GetRotationCount(shapeType) > 1;
            return TetrominoDefinitions.GetRotationCount(shapeType) > 1;
        }
    }

    /// <summary>
    /// Refreshes the cached shape data when rotation changes.
    /// </summary>
    private void RefreshShapeData()
    {
        _currentShapeData = TetrominoDefinitions.GetRotationState(shapeType, currentRotation);
        _dataCached = true;
    }

    /// <summary>
    /// Finds ItemData ScriptableObject by name using Resources system.
    /// </summary>
    private ItemData FindItemDataByName(string dataName)
    {
        ItemData itemData = Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + dataName);
        if (itemData != null)
            return itemData;

        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        foreach (var data in allItemData)
        {
            if (data.name == dataName)
                return data;
        }

        Debug.LogWarning($"ItemData '{dataName}' not found. Make sure it exists in Resources folder.");
        return null;
    }

    /// <summary>
    /// Converts this item to save data format for persistence.
    /// </summary>
    public InventoryItemSaveData ToSaveData()
    {
        return new InventoryItemSaveData(ID, itemDataName, GridPosition, currentRotation)
        {
            stackCount = stackCount
        };
    }

    /// <summary>
    /// Creates an InventoryItemData from save data format.
    /// </summary>
    public static InventoryItemData FromSaveData(InventoryItemSaveData saveData)
    {
        var itemData = Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + saveData.itemDataName);
        if (itemData == null)
        {
            ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
            foreach (var data in allItemData)
            {
                if (data.name == saveData.itemDataName)
                {
                    itemData = data;
                    break;
                }
            }
        }

        if (itemData == null)
        {
            Debug.LogError($"Cannot create InventoryItemData - ItemData '{saveData.itemDataName}' not found");
            return null;
        }

        var item = new InventoryItemData(saveData.itemID, itemData, saveData.gridPosition);
        item.SetRotation(saveData.currentRotation);
        item.stackCount = saveData.stackCount;
        return item;
    }
}