using RootMotion.FinalIK;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Ground state - player is on land, equipment usability based on ItemData settings
/// </summary>
public class GroundState : PlayerState
{
    public GroundState(PlayerStateManager manager) : base(manager) { }

    protected override void InitializeState()
    {
        movementRestrictions = MovementRestrictions.CreateGroundRestrictions();
        DebugLog("Ground state initialized - equipment based on ItemData canUseOnGround");
    }

    public override void OnEnter()
    {
        base.OnEnter();

        UpdatePlayerPhysicsForState();
        UpdatePlayerComponentsForState();

    }

    protected override void UpdatePlayerPhysicsForState()
    {
        // Re-enable player gravity (as it's set to false when entering a vehicle)
        stateManager.playerController.SetRBUseGravity(true);

        // Disable player's rb kinematic so it will move based on player input (as it's set to true when entering a vehicle, so the vehicle will move the player)
        stateManager.playerController.SetRBIsKinematic(false);

        stateManager.playerController.rb.linearDamping = stateManager.playerController.groundDrag;
        stateManager.playerController.rb.angularDamping = stateManager.playerController.groundDrag;

        // disable water object
        stateManager.playerController.swimmingMovementController.waterObject.enabled = false;

    }

    protected override void UpdatePlayerComponentsForState()
    {
        // Disable player swimming rotation and depth manager
        stateManager.playerController.swimmingBodyRotation.enabled = false;
        stateManager.playerController.swimmingDepthManager.enabled = false;

        UpdatePlayerIKComponentsForState();
    }


    protected override void UpdatePlayerIKComponentsForState()
    {
        // Re-enable grounder biped IK as it's disabled in water and vehicle states
        stateManager.grounderFBBIK.enabled = true;

        // Set aim IK to use all spine bones to look around, and disable neck bone
        stateManager.aimIK.solver.bones[0].weight = 1f; //lower spine
        stateManager.aimIK.solver.bones[1].weight = 1f; //mid spine
        stateManager.aimIK.solver.bones[2].weight = 1f; //upper spine
        stateManager.aimIK.solver.bones[3].weight = 0f; //neck
    }


    public override bool CanUseItem(ItemData itemData)
    {
        if (itemData == null) return false;

        // Check ItemData setting for ground usage
        return itemData.CanUseInState(PlayerStateType.Ground);
    }

    public override bool CanEquipItem(ItemData itemData)
    {
        // Same logic as CanUseItem - if you can't use it, you can't equip it
        return CanUseItem(itemData);
    }

    public override string GetDisplayName()
    {
        return "Ground";
    }

    public override string GetDebugInfo()
    {
        var baseInfo = base.GetDebugInfo();
        return baseInfo + "Ground State: Equipment based on ItemData.canUseOnGround\n";
    }

}