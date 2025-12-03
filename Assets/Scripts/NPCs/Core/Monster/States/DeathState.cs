using UnityEngine;

public class DeathState : EnemyState
{
    public DeathState(EnemyStateMachine stateMachine) : base("Death", stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        sm.DebugLog("DEATH STATE ENTERED");

        deathBehaviourTree.enabled = true;
    }

    public override void Exit()
    {
        base.Exit();
        deathBehaviourTree.enabled = false;
    }
}
