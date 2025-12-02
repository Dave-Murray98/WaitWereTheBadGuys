using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class MoveTowardsDeathRetreat : EnemyAction
{
    [SerializeField] private float pathfindingUpdateFrequency = 0.5f;
    private float timer = 0f;

    public override void OnStart()
    {
        base.OnStart();
        controller.ActivateMovement();
        controller.SetTargetPosition(controller.monsterDeathRetreatTransform.position);

    }

    public override TaskStatus OnUpdate()
    {

        timer += Time.deltaTime;
        if (timer >= pathfindingUpdateFrequency)
        {
            controller.SetTargetPosition(controller.monsterDeathRetreatTransform.position);
            timer = 0f;
        }

        if (controller.movement.HasReachedDestination())
            return TaskStatus.Success;

        return TaskStatus.Running;
    }
}