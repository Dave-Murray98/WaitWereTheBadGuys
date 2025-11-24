using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player-specific save data now includes PlayerStateType alongside MovementMode
/// This allows for more precise state restoration when loading saves or transitioning scenes
/// </summary>
[System.Serializable]
public class PlayerSaveData
{
    [Header("Transform")]
    public Vector3 position = Vector3.zero;
    public Vector3 rotation = Vector3.zero;
    public string currentScene = "";

    [Header("Stats")]
    public float currentHealth = 100f;
    public float maxHealth = 100f;
    public int level = 1;
    public float experience = 0f;

    [Header("Settings")]
    public float lookSensitivity = 2f;
    public float masterVolume = 1f;
    public float sfxVolume = 1f;
    public float musicVolume = 1f;

    [Header("Abilities")]
    public bool canJump = true;
    public bool canSprint = true;
    public bool canCrouch = true;

    [Header("Movement State")]
    public MovementMode savedMovementMode = MovementMode.Ground;
    [Header("Player State")]
    public PlayerStateType savedPlayerState = PlayerStateType.Ground;
    public bool wasInWater = false;

    /// <summary>
    /// Dictionary to store the data of each player dependent data component (inventory, equipment, etc.) 
    /// that is used in the game for saving, loading and persisting player data
    /// </summary>
    [Header("Dynamic Component Data")]
    public Dictionary<string, object> customStats = new Dictionary<string, object>();

    public PlayerSaveData()
    {
        customStats = new Dictionary<string, object>();
    }

    #region Dynamic Data Management

    /// <summary>
    /// Get custom stat/component data by key with type safety
    /// </summary>
    /// <typeparam name="T">Expected data type</typeparam>
    /// <param name="key">Data key</param>
    /// <returns>Data or null if not found</returns>
    public T GetCustomData<T>(string key) where T : class
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (customStats.TryGetValue(key, out object data))
        {
            return data as T;
        }

        return null;
    }

    /// <summary>
    /// Set custom stat/component data by key
    /// </summary>
    /// <param name="key">Data key</param>
    /// <param name="data">Data to store</param>
    public void SetCustomData(string key, object data)
    {
        if (string.IsNullOrEmpty(key))
            return;

        customStats[key] = data;
    }

    /// <summary>
    /// Check if custom data exists for a key
    /// </summary>
    /// <param name="key">Data key</param>
    /// <returns>True if data exists</returns>
    public bool HasCustomData(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        return customStats.ContainsKey(key);
    }

    /// <summary>
    /// Remove custom data by key
    /// </summary>
    /// <param name="key">Data key</param>
    /// <returns>True if data was removed</returns>
    public bool RemoveCustomData(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        return customStats.Remove(key);
    }

    /// <summary>
    /// Get all stored custom data keys
    /// </summary>
    /// <returns>Collection of keys that have data</returns>
    public IEnumerable<string> GetCustomDataKeys()
    {
        return customStats.Keys;
    }

    /// <summary>
    /// Clear all custom data
    /// </summary>
    public void ClearAllCustomData()
    {
        customStats.Clear();
    }

    /// <summary>
    /// Get count of stored custom data entries
    /// </summary>
    public int CustomDataCount => customStats.Count;

    #endregion

    #region Movement State Utilities

    /// <summary>
    /// Sets movement context information for save operations
    /// </summary>
    public void SetMovementContext(MovementMode mode, PlayerStateType playerState, bool inWater)
    {
        savedMovementMode = mode;
        savedPlayerState = playerState;
        wasInWater = inWater;
    }

    /// <summary>
    /// Gets whether the saved state indicates the player should be in a water environment
    /// </summary>
    public bool ShouldBeInWater()
    {
        return wasInWater || savedMovementMode == MovementMode.Swimming || savedPlayerState == PlayerStateType.Water;
    }

    /// <summary>
    /// Validates that movement state is consistent with environment
    /// </summary>
    public bool IsMovementStateConsistent()
    {
        // Check if movement mode and player state are consistent
        bool modeStateConsistent = (savedMovementMode, savedPlayerState) switch
        {
            (MovementMode.Ground, PlayerStateType.Ground) => true,
            (MovementMode.Swimming, PlayerStateType.Water) => true,
            (MovementMode.Vehicle, PlayerStateType.Vehicle) => true,
            _ => false
        };

        if (!modeStateConsistent)
            return false;

        // If player was in water, they should be in swimming mode and water state
        if (wasInWater && (savedMovementMode != MovementMode.Swimming || savedPlayerState != PlayerStateType.Water))
            return false;

        // If player is in swimming mode or water state, they should have been in water
        if ((savedMovementMode == MovementMode.Swimming || savedPlayerState == PlayerStateType.Water) && !wasInWater)
            return false;

        return true;
    }

    /// <summary>
    /// Returns debug information about movement state
    /// </summary>
    public string GetMovementDebugInfo()
    {
        return $"Movement: {savedMovementMode} | PlayerState: {savedPlayerState} | InWater: {wasInWater} | Position: {position}";
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Merge custom data from another PlayerSaveData instance
    /// </summary>
    /// <param name="other">Other save data to merge from</param>
    /// <param name="overwriteExisting">Whether to overwrite existing entries</param>
    public void MergeCustomDataFrom(PlayerSaveData other, bool overwriteExisting = true)
    {
        if (other?.customStats == null) return;

        foreach (var kvp in other.customStats)
        {
            if (overwriteExisting || !customStats.ContainsKey(kvp.Key))
            {
                customStats[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Creates a copy of this PlayerSaveData with all data intact
    /// </summary>
    public PlayerSaveData CreateCopy()
    {
        var copy = new PlayerSaveData
        {
            position = position,
            rotation = rotation,
            currentScene = currentScene,
            currentHealth = currentHealth,
            maxHealth = maxHealth,
            level = level,
            experience = experience,
            lookSensitivity = lookSensitivity,
            masterVolume = masterVolume,
            sfxVolume = sfxVolume,
            musicVolume = musicVolume,
            canJump = canJump,
            canSprint = canSprint,
            canCrouch = canCrouch,
            savedMovementMode = savedMovementMode,
            savedPlayerState = savedPlayerState, // Copy new field
            wasInWater = wasInWater
        };

        // Deep copy custom stats
        foreach (var kvp in customStats)
        {
            copy.customStats[kvp.Key] = kvp.Value;
        }

        return copy;
    }

    #endregion
}