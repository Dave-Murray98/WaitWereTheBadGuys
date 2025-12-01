using Opsive.BehaviorDesigner.Runtime.Tasks;

public class IsPlayerInAttackRange : EnemyConditional
{
    public override TaskStatus OnUpdate()
    {
        return controller.attack.playerInAttackRange ? TaskStatus.Success : TaskStatus.Failure;

    }
}
