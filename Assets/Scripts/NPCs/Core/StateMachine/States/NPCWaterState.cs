using UnityEngine;

/// <summary>
/// Water movement state for NPCs.
/// Now manages FollowerEntity lifecycle (disables it for water movement).
/// </summary>
public class NPCWaterState : NPCState
{
    private bool hasSubscribedToEvents = false;
    private bool canSwim = true;

    public override string StateName => "Water";
    public override NPCMovementStateMachine.MovementState StateType => NPCMovementStateMachine.MovementState.Water;

    public override void Initialize(NPCMovementStateMachine stateMachine)
    {
        base.Initialize(stateMachine);

        canSwim = stateMachine.CanSwim;

        if (stateMachine.npcController.waterController == null && canSwim)
        {
            Debug.LogError($"{npcTransform.name}: NPCWaterMovementController component is required for water state!");
        }
    }

    public override void OnEnter()
    {
        DebugLog("Entering water state");

        if (!canSwim)
        {
            Debug.LogWarning($"{npcTransform.name}: NPC cannot swim but entered water state!");
            return;
        }

        // Safety check - verify NPC is actually in water before activating water movement
        if (!stateMachine.npcController.waterDetector.IsInWater)
        {
            Debug.LogWarning($"{npcTransform.name}: Attempted to enter water state but not in water! This shouldn't happen after climbing.");
            // Don't activate water components - let the state machine handle the correction
            return;
        }

        SetUpCoreComponents();
        SetupStatePhysics();

        if (!hasSubscribedToEvents && stateMachine.npcController.waterController != null)
        {
            stateMachine.npcController.waterController.OnDestinationReached += OnDestinationReached;
            hasSubscribedToEvents = true;
        }
    }

    protected override void SetUpCoreComponents()
    {
        // Disable FollowerEntity for water movement
        if (stateMachine.npcController.followerEntity != null)
        {
            stateMachine.npcController.followerEntity.enabled = false;
        }

        stateMachine.npcController.waterController.enabled = true;
        stateMachine.npcController.groundController.enabled = false;
    }

    protected override void SetupStatePhysics()
    {
        stateMachine.npcController.rb.useGravity = false;
        stateMachine.npcController.rb.constraints = RigidbodyConstraints.None;
        stateMachine.npcController.rb.linearDamping = stateMachine.npcController.waterController.waterDrag;
        stateMachine.npcController.rb.angularDamping = stateMachine.npcController.waterController.waterDrag;
    }

    public override void OnExit()
    {
        DebugLog("Exiting water state");

        if (stateMachine.npcController.waterController != null)
        {
            stateMachine.npcController.waterController.enabled = false;
            DebugLog("Water movement controller deactivated");
        }

        // Note: We don't enable FollowerEntity here - let the next state handle it
    }

    public override bool HasReachedDestination()
    {
        if (stateMachine.npcController.waterController != null && stateMachine.npcController.waterController.enabled)
        {
            return stateMachine.npcController.waterController.HasReachedDestination();
        }
        return false;
    }

    public override float GetCurrentSpeed()
    {
        if (stateMachine.npcController.waterController != null && stateMachine.npcController.waterController.enabled && stateMachine.npcController.rb != null)
        {
            return stateMachine.npcController.rb.linearVelocity.magnitude;
        }
        return 0f;
    }

    public override void SetMaxSpeed(float speed)
    {
        if (stateMachine.npcController.waterController != null)
        {
            stateMachine.npcController.waterController.SetMaxSpeed(speed);
            DebugLog($"Max speed set to {speed}");
        }
    }

    public override string GetStateInfo()
    {
        if (stateMachine.npcController.waterController != null)
        {
            return $"Water State Active\n{stateMachine.npcController.waterController.GetMovementInfo()}";
        }
        else if (!canSwim)
        {
            return "Water State - NPC cannot swim";
        }
        else
        {
            return "Water State - No controller available";
        }
    }

    public override bool CanActivate()
    {
        return canSwim && stateMachine.npcController.waterController != null;
    }

    public void SetSwimmingCapability(bool canSwimValue)
    {
        canSwim = canSwimValue;
        DebugLog($"Swimming capability set to {canSwim}");
    }

    public string GetWaterMovementInfo()
    {
        if (stateMachine.npcController.waterController != null)
        {
            return $"Swimming Capability: {canSwim}\n" +
                   $"Current Mode: {stateMachine.npcController.waterController.GetCurrentMovementMode()}\n" +
                   $"In Complex Zone: {stateMachine.npcController.waterController.IsInComplexZone()}\n" +
                   $"In Transition: {stateMachine.npcController.waterController.IsInTransition()}";
        }
        return "No water controller available";
    }

    public void Cleanup()
    {
        if (hasSubscribedToEvents && stateMachine.npcController.waterController != null)
        {
            stateMachine.npcController.waterController.OnDestinationReached -= OnDestinationReached;
            hasSubscribedToEvents = false;
        }
    }
}