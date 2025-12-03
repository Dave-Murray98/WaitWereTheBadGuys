using UnityEngine;

public class PatrollingState : EnemyState
{

    public PatrollingState(EnemyStateMachine stateMachine) : base("Patrolling", stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        patrollingBehaviourTree.enabled = true;

        sm.DebugLog("PATROLLING STATE ENTERED");
    }

    public override void Exit()
    {
        base.Exit();
        patrollingBehaviourTree.enabled = false;
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();

        if (controller.vision.CanSeePlayer)
        {
            stateMachine.ChangeState(((EnemyStateMachine)stateMachine).engageState);
        }

        if (controller.hearing.HasHeardRecentNoise)
        {
            stateMachine.ChangeState(((EnemyStateMachine)stateMachine).investigateState);
        }
    }

}
