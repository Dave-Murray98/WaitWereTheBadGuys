using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Scriptable Objects/PlayerData")]
public class PlayerData : EntityData
{
    [Header("Camera")]
    public float lookSensitivity = 2f;
    public float verticalLookLimit = 85f;
    public float crouchHeightMultiplier = 0.5f; // Default crouch height multiplier


}
