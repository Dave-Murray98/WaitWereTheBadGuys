using UnityEngine;

/// <summary>
/// Base class for NPC movement states.
/// Defines the standard state lifecycle methods that each state must implement.
/// Clean separation of concerns - each state handles its own logic.
/// </summary>
public abstract class NPCState
{
    protected NPCMovementStateMachine stateMachine;
    protected Transform npcTransform;
    protected bool enableDebugLogs;

    // State identification
    public abstract string StateName { get; }
    public abstract NPCMovementStateMachine.MovementState StateType { get; }

    /// <summary>
    /// Initialize the state with reference to the state machine
    /// </summary>
    public virtual void Initialize(NPCMovementStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
        this.npcTransform = stateMachine.transform;
        this.enableDebugLogs = stateMachine.EnableDebugLogs;
    }

    /// <summary>
    /// Called when entering this state
    /// </summary>
    public abstract void OnEnter();

    /// <summary>
    /// Called every frame while in this state
    /// </summary>
    public virtual void OnUpdate()
    {
        // Base implementation does nothing - override in subclasses if needed
    }

    /// <summary>
    /// Called when exiting this state
    /// </summary>
    public abstract void OnExit();

    /// <summary>
    /// Check if the NPC has reached its destination in this state
    /// </summary>
    public abstract bool HasReachedDestination();

    /// <summary>
    /// Get current movement speed in this state
    /// </summary>
    public abstract float GetCurrentSpeed();

    /// <summary>
    /// Set maximum movement speed for this state
    /// </summary>
    public abstract void SetMaxSpeed(float speed);

    protected abstract void SetupStatePhysics();
    protected abstract void SetUpCoreComponents();

    /// <summary>
    /// Get state-specific information for debugging
    /// </summary>
    public abstract string GetStateInfo();

    /// <summary>
    /// Check if this state can be activated (useful for capability checks)
    /// </summary>
    public virtual bool CanActivate()
    {
        return true; // Default: state can always be activated
    }

    /// <summary>
    /// Debug logging with state name prefix
    /// </summary>
    protected void DebugLog(string message)
    {
        if (stateMachine.EnableDebugLogs)
        {
            Debug.Log($"[{StateName}-{npcTransform.name}] {message}");
        }
    }

    /// <summary>
    /// Handle destination reached event from movement controllers
    /// </summary>
    protected virtual void OnDestinationReached()
    {
        DebugLog("Destination reached");
        stateMachine.NotifyDestinationReached();
    }
}