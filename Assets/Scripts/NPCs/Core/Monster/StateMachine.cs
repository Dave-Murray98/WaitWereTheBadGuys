using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class StateMachine : MonoBehaviour
{
    [ShowInInspector] public State currentState;
    public UnityEvent<State> OnStateChanged;

    [Header("Debug")]
    [SerializeField] public bool enableDebugLog = false;

    protected virtual void Awake()
    {
        OnStateChanged = new UnityEvent<State>();
    }

    public virtual void Start()
    {
        currentState = GetInitialState();
        DebugLog("Initial state set to" + currentState.name);
        if (currentState != null)
            currentState.Enter();
    }

    void Update()
    {
        if (currentState != null)
            currentState.UpdateLogic();
    }

    void LateUpdate()
    {
        if (currentState != null)
            currentState.UpdatePhysics();
    }

    protected virtual State GetInitialState()
    {
        return null;
    }

    public void ChangeState(State newState)
    {
        if (currentState != null && currentState != newState)
            currentState.Exit();

        currentState = newState;
        newState.Enter();
        OnStateChanged?.Invoke(newState);
    }

    public State GetCurrentState()
    {
        return currentState;
    }

    public virtual void DebugLog(string message)
    {
        if (enableDebugLog)
            Debug.Log("[EnemyStateMachine]" + message);
    }

}