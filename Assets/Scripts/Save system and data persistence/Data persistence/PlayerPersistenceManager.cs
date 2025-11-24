// PlayerPersistenceManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

/// <summary>
/// Manages persistence of player-dependent data across scene transitions.
/// Automatically discovers and coordinates with all player-dependent save components
/// without requiring hardcoded references. Handles three restoration contexts:
/// doorway transitions, save file loads, and new game initialization.
/// </summary>
public class PlayerPersistenceManager : MonoBehaviour
{
    public static PlayerPersistenceManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Automatically discovered save components
    private List<ISaveable> playerDependentSaveables = new List<ISaveable>();

    // Temporary storage for scene transitions
    private Dictionary<string, object> persistentPlayerData = new Dictionary<string, object>();
    private bool hasPersistentData = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DebugLog("PlayerPersistenceManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        DiscoverPlayerDependentSaveables();
    }

    /// <summary>
    /// Automatically finds all player-dependent save components in the scene.
    /// This modularity means new save components can be added without modifying this manager.
    /// </summary>
    private void DiscoverPlayerDependentSaveables()
    {
        var allSaveables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveable>()
            .Where(s => s.SaveCategory == SaveDataCategory.PlayerDependent)
            .ToList();

        playerDependentSaveables = allSaveables;

        DebugLog($"Discovered {playerDependentSaveables.Count} player-dependent save components:");
        foreach (var saveable in playerDependentSaveables)
        {
            string componentType = saveable is IPlayerDependentSaveable ? "Enhanced" : "Legacy";
            DebugLog($"  - {saveable.SaveID} ({saveable.GetType().Name}) [{componentType}]");
        }
    }

    /// <summary>
    /// Prepares player data for doorway transitions by collecting current state
    /// from all player-dependent components. Called before leaving a scene via doorway.
    /// </summary>
    public void UpdatePersistentPlayerDataForTransition()
    {
        DebugLog("=== PREPARING PLAYER DATA FOR TRANSITION ===");

        DiscoverPlayerDependentSaveables();
        persistentPlayerData.Clear();

        // Collect data from each save component
        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                saveable.OnBeforeSave();
                var data = saveable.GetDataToSave();
                if (data != null)
                {
                    persistentPlayerData[saveable.SaveID] = data;
                    DebugLog($"Prepared data for {saveable.SaveID}: {data.GetType().Name}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to prepare data for {saveable.SaveID}: {e.Message}");
            }
        }

