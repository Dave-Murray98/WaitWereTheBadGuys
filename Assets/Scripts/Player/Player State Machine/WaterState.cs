using System;
using UnityEngine;

/// <summary>
/// Water state - player is swimming, equipment usability based on ItemData settings
/// </summary>
public class WaterState : PlayerState
{
    public WaterState(PlayerStateManager manager) : base(manager) { }

    protected override void InitializeState()
    {
        movementRestrictions = MovementRestrictions.CreateWaterRestrictions();
        DebugLog("Water state initialized - equipment based on ItemData canUseInWater");
    }

    public override void OnEnter()
    {
        base.OnEnter();

        UpdatePlayerPhysicsForState();
        UpdatePlayerComponentsForState();
    }

    public override void OnExit()
    {
        base.OnExit();
        stateManager.playerController.swimmingMovementController.waterObject.enabled = false;
    }

    protected override void UpdatePlayerPhysicsForState()
    {
        stateManager.playerController.rb.linearDamping = stateManager.playerController.waterDrag;
        stateManager.playerController.rb.angularDamping = stateManager.playerController.waterDrag;

        // Enable water object so it can apply buoyancy when at the water's surface
        stateManager.playerController.swimmingMovementController.waterObject.enabled = true;

        base.UpdatePlayerPhysicsForState();
    }

    protected override void UpdatePlayerComponentsForState()
    {
        // enable gravity when swimming
        stateManager.playerController.SetRBUseGravity(true);

        // Disable player's rb kinematic so it will move based on player input (as it's set to true when entering a vehicle, so the vehicle will move the player)
        stateManager.playerController.SetRBIsKinematic(false);

        // Enable player swimming rotation and depth manager
        stateManager.playerController.swimmingBodyRotation.enabled = true;
        stateManager.playerController.swimmingDepthManager.enabled = true;

        UpdatePlayerIKComponentsForState();
    }

    protected override void UpdatePlayerIKComponentsForState()
    {
        // Disable grounder biped IK to prevent it from interfering with player swimming animations
        stateManager.grounderFBBIK.enabled = false;

        // Set aim IK to use all spine bones to look around, and disable neck bone
        stateManager.aimIK.solver.bones[0].weight = 1f; //lower spine
        stateManager.aimIK.solver.bones[1].weight = 1f; //mid spine
        stateManager.aimIK.solver.bones[2].weight = 1f; //upper spine
        stateManager.aimIK.solver.bones[3].weight = 0f; //neck
    }

    public override bool CanUseItem(ItemData itemData)
    {
        if (itemData == null) return false;

        // Check ItemData setting for water usage
        return itemData.CanUseInState(PlayerStateType.Water);
    }

    public override bool CanEquipItem(ItemData itemData)
    {
        // Same logic as CanUseItem - if you can't use it, you can't equip it
        return CanUseItem(itemData);
    }

    public override string GetDisplayName()
    {
        return "Water";
    }

    public override string GetDebugInfo()
    {
        var baseInfo = base.GetDebugInfo();
        return baseInfo + "Water State: Equipment based on ItemData.canUseInWater\n";
    }
}
