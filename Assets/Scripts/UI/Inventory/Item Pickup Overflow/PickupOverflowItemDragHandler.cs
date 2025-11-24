using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Specialized drag handler for pickup overflow items that extends BaseInventoryDragHandler.
/// Handles one-way transfers from pickup overflow to player inventory only.
/// Supports precise positioning based on drag preview and automatically closes the overflow UI on successful transfer.
/// </summary>
public class PickupOverflowItemDragHandler : BaseInventoryDragHandler
{
    [Header("Pickup Overflow Specific")]
    [SerializeField] private bool enableTransferToPlayer = true;

    // Pickup-specific references
    private PickupOverflowManager pickupManager;
    private PickupOverflowGridVisual pickupGridVisual;

    // Transfer detection
    private bool isDraggedToPlayerInventory = false;
    private BaseInventoryGridVisual playerInventoryGridVisual;

    // Preview state tracking for precise placement
    private Vector2Int lastPreviewPosition;
    private int lastPreviewRotation;
    private bool hasValidPreview = false;

    #region Initialization

    public override void Initialize(InventoryItemData item, BaseInventoryGridVisual visual)
    {
        base.Initialize(item, visual);

        pickupGridVisual = visual as PickupOverflowGridVisual;

        // Find player inventory visual for transfer detection
        FindPlayerInventoryVisual();
    }

    /// <summary>
    /// Set the pickup manager reference.
    /// </summary>
    public void SetPickupManager(PickupOverflowManager manager)
    {
        pickupManager = manager;
        DebugLog($"Pickup manager set: {(manager != null ? "Connected" : "None")}");
    }

    /// <summary>
    /// Find the player inventory visual for transfer operations.
    /// </summary>
    private void FindPlayerInventoryVisual()
    {
        // Look for PlayerInventoryGridVisual in the scene
        var playerVisual = FindFirstObjectByType<PlayerInventoryGridVisual>();
        if (playerVisual != null)
        {
            playerInventoryGridVisual = playerVisual;
            DebugLog("Found player inventory visual for transfers");
        }
        else
        {
            DebugLog("Player inventory visual not found - transfers may not work");
        }
    }

    #endregion

    #region Pickup-Specific Overrides

    /// <summary>
    /// Enhanced drag feedback for pickup items with transfer detection.
    /// </summary>
    protected override void UpdateDragFeedback(PointerEventData eventData)
    {
        // Check if we're being dragged over the player inventory
        CheckForPlayerInventoryTransfer(eventData);

        // If over player inventory, show transfer preview
        if (isDraggedToPlayerInventory)
        {
            // Clear pickup overflow preview since we're over player inventory
            ClearPreview();
            ShowPlayerInventoryTransferPreview();
        }
        else
        {
            // If not over player inventory, show normal pickup overflow behavior
            // But don't allow placement back in pickup overflow (one-way transfer)
            ClearPreview();
            hasValidPreview = false;
        }
    }

    /// <summary>
    /// Enhanced drop handling with transfer support.
    /// </summary>
    protected override bool HandleDrop(PointerEventData eventData)
    {
        DebugLog($"HandleDrop() called - isDraggedToPlayerInventory: {isDraggedToPlayerInventory}");

        // Check if we're dropping on player inventory for transfer
        if (isDraggedToPlayerInventory && enableTransferToPlayer)
        {
            return HandleTransferToPlayer();
        }

        // For pickup overflow items, we don't allow dropping back in the same grid
        // Just revert to original position
        DebugLog("Invalid drop for pickup item - reverting to original position");
        RevertToOriginalState();
        return true; // Handled
    }

    /// <summary>
    /// Pickup items should generally be draggable.
    /// </summary>
    protected override bool CanBeginDrag(PointerEventData eventData)
    {
        if (!base.CanBeginDrag(eventData))
            return false;

        // Check if this is actually the pickup item
        if (pickupManager != null && !pickupManager.IsPickupItem(itemData.ID))
        {
            DebugLog("Cannot drag item - not the current pickup item");
            return false;
        }

        return true;
    }

    #endregion

    #region Transfer Detection and Handling

    /// <summary>
    /// Check if the item is being dragged over the player inventory.
    /// </summary>
    private void CheckForPlayerInventoryTransfer(PointerEventData eventData)
    {
        if (!enableTransferToPlayer || playerInventoryGridVisual == null)
        {
            isDraggedToPlayerInventory = false;
            hasValidPreview = false;
            return;
        }

        // Check if pointer is over player inventory
        RectTransform playerInventoryRect = playerInventoryGridVisual.GetComponent<RectTransform>();
        if (playerInventoryRect == null)
        {
            isDraggedToPlayerInventory = false;
            hasValidPreview = false;
            return;
        }

        Vector2 localPoint;
        bool isOverPlayerInventory = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playerInventoryRect,
            eventData.position,
            canvas.worldCamera,
            out localPoint);

        if (isOverPlayerInventory)
        {
            // Check if the point is actually within the bounds
            isDraggedToPlayerInventory = playerInventoryRect.rect.Contains(localPoint);
        }
        else
        {
            isDraggedToPlayerInventory = false;
        }

