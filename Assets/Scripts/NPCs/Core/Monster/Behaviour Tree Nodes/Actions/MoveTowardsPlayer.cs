using Opsive.BehaviorDesigner.Runtime.Tasks;

public class MoveTowardsPlayer : EnemyAction
{

    public override void OnStart()
    {
        base.OnStart();
        controller.ActivateMovement();
        controller.SetTargetPosition(controller.player.position);
    }

    public override TaskStatus OnUpdate()
    {
        if (controller.movement.HasReachedDestination())
            return TaskStatus.Success;

        return TaskStatus.Running;
    }
}
