using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Centralized manager for all ledge queues in the scene.
/// Ensures NPCs use ledges in an orderly manner without collisions.
/// 
/// SETUP: This will auto-create itself when needed. No manual setup required.
/// </summary>
public class LedgeQueueManager : MonoBehaviour
{
    private static LedgeQueueManager instance;

    [Header("Settings")]
    [SerializeField, Tooltip("Enable debug logging for all queues")]
    private bool enableDebugLogs = false;

    [Header("Debug Info")]
    [SerializeField] private int totalQueues = 0;
    [SerializeField] private int totalWaitingNPCs = 0;

    // Queue management
    private Dictionary<string, LedgeQueue> ledgeQueues = new Dictionary<string, LedgeQueue>();

    // Singleton access
    public static LedgeQueueManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<LedgeQueueManager>();

                if (instance == null)
                {
                    GameObject managerObj = new GameObject("LedgeQueueManager");
                    instance = managerObj.AddComponent<LedgeQueueManager>();
                    Debug.Log("[LedgeQueueManager] Auto-created manager");
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Debug.Log("[LedgeQueueManager] Initialized");
        }
        else if (instance != this)
        {
            Debug.LogWarning("[LedgeQueueManager] Multiple managers detected! Destroying duplicate.");
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Update debug info
        if (Application.isPlaying)
        {
            UpdateDebugInfo();
        }
    }

    #region Public API

    /// <summary>
    /// Request to use a ledge. Returns true if NPC can use it immediately.
    /// </summary>
    public static bool RequestLedgeUsage(string ledgeID, NPCController npc)
    {
        if (string.IsNullOrEmpty(ledgeID) || npc == null)
        {
            Debug.LogError("[LedgeQueueManager] Invalid ledge ID or NPC");
            return false;
        }

        LedgeQueue queue = Instance.GetOrCreateQueue(ledgeID);
        return queue.RequestUsage(npc);
    }

    /// <summary>
    /// Release a ledge after use
    /// </summary>
    public static void ReleaseLedgeUsage(string ledgeID, NPCController npc)
    {
        if (string.IsNullOrEmpty(ledgeID) || npc == null) return;

        if (Instance.ledgeQueues.TryGetValue(ledgeID, out LedgeQueue queue))
        {
            queue.ReleaseUsage(npc);
        }
    }

    /// <summary>
    /// Check if an NPC can use a ledge now (they're first in queue or ledge is free)
    /// </summary>
    public static bool CanNPCUseLedgeNow(string ledgeID, NPCController npc)
    {
        if (string.IsNullOrEmpty(ledgeID) || npc == null) return false;

        if (Instance.ledgeQueues.TryGetValue(ledgeID, out LedgeQueue queue))
        {
            return queue.CanNPCUseNow(npc);
        }

        return true; // No queue exists, so ledge is free
    }

    /// <summary>
    /// Get queue position for an NPC (-1 = not in queue, -2 = currently using, 0+ = position)
    /// </summary>
    public static int GetQueuePosition(string ledgeID, NPCController npc)
    {
        if (string.IsNullOrEmpty(ledgeID) || npc == null) return -1;

        if (Instance.ledgeQueues.TryGetValue(ledgeID, out LedgeQueue queue))
        {
            return queue.GetQueuePosition(npc);
        }

        return -1;
    }

    /// <summary>
    /// Get estimated wait time for an NPC in seconds
    /// </summary>
    public static float GetEstimatedWaitTime(string ledgeID, NPCController npc)
    {
        if (string.IsNullOrEmpty(ledgeID) || npc == null) return 0f;

        if (Instance.ledgeQueues.TryGetValue(ledgeID, out LedgeQueue queue))
        {
            return queue.GetEstimatedWaitTime(npc);
        }

        return 0f;
    }

    /// <summary>
    /// Check if a ledge is currently in use
    /// </summary>
    public static bool IsLedgeInUse(string ledgeID)
    {
        if (string.IsNullOrEmpty(ledgeID)) return false;

        if (Instance.ledgeQueues.TryGetValue(ledgeID, out LedgeQueue queue))
        {
            return queue.IsInUse();
        }

        return false;
    }

    /// <summary>
    /// Get the number of NPCs waiting for a specific ledge
    /// </summary>
    public static int GetWaitingCount(string ledgeID)
    {
        if (string.IsNullOrEmpty(ledgeID)) return 0;

        if (Instance.ledgeQueues.TryGetValue(ledgeID, out LedgeQueue queue))
        {
            return queue.GetWaitingCount();
        }

        return 0;
    }

    /// <summary>
    /// Force clear a specific ledge queue (emergency cleanup)
    /// </summary>
    public static void ForceClearQueue(string ledgeID)
    {
        if (string.IsNullOrEmpty(ledgeID)) return;

        if (Instance.ledgeQueues.TryGetValue(ledgeID, out LedgeQueue queue))
        {
            queue.ForceClear();
            Debug.Log($"[LedgeQueueManager] Force cleared queue for {ledgeID}");
        }
    }

    /// <summary>
    /// Force clear all queues (emergency cleanup)
    /// </summary>
    public static void ForceClearAllQueues()
    {
        foreach (var queue in Instance.ledgeQueues.Values)
        {
            queue.ForceClear();
        }
        Debug.Log("[LedgeQueueManager] Force cleared all queues");
    }

    #endregion

    #region Queue Management

    private LedgeQueue GetOrCreateQueue(string ledgeID)
    {
        if (!ledgeQueues.TryGetValue(ledgeID, out LedgeQueue queue))
        {
            queue = new LedgeQueue(ledgeID, enableDebugLogs);
            ledgeQueues[ledgeID] = queue;

            if (enableDebugLogs)
            {
                Debug.Log($"[LedgeQueueManager] Created new queue for ledge: {ledgeID}");
            }
        }
        return queue;
    }

    private void UpdateDebugInfo()
    {
        totalQueues = ledgeQueues.Count;
        totalWaitingNPCs = 0;

        foreach (var queue in ledgeQueues.Values)
        {
            totalWaitingNPCs += queue.GetWaitingCount();
        }
    }

    #endregion

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}