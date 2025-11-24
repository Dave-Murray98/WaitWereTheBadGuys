using UnityEngine;

// <summary>
/// Data structure defining movement restrictions for each player state
/// </summary>
[System.Serializable]
public class MovementRestrictions
{
    [Header("Basic Movement")]
    public bool canWalk = true;
    public bool canRun = true;
    public bool canJump = true;
    public bool canCrouch = true;

    [Header("Water Movement")]
    public bool canSwim = false;
    public bool canDive = false;
    public bool canSurface = false;

    [Header("Vehicle Movement")]
    public bool canAccelerate = false;
    public bool canBrake = false;
    public bool canSteer = false;

    [Header("Special Actions")]
    public bool canInteract = true;
    public bool canUseItems = true;
    public bool canEquipItems = true;

    /// <summary>
    /// Create default ground movement restrictions
    /// </summary>
    public static MovementRestrictions CreateGroundRestrictions()
    {
        return new MovementRestrictions
        {
            canWalk = true,
            canRun = true,
            canJump = true,
            canCrouch = true,
            canSwim = false,
            canDive = false,
            canSurface = false,
            canAccelerate = false,
            canBrake = false,
            canSteer = false,
            canInteract = true,
            canUseItems = true,
            canEquipItems = true
        };
    }

    /// <summary>
    /// Create default water movement restrictions  
    /// </summary>
    public static MovementRestrictions CreateWaterRestrictions()
    {
        return new MovementRestrictions
        {
            canWalk = false,
            canRun = false,
            canJump = false,
            canCrouch = false,
            canSwim = true,
            canDive = true,
            canSurface = true,
            canAccelerate = false,
            canBrake = false,
            canSteer = false,
            canInteract = true,
            canUseItems = true, // Limited by item-specific restrictions
            canEquipItems = true // Limited by item-specific restrictions
        };
    }

    /// <summary>
    /// Create default vehicle movement restrictions
    /// </summary>
    public static MovementRestrictions CreateVehicleRestrictions()
    {
        return new MovementRestrictions
        {
            canWalk = false,
            canRun = false,
            canJump = false,
            canCrouch = false,
            canSwim = false,
            canDive = false,
            canSurface = false,
            canAccelerate = true,
            canBrake = true,
            canSteer = true,
            canInteract = false, // Usually can't interact while driving
            canUseItems = true, // Very limited by item-specific restrictions
            canEquipItems = true // Very limited by item-specific restrictions
        };
    }
}
