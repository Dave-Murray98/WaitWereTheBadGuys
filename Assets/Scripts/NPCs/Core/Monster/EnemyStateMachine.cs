using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;

public class EnemyStateMachine : StateMachine
{

    #region Behaviour Trees
    [Header("Behaviour Trees")]
    public BehaviorTree patrollingBehaviourTree;
    public BehaviorTree engagingPlayerBehaviourTree;
    public BehaviorTree pursuingPlayerBehaviourTree;
    public BehaviorTree investigatingNoiseBehaviourTree;
    #endregion

    #region States
    [HideInInspector]
    public PatrollingState patrolState;

    [HideInInspector]
    public EngagingState engageState;

    [HideInInspector]
    public PursuingState pursueState;

    [HideInInspector]
    public InvestigatingNoiseState investigateState;

    [HideInInspector] public DeathState deathState;
    #endregion

    [Header("References")]
    public UnderwaterMonsterController controller;


    protected override void Awake()
    {
        base.Awake();
        AssignComponents();

        InitializeStates();
    }

    private void AssignComponents()
    {
        if (controller == null)
        {
            controller = GetComponent<UnderwaterMonsterController>();
        }
    }

    protected override State GetInitialState()
    {
        return patrolState;
    }

    protected virtual void InitializeStates()
    {

        patrolState = new PatrollingState(this);
        engageState = new EngagingState(this);
        pursueState = new PursuingState(this);
        investigateState = new InvestigatingNoiseState(this);
        deathState = new DeathState(this);

    }
}