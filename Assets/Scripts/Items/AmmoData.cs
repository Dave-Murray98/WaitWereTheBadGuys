using UnityEngine;

[System.Serializable]
public class AmmoData
{
    [Header("Ammo Properties")]
    [Tooltip("Weapons that can use this ammo type")]
    public ItemData[] compatibleWeapons;
    [Tooltip("Damage modifier for this ammo type")]
    public float damageModifier = 1f;
}