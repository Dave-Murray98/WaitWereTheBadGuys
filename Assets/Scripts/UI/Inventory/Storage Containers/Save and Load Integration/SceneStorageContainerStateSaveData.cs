using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Complete save data structure for all storage containers in a scene
/// </summary>
[System.Serializable]
public class SceneStorageContainerStateSaveData
{
    public List<StorageContainerSaveData> containerStates = new List<StorageContainerSaveData>();
    public string sceneName;
    public System.DateTime lastUpdated;
}