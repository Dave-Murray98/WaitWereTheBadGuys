using UnityEngine;

/// <summary>
/// Enumeration of the main player states that drive movement and equipment behavior
/// </summary>
public enum PlayerStateType
{
    Ground = 0,   // Player is on land (walking, running, jumping, crouching)
    Water = 1,    // Player is in water (swimming, diving, surfacing) 
    Vehicle = 2,   // Player is riding a vehicle
    Climbing = 3,  // Player is climbing a ledge
}