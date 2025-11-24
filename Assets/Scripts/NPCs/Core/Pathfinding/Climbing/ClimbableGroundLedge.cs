using UnityEngine;
using Pathfinding;
using Sirenix.OdinInspector;
using System.Collections.Generic;

/// <summary>
/// Represents a climbable ledge connection for GROUND-TO-GROUND movement only.
/// Calculates separate force multipliers for vertical and horizontal forces based on geometry.
/// </summary>
[RequireComponent(typeof(NodeLink2))]
public class ClimbableGroundLedge : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("The NodeLink2 component (auto-found if not assigned)")]
    private NodeLink2 nodeLink;

    private static Dictionary<NodeLink2, ClimbableGroundLedge> linkRegistry = new Dictionary<NodeLink2, ClimbableGroundLedge>();

    [Header("Force Multipliers - Vertical")]
    [SerializeField, Tooltip("Reference height for vertical force calculations")]
    private float referenceHeight = 4.5f;

    [SerializeField, Tooltip("Should vertical forces scale with ledge height?")]
    private bool scaleVerticalForcesByHeight = true;

    [Header("Force Multipliers - Horizontal")]
    [SerializeField, Tooltip("Reference horizontal distance for horizontal force calculations")]
    private float referenceHorizontalDistance = 4f;

    [SerializeField, Tooltip("Should horizontal forces scale with horizontal distance?")]
    private bool scaleHorizontalForcesByDistance = true;

    [Header("Movement Settings")]
    [SerializeField, Tooltip("Time to wait at start before applying forces (for rotation alignment)")]
    private float preClimbDelay = 0.2f;

    [SerializeField, Tooltip("How long to monitor the climb before considering it complete")]
    private float maxClimbDuration = 2f;

    [SerializeField, Tooltip("Distance threshold to consider climb complete")]
    private float completionDistanceThreshold = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color climbGizmoColor = Color.green;
    [SerializeField] private Color dropGizmoColor = Color.yellow;

    // Properties
    public Vector3 TopPosition => transform.position;
    public Vector3 BottomPosition => nodeLink != null && nodeLink.EndTransform != null ? nodeLink.EndTransform.position : transform.position;
    public float Height => Mathf.Abs(TopPosition.y - BottomPosition.y);
    public float HorizontalDistance => Vector3.Distance(
        new Vector3(TopPosition.x, 0, TopPosition.z),
        new Vector3(BottomPosition.x, 0, BottomPosition.z)
    );

    public float PreClimbDelay => preClimbDelay;
    public float MaxClimbDuration => maxClimbDuration;
    public float CompletionDistanceThreshold => completionDistanceThreshold;

    // Ledge Queue for managing multiple NPCs
    public string LedgeID => $"GroundLedge_{gameObject.GetInstanceID()}";

    private void Awake()
    {
        InitializeComponent();
        RegisterLedge();
    }

    private void OnDestroy()
    {
        UnregisterLedge();
    }

    private void RegisterLedge()
    {
        if (nodeLink != null)
        {
            linkRegistry[nodeLink] = this;
            DebugLog($"Registered in ground ledge registry");
        }
    }

    private void UnregisterLedge()
    {
        if (nodeLink != null && linkRegistry.ContainsKey(nodeLink))
        {
            linkRegistry.Remove(nodeLink);
            DebugLog($"Unregistered from ground ledge registry");
        }
    }

    public static ClimbableGroundLedge GetLedgeFromNodeLink(NodeLink2 nodeLink)
    {
        if (nodeLink == null) return null;

        if (linkRegistry.TryGetValue(nodeLink, out ClimbableGroundLedge ledge))
        {
            return ledge;
        }

        return null;
    }

    private void InitializeComponent()
    {
        if (nodeLink == null)
        {
            nodeLink = GetComponent<NodeLink2>();
        }

        if (nodeLink == null)
        {
            Debug.LogError($"{gameObject.name}: ClimbableGroundLedge requires a NodeLink2 component!");
            return;
        }

        if (nodeLink.EndTransform == null)
        {
            Debug.LogError($"{gameObject.name}: NodeLink2 End transform is not assigned!");
            return;
        }

        DebugLog($"Ground climbable ledge initialized - Height: {Height:F2}m, Horizontal Distance: {HorizontalDistance:F2}m");
    }

    public bool IsClimbingUp(Vector3 startPosition)
    {
        float distToBottom = Vector3.Distance(startPosition, BottomPosition);
        float distToTop = Vector3.Distance(startPosition, TopPosition);
        return distToBottom < distToTop;
    }

    /// <summary>
    /// Calculate the vertical force multiplier based on ledge height
    /// </summary>
    public float CalculateVerticalForceMultiplier()
    {
        if (!scaleVerticalForcesByHeight)
            return 1f;

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
        if (!scaleHorizontalForcesByDistance)
            return 1f;

        float horizontalMultiplier = HorizontalDistance / referenceHorizontalDistance;

        if (horizontalMultiplier < 0.5f)
        {
            horizontalMultiplier = 0.5f; // Minimum multiplier to ensure some forward force
        }

        return horizontalMultiplier;
    }

    public bool HasReachedTarget(Vector3 npcPosition, Vector3 targetPosition)
    {
        float distance = Vector3.Distance(npcPosition, targetPosition);
        return distance <= completionDistanceThreshold;
    }

    public string GetTraversalType(Vector3 startPosition)
    {
        return IsClimbingUp(startPosition) ? "CLIMB UP" : "DROP DOWN";
    }

    #region Debug & Editor

    private void DebugLog(string message)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[GroundClimbableLedge-{gameObject.name}] {message}");
        }
    }

    [Button("Validate Setup")]
    private void ValidateSetup()
    {
        if (nodeLink == null)
            nodeLink = GetComponent<NodeLink2>();

        if (nodeLink == null)
        {
            Debug.LogError($"{gameObject.name}: Missing NodeLink2 component!");
            return;
        }

        if (nodeLink.EndTransform == null)
        {
            Debug.LogError($"{gameObject.name}: NodeLink2.End is not assigned!");
            return;
        }

        float height = Height;
        float horizontalDist = HorizontalDistance;
        float verticalMultiplier = CalculateVerticalForceMultiplier();
        float horizontalMultiplier = CalculateHorizontalForceMultiplier();

        Debug.Log($"{gameObject.name}: Setup Valid!\n" +
                 $"Height (Vertical): {height:F2}m\n" +
                 $"Horizontal Distance: {horizontalDist:F2}m\n" +
                 $"Vertical Force Multiplier: {verticalMultiplier:F2}x\n" +
                 $"Horizontal Force Multiplier: {horizontalMultiplier:F2}x\n" +
                 $"Top: {TopPosition}\n" +
                 $"Bottom: {BottomPosition}");
    }

    [Button("Test Force Multipliers")]
    private void TestForceMultipliers()
    {
        float verticalMultiplier = CalculateVerticalForceMultiplier();
        float horizontalMultiplier = CalculateHorizontalForceMultiplier();

        Debug.Log($"=== Force Multiplier Test for {gameObject.name} ===\n" +
                 $"Height: {Height:F2}m\n" +
                 $"Reference Height: {referenceHeight:F2}m\n" +
                 $"Vertical Multiplier: {verticalMultiplier:F2}x\n\n" +
                 $"Horizontal Distance: {HorizontalDistance:F2}m\n" +
                 $"Reference Horizontal Distance: {referenceHorizontalDistance:F2}m\n" +
                 $"Horizontal Multiplier: {horizontalMultiplier:F2}x");
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        if (nodeLink == null)
            nodeLink = GetComponent<NodeLink2>();

        if (nodeLink == null || nodeLink.EndTransform == null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            return;
        }

        Vector3 top = TopPosition;
        Vector3 bottom = BottomPosition;
        Vector3 midpoint = (top + bottom) / 2f;

        // Draw climb direction (bottom to top)
        Gizmos.color = climbGizmoColor;
        DrawArrow(bottom, top, 0.3f);

        // Draw drop direction (top to bottom)
        Gizmos.color = dropGizmoColor;
        DrawArrow(top, bottom, 0.2f);

        // Draw top and bottom markers
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(top, 0.3f);
        Gizmos.DrawWireCube(top, Vector3.one * 0.3f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(bottom, 0.3f);
        Gizmos.DrawWireCube(bottom, Vector3.one * 0.3f);

        // Draw connecting line
        Gizmos.color = Color.white;
        Gizmos.DrawLine(top, bottom);

        // Draw height label position
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(midpoint, 0.15f);

        // Draw horizontal distance visualization
        Vector3 topFlat = new Vector3(top.x, bottom.y, top.z);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(bottom, topFlat);
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

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        if (nodeLink != null && nodeLink.EndTransform != null)
        {
            Vector3 top = TopPosition;
            Vector3 bottom = BottomPosition;

            // Draw traversal volume
            Gizmos.color = new Color(0, 1, 0, 0.1f);
            Gizmos.DrawCube((top + bottom) / 2f, new Vector3(1f, Height, 1f));

            // Draw completion threshold
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(top, completionDistanceThreshold);
            Gizmos.DrawWireSphere(bottom, completionDistanceThreshold);

            // Draw horizontal distance plane
            Vector3 topFlat = new Vector3(top.x, bottom.y, top.z);
            Gizmos.color = new Color(0, 0, 1, 0.3f);
            Gizmos.DrawLine(bottom, topFlat);
            Gizmos.DrawWireSphere(topFlat, 0.2f);
        }
    }

    #endregion
}