using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Specialized grid visual for the pickup overflow system.
/// Creates a minimal grid that auto-sizes to perfectly fit the pickup item.
/// Uses simplified styling and prevents normal inventory operations.
/// </summary>
public class PickupOverflowGridVisual : BaseInventoryGridVisual
{
    [Header("Pickup Overflow Visual Settings")]
    [SerializeField] private bool autoConnectToOverflowManager = true;
    [SerializeField] private Color pickupGridLineColor = new Color(0.8f, 0.6f, 0.2f, 0.7f);
    [SerializeField] private Color pickupValidPreviewColor = new Color(0.2f, 1f, 0.2f, 0.5f);
    [SerializeField] private Color pickupInvalidPreviewColor = new Color(1f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private float pickupItemPadding = 5f;

    // Pickup-specific references
    private PickupOverflowManager pickupManager;

    #region Initialization

    /// <summary>
    /// Initialize from the pickup overflow manager.
    /// </summary>
    protected override void InitializeFromInventoryManager()
    {
        if (autoConnectToOverflowManager)
        {
            ConnectToPickupOverflowManager();
        }
        else
        {
            DebugLogWarning("Auto-connect to pickup overflow manager disabled - manual connection required");
        }
    }

    /// <summary>
    /// Connect to the pickup overflow manager.
    /// </summary>
    private void ConnectToPickupOverflowManager()
    {
        // Look for PickupOverflowManager in the scene
        PickupOverflowManager manager = FindFirstObjectByType<PickupOverflowManager>();

        if (manager != null)
        {
            ConnectToSpecificManager(manager);
        }
        else
        {
            DebugLogWarning("PickupOverflowManager not found in scene");
        }
    }

    /// <summary>
    /// Connect to a specific pickup overflow manager.
    /// </summary>
    public void ConnectToSpecificManager(PickupOverflowManager manager)
    {
        if (manager == null)
        {
            DebugLogWarning("Cannot connect to null pickup overflow manager");
            return;
        }

        pickupManager = manager;
        SetInventoryManager(manager);
        ApplyPickupStyling();

        DebugLog($"Connected to pickup overflow manager");
    }

    /// <summary>
    /// Apply pickup-specific visual styling.
    /// </summary>
    private void ApplyPickupStyling()
    {
        // Override base colors with pickup-specific colors
        gridLineColor = pickupGridLineColor;
        validPreviewColor = pickupValidPreviewColor;
        invalidPreviewColor = pickupInvalidPreviewColor;

        // Refresh visuals with new styling
        if (currentGridData != null)
        {
            CreateGridLines();
            RefreshAllVisuals();
        }
    }

    #endregion

    #region Pickup-Specific Overrides

    /// <summary>
    /// Pickup items should have simplified drag handlers.
    /// </summary>
    protected override bool ShouldAddDragHandler(InventoryItemData item)
    {
        // Pickup items are draggable but only for one-way transfer
        return true;
    }

    /// <summary>
    /// Create pickup-specific item visual GameObjects.
    /// </summary>
    protected override GameObject CreateItemVisualGameObject(InventoryItemData item)
    {
        GameObject itemObj;

        if (itemVisualPrefab != null)
        {
            itemObj = Instantiate(itemVisualPrefab, transform);
        }
        else
        {
            // Create pickup-specific item visual
            itemObj = new GameObject($"PickupItem_{item.ID}");
            itemObj.transform.SetParent(transform, false);
            itemObj.AddComponent<RectTransform>();
            itemObj.AddComponent<InventoryItemVisualRenderer>();
            itemObj.AddComponent<PickupOverflowItemDragHandler>(); // Use specialized drag handler
            // Note: No hotkey indicator for pickup items
        }

        return itemObj;
    }

    /// <summary>
    /// Initialize visual components with pickup-specific settings.
    /// </summary>
    protected override void InitializeItemVisualComponents(GameObject itemObj, InventoryItemData item)
    {
        // Initialize visual renderer
        var renderer = itemObj.GetComponent<InventoryItemVisualRenderer>();
        if (renderer != null)
        {
            renderer.Initialize(item, this);
        }

        // Initialize pickup-specific drag handler
        var dragHandler = itemObj.GetComponent<PickupOverflowItemDragHandler>();
        if (dragHandler != null)
        {
            InitializePickupDragHandler(dragHandler, item);
        }
        else
        {
            // Fallback to base drag handler
            var baseDragHandler = itemObj.GetComponent<BaseInventoryDragHandler>();
            if (baseDragHandler != null)
            {
                InitializeDragHandler(baseDragHandler, item);
            }
        }
    }

    /// <summary>
    /// Initialize pickup-specific drag handler.
    /// </summary>
    private void InitializePickupDragHandler(PickupOverflowItemDragHandler dragHandler, InventoryItemData item)
    {
        dragHandler.Initialize(item, this);
        dragHandler.SetPickupManager(pickupManager);

        // Register with stats display
        ItemStatsDisplay.AutoRegisterNewDragHandler(dragHandler);
    }

    #endregion

    #region Grid Sizing

    /// <summary>
    /// Override grid setup to handle dynamic sizing.
    /// </summary>
    protected override void SetupGrid()
    {
        if (currentGridData == null) return;

        SetupOptimalGridSize();
        CreateGridLines();
        DebugLog($"Pickup grid setup complete: {currentGridData.Width}x{currentGridData.Height}");
    }

    /// <summary>
    /// Set up optimal grid size with minimal padding for pickup items.
    /// </summary>
    private void SetupOptimalGridSize()
    {
        // Calculate total size with minimal spacing for pickup display
        float totalWidth = currentGridData.Width * cellSize + (currentGridData.Width - 1) * cellSpacing;
        float totalHeight = currentGridData.Height * cellSize + (currentGridData.Height - 1) * cellSpacing;

        // Add pickup-specific padding
        totalWidth += pickupItemPadding * 2;
        totalHeight += pickupItemPadding * 2;

        rectTransform.sizeDelta = new Vector2(totalWidth, totalHeight);

        DebugLog($"Pickup grid sized to {totalWidth:F1}x{totalHeight:F1} for {currentGridData.Width}x{currentGridData.Height} grid");
    }

    #endregion

    #region Pickup Events

    /// <summary>
    /// Subscribe to pickup-specific events.
    /// </summary>
    protected override void SubscribeToDataEvents()
    {
        base.SubscribeToDataEvents();

        if (pickupManager != null)
        {
            pickupManager.OnPickupItemSet += OnPickupItemSet;
            pickupManager.OnPickupItemTransferred += OnPickupItemTransferred;
            pickupManager.OnOverflowCleared += OnOverflowCleared;
            DebugLog("Subscribed to pickup-specific events");
        }
    }

    /// <summary>
    /// Unsubscribe from pickup-specific events.
    /// </summary>
    protected override void UnsubscribeFromDataEvents()
    {
        base.UnsubscribeFromDataEvents();

        if (pickupManager != null)
        {
            pickupManager.OnPickupItemSet -= OnPickupItemSet;
            pickupManager.OnPickupItemTransferred -= OnPickupItemTransferred;
            pickupManager.OnOverflowCleared -= OnOverflowCleared;
            DebugLog("Unsubscribed from pickup-specific events");
        }
    }

    /// <summary>
    /// Handle pickup item set event.
    /// </summary>
    private void OnPickupItemSet(InventoryItemData item)
    {
        DebugLog($"Pickup item set: {item?.ItemData?.itemName}");

        // Refresh grid setup when new item is set
        SetupGrid();
        RefreshAllVisuals();
    }

    /// <summary>
    /// Handle pickup item transferred event.
    /// </summary>
    private void OnPickupItemTransferred()
    {
        DebugLog("Pickup item transferred");

        // Clear visuals since item was transferred
        ClearAllItemVisuals();
    }

    /// <summary>
    /// Handle overflow cleared event.
    /// </summary>
    private void OnOverflowCleared()
    {
        DebugLog("Pickup overflow cleared");

        // Reset grid to minimal size
        SetupGrid();
        RefreshAllVisuals();
    }

    #endregion

    #region Restricted Operations

    /// <summary>
    /// Override to prevent normal item addition.
    /// </summary>
    public override bool TryAddItemAt(ItemData itemData, Vector2Int position)
    {
        DebugLogWarning("Cannot add items to pickup overflow grid - items are set through the pickup system");
        return false;
    }

    /// <summary>
    /// Override to prevent moving items.
    /// </summary>
    public override bool TryMoveItem(string itemId, Vector2Int newPosition)
    {
        DebugLogWarning("Cannot move items in pickup overflow grid - item must remain at origin");
        return false;
    }

    /// <summary>
    /// Allow rotation of the pickup item.
    /// </summary>
    public override bool TryRotateItem(string itemId)
    {
        if (pickupManager != null && pickupManager.IsPickupItem(itemId))
        {
            return pickupManager.RotateItem(itemId);
        }

        return false;
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Get the connected pickup overflow manager.
    /// </summary>
    public PickupOverflowManager GetPickupManager()
    {
        return pickupManager;
    }

    /// <summary>
    /// Check if connected to a pickup overflow manager.
    /// </summary>
    public bool IsConnectedToPickupManager()
    {
        return pickupManager != null;
    }

    /// <summary>
    /// Force reconnection to pickup manager.
    /// </summary>
    [Button("Reconnect to Pickup Manager")]
    public void ReconnectToPickupManager()
    {
        ConnectToPickupOverflowManager();
        Debug.Log("Manually reconnected to pickup overflow manager");
    }

    /// <summary>
    /// Get the current pickup item.
    /// </summary>
    public InventoryItemData GetCurrentPickupItem()
    {
        return pickupManager?.GetPickupItem();
    }

    #endregion

    #region Debug Methods

    [Button("Debug Pickup Visual State")]
    private void DebugPickupVisualState()
    {
        Debug.Log($"=== PICKUP OVERFLOW VISUAL DEBUG ===");
        Debug.Log($"Connected to Manager: {IsConnectedToPickupManager()}");
        Debug.Log($"Grid Size: {currentGridData?.Width ?? 0}x{currentGridData?.Height ?? 0}");
        Debug.Log($"Cell Size: {cellSize}, Spacing: {cellSpacing}");
        Debug.Log($"Item Visuals: {itemVisuals.Count}");
        Debug.Log($"Preview Cells: {previewCells.Count}");

        if (pickupManager != null)
        {
            Debug.Log($"Has Pickup Item: {pickupManager.HasPickupItem()}");
            if (pickupManager.HasPickupItem())
            {
                var item = pickupManager.GetPickupItem();
                Debug.Log($"Pickup Item: {item?.ItemData?.itemName} at {item?.GridPosition}");
            }
        }

        foreach (var kvp in itemVisuals)
        {
            Debug.Log($"  Visual {kvp.Key}: {(kvp.Value != null ? "Active" : "NULL")}");
        }
    }

    #endregion
}