using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEditor.Callbacks;

public class Idle : EnemyAction
{
    [SerializeField] private bool freezeVelocities = false;
    [SerializeField] private float idleTime = 5f;
    private float idleTimer;

    public override void OnStart()
    {
        base.OnStart();
        idleTimer = 0f;

        controller.DeactivateMovement();

        if (freezeVelocities)
        {
            controller.rb.linearVelocity = Vector3.zero;
            controller.rb.angularVelocity = Vector3.zero;
        }
    }

    public override TaskStatus OnUpdate()
    {
        idleTimer += Time.deltaTime;
        if (idleTimer >= idleTime)
        {
            return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        base.OnEnd();
        idleTimer = 0f;
    }

}
