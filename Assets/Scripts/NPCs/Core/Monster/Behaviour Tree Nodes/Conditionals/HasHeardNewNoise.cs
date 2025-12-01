using Opsive.BehaviorDesigner.Runtime.Tasks;

public class HasHeardNewNoise : EnemyConditional
{
    public override TaskStatus OnUpdate()
    {
        return controller.hearing.HasHeardRecentNoise ? TaskStatus.Success : TaskStatus.Failure;
    }
}
