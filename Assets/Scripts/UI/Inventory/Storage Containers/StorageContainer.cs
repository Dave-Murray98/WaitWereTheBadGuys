using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// ENHANCED: Storage container inventory manager that extends BaseInventoryManager.
/// Now supports precise item placement with position and rotation control,
/// enabling proper transfer operations with drag-and-drop preview functionality.
/// </summary>
public class StorageContainer : BaseInventoryManager
{
    [Header("Container Settings")]
    private string containerID = "";
    [SerializeField] private string containerDisplayName = "Storage Container";

    [Header("Container Restrictions")]
    [SerializeField] private bool allowAllItemTypes = true;
    [SerializeField] private List<ItemType> allowedItemTypes = new List<ItemType>();
    [SerializeField] private List<ItemType> restrictedItemTypes = new List<ItemType>();

    [Header("Test Data")]
    [SerializeField] private List<ItemData> testContainerItems = new List<ItemData>();
    [SerializeField] private bool populateWithTestItems = false;

    // Properties
    public string ContainerID => containerID;
    public string DisplayName => containerDisplayName;

    // Events specific to storage containers
    public System.Action<StorageContainer> OnContainerOpened;
    public System.Action<StorageContainer> OnContainerClosed;

    #region Initialization

    protected override void Awake()
    {
        // Initialize base inventory
        base.Awake();

        DebugLog($"Storage container initialized: {containerDisplayName} (ID: {containerID})");
    }

    public virtual void SetContainerID(string newID)
    {
        containerID = newID;
    }


    private void Start()
    {
        if (populateWithTestItems)
        {
            PopulateWithTestItems();
        }
    }

    #endregion

    #region Container-Specific Overrides

    /// <summary>
    /// Generate container-specific item IDs.
    /// </summary>
    protected override string GenerateItemId()
    {
        return $"container_{containerID}_item_{nextItemId}";
    }

    /// <summary>
    /// Container-specific item addition validation.
    /// </summary>
    protected override bool CanAddItem(ItemData itemData)
    {
        if (!base.CanAddItem(itemData))
            return false;

        // Check if item type is allowed in this container
        if (!IsItemTypeAllowed(itemData.itemType))
        {
            DebugLog($"Item type {itemData.itemType} not allowed in container {containerDisplayName}");
            return false;
        }

        // Add other container-specific validation here
        // For example: check container capacity, item size restrictions, etc.

        return true;
    }

    /// <summary>
    /// Container-specific item removal validation.
    /// </summary>
    protected override bool CanRemoveItem(InventoryItemData item)
    {
        if (!base.CanRemoveItem(item))
            return false;

        // Add container-specific removal validation here
        // For example: check if container is locked, item is locked, etc.

        return true;
    }

    /// <summary>
    /// Container-specific notifications when items are added.
    /// </summary>
    protected override void OnItemAddedInternal(InventoryItemData item)
    {
        base.OnItemAddedInternal(item);

        DebugLog($"Container {containerDisplayName}: Added {item.ItemData?.itemName} at {item.GridPosition} with rotation {item.currentRotation}");

        // Add container-specific logic here
        // For example: update container weight, trigger container sounds, etc.
    }

    /// <summary>
    /// Container-specific notifications when items are removed.
    /// </summary>
    protected override void OnItemRemovedInternal(InventoryItemData item)
    {
        base.OnItemRemovedInternal(item);

        DebugLog($"Container {containerDisplayName}: Removed {item.ItemData?.itemName}");

        // Add container-specific logic here
        // For example: update container weight, trigger container sounds, etc.
    }

    #endregion

    #region Enhanced Container Methods

    /// <summary>
    /// ENHANCED: Add item with container-specific debugging.
    /// </summary>
    public override bool AddItem(ItemData itemData, Vector2Int? position = null, int? rotation = null)
    {
        string positionStr = position?.ToString() ?? "auto";
        string rotationStr = rotation?.ToString() ?? "auto";
        DebugLog($"Container AddItem called: {itemData?.itemName}, position={positionStr}, rotation={rotationStr}");

        bool result = base.AddItem(itemData, position, rotation);

        if (result)
        {
            DebugLog($"Successfully added {itemData?.itemName} to container {containerDisplayName}");
        }
        else
        {
            DebugLog($"Failed to add {itemData?.itemName} to container {containerDisplayName}");
        }

        return result;
    }

    /// <summary>
    /// ENHANCED: Add existing item with container-specific debugging.
    /// </summary>
    public override bool AddItem(InventoryItemData existingItem, Vector2Int? position = null, int? rotation = null)
    {
        string positionStr = position?.ToString() ?? "auto";
        string rotationStr = rotation?.ToString() ?? "auto";
        DebugLog($"Container AddItem (existing) called: {existingItem?.ItemData?.itemName}, position={positionStr}, rotation={rotationStr}");

        bool result = base.AddItem(existingItem, position, rotation);

        if (result)
        {
            DebugLog($"Successfully added existing item {existingItem?.ItemData?.itemName} to container {containerDisplayName}");
        }
        else
        {
            DebugLog($"Failed to add existing item {existingItem?.ItemData?.itemName} to container {containerDisplayName}");
        }

        return result;
    }

    /// <summary>
    /// ENHANCED: Try to add item at specific position with container-specific feedback.
    /// </summary>
    public override ItemPlacementResult TryAddItemAt(ItemData itemData, Vector2Int position, int rotation = 0)
    {
        DebugLog($"Container TryAddItemAt called: {itemData?.itemName} at {position} with rotation {rotation}");

        var result = base.TryAddItemAt(itemData, position, rotation);

        DebugLog($"Container TryAddItemAt result: {result.success} - {result.message}");

        return result;
    }

