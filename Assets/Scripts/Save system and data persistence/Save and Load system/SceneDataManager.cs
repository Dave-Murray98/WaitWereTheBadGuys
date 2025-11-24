using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

/// <summary>
/// Manages persistence of scene-dependent data like enemy states, door locks, and pickups.
/// Saves scene state when leaving via doorways and restores it when returning.
/// Does not handle scene transitions directly - that's SceneTransitionManager's job.
/// </summary>
public class SceneDataManager : MonoBehaviour
{
    public static SceneDataManager Instance { get; private set; }

    [Header("Data Storage")]
    [ShowInInspector][SerializeField] private SceneDataContainer sceneDataContainer;
    public bool enableDebugLogs = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            sceneDataContainer = new SceneDataContainer();
            DebugLog("SceneDataManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called by SceneTransitionManager before scene transitions to save current scene state.
    /// Only saves data for doorway transitions - save files handle complete state separately.
    /// </summary>
    public void PrepareSceneTransition(string targetScene, string targetDoorway, RestoreContext restoreContext)
    {
        DebugLog($"Preparing transition to {targetScene} via {restoreContext}");

        if (restoreContext == RestoreContext.DoorwayTransition)
        {
            SaveCurrentSceneData();
        }
    }

    /// <summary>
    /// Restores scene data for doorway transitions. Called by SceneTransitionManager
    /// after the new scene loads to restore previous scene state.
    /// </summary>
    public void RestoreSceneDataForTransition(string sceneName)
    {
        DebugLog($"Restoring scene data for doorway transition: {sceneName}");
        RestoreSceneData(sceneName, RestoreContext.DoorwayTransition);
    }

    /// <summary>
    /// Restores scene data from save files. Replaces current scene data container
    /// with saved data and restores the current scene.
    /// </summary>
    public void RestoreSceneDataFromSave(Dictionary<string, object> saveData)
    {
        DebugLog("Loading scene data from save file");

        if (saveData != null && saveData.ContainsKey("sceneData"))
        {
            if (saveData["sceneData"] is Dictionary<string, SceneSaveData> sceneDataDict)
            {
                sceneDataContainer.sceneData = new Dictionary<string, SceneSaveData>(sceneDataDict);
                DebugLog($"Loaded {sceneDataDict.Count} scenes from save file");

                // Restore current scene data
                string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                RestoreSceneData(currentScene, RestoreContext.SaveFileLoad);
            }
            else
            {
                Debug.LogWarning("Scene data in save file is not the expected format");
            }
        }
        else
        {
            DebugLog("No scene data found in save file");
        }
    }

    /// <summary>
    /// Clears all stored scene data. Used when starting new games to ensure
    /// fresh scene states without any previous modifications.
    /// </summary>
    public void ClearAllSceneData()
    {
        sceneDataContainer.sceneData.Clear();
        DebugLog("All scene data cleared for new game");
    }

    /// <summary>
    /// Captures the current state of all scene-dependent objects in the active scene.
    /// Only saves objects marked as SceneDependent - player data is handled separately.
    /// </summary>
    public void SaveCurrentSceneData()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        DebugLog($"Saving scene data for: {currentScene}");

        var sceneData = new SceneSaveData();
        sceneData.sceneName = currentScene;
        sceneData.lastVisited = System.DateTime.Now;
        sceneData.hasBeenVisited = true;

        // Find and save all scene-dependent objects
        ISaveable[] saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveable>()
            .Where(s => s.SaveCategory == SaveDataCategory.SceneDependent)
            .ToArray();

        DebugLog($"Found {saveableObjects.Length} scene-dependent saveables to process");

        foreach (var saveable in saveableObjects)
        {
            try
            {
                saveable.OnBeforeSave();
                var data = saveable.GetDataToSave();
                if (data != null)
                {
                    sceneData.SetObjectData(saveable.SaveID, data);
                    DebugLog($"Saved data for: {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save {saveable.SaveID}: {e.Message}");
            }
        }

        sceneDataContainer.SetSceneData(currentScene, sceneData);
        DebugLog($"Saved {saveableObjects.Length} scene objects for: {currentScene}");
    }

    /// <summary>
    /// Restores scene data to all matching scene-dependent objects in the current scene.
    /// Uses context-aware loading to handle different restoration scenarios.
    /// </summary>
    private void RestoreSceneData(string sceneName, RestoreContext context)
    {
        var sceneData = sceneDataContainer.GetSceneData(sceneName);
        if (sceneData == null)
        {
            DebugLog($"No scene data for: {sceneName} (normal for first visit)");
            return;
        }

        DebugLog($"Restoring scene data for: {sceneName} ({sceneData.objectData.Count} objects) with context: {context}");

        // Find and restore all scene-dependent objects
        ISaveable[] saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveable>()
            .Where(s => s.SaveCategory == SaveDataCategory.SceneDependent)
            .ToArray();

        DebugLog($"Found {saveableObjects.Length} scene-dependent saveables to restore");

        int restoredCount = 0;
        foreach (var saveable in saveableObjects)
        {
            try
            {
                var data = sceneData.GetObjectData<object>(saveable.SaveID);
                if (data != null)
                {
                    saveable.LoadSaveDataWithContext(data, context);
                    saveable.OnAfterLoad();
                    restoredCount++;
                    DebugLog($"Restored scene object: {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore {saveable.SaveID}: {e.Message}");
            }
        }

        DebugLog($"Scene data restoration complete: {restoredCount}/{saveableObjects.Length} objects restored");
    }

    /// <summary>
    /// Returns a copy of all scene data for SaveManager to include in save files.
    /// Forces a save of the current scene to ensure latest state is captured.
    /// </summary>
    public Dictionary<string, SceneSaveData> GetSceneDataForSaving()
    {
        DebugLog("GetSceneDataForSaving called - forcing current scene save");
        SaveCurrentSceneData();

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneDataContainer.sceneData.ContainsKey(currentScene))
        {
            var sceneData = sceneDataContainer.sceneData[currentScene];
            DebugLog($"Current scene '{currentScene}' saved with {sceneData.objectData.Count} objects");
        }

        DebugLog($"Returning scene data for {sceneDataContainer.sceneData.Count} total scenes");
        return new Dictionary<string, SceneSaveData>(sceneDataContainer.sceneData);
    }

    /// <summary>
    /// Gets saved data for a specific scene. Returns null if no data exists.
    /// </summary>
    public SceneSaveData GetSceneData(string sceneName)
    {
        return sceneDataContainer.GetSceneData(sceneName);
    }

    /// <summary>
    /// Checks if saved data exists for the specified scene.
    /// </summary>
    public bool HasSceneData(string sceneName)
    {
        return sceneDataContainer.sceneData.ContainsKey(sceneName);
    }

    /// <summary>
    /// Returns list of all scene names that have saved data.
    /// </summary>
    public List<string> GetScenesWithData()
    {
        return new List<string>(sceneDataContainer.sceneData.Keys);
    }

    /// <summary>
    /// Removes saved data for a specific scene. Useful for testing or cleanup.
    /// </summary>
    public void RemoveSceneData(string sceneName)
    {
        if (sceneDataContainer.sceneData.ContainsKey(sceneName))
        {
            sceneDataContainer.sceneData.Remove(sceneName);
            DebugLog($"Removed scene data for: {sceneName}");
        }
    }

    /// <summary>
    /// Returns count of scenes with saved data.
    /// </summary>
    public int GetSceneDataCount()
    {
        return sceneDataContainer.sceneData.Count;
    }

    /// <summary>
    /// Returns memory usage summary for debugging.
    /// </summary>
    public string GetMemoryInfo()
    {
        int totalObjects = 0;
        foreach (var sceneData in sceneDataContainer.sceneData.Values)
        {
            totalObjects += sceneData.objectData.Count;
        }
        return $"Scenes: {sceneDataContainer.sceneData.Count}, Total Objects: {totalObjects}";
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneDataManager] {message}");
        }
    }
}