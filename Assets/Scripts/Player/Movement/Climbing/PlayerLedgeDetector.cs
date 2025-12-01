using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

/// <summary>
/// Simple ledge detection system that identifies climbable surfaces.
/// Provides clean events and properties for other systems to consume.
/// No climbing mechanics - pure detection only.
/// </summary>
public class PlayerLedgeDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField, Tooltip("How far forward to check for walls")]
    private float forwardDetectionRange = 1.5f;

    [SerializeField, Tooltip("Height offset from player center for forward wall detection")]
    private float wallDetectionHeight = 0.2f;

    [SerializeField, Tooltip("How high above detected wall to start ledge search")]
    private float ledgeSearchHeightAboveWall = 2.0f;

    [SerializeField, Tooltip("Maximum height a ledge can be above player")]
    private float maxLedgeHeight = 2.5f;

    [SerializeField, Tooltip("Minimum height a ledge must be above player")]
    private float minLedgeHeight = 0.8f;

    [Header("Surface Validation")]
    [SerializeField, Tooltip("Maximum angle from horizontal for a surface to be considered climbable")]
    private float maxSurfaceAngle = 30f;

    [SerializeField, Tooltip("Minimum width ledge must have to be climbable")]
    private float minLedgeWidth = 1.0f;

    [SerializeField, Tooltip("How far to check ledge width on each side")]
    private float widthCheckDistance = 0.8f;

    [Header("Layer Settings")]
    [SerializeField, Tooltip("What layers can be climbed")]
    private LayerMask climbableLayerMask = -1;

    [SerializeField, Tooltip("Layers to ignore during detection")]
    private LayerMask ignoreLayerMask = 0;

    [Header("Ledge Detection UI")]
    [SerializeField] private GameObject ledgeDetectorUIPanel;
    [SerializeField] private TextMeshProUGUI ledgeDetectorUIText;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showDetailedDebug = false;

    private PlayerController playerController;

    // Current ledge state
    private bool isLedgeDetected = false;
    private bool wasLedgeDetected = false;
    [HideInInspector] public LedgeInfo currentLedge = new LedgeInfo();

    // Detection positions (calculated each frame)
    private Vector3 forwardRayOrigin;
    private Vector3 ledgeSearchOrigin;

    // Raycast hit data
    private RaycastHit wallHit;
    private RaycastHit ledgeHit;

    // Events
    public event System.Action<LedgeInfo> OnLedgeDetected;
    public event System.Action OnLedgeLost;

    // Public Properties
    public bool IsLedgeDetected => isLedgeDetected;
    public LedgeInfo CurrentLedge => currentLedge;
    [ShowInInspector] public bool HasValidLedge => isLedgeDetected && currentLedge.IsValid;

    #region Detection Data Structure

    [System.Serializable]
    public struct LedgeInfo
    {
        public bool IsValid;
        public Vector3 LedgePosition;      // Where the ledge surface is
        public Vector3 LedgeNormal;        // Surface normal of the ledge
        public Vector3 WallNormal;         // Normal of the wall below ledge
        public float LedgeHeight;          // Height above player
        public float LedgeWidth;           // How wide the ledge is
        public float SurfaceAngle;         // How flat the surface is (degrees from horizontal)
        public Transform LedgeTransform;   // What object the ledge is on (can be null)

        public static LedgeInfo Invalid => new LedgeInfo { IsValid = false };

        public override string ToString()
        {
            if (!IsValid) return "Invalid Ledge";

            return $"Ledge - Height: {LedgeHeight:F2}m, Width: {LedgeWidth:F2}m, " +
                   $"Angle: {SurfaceAngle:F1}°, Pos: {LedgePosition}";
        }
    }

    #endregion

    private void Start()
    {
        playerController = GetComponent<PlayerController>();

        ToggleLedgeUI(false);
    }

    #region Update Loop

    private void Update()
    {
        UpdateDetection();
        CheckLedgeStateChanges();
    }

    /// <summary>
    /// Main detection update - calculates positions and performs detection
    /// </summary>
    private void UpdateDetection()
    {

        //if the player is fully underwater, don't detect any ledges
        if (playerController.swimmingDepthManager.CurrentSwimmingDepthState == SwimmingDepthState.UnderwaterSwimming)
            return;

        UpdateDetectionPositions();

        wasLedgeDetected = isLedgeDetected;
        isLedgeDetected = false;
        currentLedge = LedgeInfo.Invalid;

        // Step 1: Check for wall in front of player
        if (DetectWallInFront())
        {
            // Step 2: Search for ledge above the detected wall
            if (DetectLedgeAboveWall())
            {
                // Step 3: Validate the detected ledge
                if (ValidateDetectedLedge())
                {
                    isLedgeDetected = true;

                    if (showDetailedDebug && enableDebugLogs && Time.frameCount % 30 == 0)
                    {
                        DebugLog($"Ledge detected: {currentLedge}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Update detection ray origins based on player position
    /// </summary>
    private void UpdateDetectionPositions()
    {
        Vector3 playerCenter = transform.position;

        // Forward ray starts from player center + height offset
        forwardRayOrigin = playerCenter + Vector3.up * wallDetectionHeight;

        // Ledge search starts above the wall hit point (calculated after wall detection)
    }

    #endregion

    #region Detection Steps

    /// <summary>
    /// Step 1: Detect if there's a wall in front of the player
    /// </summary>
    private bool DetectWallInFront()
    {
        Vector3 forwardDirection = transform.forward;

        // Cast forward to detect walls
        if (Physics.Raycast(forwardRayOrigin, forwardDirection, out wallHit,
            forwardDetectionRange, climbableLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Check if we should ignore this layer
            if (IsLayerIgnored(wallHit.collider.gameObject.layer))
            {
                return false;
            }

            // Basic wall validation - must be roughly vertical
            float wallAngle = Vector3.Angle(wallHit.normal, Vector3.up);

            // Wall should be between 60-120 degrees from up (roughly vertical)
            if (wallAngle > 60f && wallAngle < 120f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Step 2: Search for ledge surface above the detected wall
    /// </summary>
    private bool DetectLedgeAboveWall()
    {
        // Calculate search origin above the wall hit point
        Vector3 searchStart = wallHit.point + Vector3.up * ledgeSearchHeightAboveWall;
        ledgeSearchOrigin = searchStart;

        // Cast downward to find the ledge surface
        if (Physics.Raycast(searchStart, Vector3.down, out ledgeHit,
            ledgeSearchHeightAboveWall + 0.5f, climbableLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Check if we should ignore this layer
            if (IsLayerIgnored(ledgeHit.collider.gameObject.layer))
            {
                DebugLog("Hit ledge but layer ignored");
                return false;
            }

            // Calculate ledge height relative to player
            float ledgeHeight = ledgeHit.point.y - transform.position.y;

            // Check height constraints
            if (ledgeHeight >= minLedgeHeight && ledgeHeight <= maxLedgeHeight)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Step 3: Validate the detected ledge meets all criteria
    /// </summary>
    private bool ValidateDetectedLedge()
    {
        // Check surface angle (must be relatively flat)
        float surfaceAngle = Vector3.Angle(ledgeHit.normal, Vector3.up);
        if (surfaceAngle > maxSurfaceAngle)
        {
            DebugLog($"Surface angle too steep - {surfaceAngle:F1}°");
            return false;
        }

        // Check ledge width by casting left and right
        float ledgeWidth = CalculateLedgeWidth();
        if (ledgeWidth < minLedgeWidth)
        {
            DebugLog($"Ledge too narrow - {ledgeWidth:F2}m");
            return false;
        }

        // All validation passed - populate ledge info
        currentLedge = new LedgeInfo
        {
            IsValid = true,
            LedgePosition = ledgeHit.point,
            LedgeNormal = ledgeHit.normal,
            WallNormal = wallHit.normal,
            LedgeHeight = ledgeHit.point.y - transform.position.y,
            LedgeWidth = ledgeWidth,
            SurfaceAngle = surfaceAngle,
            LedgeTransform = ledgeHit.collider.transform
        };

        return true;
    }

    /// <summary>
    /// Calculate how wide the detected ledge is
    /// </summary>
    private float CalculateLedgeWidth()
    {
        Vector3 ledgePoint = ledgeHit.point;
        Vector3 playerRight = transform.right;

        // Cast from ledge point to left and right to find width
        float leftDistance = CastForLedgeEdge(ledgePoint, -playerRight);
        float rightDistance = CastForLedgeEdge(ledgePoint, playerRight);

        return leftDistance + rightDistance;
    }

    /// <summary>
    /// Cast in a direction to find where the ledge ends
    /// </summary>
    private float CastForLedgeEdge(Vector3 startPoint, Vector3 direction)
    {
        // Cast horizontally from the ledge point
        Vector3 castOrigin = startPoint + Vector3.up * 0.1f; // Slightly above ledge surface

        if (Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit,
            0.5f, climbableLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Check if this hit is on the same surface as our ledge
            if (hit.collider == ledgeHit.collider &&
                Vector3.Angle(hit.normal, ledgeHit.normal) < 10f) // Same surface
            {
                // Continue casting outward to find the edge
                for (float distance = 0.2f; distance <= widthCheckDistance; distance += 0.2f)
                {
                    Vector3 checkPoint = startPoint + direction * distance + Vector3.up * 0.1f;

                    if (!Physics.Raycast(checkPoint, Vector3.down, out RaycastHit edgeHit,
                        0.5f, climbableLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        return distance - 0.2f; // Found edge
                    }

                    // Check if still same surface
                    if (edgeHit.collider != ledgeHit.collider ||
                        Vector3.Angle(edgeHit.normal, ledgeHit.normal) > 10f)
                    {
                        return distance - 0.2f; // Surface changed
                    }
                }

                return widthCheckDistance; // Didn't find edge within range
            }
        }

        return 0f; // No valid surface found
    }

    #endregion

    #region State Management

    /// <summary>
    /// Check for ledge state changes and fire events
    /// </summary>
    private void CheckLedgeStateChanges()
    {
        if (isLedgeDetected != wasLedgeDetected)
        {
            if (isLedgeDetected)
            {
                DebugLog($"Ledge detected: {currentLedge}");
                OnLedgeDetected?.Invoke(currentLedge);
                ToggleLedgeUI(true);
            }
            else
            {
                DebugLog("Ledge lost");
                OnLedgeLost?.Invoke();
                ToggleLedgeUI(false);
            }
        }
    }

    public void ToggleLedgeUI(bool show)
    {
        if (ledgeDetectorUIPanel == null || ledgeDetectorUIText == null)
            return;

        if (show)
            ledgeDetectorUIPanel.SetActive(true);
        else
            ledgeDetectorUIPanel.SetActive(false);


    }

    /// <summary>
    /// Force a detection update (useful for external systems)
    /// </summary>
    public void ForceDetectionUpdate()
    {
        UpdateDetection();
        CheckLedgeStateChanges();
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Check if a layer should be ignored during detection
    /// </summary>
    private bool IsLayerIgnored(int layer)
    {
        return (ignoreLayerMask.value & (1 << layer)) != 0;
    }

    /// <summary>
    /// Get detection info for debugging
    /// </summary>
    public string GetDetectionInfo()
    {
        if (!isLedgeDetected)
        {
            return "No ledge detected";
        }

        return $"Ledge Detection Info:\n{currentLedge}\n" +
               $"Wall Normal: {currentLedge.WallNormal}\n" +
               $"Transform: {(currentLedge.LedgeTransform ? currentLedge.LedgeTransform.name : "None")}";
    }

    /// <summary>
    /// Set detection parameters at runtime
    /// </summary>
    public void SetDetectionParameters(float maxHeight, float minWidth, float maxAngle)
    {
        maxLedgeHeight = Mathf.Max(0f, maxHeight);
        minLedgeWidth = Mathf.Max(0f, minWidth);
        maxSurfaceAngle = Mathf.Clamp(maxAngle, 0f, 90f);

        DebugLog($"Detection parameters updated - Max Height: {maxLedgeHeight:F1}m, " +
                $"Min Width: {minLedgeWidth:F1}m, Max Angle: {maxSurfaceAngle:F1}°");
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerLedgeDetector] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        if (Application.isPlaying)
        {
            DrawRuntimeGizmos();
        }
        else
        {
            DrawEditorPreview();
        }
    }

    private void DrawRuntimeGizmos()
    {
        // Forward detection ray
        Gizmos.color = wallHit.collider != null ? Color.red : Color.green;
        Gizmos.DrawRay(forwardRayOrigin, transform.forward * forwardDetectionRange);

        if (wallHit.collider != null)
        {
            // Wall hit point
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(wallHit.point, 0.1f);

            // Ledge search ray
            Gizmos.color = ledgeHit.collider != null ? Color.blue : Color.yellow;
            Gizmos.DrawRay(ledgeSearchOrigin, Vector3.down * (ledgeSearchHeightAboveWall + 0.5f));

            if (ledgeHit.collider != null)
            {
                // Ledge hit point
                Gizmos.color = isLedgeDetected ? Color.cyan : Color.yellow;
                Gizmos.DrawWireSphere(ledgeHit.point, 0.15f);

                if (isLedgeDetected)
                {
                    // Ledge surface normal
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawRay(ledgeHit.point, ledgeHit.normal * 0.5f);

                    // Ledge width indicators
                    Gizmos.color = Color.magenta;
                    Vector3 right = transform.right;
                    float halfWidth = currentLedge.LedgeWidth * 0.5f;
                    Gizmos.DrawWireSphere(ledgeHit.point + right * halfWidth, 0.08f);
                    Gizmos.DrawWireSphere(ledgeHit.point - right * halfWidth, 0.08f);

                    // Draw ledge width line
                    Gizmos.DrawLine(
                        ledgeHit.point - right * halfWidth,
                        ledgeHit.point + right * halfWidth
                    );
                }
            }
        }

        // Height range indicators
        Gizmos.color = Color.gray;
        Vector3 playerPos = transform.position;
        Gizmos.DrawWireCube(playerPos + Vector3.up * minLedgeHeight, new Vector3(0.2f, 0.02f, 0.2f));
        Gizmos.DrawWireCube(playerPos + Vector3.up * maxLedgeHeight, new Vector3(0.3f, 0.02f, 0.3f));
    }

    private void DrawEditorPreview()
    {
        // Preview detection range
        Gizmos.color = Color.green;
        Vector3 origin = transform.position + Vector3.up * wallDetectionHeight;
        Gizmos.DrawRay(origin, transform.forward * forwardDetectionRange);

        // Height constraints preview
        Gizmos.color = Color.gray;
        Vector3 playerPos = transform.position;
        Gizmos.DrawWireCube(playerPos + Vector3.up * minLedgeHeight, new Vector3(0.2f, 0.02f, 0.2f));
        Gizmos.DrawWireCube(playerPos + Vector3.up * maxLedgeHeight, new Vector3(0.3f, 0.02f, 0.3f));
    }

    #endregion
}