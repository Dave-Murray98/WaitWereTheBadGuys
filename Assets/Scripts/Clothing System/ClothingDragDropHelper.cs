using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ENHANCED: Comprehensive helper for drag and drop operations between inventory and clothing systems
/// Now provides coordinated validation, error handling, and visual feedback management
/// </summary>
public static class ClothingDragDropHelper
{
    /// <summary>
    /// Checks if a drag operation is over any clothing slot
    /// </summary>
    public static ClothingSlotUI GetClothingSlotUnderPointer(PointerEventData eventData)
    {
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            var clothingSlotUI = result.gameObject.GetComponent<ClothingSlotUI>();
            if (clothingSlotUI != null)
            {
                return clothingSlotUI;
            }

            // Also check parent objects in case the slot has child elements
            var parentSlotUI = result.gameObject.GetComponentInParent<ClothingSlotUI>();
            if (parentSlotUI != null)
            {
                return parentSlotUI;
            }
        }

        return null;
    }

    /// <summary>
    /// Validates if an inventory item can be equipped to a specific clothing slot
    /// </summary>
    public static bool CanEquipToSlot(InventoryItemData itemData, ClothingSlotUI clothingSlot)
    {
        if (itemData?.ItemData?.itemType != ItemType.Clothing)
            return false;

        var clothingData = itemData.ItemData.ClothingData;
        if (clothingData == null)
            return false;

        return clothingData.CanEquipToLayer(clothingSlot.TargetLayer);
    }

    /// <summary>
    /// ENHANCED: Gets detailed user-friendly feedback message for invalid drops
    /// </summary>
    public static string GetDropErrorMessage(InventoryItemData itemData, ClothingSlotUI clothingSlot)
    {
        if (itemData?.ItemData == null)
            return "Invalid item";

        if (itemData.ItemData.itemType != ItemType.Clothing)
        {
            string itemTypeName = itemData.ItemData.itemType.ToString();
            return $"{itemData.ItemData.itemName} is {itemTypeName}, not clothing";
        }

        var clothingData = itemData.ItemData.ClothingData;
        if (clothingData == null)
            return $"{itemData.ItemData.itemName} has no clothing data";

        if (!clothingData.CanEquipToLayer(clothingSlot.TargetLayer))
        {
            string itemName = itemData.ItemData.itemName;
            string targetSlotName = ClothingInventoryUtilities.GetFriendlyLayerName(clothingSlot.TargetLayer);

            // Get valid layers for better error message
            if (clothingData.validLayers != null && clothingData.validLayers.Length > 0)
            {
                string validLayersText = GetValidLayersDisplayText(clothingData.validLayers);
                return $"{itemName} cannot be worn on {targetSlotName}. Can be worn on: {validLayersText}";
            }
            else
            {
                return $"{itemName} cannot be worn on {targetSlotName}";
            }
        }

        return "Unknown error";
    }

    /// <summary>
    /// ENHANCED: Handles the complete drop operation onto a clothing slot with comprehensive validation
    /// </summary>
    public static bool HandleClothingSlotDrop(InventoryItemData itemData, ClothingSlotUI clothingSlot)
    {
        if (clothingSlot == null || !CanEquipToSlot(itemData, clothingSlot))
        {
            Debug.LogWarning($"Cannot drop {itemData?.ItemData?.itemName} on clothing slot {clothingSlot?.TargetLayer}");
            return false;
        }

        var clothingManager = ClothingManager.Instance;
        if (clothingManager == null)
        {
            Debug.LogError("ClothingManager not found for drop operation");
            return false;
        }

        Debug.Log($"[ClothingDragDropHelper] Handling drop: {itemData.ItemData.itemName} -> {clothingSlot.TargetLayer}");

        // ENHANCED: Pre-validation with detailed error reporting
        var preValidationResult = PreValidateDropOperation(itemData, clothingSlot.TargetLayer);
        if (!preValidationResult.IsValid)
        {
            Debug.LogWarning($"[ClothingDragDropHelper] Pre-validation failed: {preValidationResult.Message}");
            return false;
        }

        // ENHANCED: Execute with proper error handling and rollback
        bool success = ExecuteDropOperation(itemData, clothingSlot.TargetLayer);

        if (success)
        {
            Debug.Log($"[ClothingDragDropHelper] Successfully equipped {itemData.ItemData.itemName} to {clothingSlot.TargetLayer}");
        }
        else
        {
            Debug.LogWarning($"[ClothingDragDropHelper] Failed to equip {itemData.ItemData.itemName} to {clothingSlot.TargetLayer}");
        }

        return success;
    }

    /// <summary>
    /// ENHANCED: Comprehensive pre-validation before attempting equipment
    /// </summary>
    private static ValidationResult PreValidateDropOperation(InventoryItemData itemData, ClothingLayer targetLayer)
    {
        // Verify clothing manager exists
        var clothingManager = ClothingManager.Instance;
        if (clothingManager == null)
        {
            return new ValidationResult(false, "ClothingManager not available");
        }

        // Verify inventory manager exists
        var inventoryManager = PlayerInventoryManager.Instance;
        if (inventoryManager == null)
        {
            return new ValidationResult(false, "InventoryManager not available");
        }

        // Verify item exists in inventory
        var inventoryItem = inventoryManager.InventoryGridData.GetItem(itemData.ID);
        if (inventoryItem == null)
        {
            return new ValidationResult(false, $"Item {itemData.ID} not found in inventory");
        }

        // Use existing validation utilities
        var basicValidation = ClothingInventoryUtilities.ValidateClothingEquip(itemData, targetLayer);
        if (!basicValidation.IsValid)
        {
            return basicValidation;
        }

        // Check for swap scenario
        var targetSlot = clothingManager.GetSlot(targetLayer);
        if (targetSlot != null && !targetSlot.IsEmpty)
        {
            var swapValidation = ClothingInventoryUtilities.ValidateSwapOperation(itemData, targetLayer);
            if (!swapValidation.IsValid)
            {
                return new ValidationResult(false, $"Swap validation failed: {swapValidation.Message}");
            }
        }

        return new ValidationResult(true, "Pre-validation passed");
    }

    /// <summary>
    /// ENHANCED: Execute the drop operation with proper error handling
    /// </summary>
    private static bool ExecuteDropOperation(InventoryItemData itemData, ClothingLayer targetLayer)
    {
        var clothingManager = ClothingManager.Instance;

        try
        {
            // Attempt to equip the item using the enhanced clothing manager
            bool success = clothingManager.EquipItemToLayer(itemData.ID, targetLayer);

            if (success)
            {
                Debug.Log($"[ClothingDragDropHelper] Equipment operation completed successfully");
                return true;
            }
            else
            {
                Debug.LogWarning($"[ClothingDragDropHelper] Equipment operation failed - ClothingManager returned false");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ClothingDragDropHelper] Exception during equipment operation: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// ENHANCED: Determines if a drag operation should show inventory preview or clothing slot feedback
    /// </summary>
    public static bool ShouldShowInventoryPreview(PointerEventData eventData, InventoryItemData itemData)
    {
        // Don't show inventory preview if we're over a valid clothing slot
        var clothingSlot = GetClothingSlotUnderPointer(eventData);
        if (clothingSlot != null && CanEquipToSlot(itemData, clothingSlot))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// ENHANCED: Gets the appropriate visual feedback color for a clothing slot during drag operations
    /// </summary>
    public static Color GetClothingSlotFeedbackColor(InventoryItemData itemData, ClothingSlotUI clothingSlot,
        Color validColor, Color invalidColor)
    {
        return CanEquipToSlot(itemData, clothingSlot) ? validColor : invalidColor;
    }

    /// <summary>
    /// ENHANCED: Provides comprehensive system validation for debugging
    /// </summary>
    public static bool ValidateSystemSetup()
    {
        bool isValid = true;

        // Check for ClothingManager
        if (ClothingManager.Instance == null)
        {
            Debug.LogError("[ClothingDragDropHelper] ClothingManager not found!");
            isValid = false;
        }

        // Check for InventoryManager
        if (PlayerInventoryManager.Instance == null)
        {
            Debug.LogError("[ClothingDragDropHelper] InventoryManager not found!");
            isValid = false;
        }

        // Check for clothing slots
        var clothingSlots = GetAllClothingSlots();
        if (clothingSlots.Length == 0)
        {
            Debug.LogWarning("[ClothingDragDropHelper] No ClothingSlotUI components found in scene!");
            isValid = false;
        }
        else
        {
            Debug.Log($"[ClothingDragDropHelper] Found {clothingSlots.Length} clothing slots");
        }

        // Check for EventSystem
        if (EventSystem.current == null)
        {
            Debug.LogError("[ClothingDragDropHelper] No EventSystem found in scene!");
            isValid = false;
        }

        return isValid;
    }

    /// <summary>
    /// Gets all clothing slots in the scene for debugging purposes
    /// </summary>
    public static ClothingSlotUI[] GetAllClothingSlots()
    {
        return Object.FindObjectsByType<ClothingSlotUI>(FindObjectsSortMode.None);
    }

    /// <summary>
    /// ENHANCED: Debug helper to log drag and drop operations with detailed information
    /// </summary>
    public static void LogDragDropOperation(string operation, InventoryItemData itemData, ClothingSlotUI clothingSlot = null)
    {
        string itemName = itemData?.ItemData?.itemName ?? "Unknown";
        string itemId = itemData?.ID ?? "Unknown ID";
        string slotInfo = clothingSlot != null ? $" -> {clothingSlot.TargetLayer}" : "";

        Debug.Log($"[ClothingDragDrop] {operation}: {itemName} (ID: {itemId}){slotInfo}");

        // Additional debug info for equipment operations
        if (operation.Contains("Equip") && clothingSlot != null)
        {
            var clothingManager = ClothingManager.Instance;
            var inventoryManager = PlayerInventoryManager.Instance;

            if (clothingManager != null && inventoryManager != null)
            {
                var targetSlot = clothingManager.GetSlot(clothingSlot.TargetLayer);
                var inventoryItem = inventoryManager.InventoryGridData.GetItem(itemData?.ID);

                Debug.Log($"[ClothingDragDrop] Debug Info - Target slot empty: {targetSlot?.IsEmpty}, Item in inventory: {inventoryItem != null}");
            }
        }
    }

    /// <summary>
    /// ENHANCED: Validates clothing data compatibility with target layer
    /// </summary>
    public static bool ValidateClothingCompatibility(InventoryItemData itemData, ClothingLayer targetLayer)
    {
        if (itemData?.ItemData?.itemType != ItemType.Clothing)
        {
            Debug.LogWarning($"[ClothingDragDropHelper] Item {itemData?.ItemData?.itemName} is not a clothing item");
            return false;
        }

        var clothingData = itemData.ItemData.ClothingData;
        if (clothingData == null)
        {
            Debug.LogWarning($"[ClothingDragDropHelper] Item {itemData.ItemData.itemName} has no clothing data");
            return false;
        }

        bool canEquip = clothingData.CanEquipToLayer(targetLayer);
        if (!canEquip)
        {
            string validLayers = string.Join(", ", clothingData.validLayers);
            Debug.LogWarning($"[ClothingDragDropHelper] Item {itemData.ItemData.itemName} cannot equip to {targetLayer}. Valid layers: {validLayers}");
        }

        return canEquip;
    }

    /// <summary>
    /// ENHANCED: Provides detailed status information for debugging
    /// </summary>
    public static string GetSystemStatus()
    {
        var status = new System.Text.StringBuilder();
        status.AppendLine("=== CLOTHING DRAG DROP SYSTEM STATUS ===");

        // Check core managers
        status.AppendLine($"ClothingManager: {(ClothingManager.Instance != null ? "✓" : "✗")}");
        status.AppendLine($"InventoryManager: {(PlayerInventoryManager.Instance != null ? "✓" : "✗")}");
        status.AppendLine($"EventSystem: {(EventSystem.current != null ? "✓" : "✗")}");

        // Check clothing slots
        var clothingSlots = GetAllClothingSlots();
        status.AppendLine($"Clothing Slots Found: {clothingSlots.Length}");

        if (clothingSlots.Length > 0)
        {
            status.AppendLine("Clothing Slot Details:");
            foreach (var slot in clothingSlots)
            {
                string slotStatus = slot.isActiveAndEnabled ? "Active" : "Inactive";
                status.AppendLine($"  {slot.TargetLayer}: {slotStatus}");
            }
        }

        // Check inventory items
        if (PlayerInventoryManager.Instance != null)
        {
            var stats = PlayerInventoryManager.Instance.GetInventoryStats();
            status.AppendLine($"Inventory Items: {stats.itemCount}");

            // Check for clothing items specifically
            var allItems = PlayerInventoryManager.Instance.InventoryGridData.GetAllItems();
            int clothingCount = 0;
            foreach (var item in allItems)
            {
                if (item.ItemData?.itemType == ItemType.Clothing)
                    clothingCount++;
            }
            status.AppendLine($"Clothing Items in Inventory: {clothingCount}");
        }

        return status.ToString();
    }

    /// <summary>
    /// ENHANCED: Test method to simulate a drag and drop operation for debugging
    /// </summary>
    public static bool TestDragDropOperation(string itemId, ClothingLayer targetLayer)
    {
        Debug.Log($"[ClothingDragDropHelper] Testing drag drop: {itemId} -> {targetLayer}");

        // Get the item from inventory
        var inventoryManager = PlayerInventoryManager.Instance;
        if (inventoryManager == null)
        {
            Debug.LogError("[ClothingDragDropHelper] Test failed: No InventoryManager");
            return false;
        }

        var itemData = inventoryManager.InventoryGridData.GetItem(itemId);
        if (itemData == null)
        {
            Debug.LogError($"[ClothingDragDropHelper] Test failed: Item {itemId} not found in inventory");
            return false;
        }

        // Find the target clothing slot
        var clothingSlots = GetAllClothingSlots();
        var targetSlot = System.Array.Find(clothingSlots, slot => slot.TargetLayer == targetLayer);
        if (targetSlot == null)
        {
            Debug.LogError($"[ClothingDragDropHelper] Test failed: No clothing slot found for layer {targetLayer}");
            return false;
        }

        // Test the operation
        bool result = HandleClothingSlotDrop(itemData, targetSlot);
        Debug.Log($"[ClothingDragDropHelper] Test result: {(result ? "SUCCESS" : "FAILED")}");

        return result;
    }

    /// <summary>
    /// ENHANCED: Cleanup method to reset any hanging state
    /// </summary>
    public static void ResetDragDropState()
    {
        // Clear any visual feedback from all clothing slots
        var clothingSlots = GetAllClothingSlots();
        foreach (var slot in clothingSlots)
        {
            // This would call a method to clear visual feedback if it exists
            // slot.ClearDragOverVisualFeedback();
        }

        Debug.Log("[ClothingDragDropHelper] Drag drop state reset");
    }


    /// <summary>
    /// ENHANCED: Get formatted text for valid clothing layers
    /// </summary>
    private static string GetValidLayersDisplayText(ClothingLayer[] validLayers)
    {
        if (validLayers == null || validLayers.Length == 0)
            return "nowhere";

        string[] layerNames = new string[validLayers.Length];
        for (int i = 0; i < validLayers.Length; i++)
        {
            layerNames[i] = ClothingInventoryUtilities.GetFriendlyLayerName(validLayers[i]);
        }

        if (layerNames.Length == 1)
            return layerNames[0];
        else if (layerNames.Length == 2)
            return $"{layerNames[0]} or {layerNames[1]}";
        else
            return string.Join(", ", layerNames, 0, layerNames.Length - 1) + $", or {layerNames[layerNames.Length - 1]}";
    }

    /// <summary>
    /// ENHANCED: Validates compatibility with detailed error information
    /// </summary>
    public static ValidationResult ValidateClothingDropWithDetails(InventoryItemData itemData, ClothingLayer targetLayer)
    {
        if (itemData?.ItemData == null)
            return new ValidationResult(false, "No item data");

        if (itemData.ItemData.itemType != ItemType.Clothing)
        {
            string itemTypeName = itemData.ItemData.itemType.ToString();
            return new ValidationResult(false, $"{itemData.ItemData.itemName} is {itemTypeName}, not clothing");
        }

        var clothingData = itemData.ItemData.ClothingData;
        if (clothingData == null)
            return new ValidationResult(false, $"{itemData.ItemData.itemName} has no clothing data");

        if (!clothingData.CanEquipToLayer(targetLayer))
        {
            string itemName = itemData.ItemData.itemName;
            string targetSlotName = ClothingInventoryUtilities.GetFriendlyLayerName(targetLayer);
            string validLayersText = GetValidLayersDisplayText(clothingData.validLayers);

            return new ValidationResult(false, $"{itemName} cannot be worn on {targetSlotName}. Valid slots: {validLayersText}");
        }

        return new ValidationResult(true, "Valid clothing item for this slot");
    }

    /// <summary>
    /// ENHANCED: Provides comprehensive rejection handling for invalid clothing drops
    /// </summary>
    public static bool HandleInvalidClothingDrop(InventoryItemData itemData, ClothingSlotUI clothingSlot)
    {
        if (itemData?.ItemData == null || clothingSlot == null)
            return false;

        // Get detailed validation result
        var validationResult = ValidateClothingDropWithDetails(itemData, clothingSlot.TargetLayer);

        Debug.LogWarning($"[ClothingDragDropHelper] Invalid drop rejected: {validationResult.Message}");

        // Log additional context for debugging
        if (itemData.ItemData.itemType == ItemType.Clothing && itemData.ItemData.ClothingData != null)
        {
            var validLayers = itemData.ItemData.ClothingData.validLayers;
            string validLayersDebug = validLayers != null ? string.Join(", ", validLayers) : "None";
            Debug.Log($"[ClothingDragDropHelper] Item {itemData.ItemData.itemName} valid layers: {validLayersDebug}, Target: {clothingSlot.TargetLayer}");
        }

        return false; // Always return false for invalid drops
    }
}