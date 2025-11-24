using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Manages the state persistence of all storage containers in the scene.
/// Tracks container contents, positions, and configurations across scene transitions.
/// Automatically discovers containers and handles save/load operations for them.
/// </summary>
public class SceneStorageContainerStateManager : MonoBehaviour, ISaveable
{
    public static SceneStorageContainerStateManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveID = "SceneStorageContainerStateManager";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    [Header("Discovery Settings")]
    [SerializeField] private bool autoDiscoverContainers = true;

    // Container tracking
    private Dictionary<string, StorageContainerSaveData> containerStates = new Dictionary<string, StorageContainerSaveData>();
    private List<StorageContainer> trackedContainers = new List<StorageContainer>();

    // ISaveable implementation
    public string SaveID => saveID;
    public SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DebugLog("SceneStorageContainerStateManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Start container discovery process
        StartCoroutine(InitializeContainerTracking());
    }


    #region Container Discovery and Registration

    /// <summary>
    /// Discovers all storage containers in the scene and begins tracking them
    /// </summary>
    private System.Collections.IEnumerator InitializeContainerTracking()
    {
        // Wait for scene to fully load
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        DebugLog("Starting container discovery and state application...");

        // Find all StorageContainerManager components in scene
        var allContainers = FindObjectsByType<StorageContainer>(FindObjectsSortMode.None);

        // Assign consistent IDs to containers
        AssignConsistentContainerIDs(allContainers.ToList());

        // Register containers that aren't already tracked
        foreach (var container in allContainers)
        {
            RegisterContainerIfNotTracked(container);
        }

        DebugLog($"Container discovery complete: {trackedContainers.Count} containers tracked");

        // Apply saved states to containers
        yield return StartCoroutine(ApplyContainerStatesAfterSceneLoad());
    }

    /// <summary>
    /// Assign consistent IDs using Instance ID for deterministic ordering
    /// </summary>
    private void AssignConsistentContainerIDs(List<StorageContainer> containers)
    {
        var containerGroups = containers
            .GroupBy(v => v.transform.gameObject.name.Replace(" ", "_"))
            .ToList();

        foreach (var group in containerGroups)
        {
            string baseName = group.Key;
            var containersInGroup = group.OrderBy(v => v.GetInstanceID()).ToList();

            for (int i = 0; i < containersInGroup.Count; i++)
            {
                var container = containersInGroup[i];
                string consistentID = $"{baseName}_{(i + 1):D2}";
                container.SetContainerID(consistentID);
                DebugLog($"Assigned ID '{consistentID}' to vehicle instance {container.GetInstanceID()}");
            }
        }
    }

    /// <summary>
    /// Registers a storage container for tracking if not already tracked
    /// </summary>
    private void RegisterContainerIfNotTracked(StorageContainer container)
    {
        if (container == null) return;

        string containerID = container.ContainerID;

        // Only register if not already tracked
        if (!trackedContainers.Contains(container))
        {
            trackedContainers.Add(container);

            // If we don't have saved state for this container, create initial state
            if (!containerStates.ContainsKey(containerID))
            {
                var initialState = CreateContainerSaveData(container);
                containerStates[containerID] = initialState;
                DebugLog($"Registered new container: {container.DisplayName} (ID: {containerID})");
            }
            else
            {
                DebugLog($"Container {container.DisplayName} already has saved state");
            }

            // Subscribe to container events for automatic state updates
            SubscribeToContainerEvents(container);
        }
    }

    /// <summary>
    /// Subscribe to container events for automatic state tracking
    /// </summary>
    private void SubscribeToContainerEvents(StorageContainer container)
    {
        if (container == null) return;

        // Subscribe to inventory change events
        container.OnItemAdded += (item) => UpdateContainerState(container);
        container.OnItemRemoved += (itemId) => UpdateContainerState(container);
        container.OnInventoryCleared += () => UpdateContainerState(container);
        container.OnInventoryDataChanged += (data) => UpdateContainerState(container);

        DebugLog($"Subscribed to events for container: {container.DisplayName}");
    }

    /// <summary>
    /// Periodic discovery of new containers
    /// </summary>
    private void UpdateContainerDiscovery()
    {
        var allContainers = FindObjectsByType<StorageContainer>(FindObjectsSortMode.None);

        foreach (var container in allContainers)
        {
            if (!trackedContainers.Contains(container))
            {
                RegisterContainerIfNotTracked(container);
            }
        }

        // Clean up destroyed containers
        trackedContainers.RemoveAll(container => container == null);
    }

    #endregion

    #region Container State Management

    /// <summary>
    /// Update the state of a specific container
    /// </summary>
    public void UpdateContainerState(StorageContainer container)
    {
        if (container == null) return;

        string containerID = container.ContainerID;
        var saveData = CreateContainerSaveData(container);
        containerStates[containerID] = saveData;

        DebugLog($"Updated state for container: {container.DisplayName} - {saveData.items.Count} items");
    }

