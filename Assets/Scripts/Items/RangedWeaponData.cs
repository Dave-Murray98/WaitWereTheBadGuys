using UnityEngine;

[System.Serializable]
public class RangedWeaponData
{
    [Header("Weapon Stats")]
    public float damage = 10f;
    public float range = 5f;
    public float fireRate = 1f; // Attacks per second
    [Tooltip("Ammo type this weapon uses (for guns)")]
    public ItemData requiredAmmoType;
    [Tooltip("Maximum ammo capacity")]
    public int maxAmmo = 30;
    [Tooltip("Current ammo loaded (saved per weapon instance)")]
    public int currentAmmo = 0;

    [Header("ADS Configuration")]
    [Tooltip("Field of view when aiming down sights")]
    public float adsFOV = 45f;
    [Tooltip("ADS transition duration")]
    public float adsTransitionTime = 0.25f;

    [Header("ADS Position & Rotation Offsets")]
    [Tooltip("Position offset for ADS camera placement")]
    public Vector3 adsPositionOffset = Vector3.zero;
    [Tooltip("Rotation offsets for perfect sight alignment (Pitch, Yaw, Roll)")]
    public Vector3 adsRotationOffset = Vector3.zero;

    [Header("ADS Setup Info")]
    [Tooltip("Information about when this ADS setup was last configured")]
    public string adsSetupInfo = "Not configured";
    [Tooltip("Version of the ADS setup system used")]
    public int adsSetupVersion = 1;

    /// <summary>
    /// Check if ADS has been properly configured for this weapon
    /// </summary>
    public bool IsADSConfigured()
    {
        // Consider it configured if we have any non-zero offsets or if setup info indicates configuration
        return adsPositionOffset != Vector3.zero ||
               adsRotationOffset != Vector3.zero ||
               !adsSetupInfo.Equals("Not configured");
    }

    /// <summary>
    /// Reset ADS configuration to defaults
    /// </summary>
    public void ResetADSConfiguration()
    {
        adsPositionOffset = Vector3.zero;
        adsRotationOffset = Vector3.zero;
        adsSetupInfo = "Not configured";
    }

    /// <summary>
    /// Update ADS configuration with new values
    /// </summary>
    public void UpdateADSConfiguration(Vector3 positionOffset, Vector3 rotationOffset, string setupInfo)
    {
        adsPositionOffset = positionOffset;
        adsRotationOffset = rotationOffset;
        adsSetupInfo = setupInfo;
        adsSetupVersion = 1; // Current version
    }

    /// <summary>
    /// Get debug information about ADS configuration
    /// </summary>
    public string GetADSDebugInfo()
    {
        return $"ADS Config - Position: {adsPositionOffset}, Rotation: {adsRotationOffset}, Info: {adsSetupInfo}";
    }
}