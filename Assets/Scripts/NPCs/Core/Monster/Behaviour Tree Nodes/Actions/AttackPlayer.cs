using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class AttackPlayer : EnemyAction
{
    private float backupTimer;
    private float timeout = 5f;

    public override void OnStart()
    {
        base.OnStart();

        backupTimer = 0;
        controller.Attack();
    }

    public override TaskStatus OnUpdate()
    {
        backupTimer += Time.deltaTime;
        if (backupTimer >= timeout)
        {
            return TaskStatus.Success;
        }

        while (controller.attack.isAttacking)
        {
            return TaskStatus.Running;
        }

        return TaskStatus.Success;
    }
}
