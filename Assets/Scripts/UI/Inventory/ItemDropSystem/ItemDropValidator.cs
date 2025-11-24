using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles collision detection and valid position finding for item drops.
/// Ensures items are placed on valid ground without clipping through obstacles.
/// </summary>
public class ItemDropValidator : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private ItemDropSettings dropSettings;
    [SerializeField] private bool enableDebugLogs = false;

    // Cache for performance
    private readonly Collider[] overlapResults = new Collider[10];
    private Vector2[] searchPattern;

    public ItemDropSettings DropSettings
    {
        get => dropSettings;
        set => dropSettings = value;
    }

    private void Awake()
    {
        if (dropSettings == null)
        {
            dropSettings = Resources.Load<ItemDropSettings>("Settings/ItemDropSettings");
            if (dropSettings == null)
            {
                Debug.LogWarning("No ItemDropSettings found in Resources/Settings/. Using default values.");
            }
        }

        RefreshSearchPattern();
    }

    private void RefreshSearchPattern()
    {
        if (dropSettings != null)
        {
            searchPattern = dropSettings.GetSearchPattern();
        }
    }

    /// <summary>
    /// Validates if an item can be dropped at the specified position
    /// </summary>
    public DropValidationResult ValidateDropPosition(Vector3 position, ItemData itemData, bool findAlternative = true)
    {
        if (itemData == null)
        {
            return new DropValidationResult(false, Vector3.zero, "ItemData is null");
        }

        // First try the exact position
        var exactResult = ValidateExactPosition(position, itemData);
        if (exactResult.isValid)
        {
            DebugLog($"Exact position valid for {itemData.itemName} at {position}");
            return exactResult;
        }

        // If exact position fails and we should find alternative
        if (findAlternative)
        {
            DebugLog($"Exact position invalid for {itemData.itemName}, searching for alternative...");
            return FindNearbyValidPosition(position, itemData);
        }

        return exactResult;
    }

    /// <summary>
    /// Validates a specific position without searching for alternatives
    /// </summary>
    private DropValidationResult ValidateExactPosition(Vector3 position, ItemData itemData)
    {
        // Step 1: Find ground below the position
        var groundResult = FindGroundBelow(position);
        if (!groundResult.hasGround)
        {
            return new DropValidationResult(false, position, "No ground found below position");
        }

        // Step 2: Check slope angle
        if (Vector3.Angle(groundResult.groundNormal, Vector3.up) > dropSettings.maxDropSlope)
        {
            return new DropValidationResult(false, position, "Ground slope too steep");
        }

        // Step 3: Calculate final drop position
        Vector3 finalPosition = CalculateFinalDropPosition(groundResult, itemData);

        // Step 4: Check for obstacles at the drop position
        if (HasObstacleAtPosition(finalPosition, itemData))
        {
            return new DropValidationResult(false, position, "Obstacle detected at drop position");
        }

        return new DropValidationResult(true, finalPosition, "Valid position found");
    }

    /// <summary>
    /// Finds a nearby valid position using spiral search pattern
    /// </summary>
    private DropValidationResult FindNearbyValidPosition(Vector3 centerPosition, ItemData itemData)
    {
        if (searchPattern == null)
        {
            RefreshSearchPattern();
        }

        foreach (var offset in searchPattern)
        {
            Vector3 testPosition = centerPosition + new Vector3(offset.x, 0, offset.y);

            // Check if this position is within max search radius
            if (Vector3.Distance(centerPosition, testPosition) > dropSettings.maxSearchRadius)
                continue;

            var result = ValidateExactPosition(testPosition, itemData);
            if (result.isValid)
            {
                DebugLog($"Found valid alternative position for {itemData.itemName} at {result.position}");
                return result;
            }
        }

        return new DropValidationResult(false, centerPosition, "No valid position found within search radius");
    }

    /// <summary>
    /// Finds ground below a given position
    /// </summary>
    private GroundCheckResult FindGroundBelow(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 0.5f; // Start slightly above

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit,
            dropSettings.maxGroundCheckDistance, dropSettings.groundLayerMask))
        {
            DebugLog($"Ground found at {hit.point}, normal: {hit.normal}");
            return new GroundCheckResult(true, hit.point, hit.normal, hit.collider);
        }

        DebugLog($"No ground found below {position}");
        return new GroundCheckResult(false, Vector3.zero, Vector3.up, null);
    }

    /// <summary>
    /// Calculates the final drop position considering item height and ground offset
    /// </summary>
    private Vector3 CalculateFinalDropPosition(GroundCheckResult groundResult, ItemData itemData)
    {
        float itemHeight = itemData.GetItemHeight();
        float totalOffset = dropSettings.additionalGroundOffset + itemData.groundHeightOffset + (itemHeight * 0.5f);

        return groundResult.groundPoint + groundResult.groundNormal * totalOffset;
    }

    /// <summary>
    /// Checks if there's an obstacle at the specified position
    /// </summary>
    private bool HasObstacleAtPosition(Vector3 position, ItemData itemData)
    {
        Vector3 colliderSize = itemData.GetInteractionColliderSize();
        float checkRadius = Mathf.Max(colliderSize.x, colliderSize.z) * 0.5f;
        checkRadius = Mathf.Max(checkRadius, dropSettings.obstacleCheckRadius);

        // Use OverlapSphereNonAlloc for performance
        int numColliders = Physics.OverlapSphereNonAlloc(
            position,
            checkRadius,
            overlapResults,
            dropSettings.obstacleLayerMask
        );

        if (numColliders > 0)
        {
            DebugLog($"Obstacle detected at {position}: {overlapResults[0].name}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the ideal drop position relative to a player
    /// </summary>
    public Vector3 GetIdealDropPosition(Transform playerTransform)
    {
        if (playerTransform == null)
            return Vector3.zero;

        Vector3 playerPosition = playerTransform.position;
        Vector3 playerForward = playerTransform.forward;

        // Calculate position in front of player
        Vector3 dropPosition = playerPosition + playerForward * dropSettings.dropDistanceFromPlayer;

        // Add some height for initial check
        dropPosition.y = playerPosition.y + 0.5f;

        return dropPosition;
    }

    /// <summary>
    /// Validates multiple positions at once (useful for bulk validation)
    /// </summary>
    public DropValidationResult[] ValidateMultiplePositions(Vector3[] positions, ItemData itemData)
    {
        var results = new DropValidationResult[positions.Length];

        for (int i = 0; i < positions.Length; i++)
        {
            results[i] = ValidateExactPosition(positions[i], itemData);
        }

        return results;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs || (dropSettings != null && dropSettings.enableDebugLogs))
        {
            Debug.Log($"[ItemDropValidator] {message}");
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (dropSettings == null || !dropSettings.showDebugVisualization)
            return;

        // Draw search pattern around this object's position
        Gizmos.color = dropSettings.debugColor;

        if (searchPattern != null)
        {
            foreach (var offset in searchPattern)
            {
                Vector3 position = transform.position + new Vector3(offset.x, 0, offset.y);
                Gizmos.DrawWireSphere(position, 0.1f);
            }
        }

        // Draw max search radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dropSettings.maxSearchRadius);
    }
#endif
}

/// <summary>
/// Result of drop position validation
/// </summary>
[System.Serializable]
public struct DropValidationResult
{
    public bool isValid;
    public Vector3 position;
    public string reason;

    public DropValidationResult(bool valid, Vector3 pos, string message)
    {
        isValid = valid;
        position = pos;
        reason = message;
    }
}

/// <summary>
/// Result of ground detection
/// </summary>
[System.Serializable]
public struct GroundCheckResult
{
    public bool hasGround;
    public Vector3 groundPoint;
    public Vector3 groundNormal;
    public Collider groundCollider;

    public GroundCheckResult(bool found, Vector3 point, Vector3 normal, Collider collider)
    {
        hasGround = found;
        groundPoint = point;
        groundNormal = normal;
        groundCollider = collider;
    }
}