    #endregion

    #region Item Type Validation

    /// <summary>
    /// Check if an item type is allowed in this container.
    /// </summary>
    private bool IsItemTypeAllowed(ItemType itemType)
    {
        // If all item types are allowed, check restricted list
        if (allowAllItemTypes)
        {
            return !restrictedItemTypes.Contains(itemType);
        }

        // If specific types are allowed, check allowed list
        return allowedItemTypes.Contains(itemType);
    }

    /// <summary>
    /// Add an allowed item type to the container.
    /// </summary>
    public void AddAllowedItemType(ItemType itemType)
    {
        if (!allowedItemTypes.Contains(itemType))
        {
            allowedItemTypes.Add(itemType);
            DebugLog($"Added allowed item type: {itemType}");
        }
    }

    /// <summary>
    /// Remove an allowed item type from the container.
    /// </summary>
    public void RemoveAllowedItemType(ItemType itemType)
    {
        if (allowedItemTypes.Remove(itemType))
        {
            DebugLog($"Removed allowed item type: {itemType}");
        }
    }

    /// <summary>
    /// Add a restricted item type to the container.
    /// </summary>
    public void AddRestrictedItemType(ItemType itemType)
    {
        if (!restrictedItemTypes.Contains(itemType))
        {
            restrictedItemTypes.Add(itemType);
            DebugLog($"Added restricted item type: {itemType}");
        }
    }

    /// <summary>
    /// Remove a restricted item type from the container.
    /// </summary>
    public void RemoveRestrictedItemType(ItemType itemType)
    {
        if (restrictedItemTypes.Remove(itemType))
        {
            DebugLog($"Removed restricted item type: {itemType}");
        }
    }

    #endregion

    #region Container Access

    /// <summary>
    /// Open the container for interaction.
    /// </summary>
    public virtual void OpenContainer()
    {
        DebugLog($"Opening container: {containerDisplayName}");

        // Add container opening logic here
        // For example: play opening sound, check access permissions, etc.

        OnContainerOpened?.Invoke(this);
    }

    /// <summary>
    /// Close the container.
    /// </summary>
    public virtual void CloseContainer()
    {
        DebugLog($"Closing container: {containerDisplayName}");

        // Add container closing logic here
        // For example: play closing sound, save container state, etc.

        OnContainerClosed?.Invoke(this);
    }

    /// <summary>
    /// Check if the player can access this container.
    /// Override for custom access logic.
    /// </summary>
    public virtual bool CanPlayerAccess()
    {
        // Default: all containers are accessible
        // Override this method to add custom access logic:
        // - Distance checks
        // - Key requirements
        // - Permission levels
        // - Lock status

        return true;
    }

    #endregion

    #region Test and Debug

    /// <summary>
    /// Populate container with test items for development.
    /// </summary>
    private void PopulateWithTestItems()
    {
        if (testContainerItems.Count == 0)
        {
            DebugLog("No test items configured for container");
            return;
        }

        DebugLog($"Populating container {containerDisplayName} with {testContainerItems.Count} test items");

        foreach (var testItem in testContainerItems)
        {
            if (testItem != null)
            {
                bool added = AddItem(testItem);
                if (!added)
                {
                    DebugLogWarning($"Failed to add test item: {testItem.itemName}");
                }
            }
        }
    }

    [Button("Add Random Test Item")]
    private void AddRandomTestItem()
    {
        if (testContainerItems.Count == 0)
        {
            DebugLogWarning("No test items configured");
            return;
        }

        var randomItem = testContainerItems[Random.Range(0, testContainerItems.Count)];
        bool success = AddItem(randomItem);

        if (success)
        {
            Debug.Log($"Added test item: {randomItem.itemName}");
        }
        else
        {
            Debug.LogWarning($"Failed to add test item: {randomItem.itemName}");
        }
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Set the container display name.
    /// </summary>
    public void SetDisplayName(string newName)
    {
        containerDisplayName = newName;
        DebugLog($"Container display name changed to: {newName}");
    }

    /// <summary>
    /// Set whether all item types are allowed.
    /// </summary>
    public void SetAllowAllItemTypes(bool allowAll)
    {
        allowAllItemTypes = allowAll;
        DebugLog($"Allow all item types: {allowAll}");
    }

    /// <summary>
    /// Get container configuration for UI display.
    /// </summary>
    public ContainerConfig GetContainerConfig()
    {
        return new ContainerConfig
        {
            containerID = this.containerID,
            displayName = this.containerDisplayName,
            gridWidth = this.gridWidth,
            gridHeight = this.gridHeight,
            allowAllItemTypes = this.allowAllItemTypes,
            allowedItemTypes = new List<ItemType>(this.allowedItemTypes),
            restrictedItemTypes = new List<ItemType>(this.restrictedItemTypes)
        };
    }

    #endregion

    #region Static Utilities

    /// <summary>
    /// Create a storage container with specific configuration.
    /// </summary>
    public static StorageContainer CreateContainer(string containerID, string displayName, int width, int height)
    {
        GameObject containerObj = new GameObject($"StorageContainer_{containerID}");
        var container = containerObj.AddComponent<StorageContainer>();

        container.containerID = containerID;
        container.containerDisplayName = displayName;
        container.gridWidth = width;
        container.gridHeight = height;

        return container;
    }

    #endregion
}

/// <summary>
/// Configuration data for storage containers.
/// </summary>
[System.Serializable]
public class ContainerConfig
{
    public string containerID;
    public string displayName;
    public int gridWidth;
    public int gridHeight;
    public bool allowAllItemTypes;
    public List<ItemType> allowedItemTypes;
    public List<ItemType> restrictedItemTypes;
}