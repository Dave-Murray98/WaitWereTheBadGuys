using Sirenix.OdinInspector;
using UnityEngine;

public class UnderwaterMonsterController : MonoBehaviour
{

    public Transform target;
    public Rigidbody rb;

    [Header("Components")]
    [SerializeField] private MonsterUnderwaterMovement underwaterMovement;
    [SerializeField] private MonsterAnimationHandler animationHandler;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private void Awake()
    {
        GetComponenets();
    }

    private void GetComponenets()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (underwaterMovement == null) underwaterMovement = GetComponent<MonsterUnderwaterMovement>();
        if (animationHandler == null) animationHandler = GetComponent<MonsterAnimationHandler>();
    }

    private void Start()
    {
        Initialize();

        underwaterMovement.ActivateMovement();
    }

    private void Initialize()
    {
        underwaterMovement.Initialize(this);
    }

    [Button]
    public void Attack()
    {
        // Placeholder for bite logic
        DebugLog("Attacking");
        animationHandler.PlayAttackAnimation();
    }

    [Button]
    public void DeactivateMovement()
    {
        underwaterMovement.DeactivateMovement();
    }

    [Button]
    public void ActivateMovement()
    {
        underwaterMovement.ActivateMovement();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[UnderwaterMonsterController] {message}");
        }
    }
}
