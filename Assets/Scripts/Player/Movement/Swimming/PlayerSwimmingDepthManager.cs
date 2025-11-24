using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// CORRECTED: Swimming depth manager that properly handles depth states within the water state.
/// 
/// Key Understanding:
/// - Player can be in Water State while at different depths (surface swimming, treading, diving)
/// - This manager categorizes those depths for animation, rotation, and gameplay systems
/// - All depth states assume the player is already in the Water State (swimming)
/// </summary>
public class PlayerSwimmingDepthManager : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerWaterDetector waterDetector;
    [SerializeField] private bool autoFindReferences = true;

    [Header("CORRECTED: Swimming Depth Classification Thresholds")]
    [SerializeField, Range(0f, 0.8f), Tooltip("Player is at surface when chest depth is below this (surface swimming/treading)")]
    private float surfaceSwimmingDepthThreshold = 0.4f;
    [SerializeField, Range(1f, 5f), Tooltip("Player is in deep water when chest depth exceeds this (full underwater movement)")]
    private float deepUnderwaterDepthThreshold = 2f;
    [SerializeField, Range(0.1f, 1f), Tooltip("Minimum time in water state before depth transitions become active")]
    private float minTimeInWaterStateForDepthTracking = 0.3f;

    [Header("Depth State Transition Settings")]
    [SerializeField, Range(0.5f, 2f)] private float enterSwimmingTransitionDuration = 1f;
    [SerializeField, Range(0.5f, 2f)] private float exitSwimmingTransitionDuration = 1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = false;

    // CORRECTED: Swimming depth state (assumes player is already in water state)
    [ShowInInspector, ReadOnly] private SwimmingDepthState currentSwimmingDepthState = SwimmingDepthState.Disabled;
    [ShowInInspector, ReadOnly] private SwimmingDepthState previousSwimmingDepthState = SwimmingDepthState.Disabled;
    [ShowInInspector, ReadOnly] private float depthTransitionProgress = 0f;
    [ShowInInspector, ReadOnly] private float currentDepthInWater = 0f;

    // Transition timing
    private float depthTransitionStartTime = 0f;
    private float depthTransitionDuration = 0f;
    private float timeInWaterState = 0f;
    private bool wasInWaterState = false;

    // Events for SwimmingBodyRotationController and other systems
    public event System.Action<SwimmingDepthState, SwimmingDepthState> OnSwimmingDepthStateChanged;

    #region Properties

    public SwimmingDepthState CurrentSwimmingDepthState => currentSwimmingDepthState;
    public float CurrentDepthInWater => currentDepthInWater;
    public float DepthTransitionProgress => depthTransitionProgress;
    public bool IsDepthTransitioning => currentSwimmingDepthState == SwimmingDepthState.EnteringSwimming ||
                                       currentSwimmingDepthState == SwimmingDepthState.ExitingSwimming;

    // CORRECTED: Surface detection for swimming body rotation controller
    public bool IsSwimmingAtSurface => currentSwimmingDepthState == SwimmingDepthState.SurfaceSwimming;
    public bool IsSwimmingUnderwater => currentSwimmingDepthState == SwimmingDepthState.UnderwaterSwimming;

    // we'll use this to enable/disable the player's water object (enabled when the player is in water state and positioned at surface of water, disabled when underwater) to ensure 
    // that the player has realistic buoyancy in waves and choppy water
    [ShowInInspector, ReadOnly] public bool IsPlayerPositionedAtSurfaceOfWater => currentSwimmingDepthState != SwimmingDepthState.UnderwaterSwimming;

    #endregion

    #region Initialization

    private void Awake()
    {
        if (autoFindReferences)
        {
            FindReferences();
        }
    }

    private void Start()
    {
        ValidateSetup();
        InitializeSwimmingDepthState();
    }

    private void FindReferences()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (waterDetector == null)
            waterDetector = GetComponent<PlayerWaterDetector>();

        DebugLog("References found automatically");
    }

    private void ValidateSetup()
    {
        bool isValid = playerController != null && waterDetector != null;

        if (!isValid)
        {
            Debug.LogError("[PlayerSwimmingDepthManager] Missing required references!");
            enabled = false;
        }
        else
        {
            DebugLog("Setup validation passed");
        }
    }

    private void InitializeSwimmingDepthState()
    {
        currentSwimmingDepthState = SwimmingDepthState.Disabled;
        depthTransitionProgress = 0f;
        timeInWaterState = 0f;
        DebugLog("Swimming depth state initialized");
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        UpdateDepthCalculations();
        UpdateSwimmingDepthState();
        UpdateDepthTransitions();
    }

    /// <summary>
    /// Update current depth using water detector data
    /// </summary>
    private void UpdateDepthCalculations()
    {
        if (waterDetector == null) return;

        currentDepthInWater = waterDetector.ChestDepthInWater;
    }

    /// <summary>
    /// CORRECTED: Update swimming depth state based on water state and depth
    /// </summary>
    private void UpdateSwimmingDepthState()
    {
        if (waterDetector == null || playerController == null) return;

        bool isInWaterState = waterDetector.IsInWaterState;
        bool isSwimmingMode = playerController.CurrentMovementMode == MovementMode.Swimming;

        // Track time in water state
        if (isInWaterState != wasInWaterState)
        {
            timeInWaterState = 0f;
            wasInWaterState = isInWaterState;
        }
        else if (isInWaterState)
        {
            timeInWaterState += Time.deltaTime;
        }

        SwimmingDepthState newDepthState = DetermineRequiredSwimmingDepthState(isInWaterState, isSwimmingMode);

        if (newDepthState != currentSwimmingDepthState)
        {
            ChangeSwimmingDepthState(newDepthState);
        }
    }

    /// <summary>
    /// CORRECTED: Determine swimming depth state based on water state and depth
    /// </summary>
    private SwimmingDepthState DetermineRequiredSwimmingDepthState(bool isInWaterState, bool isSwimmingMode)
    {
        // CORRECTED: Only active when player is in water state (swimming)
        if (!isInWaterState || !isSwimmingMode)
        {
            // Player is not swimming - transition out or stay disabled
            if (currentSwimmingDepthState == SwimmingDepthState.UnderwaterSwimming ||
                currentSwimmingDepthState == SwimmingDepthState.SurfaceSwimming)
            {
                return SwimmingDepthState.ExitingSwimming;
            }
            return SwimmingDepthState.Disabled;
        }

        // Player is swimming - determine depth-based state
        if (isInWaterState && isSwimmingMode)
        {
            // Prevent rapid state switching when first entering water
            if (currentSwimmingDepthState == SwimmingDepthState.Disabled && timeInWaterState < minTimeInWaterStateForDepthTracking)
            {
                return SwimmingDepthState.Disabled;
            }

            // Stay in current transition state until complete
            if (currentSwimmingDepthState == SwimmingDepthState.EnteringSwimming)
            {
                return currentSwimmingDepthState;
            }

            // Start entering swimming depth tracking if we were disabled
            if (currentSwimmingDepthState == SwimmingDepthState.Disabled)
            {
                return SwimmingDepthState.EnteringSwimming;
            }

            // CORRECTED: Determine active swimming depth state based on chest depth
            bool isAtSurface = currentDepthInWater <= surfaceSwimmingDepthThreshold;
            bool isDeepUnderwater = currentDepthInWater >= deepUnderwaterDepthThreshold;

            if (isAtSurface)
            {
                return SwimmingDepthState.SurfaceSwimming;
            }
            else if (isDeepUnderwater)
            {
                return SwimmingDepthState.UnderwaterSwimming;
            }
            else
            {
                // Medium depth - still considered underwater swimming
                return SwimmingDepthState.UnderwaterSwimming;
            }
        }

        return SwimmingDepthState.Disabled;
    }

    /// <summary>
    /// Change to a new swimming depth state with transition setup
    /// </summary>
    private void ChangeSwimmingDepthState(SwimmingDepthState newDepthState)
    {
        previousSwimmingDepthState = currentSwimmingDepthState;
        currentSwimmingDepthState = newDepthState;

        // Setup transition timing
        switch (newDepthState)
        {
            case SwimmingDepthState.EnteringSwimming:
                StartDepthTransition(enterSwimmingTransitionDuration);
                break;

            case SwimmingDepthState.ExitingSwimming:
                StartDepthTransition(exitSwimmingTransitionDuration);
                break;

            case SwimmingDepthState.UnderwaterSwimming:
            case SwimmingDepthState.SurfaceSwimming:
                depthTransitionProgress = 1f;
                break;

            case SwimmingDepthState.Disabled:
                depthTransitionProgress = 0f;
                break;
        }

        OnSwimmingDepthStateChanged?.Invoke(previousSwimmingDepthState, newDepthState);
        DebugLog($"Swimming depth state changed: {previousSwimmingDepthState} -> {newDepthState}");
    }

    /// <summary>
    /// Start a depth transition with specified duration
    /// </summary>
    private void StartDepthTransition(float duration)
    {
        depthTransitionStartTime = Time.time;
        depthTransitionDuration = duration;
    }

    /// <summary>
    /// Update transition progress for entering/exiting swimming depth states
    /// </summary>
    private void UpdateDepthTransitions()
    {
        if (currentSwimmingDepthState != SwimmingDepthState.EnteringSwimming &&
            currentSwimmingDepthState != SwimmingDepthState.ExitingSwimming)
            return;

        float progress = (Time.time - depthTransitionStartTime) / depthTransitionDuration;
        depthTransitionProgress = Mathf.Clamp01(progress);

        // Check for transition completion
        if (depthTransitionProgress >= 1f)
        {
            CompleteDepthTransition();
        }
    }

    /// <summary>
    /// Complete the current depth transition
    /// </summary>
    private void CompleteDepthTransition()
    {
        switch (currentSwimmingDepthState)
        {
            case SwimmingDepthState.EnteringSwimming:
                // Determine if we should be at surface or underwater based on current depth
                bool isAtSurface = currentDepthInWater <= surfaceSwimmingDepthThreshold;
                SwimmingDepthState nextState = isAtSurface ? SwimmingDepthState.SurfaceSwimming : SwimmingDepthState.UnderwaterSwimming;
                ChangeSwimmingDepthState(nextState);
                break;

            case SwimmingDepthState.ExitingSwimming:
                ChangeSwimmingDepthState(SwimmingDepthState.Disabled);
                break;
        }
    }

    #endregion

    #region Public API - SwimmingBodyRotationController Compatibility

    /// <summary>
    /// CORRECTED: Get depth state multiplier for rotation system (0-1)
    /// Now properly represents swimming depth intensity, not water contact
    /// </summary>
    public float GetSwimmingDepthStateMultiplier()
    {
        return currentSwimmingDepthState switch
        {
            SwimmingDepthState.Disabled => 0f,
            SwimmingDepthState.EnteringSwimming => depthTransitionProgress,
            SwimmingDepthState.UnderwaterSwimming => 1f,
            SwimmingDepthState.SurfaceSwimming => 0.7f, // Reduced rotation at surface for more natural movement
            SwimmingDepthState.ExitingSwimming => 1f - depthTransitionProgress,
            _ => 0f
        };
    }

    /// <summary>
    /// Force swimming depth state for external control
    /// </summary>
    public void ForceSwimmingDepthState(SwimmingDepthState state)
    {
        DebugLog($"Forcing swimming depth state to: {state}");
        ChangeSwimmingDepthState(state);
    }

    /// <summary>
    /// Reset swimming depth state
    /// </summary>
    public void ResetSwimmingDepthState()
    {
        ChangeSwimmingDepthState(SwimmingDepthState.Disabled);
        DebugLog("Swimming depth state reset");
    }

    /// <summary>
    /// CORRECTED: Check if swimming depth state is active for rotation system
    /// </summary>
    public bool IsSwimmingDepthStateActive()
    {
        return currentSwimmingDepthState == SwimmingDepthState.UnderwaterSwimming ||
               currentSwimmingDepthState == SwimmingDepthState.SurfaceSwimming ||
               currentSwimmingDepthState == SwimmingDepthState.EnteringSwimming;
    }

    /// <summary>
    /// CORRECTED: Check if player is swimming near the surface (for reduced body rotation)
    /// </summary>
    public bool IsSwimmingNearSurface()
    {
        return currentSwimmingDepthState == SwimmingDepthState.SurfaceSwimming;
    }

    /// <summary>
    /// CORRECTED: Check if player is swimming underwater (for full body rotation)
    /// </summary>
    public bool IsSwimmingDeepUnderwater()
    {
        return currentSwimmingDepthState == SwimmingDepthState.UnderwaterSwimming;
    }

    /// <summary>
    /// Set custom swimming depth thresholds for different scenarios
    /// </summary>
    public void SetSwimmingDepthThresholds(float surfaceThreshold, float deepThreshold)
    {
        surfaceSwimmingDepthThreshold = Mathf.Max(0f, surfaceThreshold);
        deepUnderwaterDepthThreshold = Mathf.Max(surfaceSwimmingDepthThreshold + 0.5f, deepThreshold);

        DebugLog($"Swimming depth thresholds updated - Surface: {surfaceSwimmingDepthThreshold:F2}m, " +
                $"Deep: {deepUnderwaterDepthThreshold:F2}m");
    }

    /// <summary>
    /// CORRECTED: Get comprehensive debug information
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== CORRECTED SWIMMING DEPTH MANAGER ===");
        info.AppendLine($"Current Swimming Depth State: {currentSwimmingDepthState}");
        info.AppendLine($"Previous Swimming Depth State: {previousSwimmingDepthState}");
        info.AppendLine($"Depth Transition Progress: {depthTransitionProgress:F2}");
        info.AppendLine($"Current Depth In Water: {currentDepthInWater:F2}m");
        info.AppendLine($"Time In Water State: {timeInWaterState:F2}s");
        info.AppendLine($"Is Swimming At Surface: {IsSwimmingNearSurface()}");
        info.AppendLine($"Is Swimming Deep Underwater: {IsSwimmingDeepUnderwater()}");
        info.AppendLine($"Is Swimming Depth State Active: {IsSwimmingDepthStateActive()}");
        info.AppendLine($"Swimming Depth State Multiplier: {GetSwimmingDepthStateMultiplier():F2}");

        // Show threshold values
        info.AppendLine($"=== SWIMMING DEPTH THRESHOLDS ===");
        info.AppendLine($"Surface Swimming Threshold: {surfaceSwimmingDepthThreshold:F2}m");
        info.AppendLine($"Deep Underwater Threshold: {deepUnderwaterDepthThreshold:F2}m");
        info.AppendLine($"Min Time In Water State: {minTimeInWaterStateForDepthTracking:F2}s");

        return info.ToString();
    }

    /// <summary>
    /// Get swimming depth classification as string for debugging
    /// </summary>
    public string GetSwimmingDepthClassification()
    {
        if (!IsSwimmingDepthStateActive())
        {
            return "Not Swimming";
        }

        return currentSwimmingDepthState switch
        {
            SwimmingDepthState.SurfaceSwimming => $"Surface Swimming (Depth: {currentDepthInWater:F2}m)",
            SwimmingDepthState.UnderwaterSwimming => $"Underwater Swimming (Depth: {currentDepthInWater:F2}m)",
            SwimmingDepthState.EnteringSwimming => $"Entering Swimming State (Progress: {depthTransitionProgress:F2})",
            SwimmingDepthState.ExitingSwimming => $"Exiting Swimming State (Progress: {depthTransitionProgress:F2})",
            _ => "Unknown Swimming State"
        };
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerSwimmingDepthManager] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        // Draw swimming depth state indicator
        Gizmos.color = currentSwimmingDepthState switch
        {
            SwimmingDepthState.Disabled => Color.red,
            SwimmingDepthState.EnteringSwimming => Color.yellow,
            SwimmingDepthState.UnderwaterSwimming => Color.blue,
            SwimmingDepthState.SurfaceSwimming => Color.cyan,
            SwimmingDepthState.ExitingSwimming => Color.gray,
            _ => Color.white
        };

        // Draw a cube indicator above the player
        Vector3 indicatorPos = transform.position + Vector3.up * 2.5f;
        Gizmos.DrawWireCube(indicatorPos, Vector3.one * 0.3f);

        // Draw swimming depth thresholds if actively swimming
        if (IsSwimmingDepthStateActive() && waterDetector != null)
        {
            Vector3 chestPos = waterDetector.ChestDetectionPosition;

            // Surface swimming threshold
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(chestPos + Vector3.down * surfaceSwimmingDepthThreshold,
                               new Vector3(0.8f, 0.05f, 0.8f));

            // Deep underwater threshold  
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(chestPos + Vector3.down * deepUnderwaterDepthThreshold,
                               new Vector3(1f, 0.05f, 1f));
        }
    }

    #endregion
}

/// <summary>
/// CORRECTED: Swimming depth states for players who are already in the water state.
/// These states classify different depths/types of swimming behavior.
/// </summary>
public enum SwimmingDepthState
{
    Disabled,           // Not in water state - no swimming depth tracking
    EnteringSwimming,   // Gradually enabling swimming depth tracking (water state entry transition)
    SurfaceSwimming,    // Swimming at or near surface (treading water, surface swimming)
    UnderwaterSwimming, // Swimming underwater (diving, deep swimming)
    ExitingSwimming     // Transitioning out of water state (water state exit transition)
}