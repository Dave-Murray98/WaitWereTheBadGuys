using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Provides Alien Isolation-style dynamic player awareness based on distance.
/// The closer the monster is to the player, the less it "knows" about the player's exact location.
/// This creates natural tension where the monster hunts near the player when far away,
/// but patrols randomly when close, allowing for hiding opportunities.
/// </summary>
public class MonsterAwarenessOfPlayerPostionSystem : MonoBehaviour
{
    [System.Serializable]
    public enum PlayerProximity
    {
        Far,    // Monster "knows" roughly where player is
        Medium, // Monster has some idea of player location  
        Close   // Monster has no knowledge of player location
    }

    [Header("Distance Thresholds")]
    [Tooltip("Distance beyond which the monster considers itself 'far' from the player")]
    [SerializeField] private float farThreshold = 20f;

    [Tooltip("Distance beyond which the monster considers itself at 'medium' distance from the player")]
    [SerializeField] private float mediumThreshold = 10f;

    // Anything closer than mediumThreshold is considered 'Close'

    [Header("Patrol Influence Radius")]
    [Tooltip("When FAR: Monster searches this radius around player's position for patrol points")]
    [SerializeField] private float farInfluenceRadius = 20f;

    [Tooltip("When MEDIUM: Monster searches this radius around player's position for patrol points")]
    [SerializeField] private float mediumInfluenceRadius = 30f;

    [Tooltip("When CLOSE: Monster searches this radius around player's position (or uses random if 0)")]
    [SerializeField] private float closeInfluenceRadius = 40f; // 0 = completely random

    [Header("Performance")]
    [Tooltip("How often to recalculate distance to player (in seconds)")]
    [SerializeField] private float updateInterval = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = false;

    // References
    private Transform playerTransform;
    private Transform monsterTransform;

    // State
    [ShowInInspector, ReadOnly] private PlayerProximity currentProximity;
    [ShowInInspector, ReadOnly] private float currentDistanceToPlayer;
    private float updateTimer;

    // Events - other systems can subscribe to proximity changes
    public System.Action<PlayerProximity> OnProximityChanged;

    private void Awake()
    {
        monsterTransform = transform;
    }

    /// <summary>
    /// Initialize the system with player reference
    /// </summary>
    public void Initialize(Transform player)
    {
        playerTransform = player;

        if (playerTransform == null)
        {
            Debug.LogError($"{gameObject.name}: PlayerAwarenessSystem requires a valid player transform!");
            enabled = false;
            return;
        }

        // Do initial calculation
        UpdateProximity();

        DebugLog($"PlayerAwarenessSystem initialized. Current proximity: {currentProximity}");
    }

    private void Update()
    {
        if (playerTransform == null) return;

        updateTimer += Time.deltaTime;

        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateProximity();
        }
    }

    /// <summary>
    /// Calculate current distance and update proximity level
    /// </summary>
    private void UpdateProximity()
    {
        currentDistanceToPlayer = Vector3.Distance(monsterTransform.position, playerTransform.position);

        PlayerProximity newProximity = CalculateProximity(currentDistanceToPlayer);

        if (newProximity != currentProximity)
        {
            PlayerProximity oldProximity = currentProximity;
            currentProximity = newProximity;

            DebugLog($"Proximity changed: {oldProximity} -> {currentProximity} (Distance: {currentDistanceToPlayer:F1})");

            OnProximityChanged?.Invoke(currentProximity);
        }
    }

    /// <summary>
    /// Determine proximity level based on distance
    /// </summary>
    private PlayerProximity CalculateProximity(float distance)
    {
        if (distance >= farThreshold)
            return PlayerProximity.Far;
        else if (distance >= mediumThreshold)
            return PlayerProximity.Medium;
        else
            return PlayerProximity.Close;
    }

    /// <summary>
    /// Get a patrol position influenced by current player awareness level.
    /// This is the main method that integrates with your existing patrol system.
    /// </summary>
    /// <param name="fallbackPosition">Position to use for random patrol if no player influence</param>
    /// <returns>Vector3 position for patrol destination</returns>
    public Vector3 GetInfluencedPatrolPosition(Vector3 fallbackPosition)
    {
        if (playerTransform == null)
        {
            DebugLog("No player reference, using fallback position");
            return fallbackPosition;
        }

        Vector3 playerPosition = playerTransform.position;
        float searchRadius = GetCurrentInfluenceRadius();

        DebugLog($"Getting patrol position - Proximity: {currentProximity}, Radius: {searchRadius}");

        // If radius is 0 (close proximity), use completely random patrol
        if (searchRadius <= 0f)
        {
            DebugLog("Using random patrol position (close proximity)");
            return NPCPathfindingUtilities.Instance.GetRandomValidPosition(fallbackPosition);
        }

        // Try to find a valid position within the influence radius of the player
        Vector3 influencedPosition = NPCPathfindingUtilities.Instance.GetRandomValidPositionNearPoint(
            playerPosition,
            searchRadius
        );

        DebugLog($"Found influenced patrol position at distance {Vector3.Distance(influencedPosition, playerPosition):F1} from player");
        return influencedPosition;
    }

    /// <summary>
    /// Get the current influence radius based on proximity level
    /// </summary>
    private float GetCurrentInfluenceRadius()
    {
        return currentProximity switch
        {
            PlayerProximity.Far => farInfluenceRadius,
            PlayerProximity.Medium => mediumInfluenceRadius,
            PlayerProximity.Close => closeInfluenceRadius,
            _ => 0f
        };
    }

    /// <summary>
    /// Public getter for current proximity (useful for other systems)
    /// </summary>
    public PlayerProximity CurrentProximity => currentProximity;

    /// <summary>
    /// Public getter for current distance (useful for debugging)
    /// </summary>
    public float CurrentDistanceToPlayer => currentDistanceToPlayer;

    /// <summary>
    /// Force an immediate proximity update (useful when monster teleports or major position changes)
    /// </summary>
    public void ForceProximityUpdate()
    {
        if (playerTransform != null)
        {
            UpdateProximity();
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerAwarenessSystem] {message}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // Draw distance thresholds around the monster
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, farThreshold);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, mediumThreshold);

        // Draw current influence radius around the player
        if (Application.isPlaying)
        {
            if (playerTransform == null) return;

            Gizmos.color = currentProximity switch
            {
                PlayerProximity.Far => Color.green,
                PlayerProximity.Medium => Color.yellow,
                PlayerProximity.Close => Color.red,
                _ => Color.white
            };

            float currentRadius = GetCurrentInfluenceRadius();
            if (currentRadius > 0)
            {
                Gizmos.DrawWireSphere(playerTransform.position, currentRadius);
            }
        }
    }
}