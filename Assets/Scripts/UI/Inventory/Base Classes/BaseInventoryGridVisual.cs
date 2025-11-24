using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Sirenix.OdinInspector;

[RequireComponent(typeof(RectTransform))]

/// <summary>
/// Abstract base class for inventory grid visuals.
/// Handles core visual functionality that can be extended for different inventory types.
/// Provides a clean separation between data and visual representation.
/// </summary>
public abstract class BaseInventoryGridVisual : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] protected float cellSize = 50f;
    [SerializeField] protected float cellSpacing = 2f;
    [SerializeField] protected Color gridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    [SerializeField] protected Color validPreviewColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] protected Color invalidPreviewColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("Prefabs")]
    [SerializeField] protected GameObject itemVisualPrefab;
    [SerializeField] protected GameObject previewCellPrefab;

    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;

    // Core components
    protected RectTransform rectTransform;
    protected Dictionary<string, GameObject> itemVisuals = new Dictionary<string, GameObject>();
    protected List<GameObject> previewCells = new List<GameObject>();
    protected List<Image> gridLines = new List<Image>();

    // Data reference - will be set by derived classes
    protected BaseInventoryManager inventoryManager;
    protected InventoryGridData currentGridData;

    // Properties
    public float CellSize => cellSize;
    public float CellSpacing => cellSpacing;
    public InventoryGridData GridData => currentGridData;
    public BaseInventoryManager InventoryManager => inventoryManager;

    #region Lifecycle

    protected virtual void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        DebugLog("Awake called");

        CreatePreviewCellPrefab();
    }

    protected virtual void Start()
    {
        InitializeFromInventoryManager();
    }

    protected virtual void OnEnable()
    {
        SubscribeToDataEvents();
        RefreshFromInventoryManager();
    }

    protected virtual void OnDisable()
    {
        UnsubscribeFromDataEvents();
    }

    protected virtual void OnDestroy()
    {
        UnsubscribeFromDataEvents();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Handle item removed event from inventory manager.
    /// </summary>
    protected virtual void OnItemRemoved(string itemId)
    {
        RemoveItemVisual(itemId);
    }

    /// <summary>
    /// Handle inventory cleared event from inventory manager.
    /// </summary>
    protected virtual void OnInventoryCleared()
    {
        ClearAllItemVisuals();
    }

    /// <summary>
    /// Handle inventory data changed event from inventory manager.
    /// </summary>
    protected virtual void OnInventoryDataChanged(InventoryGridData newData)
    {
        currentGridData = newData;
        // Derived classes can add additional logic here
    }

    #endregion

    #region Public Interface Methods

    /// <summary>
    /// Refresh the visual from the current inventory manager.
    /// </summary>
    protected virtual void RefreshFromInventoryManager()
    {
        if (inventoryManager != null)
        {
            currentGridData = inventoryManager.InventoryGridData;
            RefreshAllVisuals();
        }
    }

    /// <summary>
    /// Try to add an item at the specified position. Delegates to inventory manager.
    /// </summary>
    public virtual bool TryAddItemAt(ItemData itemData, Vector2Int position)
    {
        return inventoryManager?.AddItem(itemData, position) ?? false;
    }

    /// <summary>
    /// Try to move an item to a new position. Delegates to inventory manager.
    /// </summary>
    public virtual bool TryMoveItem(string itemId, Vector2Int newPosition)
    {
        return inventoryManager?.MoveItem(itemId, newPosition) ?? false;
    }

    /// <summary>
    /// Try to rotate an item. Delegates to inventory manager.
    /// </summary>
    public virtual bool TryRotateItem(string itemId)
    {
        return inventoryManager?.RotateItem(itemId) ?? false;
    }

    #endregion

    #region Debug Helpers

    protected void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[{GetType().Name}] {message}");
        }
    }

    protected void DebugLogWarning(string message)
    {
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[{GetType().Name}] {message}");
        }
    }

    [Button("Force Refresh Visuals")]
    public virtual void ForceRefreshVisuals()
    {
        RefreshAllVisuals();
        Debug.Log($"[{GetType().Name}] Forced visual refresh");
    }

    [Button("Debug Visual State")]
    protected virtual void DebugVisualState()
    {
        Debug.Log($"=== {GetType().Name} Visual State ===");
        Debug.Log($"Grid Size: {currentGridData?.Width ?? 0}x{currentGridData?.Height ?? 0}");
        Debug.Log($"Cell Size: {cellSize}, Spacing: {cellSpacing}");
        Debug.Log($"Item Visuals: {itemVisuals.Count}");
        Debug.Log($"Preview Cells: {previewCells.Count}");
        Debug.Log($"Grid Lines: {gridLines.Count}");

        foreach (var kvp in itemVisuals)
        {
            Debug.Log($"  Visual {kvp.Key}: {(kvp.Value != null ? "Active" : "NULL")}");
        }
    }

    #endregion

    /// Initialize the visual from the inventory manager. Must be implemented by derived classes.
    /// </summary>
    protected abstract void InitializeFromInventoryManager();

    /// <summary>
    /// Set the inventory manager this visual should represent.
    /// Called by derived classes to establish the data connection.
    /// </summary>
    protected virtual void SetInventoryManager(BaseInventoryManager manager)
    {
        if (inventoryManager != null)
        {
            UnsubscribeFromDataEvents();
        }

        inventoryManager = manager;
        currentGridData = manager?.InventoryGridData;

        if (inventoryManager != null)
        {
            DebugLog($"Connected to inventory manager: {inventoryManager.GetType().Name}");
            SetupGrid();
            SubscribeToDataEvents();
            RefreshAllVisuals();
        }
    }

    /// <summary>
    /// Subscribe to inventory manager events. Can be overridden for additional events.
    /// </summary>
    protected virtual void SubscribeToDataEvents()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnItemAdded -= OnItemAdded;
            inventoryManager.OnItemRemoved -= OnItemRemoved;
            inventoryManager.OnInventoryCleared -= OnInventoryCleared;
            inventoryManager.OnInventoryDataChanged -= OnInventoryDataChanged;

            inventoryManager.OnItemAdded += OnItemAdded;
            inventoryManager.OnItemRemoved += OnItemRemoved;
            inventoryManager.OnInventoryCleared += OnInventoryCleared;
            inventoryManager.OnInventoryDataChanged += OnInventoryDataChanged;

            DebugLog("Subscribed to inventory manager events");
        }
    }

    /// <summary>
    /// Unsubscribe from inventory manager events.
    /// </summary>
    protected virtual void UnsubscribeFromDataEvents()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnItemAdded -= OnItemAdded;
            inventoryManager.OnItemRemoved -= OnItemRemoved;
            inventoryManager.OnInventoryCleared -= OnInventoryCleared;
            inventoryManager.OnInventoryDataChanged -= OnInventoryDataChanged;

            DebugLog("Unsubscribed from inventory manager events");
        }
    }

    #region Grid Setup

    /// <summary>
    /// Set up the visual grid based on current data.
    /// </summary>
    protected virtual void SetupGrid()
    {
        if (currentGridData == null) return;

        SetupGridSize();
        CreateGridLines();
        DebugLog($"Grid setup complete: {currentGridData.Width}x{currentGridData.Height}");
    }

    /// <summary>
    /// Configure the grid size based on cell dimensions.
    /// </summary>
    protected virtual void SetupGridSize()
    {
        float totalWidth = currentGridData.Width * cellSize + (currentGridData.Width - 1) * cellSpacing;
        float totalHeight = currentGridData.Height * cellSize + (currentGridData.Height - 1) * cellSpacing;

        if (currentGridData == null)
            DebugLogWarning("No grid data found");
        else
            DebugLog($"Grid size: {currentGridData.Width}x{currentGridData.Height}");

        if (rectTransform == null)
            DebugLogWarning("No rect transform found");


        rectTransform.sizeDelta = new Vector2(totalWidth, totalHeight);
    }

    /// <summary>
    /// Create the visual grid lines.
    /// </summary>
    protected virtual void CreateGridLines()
    {
        // Clear existing grid lines
        foreach (var line in gridLines)
        {
            if (line != null)
                DestroyImmediate(line.gameObject);
        }
        gridLines.Clear();

        if (currentGridData == null) return;

        // Create vertical lines
        for (int x = 0; x <= currentGridData.Width; x++)
        {
            CreateGridLine(
                new Vector2(x * (cellSize + cellSpacing) - cellSpacing * 0.5f, 0),
                new Vector2(1, currentGridData.Height * cellSize + (currentGridData.Height - 1) * cellSpacing)
            );
        }

        // Create horizontal lines
        for (int y = 0; y <= currentGridData.Height; y++)
        {
            CreateGridLine(
                new Vector2(0, -y * (cellSize + cellSpacing) + cellSpacing * 0.5f),
                new Vector2(currentGridData.Width * cellSize + (currentGridData.Width - 1) * cellSpacing, 1)
            );
        }
    }

    /// <summary>
    /// Create a single grid line.
    /// </summary>
    protected virtual void CreateGridLine(Vector2 position, Vector2 size)
    {
        GameObject lineObj = new GameObject("GridLine");
        lineObj.transform.SetParent(transform, false);

        RectTransform lineRect = lineObj.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0, 1);
        lineRect.anchorMax = new Vector2(0, 1);
        lineRect.pivot = new Vector2(0, 1);
        lineRect.anchoredPosition = position;
        lineRect.sizeDelta = size;

        Image lineImage = lineObj.AddComponent<Image>();
        lineImage.color = gridLineColor;
        lineImage.raycastTarget = false;

        gridLines.Add(lineImage);
    }

    #endregion

    #region Preview Cell Management

    /// <summary>
    /// Create the preview cell prefab if it doesn't exist.
    /// </summary>
    protected virtual void CreatePreviewCellPrefab()
    {
        if (previewCellPrefab == null)
        {
            GameObject cell = new GameObject("PreviewCell");
            RectTransform cellRect = cell.AddComponent<RectTransform>();
            cellRect.sizeDelta = new Vector2(cellSize, cellSize);
            cellRect.anchorMin = new Vector2(0, 1);
            cellRect.anchorMax = new Vector2(0, 1);
            cellRect.pivot = new Vector2(0, 1);

            Image cellImage = cell.AddComponent<Image>();
            cellImage.color = validPreviewColor;
            cellImage.raycastTarget = false;

            previewCellPrefab = cell;
        }
    }

    /// <summary>
    /// Show placement preview for an item at the specified position.
    /// </summary>
    public virtual void ShowPlacementPreview(Vector2Int gridPosition, InventoryItemData item, bool isValid)
    {
        ClearPlacementPreview();

        var previewPositions = item.GetOccupiedPositionsAt(gridPosition);
        Color previewColor = isValid ? validPreviewColor : invalidPreviewColor;

        foreach (var pos in previewPositions)
        {
            if (pos.x >= 0 && pos.x < currentGridData.Width && pos.y >= 0 && pos.y < currentGridData.Height)
            {
                GameObject previewCell = Instantiate(previewCellPrefab, transform);
                previewCell.name = $"Preview_{pos.x}_{pos.y}";

                RectTransform cellRect = previewCell.GetComponent<RectTransform>();
                cellRect.anchorMin = new Vector2(0, 1);
                cellRect.anchorMax = new Vector2(0, 1);
                cellRect.pivot = new Vector2(0, 1);

                Vector2 cellPos = GetCellWorldPosition(pos.x, pos.y);
                cellRect.anchoredPosition = cellPos;
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);

                Image cellImage = previewCell.GetComponent<Image>();
                cellImage.color = previewColor;
                cellImage.raycastTarget = false;

                previewCells.Add(previewCell);
            }
        }
    }

    /// <summary>
    /// Clear all placement preview cells.
    /// </summary>
    public virtual void ClearPlacementPreview()
    {
        foreach (var cell in previewCells)
        {
            if (cell != null)
                Destroy(cell);
        }
        previewCells.Clear();
    }

    #endregion

    #region Coordinate Conversion

    /// <summary>
    /// Convert grid coordinates to world position.
    /// </summary>
    public virtual Vector2 GetCellWorldPosition(int x, int y)
    {
        return new Vector2(
            x * (cellSize + cellSpacing),
            -y * (cellSize + cellSpacing)
        );
    }

    /// <summary>
    /// Convert local position to grid coordinates.
    /// </summary>
    public virtual Vector2Int GetGridPosition(Vector2 localPosition)
    {
        int gridX = Mathf.FloorToInt(localPosition.x / (cellSize + cellSpacing));
        int gridY = Mathf.FloorToInt(-localPosition.y / (cellSize + cellSpacing));
        return new Vector2Int(gridX, gridY);
    }

    #endregion

    #region Item Visual Management

    /// <summary>
    /// Create visual representation for an item. Can be overridden for custom visuals.
    /// </summary>
    protected virtual void CreateItemVisual(InventoryItemData item)
    {
        if (itemVisuals.ContainsKey(item.ID))
        {
            DebugLogWarning($"Visual already exists for item {item.ID}");
            return;
        }

        GameObject itemObj = CreateItemVisualGameObject(item);
        InitializeItemVisualComponents(itemObj, item);

        itemVisuals[item.ID] = itemObj;
        DebugLog($"Created visual for item: {item.ItemData?.itemName}");
    }

    /// <summary>
    /// Create the GameObject for an item visual. Can be overridden for custom prefabs.
    /// </summary>
    protected virtual GameObject CreateItemVisualGameObject(InventoryItemData item)
    {
        GameObject itemObj;

        if (itemVisualPrefab != null)
        {
            itemObj = Instantiate(itemVisualPrefab, transform);
        }
        else
        {
            itemObj = new GameObject($"Item_{item.ID}");
            itemObj.transform.SetParent(transform, false);
            itemObj.AddComponent<RectTransform>();
            itemObj.AddComponent<InventoryItemVisualRenderer>();

            // Add drag handler - derived classes can override this behavior
            if (ShouldAddDragHandler(item))
            {
                itemObj.AddComponent<PlayerInventoryItemDragHandler>();
            }
        }

        return itemObj;
    }

    /// <summary>
    /// Initialize the visual components of an item GameObject.
    /// Can be overridden by derived classes for custom initialization.
    /// </summary>
    protected virtual void InitializeItemVisualComponents(GameObject itemObj, InventoryItemData item)
    {
        // Initialize visual renderer
        var renderer = itemObj.GetComponent<InventoryItemVisualRenderer>();
        if (renderer != null)
        {
            renderer.Initialize(item, this);
        }

        // Initialize drag handler if present
        var dragHandler = itemObj.GetComponent<PlayerInventoryItemDragHandler>();
        if (dragHandler != null)
        {
            InitializeDragHandler(dragHandler, item);
        }
    }

    /// <summary>
    /// Initialize drag handler. Can be overridden for custom drag behavior.
    /// </summary>
    protected virtual void InitializeDragHandler(BaseInventoryDragHandler dragHandler, InventoryItemData item)
    {
        dragHandler.Initialize(item, this);

        // Register with stats display if available
        ItemStatsDisplay.AutoRegisterNewDragHandler(dragHandler);
    }

    /// <summary>
    /// Determine if drag handler should be added to item visual.
    /// Can be overridden by derived classes to control drag behavior.
    /// </summary>
    protected virtual bool ShouldAddDragHandler(InventoryItemData item)
    {
        return true; // Default: all items are draggable
    }

    /// <summary>
    /// Remove visual representation for an item.
    /// </summary>
    protected virtual void RemoveItemVisual(string itemId)
    {
        DebugLog($"Removing visual for item: {itemId}");

        if (itemVisuals.ContainsKey(itemId))
        {
            if (itemVisuals[itemId] != null)
            {
                Destroy(itemVisuals[itemId]);
            }
            itemVisuals.Remove(itemId);
            DebugLog($"Visual removed for item: {itemId}");
        }
        else
        {
            DebugLog($"No visual found for item: {itemId}");
        }
    }

    /// <summary>
    /// Clear all item visuals.
    /// </summary>
    protected virtual void ClearAllItemVisuals()
    {
        foreach (var visual in itemVisuals.Values)
        {
            if (visual != null)
                Destroy(visual);
        }
        itemVisuals.Clear();
        DebugLog("All item visuals cleared");
    }

    /// <summary>
    /// Refresh all item visuals from current data.
    /// </summary>
    protected virtual void RefreshAllVisuals()
    {
        ClearAllItemVisuals();

        if (currentGridData == null) return;

        var allItems = currentGridData.GetAllItems();
        foreach (var item in allItems)
        {
            CreateItemVisual(item);
        }

        DebugLog($"Refreshed visuals for {allItems.Count} items");
    }

    #endregion

    #region Data Event Handlers

    /// <summary>
    /// Handle item added event from inventory manager.
    /// </summary>
    protected virtual void OnItemAdded(InventoryItemData item)
    {
        CreateItemVisual(item);
    }

    #endregion
}
