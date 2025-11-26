using UnityEngine;

public class EngagingState : EnemyState
{
    public EngagingState(EnemyStateMachine stateMachine) : base("Engaging", stateMachine) { }

    private float distanceCheckTimer = 0f;
    private float distanceCheckFrequency = 0.5f;

    public override void Enter()
    {
        base.Enter();
        engagingPlayerBehaviourTree.enabled = true;
        distanceCheckTimer = 0f;

        Debug.Log("ENGAGING STATE ENTERED");
    }

    public override void Exit()
    {
        base.Exit();
        engagingPlayerBehaviourTree.enabled = false;
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();

        distanceCheckTimer += Time.deltaTime;

        if (distanceCheckTimer >= distanceCheckFrequency)
        {
            distanceCheckTimer = 0f;
            if (controller.GetDistanceToTarget() > controller.maxEngageDistance)
            {
                Debug.Log("Too far from player, changing state to pursue");
                stateMachine.ChangeState(((EnemyStateMachine)stateMachine).pursueState);
            }
        }

    }
}
