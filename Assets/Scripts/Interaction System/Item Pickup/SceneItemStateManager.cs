using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// SIMPLIFIED: Unified Scene Item State Manager
/// Manages all pickup items (original and dropped) using a single PickupItemData structure
/// Tracks position, rotation, physics state, and collection status for all item types
/// </summary>
public class SceneItemStateManager : MonoBehaviour, ISaveable
{
    public static SceneItemStateManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveID = "SceneItemStateManager";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    [Header("Tracking Settings")]
    [SerializeField] private float positionChangeThreshold = 0.01f;
    [SerializeField] private float rotationChangeThreshold = 1f; // degrees
    [SerializeField] private float trackingUpdateInterval = 1f; // seconds

    // UNIFIED: Single collection for all pickup items
    [ShowInInspector] private Dictionary<string, PickupItemData> allPickupItems = new Dictionary<string, PickupItemData>();

    // ID generation for dropped items
    private int nextDroppedItemId = 1;

    // ISaveable implementation
    public string SaveID => saveID;
    public SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DebugLog("SceneItemStateManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Discover and register all original items, then apply any saved states
        StartCoroutine(InitializeItemTracking());

        // Start periodic tracking updates
        if (trackingUpdateInterval > 0)
        {
            InvokeRepeating(nameof(UpdateItemTracking), trackingUpdateInterval, trackingUpdateInterval);
        }
    }

    #region Item Discovery and Registration

    /// <summary>
    /// Discovers all original scene items and begins tracking them
    /// </summary>
    private System.Collections.IEnumerator InitializeItemTracking()
    {
        // Wait for scene to fully load
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        DebugLog("Starting item discovery and state application...");

        // Find all ItemPickupInteractable components in scene
        var allPickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);

        // Register original scene items that aren't already tracked
        var originalPickups = allPickups.Where(p => p.IsOriginalSceneItem).ToArray();
        foreach (var pickup in originalPickups)
        {
            RegisterOriginalItemIfNotTracked(pickup);
        }

        DebugLog($"Item discovery complete: {allPickupItems.Count} total items tracked");

