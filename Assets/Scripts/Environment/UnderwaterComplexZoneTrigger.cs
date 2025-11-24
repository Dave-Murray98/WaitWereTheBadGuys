using Infohazard.HyperNav;
using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Trigger zone that switches NPCs to complex zone movement when they enter areas that require detailed pathfinding.
/// This should be attached to the NavVolume GameObject used by HyperNav.
/// Now provides methods for target validation and exit point calculation.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class UnderwaterComplexZoneTrigger : MonoBehaviour
{
    [Header("Zone Configuration")]
    [SerializeField] private NavVolume navVolume; // The HyperNav navigation volume
    [SerializeField] private LayerMask npcLayerMask = -1; // Which layers contain NPCs

    [Header("Trigger Settings")]
    [SerializeField] private BoxCollider triggerCollider;
    [SerializeField] private bool autoSetTriggerSize = true; // Automatically match trigger to NavVolume bounds
    [SerializeField] private Vector3 triggerPadding = Vector3.one * 2f; // Extra space around the nav volume

    [Header("Exit Point Settings")]
    [SerializeField] private float exitPointInset = 0.5f; // How far inward from the boundary to place exit points

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showTriggerBounds = true;

    private HashSet<NPCWaterMovementController> npcsInZone = new HashSet<NPCWaterMovementController>();

    private void Awake()
    {
        InitializeTrigger();
    }

    private void Start()
    {
        SetupTriggerSize();
    }

    private void InitializeTrigger()
    {
        // Get or create the box collider for the trigger
        triggerCollider = GetComponent<BoxCollider>();

        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }

        // Make sure it's set up as a trigger
        triggerCollider.isTrigger = true;

        // Try to find the NavVolume if not assigned
        if (navVolume == null)
        {
            navVolume = GetComponent<NavVolume>();

            if (navVolume == null)
            {
                Debug.LogWarning($"{gameObject.name}: No NavVolume found! Please assign one in the inspector.");
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: Complex Zone Trigger initialized");
        }
    }

    [Button("Set Trigger Size")]
    private void SetupTriggerSize()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider>();

        if (navVolume == null)
            navVolume = GetComponent<NavVolume>();

        if (!autoSetTriggerSize || navVolume == null || triggerCollider == null)
            return;

        // Get the bounds of the NavVolume
        Bounds navBounds = navVolume.Bounds;

        // Set the trigger collider to match the NavVolume bounds plus padding, don't set the centre as this will always be Vector3.Zero
        triggerCollider.size = navBounds.size + triggerPadding;

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: Trigger size set to {triggerCollider.size} based on NavVolume bounds");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Try to find the NPC movement coordinator
        NPCWaterMovementController controller = other.GetComponent<NPCWaterMovementController>();

        // If not found on the main object, try to find it on parent objects
        if (controller == null)
        {
            controller = other.GetComponentInParent<NPCWaterMovementController>();
        }

        // If we found a coordinator, switch it to complex zone movement
        if (controller != null)
        {
            HandleNPCEnterZone(controller);
        }
        else if (showDebugInfo)
        {
            Debug.LogWarning($"{gameObject.name}: Object {other.name} entered trigger but has no NPCWaterMovementCoordinator component");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Try to find the NPC movement coordinator
        NPCWaterMovementController controller = other.GetComponent<NPCWaterMovementController>();

        // If not found on the main object, try to find it on parent objects
        if (controller == null)
        {
            controller = other.GetComponentInParent<NPCWaterMovementController>();
        }

        // If we found a coordinator, switch it to open water movement
        if (controller != null)
        {
            HandleNPCExitZone(controller);
        }
    }

    private void HandleNPCEnterZone(NPCWaterMovementController controller)
    {
        // Add to our tracking set
        npcsInZone.Add(controller);

        // Notify the coordinator that it entered this zone
        controller.OnEnteredComplexZone(this);

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: NPC {controller.name} entered complex zone. Total NPCs in zone: {npcsInZone.Count}");
        }
    }

    private void HandleNPCExitZone(NPCWaterMovementController controller)
    {
        // Remove from our tracking set
        npcsInZone.Remove(controller);

        // Notify the coordinator that it exited this zone
        controller.OnExitedComplexZone(this);

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: NPC {controller.name} exited complex zone. Total NPCs in zone: {npcsInZone.Count}");
        }
    }

    /// <summary>
    /// Checks if a given position is within this zone's navigation volume bounds
    /// </summary>
    /// <param name="position">The world position to check</param>
    /// <returns>True if the position is within the zone bounds</returns>
    public bool IsPositionInZone(Vector3 position)
    {
        if (navVolume == null)
            return false;

        // Convert world position to local space of the nav volume
        Vector3 localPosition = navVolume.transform.InverseTransformPoint(position);

        // Check if the position is within the nav volume bounds
        Bounds localBounds = new Bounds(Vector3.zero, navVolume.Bounds.size);
        return localBounds.Contains(localPosition);
    }

    /// <summary>
    /// Gets the best exit point from this zone towards a target position
    /// </summary>
    /// <param name="targetPosition">The target position outside the zone</param>
    /// <returns>The best exit point on the zone boundary</returns>
    public Vector3 GetExitPointTowards(Vector3 targetPosition)
    {
        if (navVolume == null)
        {
            Debug.LogWarning($"{gameObject.name}: No NavVolume assigned, cannot calculate exit point");
            return transform.position;
        }

        // Get the nav volume bounds in world space
        Bounds worldBounds = GetWorldBounds();

        // Find the closest point on the bounds to the target
        Vector3 closestPoint = worldBounds.ClosestPoint(targetPosition);

        // Calculate the direction from bounds center to the closest point
        Vector3 centerToPoint = closestPoint - worldBounds.center;
        Vector3 normalizedDirection = centerToPoint.normalized;

        // Move the exit point slightly inward to ensure it's navigable
        Vector3 exitPoint = closestPoint - normalizedDirection * exitPointInset;

        return exitPoint;
    }

    /// <summary>
    /// Gets the world space bounds of the navigation volume
    /// </summary>
    /// <returns>The bounds in world space</returns>
    public Bounds GetWorldBounds()
    {
        if (navVolume == null)
            return new Bounds(transform.position, Vector3.one);

        // Get the nav volume bounds and transform them to world space
        Bounds localBounds = navVolume.Bounds;
        Bounds worldBounds = new Bounds(
            navVolume.transform.TransformPoint(localBounds.center),
            Vector3.Scale(localBounds.size, navVolume.transform.lossyScale)
        );

        return worldBounds;
    }

    /// <summary>
    /// Gets the center of the navigation volume in world space
    /// </summary>
    /// <returns>The world space center position</returns>
    public Vector3 GetWorldCenter()
    {
        if (navVolume == null)
            return transform.position;

        return navVolume.transform.TransformPoint(navVolume.Bounds.center);
    }

    // Public methods for external control (existing methods preserved)

    /// <summary>
    /// Manually refresh the trigger size based on the NavVolume bounds
    /// </summary>
    public void RefreshTriggerSize()
    {
        SetupTriggerSize();
    }

    /// <summary>
    /// Get the number of NPCs currently in the zone
    /// </summary>
    /// <returns>Count of NPCs in the complex zone</returns>
    public int GetNPCCount()
    {
        // Clean up any null references (in case NPCs were destroyed)
        npcsInZone.RemoveWhere(npc => npc == null);
        return npcsInZone.Count;
    }

    /// <summary>
    /// Get all NPCs currently in the zone
    /// </summary>
    /// <returns>Array of NPCs in the complex zone</returns>
    public NPCWaterMovementController[] GetNPCsInZone()
    {
        // Clean up any null references
        npcsInZone.RemoveWhere(npc => npc == null);

        NPCWaterMovementController[] result = new NPCWaterMovementController[npcsInZone.Count];
        npcsInZone.CopyTo(result);
        return result;
    }

    /// <summary>
    /// Force all NPCs in the zone to switch to complex zone movement
    /// Useful if you need to refresh the movement mode for all NPCs
    /// </summary>
    public void ForceAllNPCsToComplexMode()
    {
        // Clean up any null references first
        npcsInZone.RemoveWhere(npc => npc == null);

        foreach (var coordinator in npcsInZone)
        {
            if (coordinator != null)
            {
                coordinator.SwitchToComplexZoneMovement();
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: Forced {npcsInZone.Count} NPCs to complex zone movement");
        }
    }

    /// <summary>
    /// Clear all tracked NPCs (useful for cleanup or reset)
    /// </summary>
    public void ClearTrackedNPCs()
    {
        npcsInZone.Clear();

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: Cleared all tracked NPCs");
        }
    }

    // Validation and setup methods (useful in editor)
    private void OnValidate()
    {
        // This runs in the editor when values change
        if (Application.isPlaying)
        {
            SetupTriggerSize();
        }
    }

    private void OnDrawGizmos()
    {
        // Always draw a small indicator at the zone center
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // Draw nav volume center if available
        if (navVolume != null)
        {
            Vector3 worldCenter = GetWorldCenter();
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(worldCenter, 0.3f);
        }
    }

    private void OnDestroy()
    {
        // Clean up when the trigger is destroyed
        ClearTrackedNPCs();
    }
}