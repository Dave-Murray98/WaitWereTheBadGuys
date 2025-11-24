using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// FIXED: Now properly applies loaded NPC states to NPCs in the scene
/// The key issue was that LoadSaveDataWithContext stored the data but never applied it
/// </summary>
public class SceneNPCStateManager : MonoBehaviour, ISaveable
{
    public static SceneNPCStateManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveID = "SceneNPCStateManager";

    [Header("Restoration Settings")]
    [SerializeField] private float restorationDelay = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // Core state data
    private Dictionary<string, NPCSaveData> npcStates = new Dictionary<string, NPCSaveData>();

    // Scene NPCs
    [ShowInInspector] private List<NPCSaveComponent> sceneNPCs = new List<NPCSaveComponent>();

    // Track current restoration context
    private RestoreContext currentContext = RestoreContext.NewGame;
    private bool hasPendingRestoration = false;

    // ISaveable implementation
    public string SaveID => saveID;
    public SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DebugLog("SceneNPCStateManager created");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StartCoroutine(InitializeAfterSceneLoad());
    }

    #region Initialization

    /// <summary>
    /// Initialize after scene has fully loaded and settled
    /// </summary>
    private IEnumerator InitializeAfterSceneLoad()
    {
        // Wait for scene to settle
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        DiscoverAndSetupNPCs();
        DebugLog($"Initialization complete - {sceneNPCs.Count} NPCs discovered");

        // If we have pending restoration from LoadSaveDataWithContext, apply it now
        if (hasPendingRestoration)
        {
            DebugLog("Applying pending restoration from save load");
            yield return StartCoroutine(ApplyLoadedStatesToScene());
            hasPendingRestoration = false;
        }
    }

    /// <summary>
    /// Discovers all NPCs in the scene and assigns consistent IDs
    /// Uses the SAME ID system as vehicles for consistency
    /// </summary>
    private void DiscoverAndSetupNPCs()
    {
        DebugLog("=== DISCOVERING AND SETTING UP NPCs ===");

        // Find all NPCSaveComponents in scene
        var npcComponents = FindObjectsByType<NPCSaveComponent>(FindObjectsSortMode.None).ToList();

        // Assign consistent IDs using the same system as vehicles
        AssignConsistentNPCIDs(npcComponents);
        sceneNPCs = npcComponents;

        DebugLog($"Discovered {sceneNPCs.Count} NPCs:");
        foreach (var npc in sceneNPCs)
        {
            DebugLog($"  - {npc.GetNPCID()} at {npc.transform.position}");
        }
    }

    /// <summary>
    /// Assign consistent IDs using Instance ID for deterministic ordering
    /// SAME SYSTEM AS VEHICLES - groups by name, orders by instance ID
    /// </summary>
    private void AssignConsistentNPCIDs(List<NPCSaveComponent> npcs)
    {
        // Group NPCs by GameObject name (removing spaces for clean IDs)
        var npcGroups = npcs
            .GroupBy(n => n.transform.gameObject.name.Replace(" ", "_"))
            .ToList();

        foreach (var group in npcGroups)
        {
            string baseName = group.Key;
            // Order by Instance ID for deterministic, consistent ordering across saves
            var npcsInGroup = group.OrderBy(n => n.GetInstanceID()).ToList();

            for (int i = 0; i < npcsInGroup.Count; i++)
            {
                var npc = npcsInGroup[i];
                string consistentID = $"{baseName}_{(i + 1):D2}";
                npc.SetNPCID(consistentID);
                DebugLog($"Assigned ID '{consistentID}' to NPC instance {npc.GetInstanceID()}");
            }
        }
    }

    #endregion

    #region ISaveable Implementation

    /// <summary>
    /// Collects all NPC states for saving
    /// </summary>
    public object GetDataToSave()
    {
        DebugLog("Collecting NPC states for save");

        // Ensure NPCs are discovered
        if (sceneNPCs.Count == 0)
        {
            DiscoverAndSetupNPCs();
        }

        // Collect current NPC states
        npcStates.Clear();
        foreach (NPCSaveComponent npc in sceneNPCs)
        {
            var npcSaveData = npc.GetDataToSave() as NPCSaveData;
            if (npcSaveData != null)
            {
                npcStates[npc.GetNPCID()] = npcSaveData;
            }
        }

        var saveData = new SceneNPCStateSaveData
        {
            npcStates = npcStates.Values.ToList()
        };

        DebugLog($"Saved NPC data - {npcStates.Count} NPCs");
        return saveData;
    }

    /// <summary>
    /// Extracts relevant data from save container
    /// </summary>
    public object ExtractRelevantData(object saveContainer)
    {
        return saveContainer;
    }

    /// <summary>
    /// FIXED: Now properly stores context and schedules restoration
    /// </summary>
    public void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        DebugLog($"LoadSaveDataWithContext called (Context: {context})");

        if (data is not SceneNPCStateSaveData saveData)
        {
            DebugLog($"Invalid data type - expected SceneNPCStateSaveData, got {data?.GetType()}");
            return;
        }

        // Store the context
        currentContext = context;

        // Convert list to dictionary for easy lookup
        npcStates = saveData.npcStates?.ToDictionary(n => n.npcID, n => n)
                    ?? new Dictionary<string, NPCSaveData>();

        // Context-aware handling
        switch (context)
        {
            case RestoreContext.SaveFileLoad:
            case RestoreContext.DoorwayTransition:
                DebugLog($"Will restore {npcStates.Count} NPC states");
                // Mark that we have pending restoration
                hasPendingRestoration = true;
                break;

            case RestoreContext.NewGame:
                npcStates.Clear();
                hasPendingRestoration = false;
                DebugLog("New game - clearing all NPC states");
                break;
        }

        DebugLog($"Loaded state: {npcStates.Count} NPCs");
    }

    /// <summary>
    /// Called before save operations
    /// </summary>
    public void OnBeforeSave()
    {
        DebugLog("OnBeforeSave called - preparing NPC data");
    }

    /// <summary>
    /// FIXED: Now triggers restoration if NPCs are already discovered
    /// </summary>
    public void OnAfterLoad()
    {
        DebugLog("OnAfterLoad called");

        // If NPCs haven't been discovered yet, discover them now
        if (sceneNPCs.Count == 0)
        {
            DebugLog("NPCs not discovered yet, discovering now");
            DiscoverAndSetupNPCs();
        }

        // If we have pending restoration and NPCs are ready, apply it
        if (hasPendingRestoration && sceneNPCs.Count > 0)
        {
            DebugLog("Applying restoration from OnAfterLoad");
            StartCoroutine(ApplyLoadedStatesToScene());
            hasPendingRestoration = false;
        }
    }

    #endregion

    #region State Application - FIXED

    /// <summary>
    /// FIXED: Now actually applies the loaded NPC states to the scene NPCs
    /// This is what was missing before!
    /// </summary>
    private IEnumerator ApplyLoadedStatesToScene()
    {
        DebugLog($"=== STARTING NPC RESTORATION PROCESS ===");
        DebugLog($"Context: {currentContext}, NPCs to restore: {npcStates.Count}");

        // Wait a moment for other systems to finish loading
        yield return new WaitForSeconds(restorationDelay);

        // Apply states to all NPCs
        int restoredCount = 0;
        foreach (var npc in sceneNPCs)
        {
            string npcID = npc.GetNPCID();

            if (npcStates.TryGetValue(npcID, out var saveData))
            {
                DebugLog($"Applying saved state to NPC: {npcID}");
                DebugLog($"  Saved Position: {saveData.position}, Current Position: {npc.transform.position}");

                // THIS IS THE KEY LINE THAT WAS MISSING!
                // Apply the saved data to the NPC
                npc.LoadSaveDataWithContext(saveData, currentContext);

                restoredCount++;
            }
            else
            {
                DebugLog($"No saved state found for NPC: {npcID} (using spawn position)");
            }
        }

        DebugLog($"✅ NPC RESTORATION COMPLETED - Applied states to {restoredCount}/{sceneNPCs.Count} NPCs");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Get all NPCs currently tracked by this manager
    /// </summary>
    public List<NPCSaveComponent> GetTrackedNPCs()
    {
        return new List<NPCSaveComponent>(sceneNPCs);
    }

    /// <summary>
    /// Get count of tracked NPCs
    /// </summary>
    public int GetNPCCount()
    {
        return sceneNPCs.Count;
    }

    /// <summary>
    /// Check if an NPC with the given ID exists
    /// </summary>
    public bool HasNPC(string npcID)
    {
        return sceneNPCs.Any(n => n.GetNPCID() == npcID);
    }

    /// <summary>
    /// Get NPC by ID
    /// </summary>
    public NPCSaveComponent GetNPCByID(string npcID)
    {
        return sceneNPCs.FirstOrDefault(n => n.GetNPCID() == npcID);
    }

    /// <summary>
    /// Manually refresh the list of NPCs (useful if NPCs spawn at runtime)
    /// </summary>
    [Button("Refresh NPC List")]
    public void RefreshNPCList()
    {
        DiscoverAndSetupNPCs();
        DebugLog("Manually refreshed NPC list");
    }

    #endregion

    #region Debug and Utility

    /// <summary>
    /// Get debug information about all tracked NPCs
    /// </summary>
    [Button("Show All NPC Info"), DisableInEditorMode]
    public void ShowAllNPCInfo()
    {
        Debug.Log("=== SCENE NPC STATE MANAGER DEBUG INFO ===");
        Debug.Log($"Total NPCs: {sceneNPCs.Count}");
        Debug.Log($"Saved States: {npcStates.Count}");
        Debug.Log($"Has Pending Restoration: {hasPendingRestoration}");
        Debug.Log($"Current Context: {currentContext}");
        Debug.Log("\nNPCs in Scene:");

        foreach (var npc in sceneNPCs)
        {
            Debug.Log($"  - {npc.GetNPCID()}: Position={npc.transform.position}, " +
                     $"Rotation={npc.transform.rotation.eulerAngles}");
        }

        if (npcStates.Count > 0)
        {
            Debug.Log("\nSaved States:");
            foreach (var state in npcStates.Values)
            {
                Debug.Log($"  - {state.npcID}: Position={state.position}, " +
                         $"Rotation={state.rotation.eulerAngles}");
            }
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneNPCStateManager] {message}");
        }
    }

    #endregion
}

/// <summary>
/// Save data structure for all NPCs in a scene
/// </summary>
[System.Serializable]
public class SceneNPCStateSaveData
{
    public List<NPCSaveData> npcStates = new List<NPCSaveData>();
}