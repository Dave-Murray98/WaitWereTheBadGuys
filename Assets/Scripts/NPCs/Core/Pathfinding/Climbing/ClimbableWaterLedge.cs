using UnityEngine;
using Pathfinding;
using Sirenix.OdinInspector;
using System.Collections.Generic;

/// <summary>
/// Represents a water ledge that NPCs can use to enter/exit water.
/// Works with A* pathfinding's NodeLink2 system for intelligent routing.
/// 
/// SETUP INSTRUCTIONS:
/// 1. Create an empty GameObject at the TOP of your water ledge (where NPC exits water)
/// 2. Add this ClimbableWaterLedge component
/// 3. Add NodeLink2 component
/// 4. Add Seeker component (for path evaluation when swimming)
/// 5. Create child GameObject called "End" at BOTTOM of ledge (in water, where NPC swims to)
/// 6. Create child GameObject called "FalseEnd" on SEAFLOOR (connects to Recast graph for pathfinding)
/// 7. Assign "FalseEnd" to the NodeLink2's End field
/// 
/// BEHAVIOR:
/// - When on GROUND: A* routes through NodeLink2, we intercept and apply drop force
/// - When SWIMMING: NPC swims to "End", then climbs up to top
/// </summary>
[RequireComponent(typeof(NodeLink2))]
[RequireComponent(typeof(Seeker))]
public class ClimbableWaterLedge : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("The NodeLink2 component (auto-found if not assigned)")]
    private NodeLink2 nodeLink;

    [SerializeField, Tooltip("The Seeker component for path evaluation (auto-found if not assigned)")]
    private Seeker seeker;

    [SerializeField, Tooltip("The End transform where the npcs will climb out of the water from (in water, where NPCs will pathfind to)")]
    private Transform waterSurfaceEndTransform;

    [SerializeField, Tooltip("The False End transform (on seafloor, used for A* pathfinding to connect the nodeLink to the Recast graph)")]
    private Transform falseEndSeaFloorTransform;

    // Static registry for fast lookup by NodeLink2
    private static Dictionary<NodeLink2, ClimbableWaterLedge> linkRegistry = new Dictionary<NodeLink2, ClimbableWaterLedge>();


    [Header("Detection")]
    [SerializeField, Tooltip("How close NPC must be to End position to trigger climb up")]
    private float climbUpTriggerDistance = 1.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color waterEntryColor = Color.cyan;
    [SerializeField] private Color waterExitColor = Color.yellow;
    [SerializeField] private Color falseEndColor = Color.red;

    // Properties for external access
    public Vector3 TopPosition => transform.position;
    public Vector3 EndPosition => waterSurfaceEndTransform != null ? waterSurfaceEndTransform.position : transform.position;
    public Vector3 FalseEndPosition => falseEndSeaFloorTransform != null ? falseEndSeaFloorTransform.position : transform.position;

    public float ClimbUpTriggerDistance => climbUpTriggerDistance;
    public Seeker Seeker => seeker;
    public NodeLink2 NodeLink => nodeLink;

    public float Height => Mathf.Abs(TopPosition.y - EndPosition.y);
    public float HorizontalDistance => Vector3.Distance(
        new Vector3(TopPosition.x, 0, TopPosition.z),
        new Vector3(EndPosition.x, 0, EndPosition.z)
    );


    [Header("Force Multipliers - Vertical")]
    [SerializeField, Tooltip("Reference height for vertical force calculations")]
    private float referenceHeight = 4.5f;

    [Header("Force Multipliers - Horizontal")]
    [SerializeField, Tooltip("Reference horizontal distance for horizontal force calculations")]
    private float referenceHorizontalDistance = 4f;

    // Ledge Queue for managing multiple NPCs
    public string LedgeID => $"WaterLedge_{gameObject.GetInstanceID()}";


    private void Awake()
    {
        InitializeComponent();

        // NEW: Register with centralized registry instead of local dictionary
        WaterLedgeRegistry.RegisterLedge(this);

        DebugLog($"Registered with WaterLedgeRegistry");
    }

    private void OnDestroy()
    {
        // NEW: Unregister from centralized registry
        WaterLedgeRegistry.UnregisterLedge(this);

        DebugLog($"Unregistered from WaterLedgeRegistry");
    }

    // Add this method to ClimbableWaterLedge.cs after the existing CalculatePathToTarget method:

    /// <summary>
    /// Register this ledge in the static registry for fast lookup
    /// </summary>
    private void RegisterLedge()
    {
        if (nodeLink != null)
        {
            linkRegistry[nodeLink] = this;
            DebugLog($"Registered in water ledge registry");
        }
    }

    /// <summary>
    /// Unregister this ledge from the static registry
    /// </summary>
    private void UnregisterLedge()
    {
        if (nodeLink != null && linkRegistry.ContainsKey(nodeLink))
        {
            linkRegistry.Remove(nodeLink);
            DebugLog($"Unregistered from water ledge registry");
        }
    }

    /// <summary>
    /// Calculate the vertical force multiplier based on ledge height
    /// </summary>
    public float CalculateVerticalForceMultiplier()
    {
        float verticalMultiplier = Height / referenceHeight;

        if (verticalMultiplier < 0.5f)
        {
            verticalMultiplier = 0.5f; // Minimum multiplier to ensure some upward force
        }

        return verticalMultiplier;
    }

    /// <summary>
    /// Calculate the horizontal force multiplier based on horizontal distance
    /// </summary>
    public float CalculateHorizontalForceMultiplier()
    {
        float horizontalMultiplier = HorizontalDistance / referenceHorizontalDistance;

        if (horizontalMultiplier < 0.5f)
        {
            horizontalMultiplier = 0.5f; // Minimum multiplier to ensure some forward force
        }

        return horizontalMultiplier;
    }


    /// <summary>
    /// Try to get a ClimbableWaterLedge from a NodeLink2 (used by NPCLedgeClimbingController)
    /// </summary>
    public static ClimbableWaterLedge GetWaterLedgeFromNodeLink(NodeLink2 nodeLink)
    {
        if (nodeLink == null) return null;

        // Now query all ledges and find matching NodeLink2
        var allLedges = WaterLedgeRegistry.GetAllLedges();
        foreach (var ledge in allLedges)
        {
            if (ledge != null && ledge.NodeLink == nodeLink)
            {
                return ledge;
            }
        }

        return null;
    }

    /// <summary>
    /// Get all water ledges in the scene
    /// </summary>
    public static List<ClimbableWaterLedge> GetAllWaterLedges()
    {
        return WaterLedgeRegistry.GetAllLedges();
    }

    private void InitializeComponent()
    {
        // Get NodeLink2 component
        if (nodeLink == null)
        {
            nodeLink = GetComponent<NodeLink2>();
        }

        if (nodeLink == null)
        {
            Debug.LogError($"{gameObject.name}: ClimbableWaterLedge requires a NodeLink2 component!");
            return;
        }

        // Get Seeker component
        if (seeker == null)
        {
            seeker = GetComponent<Seeker>();
        }

        if (seeker == null)
        {
            Debug.LogError($"{gameObject.name}: ClimbableWaterLedge requires a Seeker component!");
            return;
        }

        // Find End transform if not assigned
        if (waterSurfaceEndTransform == null)
        {
            waterSurfaceEndTransform = transform.Find("End");
            if (waterSurfaceEndTransform == null)
            {
                Debug.LogError($"{gameObject.name}: Could not find 'End' child transform! Please create it at the bottom of the ledge (in water).");
                return;
            }
        }

        // Find FalseEnd transform if not assigned
        if (falseEndSeaFloorTransform == null)
        {
            falseEndSeaFloorTransform = transform.Find("FalseEnd");
            if (falseEndSeaFloorTransform == null)
            {
                Debug.LogError($"{gameObject.name}: Could not find 'FalseEnd' child transform! Please create it on the seafloor for A* pathfinding.");
                return;
            }
        }

        // Validate that NodeLink2's End is set to FalseEnd
        if (nodeLink.EndTransform != falseEndSeaFloorTransform)
        {
            Debug.LogWarning($"{gameObject.name}: NodeLink2's End should be set to FalseEnd for pathfinding to work correctly!");
        }

        DebugLog($"Water ledge initialized - Top: {TopPosition}, End: {EndPosition}, FalseEnd: {FalseEndPosition}");
    }

    /// <summary>
    /// Calculate path length from this ledge's top position to a target position
    /// Used by NPCWaterExitDetector to find the best ledge when swimming
    /// </summary>
    public void CalculatePathToTarget(Vector3 targetPosition, System.Action<float> onPathCalculated)
    {
        Debug.Log($"[ClimbableWaterLedge] Calculating path from Top {TopPosition} to Target {targetPosition}");
        if (seeker == null)
        {
            Debug.LogError($"{gameObject.name}: No Seeker component for path calculation!");
            onPathCalculated?.Invoke(float.MaxValue);
            return;
        }

        // Start path calculation from TOP position to target
        seeker.StartPath(TopPosition, targetPosition, (Path p) =>
        {
            if (p.error)
            {
                Debug.Log($"[ClimbableWaterLedge] Path calculation failed to {targetPosition}");
                onPathCalculated?.Invoke(float.MaxValue);
            }
            else
            {
                float pathLength = p.GetTotalLength();
                DebugLog($"Path calculated to {targetPosition}: {pathLength:F2}m");
                onPathCalculated?.Invoke(pathLength);
            }
        });
    }

    /// <summary>
    /// Check if an NPC position is close enough to End position to trigger climb up
    /// </summary>
    public bool IsNPCCloseEnoughToClimbUp(Vector3 npcPosition)
    {
        float sqrDistance = (npcPosition - EndPosition).sqrMagnitude;
        float sqrTriggerDistance = climbUpTriggerDistance * climbUpTriggerDistance;

        DebugLog($"IsNPCCloseEnoughToClimbUp:{sqrDistance <= sqrTriggerDistance}, distance = {(npcPosition - EndPosition).magnitude}, climbUpTriggerDistance = {climbUpTriggerDistance}");
        return sqrDistance <= sqrTriggerDistance;
    }

    /// <summary>
    /// Get the direction from End to Top (for climb up movement)
    /// </summary>
    public Vector3 GetClimbUpDirection()
    {
        return (TopPosition - EndPosition).normalized;
    }

    /// <summary>
    /// Get the direction from Top to End (for drop down movement)
    /// </summary>
    public Vector3 GetDropDownDirection()
    {
        return (EndPosition - TopPosition).normalized;
    }

    #region Debug & Editor

    private void DebugLog(string message)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[WaterLedge-{gameObject.name}] {message}");
        }
    }

    [Button("Validate Setup")]
    private void ValidateSetup()
    {
        if (nodeLink == null)
            nodeLink = GetComponent<NodeLink2>();

        if (seeker == null)
            seeker = GetComponent<Seeker>();

        if (nodeLink == null)
        {
            Debug.LogError($"{gameObject.name}: Missing NodeLink2 component!");
            return;
        }

        if (seeker == null)
        {
            Debug.LogError($"{gameObject.name}: Missing Seeker component!");
            return;
        }

        if (waterSurfaceEndTransform == null)
        {
            Debug.LogError($"{gameObject.name}: End transform not assigned!");
            return;
        }

        if (falseEndSeaFloorTransform == null)
        {
            Debug.LogError($"{gameObject.name}: FalseEnd transform not assigned!");
            return;
        }

        if (nodeLink.EndTransform != falseEndSeaFloorTransform)
        {
            Debug.LogWarning($"{gameObject.name}: NodeLink2.End should be FalseEnd!");
        }

        Debug.Log($"{gameObject.name}: Setup Valid!\n" +
                 $"Top: {TopPosition}\n" +
                 $"End (in water): {EndPosition}\n" +
                 $"FalseEnd (seafloor): {FalseEndPosition}\n");
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Initialize if needed (for editor visualization)
        if (nodeLink == null)
            nodeLink = GetComponent<NodeLink2>();

        if (waterSurfaceEndTransform == null)
            waterSurfaceEndTransform = transform.Find("End");

        if (falseEndSeaFloorTransform == null)
            falseEndSeaFloorTransform = transform.Find("FalseEnd");

        if (waterSurfaceEndTransform == null || falseEndSeaFloorTransform == null)
        {
            // Draw warning indicator
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            return;
        }

        Vector3 top = TopPosition;
        Vector3 end = EndPosition;
        Vector3 falseEnd = FalseEndPosition;

        // Draw TOP marker (where NPC exits water)
        Gizmos.color = waterExitColor;
        Gizmos.DrawWireSphere(top, 0.4f);
        Gizmos.DrawWireCube(top, Vector3.one * 0.4f);

        // Draw END marker (in water, where NPC swims to)
        Gizmos.color = waterEntryColor;
        Gizmos.DrawWireSphere(end, 0.4f);
        Gizmos.DrawWireCube(end, Vector3.one * 0.4f);

        // Draw FALSE END marker (on seafloor, for A* pathfinding)
        Gizmos.color = falseEndColor;
        Gizmos.DrawWireSphere(falseEnd, 0.3f);
        Gizmos.DrawLine(falseEnd + Vector3.left * 0.3f, falseEnd + Vector3.right * 0.3f);
        Gizmos.DrawLine(falseEnd + Vector3.forward * 0.3f, falseEnd + Vector3.back * 0.3f);

        // Draw climb up line (End -> Top)
        Gizmos.color = Color.green;
        DrawArrow(end, top, 0.3f);

        // Draw drop down line (Top -> End)
        Gizmos.color = Color.yellow;
        DrawArrow(top, end, 0.25f);

        // Draw A* pathfinding link (Top -> FalseEnd) - dashed
        Gizmos.color = Color.red;
        DrawDashedLine(top, falseEnd, 0.3f);

        // Draw climb up trigger radius
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(end, climbUpTriggerDistance);
    }

    private void DrawArrow(Vector3 from, Vector3 to, float arrowHeadSize)
    {
        Vector3 direction = (to - from).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 arrowPoint = to - direction * arrowHeadSize;

        Gizmos.DrawLine(from, to);
        Gizmos.DrawLine(to, arrowPoint + right * arrowHeadSize * 0.5f);
        Gizmos.DrawLine(to, arrowPoint - right * arrowHeadSize * 0.5f);
    }

    private void DrawDashedLine(Vector3 from, Vector3 to, float dashSize)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        direction.Normalize();

        int dashCount = Mathf.CeilToInt(distance / (dashSize * 2));
        for (int i = 0; i < dashCount; i++)
        {
            Vector3 start = from + direction * (i * dashSize * 2);
            Vector3 end = start + direction * dashSize;
            if (Vector3.Distance(from, end) > distance)
                end = to;
            Gizmos.DrawLine(start, end);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        if (waterSurfaceEndTransform != null && falseEndSeaFloorTransform != null)
        {
            Vector3 top = TopPosition;
            Vector3 end = EndPosition;
            Vector3 falseEnd = FalseEndPosition;

            // Draw more detailed info when selected
            // Draw vertical guide lines
            Gizmos.color = Color.white;
            Gizmos.DrawLine(top, top + Vector3.down * 0.5f);
            Gizmos.DrawLine(end, end + Vector3.up * 0.5f);
            Gizmos.DrawLine(falseEnd, falseEnd + Vector3.up * 0.5f);

            // Draw horizontal plane at each level
            Gizmos.color = new Color(1, 1, 0, 0.2f);
            DrawHorizontalCircle(top, 1f);
            DrawHorizontalCircle(end, 1f);
            DrawHorizontalCircle(falseEnd, 0.8f);
        }
    }

    private void DrawHorizontalCircle(Vector3 center, float radius)
    {
        int segments = 20;
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    #endregion
}