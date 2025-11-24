using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

/// <summary>
/// Central coordinator for saving and loading complete game state to/from files.
/// Orchestrates data collection from all persistence managers (PlayerPersistenceManager
/// and SceneDataManager) and handles file I/O using the ES3 save system.
/// 
/// The SaveManager operates by:
/// 1. Collecting current state from all persistence managers
/// 2. Packaging data into a unified GameSaveData structure
/// 3. Writing to disk with ES3 for saves, or reading and reconstructing for loads
/// 4. Delegating scene transitions and data restoration to SceneTransitionManager
/// 
/// For loading, it rebuilds the modular save structure that components expect,
/// ensuring compatibility with the component-based save system architecture.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveFileName = "GameSave";
    [SerializeField] private bool showDebugLogs = true;

    [ShowInInspector] private GameSaveData currentSaveData;

    // Events for UI and external systems
    public System.Action<bool> OnSaveComplete;
    public System.Action<bool> OnLoadComplete;

    [Header("Resources Data Paths")]
    public string itemDataPath = "Data/Items/";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Initiates a complete game save operation with loading screen coordination.
    /// Collects data from all persistence managers and writes to file.
    /// </summary>
    public void SaveGame()
    {
        StartCoroutine(SaveGameCoroutine());
    }

    /// <summary>
    /// Loads a saved game and transitions to the saved scene with complete state restoration.
    /// Delegates to SceneTransitionManager for coordinated loading experience.
    /// </summary>
    public void LoadGame()
    {
        StartCoroutine(LoadGameCoroutine());
    }

    /// <summary>
    /// Coroutine that handles the complete save process with loading screen coordination.
    /// SIMPLIFIED: Position is now handled directly in PlayerSaveData collection
    /// </summary>
    private System.Collections.IEnumerator SaveGameCoroutine()
    {
        DebugLog("Starting save operation...");

        // Immediately capture current scene data before any UI changes
        if (SceneDataManager.Instance != null)
        {
            DebugLog("Forcing immediate scene data save");
            SceneDataManager.Instance.SaveCurrentSceneData();

            // Verify data was captured
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var immediateCheck = SceneDataManager.Instance.GetSceneDataForSaving();
            if (immediateCheck.ContainsKey(currentScene))
            {
                DebugLog($"Scene data confirmed - {immediateCheck[currentScene].objectData.Count} objects");
            }
            else
            {
                DebugLog("WARNING - Scene data not found in container!");
            }
        }
        else
        {
            DebugLog("SceneDataManager not found - scene data will not be saved!");
        }

        // Start loading screen and begin timed save process
        LoadingScreenManager.Instance?.ShowLoadingScreen("Saving Game...", "Please wait");
        LoadingScreenManager.Instance?.SetProgress(0.1f);
        yield return new WaitForSecondsRealtime(0.1f);

        // Initialize save data structure
        currentSaveData = new GameSaveData();
        currentSaveData.saveTime = System.DateTime.Now;
        currentSaveData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        LoadingScreenManager.Instance?.SetProgress(0.3f);

        // Collect player data from PlayerPersistenceManager with climbing state logging
        if (PlayerPersistenceManager.Instance != null)
        {
            // Check current player state before collection
            var currentPlayerState = PlayerStateManager.Instance?.CurrentStateType ?? PlayerStateType.Ground;
            var currentMovementMode = FindFirstObjectByType<PlayerController>()?.CurrentMovementMode ?? MovementMode.Ground;

            DebugLog($"PRE-SAVE PLAYER STATE CHECK:");
            DebugLog($"  Current State: {currentPlayerState}");
            DebugLog($"  Current Movement Mode: {currentMovementMode}");

            if (currentPlayerState == PlayerStateType.Climbing)
            {
                DebugLog($"  🧗 CLIMBING STATE DETECTED - Will be converted to ground state at ledge position");

                // Log climbing details if available
                var playerController = FindFirstObjectByType<PlayerController>();
                if (playerController?.climbingMovementController != null)
                {
                    DebugLog($"  Climbing Info: {playerController.climbingMovementController.GetClimbSaveInfo()}");
                }
            }

            currentSaveData.playerPersistentData = PlayerPersistenceManager.Instance.GetPersistentDataForSave();

            // SIMPLIFIED: Position is now handled directly in PlayerSaveData - no separate class needed
            currentSaveData.SetPlayerSaveDataToPlayerPersistentData();

            // Log what was actually saved
            DebugLog($"POST-SAVE PLAYER STATE CHECK:");
            DebugLog($"  Saved State: {currentSaveData.playersaveData?.savedPlayerState ?? PlayerStateType.Ground}");
            DebugLog($"  Saved Movement Mode: {currentSaveData.playersaveData?.savedMovementMode ?? MovementMode.Ground}");
            DebugLog($"  Saved Position: {currentSaveData.playersaveData?.position ?? Vector3.zero}");

            if (currentPlayerState == PlayerStateType.Climbing &&
                currentSaveData.playersaveData?.savedPlayerState == PlayerStateType.Ground)
            {
                DebugLog($"  ✅ CLIMBING STATE SUCCESSFULLY CONVERTED TO GROUND STATE FOR SAVE");
            }

            // Log save data for debugging
            DebugLog($"Saving player data: Health={currentSaveData.playerPersistentData.currentHealth}");
            DebugLog($"Player persistent components: {currentSaveData.playerPersistentData.ComponentDataCount} entries");
            DebugLog($"Player save custom data: {currentSaveData.playersaveData?.CustomDataCount ?? 0} entries");
        }

        LoadingScreenManager.Instance?.SetProgress(0.5f);

        // Collect scene data (already captured above)
        if (SceneDataManager.Instance != null)
        {
            currentSaveData.sceneData = SceneDataManager.Instance.GetSceneDataForSaving();
            DebugLog($"Final scene data collection: {currentSaveData.sceneData.Count} scenes");
        }

        LoadingScreenManager.Instance?.SetProgress(0.7f);
        LoadingScreenManager.Instance?.SetProgress(0.9f);

        // Write to file using ES3
        bool saveSuccess = false;
        try
        {
            ES3.Save("gameData", currentSaveData, saveFileName + ".es3");
            saveSuccess = true;
            DebugLog("Game saved successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save failed: {e.Message}");
            saveSuccess = false;
        }

        LoadingScreenManager.Instance?.SetProgress(1f);
        yield return new WaitForSecondsRealtime(0.3f);

        OnSaveComplete?.Invoke(saveSuccess);
        LoadingScreenManager.Instance?.HideLoadingScreen();
    }

    /// <summary>
    /// Coroutine that handles the complete load process. Reads save data from file,
    /// reconstructs the modular save structure that persistence managers expect,
    /// and delegates to SceneTransitionManager for coordinated restoration.
    /// SIMPLIFIED: No longer needs to handle separate PlayerPositionData
    /// </summary>
    private System.Collections.IEnumerator LoadGameCoroutine()
    {
        DebugLog("Starting load operation...");

        if (!LoadSaveDataFromFile())
        {
            OnLoadComplete?.Invoke(false);
            yield break;
        }

        // Rebuild the modular save structure for SceneTransitionManager
        var saveDataForTransition = PrepareSaveDataForTransition();

        // Get target scene from save data
        string targetScene = currentSaveData.currentScene;
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        DebugLog($"Load target: {targetScene}, current: {currentScene}");

        // Delegate complete loading process to SceneTransitionManager
        if (SceneTransitionManager.Instance != null)
        {
            // SceneTransitionManager handles loading screen, scene transition, and data restoration
            SceneTransitionManager.Instance.LoadSceneFromSave(targetScene, saveDataForTransition);

            // Wait for transition to complete
            yield return new WaitUntil(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == targetScene);

            // Allow time for data restoration to complete
            yield return new WaitForSecondsRealtime(0.5f);
        }
        else
        {
            Debug.LogError("SceneTransitionManager not found - cannot load game!");
            OnLoadComplete?.Invoke(false);
            yield break;
        }

        DebugLog("Game load operation completed");
        OnLoadComplete?.Invoke(true);
    }

    /// <summary>
    /// Reconstructs the modular save data structure that SceneTransitionManager and
    /// persistence managers expect. 
    /// SIMPLIFIED: No longer needs to handle separate PlayerPositionData
    /// </summary>
    private Dictionary<string, object> PrepareSaveDataForTransition()
    {
        var transitionData = new Dictionary<string, object>();

        // Rebuild PlayerPersistentData from the flattened save structure
        if (currentSaveData.playersaveData != null)
        {
            DebugLog("Rebuilding PlayerPersistentData from loaded save data...");

            // Create new PlayerPersistentData with basic stats
            var rebuiltPersistentData = new PlayerPersistentData();
            rebuiltPersistentData.currentHealth = currentSaveData.playersaveData.currentHealth;
            rebuiltPersistentData.canJump = currentSaveData.playersaveData.canJump;
            rebuiltPersistentData.canSprint = currentSaveData.playersaveData.canSprint;
            rebuiltPersistentData.canCrouch = currentSaveData.playersaveData.canCrouch;

            // Rebuild component data from PlayerSaveData.customStats to PlayerPersistentData.componentData
            foreach (string componentKey in currentSaveData.playersaveData.GetCustomDataKeys())
            {
                var componentData = currentSaveData.playersaveData.GetCustomData<object>(componentKey);
                if (componentData != null)
                {
                    rebuiltPersistentData.SetComponentData(componentKey, componentData);
                    DebugLog($"Rebuilt component data for {componentKey}: {componentData.GetType().Name}");

                    // Log specific details for key component types
                    if (componentData is InventorySaveData invData)
                    {
                        DebugLog($"  Inventory: {invData.ItemCount} items in {invData.gridWidth}x{invData.gridHeight} grid");
                    }
                    else if (componentData is EquipmentSaveData eqData)
                    {
                        var assignedCount = eqData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                        DebugLog($"  Equipment: {assignedCount} hotkey assignments, equipped: {eqData.equippedItem?.isEquipped == true}");
                    }
                }
            }

            DebugLog($"Rebuilt PlayerPersistentData with {rebuiltPersistentData.ComponentDataCount} component entries");
            transitionData["playerPersistentData"] = rebuiltPersistentData;
        }

        // Add direct PlayerSaveData access (contains position information)
        if (currentSaveData.playersaveData != null)
        {
            transitionData["playerSaveData"] = currentSaveData.playersaveData;
            DebugLog($"Added PlayerSaveData with position: {currentSaveData.playersaveData.position} and {currentSaveData.playersaveData.CustomDataCount} custom data entries");
        }

        // Add scene data for environment restoration
        if (currentSaveData.sceneData != null)
        {
            transitionData["sceneData"] = currentSaveData.sceneData;
            DebugLog($"Added scene data for {currentSaveData.sceneData.Count} scenes");
        }

        // Debug log the final structure for verification
        DebugLog("=== FINAL TRANSITION DATA STRUCTURE ===");
        foreach (var kvp in transitionData)
        {
            DebugLog($"  {kvp.Key}: {kvp.Value?.GetType().Name ?? "null"}");

            if (kvp.Value is PlayerPersistentData ppd)
            {
                DebugLog($"    PlayerPersistentData contains {ppd.ComponentDataCount} components:");
                foreach (string componentId in ppd.GetStoredComponentIDs())
                {
                    var compData = ppd.GetComponentData<object>(componentId);
                    DebugLog($"      {componentId}: {compData?.GetType().Name ?? "null"}");
                }
            }
            else if (kvp.Value is PlayerSaveData psd)
            {
                DebugLog($"    PlayerSaveData - Position: {psd.position}, State: {psd.savedPlayerState}, Mode: {psd.savedMovementMode}");
            }
        }

        DebugLog($"Prepared transition data with {transitionData.Count} data categories");
        return transitionData;
    }

    /// <summary>
    /// Loads save data from the ES3 file and stores it in currentSaveData.
    /// Performs basic validation and logs loaded content for debugging.
    /// </summary>
    private bool LoadSaveDataFromFile()
    {
        if (!ES3.FileExists(saveFileName + ".es3"))
        {
            DebugLog("No save file found");
            return false;
        }

        try
        {
            currentSaveData = ES3.Load<GameSaveData>("gameData", saveFileName + ".es3");

            // Log what was successfully loaded
            DebugLog($"Save file loaded successfully - Scene: {currentSaveData.currentScene}");
            DebugLog($"Save time: {currentSaveData.saveTime}");
            DebugLog($"Player health: {currentSaveData.playersaveData?.currentHealth ?? 0}");
            DebugLog($"Player position: {currentSaveData.playersaveData?.position ?? Vector3.zero}");
            DebugLog($"Scene data: {currentSaveData.sceneData?.Count ?? 0} scenes");

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Load failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if a save file exists for the current save slot.
    /// Used by UI systems to enable/disable load buttons.
    /// </summary>
    public bool SaveExists()
    {
        return ES3.FileExists(saveFileName + ".es3");
    }

    /// <summary>
    /// Extracts basic save file information without loading the complete save data.
    /// SIMPLIFIED: Gets position directly from PlayerSaveData
    /// </summary>
    public SaveFileInfo GetSaveFileInfo()
    {
        if (!SaveExists()) return null;

        try
        {
            // Load only the metadata needed for UI display
            var saveTime = ES3.Load<System.DateTime>("gameData.saveTime", saveFileName + ".es3");
            var sceneName = ES3.Load<string>("gameData.currentScene", saveFileName + ".es3");

            // Try to get player level if it exists in the save
            int playerLevel = 1;
            if (ES3.KeyExists("gameData.playersaveData.level", saveFileName + ".es3"))
            {
                playerLevel = ES3.Load<int>("gameData.playersaveData.level", saveFileName + ".es3");
            }

            return new SaveFileInfo
            {
                saveTime = saveTime,
                sceneName = sceneName,
                playerLevel = playerLevel,
                playTime = 0f // Could be implemented by tracking total play time
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load save file info: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Deletes the current save file from disk. Used by UI systems for
    /// delete save functionality. Returns success status.
    /// </summary>
    public bool DeleteSaveFile()
    {
        try
        {
            if (ES3.FileExists(saveFileName + ".es3"))
            {
                ES3.DeleteFile(saveFileName + ".es3");
                DebugLog("Save file deleted successfully");
                return true;
            }
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to delete save file: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates a backup copy of the current save file. Useful for
    /// preventing save corruption or implementing multiple save slots.
    /// </summary>
    public bool BackupSaveFile()
    {
        try
        {
            if (ES3.FileExists(saveFileName + ".es3"))
            {
                string backupName = saveFileName + "_backup.es3";
                ES3.CopyFile(saveFileName + ".es3", backupName);
                DebugLog("Save file backed up successfully");
                return true;
            }
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to backup save file: {e.Message}");
            return false;
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] {message}");
        }
    }
}