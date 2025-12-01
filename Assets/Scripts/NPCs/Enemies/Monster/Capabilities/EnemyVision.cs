using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Efficient trigger-based vision system for enemies.
/// Uses a cone trigger collider to detect when the player enters the vision area,
/// then performs staggered raycasts toward the player for line-of-sight validation.
/// </summary>
public class EnemyVision : MonoBehaviour
{
    [Header("Vision Configuration")]
    [SerializeField] private Transform visionOrigin;
    [SerializeField] private float visionRange = 15f;

    [Header("Raycast Settings")]
    [SerializeField] private int totalRaycastsToPlayer = 5;
    [SerializeField] private float raycastInterval = 0.1f; // Time between each raycast
    [SerializeField] private float raycastSpread = 1f; // How spread out the rays are around the player

    [Header("Detection Settings")]
    [Tooltip("Set this to everything that the enemy's vision raycasts can collide with (ground, obstacles, player, etc.)")]
    [SerializeField] private LayerMask layersEnemyCanSee = -1;
    [SerializeField] private int playerLayerMaskInt = 6;
    [SerializeField] private float minDetectionRatio = 0.4f; // Minimum percentage of rays that must hit player

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = false;
    [SerializeField] private bool enableDebugVisualization = true;
    [SerializeField] private float debugLineDuration = 0.5f;

    // Private variables
    private Transform player;
    private Coroutine raycastCoroutine;
    [ShowInInspector] private bool playerInTrigger = false;

    // Vision state
    [ShowInInspector] private bool canSeePlayer = false;
    private Vector3 lastKnownPlayerPosition;
    private float timeSinceLastDetection = 0f;

    // Raycast tracking
    private int raysHittingPlayer = 0;
    private int totalRaysThisCheck = 0;

    // Events
    public Action<bool> OnPlayerVisibilityChanged;
    public Action<Vector3> OnPlayerDetected;
    public Action OnPlayerLost;

