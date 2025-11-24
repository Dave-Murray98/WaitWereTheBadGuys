using UnityEngine;

/// <summary>
/// Simple, reliable validation for ledge climbing success.
/// Checks appropriate conditions based on ledge type and climb direction.
/// 
/// GROUND LEDGES: Distance check from target position
/// WATER CLIMB UP: Ground detector check
/// WATER DROP DOWN: Water detector check
/// </summary>
public class NPCLedgeClimbValidator : MonoBehaviour
{
    [Header("References")]
    private NPCController npcController; // REMOVED [SerializeField] - causes circular reference

    [Header("Ground Ledge Validation")]
    [SerializeField, Tooltip("Distance from target position to consider ground climb successful")]
    private float groundClimbSuccessRadius = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        // Get reference at runtime instead of serialization
        if (npcController == null)
            npcController = GetComponent<NPCController>();

        if (npcController == null)
        {
            Debug.LogError($"{gameObject.name}: NPCLedgeClimbValidator requires NPCController!");
            enabled = false;
        }
    }

    #region Ground Ledge Validation

    /// <summary>
    /// Check if ground ledge climb was successful (both up and down).
    /// Simple distance check from target position.
    /// </summary>
    public bool IsGroundLedgeClimbSuccessful(Vector3 targetPosition)
    {
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        bool isSuccessful = distanceToTarget <= groundClimbSuccessRadius;

        if (enableDebugLogs)
        {
            DebugLog($"Ground ledge validation: {(isSuccessful ? "SUCCESS" : "IN PROGRESS")} " +
                    $"(distance: {distanceToTarget:F2}m, threshold: {groundClimbSuccessRadius:F2}m)");
        }

        return isSuccessful;
    }

    #endregion

    #region Water Ledge Validation

    /// <summary>
    /// Check if water ledge climb UP was successful.
    /// Simple check: is NPC grounded?
    /// </summary>
    public bool IsWaterLedgeClimbUpSuccessful()
    {
        if (npcController.groundDetector == null)
        {
            Debug.LogWarning($"{gameObject.name}: No ground detector for water climb validation!");
            return false;
        }

        bool isSuccessful = npcController.groundDetector.IsGrounded;

        if (enableDebugLogs)
        {
            DebugLog($"Water ledge climb UP validation: {(isSuccessful ? "SUCCESS" : "IN PROGRESS")} " +
                    $"(grounded: {isSuccessful})");
        }

        return isSuccessful;
    }

    /// <summary>
    /// Check if water ledge DROP DOWN was successful.
    /// Simple check: is NPC in water?
    /// </summary>
    public bool IsWaterLedgeDropDownSuccessful()
    {
        if (npcController.waterDetector == null)
        {
            Debug.LogWarning($"{gameObject.name}: No water detector for water drop validation!");
            return false;
        }

        bool isSuccessful = npcController.waterDetector.IsInWater;

        if (enableDebugLogs)
        {
            DebugLog($"Water ledge DROP DOWN validation: {(isSuccessful ? "SUCCESS" : "IN PROGRESS")} " +
                    $"(in water: {isSuccessful})");
        }

        return isSuccessful;
    }

    #endregion

    #region Public Helpers

    /// <summary>
    /// Get the current distance to a target position (for progress tracking)
    /// </summary>
    public float GetDistanceToTarget(Vector3 targetPosition)
    {
        return Vector3.Distance(transform.position, targetPosition);
    }

    /// <summary>
    /// Set custom ground climb success radius (for testing/tuning)
    /// </summary>
    public void SetGroundClimbSuccessRadius(float radius)
    {
        groundClimbSuccessRadius = Mathf.Max(0.5f, radius);
        DebugLog($"Ground climb success radius set to {groundClimbSuccessRadius:F2}m");
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[LedgeClimbValidator-{gameObject.name}] {message}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Draw ground climb success radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, groundClimbSuccessRadius);
    }

    #endregion
}