    /// <summary>
    /// Create save data from a container's current state
    /// </summary>
    private StorageContainerSaveData CreateContainerSaveData(StorageContainer container)
    {
        var saveData = new StorageContainerSaveData
        {
            containerID = container.ContainerID,
            displayName = container.DisplayName,
            gridWidth = container.GridWidth,
            gridHeight = container.GridHeight,
            nextItemId = container.NextItemId,
            position = container.transform.position,
            rotation = container.transform.eulerAngles,
            isActive = container.gameObject.activeInHierarchy
        };

        // Save all items in the container
        var allItems = container.InventoryGridData.GetAllItems();
        foreach (var item in allItems)
        {
            var itemSaveData = item.ToSaveData();
            if (itemSaveData.IsValid())
            {
                saveData.items.Add(itemSaveData);
            }
        }

        return saveData;
    }

    /// <summary>
    /// Mark a container as collected/destroyed
    /// </summary>
    public void MarkContainerAsCollected(string containerID)
    {
        if (containerStates.TryGetValue(containerID, out var containerData))
        {
            containerData.isCollected = true;
            DebugLog($"Marked container {containerID} as collected");

            // Find and destroy the container GameObject
            var container = FindContainerByID(containerID);
            if (container != null)
            {
                DestroyContainerSafely(container);
            }
        }
        else
        {
            DebugLog($"WARNING: Tried to mark unknown container as collected: {containerID}");
        }
    }

    /// <summary>
    /// Check if a container has been collected
    /// </summary>
    public bool IsContainerCollected(string containerID)
    {
        if (containerStates.TryGetValue(containerID, out var containerData))
        {
            return containerData.isCollected;
        }
        return false;
    }

    #endregion

    #region Scene State Application

    /// <summary>
    /// Apply all container states after scene load
    /// </summary>
    private System.Collections.IEnumerator ApplyContainerStatesAfterSceneLoad()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        DebugLog($"Applying container states to scene - {containerStates.Count} containers tracked");

        // Clean up containers that shouldn't exist
        CleanupCollectedContainers();

        // Apply states to containers that should exist
        ApplyContainerStates();

        // Spawn containers that should exist but aren't in scene (if applicable)
        SpawnMissingContainers();

