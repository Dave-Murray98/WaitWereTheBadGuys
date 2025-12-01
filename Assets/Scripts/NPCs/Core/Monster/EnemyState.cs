using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
public class EnemyState : State
{
    protected EnemyStateMachine sm;
    protected UnderwaterMonsterController controller;

    protected BehaviorTree patrollingBehaviourTree;

    protected BehaviorTree engagingPlayerBehaviourTree;
    protected BehaviorTree pursuingPlayerBehaviourTree;
    protected BehaviorTree investigatingNoiseBehaviourTree;

    public EnemyState(string name, EnemyStateMachine stateMachine) : base(name, stateMachine)
    {
        sm = (EnemyStateMachine)this.stateMachine;

        patrollingBehaviourTree = sm.patrollingBehaviourTree;
        engagingPlayerBehaviourTree = sm.engagingPlayerBehaviourTree;
        investigatingNoiseBehaviourTree = sm.investigatingNoiseBehaviourTree;
        pursuingPlayerBehaviourTree = sm.pursuingPlayerBehaviourTree;

        controller = sm.controller;
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();

        if (controller.currentHealth <= 0)
        {
            if (sm.currentState != ((EnemyStateMachine)stateMachine).deathState)
                stateMachine.ChangeState(((EnemyStateMachine)stateMachine).deathState);
        }

    }

}