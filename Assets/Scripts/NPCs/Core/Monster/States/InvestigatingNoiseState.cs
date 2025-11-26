using UnityEngine;

public class InvestigatingNoiseState : EnemyState
{
    public InvestigatingNoiseState(EnemyStateMachine stateMachine) : base("Investigating Noise", stateMachine) { }

    private float investigationTimer = 0f;

    public override void Enter()
    {
        base.Enter();
        investigatingNoiseBehaviourTree.enabled = true;

        investigationTimer = 0f;

        Debug.Log("INVESTIGATING NOISE STATE ENTERED");
    }

    public override void Exit()
    {
        base.Exit();
        investigatingNoiseBehaviourTree.enabled = false;
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();

        investigationTimer += Time.deltaTime;

        if (controller.vision.CanSeePlayer)
        {
            stateMachine.ChangeState(((EnemyStateMachine)stateMachine).engageState);
        }

        if (investigationTimer >= controller.investigateStateTime)
        {
            stateMachine.ChangeState(((EnemyStateMachine)stateMachine).patrolState);
        }

        if (controller.hearing.HasHeardRecentNoise)
        {
            OnNewNoiseHeard();
        }

    }

    private void OnNewNoiseHeard()
    {
        investigationTimer = 0.0f;
    }
}
