using UnityEngine;
using BehaviorDesigner.Runtime.Tasks;

public class MoveTowardsDestination : EnemyAction
{
    public override void OnStart()
    {
        base.OnStart();

        controller.ActivateMovement();
    }

    public override TaskStatus OnUpdate()
    {
        if (controller.movement.HasReachedDestination())
            return TaskStatus.Success;

        return TaskStatus.Running;
    }
}
