using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class EnemyConditional : Conditional
{
    protected UnderwaterMonsterController controller;

    public override void OnAwake()
    {
        base.OnAwake();
        controller = gameObject.GetComponentInParent<UnderwaterMonsterController>();
    }
}
