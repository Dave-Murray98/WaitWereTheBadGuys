using UnityEngine;

/// <summary>
/// Unified data structure for tracking all pickup items (both original scene items and dropped inventory items)
/// Handles position, rotation, physics state, and collection status for any item type
/// </summary>
[System.Serializable]
public class PickupItemData
{
    [Header("Basic Info")]
    public string itemId;
    public string itemDataName;
    public PickupItemType itemType;

    [Header("Collection State")]
    public bool isCollected = false;

    [Header("Transform Data")]
    public Vector3 originalPosition;
    public Vector3 currentPosition;
    public Vector3 originalRotation;
    public Vector3 currentRotation;
    public Vector3 originalScale = Vector3.one;
    public Vector3 currentScale = Vector3.one;

    [Header("Physics State")]
    public bool hasRigidbody = false;
    public bool isKinematic = false;
    public float objectMass = 1f;
    public Vector3 velocity = Vector3.zero;
    public Vector3 angularVelocity = Vector3.zero;

    [Header("Visual Configuration")]
    public string visualPrefabName;
    public Vector3 visualScale = Vector3.one;
    public bool usePhysics = true;

    [Header("State Tracking")]
    public bool hasBeenMoved = false;
    public bool hasBeenRotated = false;
    public bool wasInteractedWith = false;

    public PickupItemData()
    {
        // Default constructor
    }

    /// <summary>
    /// Create from an existing ItemPickupInteractable (for original scene items)
    /// </summary>
    public PickupItemData(string id, ItemPickupInteractable pickup, PickupItemType type)
    {
        itemId = id;
        itemType = type;

        if (pickup != null && pickup.GetItemData() != null)
        {
            var itemData = pickup.GetItemData();
            itemDataName = itemData.name;
            visualPrefabName = itemData.visualPrefab?.name ?? "";
            visualScale = itemData.GetVisualPrefabScale();
            objectMass = itemData.objectMass;
            usePhysics = itemData.usePhysicsOnDrop;
        }

        Transform rootTransform = pickup.GetRootTransform() ?? pickup.transform;

        // Store original and current transform data
        originalPosition = rootTransform.position;
        currentPosition = originalPosition;
        originalRotation = rootTransform.eulerAngles;
        currentRotation = originalRotation;
        originalScale = rootTransform.localScale;
        currentScale = originalScale;

        // Store physics data if present
        Rigidbody rb = rootTransform.GetComponent<Rigidbody>();
        if (rb != null)
        {
            hasRigidbody = true;
            isKinematic = rb.isKinematic;
            objectMass = rb.mass;
            velocity = rb.linearVelocity;
            angularVelocity = rb.angularVelocity;
        }

        // Initialize state flags
        isCollected = false;
        hasBeenMoved = false;
        hasBeenRotated = false;
        wasInteractedWith = false;
    }

    /// <summary>
    /// Create from ItemData (for dropped inventory items)
    /// </summary>
    public static PickupItemData FromItemData(string itemId, ItemData itemData, Vector3 position, Vector3 rotation = default)
    {
        if (itemData == null)
        {
            Debug.LogError("Cannot create pickup item data: ItemData is null");
            return null;
        }

        var data = new PickupItemData
        {
            itemId = itemId,
            itemType = PickupItemType.DroppedInventoryItem,
            itemDataName = itemData.name,

            // Set positions (no "original" for dropped items, they start where dropped)
            originalPosition = position,
            currentPosition = position,
            originalRotation = rotation,
            currentRotation = rotation,
            originalScale = Vector3.one,
            currentScale = Vector3.one,

            // Visual configuration
            visualPrefabName = itemData.visualPrefab?.name ?? "",
            visualScale = itemData.GetVisualPrefabScale(),
            objectMass = itemData.objectMass,
            usePhysics = itemData.usePhysicsOnDrop,

            // State
            isCollected = false,
            hasBeenMoved = false,
            hasBeenRotated = false,
            wasInteractedWith = true // Dropped items are inherently "interacted with"
        };

        return data;
    }

