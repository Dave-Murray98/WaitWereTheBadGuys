using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages a queue of NPCs waiting to use a specific ledge.
/// Ensures only one NPC traverses a ledge at a time to prevent collisions.
/// </summary>
public class LedgeQueue
{
    private Queue<NPCController> waitingNPCs = new Queue<NPCController>();
    private NPCController currentUser = null;
    private string ledgeName;
    private bool showDebugLogs;

    // Safety timeout to prevent infinite waiting
    private float currentUserStartTime = 0f;
    private const float MAX_USAGE_TIME = 10f; // Force release after 10 seconds

    public LedgeQueue(string ledgeName, bool debugLogs = false)
    {
        this.ledgeName = ledgeName;
        this.showDebugLogs = debugLogs;
    }

    /// <summary>
    /// Request to use the ledge. Returns true if NPC can use it immediately.
    /// </summary>
    public bool RequestUsage(NPCController npc)
    {
        if (npc == null) return false;

        // Check if this NPC is already the current user
        if (currentUser == npc)
        {
            DebugLog($"{npc.name} is already using the ledge");
            return true;
        }

        // Check if this NPC is already in the queue
        if (waitingNPCs.Contains(npc))
        {
            DebugLog($"{npc.name} is already waiting in queue (position: {GetQueuePosition(npc)})");
            return false;
        }

        // If ledge is free, grant immediate access
        if (currentUser == null)
        {
            GrantAccess(npc);
            return true;
        }

        // Check for timeout - current user taking too long
        if (Time.time - currentUserStartTime > MAX_USAGE_TIME)
        {
            Debug.LogWarning($"[LedgeQueue-{ledgeName}] Current user {currentUser.name} exceeded max usage time. Forcing release.");
            ReleaseCurrentUser();
            GrantAccess(npc);
            return true;
        }

        // Ledge is busy, add to queue
        waitingNPCs.Enqueue(npc);
        DebugLog($"{npc.name} added to queue (position: {waitingNPCs.Count})");
        return false;
    }

    /// <summary>
    /// Release the ledge when NPC finishes using it
    /// </summary>
    public void ReleaseUsage(NPCController npc)
    {
        if (npc == null) return;

        // Only release if this NPC is the current user
        if (currentUser == npc)
        {
            DebugLog($"{npc.name} released the ledge");
            ReleaseCurrentUser();
            ProcessQueue();
        }
        else if (waitingNPCs.Contains(npc))
        {
            // NPC is in queue but wants to leave (e.g., target changed)
            RemoveFromQueue(npc);
            DebugLog($"{npc.name} removed from queue");
        }
    }

    /// <summary>
    /// Check if NPC can start using the ledge (they're first in queue and ledge is free)
    /// </summary>
    public bool CanNPCUseNow(NPCController npc)
    {
        if (npc == null) return false;

        // Already using it
        if (currentUser == npc) return true;

        // Check for timeout
        if (currentUser != null && Time.time - currentUserStartTime > MAX_USAGE_TIME)
        {
            Debug.LogWarning($"[LedgeQueue-{ledgeName}] Current user {currentUser.name} timed out");
            ReleaseCurrentUser();
        }

        // Ledge is free and NPC is first in queue
        if (currentUser == null && waitingNPCs.Count > 0 && waitingNPCs.Peek() == npc)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get the queue position of an NPC (0 = next, -1 = not in queue)
    /// </summary>
    public int GetQueuePosition(NPCController npc)
    {
        if (currentUser == npc) return -2; // Special value: currently using

        int position = 0;
        foreach (var waitingNPC in waitingNPCs)
        {
            if (waitingNPC == npc) return position;
            position++;
        }
        return -1; // Not in queue
    }

    /// <summary>
    /// Get estimated wait time in seconds for an NPC
    /// </summary>
    public float GetEstimatedWaitTime(NPCController npc)
    {
        int position = GetQueuePosition(npc);
        if (position == -2) return 0f; // Currently using
        if (position == -1) return 0f; // Not in queue

        // Estimate 3 seconds per NPC ahead in queue
        float waitTime = position * 3f;

        // Add remaining time for current user
        if (currentUser != null)
        {
            float currentUsageTime = Time.time - currentUserStartTime;
            float estimatedRemainingTime = Mathf.Max(0f, 3f - currentUsageTime);
            waitTime += estimatedRemainingTime;
        }

        return waitTime;
    }

    /// <summary>
    /// Check if the ledge is currently in use
    /// </summary>
    public bool IsInUse()
    {
        return currentUser != null;
    }

    /// <summary>
    /// Get the number of NPCs waiting
    /// </summary>
    public int GetWaitingCount()
    {
        return waitingNPCs.Count;
    }

    /// <summary>
    /// Get the current user
    /// </summary>
    public NPCController GetCurrentUser()
    {
        return currentUser;
    }

    /// <summary>
    /// Force clear all NPCs from this queue (emergency cleanup)
    /// </summary>
    public void ForceClear()
    {
        DebugLog("Force clearing queue");
        currentUser = null;
        waitingNPCs.Clear();
    }

    #region Private Methods

    private void GrantAccess(NPCController npc)
    {
        currentUser = npc;
        currentUserStartTime = Time.time;
        DebugLog($"{npc.name} granted access to ledge");
    }

    private void ReleaseCurrentUser()
    {
        currentUser = null;
        currentUserStartTime = 0f;
    }

    private void ProcessQueue()
    {
        // Grant access to next NPC in queue
        if (waitingNPCs.Count > 0)
        {
            NPCController nextNPC = waitingNPCs.Dequeue();

            // Validate NPC is still valid and wants to use the ledge
            if (nextNPC != null && nextNPC.gameObject != null)
            {
                GrantAccess(nextNPC);

                // Notify the NPC that they can now use the ledge
                NotifyNPCCanUse(nextNPC);
            }
            else
            {
                // Invalid NPC, process next one
                DebugLog("Next NPC in queue was invalid, processing next...");
                ProcessQueue();
            }
        }
    }

    private void NotifyNPCCanUse(NPCController npc)
    {
        // The NPC's climbing controller will check CanNPCUseNow() in its update loop
        DebugLog($"Notifying {npc.name} they can now use the ledge");
    }

    private void RemoveFromQueue(NPCController npc)
    {
        // Create new queue without the specified NPC
        Queue<NPCController> newQueue = new Queue<NPCController>();
        foreach (var waitingNPC in waitingNPCs)
        {
            if (waitingNPC != npc)
            {
                newQueue.Enqueue(waitingNPC);
            }
        }
        waitingNPCs = newQueue;
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[LedgeQueue-{ledgeName}] {message}");
        }
    }

    #endregion
}