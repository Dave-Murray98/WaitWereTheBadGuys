using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Storage container grid visual that extends BaseInventoryGridVisual.
/// Handles container-specific visual behaviors and UI integration.
/// Designed to work alongside player inventory visuals in the storage container UI.
/// </summary>
public class StorageContainerGridVisual : BaseInventoryGridVisual
{
    [Header("Container Visual Settings")]
    [SerializeField] private bool autoConnectToNearestContainer = true;
    [SerializeField] private string targetContainerID = "";
    [SerializeField] private StorageContainer specificContainer;

    [Header("Container UI Styling")]
    [SerializeField] private Color containerGridLineColor = new Color(0.4f, 0.6f, 0.8f, 0.7f);
    [SerializeField] private Color containerValidPreviewColor = new Color(0f, 0.8f, 1f, 0.5f);
    [SerializeField] private Color containerInvalidPreviewColor = new Color(1f, 0.4f, 0f, 0.5f);

    // Container-specific references
    private StorageContainer containerManager;

    #region Initialization

    /// <summary>
    /// Initialize from a storage container manager.
    /// </summary>
    protected override void InitializeFromInventoryManager()
    {
        if (specificContainer != null)
        {
            ConnectToSpecificContainer(specificContainer);
        }
        else if (!string.IsNullOrEmpty(targetContainerID))
        {
            ConnectToContainerByID(targetContainerID);
        }
        else if (autoConnectToNearestContainer)
        {
            ConnectToNearestContainer();
        }
        else
        {
            DebugLogWarning("No container connection method specified - manual connection required");
        }
    }

    /// <summary>
    /// Connect to a specific storage container.
    /// </summary>
    public void ConnectToSpecificContainer(StorageContainer container)
    {
        if (container == null)
        {
            DebugLog("Cannot connect to null container");
            return;
        }

        containerManager = container;
        SetInventoryManager(container);
        ApplyContainerStyling();

        DebugLog($"Connected to specific container: {container.DisplayName}");
    }

    /// <summary>
    /// Connect to a container by ID.
    /// </summary>
    private void ConnectToContainerByID(string containerID)
    {
        var containers = FindObjectsByType<StorageContainer>(FindObjectsSortMode.None);

        foreach (var container in containers)
        {
            if (container.ContainerID == containerID)
            {
                ConnectToSpecificContainer(container);
                return;
            }
        }

        DebugLogWarning($"Container with ID '{containerID}' not found");
    }

    /// <summary>
    /// Connect to the nearest storage container.
    /// </summary>
    private void ConnectToNearestContainer()
    {
        var containers = FindObjectsByType<StorageContainer>(FindObjectsSortMode.None);

        if (containers.Length == 0)
        {
            DebugLogWarning("No storage containers found in scene");
            return;
        }

        StorageContainer nearestContainer = containers[0];
        float nearestDistance = Vector3.Distance(transform.position, nearestContainer.transform.position);

        for (int i = 1; i < containers.Length; i++)
        {
            float distance = Vector3.Distance(transform.position, containers[i].transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestContainer = containers[i];
            }
        }

        ConnectToSpecificContainer(nearestContainer);
    }

    /// <summary>
    /// Apply container-specific visual styling.
    /// </summary>
    private void ApplyContainerStyling()
    {
        // Override base colors with container-specific colors
        gridLineColor = containerGridLineColor;
        validPreviewColor = containerValidPreviewColor;
        invalidPreviewColor = containerInvalidPreviewColor;

        // Refresh grid lines with new colors
        if (currentGridData != null)
        {
            CreateGridLines();
        }
    }

    #endregion

    #region Container-Specific Overrides

    /// <summary>
    /// Container items should have simplified drag handlers (no clothing equipping, etc.).
    /// </summary>
    protected override bool ShouldAddDragHandler(InventoryItemData item)
    {
        // Container items are draggable but with simplified behavior
        return true;
    }

