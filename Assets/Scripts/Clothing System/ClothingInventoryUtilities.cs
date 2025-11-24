using UnityEngine;

/// <summary>
/// PHASE 1: Utility class for clothing and inventory integration
/// Provides centralized validation, error handling, and user feedback
/// </summary>
public static class ClothingInventoryUtilities
{
    /// <summary>
    /// Validates that a clothing item can be equipped to a specific layer
    /// </summary>
    public static ValidationResult ValidateClothingEquip(InventoryItemData item, ClothingLayer targetLayer)
    {
        if (item == null)
        {
            return new ValidationResult(false, "No item selected");
        }

        if (item.ItemData == null)
        {
            return new ValidationResult(false, "Item has no data");
        }

        if (item.ItemData.itemType != ItemType.Clothing)
        {
            return new ValidationResult(false, "Not a clothing item");
        }

        var clothingData = item.ItemData.ClothingData;
        if (clothingData == null)
        {
            return new ValidationResult(false, "No clothing data found");
        }

        if (!clothingData.CanEquipToLayer(targetLayer))
        {
            string layerName = GetFriendlyLayerName(targetLayer);
            return new ValidationResult(false, $"Cannot equip to {layerName}");
        }

        return new ValidationResult(true, "Valid for equipment");
    }

    /// <summary>
    /// Validates that there's inventory space for an item
    /// </summary>
    public static ValidationResult ValidateInventorySpace(ItemData itemData)
    {
        if (PlayerInventoryManager.Instance == null)
        {
            return new ValidationResult(false, "Inventory system not available");
        }

        if (itemData == null)
        {
            return new ValidationResult(false, "No item to validate");
        }

        bool hasSpace = PlayerInventoryManager.Instance.HasSpaceForItem(itemData);
        if (!hasSpace)
        {
            return new ValidationResult(false, "Inventory is full");
        }

        return new ValidationResult(true, "Inventory has space");
    }

    /// <summary>
    /// REFACTORED: Validates same-space swap operation before execution
    /// Now checks if displaced item can fit in the exact same space as the new item
    /// </summary>
    public static ValidationResult ValidateSwapOperation(InventoryItemData newItem, ClothingLayer targetLayer)
    {
        var clothingManager = ClothingManager.Instance;
        if (clothingManager == null)
        {
            return new ValidationResult(false, "Clothing system not available");
        }

        // First validate the new item can be equipped
        var equipValidation = ValidateClothingEquip(newItem, targetLayer);
        if (!equipValidation.IsValid)
        {
            return equipValidation;
        }

        // Check if slot is occupied
        var slot = clothingManager.GetSlot(targetLayer);
        if (slot == null)
        {
            return new ValidationResult(false, "Clothing slot not found");
        }

        if (slot.IsEmpty)
        {
            return new ValidationResult(true, "Empty slot - no swap needed");
        }

        // REFACTORED: Check if displaced item can fit in the same space as the new item
        var currentItemData = slot.GetEquippedItemData();
        if (currentItemData == null)
        {
            return new ValidationResult(false, "Current equipped item data not found");
        }

        // Validate same-space swap: can the displaced item fit where the new item currently is?
        var sameSpaceValidation = ValidateSameSpaceSwap(newItem, currentItemData);
        if (!sameSpaceValidation.IsValid)
        {
            return new ValidationResult(false, $"Cannot swap - {sameSpaceValidation.Message}");
        }

        return new ValidationResult(true, "Same-space swap is possible");
    }

    /// <summary>
    /// NEW: Validates that displaced item can fit in the same space as the new item
    /// </summary>
    public static ValidationResult ValidateSameSpaceSwap(InventoryItemData newItem, ItemData displacedItemData)
    {
        if (PlayerInventoryManager.Instance == null)
        {
            return new ValidationResult(false, "Inventory system not available");
        }

        if (newItem == null || displacedItemData == null)
        {
            return new ValidationResult(false, "Invalid item data for swap validation");
        }

        var inventory = PlayerInventoryManager.Instance;
        Vector2Int originalPosition = newItem.GridPosition;

        // Create a temporary test item for the displaced clothing at the original position
        var tempDisplacedItem = new InventoryItemData("temp_displaced_validation", displacedItemData, originalPosition);

        // Try each possible rotation of the displaced item to see if it fits
        int maxRotations = TetrominoDefinitions.GetRotationCount(tempDisplacedItem.shapeType);

        // Temporarily remove the original item to test space
        var originalItem = inventory.InventoryGridData.GetItem(newItem.ID);
        if (originalItem == null)
        {
            return new ValidationResult(false, "New item not found in inventory");
        }

        // Store original state
        var originalGridPosition = originalItem.GridPosition;
        var originalItemRotation = originalItem.currentRotation;

        // Remove original item temporarily
        inventory.InventoryGridData.RemoveItem(newItem.ID);

        bool canFitSomewhere = false;
        for (int rotation = 0; rotation < maxRotations; rotation++)
        {
            tempDisplacedItem.SetRotation(rotation);
            tempDisplacedItem.SetGridPosition(originalPosition);

            if (inventory.InventoryGridData.IsValidPosition(originalPosition, tempDisplacedItem))
            {
                canFitSomewhere = true;
                break;
            }
        }

        // Restore original item
        originalItem.SetRotation(originalItemRotation);
        originalItem.SetGridPosition(originalGridPosition);
        inventory.InventoryGridData.PlaceItem(originalItem);

        if (canFitSomewhere)
        {
            return new ValidationResult(true, $"Displaced item {displacedItemData.itemName} can fit at original position");
        }
        else
        {
            return new ValidationResult(false, $"Displaced item {displacedItemData.itemName} cannot fit at original position");
        }
    }

