using Sirenix.OdinInspector;
using UnityEngine;

public class UnderwaterMonsterController : MonoBehaviour
{

    public Transform target;
    public Rigidbody rb;

    [SerializeField] private MonsterUnderwaterMovement underwaterMovement;
    [SerializeField] private MonsterAnimationHandler animationHandler;

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
    public void Bite()
    {
        // Placeholder for bite logic
        Debug.Log($"{gameObject.name} performed a bite!");
        animationHandler.TriggerAnimation("Bite");
    }


}
