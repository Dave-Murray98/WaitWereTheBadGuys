using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// Abstract base class for inventory item drag handlers.
/// Provides core drag functionality that can be extended for different inventory types and behaviors.
/// Handles the basic drag mechanics while allowing derived classes to customize validation and actions.
/// </summary>
public abstract class BaseInventoryDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Drag Settings")]
    [SerializeField] protected bool canDrag = true;
    [SerializeField] protected bool canRotate = true;
    [SerializeField] protected float snapAnimationDuration = 0.2f;

    [Header("Transfer Storage Settings")]
    public bool canTransfer = true;

    [Header("Drop Outside Settings")]
    [SerializeField] protected float dropOutsideBuffer = 50f;

    [Header("Debug Settings")]
    [SerializeField] protected bool enableDebugLogs = false;

    // Drop Down Menu
    public InventoryDropdownManager dropdownManager;
    public InventoryDropdownMenu dropdownMenu;

    // Core components
    protected InventoryItemData itemData;
    protected BaseInventoryGridVisual gridVisual;
    protected InventoryItemVisualRenderer visualRenderer;
    protected RectTransform rectTransform;
    protected Canvas canvas;
    protected CanvasGroup canvasGroup;

    // Drag state
    protected Vector2 originalPosition;
    protected Vector2Int originalGridPosition;
    protected int originalRotation;
    protected bool isDragging = false;
    protected bool wasValidPlacement = false;
    protected bool itemRemovedFromGrid = false;

    //Invenotory Type Context
    protected ItemInventoryTypeContext inventoryTypeContext = ItemInventoryTypeContext.NonPlayerInventoryItem;

    // Input management
    protected InputManager inputManager;

    // Events for stats display and other systems
    public System.Action<InventoryItemData> OnItemSelected;
    public System.Action OnItemDeselected;

    #region Lifecycle

    protected virtual void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        visualRenderer = GetComponent<InventoryItemVisualRenderer>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    protected virtual void Start()
    {
        inputManager = FindFirstObjectByType<InputManager>();
        SetupRotationInput();
        RegisterWithStatsDisplay();

        GetDropdownMenuFromManager();
        SetupDropdownEvents();
    }

    protected virtual void OnDestroy()
    {
        CleanupInput();
        CleanupDropdownEvents();
        UnregisterFromStatsDisplay();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize the drag handler with item data and grid visual reference.
    /// </summary>
    public virtual void Initialize(InventoryItemData item, BaseInventoryGridVisual visual)
    {
        DebugLog($"Initializing drag handler for item: {item?.ItemData?.itemName}, gridvisual: {visual?.name}");
        itemData = item;
        gridVisual = visual;
        UpdatePosition();

        DebugLog($"Initialized drag handler for item: {itemData?.ItemData?.itemName}");
    }

    /// <summary>
    /// Get dropdown menu from manager.
    /// </summary>
    private void GetDropdownMenuFromManager()
    {
        if (dropdownManager == null)
        {
            dropdownManager = FindFirstObjectByType<InventoryDropdownManager>();
            if (dropdownManager != null)
            {
                dropdownMenu = dropdownManager.DropdownMenu;
            }
            else
            {
                DebugLogWarning("InventoryDropdownManager not found! Make sure it's added to your inventory UI.");
            }
        }
    }

    #region Dropdown Menu Integration
    /// <summary>
    /// Set up dropdown menu events.
    /// </summary>
    protected virtual void SetupDropdownEvents()
    {
        if (dropdownMenu != null)
        {
            dropdownMenu.OnActionSelected += OnDropdownActionSelected;
        }
    }

    /// <summary>
    /// Clean up dropdown menu events.
    /// </summary>
    protected virtual void CleanupDropdownEvents()
    {
        if (dropdownMenu != null)
        {
            dropdownMenu.OnActionSelected -= OnDropdownActionSelected;
        }
    }
    #endregion

    /// <summary>
    /// Set up rotation input handling.
    /// </summary>
    protected virtual void SetupRotationInput()
    {
        if (inputManager != null)
        {
            inputManager.OnRotateInventoryItemPressed += OnRotateInput;
        }
    }

    /// <summary>
    /// Clean up input subscriptions.
    /// </summary>
    protected virtual void CleanupInput()
    {
        if (inputManager != null)
        {
            inputManager.OnRotateInventoryItemPressed -= OnRotateInput;
        }
    }

    /// <summary>
    /// Register with stats display system.
    /// </summary>
    protected virtual void RegisterWithStatsDisplay()
    {
        if (ItemStatsDisplay.Instance != null)
        {
            ItemStatsDisplay.Instance.RegisterDragHandler(this);
        }
    }

    /// <summary>
    /// Unregister from stats display system.
    /// </summary>
    protected virtual void UnregisterFromStatsDisplay()
    {
        if (ItemStatsDisplay.Instance != null)
        {
            ItemStatsDisplay.Instance.UnregisterDragHandler(this);
        }
    }

    #endregion

    #region Position Management

    /// <summary>
    /// Update the visual position based on item's grid position.
    /// </summary>
    protected virtual void UpdatePosition()
    {
        if (rectTransform != null && itemData != null && gridVisual != null)
        {
            Vector2 gridPos = gridVisual.GetCellWorldPosition(itemData.GridPosition.x, itemData.GridPosition.y);
            rectTransform.localPosition = gridPos;
        }
    }

    #endregion

    #region Input Handlers

    /// <summary>
    /// Handle rotation input during drag.
    /// </summary>
    protected virtual void OnRotateInput()
    {
        if (isDragging && canRotate && itemData?.CanRotate == true)
        {
            RotateItemDuringDrag();
        }
    }

    /// <summary>
    /// Handle pointer click events.
    /// </summary>
    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            TriggerStatsDisplay();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            OnRightClick(eventData.position);
        }
    }

    /// <summary>
    /// Handle right-click events. Can be overridden for custom context menus.
    /// </summary>
    protected virtual void OnRightClick(Vector2 mouseScreenPosition)
    {
        // Use mouse position with small offset so dropdown doesn't cover the cursor
        Vector2 dropdownPosition = mouseScreenPosition + new Vector2(15f, -15f);
        DebugLog($"Showing dropdown at: {dropdownPosition} (mouse was at: {mouseScreenPosition})");
        ShowDropdownMenu(dropdownPosition);
    }


    /// <summary>
    /// Show dropdown context menu.
    /// </summary>
    protected virtual void ShowDropdownMenu(Vector2 screenPosition)
    {
        if (dropdownMenu == null)
        {
            //GetDropdownMenuFromManager();
            Debug.LogWarning("Dropdown menu reference is null - cannot show context menu");
        }

        if (dropdownMenu == null)
        {
            DebugLogWarning("No dropdown menu available - falling back to direct drop");
            return;
        }

        if (itemData?.ItemData == null)
        {
            DebugLogWarning("Cannot show dropdown - no item data");
            return;
        }

        dropdownMenu.ShowMenu(itemData, screenPosition, inventoryTypeContext, canTransfer);
    }


    #endregion

    #region Drag Implementation

    /// <summary>
    /// Begin drag operation.
    /// </summary>
    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!canDrag || itemData == null) return;

        // Check if drag is allowed by derived class
        if (!CanBeginDrag(eventData))
        {
            DebugLog("Drag not allowed by derived class");
            return;
        }

        BeginDragInternal(eventData);
    }

    /// <summary>
    /// Internal drag begin logic.
    /// </summary>
    protected virtual void BeginDragInternal(PointerEventData eventData)
    {
        isDragging = true;
        originalPosition = rectTransform.localPosition;
        originalGridPosition = itemData.GridPosition;
        originalRotation = itemData.currentRotation;
        itemRemovedFromGrid = false;

        TriggerStatsDisplay();

        // Remove item from grid to prevent self-collision during drag
        if (gridVisual.GridData.GetItem(itemData.ID) != null)
        {
            gridVisual.GridData.RemoveItem(itemData.ID);
            itemRemovedFromGrid = true;
        }

        // Visual feedback
        canvasGroup.alpha = 0.8f;
        transform.SetAsLastSibling();

        DebugLog($"Began dragging item: {itemData.ItemData?.itemName}");
    }

    /// <summary>
    /// Continue drag operation.
    /// </summary>
    public virtual void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!isDragging || itemData == null || gridVisual == null) return;

        // Move the visual with the mouse
        rectTransform.localPosition += (Vector3)(eventData.delta / canvas.scaleFactor);

        // Update drag feedback
        UpdateDragFeedback(eventData);
    }

    /// <summary>
    /// Update drag feedback (preview, validation, etc.).
    /// Can be overridden by derived classes for custom feedback.
    /// </summary>
    protected virtual void UpdateDragFeedback(PointerEventData eventData)
    {
        // Check if we're still within the grid bounds
        if (IsWithinGridBounds())
        {
            ShowInventoryPreview();
        }
        else
        {
            ClearPreview();
            HandleDragOutsideBounds(eventData);
        }
    }

    /// <summary>
    /// Update drag feedback but allow derived classes to override bounds checking.
    /// This version can be called by derived classes that need custom bounds logic.
    /// </summary>
    protected virtual void UpdateDragFeedbackForInventory()
    {
        ShowInventoryPreview();
    }

    /// <summary>
    /// Show inventory placement preview.
    /// </summary>
    protected virtual void ShowInventoryPreview()
    {
        Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

        var tempItem = new InventoryItemData(itemData.ID + "_temp", itemData.ItemData, gridPos);
        tempItem.SetRotation(itemData.currentRotation);

        bool isValid = gridVisual.GridData.IsValidPosition(gridPos, tempItem);
        gridVisual.ShowPlacementPreview(gridPos, tempItem, isValid);
        wasValidPlacement = isValid;
    }

    /// <summary>
    /// Clear all previews.
    /// </summary>
    protected virtual void ClearPreview()
    {
        gridVisual.ClearPlacementPreview();
        wasValidPlacement = false;
    }

    /// <summary>
    /// Handle dragging outside grid bounds. Can be overridden for custom behavior.
    /// </summary>
    protected virtual void HandleDragOutsideBounds(PointerEventData eventData)
    {
        canvasGroup.alpha = 0.6f; // Visual feedback for being outside
    }

    /// <summary>
    /// End drag operation.
    /// </summary>
    public virtual void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!isDragging || itemData == null) return;

        EndDragInternal(eventData);
    }

    /// <summary>
    /// Internal drag end logic.
    /// </summary>
    protected virtual void EndDragInternal(PointerEventData eventData)
    {
        isDragging = false;
        canvasGroup.alpha = 1f;


        // Let derived classes handle the drop logic
        bool dropHandled = HandleDrop(eventData);

        if (!dropHandled)
        {
            // Default behavior: try to place in inventory
            HandleInventoryDrop();
        }

        ClearPreview();

        DebugLog($"Ended dragging item: {itemData.ItemData?.itemName}");
    }

    /// <summary>
    /// Handle the drop operation. Override for custom drop behavior.
    /// Return true if the drop was handled, false to use default inventory placement.
    /// </summary>
    protected virtual bool HandleDrop(PointerEventData eventData)
    {
        return false; // Default: not handled, use inventory placement
    }

    /// <summary>
    /// Handle dropping back into inventory.
    /// </summary>
    protected virtual void HandleInventoryDrop()
    {
        if (!wasValidPlacement)
            DebugLogWarning("wasValidPlacement is false on drop");

        if (IsWithinGridBounds() && wasValidPlacement)
        {
            // Place at current position
            Vector2Int targetGridPos = gridVisual.GetGridPosition(rectTransform.localPosition);
            itemData.SetGridPosition(targetGridPos);

            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
                AnimateToGridPosition();
                DebugLog($"Successfully placed item at {targetGridPos}");
            }
            else
            {
                DebugLogError("Failed to place item in grid - reverting");
                RevertToOriginalState();
            }
        }
        else
        {
            DebugLog("Invalid placement - reverting to original position");
            RevertToOriginalState();
        }
    }

    #endregion

    #region Rotation

    /// <summary>
    /// Rotate item during drag operation.
    /// </summary>
    protected virtual void RotateItemDuringDrag()
    {
        if (!canRotate || !isDragging || itemData?.CanRotate != true) return;

        var currentRotation = itemData.currentRotation;
        var currentCenter = GetVisualCenter();

        int maxRotations = TetrominoDefinitions.GetRotationCount(itemData.shapeType);
        int newRotation = (currentRotation + 1) % maxRotations;

        itemData.SetRotation(newRotation);
        visualRenderer?.RefreshVisual();

        // Adjust position to keep visual centered
        Vector2 newCenter = GetVisualCenter();
        Vector2 offset = currentCenter - newCenter;
        rectTransform.localPosition += (Vector3)offset;

        // Update preview if within bounds
        if (IsWithinGridBounds())
        {
            ShowInventoryPreview();
        }

        DebugLog($"Rotated item from {currentRotation} to {newRotation}");
    }

    /// <summary>
    /// Get the visual center of the current item shape.
    /// </summary>
    protected virtual Vector2 GetVisualCenter()
    {
        var shapeData = itemData.CurrentShapeData;
        if (shapeData.cells.Length == 0)
            return rectTransform.localPosition;

        Vector2 center = Vector2.zero;
        foreach (var cell in shapeData.cells)
        {
            center += new Vector2(
                cell.x * (gridVisual.CellSize + gridVisual.CellSpacing),
                -cell.y * (gridVisual.CellSize + gridVisual.CellSpacing)
            );
        }
        center /= shapeData.cells.Length;

        return rectTransform.localPosition + new Vector3(center.x, center.y, 0);
    }

    #endregion

    #region Validation and State Management

    /// <summary>
    /// Check if dragging can begin. Override for custom validation.
    /// </summary>
    protected virtual bool CanBeginDrag(PointerEventData eventData)
    {
        return canDrag && itemData != null;
    }

    /// <summary>
    /// Check if the item is within grid bounds.
    /// </summary>
    protected virtual bool IsWithinGridBounds()
    {
        if (gridVisual == null)
        {
            DebugLog("IsWithinGridBounds: gridVisual is null");
            return false;
        }

        RectTransform gridRect = gridVisual.GetComponent<RectTransform>();
        if (gridRect == null)
        {
            DebugLog("IsWithinGridBounds: gridRect is null");
            return false;
        }

        Vector2 localPosition;
        bool screenToLocalSuccess = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position),
            canvas.worldCamera,
            out localPosition);

        if (!screenToLocalSuccess)
        {
            DebugLog("IsWithinGridBounds: ScreenPointToLocalPointInRectangle failed");
            return false;
        }

        Rect gridBounds = gridRect.rect;
        gridBounds.xMin -= dropOutsideBuffer;
        gridBounds.xMax += dropOutsideBuffer;
        gridBounds.yMin -= dropOutsideBuffer;
        gridBounds.yMax += dropOutsideBuffer;

        bool isWithin = gridBounds.Contains(localPosition);

        //        DebugLog($"IsWithinGridBounds: localPos={localPosition}, gridBounds={gridBounds}, isWithin={isWithin}");

        return isWithin;
    }

    /// <summary>
    /// Revert item to its original state.
    /// </summary>
    protected virtual void RevertToOriginalState()
    {
        DebugLog("Reverting to original state");

        // Revert rotation if changed
        if (itemData.currentRotation != originalRotation)
        {
            itemData.SetRotation(originalRotation);
            visualRenderer?.RefreshVisual();
        }

        // Restore original position
        itemData.SetGridPosition(originalGridPosition);

        // Place item back in grid
        if (itemRemovedFromGrid)
        {
            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
            }
            else
            {
                DebugLogError($"Failed to restore item {itemData.ID} to original position!");
            }
        }

        // Animate back to original position
        AnimateToOriginalPosition();
        visualRenderer?.RefreshHotkeyIndicatorVisuals();
    }

    #endregion

    #region Drop Down Menu Handling

    /// <summary>
    /// Handle dropdown menu action selection.
    /// </summary>
    protected virtual void OnDropdownActionSelected(InventoryItemData selectedItem, string actionId)
    {
        if (selectedItem == null)
        {
            DebugLogWarning("OnDropdownActionSelected: Selected item is null");
            return;
        }

        // Always use selectedItem instead of this.itemData for consistency
        // Update our itemData reference to match the selected item
        if (itemData != selectedItem)
        {
            return;
        }

        // Child classes will add their own methods and also override each action's methods 
        switch (actionId)
        {
            case "transfer":
                TransferItem();
                break;
            case "consume":
                ConsumeItem();
                break;
            case "drop":
                DropItem();
                break;
            default:
                DebugLogWarning($"Unknown dropdown action: {actionId}");
                break;
        }
    }

    #endregion

    #region Drop Down Action Handlers

    protected virtual void TransferItem()
    {
        DebugLog("Transferring item via base class TransferItem method");
        // Implement transfer logic here in derived classes
    }

    protected virtual void ConsumeItem()
    {
        DebugLog("Consuming item via base class ConsumeItem method");
        // Implement consume logic here in derived classes
    }

    protected virtual void DropItem()
    {
        DebugLog("Dropping item via base class DropItem method");
        // Implement drop logic here in derived classes
    }


    #endregion

    #region Animation

    /// <summary>
    /// Animate to current grid position.
    /// </summary>
    protected virtual void AnimateToGridPosition()
    {
        Vector2 targetPos = gridVisual.GetCellWorldPosition(itemData.GridPosition.x, itemData.GridPosition.y);
        rectTransform.DOLocalMove(targetPos, snapAnimationDuration).SetEase(Ease.OutQuad);
    }

    /// <summary>
    /// Animate back to original position.
    /// </summary>
    protected virtual void AnimateToOriginalPosition()
    {
        rectTransform.DOLocalMove(originalPosition, snapAnimationDuration).SetEase(Ease.OutQuad);
    }

    #endregion

    #region Stats Display Integration

    /// <summary>
    /// Trigger stats display for this item.
    /// </summary>
    protected virtual void TriggerStatsDisplay()
    {
        OnItemSelected?.Invoke(itemData);
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Set whether this item can be dragged.
    /// </summary>
    public virtual void SetDraggable(bool draggable)
    {
        canDrag = draggable;
    }

    /// <summary>
    /// Set whether this item can be rotated.
    /// </summary>
    public virtual void SetRotatable(bool rotatable)
    {
        canRotate = rotatable;
    }

    /// <summary>
    /// Get the current item data.
    /// </summary>
    public virtual InventoryItemData GetItemData()
    {
        return itemData;
    }

    #endregion

    #region Debug Helpers

    protected void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[{GetType().Name}] {message}");
    }

    protected void DebugLogError(string message)
    {
        if (enableDebugLogs)
            Debug.LogError($"[{GetType().Name}] {message}");
    }

    protected void DebugLogWarning(string message)
    {
        if (enableDebugLogs)
            Debug.LogWarning($"[{GetType().Name}] {message}");
    }

    #endregion
}

// InventoryTypeContext
public enum ItemInventoryTypeContext
{
    PlayerInventoryItem,
    NonPlayerInventoryItem
}