        if (!isDraggedToPlayerInventory)
        {
            hasValidPreview = false;
        }

        // Visual feedback for transfer
        if (isDraggedToPlayerInventory)
        {
            canvasGroup.alpha = 0.9f; // More visible when over player inventory
        }
        else
        {
            canvasGroup.alpha = 0.8f; // Normal drag alpha
        }
    }

    /// <summary>
    /// Show transfer preview on player inventory and track preview state.
    /// </summary>
    private void ShowPlayerInventoryTransferPreview()
    {
        if (playerInventoryGridVisual == null) return;

        // Get grid position on player inventory
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playerInventoryGridVisual.GetComponent<RectTransform>(),
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position),
            canvas.worldCamera,
            out localPos);

        Vector2Int gridPos = playerInventoryGridVisual.GetGridPosition(localPos);

        // Use current rotation from the dragged item (which may have been rotated during drag)
        int currentRotation = itemData.currentRotation;

        // Create temporary item for validation
        var tempItem = new InventoryItemData(itemData.ID + "_transfer_temp", itemData.ItemData, gridPos);
        tempItem.SetRotation(currentRotation);

        // Check if transfer is valid
        bool isValid = playerInventoryGridVisual.GridData.IsValidPosition(gridPos, tempItem);

        // Show preview on player inventory
        playerInventoryGridVisual.ShowPlacementPreview(gridPos, tempItem, isValid);

        // Store preview state for precise placement
        lastPreviewPosition = gridPos;
        lastPreviewRotation = currentRotation;
        hasValidPreview = isValid;
        wasValidPlacement = isValid;

        DebugLog($"Preview: pos={gridPos}, rot={currentRotation}, valid={isValid}");
    }

    /// <summary>
    /// Handle transferring pickup item to player inventory with precise positioning.
    /// FIXED: Based on StorageContainerItemDragHandler pattern - restore item first, then transfer.
    /// </summary>
    private bool HandleTransferToPlayer()
    {
        if (pickupManager == null || PlayerInventoryManager.Instance == null)
        {
            DebugLogError("Cannot transfer - missing references");
            RevertToOriginalState();
            return true; // Handled, even though failed
        }

        DebugLog($"Attempting to transfer pickup item {itemData.ItemData?.itemName} to player inventory");

        // Clear player inventory preview
        if (playerInventoryGridVisual != null)
        {
            playerInventoryGridVisual.ClearPlacementPreview();
        }

        // Check if we have a valid preview position to place at
        if (!hasValidPreview)
        {
            DebugLog("No valid preview position - cannot transfer");
            RevertToOriginalState();
            return true;
        }

        // FIXED: First, restore the item to the pickup overflow if it was removed during drag
        if (itemRemovedFromGrid)
        {
            DebugLog($"Restoring item {itemData.ID} to pickup overflow before transfer");

            // Restore to original position and rotation
            itemData.SetGridPosition(originalGridPosition);
            itemData.SetRotation(originalRotation);

            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
                DebugLog($"Item {itemData.ID} restored to pickup overflow successfully");
            }
            else
            {
                DebugLogError($"Failed to restore item {itemData.ID} to pickup overflow!");
                RevertToOriginalState();
                return true; // Handled, even though failed
            }
        }

        // Now perform the transfer using the precise preview position and rotation
        bool success = PerformPreciseTransfer();

        if (success)
        {
            DebugLog($"Successfully transferred pickup item to player inventory at {lastPreviewPosition} with rotation {lastPreviewRotation}");

            // Notify that transfer was successful
            OnItemDeselected?.Invoke();

            // Notify pickup overflow UI to close
            NotifyTransferComplete();
        }
        else
        {
            DebugLog($"Failed to transfer pickup item - reverting");
            RevertToOriginalState();
        }

        return true; // Handled
    }

    /// <summary>
    /// Perform precise transfer using the exact preview position and rotation.
    /// </summary>
    private bool PerformPreciseTransfer()
    {
        DebugLog($"Performing precise transfer to position {lastPreviewPosition} with rotation {lastPreviewRotation}");

        // Double-check that the target position is still valid
        if (!PlayerInventoryManager.Instance.HasSpaceForItemAt(itemData.ItemData, lastPreviewPosition, lastPreviewRotation))
        {
            DebugLog("Target position is no longer valid - transfer cancelled");
            return false;
        }

        // Remove from pickup overflow manager
        if (pickupManager.RemoveItem(itemData.ID))
        {
            DebugLog($"Successfully removed {itemData.ID} from pickup overflow");

            // Try to add to player inventory at the exact preview position and rotation
            if (PlayerInventoryManager.Instance.AddItem(itemData.ItemData, lastPreviewPosition, lastPreviewRotation))
            {
                DebugLog($"Successfully added pickup item to player inventory at precise position");
                return true;
            }
            else
            {
                // Failed to add to player - restore to pickup overflow
                DebugLogError("Failed to add item to player inventory at precise position - attempting to restore to pickup overflow");

                if (pickupManager.SetPickupItem(itemData.ItemData))
                {
                    DebugLog("Item restored to pickup overflow after failed player inventory addition");
                }
                else
                {
                    DebugLogError("CRITICAL: Failed to restore item to pickup overflow after failed player inventory addition!");
                }
                return false;
            }
        }
        else
        {
            DebugLogError("Failed to remove item from pickup overflow manager");
            return false;
        }
    }


    /// <summary>
    /// Notify systems that transfer is complete so UI can close.
    /// </summary>
    private void NotifyTransferComplete()
    {
        // Find and notify the pickup overflow UI
        var pickupUI = FindFirstObjectByType<PickupOverflowUI>();
        if (pickupUI != null)
        {
            pickupUI.OnPickupTransferComplete();
        }
    }

    private void OnConsumed()
    {
        // destroy it and remove from pickup manager
        if (pickupManager != null)
        {
            pickupManager.RemoveItem(itemData.ID);
        }
        else
        {
            DebugLogWarning("No pickup manager - cannot remove consumed item");
        }

        Destroy(gameObject);
    }

    #endregion

    #region Pickup-Specific Rotation

    /// <summary>
    /// Override rotation to work with pickup manager and update grid sizing.
    /// </summary>
    protected override void RotateItemDuringDrag()
    {
        if (!canRotate || !isDragging || itemData?.CanRotate != true) return;

        if (pickupManager != null && pickupManager.IsPickupItem(itemData.ID))
        {
            // Get current center before rotation
            var currentCenter = GetVisualCenter();

            // Rotate through pickup manager (which handles grid resizing)
            bool success = pickupManager.RotateItem(itemData.ID);

            if (success)
            {
                // Refresh visual to show new rotation
                visualRenderer?.RefreshVisual();

                // Adjust position to keep visual centered
                Vector2 newCenter = GetVisualCenter();
                Vector2 offset = currentCenter - newCenter;
                rectTransform.localPosition += (Vector3)offset;

                // Update preview if over player inventory
                if (isDraggedToPlayerInventory)
                {
                    ShowPlayerInventoryTransferPreview();
                }

                DebugLog($"Rotated pickup item to rotation {itemData.currentRotation}");
            }
        }
    }

    #endregion

    #region Drop Down Menu Integration

    protected override void TransferItem()
    {
        if (pickupManager == null)
        {
            DebugLogWarning("No pickup manager - cannot transfer");
            return;
        }

        // Use the pickup manager's built-in transfer method for auto-placement
        bool success = pickupManager.TryTransferToPlayer();

        if (success)
        {
            DebugLog("Quick transfer to player inventory successful");
            OnItemDeselected?.Invoke();
            NotifyTransferComplete();
        }
        else
        {
            DebugLog("Quick transfer failed - player inventory may be full");
        }
    }

    protected override void ConsumeItem()
    {
        if (itemData?.ItemData?.itemType != ItemType.Consumable)
        {
            DebugLogWarning("Cannot consume non-consumable item");
            return;
        }

        DebugLog($"Consuming {itemData.ItemData.itemName}");

        var consumableData = itemData.ItemData.ConsumableData;
        if (consumableData != null)
        {
            GameManager.Instance.playerManager.ApplyConsumableEffects(consumableData);
        }
        else
        {
            DebugLogWarning("No consumable data found on item");
        }

        OnConsumed();
    }

    #endregion

    #region Cleanup

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // Clear any player inventory preview if we're being destroyed during a drag
        if (playerInventoryGridVisual != null && isDragging)
        {
            playerInventoryGridVisual.ClearPlacementPreview();
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        // Clear player inventory preview
        if (playerInventoryGridVisual != null)
        {
            playerInventoryGridVisual.ClearPlacementPreview();
        }

        // Call base implementation
        base.OnEndDrag(eventData);

        // Reset transfer state
        isDraggedToPlayerInventory = false;
        hasValidPreview = false;
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Get the pickup manager this drag handler is associated with.
    /// </summary>
    public PickupOverflowManager GetPickupManager()
    {
        return pickupManager;
    }

    /// <summary>
    /// Check if transfer to player is enabled.
    /// </summary>
    public bool IsTransferToPlayerEnabled()
    {
        return enableTransferToPlayer;
    }

    /// <summary>
    /// Enable or disable transfer to player inventory.
    /// </summary>
    public void SetTransferToPlayerEnabled(bool enabled)
    {
        enableTransferToPlayer = enabled;
        DebugLog($"Transfer to player enabled: {enabled}");
    }

    /// <summary>
    /// Get the last preview position (for debugging).
    /// </summary>
    public Vector2Int GetLastPreviewPosition()
    {
        return lastPreviewPosition;
    }

    /// <summary>
    /// Get the last preview rotation (for debugging).
    /// </summary>
    public int GetLastPreviewRotation()
    {
        return lastPreviewRotation;
    }

    /// <summary>
    /// Check if there's currently a valid preview.
    /// </summary>
    public bool HasValidPreview()
    {
        return hasValidPreview;
    }

    #endregion
}