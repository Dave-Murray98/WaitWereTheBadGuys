using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.VisualScripting;

[System.Serializable]
public class SceneDataContainer
{
    [ShowInInspector]
    public Dictionary<string, SceneSaveData> sceneData = new Dictionary<string, SceneSaveData>();

    public void SetSceneData(string sceneName, SceneSaveData data)
    {
        sceneData[sceneName] = data;

        // IMMEDIATE debug output
        //        Debug.Log($"[SceneDataContainer] SetSceneData called for '{sceneName}' with {data.objectData.Count} objects");

        // Log specific SceneItemStateManager data if present
        if (data.objectData.ContainsKey("SceneItemStateManager"))
        {
            DebugLog($"[SceneDataContainer] SceneItemStateManager data saved to container");
        }
        else
        {
            DebugLog($"[SceneDataContainer] WARNING: SceneItemStateManager data NOT found in scene data");
        }

    }

    public SceneSaveData GetSceneData(string sceneName)
    {
        if (sceneData.TryGetValue(sceneName, out SceneSaveData data))
        {
            // Debug.Log($"[SceneDataContainer] Retrieved scene data for '{sceneName}' with {data.objectData.Count} objects");
            return data;
        }

        Debug.Log($"[SceneDataContainer] No scene data found for '{sceneName}'");
        return null;
    }

    private void DebugLog(string message)
    {
        if (SceneDataManager.Instance.enableDebugLogs)
            Debug.Log($"[SceneDataContainer] {message}");
    }
}