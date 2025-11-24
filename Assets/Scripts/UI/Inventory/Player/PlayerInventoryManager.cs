using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// ENHANCED: Player-specific inventory manager that extends BaseInventoryManager.
/// Now supports precise item placement with position and rotation control,
/// enabling proper drag-and-drop preview functionality.
/// </summary>
public class PlayerInventoryManager : BaseInventoryManager
{
    public static PlayerInventoryManager Instance { get; private set; }

    [Header("Player Inventory Settings")]
    [SerializeField] private List<ItemData> testItems = new List<ItemData>();
    [SerializeField] private ItemData testItemToAdd;

    [Header("Drop System Integration")]
    [SerializeField] private bool validateDropsBeforeRemoval = true;

    [Header("Debug Controls")]
    [SerializeField] private KeyCode addItemKey = KeyCode.N;
    [SerializeField] private KeyCode clearInventoryKey = KeyCode.P;

    // Enhanced events for drop validation feedback
    public event Action<string, string> OnDropValidationFailed; // itemId, reason
    public event Action<string> OnDropValidationSucceeded; // itemId

    #region Singleton Management

    protected override void Awake()
    {
        // Handle singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Call base initialization
            base.Awake();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Update Loop (Debug Controls)

    private void Update()
    {
        HandleDebugInput();
    }

    private void HandleDebugInput()
    {
        if (Input.GetKeyDown(addItemKey) && testItems.Count > 0)
        {
            ItemData randomItem = testItems[UnityEngine.Random.Range(0, testItems.Count)];
            AddItem(randomItem, null, null);
        }

        if (Input.GetKeyDown(clearInventoryKey))
        {
            ClearInventory();
        }
    }

    #endregion

    #region Player-Specific Overrides

    /// <summary>
    /// Generate player-specific item IDs.
    /// </summary>
    protected override string GenerateItemId()
    {
        return $"player_item_{nextItemId}";
    }

    /// <summary>
    /// Player-specific item addition validation.
    /// </summary>
    protected override bool CanAddItem(ItemData itemData)
    {
        if (!base.CanAddItem(itemData))
            return false;

        // Add player-specific validation here if needed
        // For example: check player level, unlocked items, etc.

        return true;
    }

    /// <summary>
    /// Player-specific item removal validation.
    /// </summary>
    protected override bool CanRemoveItem(InventoryItemData item)
    {
        if (!base.CanRemoveItem(item))
            return false;

        // Players can remove most items, but add custom logic if needed
        // For example: prevent removing quest items, equipped items, etc.

        return true;
    }

    /// <summary>
    /// Player-specific notifications when items are added.
    /// </summary>
    protected override void OnItemAddedInternal(InventoryItemData item)
    {
        base.OnItemAddedInternal(item);

        // Add player-specific logic here
        // For example: update UI notifications, achievements, etc.

        DebugLog($"Player inventory: Added {item.ItemData?.itemName} at {item.GridPosition} with rotation {item.currentRotation}");
    }

    /// <summary>
    /// Player-specific notifications when items are removed.
    /// </summary>
    protected override void OnItemRemovedInternal(InventoryItemData item)
    {
        base.OnItemRemovedInternal(item);

        // Add player-specific logic here
        // For example: update equipped items, notify other systems, etc.

        DebugLog($"Player inventory: Removed {item.ItemData?.itemName}");
    }

    #endregion

    #region Enhanced Player Inventory Methods

    /// <summary>
    /// NEW: Add item with enhanced debugging for transfer operations.
    /// </summary>
    public override bool AddItem(ItemData itemData, Vector2Int? position = null, int? rotation = null)
    {
        string positionStr = position?.ToString() ?? "auto";
        string rotationStr = rotation?.ToString() ?? "auto";
        DebugLog($"AddItem called: {itemData?.itemName}, position={positionStr}, rotation={rotationStr}");

        bool result = base.AddItem(itemData, position, rotation);

        if (result)
        {
            DebugLog($"Successfully added {itemData?.itemName} to player inventory");
        }
        else
        {
            DebugLog($"Failed to add {itemData?.itemName} to player inventory");
        }

        return result;
    }

    /// <summary>
    /// NEW: Add existing item with enhanced debugging.
    /// </summary>
    public override bool AddItem(InventoryItemData existingItem, Vector2Int? position = null, int? rotation = null)
    {
        string positionStr = position?.ToString() ?? "auto";
        string rotationStr = rotation?.ToString() ?? "auto";
        DebugLog($"AddItem (existing) called: {existingItem?.ItemData?.itemName}, position={positionStr}, rotation={rotationStr}");

        bool result = base.AddItem(existingItem, position, rotation);

        if (result)
        {
            DebugLog($"Successfully added existing item {existingItem?.ItemData?.itemName} to player inventory");
        }
        else
        {
            DebugLog($"Failed to add existing item {existingItem?.ItemData?.itemName} to player inventory");
        }

        return result;
    }

    /// <summary>
    /// NEW: Try to add item at specific position with enhanced feedback.
    /// </summary>
    public override ItemPlacementResult TryAddItemAt(ItemData itemData, Vector2Int position, int rotation = 0)
    {
        DebugLog($"TryAddItemAt called: {itemData?.itemName} at {position} with rotation {rotation}");

        var result = base.TryAddItemAt(itemData, position, rotation);

        DebugLog($"TryAddItemAt result: {result.success} - {result.message}");

        return result;
    }

    #endregion

    #region Drop System Integration

    /// <summary>
    /// ENHANCED: Attempts to drop an item from inventory with comprehensive validation.
    /// Validates drop feasibility BEFORE removing from inventory to prevent item loss.
    /// Returns detailed result for UI feedback and error handling.
    /// </summary>
    public DropAttemptResult TryDropItem(string itemId, Vector3? customDropPosition = null)
    {
        DebugLog($"=== TryDropItem called for: {itemId} ===");

        // Step 1: Validate item exists in inventory
        var item = inventoryGridData.GetItem(itemId);
        if (item?.ItemData == null)
        {
            var result = new DropAttemptResult(false, itemId, "Item not found in inventory", DropFailureReason.ItemNotFound);
            DebugLog($"Drop failed: {result.reason}");
            OnDropValidationFailed?.Invoke(itemId, result.reason);
            return result;
        }

        ItemData itemData = item.ItemData;
        DebugLog($"Found item {itemData.itemName} in inventory");

        // Step 2: Check if item can be dropped (item type restriction)
        if (!itemData.CanDrop)
        {
            var result = new DropAttemptResult(false, itemId, $"Item {itemData.itemName} cannot be dropped (KeyItem)", DropFailureReason.ItemNotDroppable);
            DebugLog($"Drop failed: {result.reason}");
            OnDropValidationFailed?.Invoke(itemId, result.reason);
            return result;
        }

        // Step 3: Check if item has visual prefab (required for world spawning)
        if (!itemData.HasVisualPrefab)
        {
            var result = new DropAttemptResult(false, itemId, $"Item {itemData.itemName} has no visual prefab assigned", DropFailureReason.NoVisualPrefab);
            DebugLog($"Drop failed: {result.reason}");
            OnDropValidationFailed?.Invoke(itemId, result.reason);
            return result;
        }

        // Step 4: Validate with ItemDropSystem (if validation is enabled)
        if (validateDropsBeforeRemoval)
        {
            var dropValidation = ValidateWithDropSystem(itemData, customDropPosition);
            if (!dropValidation.isValid)
            {
                var result = new DropAttemptResult(false, itemId, $"Drop position invalid: {dropValidation.reason}", DropFailureReason.InvalidDropPosition);
                DebugLog($"Drop validation failed: {result.reason}");
                OnDropValidationFailed?.Invoke(itemId, result.reason);
                return result;
            }
        }

        // Step 5: All validations passed - proceed with removal and drop
        DebugLog($"All validations passed for {itemData.itemName} - proceeding with drop");

        // Remove from inventory first
        if (!RemoveItem(itemId))
        {
            var result = new DropAttemptResult(false, itemId, "Failed to remove item from inventory grid", DropFailureReason.InventoryRemovalFailed);
            DebugLog($"Drop failed: {result.reason}");
            OnDropValidationFailed?.Invoke(itemId, result.reason);
            return result;
        }

        // Attempt to drop into world
        bool dropSuccess = AttemptWorldDrop(itemData, customDropPosition);

        if (dropSuccess)
        {
            var result = new DropAttemptResult(true, itemId, $"Successfully dropped {itemData.itemName}", DropFailureReason.None);
            DebugLog($"Drop succeeded: {result.reason}");
            OnDropValidationSucceeded?.Invoke(itemId);
            return result;
        }
        else
        {
            // CRITICAL: Drop failed after inventory removal - attempt to restore item
            DebugLog($"CRITICAL: World drop failed for {itemData.itemName} after inventory removal - attempting restoration");

            bool restoreSuccess = AttemptItemRestore(itemData, item.GridPosition, item.currentRotation);

            if (restoreSuccess)
            {
                var result = new DropAttemptResult(false, itemId, $"Drop failed but item restored to inventory", DropFailureReason.DropFailedButRestored);
                DebugLog($"Item restoration successful: {result.reason}");
                OnDropValidationFailed?.Invoke(itemId, result.reason);
                return result;
            }
            else
            {
                var result = new DropAttemptResult(false, itemId, $"CRITICAL: Drop failed and item restoration failed - item lost!", DropFailureReason.DropFailedItemLost);
                DebugLogError($"CRITICAL FAILURE: {result.reason}");
                OnDropValidationFailed?.Invoke(itemId, result.reason);
                return result;
            }
        }
    }

    /// <summary>
    /// Validates drop feasibility with ItemDropSystem without actually dropping.
    /// </summary>
    private DropValidationResult ValidateWithDropSystem(ItemData itemData, Vector3? customPosition)
    {
        if (ItemDropSystem.Instance == null)
        {
            DebugLog("ItemDropSystem not found - skipping drop validation");
            return new DropValidationResult(true, Vector3.zero, "No drop system to validate with");
        }

        // Get drop position
        Vector3 targetPosition = customPosition ?? GetPlayerDropPosition();

        // Use ItemDropValidator to check feasibility
        var dropValidator = ItemDropSystem.Instance.GetComponent<ItemDropValidator>();
        if (dropValidator == null)
        {
            DebugLog("ItemDropValidator not found - assuming drop is valid");
            return new DropValidationResult(true, targetPosition, "No validator available");
        }

        // Validate the drop position
        return dropValidator.ValidateDropPosition(targetPosition, itemData, true);
    }

    /// <summary>
    /// Attempts to drop the item into the world using ItemDropSystem.
    /// </summary>
    private bool AttemptWorldDrop(ItemData itemData, Vector3? customPosition)
    {
        if (ItemDropSystem.Instance == null)
        {
            DebugLog("ItemDropSystem not found - cannot drop item");
            return false;
        }

        return ItemDropSystem.Instance.DropItem(itemData, customPosition);
    }

    /// <summary>
    /// Attempts to restore an item to inventory after a failed drop.
    /// Emergency fallback to prevent item loss.
    /// </summary>
    private bool AttemptItemRestore(ItemData itemData, Vector2Int originalPosition, int originalRotation)
    {
        DebugLog($"Attempting to restore {itemData.itemName} to position {originalPosition}");

        // Try to restore to original position first
        if (TryRestoreToPosition(itemData, originalPosition, originalRotation))
        {
            DebugLog("Item restored to original position");
            return true;
        }

        // If original position is now occupied, find any valid position
        DebugLog("Original position occupied - searching for alternative position");

        // Create temporary item for position finding
        string tempId = $"restore_{nextItemId}";
        var tempItem = new InventoryItemData(tempId, itemData, Vector2Int.zero);
        tempItem.SetRotation(originalRotation);

        var validPosition = inventoryGridData.FindValidPositionForItem(tempItem);
        if (validPosition.HasValue)
        {
            if (TryRestoreToPosition(itemData, validPosition.Value, originalRotation))
            {
                DebugLog($"Item restored to alternative position: {validPosition.Value}");
                return true;
            }
        }

        // Try different rotations if item is rotatable
        if (itemData.isRotatable)
        {
            DebugLog("Trying different rotations for restoration");

            for (int rotation = 0; rotation < 4; rotation++)
            {
                tempItem.SetRotation(rotation);
                validPosition = inventoryGridData.FindValidPositionForItem(tempItem);
                if (validPosition.HasValue)
                {
                    if (TryRestoreToPosition(itemData, validPosition.Value, rotation))
                    {
                        DebugLog($"Item restored with rotation {rotation} at position {validPosition.Value}");
                        return true;
                    }
                }
            }
        }

        DebugLog("All restoration attempts failed");
        return false;
    }

    /// <summary>
    /// Helper method to restore item to a specific position.
    /// ENHANCED: Now uses the new precise AddItem method.
    /// </summary>
    private bool TryRestoreToPosition(ItemData itemData, Vector2Int position, int rotation)
    {
        DebugLog($"Trying to restore {itemData.itemName} to position {position} with rotation {rotation}");

        // Use the enhanced AddItem method for precise placement
        bool result = AddItem(itemData, position, rotation);

        if (result)
        {
            DebugLog($"Successfully restored {itemData.itemName} to position {position} with rotation {rotation}");
        }

        return result;
    }

    /// <summary>
    /// Gets the ideal drop position relative to the player.
    /// </summary>
    private Vector3 GetPlayerDropPosition()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            return player.transform.position + player.transform.forward * 2f + Vector3.up * 0.5f;
        }

