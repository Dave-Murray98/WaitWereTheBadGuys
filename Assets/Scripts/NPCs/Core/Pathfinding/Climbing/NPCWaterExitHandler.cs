using UnityEngine;
using System.Collections;

/// <summary>
/// Handles water exit transitions by coordinating with the climbing controller.
/// Does NOT enable FollowerEntity - that's handled by climbing state.
/// </summary>
public class NPCWaterExitHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPCController npcController;
    [SerializeField] private NPCWaterExitDetector exitDetector;
    [SerializeField] private NPCMovementStateMachine stateMachine;
    [SerializeField] private NPCLedgeClimbingController climbingController;

    [Header("Transition Settings")]
    [SerializeField, Tooltip("How often to check if we're close enough to climb (seconds)")]
    private float proximityCheckInterval = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showGizmos = true;

    private ClimbableWaterLedge targetExitLedge;
    private bool isTargetingWaterExit = false;
    private float lastProximityCheck = 0f;

    // ledge queueing
    private string targetLedgeID;
    private bool isWaitingInQueue = false;


    private void Awake()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        if (npcController == null)
            npcController = GetComponent<NPCController>();

        if (exitDetector == null)
            exitDetector = GetComponent<NPCWaterExitDetector>();

        if (stateMachine == null)
            stateMachine = GetComponent<NPCMovementStateMachine>();

        if (climbingController == null)
            climbingController = GetComponent<NPCLedgeClimbingController>();

        if (npcController == null || exitDetector == null || stateMachine == null || climbingController == null)
        {
            Debug.LogError($"{gameObject.name}: NPCWaterExitHandler requires NPCController, NPCWaterExitDetector, NPCMovementStateMachine, and NPCLedgeClimbingController!");
            enabled = false;
            return;
        }

        DebugLog("Water exit handler initialized");
    }

    private void Update()
    {
        // Only active when in water state
        if (!stateMachine.IsInWater)
        {
            isTargetingWaterExit = false;
            return;
        }

        // Check proximity at intervals
        if (Time.time - lastProximityCheck >= proximityCheckInterval)
        {
            lastProximityCheck = Time.time;
            CheckProximityToWaterExit();
        }
    }

    private void CheckProximityToWaterExit()
    {
        // Check if we have a valid water exit
        if (!exitDetector.HasValidExit)
        {
            // Clean up queue state if we're leaving
            if (isWaitingInQueue && !string.IsNullOrEmpty(targetLedgeID))
            {
                LedgeQueueManager.ReleaseLedgeUsage(targetLedgeID, npcController);
                isWaitingInQueue = false;
                targetLedgeID = null;
            }
            isTargetingWaterExit = false;
            return;
        }

        ClimbableWaterLedge bestExit = exitDetector.BestExitLedge;
        Vector3 exitPoint = exitDetector.BestExitEntryPoint;
        string ledgeID = bestExit.LedgeID;

        // Check if we're close enough to trigger climb
        if (bestExit.IsNPCCloseEnoughToClimbUp(transform.position))
        {
            // NEW: Check queue system before climbing
            if (!isWaitingInQueue)
            {
                // First time close enough - request queue access
                bool canUseImmediately = LedgeQueueManager.RequestLedgeUsage(ledgeID, npcController);

                if (!canUseImmediately)
                {
                    int queuePos = LedgeQueueManager.GetQueuePosition(ledgeID, npcController);
                    float waitTime = LedgeQueueManager.GetEstimatedWaitTime(ledgeID, npcController);
                    DebugLog($"Waiting in queue for water exit (position: {queuePos}, wait: {waitTime:F1}s)");
                    isWaitingInQueue = true;
                    targetLedgeID = ledgeID;
                    isTargetingWaterExit = false; // Don't trigger climb yet
                    return;
                }
                else
                {
                    // Got immediate access
                    DebugLog("Granted immediate access to water exit ledge");
                    isWaitingInQueue = false;
                    targetLedgeID = ledgeID;
                }
            }
            else
            {
                // Already in queue - check if we can use now
                if (!LedgeQueueManager.CanNPCUseLedgeNow(ledgeID, npcController))
                {
                    // Still waiting - check queue position for debug
                    if (showDebugInfo && Time.frameCount % 60 == 0)
                    {
                        int queuePos = LedgeQueueManager.GetQueuePosition(ledgeID, npcController);
                        DebugLog($"Still waiting in queue (position: {queuePos})");
                    }
                    return;
                }
                else
                {
                    DebugLog("Queue access granted for water exit");
                    isWaitingInQueue = false;
                }
            }

            // Close enough and have queue access - trigger climb!
            if (!isTargetingWaterExit)
            {
                float distance = Vector3.Distance(transform.position, exitPoint);
                DebugLog($"Reached water exit point at distance {distance:F2}m - triggering climb!");
                TriggerClimbTransition(bestExit);
                isTargetingWaterExit = true;
                targetLedgeID = ledgeID; // Store for later release
            }
        }
        else
        {
            // Moved away from ledge - release queue if we were waiting
            if (isWaitingInQueue && !string.IsNullOrEmpty(targetLedgeID))
            {
                LedgeQueueManager.ReleaseLedgeUsage(targetLedgeID, npcController);
                DebugLog("Moved away from water exit - released queue");
                isWaitingInQueue = false;
                targetLedgeID = null;
            }

            // Update tracking
            if (targetExitLedge != bestExit)
            {
                targetExitLedge = bestExit;
                isTargetingWaterExit = false;

                if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    float distance = Vector3.Distance(transform.position, exitPoint);
                    DebugLog($"Targeting water exit: {bestExit.gameObject.name} at {exitPoint} (distance: {distance:F2}m)");
                }
            }
        }
    }



    private void TriggerClimbTransition(ClimbableWaterLedge ledge)
    {
        if (stateMachine == null || ledge == null || climbingController == null)
        {
            Debug.LogError($"{gameObject.name}: Cannot trigger climb - missing components!");
            return;
        }

        DebugLog($"Triggering water exit climb for ledge: {ledge.gameObject.name}");

        // Request climbing state (state machine will enable FollowerEntity)
        stateMachine.RequestClimbingState(ledge, isClimbingUp: true, isWaterLedge: true);

        // Tell climbing controller to apply forces
        climbingController.HandleWaterLedgeClimbUp(ledge);
    }

    public void ForceClimbAtBestExit()
    {
        if (!exitDetector.HasValidExit)
        {
            Debug.LogWarning($"{gameObject.name}: No valid exit to climb!");
            return;
        }

        DebugLog("Forcing climb at best exit");
        TriggerClimbTransition(exitDetector.BestExitLedge);
        isTargetingWaterExit = true;
    }

    public bool IsTargetingWaterExit()
    {
        return isTargetingWaterExit && exitDetector.HasValidExit;
    }

    private void DebugLog(string message)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[NPCWaterExitHandler-{gameObject.name}] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || !Application.isPlaying) return;

        if (!stateMachine.IsInWater || !exitDetector.HasValidExit)
            return;

        ClimbableWaterLedge bestExit = exitDetector.BestExitLedge;
        Vector3 exitPoint = exitDetector.BestExitEntryPoint;

        Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
        Gizmos.DrawWireSphere(exitPoint, bestExit.ClimbUpTriggerDistance);

        bool isCloseEnough = bestExit.IsNPCCloseEnoughToClimbUp(transform.position);
        Gizmos.color = isCloseEnough ? Color.green : Color.yellow;
        Gizmos.DrawLine(transform.position, exitPoint);

        if (isTargetingWaterExit)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || !Application.isPlaying) return;

        if (!stateMachine.IsInWater || !exitDetector.HasValidExit) return;

        var allExits = exitDetector.GetAllDetectedExits();
        foreach (var exit in allExits)
        {
            if (exit == null) continue;

            Vector3 entryPoint = exit.EndPosition;
            bool isTarget = exit == exitDetector.BestExitLedge;

            Gizmos.color = isTarget ? new Color(1, 0.5f, 0, 0.5f) : new Color(1, 0.5f, 0, 0.2f);
            Gizmos.DrawWireSphere(entryPoint, exit.ClimbUpTriggerDistance);
        }
    }
}