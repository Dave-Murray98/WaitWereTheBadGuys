using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;

public class EnemyAction : Action
{
    protected UnderwaterMonsterController controller;

    public override void OnAwake()
    {
        base.OnAwake();
        controller = gameObject.GetComponentInParent<UnderwaterMonsterController>();
    }

}

