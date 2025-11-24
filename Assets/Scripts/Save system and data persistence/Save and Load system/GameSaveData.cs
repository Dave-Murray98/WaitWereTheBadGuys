using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main game save data structure
/// CLEANED: Now fully modular - no hardcoded component field assignments
/// </summary>
[System.Serializable]
public class GameSaveData
{
    public System.DateTime saveTime;
    public string currentScene;

    public PlayerSaveData playersaveData;

    // Player data that persists between doorways
    public PlayerPersistentData playerPersistentData;

    // Scene data for all visited scenes
    public Dictionary<string, SceneSaveData> sceneData;

    /// <summary>
    /// SIMPLIFIED: Set PlayerSaveData from PlayerPersistentData using component system
    /// Now handles position directly without separate PlayerPositionData class
    /// </summary>
    public void SetPlayerSaveDataToPlayerPersistentData()
    {
        // Get the existing PlayerSaveData from persistent data instead of creating new one
        PlayerSaveData existingPlayerData = null;

        // Extract the complete PlayerSaveData that was saved with all the correct state info
        foreach (string componentID in playerPersistentData.GetStoredComponentIDs())
        {
            if (componentID == "Player_Main") // This should match PlayerSaveComponent.saveID
            {
                existingPlayerData = playerPersistentData.GetComponentData<PlayerSaveData>(componentID);
                break;
            }
        }

        if (existingPlayerData != null)
        {
            // Use the existing data that has all the correct state information
            playersaveData = existingPlayerData.CreateCopy();

            // Position and rotation are already in the PlayerSaveData - no need for separate class
            playersaveData.currentScene = currentScene;

            Debug.Log($"[GameSaveData] Used existing PlayerSaveData with correct state - " +
                     $"Movement: {playersaveData.savedMovementMode}, State: {playersaveData.savedPlayerState}, " +
                     $"Position: {playersaveData.position}");
        }
        else
        {
            // Fallback: Create new if existing data not found (shouldn't happen normally)
            Debug.LogWarning("[GameSaveData] No existing PlayerSaveData found - creating new (this may lose state info)");

            if (playersaveData == null)
                playersaveData = new PlayerSaveData();

            // Copy basic player data from persistent data
            playersaveData.currentHealth = playerPersistentData.currentHealth;
            playersaveData.canJump = playerPersistentData.canJump;
            playersaveData.canSprint = playerPersistentData.canSprint;
            playersaveData.canCrouch = playerPersistentData.canCrouch;
            playersaveData.currentScene = currentScene;

            // Position will be set by SaveManager directly in PlayerSaveData
        }

        // CRITICAL: Ensure all component data is copied to customStats
        foreach (string componentKey in playerPersistentData.GetStoredComponentIDs())
        {
            var componentData = playerPersistentData.GetComponentData<object>(componentKey);
            if (componentData != null)
            {
                // Store component data in customStats for proper restoration
                playersaveData.SetCustomData(componentKey, componentData);
                Debug.Log($"[GameSaveData] Copied component data: {componentKey} -> {componentData.GetType().Name}");
            }
        }

        Debug.Log($"[GameSaveData] Final PlayerSaveData - Health: {playersaveData.currentHealth}, " +
                 $"Movement: {playersaveData.savedMovementMode}, State: {playersaveData.savedPlayerState}, " +
                 $"Position: {playersaveData.position}, CustomData: {playersaveData.CustomDataCount}");
    }
}

/// <summary>
/// Statistics about save data for debugging and UI display
/// SIMPLIFIED: Removed PlayerPositionData references
/// </summary>
[System.Serializable]
public class SaveDataStats
{
    public DateTime SaveTime;
    public string CurrentScene;
    public bool HasPlayerData;
    public bool HasPersistentData;
    public float PlayerHealth;
    public int PlayerLevel;
    public Vector3 PlayerPosition;
    public int PersistentComponentCount;
    public int SceneCount;
    public int TotalSceneObjects;

    public override string ToString()
    {
        return $"Save: {CurrentScene} @ {SaveTime:yyyy-MM-dd HH:mm}, " +
               $"Health: {PlayerHealth}, Level: {PlayerLevel}, Position: {PlayerPosition}, " +
               $"Components: {PersistentComponentCount}, Scenes: {SceneCount}";
    }
}