        hasPersistentData = true;
        DebugLog($"Preparation complete - stored data for {persistentPlayerData.Count} components");
    }

    /// <summary>
    /// Restores player data after doorway transitions. Restores stats, inventory,
    /// and equipment but not position (doorway handles positioning).
    /// </summary>
    public void RestoreForDoorwayTransition()
    {
        DebugLog("=== RESTORING PLAYER DATA FOR DOORWAY TRANSITION ===");

        if (!hasPersistentData)
        {
            DebugLog("No persistent data available for doorway transition");
            return;
        }

        DiscoverPlayerDependentSaveables();

        // Restore data to each component with doorway context
        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                if (persistentPlayerData.TryGetValue(saveable.SaveID, out var data))
                {
                    saveable.LoadSaveDataWithContext(data, RestoreContext.DoorwayTransition);
                    saveable.OnAfterLoad();
                    DebugLog($"Restored doorway data for {saveable.SaveID}");
                }
                else
                {
                    DebugLog($"No persistent data found for {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore doorway data for {saveable.SaveID}: {e.Message}");
            }
        }

        ClearPersistentData();
        DebugLog("Doorway transition player data restoration complete");
    }

    /// <summary>
    /// Restores complete player data from save files including position and all state.
    /// Used when loading saved games to restore exact game state.
    /// </summary>
    public void RestoreFromSaveFile(Dictionary<string, object> saveData)
    {
        DebugLog("=== RESTORING PLAYER DATA FROM SAVE FILE ===");

        if (saveData == null)
        {
            DebugLog("No save data provided");
            return;
        }

        ClearPersistentData();
        DiscoverPlayerDependentSaveables();

        // Restore data to each component with save file context
        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                var componentData = ExtractComponentDataFromSave(saveable, saveData);
                if (componentData != null)
                {
                    saveable.LoadSaveDataWithContext(componentData, RestoreContext.SaveFileLoad);
                    saveable.OnAfterLoad();
                    DebugLog($"Restored save file data for {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore save file data for {saveable.SaveID}: {e.Message}");
            }
        }

        DebugLog("Save file player data restoration complete");
    }

    /// <summary>
    /// Initializes fresh player data for new games with default values.
    /// Clears any existing persistent data and sets starting state.
    /// </summary>
    public void InitializeForNewGame()
    {
        DebugLog("=== INITIALIZING PLAYER DATA FOR NEW GAME ===");

        ClearPersistentData();
        DiscoverPlayerDependentSaveables();

        // Initialize each component with default values
        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                var defaultData = CreateDefaultDataForComponent(saveable);
                if (defaultData != null)
                {
                    saveable.LoadSaveDataWithContext(defaultData, RestoreContext.NewGame);
                    saveable.OnAfterLoad();
                    DebugLog($"Initialized default data for {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize {saveable.SaveID} for new game: {e.Message}");
            }
        }

        DebugLog("New game player data initialization complete");
    }

    /// <summary>
    /// Extracts component-specific data from save containers using the modular interface.
    /// Components handle their own data extraction for maximum flexibility.
    /// </summary>
    private object ExtractComponentDataFromSave(ISaveable saveable, Dictionary<string, object> saveData)
    {
        // Check for direct PlayerSaveData (contains position data)
        if (saveData.ContainsKey("playerSaveData"))
        {
            var playerSaveData = saveData["playerSaveData"] as PlayerSaveData;
            if (playerSaveData != null)
            {
                return saveable.ExtractRelevantData(playerSaveData);
            }
        }

        // Check for PlayerPersistentData (modular approach)
        if (saveData.ContainsKey("playerPersistentData"))
        {
            var persistentData = saveData["playerPersistentData"] as PlayerPersistentData;
            if (persistentData != null)
            {
                return ExtractFromPlayerPersistentDataModular(saveable, persistentData);
            }
        }

        // Direct component lookup
        if (saveData.ContainsKey(saveable.SaveID))
        {
            return saveData[saveable.SaveID];
        }

        return null;
    }

    /// <summary>
    /// Uses the enhanced modular interface to extract component data from unified save structure.
    /// </summary>
    private object ExtractFromPlayerPersistentDataModular(ISaveable saveable, PlayerPersistentData persistentData)
    {
        if (persistentData == null) return null;

        if (saveable is IPlayerDependentSaveable enhancedSaveable)
        {
            return enhancedSaveable.ExtractFromUnifiedSave(persistentData);
        }

        Debug.LogError($"Component {saveable.SaveID} doesn't implement IPlayerDependentSaveable! Update it to use the modular interface.");
        return saveable.ExtractRelevantData(persistentData);
    }

    /// <summary>
    /// Creates default data for components using the modular interface.
    /// </summary>
    private object CreateDefaultDataForComponent(ISaveable saveable)
    {
        if (saveable is IPlayerDependentSaveable enhancedSaveable)
        {
            return enhancedSaveable.CreateDefaultData();
        }

        Debug.LogError($"Component {saveable.SaveID} doesn't implement IPlayerDependentSaveable! Update it to use the modular interface.");
        return null;
    }

    /// <summary>
    /// Builds unified save data by collecting from all player-dependent components.
    /// Used by SaveManager when creating save files.
    /// </summary>
    public PlayerPersistentData GetPersistentDataForSave()
    {
        UpdatePersistentPlayerDataForTransition();

        var saveData = new PlayerPersistentData();

        // Let each component contribute to the unified save structure
        foreach (var saveable in playerDependentSaveables)
        {
            DebugLog($"Getting save data for {saveable.SaveID}");
            try
            {
                var data = saveable.GetDataToSave();
                if (data != null)
                {
                    if (saveable.SaveID == "Player_Main" && data is PlayerSaveData playerData)
                    {
                        DebugLog($"🔍 PlayerSaveComponent data - Movement: {playerData.savedMovementMode}, " +
                                $"State: {playerData.savedPlayerState}, InVehicle check needed");
                    }

                    ContributeToSaveDataModular(saveable, data, saveData);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to get save data from {saveable.SaveID}: {e.Message}");
            }
        }

        return saveData;
    }

    /// <summary>
    /// Allows save components to contribute to the unified save structure using modular interface.
    /// </summary>
    private void ContributeToSaveDataModular(ISaveable saveable, object data, PlayerPersistentData saveData)
    {
        if (saveable is IPlayerDependentSaveable enhancedSaveable)
        {
            enhancedSaveable.ContributeToUnifiedSave(data, saveData);
            return;
        }

        Debug.LogError($"Component {saveable.SaveID} doesn't implement IPlayerDependentSaveable! Update it to use the modular interface.");
        saveData.SetComponentData(saveable.SaveID, data);
    }

    /// <summary>
    /// Clears temporary persistent data. Used when starting new games or after successful restoration.
    /// </summary>
    public void ClearPersistentData()
    {
        persistentPlayerData.Clear();
        hasPersistentData = false;
        DebugLog("Persistent player data cleared");
    }

    /// <summary>
    /// Returns whether persistent data is available for restoration.
    /// </summary>
    public bool HasPersistentData => hasPersistentData;

    /// <summary>
    /// Manually refreshes the list of discovered saveables. Call when components are added/removed at runtime.
    /// </summary>
    public void RefreshDiscoveredSaveables()
    {
        DiscoverPlayerDependentSaveables();
        DebugLog("Manually refreshed discovered saveables");
    }

    /// <summary>
    /// Manually registers a component for persistence. Useful for runtime-created components.
    /// </summary>
    public void RegisterComponent(ISaveable component)
    {
        if (component == null || component.SaveCategory != SaveDataCategory.PlayerDependent)
            return;

        if (!playerDependentSaveables.Contains(component))
        {
            playerDependentSaveables.Add(component);
            string componentType = component is IPlayerDependentSaveable ? "Enhanced" : "Legacy";
            DebugLog($"Manually registered component: {component.SaveID} ({component.GetType().Name}) [{componentType}]");
        }
    }

    /// <summary>
    /// Removes a component from persistence tracking. Useful for runtime-destroyed components.
    /// </summary>
    public void UnregisterComponent(ISaveable component)
    {
        if (component == null) return;

        if (playerDependentSaveables.Remove(component))
        {
            DebugLog($"Unregistered component: {component.SaveID}");
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PlayerPersistence] {message}");
        }
    }
}

