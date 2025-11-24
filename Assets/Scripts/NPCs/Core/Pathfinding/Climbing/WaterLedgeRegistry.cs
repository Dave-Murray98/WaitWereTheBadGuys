using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Centralized registry for all water ledges in the scene.
/// Provides fast spatial lookups for 20+ NPCs without performance issues.
/// 
/// SETUP: Place this component on a GameObject in your scene (typically the parent of all water ledges).
/// Each scene should have exactly one WaterLedgeRegistry.
/// 
/// HOW IT WORKS:
/// - ClimbableWaterLedge automatically registers/unregisters itself on Awake/Destroy
/// - Uses spatial grid partitioning for O(1) nearby ledge queries
/// - Caches query results to optimize multiple NPCs searching from similar positions
/// - All data is scene-specific and cleared when scene unloads
/// </summary>
public class WaterLedgeRegistry : MonoBehaviour
{
    // Singleton instance (per scene)
    private static WaterLedgeRegistry instance;

    [Header("Spatial Partitioning")]
    [SerializeField, Tooltip("Size of each grid cell in meters")]
    private float gridCellSize = 50f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool showGizmos = false;

    // All registered ledges
    private List<ClimbableWaterLedge> allLedges = new List<ClimbableWaterLedge>();

    // Spatial grid: maps grid cell coordinates to ledges in that cell
    private Dictionary<Vector2Int, List<ClimbableWaterLedge>> spatialGrid =
        new Dictionary<Vector2Int, List<ClimbableWaterLedge>>();

    // Query cache to avoid redundant searches
    private Vector3 cachedQueryPosition;
    private float cachedQueryRadius;
    private List<ClimbableWaterLedge> cachedQueryResults;
    private float cachedQueryTime;
    private const float QUERY_CACHE_DURATION = 0.5f;

    #region Singleton Management

