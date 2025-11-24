using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unified container for player data that persists across scene transitions.
/// Uses dynamic component storage to support modular save system architecture.
/// Contains basic player stats in direct fields and component-specific data in dictionary.
/// </summary>
[System.Serializable]
public class PlayerPersistentData
{
    [Header("Basic Player Stats")]
    public float currentHealth = 100f;
    public bool canJump = true;
    public bool canSprint = true;
    public bool canCrouch = true;

    [Header("Dynamic Component Data")]
    [SerializeField] private Dictionary<string, object> componentData = new Dictionary<string, object>();

    public PlayerPersistentData()
    {
        componentData = new Dictionary<string, object>();
    }

    /// <summary>
    /// Copy constructor for creating independent copies during scene transitions.
    /// </summary>
    public PlayerPersistentData(PlayerPersistentData other)
    {
        // Copy basic player data
        currentHealth = other.currentHealth;
        canJump = other.canJump;
        canSprint = other.canSprint;
        canCrouch = other.canCrouch;

        // Deep copy component data dictionary
        componentData = new Dictionary<string, object>();
        if (other.componentData != null)
        {
            foreach (var kvp in other.componentData)
            {
                componentData[kvp.Key] = kvp.Value;
            }
        }

        Debug.Log($"[PlayerPersistentData] Copy constructor: Copied {componentData.Count} component data entries");
    }

    #region Dynamic Component Data Management

    /// <summary>
    /// Retrieves component data with type safety.
    /// </summary>
    /// <typeparam name="T">Expected data type</typeparam>
    /// <param name="saveID">Component's save ID</param>
    /// <returns>Component data or null if not found</returns>
    public T GetComponentData<T>(string saveID) where T : class
    {
        if (string.IsNullOrEmpty(saveID))
            return null;

        if (componentData.TryGetValue(saveID, out object data))
        {
            return data as T;
        }

        return null;
    }

    /// <summary>
    /// Stores component data by save ID.
    /// </summary>
    /// <param name="saveID">Component's save ID</param>
    /// <param name="data">Data to store</param>
    public void SetComponentData(string saveID, object data)
    {
        if (string.IsNullOrEmpty(saveID))
            return;

        componentData[saveID] = data;
    }

    /// <summary>
    /// Checks if component data exists for a save ID.
    /// </summary>
    public bool HasComponentData(string saveID)
    {
        if (string.IsNullOrEmpty(saveID))
            return false;

        return componentData.ContainsKey(saveID);
    }

    /// <summary>
    /// Removes component data by save ID.
    /// </summary>
    public bool RemoveComponentData(string saveID)
    {
        if (string.IsNullOrEmpty(saveID))
            return false;

        return componentData.Remove(saveID);
    }

    /// <summary>
    /// Gets all stored component save IDs.
    /// </summary>
    public IEnumerable<string> GetStoredComponentIDs()
    {
        return componentData.Keys;
    }

    /// <summary>
    /// Clears all component data.
    /// </summary>
    public void ClearAllComponentData()
    {
        componentData.Clear();
    }

    /// <summary>
    /// Gets count of stored component data entries.
    /// </summary>
    public int ComponentDataCount => componentData.Count;

    #endregion

    #region Validation and Debugging

    /// <summary>
    /// Returns detailed debug information about stored data.
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== PlayerPersistentData Debug Info ===");
        info.AppendLine($"Health: {currentHealth}");
        info.AppendLine($"Abilities: Jump={canJump}, Sprint={canSprint}, Crouch={canCrouch}");
        info.AppendLine($"Component Data: {componentData.Count} entries");

        foreach (var kvp in componentData)
        {
            string dataInfo = "null";
            if (kvp.Value != null)
            {
                dataInfo = kvp.Value.GetType().Name;

                // Add specific info for known types
                if (kvp.Value is InventorySaveData invData)
                {
                    dataInfo += $" ({invData.ItemCount} items)";
                }
                else if (kvp.Value is EquipmentSaveData eqData)
                {
                    var assignedCount = eqData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                    dataInfo += $" ({assignedCount} hotkeys assigned)";
                }
            }
            info.AppendLine($"  - {kvp.Key}: {dataInfo}");
        }

        return info.ToString();
    }

    /// <summary>
    /// Validates data integrity and consistency.
    /// </summary>
    public bool IsValid()
    {
        if (currentHealth < 0)
            return false;

        if (componentData == null)
            return false;

        // Validate specific component data types
        foreach (var kvp in componentData)
        {
            if (string.IsNullOrEmpty(kvp.Key))
                return false;

            // Validate known data types
            if (kvp.Value is InventorySaveData invData && !invData.IsValid())
                return false;

            if (kvp.Value is EquipmentSaveData eqData && !eqData.IsValid())
                return false;
        }

        return true;
    }

    /// <summary>
    /// Merges data from another PlayerPersistentData instance.
    /// Useful for combining data from different sources.
    /// </summary>
    /// <param name="other">Other data to merge</param>
    /// <param name="overwriteExisting">Whether to overwrite existing entries</param>
    public void MergeFrom(PlayerPersistentData other, bool overwriteExisting = true)
    {
        if (other == null) return;

        // Merge basic stats (always overwrite)
        currentHealth = other.currentHealth;
        canJump = other.canJump;
        canSprint = other.canSprint;
        canCrouch = other.canCrouch;

        // Merge component data
        foreach (var kvp in other.componentData)
        {
            if (overwriteExisting || !componentData.ContainsKey(kvp.Key))
            {
                componentData[kvp.Key] = kvp.Value;
            }
        }

        Debug.Log($"[PlayerPersistentData] Merged data from other instance: {other.componentData.Count} entries");
    }

    #endregion
}