        // Apply saved states to the scene
        yield return StartCoroutine(ApplyItemStatesAfterSceneLoad());
    }

    /// <summary>
    /// Registers an original scene item for tracking if not already tracked
    /// </summary>
    private void RegisterOriginalItemIfNotTracked(ItemPickupInteractable pickup)
    {
        if (pickup == null) return;

        string itemId = pickup.InteractableID;

        // Only register if not already tracked (preserves loaded save data)
        if (!allPickupItems.ContainsKey(itemId))
        {
            var itemData = new PickupItemData(itemId, pickup, PickupItemType.OriginalSceneItem);
            allPickupItems[itemId] = itemData;
            DebugLog($"Registered new original item: {itemId} at {itemData.originalPosition}");
        }
        else
        {
            DebugLog($"Original item {itemId} already tracked (from save data)");
        }
    }

    /// <summary>
    /// Updates tracking data for all active items periodically
    /// </summary>
    private void UpdateItemTracking()
    {
        var activeItems = allPickupItems.Values.Where(item => item.ShouldExistInScene()).ToList();

        foreach (var itemData in activeItems)
        {
            var pickup = FindPickupById(itemData.itemId);
            if (pickup != null)
            {
                Transform rootTransform = pickup.GetRootTransform();
                Rigidbody rb = rootTransform?.GetComponent<Rigidbody>();

                itemData.UpdateCurrentState(rootTransform, rb);
            }
        }
    }

    #endregion

    #region Unified Item Management

    /// <summary>
    /// Mark any item as collected (works for both original and dropped items)
    /// </summary>
    public void MarkItemAsCollected(string itemId)
    {
        if (allPickupItems.TryGetValue(itemId, out var itemData))
        {
            itemData.MarkAsCollected();
            DebugLog($"Marked item {itemId} ({itemData.itemType}) as collected");

            // Destroy the pickup GameObject
            var pickup = FindPickupById(itemId);
            if (pickup != null)
            {
                DestroyPickupSafely(pickup);
            }
        }
        else
        {
            DebugLog($"WARNING: Tried to mark unknown item as collected: {itemId}");
        }
    }

    /// <summary>
    /// Check if any item has been collected
    /// </summary>
    public bool IsItemCollected(string itemId)
    {
        if (allPickupItems.TryGetValue(itemId, out var itemData))
        {
            return itemData.isCollected;
        }

        // If item not tracked, assume it exists
        return false;
    }

    /// <summary>
    /// Update the state of any item (when moved by physics, interactions, etc.)
    /// </summary>
    public void UpdateItemState(string itemId)
    {
        if (allPickupItems.TryGetValue(itemId, out var itemData) && itemData.ShouldExistInScene())
        {
            var pickup = FindPickupById(itemId);
            if (pickup != null)
            {
                Transform rootTransform = pickup.GetRootTransform();
                Rigidbody rb = rootTransform?.GetComponent<Rigidbody>();

                itemData.UpdateCurrentState(rootTransform, rb);
                DebugLog($"Updated item state: {itemId} - {itemData.GetDebugInfo()}");
            }
        }
    }

    /// <summary>
    /// Add a dropped inventory item to tracking
    /// </summary>
    public string AddDroppedInventoryItem(ItemData itemData, Vector3 position, Vector3 rotation = default)
    {
        string droppedId = CalculateDroppedItemId(itemData.name, position);
        var pickupData = PickupItemData.FromItemData(droppedId, itemData, position, rotation);

        // Add to tracking
        allPickupItems[droppedId] = pickupData;

        DebugLog($"Added dropped inventory item {droppedId} ({itemData.itemName}) at {position}");

        return droppedId;
    }

    private string CalculateDroppedItemId(string name, Vector3 position)
    {
        string positionHash = position.GetHashCode().ToString();
        string droppedId = $"dropped_item_{name}_{positionHash}_{nextDroppedItemId++}";

        return droppedId;
    }

    /// <summary>
    /// Remove any item from tracking (when picked up)
    /// </summary>
    public void RemoveItemFromTracking(string itemId)
    {
        if (allPickupItems.Remove(itemId))
        {
            DebugLog($"Removed item from tracking: {itemId}");
        }
    }

    /// <summary>
    /// Restore an item that was collected (when dropped back from inventory)
    /// </summary>
    public void RestoreItem(string itemId, Vector3? newPosition = null, Vector3? newRotation = null)
    {
        if (allPickupItems.TryGetValue(itemId, out var itemData) && itemData.isCollected)
        {
            itemData.RestoreToScene(newPosition, newRotation);
            DebugLog($"Restored item {itemId} to scene");

            // Respawn the item
            RespawnItem(itemData);
        }
    }

    /// <summary>
    /// Respawns an item at its current tracked position
    /// </summary>
    private void RespawnItem(PickupItemData itemData)
    {
        ItemData itemDataAsset = FindItemDataByName(itemData.itemDataName);
        if (itemDataAsset == null)
        {
            DebugLog($"ItemData {itemData.itemDataName} not found for respawn");
            return;
        }

        GameObject spawnedItem = ItemDropSystem.Instance?.SpawnDroppedItem(
            itemDataAsset, itemData.currentPosition, itemData.currentRotation, itemData.itemId);

        if (spawnedItem != null)
        {
            var pickup = spawnedItem.GetComponentInChildren<ItemPickupInteractable>();
            if (pickup != null)
            {
                // Set the correct item type
                if (itemData.itemType == PickupItemType.OriginalSceneItem)
                {
                    pickup.MarkAsOriginalSceneItem();
                }
                else
                {
                    pickup.MarkAsDroppedItem();
                }
            }

            // Apply physics state
            Rigidbody rb = spawnedItem.GetComponent<Rigidbody>();
            if (rb != null && itemData.hasRigidbody)
            {
                itemData.ApplyToTransform(rb);
            }

            DebugLog($"Respawned item {itemData.itemId} ({itemData.itemType}) at {itemData.currentPosition}");
        }
    }

    #endregion

    #region Scene State Application

    /// <summary>
    /// Apply all item states after scene load
    /// </summary>
    private System.Collections.IEnumerator ApplyItemStatesAfterSceneLoad()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        DebugLog($"Applying item states to scene - {allPickupItems.Count} total items tracked");

        // Clean up items that shouldn't exist
        CleanupInvalidItems();

        // Apply states to items that should exist
        ApplyItemStates();

        // Spawn items that should exist but aren't in scene
        SpawnMissingItems();

        DebugLog("Item states applied successfully");
    }

    /// <summary>
    /// Remove items from scene that shouldn't exist (collected items, invalid dropped items)
    /// </summary>
    private void CleanupInvalidItems()
    {
        var allPickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);

        foreach (var pickup in allPickups)
        {
            string itemId = pickup.InteractableID;

            if (allPickupItems.TryGetValue(itemId, out var itemData))
            {
                // Item is tracked - remove if it was collected
                if (itemData.isCollected)
                {
                    DebugLog($"Removing collected item from scene: {itemId}");
                    DestroyPickupSafely(pickup);
                }
            }
            else
            {
                // Item not tracked - this is either a new original item or invalid dropped item
                if (pickup.IsDroppedInventoryItem)
                {
                    DebugLog($"Removing invalid dropped item: {itemId}");
                    DestroyPickupSafely(pickup);
                }
                // Original items that aren't tracked will be registered by discovery process
            }
        }
    }

    /// <summary>
    /// Apply states to existing items in scene (position, rotation changes)
    /// </summary>
    private void ApplyItemStates()
    {
        var itemsNeedingStateApplication = allPickupItems.Values
            .Where(item => item.ShouldExistInScene() && item.HasChangedFromOriginal())
            .ToList();

        foreach (var itemData in itemsNeedingStateApplication)
        {
            var pickup = FindPickupById(itemData.itemId);
            if (pickup != null)
            {
                DebugLog($"Applying state to existing item: {itemData.itemId}");
                RestoreItemState(pickup, itemData);
            }
        }
    }

    /// <summary>
    /// Spawn items that should exist but aren't currently in the scene
    /// </summary>
    private void SpawnMissingItems()
    {
        var itemsNeedingSpawn = allPickupItems.Values
            .Where(item => item.ShouldExistInScene())
            .Where(item => FindPickupById(item.itemId) == null)
            .ToList();

        foreach (var itemData in itemsNeedingSpawn)
        {
            DebugLog($"Spawning missing item: {itemData.itemId} ({itemData.itemType})");
            RespawnItem(itemData);
        }
    }

    /// <summary>
    /// Restore an item to its tracked state
    /// </summary>
    private void RestoreItemState(ItemPickupInteractable pickup, PickupItemData itemData)
    {
        Transform rootTransform = pickup.GetRootTransform() ?? pickup.transform;
        Rigidbody rb = rootTransform.GetComponent<Rigidbody>();

        itemData.ApplyToTransform(rb);
        DebugLog($"Restored item state: {itemData.itemId} to {itemData.currentPosition}");
    }

    #endregion

    #region Physics State Management

    /// <summary>
    /// Save current physics states before scene transitions
    /// </summary>
    public void SaveCurrentPhysicsStates()
    {
        var activeItems = allPickupItems.Values.Where(item => item.ShouldExistInScene()).ToList();

        foreach (var itemData in activeItems)
        {
            var pickup = FindPickupById(itemData.itemId);
            if (pickup != null)
            {
                Transform rootTransform = pickup.GetRootTransform();
                Rigidbody rb = rootTransform?.GetComponent<Rigidbody>();

                itemData.UpdateCurrentState(rootTransform, rb);
            }
        }

        DebugLog($"Saved physics states for {activeItems.Count} active items");
    }

    #endregion

    #region ISaveable Implementation

    public object GetDataToSave()
    {
        // Ensure current state is captured
        UpdateItemTracking();
        SaveCurrentPhysicsStates();

        var saveData = new SceneItemStateSaveData
        {
            allPickupItems = allPickupItems.Values.ToList(),
            nextDroppedItemId = nextDroppedItemId,
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            lastUpdated = System.DateTime.Now
        };

        DebugLog($"GetDataToSave: {allPickupItems.Count} total items");
        return saveData;
    }

    public object ExtractRelevantData(object saveContainer)
    {
        if (saveContainer is SceneSaveData sceneData)
        {
            return sceneData.GetObjectData<SceneItemStateSaveData>(SaveID);
        }
        return saveContainer;
    }

    public void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is SceneItemStateSaveData saveData)
        {
            DebugLog($"Loading item state data (Context: {context})");

            // Load all items
            allPickupItems = saveData.allPickupItems?.ToDictionary(item => item.itemId, item => item)
                            ?? new Dictionary<string, PickupItemData>();

            nextDroppedItemId = saveData.nextDroppedItemId;

            DebugLog($"Loaded: {allPickupItems.Count} total items");

            // Context-specific handling
            switch (context)
            {
                case RestoreContext.NewGame:
                    DebugLog("New game - clearing all item states");
                    allPickupItems.Clear();
                    nextDroppedItemId = 1;
                    break;
            }
        }
    }

    public void OnAfterLoad()
    {
        DebugLog("Item state data loaded - applying to scene");
        StartCoroutine(ApplyItemStatesAfterSaveLoad());
    }

    private System.Collections.IEnumerator ApplyItemStatesAfterSaveLoad()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.2f);

        DebugLog("Applying loaded item states to scene");
        yield return StartCoroutine(ApplyItemStatesAfterSceneLoad());
        DebugLog("Loaded item states applied successfully");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Public API: Handle when any item is picked up
    /// </summary>
    public static void OnItemPickedUp(string itemId)
    {
        Instance?.MarkItemAsCollected(itemId);
    }

    /// <summary>
    /// Public API: Drop an item from inventory
    /// </summary>
    public static bool DropItemFromInventoryIntoScene(ItemData itemData, Vector3? position = null)
    {
        if (Instance == null || itemData == null) return false;

        Vector3 dropPosition = position ?? CalculateDropPosition();
        string droppedId = Instance.AddDroppedInventoryItem(itemData, dropPosition);

        // Spawn the item immediately
        var itemTrackingData = Instance.allPickupItems[droppedId];
        Instance.RespawnItem(itemTrackingData);

        return true;
    }

    /// <summary>
    /// Public API: Force update tracking for an item
    /// </summary>
    public static void NotifyItemMoved(string itemId)
    {
        Instance?.UpdateItemState(itemId);
    }

    /// <summary>
    /// Get debug information about all tracked items
    /// </summary>
    [Button("Debug All Items")]
    public void DebugAllItems()
    {
        DebugLog("=== ALL TRACKED ITEMS DEBUG ===");
        DebugLog($"Total Items: {allPickupItems.Count}");

        var originalItems = allPickupItems.Values.Where(i => i.itemType == PickupItemType.OriginalSceneItem).ToList();
        var droppedItems = allPickupItems.Values.Where(i => i.itemType == PickupItemType.DroppedInventoryItem).ToList();

        DebugLog($"Original Items: {originalItems.Count}");
        foreach (var item in originalItems)
        {
            DebugLog($"  {item.GetDebugInfo()}");
        }

        DebugLog($"Dropped Items: {droppedItems.Count}");
        foreach (var item in droppedItems)
        {
            DebugLog($"  {item.GetDebugInfo()}");
        }
        DebugLog("===============================");
    }

    #endregion

    #region Utility Methods

    private ItemPickupInteractable FindPickupById(string itemId)
    {
        var allPickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);
        return allPickups.FirstOrDefault(p => p.InteractableID == itemId);
    }

    private ItemData FindItemDataByName(string itemDataName)
    {
        ItemData itemData = Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + itemDataName);
        if (itemData != null) return itemData;

        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        return allItemData.FirstOrDefault(data => data.name == itemDataName);
    }

    private void DestroyPickupSafely(ItemPickupInteractable pickup)
    {
        if (ItemDropSystem.Instance != null)
        {
            ItemDropSystem.Instance.RemoveDroppedItem(pickup.GetRootTransform().gameObject);
        }

        Destroy(pickup.GetRootTransform()?.gameObject ?? pickup.gameObject);
    }

    private static Vector3 CalculateDropPosition()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return Vector3.zero;

        Vector3 dropPosition = player.transform.position + player.transform.forward * 2f;
        dropPosition.y += 0.5f;

        if (Physics.Raycast(dropPosition, Vector3.down, out RaycastHit hit, 10f))
        {
            dropPosition.y = hit.point.y + 0.1f;
        }

        return dropPosition;
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneItemStateManager] {message}");
        }
    }

    private void OnDestroy()
    {
        CancelInvoke(nameof(UpdateItemTracking));
    }

    #endregion
}

/// <summary>
/// Simplified save data structure
/// </summary>
[System.Serializable]
public class SceneItemStateSaveData
{
    public List<PickupItemData> allPickupItems = new List<PickupItemData>();
    public int nextDroppedItemId = 1;
    public string sceneName;
    public System.DateTime lastUpdated;
}