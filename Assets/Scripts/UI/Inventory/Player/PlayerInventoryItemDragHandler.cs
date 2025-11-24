using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using NUnit.Framework;

/// <summary>
/// REFACTORED: Player inventory item drag handler that extends BaseInventoryDragHandler.
/// Now provides clean separation of concerns while maintaining all existing functionality.
/// Handles player-specific drag behaviors like clothing equipping, item dropping, and context menus.
/// </summary>
public class PlayerInventoryItemDragHandler : BaseInventoryDragHandler
{
    [Header("Player Inventory Specific")]
    //[SerializeField] private InventoryDropdownMenu dropdownMenu;

    // Player-specific drag state
    private bool isDraggedOutsideInventory = false;
    private ClothingSlotUI lastHoveredClothingSlot = null;


    // Reference to player inventory manager
    private PlayerInventoryManager playerInventoryManager;

    #region  Storage container transfer detection
    private bool isDraggedToStorageContainer = false;
    private StorageContainerGridVisual targetStorageContainer = null;
    private Vector2Int lastStoragePreviewPosition;
    private int lastStoragePreviewRotation;
    private bool hasValidStoragePreview = false;

    #endregion

    #region Initialization

    protected override void Awake()
    {
        // Set inventory type context for player inventory
        inventoryTypeContext = ItemInventoryTypeContext.PlayerInventoryItem;

        base.Awake();
    }

    protected override void Start()
    {
        base.Start();

        // Get reference to player inventory manager
        playerInventoryManager = PlayerInventoryManager.Instance;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        CleanupDropdownEvents();
    }

    #endregion

    #region Player-Specific Overrides

    /// <summary>
    /// ENHANCED: Enhanced drag feedback for player inventory with clothing slot and storage container integration.
    /// </summary>
    protected override void UpdateDragFeedback(PointerEventData eventData)
    {
        // First check if we're outside inventory bounds
        CheckIfOutsideInventoryBounds();

        // Clear previous clothing slot feedback
        if (lastHoveredClothingSlot != null)
        {
            ClothingSlotUI.ClearAllDragFeedback();
            lastHoveredClothingSlot = null;
        }

        // NEW: Check for storage container transfer
        CheckForStorageContainerTransfer(eventData);

        // Check for clothing slot under pointer
        var currentClothingSlot = ClothingDragDropHelper.GetClothingSlotUnderPointer(eventData);

        if (currentClothingSlot != null)
        {
            // We're over a clothing slot
            lastHoveredClothingSlot = currentClothingSlot;

            // Provide visual feedback to the clothing slot
            ClothingSlotUI.HandleDragOverClothingSlot(eventData, itemData);

            // Clear inventory and storage previews since we're over clothing
            ClearPreview();
            ClearStoragePreview();
        }
        else if (isDraggedToStorageContainer)
        {
            // NEW: We're over a storage container - show storage preview
            ClearPreview(); // Clear player inventory preview
            ShowStorageContainerPreview();
        }
        else if (!isDraggedOutsideInventory)
        {
            // We're over player inventory - show inventory preview
            ClearStoragePreview(); // Clear storage preview
            ShowInventoryPreview();
        }
        else
        {
            // We're outside both inventory, clothing slots, and storage containers
            ClearPreview();
            ClearStoragePreview();
        }
    }

    /// <summary>
    /// ENHANCED: Enhanced drop handling with clothing slot integration, storage container transfers, and item dropping.
    /// </summary>
    protected override bool HandleDrop(PointerEventData eventData)
    {
        // Clear any clothing slot feedback
        ClothingSlotUI.ClearAllDragFeedback();

        // Clear storage container preview
        ClearStoragePreview();

        // Check if we dropped on a clothing slot
        var droppedOnClothingSlot = ClothingDragDropHelper.GetClothingSlotUnderPointer(eventData);

        if (droppedOnClothingSlot != null)
        {
            return HandleClothingSlotDrop(droppedOnClothingSlot);
        }

        // NEW: Check if we dropped on a storage container
        if (isDraggedToStorageContainer && targetStorageContainer != null)
        {
            return HandleStorageContainerDrop();
        }

        // Check if we dropped outside inventory (for item dropping)
        if (isDraggedOutsideInventory)
        {
            return HandleDropOutsideInventory();
        }

        // IMPORTANT: Return false to let base class handle normal inventory placement
        // This ensures that normal inventory drag-and-drop still works
        return false;
    }