    /// <summary>
    /// Create container-specific item visual GameObjects.
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
            // Create container-specific item visual
            itemObj = new GameObject($"ContainerItem_{item.ID}");
            itemObj.transform.SetParent(transform, false);
            itemObj.AddComponent<RectTransform>();
            itemObj.AddComponent<InventoryItemVisualRenderer>();
            itemObj.AddComponent<StorageContainerItemDragHandler>(); // Use simplified drag handler
            // Note: No hotkey indicator for container items
        }

        return itemObj;
    }

    /// <summary>
    /// Initialize visual components with container-specific settings.
    /// </summary>
    protected override void InitializeItemVisualComponents(GameObject itemObj, InventoryItemData item)
    {
        // Initialize visual renderer
        var renderer = itemObj.GetComponent<InventoryItemVisualRenderer>();
        if (renderer != null)
        {
            renderer.Initialize(item, this);
        }

        // Initialize container-specific drag handler
        var dragHandler = itemObj.GetComponent<StorageContainerItemDragHandler>();
        if (dragHandler != null)
        {
            InitializeContainerDragHandler(dragHandler, item);
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
    /// Initialize container-specific drag handler.
    /// </summary>
    private void InitializeContainerDragHandler(StorageContainerItemDragHandler dragHandler, InventoryItemData item)
    {
        dragHandler.Initialize(item, this);
        dragHandler.SetContainerManager(containerManager);

        // Register with stats display
        ItemStatsDisplay.AutoRegisterNewDragHandler(dragHandler);
    }

    #endregion

    #region Container Events

    /// <summary>
    /// Subscribe to container-specific events.
    /// </summary>
    protected override void SubscribeToDataEvents()
    {
        base.SubscribeToDataEvents();

        if (containerManager != null)
        {
            containerManager.OnContainerOpened += OnContainerOpened;
            containerManager.OnContainerClosed += OnContainerClosed;
            DebugLog("Subscribed to container-specific events");
        }
    }

    /// <summary>
    /// Unsubscribe from container-specific events.
    /// </summary>
    protected override void UnsubscribeFromDataEvents()
    {
        base.UnsubscribeFromDataEvents();

        if (containerManager != null)
        {
            containerManager.OnContainerOpened -= OnContainerOpened;
            containerManager.OnContainerClosed -= OnContainerClosed;
            DebugLog("Unsubscribed from container-specific events");
        }
    }

    /// <summary>
    /// Handle container opened event.
    /// </summary>
    private void OnContainerOpened(StorageContainer container)
    {
        DebugLog($"Container opened: {container.DisplayName}");
        // Add visual feedback for container opening if needed
    }

    /// <summary>
    /// Handle container closed event.
    /// </summary>
    private void OnContainerClosed(StorageContainer container)
    {
        DebugLog($"Container closed: {container.DisplayName}");
        // Add visual feedback for container closing if needed
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Get the connected container manager.
    /// </summary>
    public StorageContainer GetContainerManager()
    {
        return containerManager;
    }

    /// <summary>
    /// Check if this visual is connected to a container.
    /// </summary>
    public bool IsConnectedToContainer()
    {
        return containerManager != null;
    }

    /// <summary>
    /// Manually set the target container ID.
    /// </summary>
    public void SetTargetContainerID(string containerID)
    {
        targetContainerID = containerID;
        ConnectToContainerByID(containerID);
    }

    /// <summary>
    /// Force reconnection to container.
    /// </summary>
    [Button("Reconnect to Container")]
    public void ReconnectToContainer()
    {
        InitializeFromInventoryManager();
        Debug.Log("Manually reconnected to container");
    }

    #endregion

    #region Transfer Operations

    /// <summary>
    /// Transfer an item from this container to the player inventory.
    /// IMPROVED: Added better debugging and validation.
    /// </summary>
    public bool TransferItemToPlayer(string itemId)
    {
        DebugLog($"TransferItemToPlayer called for item: {itemId}");

        if (containerManager == null || PlayerInventoryManager.Instance == null)
        {
            DebugLog("Cannot transfer item - missing manager references");
            return false;
        }

        var item = containerManager.InventoryGridData.GetItem(itemId);
        if (item == null)
        {
            DebugLogWarning($"Item {itemId} not found in container");

            // Additional debugging - list all items in container
            var allItems = containerManager.InventoryGridData.GetAllItems();
            DebugLog($"Container currently has {allItems.Count} items:");
            foreach (var containerItem in allItems)
            {
                DebugLog($"  - {containerItem.ID}: {containerItem.ItemData?.itemName}");
            }

            return false;
        }

        DebugLog($"Found item {item.ItemData?.itemName} in container");

        // Check if player has space
        if (!PlayerInventoryManager.Instance.HasSpaceForItem(item.ItemData))
        {
            DebugLog("Player inventory has no space for item");
            return false;
        }

        // Remove from container
        if (containerManager.RemoveItem(itemId))
        {
            DebugLog($"Successfully removed {itemId} from container");

            // Add to player inventory
            if (PlayerInventoryManager.Instance.AddItem(item.ItemData))
            {
                DebugLog($"Transferred {item.ItemData?.itemName} from container to player inventory");
                return true;
            }
            else
            {
                // Failed to add to player - restore to container
                DebugLogWarning("Failed to add item to player inventory - restoring to container");
                containerManager.AddItem(item.ItemData, item.GridPosition);
                return false;
            }
        }

        DebugLogWarning("Failed to remove item from container");
        return false;
    }

    /// <summary>
    /// Transfer an item from the player inventory to this container.
    /// </summary>
    public bool TransferItemFromPlayer(string playerItemId)
    {
        if (containerManager == null || PlayerInventoryManager.Instance == null)
        {
            DebugLog("Cannot transfer item - missing manager references");
            return false;
        }

        var item = PlayerInventoryManager.Instance.InventoryGridData.GetItem(playerItemId);
        if (item == null)
        {
            DebugLogWarning($"Item {playerItemId} not found in player inventory");
            return false;
        }

        // Check if container has space
        if (!containerManager.HasSpaceForItem(item.ItemData))
        {
            DebugLog("Container has no space for item");
            return false;
        }

        // Remove from player inventory
        if (PlayerInventoryManager.Instance.RemoveItem(playerItemId))
        {
            // Add to container
            if (containerManager.AddItem(item.ItemData))
            {
                DebugLog($"Transferred {item.ItemData?.itemName} from player inventory to container");
                return true;
            }
            else
            {
                // Failed to add to container - restore to player
                PlayerInventoryManager.Instance.AddItem(item.ItemData, item.GridPosition);
                DebugLog("Failed to add item to container - restored to player inventory");
                return false;
            }
        }

        DebugLog("Failed to remove item from player inventory");
        return false;
    }

    #endregion

}