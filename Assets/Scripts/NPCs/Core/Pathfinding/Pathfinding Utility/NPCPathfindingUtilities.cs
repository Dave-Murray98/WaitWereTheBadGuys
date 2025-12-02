using UnityEngine;
using Infohazard.HyperNav;
using Infohazard.HyperNav.Jobs.Utility;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Infohazard.HyperNav.Jobs;

public class NPCPathfindingUtilities : MonoBehaviour
{
    public static NPCPathfindingUtilities Instance { get; private set; }

    [SerializeField] private NavVolume[] navVolumes;

    [Header("Random Position Settings")]
    [SerializeField] private int maxRandomAttempts = 10;
    [SerializeField] private float defaultSampleRadius = 2f;
    [Tooltip("Number of random points to generate and test in a single batch for better performance")]
    [SerializeField] private int batchSize = 5;

    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugSpheres = true;
    [SerializeField] private Color debugSphereColor = Color.cyan;
    [SerializeField] private Color validPositionColor = Color.green;
    [SerializeField] private Color invalidPositionColor = Color.red;
    [SerializeField] private float debugSphereDuration = 4f;
    [SerializeField] private float debugPositionSize = 0.5f;

    // Debug sphere data for visualization
    private struct DebugSphere
    {
        public Vector3 center;
        public float radius;
        public float endTime;
        public Color color;

        public DebugSphere(Vector3 center, float radius, float duration, Color color)
        {
            this.center = center;
            this.radius = radius;
            this.endTime = Time.time + duration;
            this.color = color;
        }
    }

    private struct DebugPosition
    {
        public Vector3 position;
        public float endTime;
        public Color color;
        public float size;

        public DebugPosition(Vector3 position, float duration, Color color, float size)
        {
            this.position = position;
            this.endTime = Time.time + duration;
            this.color = color;
            this.size = size;
        }
    }

    private List<DebugSphere> activeSpheres = new List<DebugSphere>();
    private List<DebugPosition> activePositions = new List<DebugPosition>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Gets a completely random valid position from any NavVolume
    /// </summary>
    public Vector3 GetRandomValidPosition(Vector3 agentPos)
    {
        NavVolume closestVolume = GetClosestVolume(agentPos);

        if (closestVolume == null)
        {
            Debug.LogWarning("No NavVolume found");
            return Vector3.zero;
        }

        // Use the volume's bounds to generate random positions
        Bounds volumeBounds = closestVolume.Bounds;

        // Generate random positions within the volume bounds and test them
        Vector3 validPosition = GenerateRandomValidPositionInBounds(volumeBounds, NavAreaTypes.Volume);

        if (validPosition == Vector3.zero)
        {
            Debug.LogWarning("Failed to find random valid position, falling back to region center");
            // Fallback to your original method if needed
            int randomIndex = UnityEngine.Random.Range(0, closestVolume.Data.Regions.Count);
            validPosition = closestVolume.Data.Regions[randomIndex].Bounds.center;
        }

        return validPosition;
    }

    /// <summary>
    /// Gets a random valid position near a specific point using NavSampleJob
    /// </summary>
    public Vector3 GetRandomValidPositionNearPoint(Vector3 point, float radius = 20f)
    {
        // Add debug sphere visualization
        if (showDebugSpheres)
        {
            AddDebugSphere(point, radius, debugSphereDuration, debugSphereColor);
        }

        // Generate multiple random points within the sphere and test them all at once
        Vector3[] candidatePositions = GenerateRandomPointsInSphere(point, radius, batchSize);
        Vector3 validPosition = FindValidPositionFromCandidates(candidatePositions, NavAreaTypes.Volume);

        if (validPosition == Vector3.zero)
        {
            Debug.LogWarning($"No valid positions found within radius {radius} of point {point}");

            // Fallback: try to find any valid position nearby using the original method
            NavVolume closestVolume = GetClosestVolume(point);
            if (closestVolume != null)
            {
                validPosition = GetNearestValidPosition(point);
            }
        }

        return validPosition;
    }

