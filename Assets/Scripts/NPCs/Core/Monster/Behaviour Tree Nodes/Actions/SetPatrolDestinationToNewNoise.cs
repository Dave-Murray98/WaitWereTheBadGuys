using UnityEngine;
using BehaviorDesigner.Runtime.Tasks;

public class SetPatrolDestinationToNewNoise : EnemyAction
{
    public override void OnStart()
    {
        base.OnStart();

        controller.SetTargetPosition(controller.hearing.LastHeardNoisePosition);
    }
}
