using System;
using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections;

/// <summary>
/// enemy hearing system that detects noise sources within a spherical trigger area.
/// Uses volume-to-distance ratio calculations to determine if sounds are audible to the enemy.
/// </summary>
public class EnemyHearing : MonoBehaviour
{
    [Header("Hearing Configuration")]
    [SerializeField, Range(0.1f, 5f)]
    [Tooltip("The threshold for hearing. Lower values = more sensitive hearing.")]
    private float hearingSensitivity = 1f;

    [SerializeField]
    [Tooltip("Maximum distance the enemy can hear sounds from")]
    private float maxHearingRange = 20f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool enableDebugGizmos = false;
    [SerializeField] private bool enableDebugVisualization = true;

    // Hearing state
    [ShowInInspector, ReadOnly] private bool hasHeardRecentNoise = false;
    [ShowInInspector, ReadOnly] private Vector3 lastHeardNoisePosition;

    // Events
    public Action<Vector3> OnNoiseHeard; // Position of the noise

    #region Public Properties
    public bool HasHeardRecentNoise => hasHeardRecentNoise;
    public Vector3 LastHeardNoisePosition => lastHeardNoisePosition;

    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        SetupComponents();
    }

    #endregion

    #region Setup and Initialization
    private void SetupComponents()
    {
        // Ensure this GameObject has a sphere trigger collider
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider == null)
        {
            sphereCollider = gameObject.AddComponent<SphereCollider>();

            float parentScale1 = transform.parent.lossyScale.x;
            float adjustedRange1 = maxHearingRange * 1 / parentScale1;

            sphereCollider.radius = maxHearingRange * adjustedRange1;
            Debug.LogWarning($"{gameObject.name}: No SphereCollider found! Added one automatically.");
        }

        if (!sphereCollider.isTrigger)
        {
            Debug.LogWarning($"{gameObject.name}: SphereCollider is not set as trigger. Setting it now.");
            sphereCollider.isTrigger = true;
        }

        float parentScale = transform.parent.lossyScale.x;
        float adjustedRange = maxHearingRange * 1 / parentScale;

        // Update collider radius to match hearing range based on parent scale
        sphereCollider.radius = adjustedRange;

        DebugLog("enemyHearing system initialized");
    }
    #endregion

    #region Noise Detection
    private void OnTriggerEnter(Collider other)
    {

        // Try to get the Noise component
        Noise noiseComponent = other.GetComponent<Noise>();
        if (noiseComponent == null)
        {
            DebugLog($"Object {other.name} is on Noise layer but has no Noise component!");
            return;
        }

        // Calculate if the enemy can hear this noise
        ProcessNoise(noiseComponent);
    }


    /// <summary>
    /// Process a noise to determine if the enemy can hear it
    /// </summary>
    private void ProcessNoise(Noise noise)
    {
        Vector3 noisePosition = noise.transform.position;
        float noiseVolume = noise.Volume;
        float distanceToNoise = Vector3.Distance(transform.position, noisePosition);

        DebugLog($"Processing noise: Volume={noiseVolume:F2}, Distance={distanceToNoise:F2}");

        // Calculate the effective volume based on distance using a more forgiving formula
        // NEW Formula: effectiveVolume = volume * volume / (1 + distance * hearingThreshold)
        // This gives much better hearing sensitivity, especially at close to medium ranges
        float distanceFactor = 1f + (distanceToNoise * hearingSensitivity);
        float effectiveVolume = (noiseVolume * noiseVolume) / distanceFactor;

        DebugLog($"Effective volume: {effectiveVolume:F2} (threshold: {hearingSensitivity:F2})");
        DebugLog($"Distance factor: {distanceFactor:F2}, Raw calculation: ({noiseVolume} * {noiseVolume}) / {distanceFactor}");

        // Check if the noise is loud enough to be heard
        if (effectiveVolume >= hearingSensitivity)
        {
            // enemy heard the noise!
            RegisterHeardNoise(noisePosition, noiseVolume, distanceToNoise);
        }
        else
        {
            DebugLog($"Noise too quiet to hear (effective: {effectiveVolume:F2} < threshold: {hearingSensitivity:F2})");
        }
    }

    /// <summary>
    /// Register a noise that the enemy successfully heard
    /// </summary>
    private void RegisterHeardNoise(Vector3 position, float volume, float distance)
    {
        hasHeardRecentNoise = true;
        lastHeardNoisePosition = position;

        DebugLog($"NOISE HEARD! Position: {position}, Volume: {volume:F2}, Distance: {distance:F2}");

        // Trigger the heard noise event
        OnNoiseHeard?.Invoke(position);

        // Draw debug visualization
        if (enableDebugVisualization)
        {
            Debug.DrawLine(transform.position, position, Color.green);
        }

        StartCoroutine(ForgetNoise());
    }

    private IEnumerator ForgetNoise()
    {
        yield return new WaitForSeconds(1f);
        hasHeardRecentNoise = false;
    }

    #endregion

    #region Public Interface
    /// <summary>
    /// Check if a specific position would be audible if a noise was made there
    /// </summary>
    public bool WouldHearNoiseAt(Vector3 position, float volume)
    {
        float distance = Vector3.Distance(transform.position, position);
        if (distance > maxHearingRange) return false;

        // Use the same calculation as ProcessNoise
        float distanceFactor = 1f + (distance * hearingSensitivity * 0.5f);
        float effectiveVolume = (volume * volume) / distanceFactor;
        return effectiveVolume >= hearingSensitivity;
    }

    /// <summary>
    /// Get the minimum volume needed to be heard at a specific distance
    /// </summary>
    public float GetMinimumVolumeForDistance(float distance)
    {
        // Solve for volume: effectiveVolume = (volume^2) / (1 + distance * hearingThreshold * 0.5)
        // hearingThreshold = (volume^2) / (1 + distance * hearingThreshold * 0.5)
        // volume = sqrt(hearingThreshold * (1 + distance * hearingThreshold * 0.5))
        float distanceFactor = 1f + (distance * hearingSensitivity * 0.5f);
        return Mathf.Sqrt(hearingSensitivity * distanceFactor);
    }

    /// <summary>
    /// Get the maximum distance a volume can be heard from (approximate)
    /// </summary>
    public float GetMaximumDistanceForVolume(float volume)
    {
        // Solve for distance: hearingThreshold = (volume^2) / (1 + distance * hearingThreshold * 0.5)
        // distance = ((volume^2 / hearingThreshold) - 1) / (hearingThreshold * 0.5)
        float volumeSquared = volume * volume;
        float theoreticalMax = ((volumeSquared / hearingSensitivity) - 1f) / (hearingSensitivity * 0.5f);
        return Mathf.Min(Mathf.Max(theoreticalMax, 0f), maxHearingRange);
    }

    /// <summary>
    /// Update hearing parameters at runtime
    /// </summary>
    public void UpdateHearingParameters(float newThreshold, float newRange, float newMemoryDuration)
    {
        hearingSensitivity = newThreshold;
        maxHearingRange = newRange;

        // Update sphere collider radius
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            sphereCollider.radius = maxHearingRange;
        }

        DebugLog("Hearing parameters updated");
    }

    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EnemyHearing] {message}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!enableDebugGizmos) return;

        // Draw hearing range
        Gizmos.color = hasHeardRecentNoise ? Color.red : Color.blue;
        Gizmos.DrawWireSphere(transform.position, maxHearingRange);

        // Draw last heard noise position
        if (hasHeardRecentNoise)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lastHeardNoisePosition, 0.5f);
            Gizmos.DrawLine(transform.position, lastHeardNoisePosition);

            // Draw a label showing how long ago the noise was heard
            Gizmos.color = Color.white;
        }

        // Draw threshold visualization - show minimum detectable noise at various distances
        if (enableDebugVisualization)
        {
            Gizmos.color = Color.yellow;
            for (float dist = 2f; dist <= maxHearingRange; dist += 2f)
            {
                float minVolume = GetMinimumVolumeForDistance(dist);
                Vector3 pos = transform.position + Vector3.right * dist;
                // Scale the visualization based on the improved hearing sensitivity
                float visualSize = Mathf.Clamp(minVolume * 0.3f, 0.1f, 2f);
                Gizmos.DrawWireCube(pos, Vector3.one * visualSize);
            }
        }
    }

    private void OnValidate()
    {
        // Update sphere collider radius when values change in inspector
        if (Application.isPlaying)
        {
            SphereCollider sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {

                float parentScale = transform.parent.lossyScale.x;
                float adjustedRange = maxHearingRange * 1 / parentScale;
                sphereCollider.radius = maxHearingRange * adjustedRange;
            }
        }
    }
    #endregion

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        StopAllCoroutines();
    }
}