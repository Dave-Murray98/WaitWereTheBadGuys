using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: Player inventory grid visual that extends BaseInventoryGridVisual.
/// Now provides a clean, reusable foundation while maintaining all existing functionality.
/// Handles player-specific visual behaviors and UI integration.
/// </summary>
public class PlayerInventoryGridVisual : BaseInventoryGridVisual
{
    [Header("Player Inventory Specific")]
    [SerializeField] private bool autoConnectToPlayerInventory = true;

    // Flag indicating if this inventory grid was opened via an external inventory (like a storage container)
    // and whether the drop down menu actions should adjust accordingly (ie show transfer to storage container option)
    public bool isOpenedViaExternalInventory = false;

    #region Initialization

    /// <summary>
    /// Initialize from the player inventory manager.
    /// </summary>
    protected override void InitializeFromInventoryManager()
    {
        if (autoConnectToPlayerInventory)
        {
            ConnectToPlayerInventory();
        }
        else
        {
            DebugLogWarning("Auto-connect to player inventory disabled - manual connection required");
        }
    }

    /// <summary>
    /// Connect to the player inventory manager specifically.
    /// </summary>
    private void ConnectToPlayerInventory()
    {
        // Look for the InventoryManager (player inventory) specifically, not the base class
        PlayerInventoryManager playerInventory = PlayerInventoryManager.Instance;
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventoryManager>();
        }

