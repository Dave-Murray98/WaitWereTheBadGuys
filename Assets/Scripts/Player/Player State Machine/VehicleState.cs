using System.Data;
using UnityEngine;


public class VehicleState : PlayerState
{

    public VehicleState(PlayerStateManager manager) : base(manager) { }

    protected override void InitializeState()
    {
        movementRestrictions = MovementRestrictions.CreateVehicleRestrictions();
        DebugLog("Vehicle state initialized - equipment based on ItemData canUseInVehicle");
    }

    public override bool CanUseItem(ItemData itemData)
    {
        if (itemData == null) return false;

        // Check ItemData setting for vehicle usage
        return itemData.CanUseInState(PlayerStateType.Vehicle);
    }

    public override bool CanEquipItem(ItemData itemData)
    {
        // Same logic as CanUseItem - if you can't use it, you can't equip it
        return CanUseItem(itemData);
    }

    public override string GetDisplayName()
    {
        return "Vehicle";
    }

    public override void OnEnter()
    {
        base.OnEnter();


        UpdatePlayerPhysicsForState();
        UpdatePlayerComponentsForState();
    }

    protected override void UpdatePlayerPhysicsForState()
    {
        stateManager.playerController.rb.linearDamping = stateManager.playerController.groundDrag;
        stateManager.playerController.rb.angularDamping = stateManager.playerController.groundDrag;

        // Disable player gravity while in vehicle
        stateManager.playerController.SetRBUseGravity(false);

        // Set the player's rb to kinematic so it will stick to the player when the vehicle moves
        stateManager.playerController.SetRBIsKinematic(true);

        // disable water object
        stateManager.playerController.swimmingMovementController.waterObject.enabled = false;
    }

    protected override void UpdatePlayerComponentsForState()
    {
        // Disable player swimming rotation and depth manager
        stateManager.playerController.swimmingBodyRotation.enabled = false;
        stateManager.playerController.swimmingDepthManager.enabled = false;

        //set the player's rotation to match that of the seat, so he faces in the direction of the seat
        stateManager.playerController.transform.localRotation = Quaternion.Euler(0, 0, 0);

        UpdatePlayerIKComponentsForState();
    }

    protected override void UpdatePlayerIKComponentsForState()
    {
        // Disable grounder biped IK to prevent it from interfering with player vehicle animations
        stateManager.grounderFBBIK.enabled = false;


        // Set aim IK to use neck and upper spine bone to look around, and disable lower spine bones (we don't want the spine to bend while the player is seated in the vehicle)
        stateManager.aimIK.solver.bones[0].weight = 0f; //lower spine
        stateManager.aimIK.solver.bones[1].weight = 0f; //mid spine
        stateManager.aimIK.solver.bones[2].weight = 1f; //upper spine
        stateManager.aimIK.solver.bones[3].weight = 1f; //neck
    }

    public override void OnExit()
    {
        base.OnExit();

    }

    public override string GetDebugInfo()
    {
        var baseInfo = base.GetDebugInfo();
        return baseInfo + "Vehicle State: Equipment based on ItemData.canUseInVehicle\n" +
               "TODO: Expand when vehicle system is implemented\n";
    }

}