using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Detects obstacles in front of the NPC during ground movement.
/// IMPROVED stuck detection: Tracks actual position change instead of just speed.
/// This prevents false positives when climbing stairs/slopes (where speed is low but progress is being made).
/// </summary>
public class NPCGroundObstacleDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Will auto-find if not assigned")]
    private NPCController npcController;

    [Header("Detection Settings")]
    [SerializeField, Tooltip("How far ahead to check for obstacles")]
    private float detectionDistance = 1.2f;

    [SerializeField, Tooltip("Height from ground to check (at NPC's legs/body)")]
    private float detectionHeightOffset = 0.1f;

    [SerializeField, Tooltip("Height to check for tall obstacles (chest/head level)")]
    private float upperDetectionHeightOffset = 1f;

    [SerializeField, Tooltip("Layers to check for obstacles")]
    private LayerMask obstacleLayerMask = -1;

    [Header("Obstacle Thresholds")]
    [SerializeField, Tooltip("Minimum obstacle height to report (too small = ignore)")]
    private float minObstacleHeight = 0.03f;

    [SerializeField, Tooltip("Maximum obstacle height to report (too tall = can't jump)")]
    private float maxObstacleHeight = 0.5f;

    [Header("Stuck Detection - Simplified")]
    [SerializeField, Tooltip("How long to track position before checking if stuck (seconds)")]
    private float stuckCheckDuration = 0.2f;

    [SerializeField, Tooltip("Minimum distance NPC must move in stuckCheckDuration to not be considered stuck")]
    private float minimumProgressDistance = 0.1f;

    [SerializeField, Tooltip("Minimum speed threshold - below this AND not making progress = stuck")]
    private float minimumSpeedThreshold = 0.3f;

    [Header("Performance Optimization")]
    [SerializeField, Tooltip("How often to check for obstacles (seconds)")]
    private float checkInterval = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugRays = true;
    [SerializeField] private bool forceAlwaysCheck = false; // Override stuck checks for testing

    // OPTIMIZATION: Timing and state management
    private float lastCheckTime = 0f;

    // IMPROVED: Position tracking for stuck detection
    private Vector3 lastRecordedPosition;
    private float positionRecordTime;
    private bool isStuck = false;

    // OPTIMIZATION: Cached detection results
    private bool hasObstacleAhead = false;
    private bool isTooTallToJump = false;
    private float cachedObstacleHeight = 0f;
    private Vector3 cachedObstaclePosition = Vector3.zero;
    private Vector3 cachedObstacleNormal = Vector3.zero;

    // OPTIMIZATION: Pre-calculated values
    private float minimumSpeedThresholdSqr;
    private float minimumProgressDistanceSqr;

    // Debug visualization
    private RaycastHit lastLowerRayHit;
    private RaycastHit lastUpperRayHit;
    private Vector3 lastRayDirection;

    // Public read-only properties
    public bool HasObstacleAhead => hasObstacleAhead;
    public bool IsTooTallToJump => isTooTallToJump;
    public float ObstacleHeight => cachedObstacleHeight;
    public Vector3 ObstaclePosition => cachedObstaclePosition;
    public Vector3 ObstacleNormal => cachedObstacleNormal;
    public bool IsStuck => isStuck;

    private void Awake()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        if (npcController == null)
        {
            npcController = GetComponent<NPCController>();
        }

        if (npcController == null)
        {
            Debug.LogError($"{gameObject.name}: NPCController not found! Obstacle detector requires NPCController.");
            enabled = false;
            return;
        }

        // OPTIMIZATION: Pre-calculate squared values
        minimumSpeedThresholdSqr = minimumSpeedThreshold * minimumSpeedThreshold;
        minimumProgressDistanceSqr = minimumProgressDistance * minimumProgressDistance;

        // OPTIMIZATION: Stagger initial checks across NPCs
        lastCheckTime = -Random.Range(0f, checkInterval);

        // Initialize position tracking
        lastRecordedPosition = transform.position;
        positionRecordTime = Time.time;

        DebugLog("Obstacle detector initialized");
    }

    private void Update()
    {
        // Update stuck state using improved position-based detection
        UpdateStuckState();

        // OPTIMIZATION: Only check at intervals
        if (Time.time - lastCheckTime >= checkInterval)
        {
            lastCheckTime = Time.time;
            PerformObstacleCheck();
        }
    }

    /// <summary>
    /// IMPROVED stuck detection: Checks if NPC has made meaningful progress over time.
    /// This approach is much more reliable than speed-based checks alone.
    /// </summary>
    private void UpdateStuckState()
    {
        // Quick exit: If reached destination, definitely not stuck
        if (npcController.groundController != null &&
            npcController.groundController.HasReachedDestination)
        {
            if (isStuck)
            {
                DebugLog("No longer stuck - reached destination");
                ResetStuckState();
            }
            return;
        }

        // Check how long we've been tracking this position
        float timeSinceLastRecord = Time.time - positionRecordTime;

        // Once enough time has passed, check if we've made progress
        if (timeSinceLastRecord >= stuckCheckDuration)
        {
            // Calculate how far we've moved since last position record
            Vector3 positionDelta = transform.position - lastRecordedPosition;
            positionDelta.y = 0f; // Ignore vertical movement (jumping, slopes, etc.)
            float distanceMovedSqr = positionDelta.sqrMagnitude;

            // Get current speed
            float currentSpeedSqr = 0f;
            if (npcController.groundController != null)
            {
                currentSpeedSqr = npcController.groundController.CurrentVelocity.sqrMagnitude;
            }

            // Determine if stuck based on TWO conditions:
            // 1. Moving very slowly (below threshold)
            // 2. Haven't made meaningful progress in the tracking duration
            bool isMovingSlowly = currentSpeedSqr < minimumSpeedThresholdSqr;
            bool hasNotMadeProgress = distanceMovedSqr < minimumProgressDistanceSqr;

            if (isMovingSlowly && hasNotMadeProgress)
            {
                if (!isStuck)
                {
                    isStuck = true;

                    if (enableDebugLogs)
                    {
                        float distanceMoved = Mathf.Sqrt(distanceMovedSqr);
                        DebugLog($"NPC is STUCK - Only moved {distanceMoved:F3}m in {stuckCheckDuration:F1}s (minimum: {minimumProgressDistance:F2}m)");
                    }
                }
            }
            else
            {
                // Making progress! Not stuck.
                if (isStuck)
                {
                    float distanceMoved = Mathf.Sqrt(distanceMovedSqr);
                    DebugLog($"No longer stuck - moved {distanceMoved:F2}m in {timeSinceLastRecord:F1}s");
                    ResetStuckState();
                }
            }

            // Reset position tracking for next check
            lastRecordedPosition = transform.position;
            positionRecordTime = Time.time;
        }
    }

    /// <summary>
    /// Reset stuck state and position tracking
    /// </summary>
    private void ResetStuckState()
    {
        isStuck = false;
        lastRecordedPosition = transform.position;
        positionRecordTime = Time.time;
    }

    /// <summary>
    /// Main obstacle detection logic - only runs when NPC is stuck
    /// </summary>
    private void PerformObstacleCheck()
    {
        // Reset detection state
        hasObstacleAhead = false;
        isTooTallToJump = false;

        // Only check when NPC is stuck (or force check is enabled for testing)
        if (!forceAlwaysCheck && !isStuck)
        {
            return;
        }

        // Make sure we're grounded before checking
        if (!IsGrounded())
        {
            return;
        }

        // Get movement direction
        Vector3 moveDirection = GetMovementDirection();
        if (moveDirection.sqrMagnitude < 0.01f)
        {
            moveDirection = transform.forward;
        }

        // Perform raycast detection
        DetectObstacleInDirection(moveDirection);
    }

    /// <summary>
    /// Check if NPC is on the ground
    /// </summary>
    private bool IsGrounded()
    {
        if (npcController.groundDetector == null)
            return false;

        return npcController.groundDetector.IsGrounded;
    }

    /// <summary>
    /// Get the direction the NPC is moving
    /// </summary>
    private Vector3 GetMovementDirection()
    {
        if (npcController.groundController == null)
            return transform.forward;

        Vector3 velocity = npcController.groundController.CurrentVelocity;

        // If not moving much, use forward direction
        if (velocity.sqrMagnitude < 0.01f)
            return transform.forward;

        // Use horizontal velocity direction (ignore vertical component)
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        return horizontalVelocity.normalized;
    }

    /// <summary>
    /// Cast rays at two heights to detect obstacles ahead
    /// Lower ray detects jumpable obstacles, upper ray detects walls/tall obstacles
    /// </summary>
    private void DetectObstacleInDirection(Vector3 moveDirection)
    {
        // Get detection origin points
        Vector3 lowerDetectionOrigin = transform.position + Vector3.up * detectionHeightOffset;
        Vector3 upperDetectionOrigin = transform.position + Vector3.up * upperDetectionHeightOffset;

        // Store ray direction for debug visualization
        lastRayDirection = moveDirection;

        // Cast LOWER ray (leg height - for jumpable obstacles)
        bool lowerHit = Physics.Raycast(lowerDetectionOrigin, moveDirection, out RaycastHit lowerHitInfo,
            detectionDistance, obstacleLayerMask);
        lastLowerRayHit = lowerHit ? lowerHitInfo : new RaycastHit();

        // Cast UPPER ray (chest height - for walls/tall obstacles)
        bool upperHit = Physics.Raycast(upperDetectionOrigin, moveDirection, out RaycastHit upperHitInfo,
            detectionDistance, obstacleLayerMask);
        lastUpperRayHit = upperHit ? upperHitInfo : new RaycastHit();

        // Analyze results
        if (lowerHit)
        {
            float obstacleDistance = lowerHitInfo.distance;
            float obstacleHeight = CalculateObstacleHeight(lowerHitInfo.point);

            if (enableDebugLogs)
            {
                DebugLog($"Lower ray hit {lowerHitInfo.collider.name} - Height: {obstacleHeight:F2}m, Distance: {obstacleDistance:F2}m");
            }

            // Check if upper ray also hit (indicates wall/tall obstacle)
            if (upperHit)
            {
                // Both rays hit - this is too tall to jump
                isTooTallToJump = true;
                hasObstacleAhead = false;

                if (enableDebugLogs)
                {
                    DebugLog($"✗ WALL DETECTED (too tall) - Upper ray also hit at {upperHitInfo.distance:F2}m");
                }
            }
            else
            {
                // Only lower ray hit - check if jumpable
                if (IsObstacleInRange(obstacleHeight))
                {
                    // Jumpable obstacle detected!
                    hasObstacleAhead = true;
                    isTooTallToJump = false;
                    cachedObstacleHeight = obstacleHeight;
                    cachedObstaclePosition = lowerHitInfo.point;
                    cachedObstacleNormal = lowerHitInfo.normal;

                    if (enableDebugLogs)
                    {
                        DebugLog($"✓ JUMPABLE OBSTACLE DETECTED - {lowerHitInfo.collider.name} - Height: {obstacleHeight:F2}m, Distance: {obstacleDistance:F2}m");
                    }
                }
                else
                {
                    hasObstacleAhead = false;

                    if (enableDebugLogs)
                    {
                        DebugLog($"✗ Hit ignored - Height {obstacleHeight:F2}m outside range ({minObstacleHeight:F2}m - {maxObstacleHeight:F2}m)");
                    }
                }
            }
        }
        else
        {
            // No obstacle detected
            hasObstacleAhead = false;
            isTooTallToJump = false;
        }
    }

    /// <summary>
    /// Calculate how tall an obstacle is relative to ground
    /// </summary>
    private float CalculateObstacleHeight(Vector3 obstaclePoint)
    {
        // Height is difference between obstacle hit point and NPC's ground level
        return obstaclePoint.y - transform.position.y;
    }

    /// <summary>
    /// Check if obstacle height is within detectable range
    /// </summary>
    private bool IsObstacleInRange(float obstacleHeight)
    {
        return obstacleHeight >= minObstacleHeight && obstacleHeight <= maxObstacleHeight;
    }

    #region Public API

    /// <summary>
    /// Force an immediate obstacle check (useful when movement controller needs fresh data)
    /// </summary>
    public void ForceCheck()
    {
        lastCheckTime = 0f; // Will trigger check on next Update
    }

    /// <summary>
    /// Get the direction from NPC to the obstacle
    /// </summary>
    public Vector3 GetDirectionToObstacle()
    {
        if (!hasObstacleAhead)
            return Vector3.zero;

        Vector3 direction = cachedObstaclePosition - transform.position;
        direction.y = 0; // Horizontal direction only
        return direction.normalized;
    }

    /// <summary>
    /// Check if NPC is moving toward the detected obstacle
    /// </summary>
    public bool IsMovingTowardObstacle()
    {
        if (!hasObstacleAhead || npcController.groundController == null)
            return false;

        Vector3 velocity = npcController.groundController.CurrentVelocity;
        if (velocity.sqrMagnitude < 0.01f)
            return false;

        Vector3 toObstacle = cachedObstaclePosition - transform.position;
        toObstacle.y = 0; // Ignore vertical component

        // OPTIMIZATION: Use dot product to check if moving toward obstacle
        float dot = Vector3.Dot(velocity.normalized, toObstacle.normalized);
        return dot > 0.5f; // Moving reasonably toward obstacle (within ~60 degrees)
    }

    /// <summary>
    /// Set obstacle height range
    /// </summary>
    public void SetObstacleHeightRange(float minHeight, float maxHeight)
    {
        minObstacleHeight = Mathf.Max(0f, minHeight);
        maxObstacleHeight = Mathf.Max(minObstacleHeight, maxHeight);
    }

    /// <summary>
    /// Clear cached obstacle data
    /// </summary>
    public void ClearObstacleData()
    {
        hasObstacleAhead = false;
        isTooTallToJump = false;
        cachedObstacleHeight = 0f;
        cachedObstaclePosition = Vector3.zero;
        cachedObstacleNormal = Vector3.zero;
        ResetStuckState();
    }

    #endregion

    #region Debug & Visualization

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCObstacleDetector-{gameObject.name}] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugRays)
            return;

        Vector3 lowerDetectionOrigin;
        Vector3 upperDetectionOrigin;

        if (Application.isPlaying)
        {
            lowerDetectionOrigin = transform.position + Vector3.up * detectionHeightOffset;
            upperDetectionOrigin = transform.position + Vector3.up * upperDetectionHeightOffset;
        }
        else
        {
            lowerDetectionOrigin = transform.position + Vector3.up * detectionHeightOffset;
            upperDetectionOrigin = transform.position + Vector3.up * upperDetectionHeightOffset;
        }

        // Draw detection height indicators
        Gizmos.color = Color.cyan;
        float heightIndicatorLength = 0.8f;
        Gizmos.DrawLine(
            lowerDetectionOrigin - transform.right * heightIndicatorLength,
            lowerDetectionOrigin + transform.right * heightIndicatorLength
        );

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(
            upperDetectionOrigin - transform.right * heightIndicatorLength,
            upperDetectionOrigin + transform.right * heightIndicatorLength
        );

        // Draw vertical lines from NPC base to detection heights
        Gizmos.color = new Color(0, 1, 1, 0.5f);
        Gizmos.DrawLine(transform.position, lowerDetectionOrigin);

        Gizmos.color = new Color(1, 0, 1, 0.5f);
        Gizmos.DrawLine(transform.position, upperDetectionOrigin);

        // Draw detection origin spheres
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(lowerDetectionOrigin, 0.15f);
        Gizmos.color = new Color(1, 0.5f, 0);
        Gizmos.DrawWireSphere(upperDetectionOrigin, 0.15f);

        if (!Application.isPlaying)
        {
            // In editor mode, show forward detection rays
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawLine(lowerDetectionOrigin, lowerDetectionOrigin + transform.forward * detectionDistance);
            Gizmos.DrawLine(upperDetectionOrigin, upperDetectionOrigin + transform.forward * detectionDistance);
            return;
        }

        // Runtime visualization
        if (!enabled)
            return;

        // Draw detection rays
        if (lastRayDirection.sqrMagnitude > 0.01f)
        {
            bool lowerHasHit = lastLowerRayHit.collider != null;

            if (lowerHasHit)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(lowerDetectionOrigin, lastLowerRayHit.point);
                Gizmos.DrawWireSphere(lastLowerRayHit.point, 0.15f);
            }
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(lowerDetectionOrigin, lowerDetectionOrigin + lastRayDirection * detectionDistance);
            }

            bool upperHasHit = lastUpperRayHit.collider != null;

            if (upperHasHit)
            {
                Gizmos.color = new Color(1, 0.5f, 0);
                Gizmos.DrawLine(upperDetectionOrigin, lastUpperRayHit.point);
                Gizmos.DrawWireSphere(lastUpperRayHit.point, 0.15f);
            }
            else
            {
                Gizmos.color = new Color(0.5f, 1, 0.5f);
                Gizmos.DrawLine(upperDetectionOrigin, upperDetectionOrigin + lastRayDirection * detectionDistance);
            }
        }

        // Draw stuck indicator
        if (isStuck)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.5f, 0.4f);

            // Draw position tracking visualization
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(lastRecordedPosition, transform.position);
            Gizmos.DrawWireSphere(lastRecordedPosition, 0.2f);
        }

        // Draw obstacle indicator
        if (hasObstacleAhead)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(cachedObstaclePosition, Vector3.one * 0.5f);
            Gizmos.DrawCube(cachedObstaclePosition, Vector3.one * 0.25f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, cachedObstaclePosition);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(cachedObstaclePosition, cachedObstaclePosition + cachedObstacleNormal * 0.8f);
        }

        // Draw wall indicator
        if (isTooTallToJump)
        {
            Vector3 wallPos = lastLowerRayHit.point;
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(wallPos, Vector3.one * 0.6f);
        }
    }

    #endregion
}