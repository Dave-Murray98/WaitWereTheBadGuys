using System;
using Pathfinding.ECS;
using Sirenix.OdinInspector;
using UnityEngine;

public class UnderwaterMonsterController : MonoBehaviour
{
    public Transform player;
    public Rigidbody rb;

    [Header("Components")]
    public MonsterUnderwaterMovement movement;
    public MonsterAwarenessOfPlayerPostionSystem awarenessSystem;
    [SerializeField] private MonsterAnimationHandler animationHandler;

    [Header("Capabilities")]
    public MonsterHealth health;
    public MonsterAttack attack;

    public EnemyVision vision;
    public EnemyHearing hearing;

    [Header("State Stats")]
    public Transform monsterDeathRetreatTransform;
    public float maxEngageDistance = 10f;
    public float maxPursuitTime = 10f;
    public float investigateStateTime = 10f;
    public float investigateStateRadius = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    public Vector3 targetPosition;

    public event Action OnMonsterDespawned;

    private void Awake()
    {
        GetComponenets();
    }

    private void GetComponenets()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (movement == null) movement = GetComponent<MonsterUnderwaterMovement>();
        if (animationHandler == null) animationHandler = GetComponent<MonsterAnimationHandler>();
        if (health == null) health = GetComponent<MonsterHealth>();
        if (attack == null) attack = GetComponentInChildren<MonsterAttack>();
        if (awarenessSystem == null) awarenessSystem = GetComponent<MonsterAwarenessOfPlayerPostionSystem>();
    }

    private void Start()
    {
        Initialize();
        movement.ActivateMovement();
    }

    private void Initialize()
    {
        movement.Initialize(this);

        // Initialize the awareness system with player reference
        if (awarenessSystem != null && player != null)
        {
            awarenessSystem.Initialize(player);
        }
        else
        {
            if (awarenessSystem == null)
                DebugLog("Warning: No PlayerAwarenessSystem found! AI will use basic random patrol.");
            if (player == null)
                DebugLog("Warning: No player reference assigned!");
        }
    }

    /// <summary>
    /// Sets a patrol destination using the awareness system to influence position based on distance to player.
    /// </summary>
    public void SetPatrolDestinationBasedOnDistanceToPlayer()
    {
        if (awarenessSystem == null)
        {
            DebugLog("Warning: No PlayerAwarenessSystem found! AI will use basic random patrol.");
            return;
        }

        Vector3 newPatrolPosition = awarenessSystem.GetInfluencedPatrolPosition(transform.position);
        DebugLog($"Set influenced patrol destination: {newPatrolPosition} (Proximity: {awarenessSystem.CurrentProximity})");

        //fallback to random patrol
        if (newPatrolPosition == Vector3.zero)
        {
            newPatrolPosition = NPCPathfindingUtilities.Instance.GetRandomValidPosition(transform.position);
        }

        SetTargetPosition(newPatrolPosition);
    }

    public void SetPatrolPositionToRandomPositionNearLastHeardNoise()
    {
        Vector3 newPatrolPosition = NPCPathfindingUtilities.Instance.GetRandomValidPositionNearPoint(hearing.LastHeardNoisePosition, investigateStateRadius);

        if (targetPosition == Vector3.zero)
            targetPosition = NPCPathfindingUtilities.Instance.GetRandomValidPosition(transform.position);

        SetTargetPosition(newPatrolPosition);
    }

    /// <summary>
    /// Alternative patrol method that forces random patrol regardless of awareness system.
    /// Useful for specific behavior tree nodes or when you want to override the awareness system.
    /// </summary>
    [Button("Set Random Patrol")]
    public void SetRandomPatrolDestination()
    {
        Vector3 randomPosition = NPCPathfindingUtilities.Instance.GetRandomValidPosition(transform.position);
        SetTargetPosition(randomPosition);
        DebugLog($"Set forced random patrol destination: {randomPosition}");
    }

    [Button]
    public void Despawn()
    {
        Debug.Log("MONSTER DESPAWNING");
        OnMonsterDespawned?.Invoke();
        gameObject.SetActive(false);
    }

    [Button]
    public void Attack()
    {
        if (attack.isAttacking) return;

        StartCoroutine(attack.AttackCoroutine());
    }

    [Button]
    public void DeactivateMovement()
    {
        movement.DeactivateMovement();
    }

    [Button]
    public void ActivateMovement()
    {
        movement.ActivateMovement();
    }


    public float GetDistanceToTarget()
    {
        return movement.GetDistanceToTarget();
    }

    public void SetTargetPosition(Vector3 targetPosition)
    {
        this.targetPosition = targetPosition;
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        StopAllCoroutines();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[UnderwaterMonsterController] {message}");
        }
    }

}
