using UnityEngine;

/// <summary>
/// Save data structure for NPCs.
/// Currently stores only position and rotation for simplicity.
/// Designed to be easily extensible for future data needs (health, AI state, etc.)
/// </summary>
[System.Serializable]
public class NPCSaveData
{
    [Header("NPC Identity")]
    public string npcID;

    [Header("Transform Data")]
    public Vector3 position;
    public Quaternion rotation;

    [Header("Timestamps")]
    public string saveTimestamp;

    #region Constructors

    public NPCSaveData()
    {
        saveTimestamp = System.DateTime.Now.ToString();
    }

    public NPCSaveData(string id, Vector3 pos, Quaternion rot)
    {
        npcID = id;
        position = pos;
        rotation = rot;
        saveTimestamp = System.DateTime.Now.ToString();
    }

    #endregion

    #region State Management

    /// <summary>
    /// Update save data from a Transform component
    /// </summary>
    public void UpdateFromTransform(Transform transform, string id)
    {
        if (transform == null) return;

        npcID = id;
        position = transform.position;
        rotation = transform.rotation;
        saveTimestamp = System.DateTime.Now.ToString();
    }

    /// <summary>
    /// Apply this save data to a Transform component
    /// </summary>
    public void ApplyToTransform(Transform transform)
    {
        if (transform == null) return;

        transform.position = position;
        transform.rotation = rotation;
    }

    #endregion

    #region Debug

    public override string ToString()
    {
        return $"NPCSaveData[ID: {npcID}, Pos: {position}, Rot: {rotation.eulerAngles}]";
    }

    #endregion
}