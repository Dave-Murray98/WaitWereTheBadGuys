using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class SetDestinationToDeathRetreat : EnemyAction
{

    public override void OnStart()
    {
        base.OnStart();
        controller.SetTargetPosition(controller.monsterDeathRetreatTransform.position);
        controller.ActivateMovement();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}
