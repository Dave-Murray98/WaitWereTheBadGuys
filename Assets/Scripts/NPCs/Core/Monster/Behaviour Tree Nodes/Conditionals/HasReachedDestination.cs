using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class HasReachedDestination : EnemyConditional
{
    public override TaskStatus OnUpdate()
    {
        return controller.movement.HasReachedDestination() ? TaskStatus.Success : TaskStatus.Failure;
    }
}