        if (playerInventory != null)
        {
            SetInventoryManager(playerInventory); // This calls the base class method with the derived class
            DebugLog("Connected to player inventory manager");
        }
        else
        {
            DebugLog("Player inventory manager not found! Make sure InventoryManager exists in the scene.");
        }
    }

    #endregion

    #region Player-Specific Overrides

    /// <summary>
    /// Player inventory items should always have drag handlers.
    /// </summary>
    protected override bool ShouldAddDragHandler(InventoryItemData item)
    {
        // Player inventory items are always draggable
        return true;
    }

    /// <summary>
    /// Initialize drag handler with player-specific behavior.
    /// </summary>
    protected override void InitializeDragHandler(BaseInventoryDragHandler dragHandler, InventoryItemData item)
    {
        // Use the specialized player inventory drag handler if this is one
        if (dragHandler is PlayerInventoryItemDragHandler playerDragHandler)
        {
            DebugLog("Initializing player-specific drag handler");
            playerDragHandler.Initialize(item, this);

            //if opened via external inventory, set the flag on the drag handler so it's drop down menu shows transfer option
            playerDragHandler.canTransfer = isOpenedViaExternalInventory;

            // Register with stats display
            ItemStatsDisplay.AutoRegisterNewDragHandler(playerDragHandler);
        }
        else
        {
            // Fallback to base implementation
            base.InitializeDragHandler(dragHandler, item);
        }
    }

    /// <summary>
    /// Create player-specific item visual GameObjects.
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
            // Create default player inventory item visual
            itemObj = new GameObject($"PlayerItem_{item.ID}");
            itemObj.transform.SetParent(transform, false);
            itemObj.AddComponent<RectTransform>();
            itemObj.AddComponent<InventoryItemVisualRenderer>();
            itemObj.AddComponent<PlayerInventoryItemDragHandler>(); // Player items use the enhanced drag handler
            itemObj.AddComponent<InventoryHotkeyIndicator>(); // Player items show hotkey indicators
        }

        return itemObj;
    }

    /// <summary>
    /// Initialize visual components with player-specific settings.
    /// </summary>
    protected override void InitializeItemVisualComponents(GameObject itemObj, InventoryItemData item)
    {
        // Initialize visual renderer
        var renderer = itemObj.GetComponent<InventoryItemVisualRenderer>();
        if (renderer != null)
        {
            renderer.Initialize(item, this);
        }

        // Initialize drag handler with player-specific behavior
        var dragHandler = itemObj.GetComponent<PlayerInventoryItemDragHandler>();
        if (dragHandler != null)
        {
            InitializeDragHandler(dragHandler, item);
        }

        // Initialize hotkey indicator (player inventory specific)
        var hotkeyIndicator = itemObj.GetComponent<InventoryHotkeyIndicator>();
        if (hotkeyIndicator != null)
        {
            hotkeyIndicator.Initialize(item);
        }
        else if (enableDebugLogs)
        {
            DebugLogWarning($"Missing InventoryHotkeyIndicator component on {itemObj.name}");
        }
    }

    #endregion

    #region Player Inventory Events

    /// <summary>
    /// Subscribe to additional player-specific events.
    /// </summary>
    protected override void SubscribeToDataEvents()
    {
        base.SubscribeToDataEvents();

        // Subscribe to player-specific events if the inventory manager is the player's
        if (inventoryManager is PlayerInventoryManager playerInventory)
        {
            playerInventory.OnDropValidationFailed += OnPlayerDropValidationFailed;
            playerInventory.OnDropValidationSucceeded += OnPlayerDropValidationSucceeded;
            DebugLog("Subscribed to player-specific inventory events");
        }
    }

    /// <summary>
    /// Unsubscribe from player-specific events.
    /// </summary>
    protected override void UnsubscribeFromDataEvents()
    {
        base.UnsubscribeFromDataEvents();

        // Unsubscribe from player-specific events
        if (inventoryManager is PlayerInventoryManager playerInventory)
        {
            playerInventory.OnDropValidationFailed -= OnPlayerDropValidationFailed;
            playerInventory.OnDropValidationSucceeded -= OnPlayerDropValidationSucceeded;
            DebugLog("Unsubscribed from player-specific inventory events");
        }
    }

    /// <summary>
    /// Handle player drop validation failure.
    /// </summary>
    private void OnPlayerDropValidationFailed(string itemId, string reason)
    {
        DebugLog($"Player drop validation failed for {itemId}: {reason}");
        // Add visual feedback for failed drops if needed
    }

    /// <summary>
    /// Handle player drop validation success.
    /// </summary>
    private void OnPlayerDropValidationSucceeded(string itemId)
    {
        DebugLog($"Player drop validation succeeded for {itemId}");
        // Add visual feedback for successful drops if needed
    }

    #endregion

    #region Public Interface (Backward Compatibility)

    /// <summary>
    /// Get the player inventory manager (for backward compatibility).
    /// </summary>
    public PlayerInventoryManager GetPlayerInventoryManager()
    {
        return inventoryManager as PlayerInventoryManager;
    }

    /// <summary>
    /// Force reconnection to player inventory.
    /// </summary>
    [Button("Reconnect to Player Inventory")]
    public void ReconnectToPlayerInventory()
    {
        ConnectToPlayerInventory();
        Debug.Log("Manually reconnected to player inventory");
    }

    #endregion

    #region Debug Methods

    [Button("Debug Player Inventory State")]
    private void DebugPlayerInventoryState()
    {
        base.DebugVisualState();

        var playerInventory = GetPlayerInventoryManager();
        if (playerInventory != null)
        {
            Debug.Log($"=== PLAYER INVENTORY SPECIFIC ===");
            Debug.Log($"Drop Validation Enabled: {playerInventory.IsDropValidationEnabled}");
            Debug.Log($"Singleton Instance: {PlayerInventoryManager.Instance != null}");
        }
        else
        {
            Debug.LogWarning("No player inventory manager connected!");
        }
    }

    [Button("Test Player Inventory Operations")]
    private void TestPlayerInventoryOperations()
    {
        var playerInventory = GetPlayerInventoryManager();
        if (playerInventory == null)
        {
            Debug.LogError("No player inventory manager available for testing");
            return;
        }

        Debug.Log("=== TESTING PLAYER INVENTORY OPERATIONS ===");

        // Test inventory stats
        var stats = playerInventory.GetInventoryStats();
        Debug.Log($"Current Stats: {stats.itemCount} items, {stats.occupiedCells}/{stats.totalCells} cells used");

        // Test space checking
        var testField = playerInventory.GetType().GetField("testItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (testField?.GetValue(playerInventory) is System.Collections.Generic.List<ItemData> testItems && testItems.Count > 0)
        {
            var testItem = testItems[0];
            bool hasSpace = playerInventory.HasSpaceForItem(testItem);
            Debug.Log($"Has space for {testItem.itemName}: {hasSpace}");
        }

        Debug.Log("=== END TESTING ===");
    }

    #endregion
}