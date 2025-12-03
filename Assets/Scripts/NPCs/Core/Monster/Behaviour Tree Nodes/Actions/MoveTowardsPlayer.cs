using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class MoveTowardsPlayer : EnemyAction
{
    [SerializeField]
    private float pathfindingUpdateFrequency = 0.5f;
    private float timer = 0f;

    public override void OnStart()
    {
        base.OnStart();
        controller.ActivateMovement();
        controller.SetTargetPosition(controller.player.position);

        timer = 0f;
    }

    public override TaskStatus OnUpdate()
    {
        // if (controller.movement.HasReachedDestination())
        //     return TaskStatus.Success;

        timer += Time.deltaTime;
        if (timer >= pathfindingUpdateFrequency)
        {
            controller.SetTargetPosition(controller.player.position);
            timer = 0f;
        }

        return TaskStatus.Running;
    }
}