    /// <summary>
    /// Player-specific validation for drag beginning.
    /// </summary>
    protected override bool CanBeginDrag(PointerEventData eventData)
    {
        if (!base.CanBeginDrag(eventData))
            return false;

        // Add player-specific validation here if needed
        // For example: check if item is currently equipped, in use, etc.

        return true;
    }

    #endregion

    #region Storage Container Transfer Detection

    /// <summary>
    /// NEW: Check if the item is being dragged over a storage container.
    /// </summary>
    private void CheckForStorageContainerTransfer(PointerEventData eventData)
    {
        // Reset storage container state
        isDraggedToStorageContainer = false;
        targetStorageContainer = null;
        hasValidStoragePreview = false;

        // Find all storage container grid visuals in the scene
        var storageContainers = FindObjectsByType<StorageContainerGridVisual>(FindObjectsSortMode.None);

        foreach (var container in storageContainers)
        {
            if (container == null || !container.gameObject.activeInHierarchy) continue;

            RectTransform containerRect = container.GetComponent<RectTransform>();
            if (containerRect == null) continue;

            // Check if pointer is over this storage container
            Vector2 localPoint;
            bool isOverContainer = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                containerRect,
                eventData.position,
                canvas.worldCamera,
                out localPoint);

            if (isOverContainer && containerRect.rect.Contains(localPoint))
            {
                // We're over this storage container
                isDraggedToStorageContainer = true;
                targetStorageContainer = container;
                DebugLog($"Dragging over storage container: {container.GetContainerManager()?.DisplayName ?? "Unknown"}");
                break;
            }
        }

