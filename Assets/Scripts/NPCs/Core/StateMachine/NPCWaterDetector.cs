using UnityEngine;
using Crest;
using System.Collections;
using Sirenix.OdinInspector;

/// <summary>
/// Improved water detection with better timing and reliability
/// Uses adaptive checking intervals and immediate detection for critical changes
/// Now includes surface height detection for target validation with efficient caching
/// OPTIMIZED for multiple NPCs (10-20+) with minimal performance impact
/// </summary>
public class NPCWaterDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField, Tooltip("Height offset from NPC center for water sampling")]
    private float detectionHeightOffset = 0f;

    [SerializeField, Tooltip("How deep NPC must be to enter water state")]
    private float waterEntryDepth = 0.3f;

    [SerializeField, Tooltip("How shallow NPC must be to exit water state")]
    private float waterExitDepth = 0.1f;

    [Header("Performance")]
    [SerializeField, Tooltip("Normal check interval when no transition expected")]
    private float normalCheckInterval = 0.2f;

    [SerializeField, Tooltip("Fast check interval when near water surface")]
    private float transitionCheckInterval = 0.15f;

    [SerializeField, Tooltip("Distance from water surface to use fast checking")]
    private float fastCheckThreshold = 1f;

    [Header("Stability")]
    [SerializeField, Tooltip("Required time in new state before transition")]
    private float stateConfirmationTime = 0.2f;

    [SerializeField, Tooltip("Minimum time between state changes")]
    private float minTimeBetweenTransitions = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = false;

    // Crest components
    private SampleHeightHelper waterSampleHelper;
    private SampleHeightHelper externalSampleHelper; // Separate helper for external queries
    private OceanRenderer oceanRenderer;

    // State tracking with stability
    [ShowInInspector] private bool isInWater = false;
    private bool pendingWaterState = false;
    private float pendingStateTime = 0f;
    private float lastTransitionTime = 0f;
    private bool sceneHasWater = false;

    // Performance optimization
    private float lastCheckTime = 0f;
    private float currentCheckInterval;
    private Vector3 lastSamplePosition;
    private float cachedWaterHeight = 0f;
    private float currentDepthInWater = 0f;

    // Coroutine management
    private Coroutine stateConfirmationCoroutine;

    // Events
    public event System.Action OnEnteredWater;
    public event System.Action OnExitedWater;

    // Public properties
    public bool IsInWater => sceneHasWater && isInWater;
    public bool IsOnGround => !IsInWater;
    public float DepthInWater => IsInWater ? currentDepthInWater : 0f;
    public float WaterEntryDepth => waterEntryDepth;
    public bool SceneHasWater => sceneHasWater;
    public bool IsTransitioning => stateConfirmationCoroutine != null;

    // OPTIMIZATION: Static shared water height cache across all NPCs
    private static WaterHeightCache sharedWaterCache = new WaterHeightCache();

    #region Initialization

    private void Start()
    {
        InitializeWaterDetection();
        currentCheckInterval = normalCheckInterval;

        // OPTIMIZATION: Stagger initial checks across NPCs to spread CPU load
        lastCheckTime = -Random.Range(0f, normalCheckInterval);
    }

    private void InitializeWaterDetection()
    {
        oceanRenderer = FindFirstObjectByType<OceanRenderer>();
        sceneHasWater = oceanRenderer != null;

        if (sceneHasWater)
        {
            SetupCrestComponents();

            // Do an immediate check to set initial state
            StartCoroutine(InitialStateCheck());
        }
        else
        {
            isInWater = false;
            DebugLog("No water found in scene - NPC will remain on ground");
        }

        DebugLog($"Water detection initialized - Scene has water: {sceneHasWater}");
    }

    private void SetupCrestComponents()
    {
        try
        {
            waterSampleHelper = new SampleHeightHelper();
            Vector3 testPosition = GetSamplePosition();
            waterSampleHelper.Init(testPosition, 0f, false, this);

            DebugLog("Crest water sampling initialized successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NPCWaterDetector] Failed to initialize Crest sampling for {gameObject.name}: {e.Message}");
            sceneHasWater = false;
            isInWater = false;
        }
    }

    private IEnumerator InitialStateCheck()
    {
        yield return new WaitForEndOfFrame(); // Wait for physics to settle

        ForceWaterStateCheck();

        // Set initial state without confirmation delay
        Vector3 samplePosition = GetSamplePosition();
        float waterHeight = SampleWaterHeightAtPosition(samplePosition);
        float depthInWater = Mathf.Max(0f, waterHeight - samplePosition.y);

        bool shouldBeInWater = depthInWater > waterEntryDepth;

        if (shouldBeInWater != isInWater)
        {
            isInWater = shouldBeInWater;
            currentDepthInWater = depthInWater;

            DebugLog($"Initial water state set to: {(isInWater ? "IN WATER" : "ON GROUND")} (depth: {depthInWater:F2}m)");

            // Don't fire events for initial state - let state machine handle it
        }
    }

    #endregion

    #region Water Detection

    private void Update()
    {
        if (!sceneHasWater) return;

        // Check if it's time for a water state check
        if (Time.time - lastCheckTime >= currentCheckInterval)
        {
            lastCheckTime = Time.time;
            CheckWaterState();
        }
    }

    private void CheckWaterState()
    {
        Vector3 samplePosition = GetSamplePosition();
        float waterHeight = SampleWaterHeightAtPosition(samplePosition);
        float depthInWater = Mathf.Max(0f, waterHeight - samplePosition.y);
        currentDepthInWater = depthInWater;

        // Optimize check interval based on proximity to water surface
        OptimizeCheckInterval(depthInWater);

        // Determine what state we should be in
        bool shouldBeInWater = DetermineWaterState(depthInWater);

        // Handle state changes with confirmation system
        if (shouldBeInWater != isInWater)
        {
            HandlePendingStateChange(shouldBeInWater);
        }
        else
        {
            // Current state is correct, cancel any pending changes
            CancelPendingStateChange();
        }
    }

    private void OptimizeCheckInterval(float depthInWater)
    {
        // Use faster checking when near water surface transitions
        bool nearSurface = Mathf.Abs(depthInWater - waterEntryDepth) < fastCheckThreshold ||
                          Mathf.Abs(depthInWater - waterExitDepth) < fastCheckThreshold;

        currentCheckInterval = nearSurface ? transitionCheckInterval : normalCheckInterval;
    }

    private Vector3 GetSamplePosition()
    {
        return transform.position + Vector3.up * detectionHeightOffset;
    }

    private float SampleWaterHeightAtPosition(Vector3 position)
    {
        if (waterSampleHelper == null || oceanRenderer == null)
            return 0f;

        try
        {
            // Re-initialize if position changed significantly
            if (Vector3.Distance(position, lastSamplePosition) > 2f)
            {
                waterSampleHelper.Init(position, 0f, false, this);
                lastSamplePosition = position;
            }

            if (waterSampleHelper.Sample(out float waterHeight))
            {
                cachedWaterHeight = waterHeight;
                return waterHeight;
            }

            return oceanRenderer.SeaLevel;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[NPCWaterDetector] Water sampling error for {gameObject.name}: {e.Message}");
            return cachedWaterHeight;
        }
    }

    private bool DetermineWaterState(float depthInWater)
    {
        if (isInWater)
        {
            // Currently in water - use exit threshold (hysteresis)
            return depthInWater > waterExitDepth;
        }
        else
        {
            // Currently on ground - use entry threshold
            return depthInWater > waterEntryDepth;
        }
    }

    #endregion

    #region State Change Management

    private void HandlePendingStateChange(bool targetState)
    {
        // Check minimum time between transitions
        if (Time.time - lastTransitionTime < minTimeBetweenTransitions)
        {
            return;
        }

        // If this is a new pending state, start confirmation
        if (pendingWaterState != targetState || stateConfirmationCoroutine == null)
        {
            pendingWaterState = targetState;
            pendingStateTime = Time.time;

            // Cancel existing confirmation
            if (stateConfirmationCoroutine != null)
            {
                StopCoroutine(stateConfirmationCoroutine);
            }

            // Start new confirmation
            stateConfirmationCoroutine = StartCoroutine(ConfirmStateChange(targetState));
        }
    }

    private void CancelPendingStateChange()
    {
        if (stateConfirmationCoroutine != null)
        {
            StopCoroutine(stateConfirmationCoroutine);
            stateConfirmationCoroutine = null;
        }
    }

    private IEnumerator ConfirmStateChange(bool targetState)
    {
        float confirmationStartTime = Time.time;

        while (Time.time - confirmationStartTime < stateConfirmationTime)
        {
            yield return new WaitForFixedUpdate();

            // Re-check state during confirmation period
            Vector3 samplePosition = GetSamplePosition();
            float waterHeight = SampleWaterHeightAtPosition(samplePosition);
            float depthInWater = Mathf.Max(0f, waterHeight - samplePosition.y);
            bool currentDesiredState = DetermineWaterState(depthInWater);

            // If desired state changed during confirmation, restart
            if (currentDesiredState != targetState)
            {
                stateConfirmationCoroutine = null;
                HandlePendingStateChange(currentDesiredState);
                yield break;
            }
        }

        // Confirmation period completed, execute state change
        ExecuteStateChange(targetState);
        stateConfirmationCoroutine = null;
    }

    private void ExecuteStateChange(bool newWaterState)
    {
        if (newWaterState == isInWater)
            return; // State hasn't actually changed

        isInWater = newWaterState;
        lastTransitionTime = Time.time;

        if (isInWater)
        {
            DebugLog($"Confirmed transition to WATER - depth: {currentDepthInWater:F2}m");
            OnEnteredWater?.Invoke();
        }
        else
        {
            DebugLog($"Confirmed transition to GROUND");
            currentDepthInWater = 0f;
            OnExitedWater?.Invoke();
        }
    }

    #endregion

    #region Surface Height Detection - OPTIMIZED

    /// <summary>
    /// Get the water surface height at any world position
    /// OPTIMIZED: Uses static shared cache across all NPCs
    /// </summary>
    public float GetWaterSurfaceHeightAt(Vector3 worldPosition)
    {
        if (!sceneHasWater || oceanRenderer == null)
            return 0f;

        // OPTIMIZATION: Check shared cache first (benefits all NPCs)
        if (sharedWaterCache.TryGetCachedHeight(worldPosition, out float cachedHeight))
        {
            return cachedHeight;
        }

        // Need to sample
        float waterHeight = SampleWaterHeightAtPositionExternal(worldPosition);

        // Cache in shared cache for other NPCs to use
        sharedWaterCache.CacheHeight(worldPosition, waterHeight);

        return waterHeight;
    }

    /// <summary>
    /// Sample water height for external queries (not the NPC's own position)
    /// Uses a simpler approach that doesn't interfere with the main SampleHeightHelper
    /// </summary>
    private float SampleWaterHeightAtPositionExternal(Vector3 position)
    {
        if (oceanRenderer == null)
            return 0f;

        // Default: use sea level (fast, no additional Crest calls)
        return oceanRenderer.SeaLevel;
    }

    /// <summary>
    /// Check if a position is above the water surface
    /// OPTIMIZED: Uses shared cache
    /// </summary>
    public bool IsPositionAboveWater(Vector3 worldPosition)
    {
        if (!sceneHasWater)
            return true; // No water means everything is "above water"

        float waterHeight = GetWaterSurfaceHeightAt(worldPosition);
        return worldPosition.y > waterHeight;
    }

    /// <summary>
    /// Get the depth of a position in water (negative if above water)
    /// OPTIMIZED: Uses shared cache
    /// </summary>
    public float GetDepthAtPosition(Vector3 worldPosition)
    {
        if (!sceneHasWater)
            return 0f;

        float waterHeight = GetWaterSurfaceHeightAt(worldPosition);
        return waterHeight - worldPosition.y;
    }

    /// <summary>
    /// Get a position clamped to just below the water surface
    /// Useful for keeping NPCs submerged when following above-water targets
    /// OPTIMIZED: Uses shared cache
    /// </summary>
    public Vector3 GetSubmergedPosition(Vector3 worldPosition, float depthBelowSurface = 0.5f)
    {
        if (!sceneHasWater)
            return worldPosition;

        float waterHeight = GetWaterSurfaceHeightAt(worldPosition);
        float targetY = waterHeight - depthBelowSurface;

        return new Vector3(worldPosition.x, targetY, worldPosition.z);
    }

    #endregion

    #region Shared Water Height Cache - NEW

    /// <summary>
    /// Static shared cache for water heights across all NPCs
    /// Dramatically reduces redundant water sampling when multiple NPCs check similar positions
    /// </summary>
    private class WaterHeightCache
    {
        private const int MAX_CACHE_ENTRIES = 32; // Balance memory vs hits
        private const float CACHE_POSITION_THRESHOLD = 2f; // Consider positions within 2m as "same"
        private const float CACHE_DURATION = 0.5f; // Cache valid for half a second

        private struct CacheEntry
        {
            public Vector3 position;
            public float height;
            public float timestamp;
        }

        private CacheEntry[] entries = new CacheEntry[MAX_CACHE_ENTRIES];
        private int nextIndex = 0;

        public bool TryGetCachedHeight(Vector3 position, out float height)
        {
            float currentTime = Time.time;

            // Search cache for matching position
            for (int i = 0; i < MAX_CACHE_ENTRIES; i++)
            {
                if (entries[i].timestamp > 0f && // Entry is valid
                    currentTime - entries[i].timestamp < CACHE_DURATION && // Not expired
                    Vector3.Distance(entries[i].position, position) < CACHE_POSITION_THRESHOLD) // Close enough
                {
                    height = entries[i].height;
                    return true;
                }
            }

            height = 0f;
            return false;
        }

        public void CacheHeight(Vector3 position, float height)
        {
            entries[nextIndex] = new CacheEntry
            {
                position = position,
                height = height,
                timestamp = Time.time
            };

            nextIndex = (nextIndex + 1) % MAX_CACHE_ENTRIES;
        }

        // Call this occasionally to clear expired entries (optional optimization)
        public void CleanExpiredEntries()
        {
            float currentTime = Time.time;
            for (int i = 0; i < MAX_CACHE_ENTRIES; i++)
            {
                if (entries[i].timestamp > 0f && currentTime - entries[i].timestamp >= CACHE_DURATION)
                {
                    entries[i].timestamp = 0f; // Mark as invalid
                }
            }
        }
    }

    #endregion

    #region Public API

    public void ForceWaterStateCheck()
    {
        if (sceneHasWater)
        {
            lastCheckTime = 0f; // Force immediate check
            CancelPendingStateChange(); // Cancel any pending changes
            CheckWaterState();
        }
    }

    public void ForceStateTransition(bool toWaterState)
    {
        if (!sceneHasWater && toWaterState)
        {
            Debug.LogWarning($"{gameObject.name}: Cannot force water state - no water in scene!");
            return;
        }

        CancelPendingStateChange();
        ExecuteStateChange(toWaterState);

        DebugLog($"Forced state transition to {(toWaterState ? "WATER" : "GROUND")}");
    }

    public string GetWaterStateInfo()
    {
        if (!sceneHasWater)
            return "No water in scene";

        string transitionInfo = IsTransitioning ? " (TRANSITIONING)" : "";

        return $"Water State: {(isInWater ? "IN WATER" : "ON GROUND")}{transitionInfo}, " +
               $"Depth: {currentDepthInWater:F2}m, " +
               $"Check Interval: {currentCheckInterval:F3}s, " +
               $"Sample Position: {GetSamplePosition()}";
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCWaterDetector-{gameObject.name}] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Vector3 samplePos = GetSamplePosition();

        // Draw sample position with state-based color
        if (Application.isPlaying)
        {
            if (IsTransitioning)
                Gizmos.color = Color.yellow;
            else if (isInWater)
                Gizmos.color = Color.blue;
            else
                Gizmos.color = Color.green;
        }
        else
        {
            Gizmos.color = Color.white;
        }

        Gizmos.DrawWireSphere(samplePos, 0.2f);

        if (Application.isPlaying && sceneHasWater)
        {
            // Draw thresholds
            Gizmos.color = Color.green;
            Vector3 entryPos = samplePos + Vector3.down * waterEntryDepth;
            Gizmos.DrawLine(entryPos + Vector3.left * 0.5f, entryPos + Vector3.right * 0.5f);

            Gizmos.color = Color.red;
            Vector3 exitPos = samplePos + Vector3.down * waterExitDepth;
            Gizmos.DrawLine(exitPos + Vector3.left * 0.3f, exitPos + Vector3.right * 0.3f);

            // Current water level
            if (cachedWaterHeight > 0f)
            {
                Gizmos.color = Color.cyan;
                Vector3 waterLevelPos = new Vector3(samplePos.x, cachedWaterHeight, samplePos.z);
                Gizmos.DrawLine(waterLevelPos + Vector3.left * 0.7f, waterLevelPos + Vector3.right * 0.7f);

                // Draw depth indicator
                if (currentDepthInWater > 0f)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(samplePos, waterLevelPos);
                }
            }
        }

        // Connection to NPC center
        Gizmos.color = Color.gray;
        Gizmos.DrawLine(transform.position, samplePos);
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        CancelPendingStateChange();
    }

    #endregion
}