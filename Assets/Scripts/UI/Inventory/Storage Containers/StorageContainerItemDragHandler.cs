using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ENHANCED: Simplified drag handler for storage container items that extends BaseInventoryDragHandler.
/// Now supports precise position and rotation transfer based on the drag preview.
/// Handles container-specific drag behaviors like transferring items between container and player inventory.
/// </summary>
public class StorageContainerItemDragHandler : BaseInventoryDragHandler
{
    [Header("Container Specific")]
    [SerializeField] private bool enableTransferToPlayer = true;
    [SerializeField] private bool enableTransferToOtherContainers = false;

    // Container-specific references
    private StorageContainer containerManager;
    private StorageContainerGridVisual containerGridVisual;

    // Transfer detection
    private bool isDraggedToPlayerInventory = false;
    private BaseInventoryGridVisual playerInventoryGridVisual;

    // NEW: Preview state tracking for precise placement
    private Vector2Int lastPreviewPosition;
    private int lastPreviewRotation;
    private bool hasValidPreview = false;

    #region Initialization

    public override void Initialize(InventoryItemData item, BaseInventoryGridVisual visual)
    {
        base.Initialize(item, visual);

        containerGridVisual = visual as StorageContainerGridVisual;

        // Find player inventory visual for transfer detection
        FindPlayerInventoryVisual();
    }

    /// <summary>
    /// Set the container manager reference.
    /// </summary>
    public void SetContainerManager(StorageContainer manager)
    {
        containerManager = manager;
        DebugLog($"Container manager set: {manager?.DisplayName ?? "None"}");
    }

    /// <summary>
    /// Find the player inventory visual for transfer operations.
    /// </summary>
    private void FindPlayerInventoryVisual()
    {
        // Look for InventoryGridVisual (player inventory) in the scene
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

    #region Container-Specific Overrides

    /// <summary>
    /// Enhanced drag feedback for container items with transfer detection.
    /// </summary>
    protected override void UpdateDragFeedback(PointerEventData eventData)
    {
        // Check if we're being dragged over the player inventory
        CheckForPlayerInventoryTransfer(eventData);

        // If not over player inventory, use normal container behavior
        if (!isDraggedToPlayerInventory)
        {
            base.UpdateDragFeedback(eventData);
            hasValidPreview = false; // Reset preview state when not over player inventory
        }
        else
        {
            // Clear container preview since we're over player inventory
            ClearPreview();
            ShowPlayerInventoryTransferPreview();
        }
    }

    /// <summary>
    /// Enhanced drop handling with transfer support.
    /// ENHANCED: Now uses the exact preview position and rotation for placement.
    /// </summary>
    protected override bool HandleDrop(PointerEventData eventData)
    {
        DebugLog($"HandleDrop() called - isDraggedToPlayerInventory: {isDraggedToPlayerInventory}");

        // Check if we're dropping on player inventory for transfer
        if (isDraggedToPlayerInventory && enableTransferToPlayer)
        {
            return HandleTransferToPlayer();
        }

        // Use default container behavior for other drops
        return false;
    }

    /// <summary>
    /// Container items should generally be draggable.
    /// </summary>
    protected override bool CanBeginDrag(PointerEventData eventData)
    {
        if (!base.CanBeginDrag(eventData))
            return false;

        // Add container-specific validation
        if (containerManager != null && !containerManager.CanPlayerAccess())
        {
            DebugLog("Cannot drag item - player cannot access container");
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
            canvasGroup.alpha = 0.9f; // Slightly more visible when over player inventory
        }
        else
        {
            canvasGroup.alpha = 0.8f; // Normal drag alpha
        }
    }

    /// <summary>
    /// ENHANCED: Show transfer preview on player inventory and track preview state.
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
    /// ENHANCED: Handle transferring item to player inventory with precise positioning.
    /// Now places the item exactly where the preview was shown.
    /// </summary>
    private bool HandleTransferToPlayer()
    {
        if (containerManager == null || PlayerInventoryManager.Instance == null)
        {
            DebugLogError("Cannot transfer - missing references");
            RevertToOriginalState();
            return true; // Handled, even though failed
        }

        DebugLog($"Attempting to transfer {itemData.ItemData?.itemName} to player inventory");

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

        // ENHANCED: First, restore the item to the container if it was removed during drag
        if (itemRemovedFromGrid)
        {
            DebugLog($"Restoring item {itemData.ID} to container before transfer");

            // Restore to original position and rotation
            itemData.SetGridPosition(originalGridPosition);
            itemData.SetRotation(originalRotation);

            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
                DebugLog($"Item {itemData.ID} restored to container successfully");
            }
            else
            {
                DebugLogError($"Failed to restore item {itemData.ID} to container!");
                RevertToOriginalState();
                return true; // Handled, even though failed
            }
        }

        // ENHANCED: Now perform the transfer using the precise preview position and rotation
        bool success = PerformPreciseTransfer();

        if (success)
        {
            DebugLog($"Successfully transferred {itemData.ItemData?.itemName} to player inventory at {lastPreviewPosition} with rotation {lastPreviewRotation}");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLog($"Failed to transfer {itemData.ItemData?.itemName} - reverting");
            RevertToOriginalState();
        }

        return true; // Handled
    }