        // Visual feedback for storage container transfer
        if (isDraggedToStorageContainer)
        {
            canvasGroup.alpha = 0.9f; // Slightly more visible when over storage container
        }
        else
        {
            // Reset to normal alpha if not over storage or special areas
            if (!lastHoveredClothingSlot && !isDraggedOutsideInventory)
            {
                canvasGroup.alpha = 0.8f; // Normal drag alpha
            }
        }
    }

    /// <summary>
    /// NEW: Show transfer preview on storage container.
    /// </summary>
    private void ShowStorageContainerPreview()
    {
        if (targetStorageContainer == null) return;

        // Get grid position on storage container
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetStorageContainer.GetComponent<RectTransform>(),
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position),
            canvas.worldCamera,
            out localPos);

        Vector2Int gridPos = targetStorageContainer.GetGridPosition(localPos);

        // Use current rotation from the dragged item (which may have been rotated during drag)
        int currentRotation = itemData.currentRotation;

        // Create temporary item for validation
        var tempItem = new InventoryItemData(itemData.ID + "_storage_transfer_temp", itemData.ItemData, gridPos);
        tempItem.SetRotation(currentRotation);

        // Check if transfer is valid
        bool isValid = targetStorageContainer.GridData.IsValidPosition(gridPos, tempItem);

        // Show preview on storage container
        targetStorageContainer.ShowPlacementPreview(gridPos, tempItem, isValid);

        // Store preview state for precise placement
        lastStoragePreviewPosition = gridPos;
        lastStoragePreviewRotation = currentRotation;
        hasValidStoragePreview = isValid;
        wasValidPlacement = isValid;

        DebugLog($"Storage Preview: pos={gridPos}, rot={currentRotation}, valid={isValid}");
    }

    /// <summary>
    /// NEW: Clear storage container preview.
    /// </summary>
    private void ClearStoragePreview()
    {
        if (targetStorageContainer != null)
        {
            targetStorageContainer.ClearPlacementPreview();
        }
    }

    /// <summary>
    /// NEW: Handle dropping on a storage container with precise positioning.
    /// </summary>
    private bool HandleStorageContainerDrop()
    {
        if (targetStorageContainer == null || playerInventoryManager == null)
        {
            DebugLogError("Cannot transfer to storage - missing references");
            RevertToOriginalState();
            return true; // Handled, even though failed
        }

        var containerManager = targetStorageContainer.GetContainerManager();
        if (containerManager == null)
        {
            DebugLogError("Cannot transfer - storage container manager not found");
            RevertToOriginalState();
            return true;
        }

        DebugLog($"Attempting to transfer {itemData.ItemData?.itemName} to storage container {containerManager.DisplayName}");

        // Check if we have a valid preview position to place at
        if (!hasValidStoragePreview)
        {
            DebugLog("No valid storage preview position - cannot transfer");
            RevertToOriginalState();
            return true;
        }

        // Restore the item to player inventory if it was removed during drag
        if (itemRemovedFromGrid)
        {
            DebugLog($"Restoring item {itemData.ID} to player inventory before transfer");

            // Restore to original position and rotation
            itemData.SetGridPosition(originalGridPosition);
            itemData.SetRotation(originalRotation);

            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
                DebugLog($"Item {itemData.ID} restored to player inventory successfully");
            }
            else
            {
                DebugLogError($"Failed to restore item {itemData.ID} to player inventory!");
                RevertToOriginalState();
                return true; // Handled, even though failed
            }
        }

        // Perform the precise transfer to storage container
        bool success = PerformPreciseStorageTransfer(containerManager);

        if (success)
        {
            DebugLog($"Successfully transferred {itemData.ItemData?.itemName} to storage container at {lastStoragePreviewPosition} with rotation {lastStoragePreviewRotation}");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLog($"Failed to transfer {itemData.ItemData?.itemName} to storage - reverting");
            RevertToOriginalState();
        }

        return true; // Handled
    }

    /// <summary>
    /// NEW: Perform precise transfer to storage container using exact preview position and rotation.
    /// </summary>
    private bool PerformPreciseStorageTransfer(StorageContainer containerManager)
    {
        DebugLog($"Performing precise storage transfer to position {lastStoragePreviewPosition} with rotation {lastStoragePreviewRotation}");

        // Double-check that the target position is still valid
        if (!containerManager.HasSpaceForItemAt(itemData.ItemData, lastStoragePreviewPosition, lastStoragePreviewRotation))
        {
            DebugLog("Storage target position is no longer valid - transfer cancelled");
            return false;
        }

        // Remove from player inventory
        if (playerInventoryManager.RemoveItem(itemData.ID))
        {
            DebugLog($"Successfully removed {itemData.ID} from player inventory");

            // Try to add to storage container at the exact preview position and rotation
            if (containerManager.AddItem(itemData.ItemData, lastStoragePreviewPosition, lastStoragePreviewRotation))
            {
                DebugLog($"Successfully added {itemData.ItemData?.itemName} to storage container at precise position");
                return true;
            }
            else
            {
                // Failed to add to storage - restore to player inventory
                DebugLogError("Failed to add item to storage container at precise position - attempting to restore to player");

                if (playerInventoryManager.AddItem(itemData.ItemData, originalGridPosition, originalRotation))
                {
                    DebugLog("Item restored to player inventory after failed storage transfer");
                }
                else
                {
                    DebugLogError("CRITICAL: Failed to restore item to player inventory after failed storage transfer!");
                }
                return false;
            }
        }
        else
        {
            DebugLogError("Failed to remove item from player inventory for storage transfer");
            return false;
        }
    }

    #endregion


    #region Clothing Slot Integration

    /// <summary>
    /// Handle dropping on a clothing slot with comprehensive validation.
    /// </summary>
    private bool HandleClothingSlotDrop(ClothingSlotUI clothingSlot)
    {
        // Check if this is a valid clothing item for ANY clothing slot
        if (itemData.ItemData?.itemType == ItemType.Clothing)
        {
            // Check if it can be equipped to THIS specific slot
            if (ClothingDragDropHelper.CanEquipToSlot(itemData, clothingSlot))
            {
                DebugLog($"Attempting to equip {itemData.ItemData?.itemName} to clothing slot {clothingSlot.TargetLayer}");

                // Restore item to inventory first if it was removed during drag
                if (itemRemovedFromGrid)
                {
                    if (!RestoreItemToInventoryForEquipment())
                    {
                        RevertToOriginalState();
                        return true; // Handled, even though failed
                    }
                }

                // Attempt equipment
                bool success = ClothingDragDropHelper.HandleClothingSlotDrop(itemData, clothingSlot);

                if (success)
                {
                    DebugLog($"Successfully equipped {itemData.ItemData?.itemName} to {clothingSlot.TargetLayer}");
                    OnItemDeselected?.Invoke();
                }
                else
                {
                    DebugLogWarning($"Failed to equip {itemData.ItemData?.itemName} to {clothingSlot.TargetLayer}");
                    RevertToOriginalState();
                }

                return true; // Handled
            }
            else
            {
                // Wrong slot for this clothing item
                var clothingData = itemData.ItemData.ClothingData;
                string itemName = itemData.ItemData.itemName;
                string targetSlotName = ClothingInventoryUtilities.GetFriendlyLayerName(clothingSlot.TargetLayer);

                if (clothingData != null && clothingData.validLayers.Length > 0)
                {
                    string validSlots = GetValidSlotsText(clothingData.validLayers);
                    DebugLogWarning($"Invalid clothing slot: {itemName} cannot be worn on {targetSlotName}. Can be worn on: {validSlots}");
                }
                else
                {
                    DebugLogWarning($"Invalid clothing slot: {itemName} cannot be worn on {targetSlotName}");
                }

                RevertToOriginalStateWithRejectionFeedback();
                return true; // Handled
            }
        }
        else
        {
            // Not a clothing item at all
            string itemTypeName = itemData.ItemData?.itemType.ToString() ?? "Unknown";
            DebugLogWarning($"Non-clothing item rejected: {itemData.ItemData?.itemName} is {itemTypeName}, not clothing");

            RevertToOriginalStateWithRejectionFeedback();
            return true; // Handled
        }
    }

    /// <summary>
    /// Get user-friendly text for valid clothing slots.
    /// </summary>
    private string GetValidSlotsText(ClothingLayer[] validLayers)
    {
        if (validLayers == null || validLayers.Length == 0)
            return "nowhere";

        string[] slotNames = new string[validLayers.Length];
        for (int i = 0; i < validLayers.Length; i++)
        {
            slotNames[i] = ClothingInventoryUtilities.GetFriendlyLayerName(validLayers[i]);
        }

        if (slotNames.Length == 1)
            return slotNames[0];
        else if (slotNames.Length == 2)
            return $"{slotNames[0]} or {slotNames[1]}";
        else
            return string.Join(", ", slotNames, 0, slotNames.Length - 1) + $", or {slotNames[slotNames.Length - 1]}";
    }

    /// <summary>
    /// Restore item to inventory specifically for equipment operations.
    /// </summary>
    private bool RestoreItemToInventoryForEquipment()
    {
        DebugLog($"Restoring item {itemData.ID} to inventory before equipment");

        itemData.SetGridPosition(originalGridPosition);
        itemData.SetRotation(originalRotation);

        if (gridVisual.GridData.PlaceItem(itemData))
        {
            itemRemovedFromGrid = false;
            DebugLog($"Item {itemData.ID} restored to inventory successfully");
            return true;
        }
        else
        {
            DebugLogError($"Failed to restore item {itemData.ID} to inventory!");
            gridVisual.GridData.RemoveItem(itemData.ID);
            if (!gridVisual.GridData.PlaceItem(itemData))
            {
                DebugLogError($"Could not restore item to inventory - aborting equipment");
                return false;
            }
            itemRemovedFromGrid = false;
            return true;
        }
    }

    #endregion

    #region Item Dropping

    /// <summary>
    /// Handle dropping item outside inventory with proper restoration.
    /// </summary>
    private bool HandleDropOutsideInventory()
    {
        DebugLog($"Item {itemData.ItemData?.itemName} dropped outside inventory - attempting to drop into scene");

        if (itemData?.ItemData?.CanDrop != true)
        {
            DebugLogWarning($"Cannot drop {itemData.ItemData?.itemName} - it's a key item");
            RevertToOriginalState();
            return true; // Handled
        }

        // Restore item to inventory first if it was removed during drag
        if (itemRemovedFromGrid)
        {
            DebugLog($"Restoring item {itemData.ID} to inventory before dropping");

            itemData.SetGridPosition(originalGridPosition);
            itemData.SetRotation(originalRotation);

            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
                DebugLog($"Item {itemData.ID} restored to inventory successfully");
            }
            else
            {
                DebugLogError($"Failed to restore item {itemData.ID} to inventory before dropping!");
                gridVisual.GridData.RemoveItem(itemData.ID);
                if (!gridVisual.GridData.PlaceItem(itemData))
                {
                    DebugLogError($"Could not restore item to inventory - aborting drop");
                    return true; // Handled, even though failed
                }
                itemRemovedFromGrid = false;
            }
        }

        // Now try to drop the item using player inventory manager
        bool success = false;
        if (playerInventoryManager != null)
        {
            var dropResult = playerInventoryManager.TryDropItem(itemData.ID);
            success = dropResult.success;
        }
        else
        {
            // Fallback to direct ItemDropSystem
            success = ItemDropSystem.DropItemFromInventory(itemData.ID);
        }

        if (success)
        {
            DebugLog($"Successfully dropped {itemData.ItemData?.itemName} into scene");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLogWarning($"Failed to drop {itemData.ItemData?.itemName} - reverting to original position");
            RevertToOriginalState();
        }

        return true; // Handled
    }

    /// <summary>
    /// Check if the item is being dragged outside inventory bounds.
    /// </summary>
    private void CheckIfOutsideInventoryBounds()
    {
        if (gridVisual == null) return;

        RectTransform gridRect = gridVisual.GetComponent<RectTransform>();
        if (gridRect == null) return;

        Vector2 localPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position),
            canvas.worldCamera,
            out localPosition);

        Rect gridBounds = gridRect.rect;
        gridBounds.xMin -= dropOutsideBuffer;
        gridBounds.xMax += dropOutsideBuffer;
        gridBounds.yMin -= dropOutsideBuffer;
        gridBounds.yMax += dropOutsideBuffer;

        isDraggedOutsideInventory = !gridBounds.Contains(localPosition);

        // Visual feedback for dragging outside
        if (isDraggedOutsideInventory)
        {
            canvasGroup.alpha = 0.6f;
        }
        else
        {
            canvasGroup.alpha = 0.8f;
        }
    }

    #endregion

    #region Enhanced Animation and Feedback

    /// <summary>
    /// Revert to original state with special rejection feedback.
    /// </summary>
    private void RevertToOriginalStateWithRejectionFeedback()
    {
        DebugLog($"Reverting {itemData.ItemData?.itemName} to original position due to invalid drop");

        // Revert rotation if changed
        if (itemData.currentRotation != originalRotation)
        {
            itemData.SetRotation(originalRotation);
            visualRenderer?.RefreshVisual();
        }

        // Restore original position
        itemData.SetGridPosition(originalGridPosition);

        // Place item back in grid at original position
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

        // Animate back with special rejection animation
        AnimateToOriginalPositionWithRejectionFeedback();
        visualRenderer?.RefreshHotkeyIndicatorVisuals();
    }

    /// <summary>
    /// Animate back to original position with rejection feedback.
    /// </summary>
    private void AnimateToOriginalPositionWithRejectionFeedback()
    {
        // First shake the item to indicate rejection
        var originalPos = originalPosition;

        // Quick shake animation
        rectTransform.DOShakePosition(0.3f, 15f, 10, 90, false, true)
            .OnComplete(() =>
            {
                // Then smoothly animate back to original position
                rectTransform.DOLocalMove(originalPos, snapAnimationDuration * 1.5f)
                    .SetEase(Ease.OutBack);
            });

        // Also add a brief color flash to the visual renderer if possible
        if (visualRenderer != null)
        {
            visualRenderer.SetAlpha(0.5f);
            DOVirtual.Float(0.5f, 1f, 0.5f, (alpha) => visualRenderer.SetAlpha(alpha));
        }
    }

    #endregion

    #region Context Menu Integration

    /// <summary>
    /// Handle dropdown menu action selection.
    /// </summary>
    protected override void OnDropdownActionSelected(InventoryItemData selectedItem, string actionId)
    {
        base.OnDropdownActionSelected(selectedItem, actionId);

        if (itemData != selectedItem)
        {
            return;
        }

        // Handle clothing wear actions
        if (actionId.StartsWith("wear_"))
        {
            string layerName = actionId.Substring(5);
            if (System.Enum.TryParse<ClothingLayer>(layerName, out ClothingLayer targetLayer))
            {
                WearInSlot(targetLayer);
            }
            return;
        }

        switch (actionId)
        {
            case "assign_hotkey":
                AssignHotkey();
                break;
            case "unload":
                UnloadWeapon();
                break;
        }
    }

    #endregion

    #region Dropdown Action Handlers

    protected override void TransferItem()
    {
        DebugLog("Trying to transfer to the other inventory");

        if (StorageContainerUI.Instance != null && StorageContainerUI.Instance.currentContainer != null)
        {
            bool success = StorageContainerUI.Instance.currentContainer.AddItem(itemData);

            if (success)
            {
                DebugLog($"Successfully transferred {itemData.ItemData.itemName} to storage container");
                playerInventoryManager.RemoveItem(itemData.ID);
                OnItemDeselected?.Invoke();
            }
            else
            {
                DebugLogWarning($"Failed to transfer {itemData.ItemData.itemName} to storage container");
            }
        }
        else
        {
            DebugLogWarning("No storage container open - cannot transfer item");
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

        if (playerInventoryManager != null)
        {
            playerInventoryManager.RemoveItem(itemData.ID);
            OnItemDeselected?.Invoke();
        }
    }

    private void AssignHotkey()
    {
        Debug.Log($"AssignHotkey called for item ID: {itemData?.ID}");
        if (itemData?.ItemData == null)
        {
            DebugLogWarning("Cannot assign hotkey - no item data");
            return;
        }

        DebugLog($"Assigning hotkey for {itemData.ItemData.itemName}");
        ShowHotkeySelectionUI();
    }

    private void ShowHotkeySelectionUI()
    {
        if (HotkeySelectionUI.Instance != null)
        {
            HotkeySelectionUI.Instance.ShowSelection(itemData);
        }
        else
        {
            AutoAssignToAvailableSlot();
        }
    }

    private void AutoAssignToAvailableSlot()
    {
        if (EquippedItemManager.Instance == null) return;

        var bindings = EquippedItemManager.Instance.GetAllHotkeyBindings();

        foreach (var binding in bindings)
        {
            if (binding.isAssigned)
            {
                var assignedItemData = binding.GetCurrentItemData();
                if (assignedItemData != null && assignedItemData.name == itemData.ItemData.name)
                {
                    bool success = EquippedItemManager.Instance.AssignItemToHotkey(itemData.ID, binding.slotNumber);
                    if (success)
                    {
                        DebugLog($"Added {itemData.ItemData.itemName} to existing hotkey {binding.slotNumber} stack");
                    }
                    return;
                }
            }
        }

        foreach (var binding in bindings)
        {
            if (!binding.isAssigned)
            {
                bool success = EquippedItemManager.Instance.AssignItemToHotkey(itemData.ID, binding.slotNumber);
                if (success)
                {
                    DebugLog($"Assigned {itemData.ItemData.itemName} to hotkey {binding.slotNumber}");
                }
                return;
            }
        }

        DebugLogWarning("All hotkey slots are occupied - cannot auto-assign");
    }

    private void UnloadWeapon()
    {
        if (itemData?.ItemData?.itemType != ItemType.RangedWeapon)
        {
            DebugLogWarning("Cannot unload non-weapon item");
            return;
        }

        var weaponData = itemData.ItemData.RangedWeaponData;
        if (weaponData == null || weaponData.currentAmmo <= 0)
        {
            DebugLogWarning("No ammo to unload");
            return;
        }

        DebugLog($"Unloading {weaponData.currentAmmo} rounds from {itemData.ItemData.itemName}");

        if (weaponData.requiredAmmoType != null)
        {
            DebugLog($"Would add {weaponData.currentAmmo} {weaponData.requiredAmmoType.itemName} to inventory");
        }
    }

    protected override void DropItem()
    {
        if (itemData?.ItemData?.CanDrop != true)
        {
            DebugLogWarning($"Cannot drop {itemData.ItemData.itemName} - it's a key item");
            return;
        }

        if (itemData?.ID == null)
        {
            DebugLogWarning("Cannot drop item - no item data or ID");
            return;
        }

        bool success = false;
        if (playerInventoryManager != null)
        {
            var dropResult = playerInventoryManager.TryDropItem(itemData.ID);
            success = dropResult.success;
        }
        else
        {
            success = ItemDropSystem.DropItemFromInventory(itemData.ID);
        }

        if (success)
        {
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLogWarning($"Failed to drop {itemData.ItemData?.itemName}");
        }
    }

    /// <summary>
    /// Equips the item to the specified clothing layer with improved error handling.
    /// </summary>
    private void WearInSlot(ClothingLayer targetLayer)
    {
        if (itemData?.ItemData?.itemType != ItemType.Clothing)
        {
            DebugLogWarning("Cannot wear - not a clothing item");
            return;
        }

        if (ClothingManager.Instance == null)
        {
            DebugLogWarning("ClothingManager not found - cannot equip clothing");
            return;
        }

        DebugLog($"Equipping {itemData.ItemData.itemName} to {targetLayer}");

        var validation = ClothingInventoryUtilities.ValidateClothingEquip(itemData, targetLayer);
        if (!validation.IsValid)
        {
            DebugLogWarning($"Cannot equip {itemData.ItemData.itemName} to {targetLayer}: {validation.Message}");
            return;
        }

        var slot = ClothingManager.Instance.GetSlot(targetLayer);
        if (slot != null && !slot.IsEmpty)
        {
            var swapValidation = ClothingInventoryUtilities.ValidateSwapOperation(itemData, targetLayer);
            if (!swapValidation.IsValid)
            {
                DebugLogWarning($"Cannot swap {itemData.ItemData.itemName}: {swapValidation.Message}");
                return;
            }
        }

        bool success = ClothingManager.Instance.EquipItemToLayer(itemData.ID, targetLayer);
        if (success)
        {
            DebugLog($"Successfully equipped {itemData.ItemData.itemName} to {targetLayer}");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLogWarning($"Failed to equip {itemData.ItemData.itemName} to {targetLayer}");
        }
    }

    #endregion

    #region Backward Compatibility

    /// <summary>
    /// Get the player inventory manager (for backward compatibility).
    /// </summary>
    public PlayerInventoryManager GetPlayerInventoryManager()
    {
        return playerInventoryManager;
    }

    #endregion
}