using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class SetPatrolDestinationToPosNearNoise : EnemyAction
{
    public override void OnStart()
    {
        base.OnStart();

        controller.SetPatrolPositionToRandomPositionNearLastHeardNoise();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}