    #region Public Properties
    public bool CanSeePlayer => canSeePlayer;
    public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;
    public float TimeSinceLastDetection => timeSinceLastDetection;
    public bool PlayerInTrigger => playerInTrigger;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        SetupComponents();
    }

    private void Start()
    {
        if (player == null)
            FindPlayer();
    }

    private void Update()
    {
        if (!canSeePlayer)
        {
            timeSinceLastDetection += Time.deltaTime;
        }
    }

    private void OnDisable()
    {
        StopRaycastChecks();
    }
    #endregion

    #region Setup and Initialization
    private void SetupComponents()
    {
        // Set vision origin to parent transform if not specified
        if (visionOrigin == null)
        {
            visionOrigin = transform.parent;
            if (visionOrigin == null)
            {
                visionOrigin = transform;
                Debug.LogWarning($"{gameObject.name}: No parent found for vision origin, using self");
            }
        }

        // Ensure this GameObject has a trigger collider
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogError($"{gameObject.name}: No collider found! This component needs a trigger collider.");
        }
        else if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning($"{gameObject.name}: Collider is not set as trigger. Setting it now.");
            triggerCollider.isTrigger = true;
        }
    }

    private void FindPlayer()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
            DebugLog("Player found and assigned to vision system");
        }
        else
        {
            Debug.LogError($"{gameObject.name}: Could not find player! Make sure player has 'Player' tag.");
        }
    }
    #endregion

    #region Trigger Detection
    private void OnTriggerEnter(Collider other)
    {
        // Check if the player entered the trigger
        if (IsPlayerCollider(other))
        {
            playerInTrigger = true;
            DebugLog("Player entered vision trigger - starting raycast checks");
            StartRaycastChecks();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Check if the player left the trigger
        if (IsPlayerCollider(other))
        {
            playerInTrigger = false;
            DebugLog("Player left vision trigger - stopping raycast checks");
            StopRaycastChecks();

            // Player is definitely not visible if they're outside the trigger
            SetPlayerDetected(false, Vector3.zero);
        }
    }

    private bool IsPlayerCollider(Collider other)
    {
        return other.gameObject.layer == playerLayerMaskInt ||
               other.CompareTag("Player") ||
               other.transform == player;
    }
    #endregion

    #region Raycast System
    private void StartRaycastChecks()
    {
        if (raycastCoroutine != null)
        {
            StopCoroutine(raycastCoroutine);
        }

        raycastCoroutine = StartCoroutine(StaggeredRaycastCoroutine());
    }

    private void StopRaycastChecks()
    {
        if (raycastCoroutine != null)
        {
            StopCoroutine(raycastCoroutine);
            raycastCoroutine = null;
        }
    }

    /// <summary>
    /// Performs staggered raycasts toward the player one at a time
    /// </summary>
    private IEnumerator StaggeredRaycastCoroutine()
    {
        while (playerInTrigger)
        {
            if (player != null)
            {
                // Reset counters for new check cycle
                raysHittingPlayer = 0;
                totalRaysThisCheck = 0;

                // Perform raycasts one by one with delays
                for (int i = 0; i < totalRaycastsToPlayer; i++)
                {
                    if (!playerInTrigger) break; // Exit early if player left trigger

                    PerformSingleRaycastToPlayer(i);
                    totalRaysThisCheck++;

                    yield return new WaitForSeconds(raycastInterval);
                }

                // Evaluate detection after all rays are complete
                EvaluatePlayerDetection();
            }

            // Small delay before next full check cycle
            yield return new WaitForSeconds(0.1f);
        }
    }

    /// <summary>
    /// Performs a single raycast toward the player with some spread
    /// </summary>
    private void PerformSingleRaycastToPlayer(int rayIndex)
    {
        if (player == null) return;

        Vector3 rayOrigin = visionOrigin.position;

        // Calculate direction to player with some random spread
        Vector3 baseDirection = (player.position - rayOrigin).normalized;

        // Add some spread to the raycast (different for each ray)
        Vector3 spreadOffset = GetRaySpreadOffset(rayIndex);
        Vector3 rayDirection = (baseDirection + spreadOffset).normalized;

        DebugLog($"Raycast {rayIndex}: From {rayOrigin} toward player with direction {rayDirection}");

        // Perform the raycast
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, visionRange, layersEnemyCanSee))
        {
            DebugLog($"Raycast {rayIndex} hit: {hit.collider.name} at distance {hit.distance}");

            // Check if we hit the player
            if (hit.collider.gameObject.layer == playerLayerMaskInt)
            {
                raysHittingPlayer++;
                DebugLog($"Raycast {rayIndex} hit player! ({raysHittingPlayer}/{totalRaysThisCheck + 1})");

                // Draw debug ray in green if it hits the player
                if (enableDebugVisualization)
                {
                    Debug.DrawRay(rayOrigin, rayDirection * hit.distance, Color.green, debugLineDuration);
                }
            }
            else
            {
                DebugLog($"Raycast {rayIndex} blocked by: {hit.collider.name}");

                // Draw debug ray in red if it hits an obstacle
                if (enableDebugVisualization)
                {
                    Debug.DrawRay(rayOrigin, rayDirection * hit.distance, Color.red, debugLineDuration);
                }
            }
        }
        else
        {
            DebugLog($"Raycast {rayIndex} hit nothing");

            // Draw debug ray in yellow if it doesn't hit anything
            if (enableDebugVisualization)
            {
                Debug.DrawRay(rayOrigin, rayDirection * visionRange, Color.yellow, debugLineDuration);
            }
        }
    }

    /// <summary>
    /// Generates spread offset for each raycast to sample around the player
    /// </summary>
    private Vector3 GetRaySpreadOffset(int rayIndex)
    {
        if (totalRaycastsToPlayer == 1) return Vector3.zero;

        // Create a pattern of offsets around the player
        float angle = (360f / totalRaycastsToPlayer) * rayIndex;
        float spread = raycastSpread;

        Vector3 offset = new Vector3(
            Mathf.Sin(angle * Mathf.Deg2Rad) * spread,
            Mathf.Cos(angle * Mathf.Deg2Rad) * spread * 0.5f, // Less vertical spread
            0
        );

        return offset * 0.1f; // Scale down the offset
    }

    /// <summary>
    /// Evaluates if the player is detected based on raycast results
    /// </summary>
    private void EvaluatePlayerDetection()
    {
        float detectionRatio = totalRaysThisCheck > 0 ? (float)raysHittingPlayer / totalRaysThisCheck : 0f;
        bool detectedThisCheck = detectionRatio >= minDetectionRatio;

        DebugLog($"Detection evaluation: {raysHittingPlayer}/{totalRaysThisCheck} = {detectionRatio:F2} (min: {minDetectionRatio:F2}) -> {(detectedThisCheck ? "DETECTED" : "NOT DETECTED")}");

        if (detectedThisCheck)
        {
            SetPlayerDetected(true, player.position);
        }
        else
        {
            SetPlayerDetected(false, Vector3.zero);
        }
    }

    /// <summary>
    /// Updates the player detection state and triggers events
    /// </summary>
    private void SetPlayerDetected(bool detected, Vector3 hitPosition)
    {
        bool previousCanSeePlayer = canSeePlayer;
        canSeePlayer = detected;

        // Update last known position if we can see the player
        if (canSeePlayer)
        {
            lastKnownPlayerPosition = hitPosition;
            timeSinceLastDetection = 0f;

            // Trigger detection event
            OnPlayerDetected?.Invoke(lastKnownPlayerPosition);
        }

        // Check for visibility state changes
        if (canSeePlayer != previousCanSeePlayer)
        {
            OnPlayerVisibilityChanged?.Invoke(canSeePlayer);

            if (canSeePlayer)
            {
                DebugLog("Player DETECTED!");
            }
            else
            {
                DebugLog("Player LOST from sight");
                OnPlayerLost?.Invoke();
            }
        }
    }
    #endregion

    #region Public Interface
    /// <summary>
    /// Manually trigger a quick vision check (if player is in trigger)
    /// </summary>
    public void PerformQuickVisionCheck()
    {
        if (playerInTrigger && player != null)
        {
            // Do a single raycast directly to player
            Vector3 rayOrigin = visionOrigin.position;
            Vector3 rayDirection = (player.position - rayOrigin).normalized;

            if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, visionRange, layersEnemyCanSee))
            {
                bool hitPlayer = hit.collider.gameObject.layer == playerLayerMaskInt;
                SetPlayerDetected(hitPlayer, hitPlayer ? hit.point : Vector3.zero);
            }
            else
            {
                SetPlayerDetected(false, Vector3.zero);
            }
        }
    }

    /// <summary>
    /// Update vision parameters at runtime
    /// </summary>
    public void UpdateVisionParameters(float newRange, int newRayCount, float newInterval)
    {
        visionRange = newRange;
        totalRaycastsToPlayer = newRayCount;
        raycastInterval = newInterval;

        DebugLog("Vision parameters updated");
    }

    /// <summary>
    /// Force stop all vision checks (useful for when enemy is disabled)
    /// </summary>
    public void ForceStopVision()
    {
        playerInTrigger = false;
        StopRaycastChecks();
        SetPlayerDetected(false, Vector3.zero);
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EnemyVision] {message}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        if (visionOrigin == null) return;

        // Draw vision range
        Gizmos.color = canSeePlayer ? Color.red : (playerInTrigger ? Color.yellow : Color.blue);
        Gizmos.DrawWireSphere(visionOrigin.position, visionRange);

        // Draw connection to player if in trigger
        if (playerInTrigger && player != null)
        {
            Gizmos.color = canSeePlayer ? Color.green : Color.red;
            Gizmos.DrawLine(visionOrigin.position, player.position);
        }

        // Draw last known player position
        if (lastKnownPlayerPosition != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lastKnownPlayerPosition, 0.5f);
        }
    }
    #endregion
}