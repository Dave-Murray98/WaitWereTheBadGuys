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
    [SerializeField] private MonsterAnimationHandler animationHandler;

    [Header("Attack")]
    public MonsterAttack attack;

    [Header("Vision and Hearing")]
    public EnemyVision vision;
    public EnemyHearing hearing;

    [Header("State Stats")]
    public float maxEngageDistance = 10f;
    public float maxPursuitTime = 10f;
    public float investigateStateTime = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    public float currentHealth = 100f;
    public Vector3 targetPosition;

    private void Awake()
    {
        GetComponenets();
    }

    private void GetComponenets()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (movement == null) movement = GetComponent<MonsterUnderwaterMovement>();
        if (animationHandler == null) animationHandler = GetComponent<MonsterAnimationHandler>();
        if (attack == null) attack = GetComponentInChildren<MonsterAttack>();
    }

    private void Start()
    {
        Initialize();

        movement.ActivateMovement();
    }

    private void Initialize()
    {
        movement.Initialize(this);
    }

    public void SetPatrolDestination()
    {
        SetTargetPosition(NPCPathfindingUtilities.Instance.GetRandomValidPositionToMoveTo(transform.position));
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
