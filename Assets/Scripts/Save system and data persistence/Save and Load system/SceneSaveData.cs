using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Data for a specific scene
/// </summary>
[System.Serializable]
public class SceneSaveData
{
    [Header("Scene Info")]
    public string sceneName = "";
    public DateTime lastVisited = DateTime.Now;
    public bool hasBeenVisited = false;

    [Header("Scene Objects")]
    public Dictionary<string, object> objectData = new Dictionary<string, object>();

    [Header("Scene State")]
    public Dictionary<string, bool> sceneFlags = new Dictionary<string, bool>();
    public Dictionary<string, int> sceneCounters = new Dictionary<string, int>();

    // Helper methods
    public void SetObjectData(string objectID, object data) => objectData[objectID] = data;
    public T GetObjectData<T>(string objectID) where T : class
    {
        return objectData.ContainsKey(objectID) ? objectData[objectID] as T : null;
    }

    public void SetFlag(string flagName, bool value) => sceneFlags[flagName] = value;
    public bool GetFlag(string flagName) => sceneFlags.ContainsKey(flagName) ? sceneFlags[flagName] : false;

    public void SetCounter(string counterName, int value) => sceneCounters[counterName] = value;
    public int GetCounter(string counterName) => sceneCounters.ContainsKey(counterName) ? sceneCounters[counterName] : 0;
}