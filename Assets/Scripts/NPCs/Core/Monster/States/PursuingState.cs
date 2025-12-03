using UnityEngine;

public class PursuingState : EnemyState
{
    public PursuingState(EnemyStateMachine stateMachine) : base("Pursuing", stateMachine) { }

    private float pusuitTimer = 0f;

    public override void Enter()
    {
        base.Enter();
        pursuingPlayerBehaviourTree.enabled = true;

        pusuitTimer = 0f;

        sm.DebugLog("PURSUING STATE ENTERED");
    }


    public override void Exit()
    {
        base.Exit();
        pursuingPlayerBehaviourTree.enabled = false;
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();

        sm.DebugLog($"Pursuit timer = {pusuitTimer}");

        if (controller.GetDistanceToTarget() <= controller.maxEngageDistance)
        {
            stateMachine.ChangeState(((EnemyStateMachine)stateMachine).engageState);
        }

        if (!controller.vision.CanSeePlayer)
        {
            pusuitTimer += Time.deltaTime;

            if (pusuitTimer >= controller.maxPursuitTime)
            {
                stateMachine.ChangeState(((EnemyStateMachine)stateMachine).patrolState);
                sm.DebugLog("Pursuit timer exceeded, changing state to patrol");
            }

            if (controller.hearing.HasHeardRecentNoise)
            {
                stateMachine.ChangeState(((EnemyStateMachine)stateMachine).investigateState);
            }
        }
        else //if you can see the player, reset the pursuit timer
            OnPlayerSeen();


    }

    private void OnPlayerSeen()
    {
        sm.DebugLog("PLAYER SEEN, RESETTING PUSUIT TIMER");
        pusuitTimer = 0f;
    }

}
