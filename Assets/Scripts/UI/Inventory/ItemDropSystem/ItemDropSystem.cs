using UnityEngine;
using System.Collections.Generic;
using NWH.DWP2.WaterObjects;
using NGS.AdvancedCullingSystem.Dynamic;

/// <summary>
/// UPDATED: ItemDropSystem with enhanced inventory integration.
/// Now works cooperatively with InventoryManager's validation system
/// instead of directly removing items from inventory. Provides validation
/// methods for pre-drop checks and maintains robust item spawning.
/// </summary>
public class ItemDropSystem : MonoBehaviour
{
    public static ItemDropSystem Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private ItemDropSettings dropSettings;
    [SerializeField] private GameObject pickupInteractionPrefab;

    [Header("Scene Organization")]
    [SerializeField] private Transform droppedItemsContainer;
    [SerializeField] private string droppedItemsContainerName = "Items";
    [SerializeField] private bool autoFindContainer = true;

    [Header("Components")]
    [SerializeField] private ItemDropValidator dropValidator;

    [Header("Object Pooling")]
    [SerializeField] private bool useObjectPooling = true;
    [SerializeField] private int poolSize = 20;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Object pool for pickup interaction components
    private Queue<GameObject> pickupPool = new Queue<GameObject>();
    private List<GameObject> activeItems = new List<GameObject>();