    /// <summary>
    /// Gets user-friendly layer names for error messages
    /// </summary>
    public static string GetFriendlyLayerName(ClothingLayer layer)
    {
        return layer switch
        {
            ClothingLayer.HeadUpper => "Head (Upper)",
            ClothingLayer.HeadLower => "Head (Lower)",
            ClothingLayer.TorsoInner => "Torso (Inner)",
            ClothingLayer.TorsoOuter => "Torso (Outer)",
            ClothingLayer.LegsInner => "Legs (Inner)",
            ClothingLayer.LegsOuter => "Legs (Outer)",
            ClothingLayer.Hands => "Hands",
            ClothingLayer.Socks => "Socks",
            ClothingLayer.Shoes => "Shoes",
            _ => layer.ToString()
        };
    }

    /// <summary>
    /// Gets short layer names for UI display
    /// </summary>
    public static string GetShortLayerName(ClothingLayer layer)
    {
        return layer switch
        {
            ClothingLayer.HeadUpper => "Hat",
            ClothingLayer.HeadLower => "Scarf",
            ClothingLayer.TorsoInner => "Shirt",
            ClothingLayer.TorsoOuter => "Jacket",
            ClothingLayer.LegsInner => "Under",
            ClothingLayer.LegsOuter => "Pants",
            ClothingLayer.Hands => "Gloves",
            ClothingLayer.Socks => "Socks",
            ClothingLayer.Shoes => "Shoes",
            _ => layer.ToString()
        };
    }

    /// <summary>
    /// Provides contextual error messages for common failure scenarios
    /// </summary>
    public static string GetContextualErrorMessage(ClothingOperationResult result)
    {
        return result.ResultType switch
        {
            ClothingOperationType.Equip when !result.Success => $"Cannot equip: {result.Message}",
            ClothingOperationType.Unequip when !result.Success => $"Cannot unequip: {result.Message}",
            ClothingOperationType.Swap when !result.Success => $"Cannot swap: {result.Message}",
            _ => result.Message
        };
    }

    /// <summary>
    /// Checks if an item is currently equipped anywhere
    /// </summary>
    public static bool IsItemCurrentlyEquipped(string itemId)
    {
        var clothingManager = ClothingManager.Instance;
        if (clothingManager == null) return false;

        return clothingManager.IsItemEquipped(itemId);
    }

    /// <summary>
    /// Gets the layer where an item is currently equipped
    /// </summary>
    public static ClothingLayer? GetEquippedLayer(string itemId)
    {
        var clothingManager = ClothingManager.Instance;
        if (clothingManager == null) return null;

        var slot = clothingManager.GetSlotForItem(itemId);
        return slot?.layer;
    }
}

/// <summary>
/// Result structure for validation operations
/// </summary>
public struct ValidationResult
{
    public bool IsValid { get; }
    public string Message { get; }

    public ValidationResult(bool isValid, string message)
    {
        IsValid = isValid;
        Message = message;
    }
}

/// <summary>
/// Result structure for clothing operations
/// </summary>
public struct ClothingOperationResult
{
    public bool Success { get; }
    public string Message { get; }
    public ClothingOperationType ResultType { get; }
    public ClothingLayer? TargetLayer { get; }

    public ClothingOperationResult(bool success, string message, ClothingOperationType type, ClothingLayer? layer = null)
    {
        Success = success;
        Message = message;
        ResultType = type;
        TargetLayer = layer;
    }
}

/// <summary>
/// Types of clothing operations for result categorization
/// </summary>
public enum ClothingOperationType
{
    Equip,
    Unequip,
    Swap,
    Drop
}