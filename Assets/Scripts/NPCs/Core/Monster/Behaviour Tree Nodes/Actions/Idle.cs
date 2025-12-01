using UnityEngine;

using BehaviorDesigner.Runtime.Tasks;

public class Idle : EnemyAction
{
    [SerializeField] private float idleTime = 5f;
    private float idleTimer;

    public override void OnStart()
    {
        base.OnStart();
        idleTimer = 0f;

        controller.DeactivateMovement();
    }

    public override TaskStatus OnUpdate()
    {
        idleTimer += Time.deltaTime;
        if (idleTimer >= idleTime)
        {
            return TaskStatus.Success;
        }

        return TaskStatus.Success;
    }

    public override void OnEnd()
    {
        base.OnEnd();
        idleTimer = 0f;
    }

}