        DebugLog("Container states applied successfully");
    }

    /// <summary>
    /// Remove containers from scene that were collected
    /// </summary>
    private void CleanupCollectedContainers()
    {
        var allContainers = FindObjectsByType<StorageContainer>(FindObjectsSortMode.None);

        foreach (var container in allContainers)
        {
            string containerID = container.ContainerID;

            if (containerStates.TryGetValue(containerID, out var containerData))
            {
                if (containerData.isCollected)
                {
                    DebugLog($"Removing collected container from scene: {container.DisplayName}");
                    DestroyContainerSafely(container);
                }
            }
        }
    }

    /// <summary>
    /// Apply states to existing containers in scene
    /// </summary>
    private void ApplyContainerStates()
    {
        var containersNeedingStateApplication = containerStates.Values
            .Where(data => !data.isCollected)
            .ToList();

        foreach (var containerData in containersNeedingStateApplication)
        {
            var container = FindContainerByID(containerData.containerID);
            if (container != null)
            {
                DebugLog($"Applying state to existing container: {containerData.displayName}");
                RestoreContainerState(container, containerData);
            }
        }
    }

    /// <summary>
    /// Spawn containers that should exist but aren't currently in the scene
    /// </summary>
    private void SpawnMissingContainers()
    {
        // This method would be used if containers can be dynamically created
        // For now, we assume all containers exist in the scene initially
        DebugLog("Container spawning not implemented - assuming all containers exist in scene");
    }

    /// <summary>
    /// Restore a container to its tracked state
    /// </summary>
    private void RestoreContainerState(StorageContainer container, StorageContainerSaveData saveData)
    {
        if (container == null || saveData == null) return;

        DebugLog($"Restoring container state: {saveData.displayName} with {saveData.items.Count} items");

        try
        {
            // Create new inventory grid with saved dimensions
            var newInventoryData = new InventoryGridData(saveData.gridWidth, saveData.gridHeight);

            // Restore each item to the container
            int restoredCount = 0;
            foreach (var itemSaveData in saveData.items)
            {
                var item = InventoryItemData.FromSaveData(itemSaveData);
                if (item != null)
                {
                    if (newInventoryData.PlaceItem(item))
                    {
                        restoredCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to restore item {item.ID} to container {saveData.displayName}");
                    }
                }
            }

            // Set the restored data to the container
            container.SetInventoryData(newInventoryData, saveData.nextItemId);

            // Restore container transform if it has changed
            if (Vector3.Distance(container.transform.position, saveData.position) > 0.1f)
            {
                container.transform.position = saveData.position;
                container.transform.eulerAngles = saveData.rotation;
                DebugLog($"Restored container position: {saveData.position}");
            }

            // Restore active state
            container.gameObject.SetActive(saveData.isActive);

            DebugLog($"Container state restored: {restoredCount}/{saveData.items.Count} items placed successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to restore container state for {saveData.displayName}: {e.Message}");
        }
    }

    #endregion

    #region ISaveable Implementation

    public object GetDataToSave()
    {
        // Update all container states before saving
        UpdateAllContainerStates();

        var saveData = new SceneStorageContainerStateSaveData
        {
            containerStates = containerStates.Values.ToList(),
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            lastUpdated = System.DateTime.Now
        };

        DebugLog($"GetDataToSave: {containerStates.Count} containers");
        return saveData;
    }

    public object ExtractRelevantData(object saveContainer)
    {
        if (saveContainer is SceneSaveData sceneData)
        {
            return sceneData.GetObjectData<SceneStorageContainerStateSaveData>(SaveID);
        }
        return saveContainer;
    }

    public void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is SceneStorageContainerStateSaveData saveData)
        {
            DebugLog($"Loading container state data (Context: {context})");

            // Load all container states
            containerStates = saveData.containerStates?.ToDictionary(container => container.containerID, container => container)
                            ?? new Dictionary<string, StorageContainerSaveData>();

            DebugLog($"Loaded: {containerStates.Count} container states");

            // Context-specific handling
            switch (context)
            {
                case RestoreContext.NewGame:
                    DebugLog("New game - clearing all container states");
                    containerStates.Clear();
                    break;
            }
        }
    }

    public void OnAfterLoad()
    {
        DebugLog("Container state data loaded - applying to scene");
        StartCoroutine(ApplyContainerStatesAfterSaveLoad());
    }

    private System.Collections.IEnumerator ApplyContainerStatesAfterSaveLoad()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.2f);

        DebugLog("Applying loaded container states to scene");
        yield return StartCoroutine(ApplyContainerStatesAfterSceneLoad());
        DebugLog("Loaded container states applied successfully");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Force update all container states
    /// </summary>
    public void UpdateAllContainerStates()
    {
        foreach (var container in trackedContainers)
        {
            if (container != null)
            {
                UpdateContainerState(container);
            }
        }
    }

    /// <summary>
    /// Get container state by ID
    /// </summary>
    public StorageContainerSaveData GetContainerState(string containerID)
    {
        containerStates.TryGetValue(containerID, out var state);
        return state;
    }

    /// <summary>
    /// Get all container states
    /// </summary>
    public Dictionary<string, StorageContainerSaveData> GetAllContainerStates()
    {
        return new Dictionary<string, StorageContainerSaveData>(containerStates);
    }

    /// <summary>
    /// Public API: Handle when a container is collected/destroyed
    /// </summary>
    public static void OnContainerCollected(string containerID)
    {
        Instance?.MarkContainerAsCollected(containerID);
    }

    /// <summary>
    /// Public API: Force update tracking for a container
    /// </summary>
    public static void NotifyContainerChanged(StorageContainer container)
    {
        Instance?.UpdateContainerState(container);
    }

    /// <summary>
    /// Get debug information about all tracked containers
    /// </summary>
    [Button("Debug All Containers")]
    public void DebugAllContainers()
    {
        DebugLog("=== ALL TRACKED CONTAINERS DEBUG ===");
        DebugLog($"Total Containers: {containerStates.Count}");

        foreach (var kvp in containerStates)
        {
            var containerData = kvp.Value;
            DebugLog($"Container: {containerData.displayName} (ID: {containerData.containerID})");
            DebugLog($"  Items: {containerData.items.Count}");
            DebugLog($"  Grid: {containerData.gridWidth}x{containerData.gridHeight}");
            DebugLog($"  Position: {containerData.position}");
            DebugLog($"  Active: {containerData.isActive}");
            DebugLog($"  Collected: {containerData.isCollected}");
        }
        DebugLog("===============================");
    }

    #endregion

    #region Utility Methods

    private StorageContainer FindContainerByID(string containerID)
    {
        return trackedContainers.FirstOrDefault(c => c != null && c.ContainerID == containerID);
    }

    private void DestroyContainerSafely(StorageContainer container)
    {
        if (container != null)
        {
            trackedContainers.Remove(container);
            Destroy(container.gameObject);
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneStorageContainerStateManager] {message}");
        }
    }

    private void OnDestroy()
    {
        CancelInvoke(nameof(UpdateContainerDiscovery));
    }

    #endregion
}

/// <summary>
/// Save data for individual storage containers
/// </summary>
[System.Serializable]
public class StorageContainerSaveData
{
    [Header("Container Identity")]
    public string containerID;
    public string displayName;

    [Header("Grid Configuration")]
    public int gridWidth;
    public int gridHeight;
    public int nextItemId;

    [Header("Container Transform")]
    public Vector3 position;
    public Vector3 rotation;
    public bool isActive = true;

    [Header("Container State")]
    public bool isCollected = false;

    [Header("Container Contents")]
    public List<InventoryItemSaveData> items = new List<InventoryItemSaveData>();

    public StorageContainerSaveData()
    {
        items = new List<InventoryItemSaveData>();
    }

    /// <summary>
    /// Validate that this container save data is valid
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(containerID) &&
               !string.IsNullOrEmpty(displayName) &&
               gridWidth > 0 && gridHeight > 0;
    }

    /// <summary>
    /// Get debug string representation
    /// </summary>
    public override string ToString()
    {
        return $"Container[{containerID}] {displayName} - {items.Count} items, Grid: {gridWidth}x{gridHeight}";
    }
}
