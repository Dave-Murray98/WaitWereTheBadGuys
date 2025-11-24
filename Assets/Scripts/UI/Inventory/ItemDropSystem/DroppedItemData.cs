using UnityEngine;

/// <summary>
/// UPDATED: Enhanced data structure for dropped items with visual prefab support.
/// Now stores prefab reference instead of individual mesh/material data.
/// </summary>
[System.Serializable]
public class DroppedItemData
{
    [Header("Basic Item Info")]
    public string id;
    public string itemDataName;
    public Vector3 position;
    public Vector3 rotation; // Changed from float to Vector3 for full rotation
    public float objectMass = 1f;

    [Header("Visual Configuration")]
    public string visualPrefabName;
    public Vector3 visualScale = Vector3.one;
    public bool usePhysics = true;

    [Header("Physics State")]
    public bool isKinematic = false;
    public Vector3 velocity = Vector3.zero;
    public Vector3 angularVelocity = Vector3.zero;

    public DroppedItemData()
    {
        // Default constructor
    }

    public DroppedItemData(string itemId, string dataName, Vector3 pos, Vector3 rot = default)
    {
        id = itemId;
        itemDataName = dataName;
        position = pos;
        rotation = rot;
        objectMass = 1f;
        visualScale = Vector3.one;
        usePhysics = true;
        isKinematic = false;
    }

    /// <summary>
    /// Create dropped item data from ItemData with visual prefab information
    /// </summary>
    public static DroppedItemData FromItemData(string itemId, ItemData itemData, Vector3 position, Vector3 rotation = default)
    {
        if (itemData == null)
            return null;

        var data = new DroppedItemData(itemId, itemData.name, position, rotation);

        // Store visual prefab configuration
        data.visualPrefabName = itemData.visualPrefab?.name ?? "";
        data.visualScale = itemData.GetVisualPrefabScale();
        data.objectMass = itemData.objectMass;
        data.usePhysics = itemData.usePhysicsOnDrop;

        return data;
    }

    /// <summary>
    /// Update physics state from a rigidbody
    /// </summary>
    public void UpdatePhysicsState(Rigidbody rb)
    {
        if (rb != null)
        {
            isKinematic = rb.isKinematic;
            velocity = rb.linearVelocity;
            angularVelocity = rb.angularVelocity;
        }
    }

    /// <summary>
    /// Apply physics state to a rigidbody
    /// </summary>
    public void ApplyPhysicsState(Rigidbody rb)
    {
        if (rb != null)
        {
            rb.isKinematic = isKinematic;
            if (!isKinematic)
            {
                rb.linearVelocity = velocity;
                rb.angularVelocity = angularVelocity;
            }
        }
    }

    /// <summary>
    /// Check if this dropped item data is valid
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(id) &&
               !string.IsNullOrEmpty(itemDataName) &&
               !string.IsNullOrEmpty(visualPrefabName);
    }

    /// <summary>
    /// Get debug string representation
    /// </summary>
    public override string ToString()
    {
        return $"DroppedItem[{id}] {itemDataName} ({visualPrefabName}) at {position}, rot: {rotation} (Mass: {objectMass}, Physics: {usePhysics}, Kinematic: {isKinematic})";
    }
}