    public GameObject GetPickupPrefab() => pickupInteractionPrefab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeSystem();
    }

    private void Start()
    {
        ValidateSetup();
        SetupDroppedItemsContainer();

        if (useObjectPooling)
        {
            InitializeObjectPool();
        }
    }

    /// <summary>
    /// Initializes the drop system components and settings
    /// </summary>
    private void InitializeSystem()
    {
        LoadDropSettings();
        SetupDropValidator();
    }

    /// <summary>
    /// Loads drop settings from Resources if not assigned
    /// </summary>
    private void LoadDropSettings()
    {
        if (dropSettings == null)
        {
            dropSettings = Resources.Load<ItemDropSettings>("Settings/ItemDropSettings");
            if (dropSettings == null)
            {
                DebugLog("No ItemDropSettings found in Resources/Settings/. Creating default settings.");
                CreateDefaultDropSettings();
            }
        }
    }

    /// <summary>
    /// Sets up the drop validator component
    /// </summary>
    private void SetupDropValidator()
    {
        if (dropValidator == null)
        {
            dropValidator = GetComponent<ItemDropValidator>();
            if (dropValidator == null)
            {
                dropValidator = gameObject.AddComponent<ItemDropValidator>();
            }
        }

        if (dropValidator != null)
        {
            dropValidator.DropSettings = dropSettings;
        }
    }

    /// <summary>
    /// Sets up or finds the dropped items container GameObject
    /// </summary>
    private void SetupDroppedItemsContainer()
    {
        if (droppedItemsContainer == null && autoFindContainer)
        {
            // Try to find existing container by name
            GameObject containerGO = GameObject.Find(droppedItemsContainerName);
            if (containerGO != null)
            {
                droppedItemsContainer = containerGO.transform;
                DebugLog($"Found existing dropped items container: {droppedItemsContainerName}");
            }
            else
            {
                // Create new container
                containerGO = new GameObject(droppedItemsContainerName);
                droppedItemsContainer = containerGO.transform;
                DebugLog($"Created new dropped items container: {droppedItemsContainerName}");
            }
        }
        else if (droppedItemsContainer != null)
        {
            DebugLog($"Using assigned dropped items container: {droppedItemsContainer.name}");
        }
        else
        {
            DebugLog("Warning: No dropped items container configured - items will spawn at world root");
        }
    }

    /// <summary>
    /// Validates the system setup and creates missing components
    /// </summary>
    private void ValidateSetup()
    {
        if (pickupInteractionPrefab == null)
        {
            if (dropSettings != null && dropSettings.pickupInteractionPrefab != null)
            {
                pickupInteractionPrefab = dropSettings.pickupInteractionPrefab;
            }
            else
            {
                CreateDefaultPickupInteractionPrefab();
            }
        }

        ValidatePickupInteractionPrefab();
    }

    /// <summary>
    /// Validates that the pickup interaction prefab has the correct structure
    /// </summary>
    private void ValidatePickupInteractionPrefab()
    {
        if (pickupInteractionPrefab == null)
            return;

        // Check for required components
        bool hasCollider = pickupInteractionPrefab.GetComponent<Collider>() != null;
        bool hasPickupScript = pickupInteractionPrefab.GetComponent<ItemPickupInteractable>() != null;

        if (!hasCollider)
        {
            DebugLog("Warning: Pickup interaction prefab missing Collider component");
        }

        if (!hasPickupScript)
        {
            DebugLog("Warning: Pickup interaction prefab missing ItemPickupInteractable component");
        }
    }

    /// <summary>
    /// Creates default drop settings if none exist
    /// </summary>
    private void CreateDefaultDropSettings()
    {
        dropSettings = ScriptableObject.CreateInstance<ItemDropSettings>();
        DebugLog("Created default ItemDropSettings");
    }

    /// <summary>
    /// Creates a default pickup interaction prefab if none exists
    /// </summary>
    private void CreateDefaultPickupInteractionPrefab()
    {
        DebugLog("No pickup interaction prefab assigned - creating basic one");

        GameObject prefab = new GameObject("PickupInteraction");

        // Add interaction components
        var collider = prefab.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 1f;

        var pickup = prefab.AddComponent<ItemPickupInteractable>();

        pickupInteractionPrefab = prefab;
        DebugLog("Created default pickup interaction prefab");
    }

    /// <summary>
    /// Initializes the object pool for pickup interaction components
    /// </summary>
    private void InitializeObjectPool()
    {
        if (pickupInteractionPrefab == null)
            return;

        pickupPool.Clear();

        for (int i = 0; i < poolSize; i++)
        {
            GameObject pooledPickup = Instantiate(pickupInteractionPrefab);

            // Parent pooled items to the dropped items container for organization
            if (droppedItemsContainer != null)
            {
                pooledPickup.transform.SetParent(droppedItemsContainer);
            }

            pooledPickup.SetActive(false);
            pickupPool.Enqueue(pooledPickup);
        }

        DebugLog($"Initialized pickup interaction pool with {poolSize} items under container: {(droppedItemsContainer != null ? droppedItemsContainer.name : "world root")}");
    }

    #region UPDATED: Enhanced Public API with Validation

    /// <summary>
    /// UPDATED: Drop item from inventory with enhanced error handling.
    /// Now uses InventoryManager's validation system instead of directly removing items.
    /// This prevents item loss when drops fail.
    /// </summary>
    public static bool DropItemFromInventory(string itemId, Vector3? customDropPosition = null)
    {
        if (Instance == null)
        {
            Debug.LogError("ItemDropSystem instance not found");
            return false;
        }

        // Get the inventory manager
        var inventory = PlayerInventoryManager.Instance;
        if (inventory == null)
        {
            Debug.LogError("InventoryManager not found");
            return false;
        }

        Instance.DebugLog($"=== DropItemFromInventory called for: {itemId} ===");

        // Use InventoryManager's enhanced drop method instead of direct removal
        var dropResult = inventory.TryDropItem(itemId, customDropPosition);

        if (dropResult.success)
        {
            Instance.DebugLog($"Successfully dropped item {itemId}: {dropResult.reason}");
            return true;
        }
        else
        {
            // Log failure reason for debugging
            Instance.DebugLog($"Failed to drop item {itemId}: {dropResult.reason} (FailureType: {dropResult.failureReason})");

            // Show user-friendly message based on failure type
            Instance.ShowDropFailureMessage(dropResult);

            return false;
        }
    }

    /// <summary>
    /// UPDATED: Validates if an item can be dropped without actually dropping it.
    /// Used by UI systems to enable/disable drop buttons.
    /// </summary>
    public static bool CanDropItem(string itemId, Vector3? customDropPosition = null)
    {
        if (Instance == null) return false;

        var inventory = PlayerInventoryManager.Instance;
        if (inventory == null) return false;

        var item = inventory.InventoryGridData.GetItem(itemId);
        if (item?.ItemData == null) return false;

        ItemData itemData = item.ItemData;

        // Check basic droppability
        if (!itemData.CanDrop || !itemData.HasVisualPrefab)
            return false;

        // Check with drop validator if enabled
        if (inventory.IsDropValidationEnabled && Instance.dropValidator != null)
        {
            Vector3 targetPosition = customDropPosition ?? Instance.GetPlayerDropPosition();
            var validationResult = Instance.dropValidator.ValidateDropPosition(targetPosition, itemData, true);
            return validationResult.isValid;
        }

        return true;
    }

    /// <summary>
    /// UPDATED: Gets detailed information about why an item cannot be dropped.
    /// Useful for providing specific feedback to players.
    /// </summary>
    public static string GetDropFailureReason(string itemId, Vector3? customDropPosition = null)
    {
        if (Instance == null) return "Drop system not available";

        var inventory = PlayerInventoryManager.Instance;
        if (inventory == null) return "Inventory system not available";

        var item = inventory.InventoryGridData.GetItem(itemId);
        if (item?.ItemData == null) return "Item not found in inventory";

        ItemData itemData = item.ItemData;

        // Check basic restrictions
        if (!itemData.CanDrop)
            return $"{itemData.itemName} cannot be dropped (Key Item)";

        if (!itemData.HasVisualPrefab)
            return $"{itemData.itemName} has no visual representation for dropping";

        // Check drop position if validation is enabled
        if (inventory.IsDropValidationEnabled && Instance.dropValidator != null)
        {
            Vector3 targetPosition = customDropPosition ?? Instance.GetPlayerDropPosition();
            var validationResult = Instance.dropValidator.ValidateDropPosition(targetPosition, itemData, true);
            if (!validationResult.isValid)
                return $"Cannot drop here: {validationResult.reason}";
        }

        return "Item can be dropped";
    }

    #endregion

    #region Core Drop Implementation

    /// <summary>
    /// Core drop implementation - creates and configures dropped item in world.
    /// Called internally by InventoryManager after validation and removal.
    /// </summary>
    public bool DropItem(ItemData itemData, Vector3? dropPosition = null)
    {
        if (itemData == null)
        {
            DebugLog("Cannot drop null ItemData");
            return false;
        }

        if (!itemData.CanDrop)
        {
            DebugLog($"Item {itemData.itemName} cannot be dropped");
            return false;
        }

        if (!itemData.HasVisualPrefab)
        {
            Debug.LogError($"Item {itemData.itemName} has no visual prefab assigned! Cannot drop.");
            return false;
        }

        // Get drop position
        Vector3 targetPosition = dropPosition ?? GetPlayerDropPosition();

        // Validate drop position
        var validationResult = dropValidator.ValidateDropPosition(targetPosition, itemData, true);
        if (!validationResult.isValid)
        {
            DebugLog($"Cannot drop {itemData.itemName}: {validationResult.reason}");
            return false;
        }

        // Create the dropped item
        GameObject droppedItem = CreateDroppedItemObject(itemData, validationResult.position);
        if (droppedItem == null)
        {
            DebugLog($"Failed to create dropped item object for {itemData.itemName}");
            return false;
        }

        // Register with scene state manager
        string droppedId = RegisterDroppedItemWithStateManager(itemData, validationResult.position);
        if (string.IsNullOrEmpty(droppedId))
        {
            DebugLog($"Failed to register {itemData.itemName} with state manager");
            Destroy(droppedItem);
            return false;
        }

        // Configure the dropped item
        ConfigureDroppedItem(droppedItem, itemData, droppedId);

        // Play drop effects
        PlayDropEffects(validationResult.position);

        DebugLog($"Successfully dropped {itemData.itemName} at {validationResult.position}");
        return true;
    }

    /// <summary>
    /// Creates a dropped item GameObject using the unified visual prefab system
    /// </summary>
    private GameObject CreateDroppedItemObject(ItemData itemData, Vector3 position)
    {
        // Always spawn the visual prefab as the root
        GameObject visualRoot = Instantiate(itemData.visualPrefab, position, Quaternion.identity);

        if (visualRoot == null)
        {
            Debug.LogError($"Failed to instantiate visual prefab for {itemData.itemName}");
            return null;
        }

        // Parent to dropped items container if available
        if (droppedItemsContainer != null)
        {
            visualRoot.transform.SetParent(droppedItemsContainer);
        }

        // Apply scale override if specified
        Vector3 targetScale = itemData.GetVisualPrefabScale();
        if (targetScale != Vector3.one)
        {
            visualRoot.transform.localScale = targetScale;
        }

        // set up advanced culling system for visual root
        SetUpAdvancedCulling(visualRoot);

        // Setup or configure physics
        SetupItemPhysics(visualRoot, itemData);

        // Add pickup interaction child
        GameObject pickupChild = CreatePickupInteractionChild(visualRoot.transform);
        if (pickupChild == null)
        {
            Debug.LogError($"Failed to create pickup interaction child for {itemData.itemName}");
            Destroy(visualRoot);
            return null;
        }

        activeItems.Add(visualRoot);
        EnforceItemLimit();

        return visualRoot;
    }

    private void SetUpAdvancedCulling(GameObject visualRoot)
    {
        // try to get a dc_sourcesettings component, if it doesn't have one add it
        DC_SourceSettings dcSourceSettings = visualRoot.GetComponent<DC_SourceSettings>();
        DC_CullingTargetObserver cullingTargetObserver = visualRoot.GetComponent<DC_CullingTargetObserver>();
        if (dcSourceSettings == null && cullingTargetObserver == null)
        {
            visualRoot.AddComponent<DC_SourceSettings>();
        }

    }

    /// <summary>
    /// Sets up or configures physics for the dropped item
    /// </summary>
    private void SetupItemPhysics(GameObject visualRoot, ItemData itemData)
    {
        if (!itemData.usePhysicsOnDrop)
            return;

        // Get or add rigidbody
        Rigidbody rb = visualRoot.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = visualRoot.AddComponent<Rigidbody>();
        }

        // Configure physics properties
        rb.mass = itemData.objectMass;

        if (dropSettings != null)
        {
            rb.linearDamping = dropSettings.itemDrag;
            rb.angularDamping = dropSettings.itemAngularDrag;
        }

        // Add MeshCollider if not present
        MeshCollider meshCollider = visualRoot.GetComponent<MeshCollider>();
        if (meshCollider == null)
            meshCollider = visualRoot.AddComponent<MeshCollider>();
        meshCollider.convex = true; // Ensure it's suitable for physics interactions

        //for items that should float add a water object
        if (itemData.shouldFloat)
        {
            WaterObject waterObject = visualRoot.AddComponent<WaterObject>();
            waterObject.targetTriangleCount = 20;
            waterObject.buoyantForceCoefficient = rb.mass * 0.1f; // Adjust buoyancy based on mass
            waterObject.GenerateSimMesh();
        }

        DebugLog($"Physics setup for {itemData.itemName} - Mass: {rb.mass}, Drag: {rb.linearDamping}, Angular Drag: {rb.angularDamping}");
    }

    /// <summary>
    /// Creates the pickup interaction child component
    /// </summary>
    private GameObject CreatePickupInteractionChild(Transform parent)
    {
        GameObject pickupChild;

        if (useObjectPooling && pickupPool.Count > 0)
        {
            pickupChild = pickupPool.Dequeue();
            pickupChild.transform.SetParent(parent);
            pickupChild.transform.localPosition = Vector3.zero;
            pickupChild.transform.localRotation = Quaternion.identity;
            pickupChild.SetActive(true);
        }
        else
        {
            pickupChild = Instantiate(pickupInteractionPrefab, parent);
            pickupChild.transform.localPosition = Vector3.zero;
            pickupChild.transform.localRotation = Quaternion.identity;
        }

        return pickupChild;
    }

    /// <summary>
    /// Configures a dropped item with data and interaction setup
    /// </summary>
    private void ConfigureDroppedItem(GameObject droppedItem, ItemData itemData, string droppedId)
    {
        // Find the pickup interaction child
        var pickupComponent = droppedItem.GetComponentInChildren<ItemPickupInteractable>();
        if (pickupComponent != null)
        {
            pickupComponent.SetItemData(itemData);
            pickupComponent.SetInteractableID(droppedId);
            pickupComponent.MarkAsDroppedItem();
            pickupComponent.ConfigurePhysics(dropSettings);
            pickupComponent.SetupComponentReferences();
        }
        else
        {
            Debug.LogError($"No ItemPickupInteractable found in children of {itemData.itemName}");
        }

        // Configure interaction collider size based on visual bounds
        ConfigureInteractionCollider(droppedItem, itemData);
    }

    /// <summary>
    /// Configures the interaction collider based on visual bounds or item data
    /// </summary>
    private void ConfigureInteractionCollider(GameObject droppedItem, ItemData itemData)
    {
        var pickupComponent = droppedItem.GetComponentInChildren<ItemPickupInteractable>();
        if (pickupComponent == null) return;

        var collider = pickupComponent.GetComponent<Collider>();
        if (collider == null) return;

        // If item specifies custom collider size, use that
        if (itemData.interactionColliderSize != Vector3.zero)
        {
            if (collider is SphereCollider sphereCollider)
            {
                float radius = Mathf.Max(itemData.interactionColliderSize.x, itemData.interactionColliderSize.y, itemData.interactionColliderSize.z) * 0.5f;
                sphereCollider.radius = radius;
            }
            else if (collider is BoxCollider boxCollider)
            {
                boxCollider.size = itemData.interactionColliderSize;
            }
        }
        else
        {
            // Auto-calculate based on visual bounds
            AutoConfigureColliderFromVisualBounds(droppedItem, collider);
        }
    }

    /// <summary>
    /// Auto-configures collider size based on the visual prefab bounds
    /// </summary>
    private void AutoConfigureColliderFromVisualBounds(GameObject droppedItem, Collider collider)
    {
        // Get bounds from all renderers in the visual root
        Renderer[] renderers = droppedItem.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        // Calculate combined bounds
        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        // Convert world bounds to local space of the collider
        Vector3 localSize = droppedItem.transform.InverseTransformVector(combinedBounds.size);

        // Apply to collider with some padding
        float padding = 1.2f; // 20% padding for easier interaction

        if (collider is SphereCollider sphereCollider)
        {
            float radius = Mathf.Max(localSize.x, localSize.y, localSize.z) * 0.5f * padding;
            sphereCollider.radius = radius;
        }
        else if (collider is BoxCollider boxCollider)
        {
            boxCollider.size = localSize * padding;
        }
    }

    /// <summary>
    /// Plays drop effects at the specified position
    /// </summary>
    private void PlayDropEffects(Vector3 position)
    {
        if (dropSettings == null) return;

        // Play drop effect
        if (dropSettings.dropEffect != null)
        {
            Instantiate(dropSettings.dropEffect, position, Quaternion.identity);
        }

        // Play drop sound
        if (dropSettings.dropSound != null)
        {
            AudioSource.PlayClipAtPoint(dropSettings.dropSound, position);
        }
    }

    /// <summary>
    /// Registers the dropped item with SceneItemStateManager
    /// </summary>
    private string RegisterDroppedItemWithStateManager(ItemData itemData, Vector3 position)
    {
        DebugLog($"Registering {itemData.itemName} with SceneItemStateManager");

        if (SceneItemStateManager.Instance == null)
        {
            DebugLog("SceneItemStateManager not found");
            return null;
        }

        return SceneItemStateManager.Instance.AddDroppedInventoryItem(itemData, position);
    }

    /// <summary>
    /// Gets the ideal drop position relative to the player
    /// </summary>
    private Vector3 GetPlayerDropPosition()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null && dropValidator != null)
        {
            return dropValidator.GetIdealDropPosition(player.transform);
        }

        DebugLog("No player found - dropping at world origin");
        return Vector3.zero;
    }

    #endregion

    #region UPDATED: User Feedback System

    /// <summary>
    /// UPDATED: Shows appropriate feedback message for drop failures
    /// </summary>
    private void ShowDropFailureMessage(DropAttemptResult dropResult)
    {
        string userMessage = GetUserFriendlyFailureMessage(dropResult.failureReason, dropResult.reason);

        // You can integrate this with your UI notification system
        Debug.LogWarning($"Drop Failed: {userMessage}");

        // TODO: Integrate with your UI system to show notifications
        // Example: UINotificationManager.Instance?.ShowNotification(userMessage, NotificationType.Warning);
    }

    /// <summary>
    /// UPDATED: Converts technical failure reasons to user-friendly messages
    /// </summary>
    private string GetUserFriendlyFailureMessage(DropFailureReason failureReason, string technicalReason)
    {
        switch (failureReason)
        {
            case DropFailureReason.ItemNotFound:
                return "Item not found in inventory.";

            case DropFailureReason.ItemNotDroppable:
                return "This item cannot be dropped.";

            case DropFailureReason.NoVisualPrefab:
                return "This item cannot be visualized in the world.";

            case DropFailureReason.InvalidDropPosition:
                return "Cannot drop item here. Try moving to a clearer area.";

            case DropFailureReason.InventoryRemovalFailed:
                return "Failed to remove item from inventory.";

            case DropFailureReason.DropFailedButRestored:
                return "Drop failed, but item was safely returned to inventory.";

            case DropFailureReason.DropFailedItemLost:
                return "CRITICAL ERROR: Item was lost during drop attempt!";

            default:
                return technicalReason;
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Enforces the maximum item limit by removing oldest items
    /// </summary>
    private void EnforceItemLimit()
    {
        if (dropSettings == null) return;

        while (activeItems.Count > dropSettings.maxDroppedItems)
        {
            GameObject oldestItem = activeItems[0];
            activeItems.RemoveAt(0);

            if (oldestItem != null)
            {
                ReturnItemToPool(oldestItem);
            }
        }
    }

    /// <summary>
    /// Returns pickup interaction component to pool and destroys visual
    /// </summary>
    private void ReturnItemToPool(GameObject item)
    {
        // Find pickup interaction child
        var pickupComponent = item.GetComponentInChildren<ItemPickupInteractable>();
        if (pickupComponent != null && useObjectPooling && pickupPool.Count < poolSize)
        {
            // Clean up and return pickup child to pool
            GameObject pickupChild = pickupComponent.gameObject;
            CleanupPickupForPooling(pickupChild);

            // Re-parent to dropped items container and deactivate
            if (droppedItemsContainer != null)
            {
                pickupChild.transform.SetParent(droppedItemsContainer);
            }
            else
            {
                pickupChild.transform.SetParent(null);
            }

            pickupChild.SetActive(false);
            pickupPool.Enqueue(pickupChild);
        }

        // Destroy the visual root
        Destroy(item);
    }

    /// <summary>
    /// Cleans up a pickup component before returning to pool
    /// </summary>
    private void CleanupPickupForPooling(GameObject pickup)
    {
        // Reset transform
        pickup.transform.localPosition = Vector3.zero;
        pickup.transform.localRotation = Quaternion.identity;
        pickup.transform.localScale = Vector3.one;

        // Clear pickup component data
        var pickupComponent = pickup.GetComponent<ItemPickupInteractable>();
        if (pickupComponent != null)
        {
            pickupComponent.SetItemData(null);
            pickupComponent.SetInteractableID("");
        }
    }

    /// <summary>
    /// Spawn a dropped item at a specific position (used by SceneItemStateManager for save/load)
    /// </summary>
    public GameObject SpawnDroppedItem(ItemData itemData, Vector3 position, string itemId)
    {
        if (itemData == null)
        {
            Debug.LogError("Cannot spawn dropped item: ItemData is null");
            return null;
        }

        if (!itemData.HasVisualPrefab)
        {
            Debug.LogError($"Cannot spawn dropped item {itemData.itemName}: No visual prefab assigned");
            return null;
        }

        GameObject droppedItem = CreateDroppedItemObject(itemData, position);
        if (droppedItem != null)
        {
            ConfigureDroppedItem(droppedItem, itemData, itemId);
            DebugLog($"Spawned dropped item {itemData.itemName} at {position}");
        }

        return droppedItem;
    }

    /// <summary>
    /// ENHANCED: Spawn a dropped item with rotation (used by SceneItemStateManager for save/load)
    /// </summary>
    public GameObject SpawnDroppedItem(ItemData itemData, Vector3 position, Vector3 rotation, string itemId)
    {
        if (itemData == null)
        {
            Debug.LogError("Cannot spawn dropped item: ItemData is null");
            return null;
        }

        if (!itemData.HasVisualPrefab)
        {
            Debug.LogError($"Cannot spawn dropped item {itemData.itemName}: No visual prefab assigned");
            return null;
        }

        GameObject droppedItem = CreateDroppedItemObject(itemData, position);
        if (droppedItem != null)
        {
            // Apply rotation
            droppedItem.transform.rotation = Quaternion.Euler(rotation);

            ConfigureDroppedItem(droppedItem, itemData, itemId);
            DebugLog($"Spawned dropped item {itemData.itemName} at {position} with rotation {rotation}");
        }

        return droppedItem;
    }

    /// <summary>
    /// Remove a dropped item from tracking (called when picked up)
    /// </summary>
    public void RemoveDroppedItem(GameObject item)
    {
        if (activeItems.Contains(item))
        {
            activeItems.Remove(item);
        }
    }

    /// <summary>
    /// Get current drop settings
    /// </summary>
    public ItemDropSettings GetDropSettings()
    {
        return dropSettings;
    }

    /// <summary>
    /// Update drop settings (useful for runtime configuration)
    /// </summary>
    public void SetDropSettings(ItemDropSettings newSettings)
    {
        dropSettings = newSettings;
        if (dropValidator != null)
        {
            dropValidator.DropSettings = dropSettings;
        }
    }

    /// <summary>
    /// Get statistics about the drop system
    /// </summary>
    public (int activeItems, int pooledPickups, int totalCapacity) GetDropSystemStats()
    {
        return (activeItems.Count, pickupPool.Count, dropSettings?.maxDroppedItems ?? 0);
    }

    /// <summary>
    /// Get the dropped items container transform
    /// </summary>
    public Transform GetDroppedItemsContainer()
    {
        return droppedItemsContainer;
    }

    /// <summary>
    /// Set a custom dropped items container
    /// </summary>
    public void SetDroppedItemsContainer(Transform container)
    {
        droppedItemsContainer = container;
        DebugLog($"Dropped items container set to: {(container != null ? container.name : "null")}");
    }

    /// <summary>
    /// Force cleanup of all dropped items (useful for scene transitions)
    /// </summary>
    public void ClearAllDroppedItems()
    {
        foreach (var item in activeItems.ToArray())
        {
            if (item != null)
            {
                ReturnItemToPool(item);
            }
        }
        activeItems.Clear();
        DebugLog("Cleared all dropped items");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs || (dropSettings != null && dropSettings.enableDebugLogs))
        {
            Debug.Log($"[ItemDropSystem] {message}");
        }
    }

    private void OnDestroy()
    {
        // Clean up when system is destroyed
        ClearAllDroppedItems();
    }
}