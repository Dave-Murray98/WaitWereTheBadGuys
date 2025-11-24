using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Game progress and flags
/// </summary>
[System.Serializable]
public class GameProgressData
{
    [Header("Progress")]
    public float completionPercentage = 0f;
    public int checkpointsReached = 0;
    public string lastCheckpoint = "";

    [Header("Flags")]
    public Dictionary<string, bool> gameFlags = new Dictionary<string, bool>();
    public Dictionary<string, int> gameCounters = new Dictionary<string, int>();
    public Dictionary<string, string> gameStrings = new Dictionary<string, string>();

    [Header("Achievements")]
    public List<string> unlockedAchievements = new List<string>();

    // Helper methods for easy flag management
    public void SetFlag(string flagName, bool value) => gameFlags[flagName] = value;
    public bool GetFlag(string flagName) => gameFlags.ContainsKey(flagName) ? gameFlags[flagName] : false;

    public void SetCounter(string counterName, int value) => gameCounters[counterName] = value;
    public int GetCounter(string counterName) => gameCounters.ContainsKey(counterName) ? gameCounters[counterName] : 0;

    public void SetString(string stringName, string value) => gameStrings[stringName] = value;
    public string GetString(string stringName) => gameStrings.ContainsKey(stringName) ? gameStrings[stringName] : "";
}
