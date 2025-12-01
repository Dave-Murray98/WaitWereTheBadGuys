using Opsive.BehaviorDesigner.Runtime.Tasks;

public class SetPatrolDestination : EnemyAction
{

    public override TaskStatus OnUpdate()
    {
        controller.SetPatrolDestination();
        return TaskStatus.Success;
    }


}
