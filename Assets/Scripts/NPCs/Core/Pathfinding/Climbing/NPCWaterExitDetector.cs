using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// ULTRA-OPTIMIZED water exit detector using centralized registry.
/// Designed for 20+ NPCs with minimal performance impact.
/// 
/// KEY OPTIMIZATIONS:
/// - Uses WaterLedgeRegistry (no FindObjectsByType calls)
/// - Spatial partitioning for fast nearby queries
/// - Staggered scan intervals across NPCs
/// - Cached path evaluation results
/// - Early exit conditions to minimize work
/// </summary>
public class NPCWaterExitDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPCController npcController;

    [Header("Detection Settings")]
    [SerializeField, Tooltip("Maximum distance to search for water exits")]
    private float maxDetectionRange = 60f;

    [SerializeField, Tooltip("How often to scan for exits (seconds)")]
    private float scanInterval = 1f;

    [Header("Path Evaluation")]
    [SerializeField, Tooltip("Timeout for path calculations (seconds)")]
    private float pathCalculationTimeout = 2f;

    [SerializeField, Tooltip("Maximum number of ledges to evaluate per scan (performance)")]
    private int maxLedgesToEvaluate = 5;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool showGizmos = true;

    // Detection state
    private float lastScanTime = -999f;
    [ShowInInspector] private ClimbableWaterLedge bestExitLedge;
    private Vector3 bestExitEntryPoint;
    private float bestExitDistance;
    [ShowInInspector] private bool hasValidExit;

    // Path evaluation state
    private bool isEvaluatingPaths = false;
    private int pathsEvaluated = 0;
    private int pathsToEvaluate = 0;
    private Dictionary<ClimbableWaterLedge, float> ledgePathLengths = new Dictionary<ClimbableWaterLedge, float>();

    // OPTIMIZATION: Cache last target position to avoid unnecessary scans
    private Vector3 lastTargetPosition;
    private bool targetHasMoved = false;

    // Properties
    public bool HasValidExit => hasValidExit;
    public ClimbableWaterLedge BestExitLedge => bestExitLedge;
    public Vector3 BestExitEntryPoint => bestExitEntryPoint;
    public float BestExitDistance => bestExitDistance;

    private void Awake()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        if (npcController == null)
            npcController = GetComponent<NPCController>();

        if (npcController == null)
        {
            Debug.LogError($"{gameObject.name}: NPCWaterExitDetector requires NPCController component!");
            enabled = false;
            return;
        }

        // OPTIMIZATION: Stagger scan times across NPCs to spread the load
        lastScanTime = -Random.Range(0f, scanInterval);

        DebugLog("Water exit detector initialized with registry-based search");
    }

    private void Update()
    {
        // Early exit: Only scan when in water
        if (!npcController.waterDetector.IsInWater)
        {
            //            Debug.Log("Not in water, skipping water exit scan");
            if (hasValidExit)
            {
                ClearExitData();
            }

            //reset state
            isEvaluatingPaths = false;
            return;
        }

        // Early exit: Don't scan if already evaluating paths
        if (isEvaluatingPaths)
        {
            //  Debug.Log("Already evaluating paths, skipping scan");
            return;
        }

        // Check if target has moved significantly (invalidate cache)
        if (npcController.target != null)
        {
            Vector3 currentTargetPos = npcController.target.position;
            if (Vector3.Distance(currentTargetPos, lastTargetPosition) > 1f)
            {
                targetHasMoved = true;
                lastTargetPosition = currentTargetPos;
            }
        }

        // Periodic scanning or immediate scan if target moved
        if (Time.time - lastScanTime >= scanInterval || targetHasMoved)
        {
            lastScanTime = Time.time;
            targetHasMoved = false;
            ScanForWaterExits();
        }
    }

    /// <summary>
    /// OPTIMIZED: Scans using centralized registry instead of FindObjectsByType
    /// </summary>
    private void ScanForWaterExits()
    {
        //Debug.Log("Scanning for water exits...");
        // Early exit: Check if target exists and is on land
        if (npcController.target == null)
        {
            //Debug.Log("npcController.target is null, skipping water exit scan");
            ClearExitData();
            return;
        }

        Vector3 targetPosition = npcController.target.position;
        bool targetIsOnLand = npcController.waterDetector.IsPositionAboveWater(targetPosition);

        if (!targetIsOnLand)
        {
            //Debug.Log("!targetIsOnLand, skipping water exit scan");
            ClearExitData();
            return;
        }

        // OPTIMIZATION: Use registry spatial query instead of FindObjectsByType
        Vector3 npcPosition = transform.position;
        List<ClimbableWaterLedge> ledgesInRange = WaterLedgeRegistry.GetLedgesNearPosition(
            npcPosition,
            maxDetectionRange
        );

        if (ledgesInRange.Count == 0)
        {
            //Debug.Log("ledgesInRange.Count == 0, no exits found");
            ClearExitData();
            return;
        }

        // OPTIMIZATION: Sort by distance and only evaluate closest N ledges
        ledgesInRange.Sort((a, b) =>
        {
            float distA = (a.EndPosition - npcPosition).sqrMagnitude;
            float distB = (b.EndPosition - npcPosition).sqrMagnitude;
            return distA.CompareTo(distB);
        });

        // Limit evaluation to closest ledges only
        int ledgesToEvaluate = Mathf.Min(maxLedgesToEvaluate, ledgesInRange.Count);
        List<ClimbableWaterLedge> candidateLedges = ledgesInRange.GetRange(0, ledgesToEvaluate);


        // Debug.Log($"Found {ledgesInRange.Count} exits in range, evaluating closest {candidateLedges.Count}");


        // Start path evaluation for candidate ledges
        StartPathEvaluation(candidateLedges, targetPosition);
    }

    /// <summary>
    /// Start asynchronous path evaluation for candidate ledges
    /// </summary>
    private void StartPathEvaluation(List<ClimbableWaterLedge> ledges, Vector3 targetPosition)
    {
        // Debug.Log("Starting path evaluation for candidate ledges...");
        isEvaluatingPaths = true;
        pathsEvaluated = 0;
        pathsToEvaluate = ledges.Count;
        ledgePathLengths.Clear();

        float evaluationStartTime = Time.time;

        foreach (var ledge in ledges)
        {
            // Each ledge calculates path from its TOP position to the target
            ledge.CalculatePathToTarget(targetPosition, (pathLength) =>
            {
                // Check for timeout
                if (Time.time - evaluationStartTime > pathCalculationTimeout)
                {
                    DebugLog("Path evaluation timeout reached");
                    FinishPathEvaluation();
                    return;
                }

                // Store result
                ledgePathLengths[ledge] = pathLength;
                pathsEvaluated++;

                // Check if all paths evaluated
                if (pathsEvaluated >= pathsToEvaluate)
                {
                    FinishPathEvaluation();
                }
            });
        }
    }

    /// <summary>
    /// Finish path evaluation and select the best ledge
    /// </summary>
    private void FinishPathEvaluation()
    {
        // Debug.Log("Finish Path Evaluation");
        isEvaluatingPaths = false;

        if (ledgePathLengths.Count == 0)
        {
            // Debug.Log("Finish Path Evaluation: ledgePathLengths.Count == 0, returning null");
            ClearExitData();
            return;
        }

        // Find ledge with shortest path
        ClimbableWaterLedge bestLedge = null;
        float shortestPath = float.MaxValue;

        foreach (var kvp in ledgePathLengths)
        {
            if (kvp.Value < shortestPath)
            {
                shortestPath = kvp.Value;
                bestLedge = kvp.Key;
            }
        }

        if (bestLedge != null && shortestPath < float.MaxValue)
        {
            // Update best exit
            bool isNewExit = bestExitLedge != bestLedge;

            bestExitLedge = bestLedge;
            bestExitEntryPoint = bestLedge.EndPosition;
            bestExitDistance = Vector3.Distance(transform.position, bestExitEntryPoint);
            hasValidExit = true;

            if (isNewExit && showDebugInfo)
            {
                DebugLog($"Found best water exit: {bestLedge.gameObject.name} at {bestExitEntryPoint} " +
                        $"(distance: {bestExitDistance:F2}m, path: {shortestPath:F2}m)");
            }
        }
        else
        {
            ClearExitData();
        }

        // Clear evaluation data
        ledgePathLengths.Clear();
    }

    /// <summary>
    /// Clear exit data
    /// </summary>
    private void ClearExitData()
    {
        //Debug.Log("Cleared water exit data");

        hasValidExit = false;
        bestExitLedge = null;
        bestExitEntryPoint = Vector3.zero;
        bestExitDistance = 0f;
    }

    /// <summary>
    /// Force an immediate scan for water exits
    /// </summary>
    public void ForceImmediateScan()
    {
        lastScanTime = 0f;
        targetHasMoved = true;
        ScanForWaterExits();
    }

    /// <summary>
    /// Check if a specific position would benefit from using a water exit
    /// </summary>
    public bool ShouldUseWaterExit(Vector3 targetPosition)
    {
        if (!hasValidExit)
        {
            //Debug.Log("hasValidExit is false");
            return false;
        }

        if (!npcController.waterDetector.IsPositionAboveWater(targetPosition))
        {
            //Debug.Log("Target position is not on land, no need for water exit");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Get all detected exits within range (for debugging/visualization)
    /// OPTIMIZED: Uses registry query
    /// </summary>
    public List<ClimbableWaterLedge> GetAllDetectedExits()
    {
        return WaterLedgeRegistry.GetLedgesNearPosition(transform.position, maxDetectionRange);
    }

    /// <summary>
    /// Set custom scan interval for this NPC (for LOD/importance-based optimization)
    /// </summary>
    public void SetScanInterval(float interval)
    {
        scanInterval = Mathf.Max(0.1f, interval);
    }

    /// <summary>
    /// Set max ledges to evaluate per scan (for LOD/importance-based optimization)
    /// </summary>
    public void SetMaxLedgesToEvaluate(int max)
    {
        maxLedgesToEvaluate = Mathf.Max(1, max);
    }

    #region Debug

    private void DebugLog(string message)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[NPCWaterExitDetector-{gameObject.name}] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || !Application.isPlaying) return;

        if (npcController == null || !npcController.waterDetector.IsInWater)
            return;

        // Draw detection range
        Gizmos.color = new Color(0, 1, 1, 0.1f);
        Gizmos.DrawWireSphere(transform.position, maxDetectionRange);

        // Draw best exit if we have one
        if (hasValidExit && bestExitLedge != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, bestExitEntryPoint);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(bestExitEntryPoint, 0.5f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(bestExitEntryPoint, bestExitLedge.TopPosition);
        }

        // Show path evaluation status
        if (isEvaluatingPaths)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || !Application.isPlaying) return;

        if (npcController == null || !npcController.waterDetector.IsInWater)
            return;

        // Draw all detected exits
        var allExits = GetAllDetectedExits();
        foreach (var exit in allExits)
        {
            if (exit == null) continue;

            Vector3 entryPoint = exit.EndPosition;
            bool isBest = exit == bestExitLedge;

            Gizmos.color = isBest ? new Color(0, 1, 0, 0.8f) : new Color(1, 1, 0, 0.3f);
            Gizmos.DrawLine(transform.position, entryPoint);

            Gizmos.color = isBest ? Color.yellow : new Color(1, 1, 0, 0.5f);
            Gizmos.DrawWireSphere(entryPoint, isBest ? 0.5f : 0.3f);
        }

        // Draw detection range with detail
        Gizmos.color = new Color(0, 1, 1, 0.2f);
        Gizmos.DrawWireSphere(transform.position, maxDetectionRange);
        Gizmos.DrawWireSphere(transform.position, maxDetectionRange * 0.5f);
    }

    #endregion
}