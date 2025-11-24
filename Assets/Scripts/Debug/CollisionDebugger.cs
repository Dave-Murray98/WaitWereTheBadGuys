using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

/// <summary>
/// Debug script that shows what objects this collider is colliding with.
/// Provides real-time collision information in the inspector and console.
/// Useful for debugging physics interactions and collision detection issues.
/// </summary>
public class CollisionDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableConsoleLogging = true;
    [SerializeField] private bool logOnCollisionEnter = true;
    [SerializeField] private bool logOnCollisionStay = false;
    [SerializeField] private bool logOnCollisionExit = true;

    [Header("Trigger Detection")]
    [SerializeField] private bool detectTriggers = true;
    [SerializeField] private bool logOnTriggerEnter = true;
    [SerializeField] private bool logOnTriggerStay = false;
    [SerializeField] private bool logOnTriggerExit = true;

    [Header("Display Options")]
    [SerializeField] private bool showColliderDetails = true;
    [SerializeField] private bool showCollisionForces = true;
    [SerializeField] private bool showRelativeVelocity = true;
    [SerializeField] private bool showContactPoints = false;

    [Header("Filtering")]
    [SerializeField] private LayerMask ignoreLayers = 0;
    [SerializeField] private string[] ignoreObjectsWithTags = new string[0];
    [SerializeField] private string[] ignoreObjectsWithNames = new string[0];

    [Header("Current Collisions")]
    [ShowInInspector, ReadOnly]
    private List<string> currentCollisions = new List<string>();

    [ShowInInspector, ReadOnly]
    private List<string> currentTriggers = new List<string>();

    [ShowInInspector, ReadOnly]
    private int totalCollisionCount = 0;

    [ShowInInspector, ReadOnly]
    private int totalTriggerCount = 0;

    // Internal tracking
    private Dictionary<Collider, CollisionInfo> activeCollisions = new Dictionary<Collider, CollisionInfo>();
    private Dictionary<Collider, TriggerInfo> activeTriggers = new Dictionary<Collider, TriggerInfo>();

    // Component references
    private Collider thisCollider;
    private Rigidbody thisRigidbody;

    private class CollisionInfo
    {
        public string objectName;
        public string layerName;
        public Vector3 relativeVelocity;
        public Vector3 impulse;
        public int contactCount;
        public float collisionTime;

        public CollisionInfo(Collision collision)
        {
            objectName = collision.gameObject.name;
            layerName = LayerMask.LayerToName(collision.gameObject.layer);
            relativeVelocity = collision.relativeVelocity;
            impulse = collision.impulse;
            contactCount = collision.contactCount;
            collisionTime = Time.time;
        }

        public override string ToString()
        {
            return $"{objectName} (Layer: {layerName})";
        }

        public string GetDetailedInfo()
        {
            return $"{objectName} - Layer: {layerName}, Contacts: {contactCount}, " +
                   $"RelVel: {relativeVelocity.magnitude:F2}m/s, Impulse: {impulse.magnitude:F2}";
        }
    }

    private class TriggerInfo
    {
        public string objectName;
        public string layerName;
        public float enterTime;

        public TriggerInfo(Collider other)
        {
            objectName = other.gameObject.name;
            layerName = LayerMask.LayerToName(other.gameObject.layer);
            enterTime = Time.time;
        }

        public override string ToString()
        {
            return $"{objectName} (Layer: {layerName})";
        }

        public string GetDetailedInfo()
        {
            float duration = Time.time - enterTime;
            return $"{objectName} - Layer: {layerName}, Duration: {duration:F2}s";
        }
    }

    #region Unity Lifecycle

    private void Awake()
    {
        thisCollider = GetComponent<Collider>();
        thisRigidbody = GetComponent<Rigidbody>();

        if (thisCollider == null)
        {
            Debug.LogError($"[CollisionDebugger] No Collider found on {gameObject.name}!");
        }
    }

    private void Update()
    {
        UpdateDisplayLists();
    }

    #endregion

    #region Collision Events

    private void OnCollisionEnter(Collision collision)
    {
        if (!ShouldProcessCollision(collision.gameObject)) return;

        var info = new CollisionInfo(collision);
        activeCollisions[collision.collider] = info;

        if (logOnCollisionEnter && enableConsoleLogging)
        {
            LogCollisionEvent("COLLISION ENTER", info, collision);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!ShouldProcessCollision(collision.gameObject)) return;

        if (activeCollisions.ContainsKey(collision.collider))
        {
            // Update collision info
            var info = activeCollisions[collision.collider];
            info.relativeVelocity = collision.relativeVelocity;
            info.impulse = collision.impulse;
            info.contactCount = collision.contactCount;
        }

        if (logOnCollisionStay && enableConsoleLogging)
        {
            LogCollisionEvent("COLLISION STAY", activeCollisions[collision.collider], collision);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!ShouldProcessCollision(collision.gameObject)) return;

        if (activeCollisions.ContainsKey(collision.collider))
        {
            var info = activeCollisions[collision.collider];
            activeCollisions.Remove(collision.collider);

            if (logOnCollisionExit && enableConsoleLogging)
            {
                float duration = Time.time - info.collisionTime;
                LogMessage($"COLLISION EXIT: {info.objectName} (Duration: {duration:F2}s)");
            }
        }
    }

    #endregion

    #region Trigger Events

    private void OnTriggerEnter(Collider other)
    {
        if (!detectTriggers || !ShouldProcessCollision(other.gameObject)) return;

        var info = new TriggerInfo(other);
        activeTriggers[other] = info;

        if (logOnTriggerEnter && enableConsoleLogging)
        {
            LogTriggerEvent("TRIGGER ENTER", info);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!detectTriggers || !ShouldProcessCollision(other.gameObject)) return;

        if (logOnTriggerStay && enableConsoleLogging && activeTriggers.ContainsKey(other))
        {
            LogTriggerEvent("TRIGGER STAY", activeTriggers[other]);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!detectTriggers || !ShouldProcessCollision(other.gameObject)) return;

        if (activeTriggers.ContainsKey(other))
        {
            var info = activeTriggers[other];
            activeTriggers.Remove(other);

            if (logOnTriggerExit && enableConsoleLogging)
            {
                float duration = Time.time - info.enterTime;
                LogMessage($"TRIGGER EXIT: {info.objectName} (Duration: {duration:F2}s)");
            }
        }
    }

    #endregion

    #region Filtering and Processing

    private bool ShouldProcessCollision(GameObject other)
    {
        // Check layer mask
        if (ignoreLayers != 0 && ((1 << other.layer) & ignoreLayers) != 0)
        {
            return false;
        }

        // Check tags
        if (ignoreObjectsWithTags.Contains(other.tag))
        {
            return false;
        }

        // Check names
        if (ignoreObjectsWithNames.Any(name => other.name.Contains(name)))
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Logging

    private void LogCollisionEvent(string eventType, CollisionInfo info, Collision collision)
    {
        string message = $"[{gameObject.name}] {eventType}: {info.objectName}";

        if (showColliderDetails)
        {
            message += $" (Layer: {info.layerName}, Contacts: {info.contactCount})";
        }

        if (showRelativeVelocity)
        {
            message += $" RelVel: {info.relativeVelocity.magnitude:F2}m/s";
        }

        if (showCollisionForces)
        {
            message += $" Impulse: {info.impulse.magnitude:F2}";
        }

        if (showContactPoints && collision.contactCount > 0)
        {
            message += $" Contact: {collision.contacts[0].point}";
        }

        LogMessage(message);
    }

    private void LogTriggerEvent(string eventType, TriggerInfo info)
    {
        string message = $"[{gameObject.name}] {eventType}: {info.objectName}";

        if (showColliderDetails)
        {
            message += $" (Layer: {info.layerName})";
        }

        LogMessage(message);
    }

    private void LogMessage(string message)
    {
        Debug.Log($"[CollisionDebugger] {message}");
    }

    #endregion

    #region Display Updates

    private void UpdateDisplayLists()
    {
        // Update collision list for inspector display
        currentCollisions.Clear();
        foreach (var collision in activeCollisions.Values)
        {
            currentCollisions.Add(showColliderDetails ? collision.GetDetailedInfo() : collision.ToString());
        }

        // Update trigger list for inspector display
        currentTriggers.Clear();
        foreach (var trigger in activeTriggers.Values)
        {
            currentTriggers.Add(showColliderDetails ? trigger.GetDetailedInfo() : trigger.ToString());
        }

        totalCollisionCount = activeCollisions.Count;
        totalTriggerCount = activeTriggers.Count;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Get all currently active collision objects
    /// </summary>
    public GameObject[] GetCurrentCollisionObjects()
    {
        return activeCollisions.Keys.Select(c => c.gameObject).ToArray();
    }

    /// <summary>
    /// Get all currently active trigger objects
    /// </summary>
    public GameObject[] GetCurrentTriggerObjects()
    {
        return activeTriggers.Keys.Select(c => c.gameObject).ToArray();
    }

    /// <summary>
    /// Check if currently colliding with a specific object
    /// </summary>
    public bool IsCollidingWith(GameObject obj)
    {
        return activeCollisions.Keys.Any(c => c.gameObject == obj);
    }

    /// <summary>
    /// Check if currently in trigger with a specific object
    /// </summary>
    public bool IsInTriggerWith(GameObject obj)
    {
        return activeTriggers.Keys.Any(c => c.gameObject == obj);
    }

    /// <summary>
    /// Get detailed collision info for a specific object
    /// </summary>
    public string GetCollisionInfo(GameObject obj)
    {
        var collider = activeCollisions.Keys.FirstOrDefault(c => c.gameObject == obj);
        return collider != null ? activeCollisions[collider].GetDetailedInfo() : "No collision found";
    }

    /// <summary>
    /// Clear all tracked collisions (useful for reset/testing)
    /// </summary>
    [Button]
    public void ClearAllTrackedCollisions()
    {
        activeCollisions.Clear();
        activeTriggers.Clear();
        currentCollisions.Clear();
        currentTriggers.Clear();
        totalCollisionCount = 0;
        totalTriggerCount = 0;

        LogMessage("All tracked collisions cleared");
    }

    /// <summary>
    /// Print current collision summary to console
    /// </summary>
    [Button]
    public void PrintCollisionSummary()
    {
        LogMessage("=== COLLISION SUMMARY ===");
        LogMessage($"Active Collisions: {totalCollisionCount}");
        LogMessage($"Active Triggers: {totalTriggerCount}");

        if (totalCollisionCount > 0)
        {
            LogMessage("Current Collisions:");
            foreach (var info in activeCollisions.Values)
            {
                LogMessage($"  - {info.GetDetailedInfo()}");
            }
        }

        if (totalTriggerCount > 0)
        {
            LogMessage("Current Triggers:");
            foreach (var info in activeTriggers.Values)
            {
                LogMessage($"  - {info.GetDetailedInfo()}");
            }
        }
    }

    #endregion
}