using UnityEngine;
using System;
using Sirenix.OdinInspector;

/// <summary>
/// DEAD SIMPLE: Ground detection that just uses this GameObject's position.
/// Position this GameObject at your player's feet and it will detect ground from there.
/// No calculations, no auto-positioning, no complexity.
/// </summary>
public class PlayerGroundDetector : MonoBehaviour
{
    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundMask = 1;
    [SerializeField] private float detectionRadius = 0.4f;
    [SerializeField] private float detectionDistance = 0.55f;
    public float maxSlopeAngle = 45f;
    [HideInInspector] public float currentSlopeAngle = 0f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = true;

    // Ground state
    private bool isGrounded = false;
    private bool wasGrounded = false;
    private Vector3 groundNormal = Vector3.up;
    private GroundType currentGroundType = GroundType.Default;
    private float distanceToGround = 0f;
    private RaycastHit lastGroundHit;

    // Events
    public event Action OnGrounded;
    public event Action OnLeftGround;
    public event Action<RaycastHit> OnGroundHitChanged;

    // Public properties
    [ShowInInspector, ReadOnly] public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal => groundNormal;
    public GroundType CurrentGroundType => currentGroundType;
    public float DistanceToGround => distanceToGround;
    public RaycastHit GroundHit => lastGroundHit;

    private void Update()
    {
        PerformGroundCheck();
        CheckForStateChanges();
    }

    /// <summary>
    /// Performs a ground check for ground from this GameObject's position
    /// </summary>
    private void PerformGroundCheck()
    {
        // Use this GameObject's world position directly
        Vector3 checkPosition = transform.position;

        if (Physics.SphereCast(checkPosition, detectionRadius, Vector3.down, out RaycastHit hit, detectionDistance, groundMask))
        {
            // Check slope angle
            currentSlopeAngle = Vector3.Angle(hit.normal, Vector3.up);

            // if (slopeAngle <= slopeLimit)
            // {
            isGrounded = true;
            groundNormal = hit.normal;
            distanceToGround = hit.distance;
            currentGroundType = DetermineGroundType(hit.collider);
            lastGroundHit = hit;

            if (enableDebugLogs)
            {
                DebugLog($"Ground detected - Distance: {hit.distance:F3}, Angle: {currentSlopeAngle:F1}°");
            }

        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
            distanceToGround = detectionDistance;
            currentGroundType = GroundType.Default;
        }
    }

    private GroundType DetermineGroundType(Collider groundCollider)
    {
        if (groundCollider == null) return GroundType.Default;

        var groundTypeId = groundCollider.GetComponent<GroundTypeIdentifier>();
        return groundTypeId?.groundType ?? GroundType.Default;
    }

    private void CheckForStateChanges()
    {
        if (isGrounded != wasGrounded)
        {
            if (isGrounded)
            {
                OnGrounded?.Invoke();
                DebugLog("Player grounded");
            }
            else
            {
                OnLeftGround?.Invoke();
                DebugLog("Player left ground");
            }

            wasGrounded = isGrounded;
        }

        if (isGrounded)
        {
            OnGroundHitChanged?.Invoke(lastGroundHit);
        }
    }

    public void ForceGroundCheck()
    {
        PerformGroundCheck();
        CheckForStateChanges();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GroundDetector] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (showDebugGizmos)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * detectionDistance);
        }
    }

}