        DebugLog("No player found - using world origin");
        return Vector3.zero;
    }

    #endregion

    #region Configuration Methods

    /// <summary>
    /// Configure drop validation settings at runtime.
    /// </summary>
    public void SetDropValidationEnabled(bool enabled)
    {
        validateDropsBeforeRemoval = enabled;
        DebugLog($"Drop validation {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Check current drop validation settings.
    /// </summary>
    public bool IsDropValidationEnabled => validateDropsBeforeRemoval;

    #endregion

    #region Debug Methods

    [Button("Add Test Item")]
    private void AddTestItem()
    {
        if (testItemToAdd != null)
        {
            AddItem(testItemToAdd, null, null);
        }
        else if (testItems.Count > 0)
        {
            ItemData randomItem = testItems[UnityEngine.Random.Range(0, testItems.Count)];
            AddItem(randomItem, null, null);
        }
        else
        {
            Debug.LogWarning("No test items configured");
        }
    }

    [Button("Add Test Item at Specific Position")]
    private void AddTestItemAtPosition()
    {
        if (testItemToAdd != null)
        {
            Vector2Int testPosition = new Vector2Int(2, 2);
            int testRotation = 1;

            var result = TryAddItemAt(testItemToAdd, testPosition, testRotation);
            Debug.Log($"Test placement result: {result.success} - {result.message}");
        }
        else
        {
            Debug.LogWarning("No test item configured");
        }
    }

    [Button("Clear Inventory")]
    private void DebugClearInventory()
    {
        ClearInventory();
    }

    [Button("Test Drop Validation")]
    private void TestDropValidation()
    {
        if (testItems.Count > 0)
        {
            var testItem = testItems[0];
            var result = ValidateWithDropSystem(testItem, null);
            Debug.Log($"Drop validation for {testItem.itemName}: {result.isValid} - {result.reason}");
        }
        else
        {
            Debug.LogWarning("No test items available for drop validation test");
        }
    }

    [Button("Debug Inventory Stats")]
    private void DebugInventoryStats()
    {
        var stats = GetInventoryStats();
        Debug.Log($"=== PLAYER INVENTORY STATS ===");
        Debug.Log($"Grid Size: {GridWidth}x{GridHeight}");
        Debug.Log($"Total Items: {stats.itemCount}");
        Debug.Log($"Occupied Cells: {stats.occupiedCells}/{stats.totalCells}");
        Debug.Log($"Grid Utilization: {(float)stats.occupiedCells / stats.totalCells * 100:F1}%");
    }

    #endregion

    #region Save/Load Integration

    /// <summary>
    /// Called before save operations to ensure current references.
    /// </summary>
    public virtual void OnBeforeSave()
    {
        DebugLog("Preparing player inventory for save");
        // Add any pre-save logic here
    }

    /// <summary>
    /// Called after load operations.
    /// </summary>
    public virtual void OnAfterLoad()
    {
        DebugLog("Player inventory load completed");
        // Add any post-load logic here
    }

    #endregion
}

/// <summary>
/// Result structure for drop attempts with detailed feedback.
/// </summary>
[System.Serializable]
public struct DropAttemptResult
{
    public bool success;
    public string itemId;
    public string reason;
    public DropFailureReason failureReason;

    public DropAttemptResult(bool isSuccess, string id, string message, DropFailureReason failure)
    {
        success = isSuccess;
        itemId = id;
        reason = message;
        failureReason = failure;
    }
}

/// <summary>
/// Categorized failure reasons for better error handling.
/// </summary>
public enum DropFailureReason
{
    None,
    ItemNotFound,
    ItemNotDroppable,
    NoVisualPrefab,
    InvalidDropPosition,
    InventoryRemovalFailed,
    DropFailedButRestored,
    DropFailedItemLost
}