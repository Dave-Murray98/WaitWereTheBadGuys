using Sirenix.OdinInspector;
using UnityEngine;

public class UnderwaterMonsterController : MonoBehaviour
{

    public Transform player;
    public Rigidbody rb;

    [Header("Components")]
    [SerializeField] private MonsterUnderwaterMovement movement;
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

    [HideInInspector] public Vector3 targetPosition;

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

    [Button]
    public void Attack()
    {
        attack.PerformAttack();
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

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[UnderwaterMonsterController] {message}");
        }
    }

    public float GetDistanceToTarget()
    {
        return movement.GetDistanceToTarget();
    }
}
