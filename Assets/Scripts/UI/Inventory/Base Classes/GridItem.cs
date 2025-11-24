using UnityEngine;

[System.Serializable]
public class GridItem
{
    [Header("Basic Properties")]
    public string ID;
    public Vector2Int GridPosition;

    [Header("Item Data")]
    public ItemData itemData; // New: Reference to ScriptableObject

    [Header("Shape Properties")]
    public TetrominoType shapeType;
    public int currentRotation = 0;

    // Cached data for performance
    private TetrominoData _currentShapeData;
    private bool _dataCached = false;


    // New constructor with ItemData
    public GridItem(string id, ItemData data, Vector2Int gridPosition)
    {
        ID = id;
        itemData = data;
        shapeType = data.shapeType;
        GridPosition = gridPosition;
        currentRotation = 0;
        RefreshShapeData();
        //        Debug.Log($"Created GridItem with ID: {ID}, ShapeType: {shapeType}, Position: {GridPosition}");
    }

    // Get item name (from ItemData if available, otherwise default)
    public string ItemName
    {
        get
        {
            if (itemData != null)
                return itemData.itemName;
            return $"Item_{ID}";
        }
    }

    // Get item description
    public string Description
    {
        get
        {
            if (itemData != null)
                return itemData.description;
            return string.Empty;
        }
    }

    // Get item sprite
    public Sprite ItemSprite
    {
        get
        {
            if (itemData != null)
                return itemData.itemSprite;
            return null;
        }
    }

    // Get sprite scale
    public float SpriteScaleX
    {
        get
        {
            if (itemData != null)
                return itemData.spriteScaleX;
            return 1.0f;
        }
    }

    public float SpriteScaleY
    {
        get
        {
            if (itemData != null)
                return itemData.spriteScaleY;
            return 1.0f;
        }
    }

    // Get sprite offset
    public Vector2 SpriteOffset
    {
        get
        {
            if (itemData != null)
                return itemData.spritePositionOffset;
            return Vector2.zero;
        }
    }

    // Check if item can rotate
    public bool CanRotate
    {
        get
        {
            if (itemData != null)
                return itemData.isRotatable && TetrominoDefinitions.GetRotationCount(shapeType) > 1;
            return TetrominoDefinitions.GetRotationCount(shapeType) > 1;
        }
    }

    // Get current shape data (cached)
    public TetrominoData CurrentShapeData
    {
        get
        {
            if (!_dataCached)
                RefreshShapeData();
            return _currentShapeData;
        }
    }

    // Get all occupied grid positions relative to GridPosition
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

    // Get occupied positions for a specific grid position (for validation)
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

    // Get occupied positions for a specific rotation (for validation)
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

    // Rotate the item (cycles through all available rotations)
    public void RotateItem()
    {
        if (!CanRotate) return;

        int maxRotations = TetrominoDefinitions.GetRotationCount(shapeType);
        currentRotation = (currentRotation + 1) % maxRotations;
        RefreshShapeData();
    }

    // Set specific rotation
    public void SetRotation(int rotation)
    {
        int maxRotations = TetrominoDefinitions.GetRotationCount(shapeType);
        currentRotation = Mathf.Clamp(rotation, 0, maxRotations - 1);
        RefreshShapeData();
    }

    // Set grid position
    public void SetGridPosition(Vector2Int position)
    {
        GridPosition = position;
    }

    // Get bounding box of the shape
    public Vector2Int GetBoundingSize()
    {
        var shapeData = CurrentShapeData;
        if (shapeData.cells.Length == 0)
            return Vector2Int.one;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var cell in shapeData.cells)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        return new Vector2Int(maxX - minX + 1, maxY - minY + 1);
    }

    // Get the color of this item (from ItemData if available, otherwise fallback to shape color)
    public Color ItemColor
    {
        get
        {
            return itemData.CellColor;
        }
    }

    // Check if item is currently rotated from its base state
    public bool IsRotated => currentRotation != 0;

    // Refresh cached shape data
    private void RefreshShapeData()
    {
        _currentShapeData = TetrominoDefinitions.GetRotationState(shapeType, currentRotation);
        _dataCached = true;
    }
}