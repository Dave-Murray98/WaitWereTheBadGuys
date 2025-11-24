using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Specialized inventory manager for handling pickup overflow situations.
/// This creates a minimal inventory that holds exactly one item - the item that couldn't fit in the player's inventory.
/// The grid is automatically sized to perfectly fit the pickup item's shape.
/// Items can only be transferred OUT of this inventory (one-way transfer to player inventory).
/// </summary>
public class PickupOverflowManager : BaseInventoryManager
{
    // The single item this overflow inventory holds
    private InventoryItemData pickupItem;
    private ItemData originalItemData;

    // Events specific to pickup overflow
    public System.Action<InventoryItemData> OnPickupItemSet;
    public System.Action OnPickupItemTransferred;
    public System.Action OnOverflowCleared;

    #region Initialization

    protected override void InitializeInventory()
    {
        // Start with 1x1 grid - will be resized when item is set
        gridWidth = 1;
        gridHeight = 1;
        inventoryGridData = new InventoryGridData(gridWidth, gridHeight);

        DebugLog("Pickup overflow manager initialized with 1x1 grid");
    }

    #endregion

    #region Pickup Item Management

    /// <summary>
    /// Set the pickup item that couldn't fit in player inventory.
    /// This automatically resizes the grid to perfectly fit the item.
    /// </summary>
    public bool SetPickupItem(ItemData itemData)
    {
        if (itemData == null)
        {
            DebugLogError("Cannot set null item data for pickup overflow");
            return false;
        }

        // Clear any existing item first
        ClearPickupItem();

        originalItemData = itemData;

        // Create inventory item data
        string itemId = GenerateItemId();
        pickupItem = new InventoryItemData(itemId, itemData, Vector2Int.zero);

        // Calculate required grid size for this item's shape
        ResizeGridForItem(pickupItem);

        // Place the item at (0,0) in the resized grid
        pickupItem.SetGridPosition(Vector2Int.zero);

        if (inventoryGridData.PlaceItem(pickupItem))
        {
            nextItemId++;
            DebugLog($"Set pickup item: {itemData.itemName} in {gridWidth}x{gridHeight} grid");

            // Notify systems
            OnPickupItemSet?.Invoke(pickupItem);
            TriggerOnItemAdded(pickupItem);
            TriggerOnInventoryDataChanged(inventoryGridData);

            return true;
        }
        else
        {
            DebugLogError($"Failed to place pickup item {itemData.itemName} in grid");
            pickupItem = null;
            originalItemData = null;
            return false;
        }
    }

    /// <summary>
    /// Clear the pickup item and reset the grid.
    /// </summary>
    public void ClearPickupItem()
    {
        if (pickupItem != null)
        {
            DebugLog($"Clearing pickup item: {pickupItem.ItemData?.itemName}");

            // Remove from grid
            inventoryGridData.RemoveItem(pickupItem.ID);

            // Fire events
            TriggerOnItemRemoved(pickupItem.ID);

            pickupItem = null;
            originalItemData = null;

            OnOverflowCleared?.Invoke();
        }

        // Reset to minimal grid
        ResizeGrid(1, 1);
    }

    /// <summary>
    /// Get the current pickup item.
    /// </summary>
    public InventoryItemData GetPickupItem()
    {
        return pickupItem;
    }

    /// <summary>
    /// Get the original item data.
    /// </summary>
    public ItemData GetOriginalItemData()
    {
        return originalItemData;
    }

    /// <summary>
    /// Check if there's currently a pickup item.
    /// </summary>
    public bool HasPickupItem()
    {
        return pickupItem != null;
    }

    #endregion

    #region Grid Resizing

    /// <summary>
    /// Resize the grid to perfectly fit the given item.
    /// </summary>
    private void ResizeGridForItem(InventoryItemData item)
    {
        if (item?.ItemData == null)
        {
            DebugLogError("Cannot resize grid for null item");
            return;
        }

        // Get the item's shape data
        var shapeData = item.CurrentShapeData;
        if (shapeData.cells.Length == 0)
        {
            DebugLogWarning("Item has no shape cells - using 1x1 grid");
            ResizeGrid(1, 1);
            return;
        }

        // Calculate bounding box of the shape
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var cell in shapeData.cells)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        // Calculate required grid size
        int requiredWidth = maxX - minX + 1;
        int requiredHeight = maxY - minY + 1;

        // Add small padding
        int paddedWidth = requiredWidth + 1;
        int paddedHeight = requiredHeight + 1;

        ResizeGrid(paddedWidth, paddedHeight);

