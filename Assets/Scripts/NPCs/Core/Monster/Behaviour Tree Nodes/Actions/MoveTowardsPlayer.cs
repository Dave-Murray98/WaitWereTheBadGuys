using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class MoveTowardsPlayer : EnemyAction
{

    public override void OnStart()
    {
        base.OnStart();

        controller.SetTargetPosition(controller.player.position);
    }

    public override TaskStatus OnUpdate()
    {
        if (controller.movement.HasReachedDestination())
            return TaskStatus.Success;

        return TaskStatus.Running;
    }
}
