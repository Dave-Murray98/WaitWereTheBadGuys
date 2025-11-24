using UnityEngine;

[System.Serializable]
public class MeleeWeaponData
{
    [Header("Weapon Stats")]
    public float damage = 10f;

    public MeleeWeaponType weaponType = MeleeWeaponType.Sharp; // Sharp or Blunt

}

public enum MeleeWeaponType
{
    Sharp,
    Blunt
}