        DebugLog($"Resized grid to {paddedWidth}x{paddedHeight} for item {item.ItemData.itemName} " +
                 $"(item size: {requiredWidth}x{requiredHeight})");
    }

    /// <summary>
    /// Resize the inventory grid to new dimensions.
    /// </summary>
    private void ResizeGrid(int newWidth, int newHeight)
    {
        gridWidth = newWidth;
        gridHeight = newHeight;

        // Create new grid data
        inventoryGridData = new InventoryGridData(gridWidth, gridHeight);

        DebugLog($"Grid resized to {gridWidth}x{gridHeight}");

        // Notify of data change
        TriggerOnInventoryDataChanged(inventoryGridData);
    }

    #endregion

    #region Overridden Methods - Restricted Functionality

    /// <summary>
    /// Generate pickup-specific item IDs.
    /// </summary>
    protected override string GenerateItemId()
    {
        return $"pickup_overflow_item_{nextItemId}";
    }

    /// <summary>
    /// Restrict item addition - only allow through SetPickupItem.
    /// </summary>
    public override bool AddItem(ItemData itemData, Vector2Int? position = null, int? rotation = null)
    {
        DebugLogWarning("Cannot add items directly to pickup overflow inventory. Use SetPickupItem() instead.");
        return false;
    }

    /// <summary>
    /// Restrict existing item addition.
    /// </summary>
    public override bool AddItem(InventoryItemData existingItem, Vector2Int? position = null, int? rotation = null)
    {
        DebugLogWarning("Cannot add items directly to pickup overflow inventory. Use SetPickupItem() instead.");
        return false;
    }

    /// <summary>
    /// Allow removal only for transferring the pickup item.
    /// </summary>
    public override bool RemoveItem(string itemId)
    {
        if (pickupItem != null && pickupItem.ID == itemId)
        {
            DebugLog($"Transferring pickup item: {pickupItem.ItemData?.itemName}");

            // Store reference before clearing
            var transferredItem = pickupItem;

            // Clear the pickup item
            pickupItem = null;
            originalItemData = null;

            // Remove from grid
            bool success = inventoryGridData.RemoveItem(itemId);

            if (success)
            {
                DebugLog("Pickup item successfully transferred");

                // Fire events
                TriggerOnItemRemoved(itemId);
                OnPickupItemTransferred?.Invoke();

                // Reset grid after successful transfer
                ResizeGrid(1, 1);
                TriggerOnInventoryDataChanged(inventoryGridData);

                return true;
            }
            else
            {
                // Restore pickup item if removal failed
                pickupItem = transferredItem;
                DebugLogError("Failed to remove pickup item from grid");
                return false;
            }
        }

        DebugLogWarning($"Cannot remove item {itemId} - not the current pickup item");
        return false;
    }

    /// <summary>
    /// Allow rotation of the pickup item.
    /// </summary>
    public override bool RotateItem(string itemId)
    {
        if (pickupItem != null && pickupItem.ID == itemId)
        {
            // Try rotation
            bool success = base.RotateItem(itemId);

            if (success)
            {
                // If rotation succeeded, check if we need to resize grid
                ResizeGridForItem(pickupItem);

                // Ensure item is still at (0,0)
                pickupItem.SetGridPosition(Vector2Int.zero);
                inventoryGridData.RemoveItem(itemId);
                inventoryGridData.PlaceItem(pickupItem);

                TriggerOnInventoryDataChanged(inventoryGridData);
            }

            return success;
        }

        return false;
    }

    /// <summary>
    /// Prevent moving the pickup item (it should stay at 0,0).
    /// </summary>
    public override bool MoveItem(string itemId, Vector2Int newPosition)
    {
        DebugLog("Cannot move pickup item - it must remain at origin");
        return false;
    }

    /// <summary>
    /// Prevent clearing the inventory directly.
    /// </summary>
    public override void ClearInventory()
    {
        DebugLogWarning("Cannot clear pickup overflow inventory directly. Use ClearPickupItem() instead.");
    }

    /// <summary>
    /// Override space checking to always return false for additional items.
    /// </summary>
    public override bool HasSpaceForItem(ItemData itemData)
    {
        // Pickup overflow can only hold one specific item
        return false;
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Check if the given item is the current pickup item.
    /// </summary>
    public bool IsPickupItem(string itemId)
    {
        return pickupItem != null && pickupItem.ID == itemId;
    }

    /// <summary>
    /// Try to transfer the pickup item to the player inventory.
    /// Returns true if successful, false if player inventory has no space.
    /// </summary>
    public bool TryTransferToPlayer()
    {
        if (pickupItem == null)
        {
            DebugLogWarning("No pickup item to transfer");
            return false;
        }

        if (PlayerInventoryManager.Instance == null)
        {
            DebugLogError("PlayerInventoryManager not found - cannot transfer");
            return false;
        }

        // Check if player has space
        if (!PlayerInventoryManager.Instance.HasSpaceForItem(pickupItem.ItemData))
        {
            DebugLog("Player inventory has no space for pickup item");
            return false;
        }

        // Try to add to player inventory
        if (PlayerInventoryManager.Instance.AddItem(pickupItem.ItemData))
        {
            // Remove from overflow (this will trigger events)
            RemoveItem(pickupItem.ID);
            return true;
        }

        DebugLogError("Failed to add pickup item to player inventory");
        return false;
    }

    #endregion
}