    /// <summary>
    /// Generates random points within a sphere around a center point
    /// </summary>
    private Vector3[] GenerateRandomPointsInSphere(Vector3 center, float radius, int count)
    {
        Vector3[] points = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            // Generate a random point within a sphere
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere;
            Vector3 randomPoint = center + (randomDirection * radius);
            points[i] = randomPoint;
        }

        return points;
    }

    /// <summary>
    /// Generates random points within bounds
    /// </summary>
    private Vector3[] GenerateRandomPointsInBounds(Bounds bounds, int count)
    {
        Vector3[] points = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            Vector3 randomPoint = new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                UnityEngine.Random.Range(bounds.min.z, bounds.max.z)
            );
            points[i] = randomPoint;
        }

        return points;
    }

    /// <summary>
    /// Uses NavSampleJob to test multiple positions at once and return the first valid one
    /// </summary>
    private Vector3 FindValidPositionFromCandidates(Vector3[] candidates, NavAreaTypes areaTypeMask)
    {
        if (candidates == null || candidates.Length == 0)
            return Vector3.zero;

        // Convert to NavSampleQuery array for batch processing
        NavSampleQuery[] queries = new NavSampleQuery[candidates.Length];
        for (int i = 0; i < candidates.Length; i++)
        {
            queries[i] = new NavSampleQuery(
                candidates[i],
                defaultSampleRadius,
                areaTypeMask,
                uint.MaxValue, // all layers
                NavSamplePriority.Nearest
            );
        }

        // Create results array
        NavSampleResult[] results = new NavSampleResult[candidates.Length];

        try
        {
            // Use the static method to sample all positions at once
            NavSampleJob.SamplePositionsInAllAreas(queries, results);

            // Find the first valid result
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].AreaID > 0) // Valid hit
                {
                    Vector3 validPosition = results[i].Position.xyz;

                    // Add debug visualization
                    if (showDebugSpheres)
                    {
                        AddDebugPosition(validPosition, debugSphereDuration, validPositionColor, debugPositionSize);

                        // Also show invalid positions for debugging
                        for (int j = 0; j < candidates.Length; j++)
                        {
                            if (j != i) // Don't show the valid one twice
                            {
                                AddDebugPosition(candidates[j], debugSphereDuration, invalidPositionColor, debugPositionSize * 0.5f);
                            }
                        }
                    }

                    Debug.Log($"Found valid position {validPosition} from {candidates.Length} candidates");
                    return validPosition;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during position sampling: {e.Message}");
        }

        return Vector3.zero; // No valid position found
    }

    /// <summary>
    /// Generate a single random valid position within given bounds
    /// </summary>
    private Vector3 GenerateRandomValidPositionInBounds(Bounds bounds, NavAreaTypes areaTypeMask)
    {
        for (int attempt = 0; attempt < maxRandomAttempts; attempt++)
        {
            Vector3[] candidates = GenerateRandomPointsInBounds(bounds, batchSize);
            Vector3 validPosition = FindValidPositionFromCandidates(candidates, areaTypeMask);

            if (validPosition != Vector3.zero)
                return validPosition;
        }

        return Vector3.zero; // Failed to find valid position
    }

    /// <summary>
    /// Add a debug sphere that will be visualized for a specific duration
    /// </summary>
    private void AddDebugSphere(Vector3 center, float radius, float duration, Color color)
    {
        activeSpheres.Add(new DebugSphere(center, radius, duration, color));
        CleanupExpiredSpheres();
    }

    /// <summary>
    /// Add a debug position marker
    /// </summary>
    private void AddDebugPosition(Vector3 position, float duration, Color color, float size)
    {
        activePositions.Add(new DebugPosition(position, duration, color, size));
        CleanupExpiredPositions();
    }

    /// <summary>
    /// Remove spheres that have exceeded their display duration
    /// </summary>
    private void CleanupExpiredSpheres()
    {
        float currentTime = Time.time;
        activeSpheres.RemoveAll(sphere => currentTime > sphere.endTime);
    }

    /// <summary>
    /// Remove positions that have exceeded their display duration
    /// </summary>
    private void CleanupExpiredPositions()
    {
        float currentTime = Time.time;
        activePositions.RemoveAll(pos => currentTime > pos.endTime);
    }

    private NavVolume GetClosestVolume(Vector3 position)
    {
        if (navVolumes == null || navVolumes.Length == 0)
        {
            Debug.LogWarning("No NavVolumes assigned to NPCPathfindingUtilities");
            return null;
        }

        NavVolume closest = navVolumes[0];
        float closestDistance = Vector3.Distance(position, closest.Bounds.center);

        foreach (NavVolume volume in navVolumes)
        {
            float distance = Vector3.Distance(position, volume.Bounds.center);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = volume;
            }
        }

        return closest;
    }

    [Button]
    public void FindVolumes()
    {
        navVolumes = FindObjectsByType<NavVolume>(FindObjectsSortMode.None);
        Debug.Log($"Found {navVolumes.Length} NavVolumes");
    }

    private void OnDrawGizmos()
    {
        if (!showDebugSpheres) return;

        // Clean up expired items
        CleanupExpiredSpheres();
        CleanupExpiredPositions();

        // Draw all active debug spheres
        foreach (DebugSphere sphere in activeSpheres)
        {
            // Calculate alpha based on remaining time for fade effect
            float remainingTime = sphere.endTime - Time.time;
            float alpha = Mathf.Clamp01(remainingTime / debugSphereDuration);

            Color fadeColor = sphere.color;
            fadeColor.a = alpha * 0.3f;

            Gizmos.color = fadeColor;
            Gizmos.DrawSphere(sphere.center, sphere.radius);

            // Draw wireframe for better visibility
            fadeColor.a = alpha * 0.8f;
            Gizmos.color = fadeColor;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }

        // Draw position markers
        foreach (DebugPosition pos in activePositions)
        {
            float remainingTime = pos.endTime - Time.time;
            float alpha = Mathf.Clamp01(remainingTime / debugSphereDuration);

            Color fadeColor = pos.color;
            fadeColor.a = alpha;

            Gizmos.color = fadeColor;
            Gizmos.DrawSphere(pos.position, pos.size);
        }
    }

    #region Extension Methods for PlayerAwarenessSystem Integration

    /// <summary>
    /// Extension method to match the PlayerAwarenessSystem interface
    /// </summary>
    public Vector3 GetRandomValidPositionInRadius(Vector3 centerPosition, float radius, int maxAttempts = 10)
    {
        return GetRandomValidPositionNearPoint(centerPosition, radius);
    }

    /// <summary>
    /// Get nearest valid position using NavSampleJob
    /// </summary>
    public Vector3 GetNearestValidPosition(Vector3 targetPosition)
    {
        // Try to sample the exact position first
        if (NavSampleJob.SamplePositionInAllAreas(targetPosition, out NavSampleResult hit, defaultSampleRadius, NavAreaTypes.Volume))
        {
            return hit.Position.xyz;
        }

        // If that fails, try positions in an expanding radius
        for (float radius = defaultSampleRadius; radius <= 20f; radius += defaultSampleRadius)
        {
            Vector3[] candidates = GenerateRandomPointsInSphere(targetPosition, radius, batchSize);
            Vector3 validPosition = FindValidPositionFromCandidates(candidates, NavAreaTypes.Volume);

            if (validPosition != Vector3.zero)
                return validPosition;
        }

        Debug.LogWarning($"Could not find valid position near {targetPosition}");
        return targetPosition; // Return original as fallback
    }

    #endregion
}