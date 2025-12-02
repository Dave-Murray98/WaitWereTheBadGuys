using NUnit.Framework;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class Despawn : EnemyAction
{
    public override void OnStart()
    {
        base.OnStart();
        controller.Despawn();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}