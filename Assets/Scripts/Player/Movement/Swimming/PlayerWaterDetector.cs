using UnityEngine;
using Crest;

/// <summary>
/// ROTATION-INDEPENDENT: PlayerWaterDetector now calculates detection points
/// relative to the main player transform, ignoring body rotation from swimming controller.
/// This ensures consistent water detection regardless of body orientation.
/// </summary>
public class PlayerWaterDetector : MonoBehaviour
{

    [Header("Scene Compatibility")]
    [SerializeField] private bool forceWaterDetectionOff = false;
    [SerializeField] private float sceneWaterCheckDelay = 0.5f;

    [Header("ROTATION-INDEPENDENT: Detection Point Offsets")]
    [SerializeField, Tooltip("Chest detection point offset from player center (in local space) - for rotation-independent detection")]
    private Vector3 chestPointOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField, Tooltip("Head detection point offset from player center (in local space) - for rotation-independent detection")]
    private Vector3 headPointOffset = new Vector3(0f, 0.7f, 0f);
    [SerializeField, Tooltip("Feet detection point offset from player center (in local space) - for rotation-independent detection")]
    private Vector3 feetPointOffset = new Vector3(0f, -0.8f, 0f);

    [Header("Water State Thresholds")]
    [SerializeField, Tooltip("Player enters WATER STATE when chest is this deep")]
    private float waterStateEntryDepthThreshold = 0.1f;
    [SerializeField, Tooltip("Player exits WATER STATE when ALL points are above this depth")]
    private float waterStateExitDepthThreshold = 0f; // Fixed to 0 as you discovered
    [SerializeField, Tooltip("Head is considered underwater when this deep")]
    private float headSubmersionDepthThreshold = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showDetailedDebug = false;
    [SerializeField] private bool showRotationDebug = false;

    // Scene water system tracking
    private bool sceneHasWater = false;
    private bool hasCheckedForWater = false;

    // Crest water sampling helpers
    private SampleHeightHelper chestSampleHelper;
    private SampleHeightHelper headSampleHelper;
    private SampleHeightHelper feetSampleHelper;
    private SampleHeightHelper surfaceSampleHelper;
    private OceanRenderer oceanRenderer;

    // Water state tracking - immediate transitions
    private bool isPlayerInWaterState = false;
    private bool isHeadUnderwater = false;
    private bool wasPlayerInWaterState = false;
    private bool wasHeadUnderwater = false;

    // ROTATION-INDEPENDENT: Calculated detection positions
    private Vector3 chestDetectionPosition;
    private Vector3 headDetectionPosition;
    private Vector3 feetDetectionPosition;

    // Water height and depth data
    private float waterHeightAtChest;
    private float waterHeightAtHead;
    private float waterHeightAtFeet;
    private float chestDepthInWater;
    private float headDepthInWater;
    private float feetDepthInWater;

    // Events - immediate transitions
    public event System.Action OnWaterStateEntered;  // Player starts swimming (immediate)
    public event System.Action OnWaterStateExited;   // Player stops swimming (immediate)
    public event System.Action OnHeadSubmerged;      // Head goes underwater while swimming
    public event System.Action OnHeadSurfaced;       // Head surfaces while swimming

    // Public properties - immediate water state
    public bool IsInWaterState => sceneHasWater && isPlayerInWaterState;
    public bool IsHeadUnderwater => sceneHasWater && isHeadUnderwater;
    public float ChestDepthInWater => sceneHasWater ? chestDepthInWater : 0f;
    public float HeadDepthInWater => sceneHasWater ? headDepthInWater : 0f;
    public float FeetDepthInWater => sceneHasWater ? feetDepthInWater : 0f;

    // Swimming state properties for surface systems
    public bool IsSwimmingAtSurface => IsInWaterState && !IsHeadUnderwater;
    public bool IsSwimmingUnderwater => IsInWaterState && IsHeadUnderwater;
    public float WaterHeightAtChest => sceneHasWater ? waterHeightAtChest : 0f;
    public float WaterHeightAtHead => sceneHasWater ? waterHeightAtHead : 0f;
    public float WaterHeightAtFeet => sceneHasWater ? waterHeightAtFeet : 0f;

    // ROTATION-INDEPENDENT: Detection position accessors
    public Vector3 ChestDetectionPosition => chestDetectionPosition;
    public Vector3 HeadDetectionPosition => headDetectionPosition;
    public Vector3 FeetDetectionPosition => feetDetectionPosition;

    // Scene compatibility
    public bool SceneHasWater => sceneHasWater;
    public float HeadDepth => HeadDepthInWater;

    #region Initialization

    private void Start()
    {
        StartCoroutine(DelayedWaterSystemInitialization());
    }

    private System.Collections.IEnumerator DelayedWaterSystemInitialization()
    {
        yield return new WaitForSecondsRealtime(sceneWaterCheckDelay);

        CheckSceneForWater();

        if (sceneHasWater && !forceWaterDetectionOff)
        {
            InitializeCrestComponents();
        }
        else
        {
            InitializeNonWaterScene();
        }

        ValidateSetup();
        hasCheckedForWater = true;

        DebugLog($"Water detection initialization complete - Scene has water: {sceneHasWater}");
    }

    private void CheckSceneForWater()
    {
        oceanRenderer = FindFirstObjectByType<OceanRenderer>();
        sceneHasWater = oceanRenderer != null && !forceWaterDetectionOff;

        DebugLog(sceneHasWater ? "Scene has water - enabling rotation-independent detection" : "Scene has no water");
    }

    private void InitializeCrestComponents()
    {
        if (oceanRenderer == null) return;

        try
        {
            // Create sample helpers for all detection points
            chestSampleHelper = new SampleHeightHelper();
            headSampleHelper = new SampleHeightHelper();
            feetSampleHelper = new SampleHeightHelper();
            surfaceSampleHelper = new SampleHeightHelper();

            // Test initialization
            chestSampleHelper.Init(transform.position, 0f, false, this);

            DebugLog("Crest components initialized successfully with rotation-independent detection");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerWaterDetector] Failed to initialize Crest: {e.Message}");
            InitializeNonWaterScene();
        }
    }

    private void InitializeNonWaterScene()
    {
        // Reset all water state
        isPlayerInWaterState = false;
        isHeadUnderwater = false;
        wasPlayerInWaterState = false;
        wasHeadUnderwater = false;
        chestDepthInWater = 0f;
        headDepthInWater = 0f;
        feetDepthInWater = 0f;
        waterHeightAtChest = 0f;
        waterHeightAtHead = 0f;
        waterHeightAtFeet = 0f;

        DebugLog("Non-water scene initialization complete");
    }

    private void ValidateSetup()
    {
        bool isValid = true;

        if (sceneHasWater && !forceWaterDetectionOff)
        {
            isValid = chestSampleHelper != null && headSampleHelper != null && feetSampleHelper != null;
        }

        if (!isValid)
        {
            Debug.LogError("[PlayerWaterDetector] Setup validation failed - missing sample helpers");
        }
        else
        {
            DebugLog("Setup validation passed with rotation-independent detection");
        }
    }

    #endregion

    #region ROTATION-INDEPENDENT: Water State Detection

    private void Update()
    {
        if (sceneHasWater && hasCheckedForWater)
        {
            UpdateDetectionPositions();
            UpdateWaterDetection();
            CheckWaterStateChanges();
        }
        else if (hasCheckedForWater && !sceneHasWater)
        {
            EnsureNotInWaterState();
        }
    }

    /// <summary>
    /// ROTATION-INDEPENDENT: Calculate detection positions based on player's main transform
    /// This ignores any body rotation from the swimming controller
    /// </summary>
    private void UpdateDetectionPositions()
    {
        // Calculate positions relative to the main player transform (ignores body rotation)
        Vector3 playerPosition = transform.position;

        // Use only the Y rotation from the main transform (horizontal rotation)
        // Ignore X and Z rotations (body rotation from swimming controller)
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 up = Vector3.up;

        // Calculate world positions using only horizontal rotation
        chestDetectionPosition = playerPosition +
            (right * chestPointOffset.x) +
            (up * chestPointOffset.y) +
            (forward * chestPointOffset.z);

        headDetectionPosition = playerPosition +
            (right * headPointOffset.x) +
            (up * headPointOffset.y) +
            (forward * headPointOffset.z);

        feetDetectionPosition = playerPosition +
            (right * feetPointOffset.x) +
            (up * feetPointOffset.y) +
            (forward * feetPointOffset.z);

        // Debug rotation independence
        if (showRotationDebug && enableDebugLogs && Time.frameCount % 60 == 0)
        {
            float bodyRotationX = transform.eulerAngles.x;
            DebugLog($"Rotation Independence - Body X Rotation: {bodyRotationX:F1}°, " +
                    $"Detection points calculated from main transform only");
        }
    }

    private void UpdateWaterDetection()
    {
        // Sample water height at calculated detection positions (rotation-independent)
        waterHeightAtChest = SampleWaterHeightAtPosition(chestDetectionPosition, chestSampleHelper);
        chestDepthInWater = Mathf.Max(0f, waterHeightAtChest - chestDetectionPosition.y);

        waterHeightAtHead = SampleWaterHeightAtPosition(headDetectionPosition, headSampleHelper);
        headDepthInWater = Mathf.Max(0f, waterHeightAtHead - headDetectionPosition.y);

        waterHeightAtFeet = SampleWaterHeightAtPosition(feetDetectionPosition, feetSampleHelper);
        feetDepthInWater = Mathf.Max(0f, waterHeightAtFeet - feetDetectionPosition.y);

        UpdateWaterStateFlags();
    }

    /// <summary>
    /// SIMPLIFIED: Update water state flags with three-point immediate logic
    /// </summary>
    private void UpdateWaterStateFlags()
    {
        wasPlayerInWaterState = isPlayerInWaterState;
        wasHeadUnderwater = isHeadUnderwater;

        // Three-point water state logic
        if (!isPlayerInWaterState)
        {
            // Not in water state - check for entry when chest enters water
            isPlayerInWaterState = chestDepthInWater > waterStateEntryDepthThreshold;
        }
        else
        {
            // In water state - only exit when ALL detection points are out of water
            bool chestOutOfWater = chestDepthInWater <= waterStateExitDepthThreshold;
            bool headOutOfWater = headDepthInWater <= waterStateExitDepthThreshold;
            bool feetOutOfWater = feetDepthInWater <= waterStateExitDepthThreshold;

            // Stay in water state unless ALL points are out
            isPlayerInWaterState = !(chestOutOfWater && headOutOfWater && feetOutOfWater);
        }

        // Head submersion state (for underwater vs surface swimming distinction)
        isHeadUnderwater = headDepthInWater > headSubmersionDepthThreshold;

        // Debug logging for rotation-independent detection
        if (showDetailedDebug && enableDebugLogs && Time.frameCount % 60 == 0)
        {
            DebugLog($"Rotation-independent detection - Chest: {chestDepthInWater:F2}m, " +
                    $"Head: {headDepthInWater:F2}m, Feet: {feetDepthInWater:F2}m, WaterState: {isPlayerInWaterState}");
        }
    }

    /// <summary>
    /// Check for immediate water state changes
    /// </summary>
    private void CheckWaterStateChanges()
    {
        // Immediate water state entry/exit events
        if (isPlayerInWaterState != wasPlayerInWaterState)
        {
            if (isPlayerInWaterState)
            {
                DebugLog($"Water state entered (rotation-independent) - chest depth: {chestDepthInWater:F2}m");
                OnWaterStateEntered?.Invoke();
            }
            else
            {
                DebugLog($"Water state exited (rotation-independent) - all points out of water " +
                        $"(chest: {chestDepthInWater:F2}m, head: {headDepthInWater:F2}m, feet: {feetDepthInWater:F2}m)");
                OnWaterStateExited?.Invoke();
            }
        }

        // Head submersion events (for underwater vs surface swimming distinction)
        if (isHeadUnderwater != wasHeadUnderwater)
        {
            if (isHeadUnderwater)
            {
                DebugLog($"Head submerged (rotation-independent) - head depth: {headDepthInWater:F2}m");
                OnHeadSubmerged?.Invoke();
            }
            else
            {
                DebugLog($"Head surfaced (rotation-independent) - head depth: {headDepthInWater:F2}m");
                OnHeadSurfaced?.Invoke();
            }
        }
    }

    private void EnsureNotInWaterState()
    {
        if (isPlayerInWaterState || isHeadUnderwater)
        {
            bool wasPlayerInWaterStatePreviously = isPlayerInWaterState;
            bool wasPlayerHeadUnder = isHeadUnderwater;

            isPlayerInWaterState = false;
            isHeadUnderwater = false;

            if (wasPlayerInWaterStatePreviously)
                OnWaterStateExited?.Invoke();

            if (wasPlayerHeadUnder)
                OnHeadSurfaced?.Invoke();
        }
    }

    #endregion

    #region Water Sampling (Unchanged)

    /// <summary>
    /// Sample water height at a specific position using Crest
    /// </summary>
    private float SampleWaterHeightAtPosition(Vector3 worldPosition, SampleHeightHelper helper)
    {
        if (helper == null || oceanRenderer == null || !sceneHasWater)
            return 0f;

        try
        {
            helper.Init(worldPosition, 0f, false, this);

            if (helper.Sample(out float waterHeight))
            {
                return waterHeight;
            }

            return oceanRenderer.SeaLevel;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerWaterDetector] Error sampling water: {e.Message}");
            return 0f;
        }
    }

    /// <summary>
    /// Get water surface height at player's current XZ position for bobbing
    /// </summary>
    public float GetWaterSurfaceHeightAtPlayer()
    {
        if (!sceneHasWater || surfaceSampleHelper == null) return 0f;

        Vector3 playerXZ = new Vector3(transform.position.x, 0f, transform.position.z);
        return SampleWaterHeightAtPosition(playerXZ, surfaceSampleHelper);
    }

    /// <summary>
    /// Get water depth at any position (utility method)
    /// </summary>
    public float GetWaterDepthAtPosition(Vector3 worldPosition)
    {
        if (!sceneHasWater) return 0f;

        float waterHeight = SampleWaterHeightAtPosition(worldPosition, surfaceSampleHelper);
        return Mathf.Max(0f, waterHeight - worldPosition.y);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Force a water state check (useful for external systems)
    /// </summary>
    public void ForceWaterStateCheck()
    {
        if (sceneHasWater && hasCheckedForWater)
        {
            UpdateDetectionPositions();
            UpdateWaterDetection();
            CheckWaterStateChanges();
        }
        else if (!sceneHasWater)
        {
            EnsureNotInWaterState();
        }
    }

    /// <summary>
    /// Get water state info with rotation-independent detection
    /// </summary>
    public string GetWaterStateInfo()
    {
        if (!sceneHasWater)
        {
            return "No water in scene - detection disabled";
        }

        return $"Water State (Rotation-Independent) - Swimming: {isPlayerInWaterState}, " +
               $"Surface Swimming: {IsSwimmingAtSurface}, Underwater Swimming: {IsSwimmingUnderwater}\n" +
               $"Detection Depths - Chest: {chestDepthInWater:F2}m, Head: {headDepthInWater:F2}m, " +
               $"Feet: {feetDepthInWater:F2}m\n" +
               $"Water Heights - Chest: {waterHeightAtChest:F2}, Head: {waterHeightAtHead:F2}, " +
               $"Feet: {waterHeightAtFeet:F2}";
    }

    /// <summary>
    /// Set custom water state transition thresholds for different scenarios
    /// </summary>
    public void SetWaterStateTransitionThresholds(float entryThreshold, float exitThreshold)
    {
        waterStateEntryDepthThreshold = Mathf.Max(0f, entryThreshold);
        waterStateExitDepthThreshold = exitThreshold;

        DebugLog($"Water state transition thresholds updated - Entry: {waterStateEntryDepthThreshold:F2}m, " +
                $"Exit: {waterStateExitDepthThreshold:F2}m");
    }

    /// <summary>
    /// Set custom detection point offsets (useful for different character sizes)
    /// </summary>
    public void SetDetectionPointOffsets(Vector3 chest, Vector3 head, Vector3 feet)
    {
        chestPointOffset = chest;
        headPointOffset = head;
        feetPointOffset = feet;

        DebugLog($"Detection point offsets updated - Chest: {chest}, Head: {head}, Feet: {feet}");
    }

    /// <summary>
    /// Check if all detection points are out of water (for ground state validation)
    /// </summary>
    public bool AreAllPointsOutOfWater()
    {
        if (!sceneHasWater) return true;

        bool chestOut = chestDepthInWater <= waterStateExitDepthThreshold;
        bool headOut = headDepthInWater <= waterStateExitDepthThreshold;
        bool feetOut = feetDepthInWater <= waterStateExitDepthThreshold;

        return chestOut && headOut && feetOut;
    }

    /// <summary>
    /// Get detailed detection info for debugging
    /// </summary>
    public string GetThreePointDetectionInfo()
    {
        return $"Rotation-Independent Water Detection:\n" +
               $"  Chest: {(chestDepthInWater > waterStateExitDepthThreshold ? "IN" : "OUT")} " +
               $"(depth: {chestDepthInWater:F2}m) Pos: {chestDetectionPosition}\n" +
               $"  Head: {(headDepthInWater > waterStateExitDepthThreshold ? "IN" : "OUT")} " +
               $"(depth: {headDepthInWater:F2}m) Pos: {headDetectionPosition}\n" +
               $"  Feet: {(feetDepthInWater > waterStateExitDepthThreshold ? "IN" : "OUT")} " +
               $"(depth: {feetDepthInWater:F2}m) Pos: {feetDetectionPosition}\n" +
               $"All Points Out: {AreAllPointsOutOfWater()}\n" +
               $"Water State: {isPlayerInWaterState}";
    }

    /// <summary>
    /// Manual scene water refresh
    /// </summary>
    public void RefreshSceneWaterDetection()
    {
        hasCheckedForWater = false;
        StartCoroutine(DelayedWaterSystemInitialization());
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerWaterDetector] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // ROTATION-INDEPENDENT: Draw calculated detection positions (not bone positions)
        if (Application.isPlaying && sceneHasWater)
        {
            // Draw chest detection position
            Gizmos.color = isPlayerInWaterState ? Color.blue : Color.yellow;
            Gizmos.DrawWireSphere(chestDetectionPosition, 0.15f);

            // Draw head detection position
            Gizmos.color = isHeadUnderwater ? Color.red : Color.green;
            Gizmos.DrawWireSphere(headDetectionPosition, 0.1f);

            // Draw feet detection position
            Gizmos.color = (feetDepthInWater > waterStateExitDepthThreshold) ? Color.cyan : Color.gray;
            Gizmos.DrawWireSphere(feetDetectionPosition, 0.1f);

            // Draw threshold indicators
            Gizmos.color = Color.green;
            Gizmos.DrawLine(
                chestDetectionPosition + Vector3.left * 0.5f + Vector3.down * waterStateEntryDepthThreshold,
                chestDetectionPosition + Vector3.right * 0.5f + Vector3.down * waterStateEntryDepthThreshold
            );

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(
                chestDetectionPosition + Vector3.left * 0.7f + Vector3.down * waterStateExitDepthThreshold,
                chestDetectionPosition + Vector3.right * 0.7f + Vector3.down * waterStateExitDepthThreshold
            );
        }
        else if (!Application.isPlaying)
        {
            // Show preview of detection points in editor
            Vector3 playerPos = transform.position;

            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(playerPos + chestPointOffset, 0.15f);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerPos + headPointOffset, 0.1f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(playerPos + feetPointOffset, 0.1f);
        }
    }

    #endregion
}