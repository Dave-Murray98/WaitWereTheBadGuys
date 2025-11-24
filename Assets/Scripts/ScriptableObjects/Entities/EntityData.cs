using UnityEngine;

[CreateAssetMenu(fileName = "EntityData", menuName = "Scriptable Objects/EntityData")]
public class EntityData : ScriptableObject
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float healthRegenRate = 0f;

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float crouchSpeed = 2.5f;
    public float jumpHeight = 4f;
    public float gravity = -9.81f;
    public float stopForce = 10f; // Force applied to stop the character when not moving

    [Header("position")]
    public Vector3 currentPosition;
    public Vector3 currentRotation;

    [Header("Audio")]
    public float footstepRate = 0.5f; // Time between footsteps

}
