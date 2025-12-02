using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class DidPerformAttack : EnemyConditional
{
    public override TaskStatus OnUpdate()
    {
        // return controller.attack.didAttack ? TaskStatus.Success : TaskStatus.Failure;
        return TaskStatus.Success;
    }
}
