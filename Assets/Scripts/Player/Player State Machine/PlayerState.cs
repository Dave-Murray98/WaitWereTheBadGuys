using System;
using UnityEngine;


/// <summary>
/// Base class for all player states. Defines the interface and common functionality
/// that all player states must implement. Each state handles its own equipment
/// restrictions and movement capabilities.
/// </summary>
public abstract class PlayerState
{
    // Reference to the state manager
    protected PlayerStateManager stateManager;

    // State configuration
    protected MovementRestrictions movementRestrictions;

    // State lifecycle tracking
    protected bool isActive = false;
    protected float stateEnterTime = 0f;

    public PlayerState(PlayerStateManager manager)
    {
        stateManager = manager;
        InitializeState();
    }

    #region Abstract Methods - Must be implemented by concrete states

    /// <summary>
    /// Initialize state-specific configuration
    /// </summary>
    protected abstract void InitializeState();

    /// <summary>
    /// Check if a specific item can be used in this state
    /// </summary>
    public abstract bool CanUseItem(ItemData itemData);

    /// <summary>
    /// Check if a specific item can be equipped in this state
    /// </summary>
    public abstract bool CanEquipItem(ItemData itemData);

    /// <summary>
    /// Get the display name for this state
    /// </summary>
    public abstract string GetDisplayName();

    #endregion

    #region Virtual Methods - Can be overridden by concrete states

    /// <summary>
    /// Called when entering this state
    /// </summary>
    public virtual void OnEnter()
    {
        isActive = true;
        stateEnterTime = Time.time;
        DebugLog($"Entered {GetDisplayName()} state");
    }

    /// <summary>
    /// Called when exiting this state
    /// </summary>
    public virtual void OnExit()
    {
        isActive = false;
        float timeInState = Time.time - stateEnterTime;
        DebugLog($"Exited {GetDisplayName()} state (was active for {timeInState:F2}s)");
    }

    /// <summary>
    /// Called every frame while this state is active
    /// </summary>
    public virtual void OnUpdate()
    {
        // Override in concrete states if per-frame logic is needed
    }

    /// <summary>
    /// Called when the state is being destroyed
    /// </summary>
    public virtual void OnDestroy()
    {
        // Override in concrete states for cleanup
    }

    /// <summary>
    /// Get movement restrictions for this state
    /// </summary>
    public virtual MovementRestrictions GetMovementRestrictions()
    {
        return movementRestrictions ?? new MovementRestrictions();
    }

    /// <summary>
    /// Update player physics for this state
    /// </summary>
    protected virtual void UpdatePlayerPhysicsForState() { }

    /// <summary>
    /// Update player components for this state
    /// </summary>
    protected virtual void UpdatePlayerComponentsForState() { }

    /// <summary>
    /// Update player IK components for this state (ie disable/enable grounder biped and set weights of aim IK bones)
    /// </summary>
    protected virtual void UpdatePlayerIKComponentsForState() { }

    #endregion

    #region Common Functionality

    /// <summary>
    /// Check if this state allows any item usage
    /// </summary>
    public bool CanUseAnyItems()
    {
        return movementRestrictions?.canUseItems ?? false;
    }

    /// <summary>
    /// Check if this state allows any item equipping
    /// </summary>
    public bool CanEquipAnyItems()
    {
        return movementRestrictions?.canEquipItems ?? false;
    }

    /// <summary>
    /// Get debug information about this state
    /// </summary>
    public virtual string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"State: {GetDisplayName()}");
        info.AppendLine($"Active: {isActive}");
        info.AppendLine($"Time in State: {(isActive ? Time.time - stateEnterTime : 0):F2}s");
        info.AppendLine($"Can Use Items: {CanUseAnyItems()}");
        info.AppendLine($"Can Equip Items: {CanEquipAnyItems()}");
        return info.ToString();
    }

    /// <summary>
    /// Log debug messages if enabled
    /// </summary>
    protected virtual void DebugLog(string message)
    {
        if (PlayerStateManager.Instance.enableDebugLogs)
        {
            Debug.Log($"[{GetDisplayName()}State] {message}");
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Whether this state is currently active
    /// </summary>
    public bool IsActive => isActive;

    /// <summary>
    /// How long this state has been active
    /// </summary>
    public float TimeInState => isActive ? Time.time - stateEnterTime : 0f;

    /// <summary>
    /// Reference to the state manager
    /// </summary>
    public PlayerStateManager StateManager => stateManager;

    #endregion
}