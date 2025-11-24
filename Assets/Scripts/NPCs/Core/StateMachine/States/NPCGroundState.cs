using UnityEngine;

/// <summary>
/// Ground movement state for NPCs.
/// Now manages FollowerEntity lifecycle.
/// </summary>
public class NPCGroundState : NPCState
{
    private bool hasSubscribedToEvents = false;

    public override string StateName => "Ground";
    public override NPCMovementStateMachine.MovementState StateType => NPCMovementStateMachine.MovementState.Ground;

    public override void Initialize(NPCMovementStateMachine stateMachine)
    {
        base.Initialize(stateMachine);
    }

    public override void OnEnter()
    {
        DebugLog("Entering ground state");

        SetUpCoreComponents();
        SetupStatePhysics();

        // Subscribe to events if not already subscribed
        if (!hasSubscribedToEvents && stateMachine.npcController.groundController != null)
        {
            stateMachine.npcController.groundController.OnDestinationReached += OnDestinationReached;
            hasSubscribedToEvents = true;
        }
    }

    protected override void SetUpCoreComponents()
    {
        // Enable FollowerEntity for ground movement
        if (stateMachine.npcController.followerEntity != null)
        {
            stateMachine.npcController.followerEntity.enabled = true;
        }

        stateMachine.npcController.groundController.enabled = true;
        stateMachine.npcController.waterController.enabled = false;
    }

    protected override void SetupStatePhysics()
    {
        stateMachine.npcController.rb.useGravity = true;

        stateMachine.npcController.rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        stateMachine.npcController.rb.linearDamping = stateMachine.npcController.groundController.groundDrag;
        stateMachine.npcController.rb.angularDamping = stateMachine.npcController.groundController.groundDrag;
    }

    public override void OnExit()
    {
        DebugLog("Exiting ground state");
        stateMachine.npcController.groundController.enabled = false;

        // Note: We don't disable FollowerEntity here - let the next state handle it
    }

    public override bool HasReachedDestination()
    {
        if (stateMachine.npcController.groundController != null && stateMachine.npcController.groundController.enabled)
        {
            return stateMachine.npcController.groundController.HasReachedDestination;
        }
        return false;
    }

    public override float GetCurrentSpeed()
    {
        if (stateMachine.npcController.groundController != null && stateMachine.npcController.groundController.enabled)
        {
            return stateMachine.npcController.groundController.CurrentSpeed;
        }
        return 0f;
    }

    public override void SetMaxSpeed(float speed)
    {
        if (stateMachine.npcController.groundController != null)
        {
            stateMachine.npcController.groundController.SetMaxSpeed(speed);
            DebugLog($"Max speed set to {speed}");
        }
    }

    public override string GetStateInfo()
    {
        if (stateMachine.npcController.groundController != null)
        {
            return $"Ground State Active\n{stateMachine.npcController.groundController.GetMovementInfo()}";
        }
        else
        {
            return "Ground State - No controller available";
        }
    }

    public override bool CanActivate()
    {
        return stateMachine.npcController.groundController != null;
    }

    public void Cleanup()
    {
        if (hasSubscribedToEvents && stateMachine.npcController.groundController != null)
        {
            stateMachine.npcController.groundController.OnDestinationReached -= OnDestinationReached;
            hasSubscribedToEvents = false;
        }
    }
}