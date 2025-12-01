using Opsive.BehaviorDesigner.Runtime.Tasks;

public class SetPatrolDestination : EnemyAction
{

    public override void OnStart()
    {
        base.OnStart();
        controller.SetPatrolDestination();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }


}
