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

        Debug.Log("PURSUING STATE ENTERED");
    }


    public override void Exit()
    {
        base.Exit();
        pursuingPlayerBehaviourTree.enabled = false;
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();

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
            }
        }
        else //if you can see the player, reset the pursuit timer
            OnPlayerSeen();


    }

    private void OnPlayerSeen()
    {
        pusuitTimer = 0f;
    }

}