    /// <summary>
    /// Updates current state from a transform and rigidbody
    /// </summary>
    public void UpdateCurrentState(Transform transform, Rigidbody rigidbody = null)
    {
        if (transform == null) return;

        Vector3 newPosition = transform.position;
        Vector3 newRotation = transform.eulerAngles;
        Vector3 newScale = transform.localScale;

        // Check if position has changed significantly
        if (Vector3.Distance(currentPosition, newPosition) > 0.01f)
        {
            hasBeenMoved = true;
            currentPosition = newPosition;
            wasInteractedWith = true;
        }

        // Check if rotation has changed significantly
        if (Vector3.Distance(currentRotation, newRotation) > 1f) // 1 degree threshold
        {
            hasBeenRotated = true;
            currentRotation = newRotation;
            wasInteractedWith = true;
        }

        currentScale = newScale;

        // Update physics state
        if (rigidbody != null && hasRigidbody)
        {
            isKinematic = rigidbody.isKinematic;
            objectMass = rigidbody.mass;
            if (!isKinematic)
            {
                velocity = rigidbody.linearVelocity;
                angularVelocity = rigidbody.angularVelocity;
            }
        }
    }

    /// <summary>
    /// Apply this state to a transform and rigidbody
    /// </summary>
    public void ApplyToTransform(Rigidbody rigidbody)
    {
        if (rigidbody == null)
        {
            Debug.LogWarning($"PickupItemData: No Rigidbody provided to apply state for item {itemId}");
            return;
        }

        rigidbody.Move(currentPosition, Quaternion.Euler(currentRotation));

        if (rigidbody != null && hasRigidbody)
        {
            rigidbody.isKinematic = isKinematic;
            rigidbody.mass = objectMass;

            if (!isKinematic)
            {
                rigidbody.linearVelocity = velocity;
                rigidbody.angularVelocity = angularVelocity;
            }
        }
    }

    /// <summary>
    /// Check if this item has changed from its original state
    /// </summary>
    public bool HasChangedFromOriginal()
    {
        return hasBeenMoved || hasBeenRotated || wasInteractedWith || isCollected;
    }

    /// <summary>
    /// Check if this item needs to exist in the scene
    /// </summary>
    public bool ShouldExistInScene()
    {
        return !isCollected;
    }

    /// <summary>
    /// Mark item as collected
    /// </summary>
    public void MarkAsCollected()
    {
        isCollected = true;
        wasInteractedWith = true;
    }

    /// <summary>
    /// Restore item (uncollect it)
    /// </summary>
    public void RestoreToScene(Vector3? newPosition = null, Vector3? newRotation = null)
    {
        isCollected = false;
        wasInteractedWith = true;

        if (newPosition.HasValue)
        {
            currentPosition = newPosition.Value;
            hasBeenMoved = true;
        }

        if (newRotation.HasValue)
        {
            currentRotation = newRotation.Value;
            hasBeenRotated = true;
        }
    }

    /// <summary>
    /// Check if this item data is valid
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(itemId) &&
               !string.IsNullOrEmpty(itemDataName) &&
               !string.IsNullOrEmpty(visualPrefabName);
    }

    /// <summary>
    /// Get debug information
    /// </summary>
    public string GetDebugInfo()
    {
        return $"PickupItem[{itemId}] Type:{itemType} Collected:{isCollected} " +
               $"Moved:{hasBeenMoved} Rotated:{hasBeenRotated} Pos:{currentPosition} Rot:{currentRotation}";
    }

    /// <summary>
    /// Get a detailed string representation for debugging
    /// </summary>
    public override string ToString()
    {
        return $"PickupItemData[{itemId}] {itemDataName} ({itemType}) " +
               $"at {currentPosition}, rot: {currentRotation} " +
               $"(Collected: {isCollected}, Mass: {objectMass}, Physics: {usePhysics})";
    }
}

/// <summary>
/// Enum to distinguish between different types of pickup items
/// </summary>
public enum PickupItemType
{
    OriginalSceneItem,      // Items that were in the scene from the start
    DroppedInventoryItem    // Items that were dropped from player inventory
}