    private void Awake()
    {
        // If no instance exists, this becomes the instance
        if (instance == null)
        {
            instance = this;
            if (showDebugInfo)
            {
                Debug.Log($"[WaterLedgeRegistry] Initialized on {gameObject.name}");
            }
        }
        // If an instance already exists and it's not this one, warn and destroy component
        else if (instance != this)
        {
            Debug.LogError($"[WaterLedgeRegistry] Multiple registries detected! Only one should exist per scene. " +
                          $"Destroying duplicate on {gameObject.name}. Keep the one on {instance.gameObject.name}");
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        // Clear singleton reference if this was the active instance
        if (instance == this)
        {
            instance = null;
            if (showDebugInfo)
            {
                Debug.Log("[WaterLedgeRegistry] Registry cleared");
            }
        }
    }

    /// <summary>
    /// Get the active registry instance. Creates one if needed.
    /// </summary>
    public static WaterLedgeRegistry Instance
    {
        get
        {
            // If no instance, try to find one in the scene
            if (instance == null)
            {
                instance = FindFirstObjectByType<WaterLedgeRegistry>();

                // If still none found, create one automatically
                if (instance == null)
                {
                    GameObject registryObj = new GameObject("WaterLedgeRegistry");
                    instance = registryObj.AddComponent<WaterLedgeRegistry>();
                    Debug.Log("[WaterLedgeRegistry] Auto-created registry (none found in scene)");
                }
            }

            return instance;
        }
    }

    #endregion

    #region Registration

    /// <summary>
    /// Register a water ledge. Called automatically by ClimbableWaterLedge.
    /// </summary>
    public static void RegisterLedge(ClimbableWaterLedge ledge)
    {
        if (ledge == null) return;

        Instance.RegisterLedgeInternal(ledge);
    }

    /// <summary>
    /// Unregister a water ledge. Called automatically by ClimbableWaterLedge.
    /// </summary>
    public static void UnregisterLedge(ClimbableWaterLedge ledge)
    {
        // Don't access Instance if no instance exists (during scene unload)
        if (instance == null || ledge == null) return;

        instance.UnregisterLedgeInternal(ledge);
    }

    private void RegisterLedgeInternal(ClimbableWaterLedge ledge)
    {
        // Avoid duplicate registrations
        if (allLedges.Contains(ledge))
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"[WaterLedgeRegistry] Ledge {ledge.gameObject.name} already registered");
            }
            return;
        }

        allLedges.Add(ledge);
        AddToSpatialGrid(ledge);

        if (showDebugInfo)
        {
            Debug.Log($"[WaterLedgeRegistry] Registered: {ledge.gameObject.name} | Total: {allLedges.Count}");
        }
    }

    private void UnregisterLedgeInternal(ClimbableWaterLedge ledge)
    {
        allLedges.Remove(ledge);
        RemoveFromSpatialGrid(ledge);

        if (showDebugInfo)
        {
            Debug.Log($"[WaterLedgeRegistry] Unregistered: {ledge.gameObject.name} | Total: {allLedges.Count}");
        }
    }

    #endregion

    #region Spatial Grid

    private void AddToSpatialGrid(ClimbableWaterLedge ledge)
    {
        Vector2Int cell = GetGridCell(ledge.EndPosition);

        if (!spatialGrid.ContainsKey(cell))
        {
            spatialGrid[cell] = new List<ClimbableWaterLedge>();
        }

        spatialGrid[cell].Add(ledge);
    }

    private void RemoveFromSpatialGrid(ClimbableWaterLedge ledge)
    {
        Vector2Int cell = GetGridCell(ledge.EndPosition);

        if (spatialGrid.ContainsKey(cell))
        {
            spatialGrid[cell].Remove(ledge);

            // Clean up empty cells
            if (spatialGrid[cell].Count == 0)
            {
                spatialGrid.Remove(cell);
            }
        }
    }

    private Vector2Int GetGridCell(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x / gridCellSize);
        int z = Mathf.FloorToInt(worldPosition.z / gridCellSize);
        return new Vector2Int(x, z);
    }

    private List<Vector2Int> GetCellsInRadius(Vector3 center, float radius)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        Vector2Int centerCell = GetGridCell(center);

        int cellRadius = Mathf.CeilToInt(radius / gridCellSize);

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int z = -cellRadius; z <= cellRadius; z++)
            {
                cells.Add(new Vector2Int(centerCell.x + x, centerCell.y + z));
            }
        }

        return cells;
    }

    #endregion

    #region Public Query API

    /// <summary>
    /// Get all water ledges within a radius of a position.
    /// Uses spatial partitioning and caching for optimal performance.
    /// </summary>
    public static List<ClimbableWaterLedge> GetLedgesNearPosition(Vector3 position, float radius)
    {
        return Instance.GetLedgesNearPositionInternal(position, radius);
    }

    /// <summary>
    /// Get the closest water ledge to a position within a max radius.
    /// Returns null if no ledges found within radius.
    /// </summary>
    public static ClimbableWaterLedge GetClosestLedge(Vector3 position, float maxRadius)
    {
        return Instance.GetClosestLedgeInternal(position, maxRadius);
    }

    /// <summary>
    /// Get all registered water ledges in the scene.
    /// Use sparingly - prefer GetLedgesNearPosition for better performance.
    /// </summary>
    public static List<ClimbableWaterLedge> GetAllLedges()
    {
        return new List<ClimbableWaterLedge>(Instance.allLedges);
    }

    /// <summary>
    /// Get the total number of registered ledges.
    /// </summary>
    public static int GetLedgeCount()
    {
        return Instance.allLedges.Count;
    }

    #endregion

    #region Internal Query Implementation

    private List<ClimbableWaterLedge> GetLedgesNearPositionInternal(Vector3 position, float radius)
    {
        // Check if we can use cached results
        if (CanUseCachedQuery(position, radius))
        {
            return cachedQueryResults;
        }

        // Perform new query
        List<ClimbableWaterLedge> results = new List<ClimbableWaterLedge>();
        float radiusSqr = radius * radius;

        // Get all cells within radius
        List<Vector2Int> cells = GetCellsInRadius(position, radius);

        // Check ledges in each cell
        foreach (Vector2Int cell in cells)
        {
            if (!spatialGrid.ContainsKey(cell)) continue;

            foreach (ClimbableWaterLedge ledge in spatialGrid[cell])
            {
                if (ledge == null) continue;

                // Distance check using squared distance (faster)
                float distanceSqr = (ledge.EndPosition - position).sqrMagnitude;
                if (distanceSqr <= radiusSqr)
                {
                    results.Add(ledge);
                }
            }
        }

        // Update cache
        cachedQueryPosition = position;
        cachedQueryRadius = radius;
        cachedQueryResults = results;
        cachedQueryTime = Time.time;

        if (showDebugInfo)
        {
            Debug.Log($"[WaterLedgeRegistry] Query found {results.Count} ledges within {radius}m of {position}");
        }

        return results;
    }

    private ClimbableWaterLedge GetClosestLedgeInternal(Vector3 position, float maxRadius)
    {
        List<ClimbableWaterLedge> nearbyLedges = GetLedgesNearPositionInternal(position, maxRadius);

        if (nearbyLedges.Count == 0) return null;

        ClimbableWaterLedge closest = null;
        float closestDistanceSqr = float.MaxValue;

        foreach (ClimbableWaterLedge ledge in nearbyLedges)
        {
            float distanceSqr = (ledge.EndPosition - position).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closest = ledge;
            }
        }

        return closest;
    }

    private bool CanUseCachedQuery(Vector3 position, float radius)
    {
        if (Time.time - cachedQueryTime > QUERY_CACHE_DURATION)
            return false;

        if (cachedQueryResults == null)
            return false;

        // Check if position and radius are similar enough
        float positionDiff = Vector3.Distance(position, cachedQueryPosition);
        float radiusDiff = Mathf.Abs(radius - cachedQueryRadius);

        return positionDiff < 1f && radiusDiff < 0.1f;
    }

    #endregion

    #region Debug & Utilities

    /// <summary>
    /// Get statistics about the registry.
    /// </summary>
    public static string GetStats()
    {
        if (instance == null) return "No registry active";

        bool cacheActive = Time.time - instance.cachedQueryTime <= QUERY_CACHE_DURATION;

        return $"Ledges: {instance.allLedges.Count}\n" +
               $"Grid Cells: {instance.spatialGrid.Count}\n" +
               $"Cell Size: {instance.gridCellSize}m\n" +
               $"Query Cache: {(cacheActive ? "Active" : "Inactive")}";
    }

    /// <summary>
    /// Force rebuild the spatial grid (only needed if ledges move at runtime).
    /// </summary>
    public static void RebuildSpatialGrid()
    {
        Instance.RebuildSpatialGridInternal();
    }

    private void RebuildSpatialGridInternal()
    {
        spatialGrid.Clear();

        foreach (ClimbableWaterLedge ledge in allLedges)
        {
            if (ledge != null)
            {
                AddToSpatialGrid(ledge);
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"[WaterLedgeRegistry] Rebuilt spatial grid: {allLedges.Count} ledges, {spatialGrid.Count} cells");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || !Application.isPlaying) return;

        // Draw spatial grid cells
        Gizmos.color = new Color(0, 1, 1, 0.1f);
        foreach (Vector2Int cell in spatialGrid.Keys)
        {
            Vector3 cellCenter = new Vector3(
                cell.x * gridCellSize + gridCellSize * 0.5f,
                0,
                cell.y * gridCellSize + gridCellSize * 0.5f
            );

            Gizmos.DrawWireCube(cellCenter, new Vector3(gridCellSize, 1f, gridCellSize));
        }

        // Draw cached query if active
        if (Time.time - cachedQueryTime <= QUERY_CACHE_DURATION)
        {
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(cachedQueryPosition, cachedQueryRadius);
        }
    }

    #endregion
}