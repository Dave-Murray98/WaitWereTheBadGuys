using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class EnemyAction : Action
{
    protected UnderwaterMonsterController controller;

    public override void OnAwake()
    {
        base.OnAwake();
        controller = gameObject.GetComponentInParent<UnderwaterMonsterController>();
    }

}