    /// <summary>
    /// ENHANCED: Perform precise transfer using the exact preview position and rotation.
    /// This ensures the item is placed exactly where the player saw it in the preview.
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

        // Remove from container manager
        if (containerManager.RemoveItem(itemData.ID))
        {
            DebugLog($"Successfully removed {itemData.ID} from container");

            // Try to add to player inventory at the exact preview position and rotation
            if (PlayerInventoryManager.Instance.AddItem(itemData.ItemData, lastPreviewPosition, lastPreviewRotation))
            {
                DebugLog($"Successfully added {itemData.ItemData?.itemName} to player inventory at precise position");
                return true;
            }
            else
            {
                // Failed to add to player - restore to container
                DebugLogError("Failed to add item to player inventory at precise position - attempting to restore to container");

                if (containerManager.AddItem(itemData.ItemData, originalGridPosition, originalRotation))
                {
                    DebugLog("Item restored to container after failed player inventory addition");
                }
                else
                {
                    DebugLogError("CRITICAL: Failed to restore item to container after failed player inventory addition!");
                }
                return false;
            }
        }
        else
        {
            DebugLogError("Failed to remove item from container manager");
            return false;
        }
    }

    #endregion

    #region Drop Down Menu Integration

    protected override void TransferItem()
    {
        Debug.Log("Transfering item to player inventory via right-click");
        // For right-click transfers, we don't have a drag preview, so use auto-placement
        bool success = PerformAutoTransfer();
        if (success)
        {
            DebugLog($"Quick-transferred {itemData.ItemData?.itemName} to player inventory");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLog("Quick transfer failed");
        }
    }

    /// <summary>
    /// Perform auto transfer without specific position (for right-click transfers).
    /// </summary>
    private bool PerformAutoTransfer()
    {
        // Check if player has space
        if (!PlayerInventoryManager.Instance.HasSpaceForItem(itemData.ItemData))
        {
            DebugLog("Player inventory has no space for item");
            return false;
        }

        // Remove from container manager
        if (containerManager.RemoveItem(itemData.ID))
        {
            DebugLog($"Successfully removed {itemData.ID} from container for auto transfer");

            // Try to add to player inventory (auto-placement)
            if (PlayerInventoryManager.Instance.AddItem(itemData.ItemData))
            {
                DebugLog($"Successfully auto-transferred {itemData.ItemData?.itemName} to player inventory");
                return true;
            }
            else
            {
                // Failed to add to player - restore to container
                DebugLogError("Failed to add item to player inventory - attempting to restore to container");

                if (containerManager.AddItem(itemData.ItemData, originalGridPosition, originalRotation))
                {
                    DebugLog("Item restored to container after failed auto transfer");
                }
                else
                {
                    DebugLogError("CRITICAL: Failed to restore item to container after failed auto transfer!");
                }
                return false;
            }
        }
        else
        {
            DebugLogError("Failed to remove item from container manager for auto transfer");
            return false;
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

        // Remove item from container
        if (containerManager.RemoveItem(itemData.ID))
        {
            DebugLog($"Item {itemData.ItemData.itemName} consumed and removed from container");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLogError("Failed to remove consumed item from container");
        }
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
    /// Get the container manager this drag handler is associated with.
    /// </summary>
    public StorageContainer GetContainerManager()
    {
        return containerManager;
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
    /// NEW: Get the last preview position (for debugging).
    /// </summary>
    public Vector2Int GetLastPreviewPosition()
    {
        return lastPreviewPosition;
    }

    /// <summary>
    /// NEW: Get the last preview rotation (for debugging).
    /// </summary>
    public int GetLastPreviewRotation()
    {
        return lastPreviewRotation;
    }

    /// <summary>
    /// NEW: Check if there's currently a valid preview.
    /// </summary>
    public bool HasValidPreview()
    {
        return hasValidPreview;
    }
}

#endregion