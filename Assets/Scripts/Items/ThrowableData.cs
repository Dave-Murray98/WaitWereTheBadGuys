using UnityEngine;

[System.Serializable]
public class ThrowableData
{
    [Header("Weapon Stats")]
    public float damage = 10f;
    public ThrowableType throwableType;

    public float effectRadius = 5f; // Radius of explosion for explosives
    public float fuseTime = 3f; // Time before explosion for explosives

    public bool canCancelThrow = true; // Whether the throw can be canceled while aiming (ie throwing knives can, but grenades cannot)

}

public enum ThrowableType
{
    Grenade,
    Molotov,
    SmokeGrenade,
    Flashbang,
    StickyBomb,
    ThrowingKnife
}