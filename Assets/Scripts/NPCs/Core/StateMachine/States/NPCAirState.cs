using UnityEngine;

/// <summary>
/// Air/falling state for NPCs.
/// Handles the mid-air transition period between ground and water states.
/// Keeps things simple - just physics and falling, no active pathfinding.
/// This prevents teleporting issues when NPCs are pushed out of water or fall off ledges (when in ground state, 
/// if not on an A* nav grid, they are teleported to the nearest nav grid).
/// </summary>
public class NPCAirState : NPCState
{
    private bool hasSubscribedToEvents = false;

    public override string StateName => "Air";
    public override NPCMovementStateMachine.MovementState StateType => NPCMovementStateMachine.MovementState.Air;

    public override void Initialize(NPCMovementStateMachine stateMachine)
    {
        base.Initialize(stateMachine);
    }

    public override void OnEnter()
    {
        DebugLog("Entering air state (falling/mid-air)");

        SetUpCoreComponents();
        SetupStatePhysics();

        // No events to subscribe to - air state is purely passive
        // We just fall until we hit ground or water
    }

    protected override void SetUpCoreComponents()
    {
        // Disable ALL pathfinding components - we're in free fall
        // This is the key to preventing the teleporting issue

        // Disable ground movement
        if (stateMachine.npcController.groundController != null)
        {
            stateMachine.npcController.groundController.enabled = false;
        }

        // Disable water movement
        if (stateMachine.npcController.waterController != null)
        {
            stateMachine.npcController.waterController.enabled = false;
        }

        DebugLog("All movement controllers disabled - in free fall");
    }

    protected override void SetupStatePhysics()
    {
        // Enable gravity so NPC falls naturally
        if (stateMachine.npcController.rb != null)
        {
            stateMachine.npcController.rb.useGravity = true;

            //constrain the x and z rotation
            stateMachine.npcController.rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            stateMachine.npcController.rb.linearDamping = stateMachine.npcController.groundController.groundDrag;
            stateMachine.npcController.rb.angularDamping = stateMachine.npcController.groundController.groundDrag;

            // We deliberately DON'T reset velocity here
            // This preserves any horizontal momentum from being pushed/thrown
            // The NPC will maintain their horizontal movement while falling
        }

        DebugLog("Gravity enabled - NPC will fall naturally");
    }

    public override void OnExit()
    {
        DebugLog("Exiting air state");

        // Clean exit - the next state will set up its own components
    }

    public override bool HasReachedDestination()
    {
        // Can't reach destination while falling
        return false;
    }

    public override float GetCurrentSpeed()
    {
        // Return the magnitude of velocity (how fast we're falling/moving)
        if (stateMachine.npcController.rb != null)
        {
            return stateMachine.npcController.rb.linearVelocity.magnitude;
        }
        return 0f;
    }

    public override void SetMaxSpeed(float speed)
    {
        // No max speed control in air state - physics handles everything
        DebugLog($"Max speed setting ignored in air state (physics controlled)");
    }

    public override string GetStateInfo()
    {
        if (stateMachine.npcController.rb != null)
        {
            Vector3 velocity = stateMachine.npcController.rb.linearVelocity;
            return $"Air State Active (Falling)\n" +
                   $"Velocity: {velocity}\n" +
                   $"Speed: {velocity.magnitude:F2} m/s\n" +
                   $"Vertical Speed: {velocity.y:F2} m/s\n" +
                   $"Horizontal Speed: {new Vector3(velocity.x, 0, velocity.z).magnitude:F2} m/s";
        }
        return "Air State - No rigidbody available";
    }

    public override bool CanActivate()
    {
        // Air state can always be activated
        // It's a fallback state when NPC is neither grounded nor in water
        return true;
    }

    /// <summary>
    /// Cleanup method called when state machine is destroyed
    /// </summary>
    public void Cleanup()
    {
        // Nothing to clean up - no events subscribed
    }
}