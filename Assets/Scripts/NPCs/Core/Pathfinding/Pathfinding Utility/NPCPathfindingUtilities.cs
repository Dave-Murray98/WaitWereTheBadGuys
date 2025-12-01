using UnityEngine;
using Infohazard.HyperNav;
using Sirenix.OdinInspector;
using System.Collections.Generic;

public class NPCPathfindingUtilities : MonoBehaviour
{
    public static NPCPathfindingUtilities Instance { get; private set; }

    [SerializeField] private NavVolume[] navVolumes;

    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugSpheres = true;
    [SerializeField] private Color debugSphereColor = Color.cyan;
    [SerializeField] private float debugSphereDuration = 4f;

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

    private List<DebugSphere> activeSpheres = new List<DebugSphere>();

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

    public Vector3 GetRandomValidPosition(Vector3 agentPos)
    {
        NavVolume closestVolume = GetClosestVolume(agentPos);

        if (closestVolume == null)
        {
            Debug.LogWarning("No NavVolume found");
            return Vector3.zero;
        }

        int randomIndex = Random.Range(0, closestVolume.Data.Regions.Count - 1);

        Debug.Log($"Patrol Position set to {closestVolume.Data.Regions[randomIndex].Bounds.center}");

        return closestVolume.Data.Regions[randomIndex].Bounds.center;
    }

    public Vector3 GetRandomValidPositionNearPoint(Vector3 point, float radius = 20f)
    {
        // Add debug sphere visualization
        if (showDebugSpheres)
        {
            AddDebugSphere(point, radius, debugSphereDuration, debugSphereColor);
        }

        NavVolume closestVolume = GetClosestVolume(point);

        if (closestVolume == null)
        {
            Debug.LogWarning("No NavVolume found");
            return Vector3.zero;
        }

        List<NavRegionData> regionsInsideSphere = new List<NavRegionData>();

        foreach (NavRegionData region in closestVolume.Data.Regions)
        {
            if (Vector3.Distance(point, region.Bounds.center) <= radius)
            {
                regionsInsideSphere.Add(region);
            }
        }

        if (regionsInsideSphere.Count == 0)
        {
            Debug.LogWarning("No regions found inside the sphere");
            return Vector3.zero;
        }

        int randomIndex = Random.Range(0, regionsInsideSphere.Count);
        Vector3 selectedPosition = regionsInsideSphere[randomIndex].Bounds.center;

        Debug.Log($"Selected position {selectedPosition} within radius {radius} of point {point}");

        return selectedPosition;
    }

    /// <summary>
    /// Add a debug sphere that will be visualized for a specific duration
    /// </summary>
    private void AddDebugSphere(Vector3 center, float radius, float duration, Color color)
    {
        activeSpheres.Add(new DebugSphere(center, radius, duration, color));

        // Clean up expired spheres while we're at it
        CleanupExpiredSpheres();
    }

    /// <summary>
    /// Remove spheres that have exceeded their display duration
    /// </summary>
    private void CleanupExpiredSpheres()
    {
        float currentTime = Time.time;
        activeSpheres.RemoveAll(sphere => currentTime > sphere.endTime);
    }

    private NavVolume GetClosestVolume(Vector3 position)
    {
        NavVolume closest = navVolumes[0];

        float closestDistance = float.MaxValue;

        foreach (NavVolume volume in navVolumes)
        {
            float distance = Vector3.Distance(position, volume.Bounds.center);
            if (distance < closestDistance)
            {
                closest = volume;
            }
        }

        return closest;
    }

    [Button]
    public void FindVolumes()
    {
        navVolumes = FindObjectsByType<NavVolume>(FindObjectsSortMode.None);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugSpheres) return;

        // Clean up expired spheres
        CleanupExpiredSpheres();

        // Draw all active debug spheres
        foreach (DebugSphere sphere in activeSpheres)
        {
            // Calculate alpha based on remaining time for fade effect
            float remainingTime = sphere.endTime - Time.time;
            float alpha = Mathf.Clamp01(remainingTime / debugSphereDuration);

            Color fadeColor = sphere.color;
            fadeColor.a = alpha * 0.3f; // Make it semi-transparent

            Gizmos.color = fadeColor;
            Gizmos.DrawSphere(sphere.center, sphere.radius);

            // Draw wireframe for better visibility
            fadeColor.a = alpha * 0.8f;
            Gizmos.color = fadeColor;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
    }

    #region Extension Methods for PlayerAwarenessSystem Integration

    /// <summary>
    /// Extension method to match the PlayerAwarenessSystem interface
    /// This allows the awareness system to use this method directly
    /// </summary>
    public Vector3 GetRandomValidPositionInRadius(Vector3 centerPosition, float radius, int maxAttempts = 10)
    {
        // Use our existing method which already handles the radius-based search
        return GetRandomValidPositionNearPoint(centerPosition, radius);
    }

    /// <summary>
    /// Get nearest valid position (placeholder for HyperNav integration)
    /// You may want to implement this with actual HyperNav position validation
    /// </summary>
    public Vector3 GetNearestValidPosition(Vector3 targetPosition)
    {
        // For now, use the closest volume's nearest region
        NavVolume closestVolume = GetClosestVolume(targetPosition);

        if (closestVolume == null)
        {
            Debug.LogWarning("No NavVolume found for position validation");
            return Vector3.zero;
        }

        // Find the closest region to the target position
        NavRegionData closestRegion = closestVolume.Data.Regions[0];
        float closestDistance = Vector3.Distance(targetPosition, closestRegion.Bounds.center);

        foreach (NavRegionData region in closestVolume.Data.Regions)
        {
            float distance = Vector3.Distance(targetPosition, region.Bounds.center);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestRegion = region;
            }
        }

        return closestRegion.Bounds.center;
    }

    #endregion
}