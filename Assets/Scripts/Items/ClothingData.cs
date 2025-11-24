using UnityEngine;

/// <summary>
/// Enhanced clothing data structure with stats, condition, and equipment layers.
/// Integrated with the existing ItemData system for clothing items.
/// </summary>
[System.Serializable]
public class ClothingData
{
    [Header("Clothing Type & Layers")]
    [Tooltip("What type of clothing this is (Head, Torso, etc.)")]
    public ClothingType clothingType;

    [Tooltip("Which layers this clothing can be equipped to")]
    public ClothingLayer[] validLayers;

    [Header("Defensive Stats")]
    [Tooltip("Physical damage reduction provided by this clothing")]
    [Range(0f, 100f)]
    public float defenseValue = 0f;

    [Tooltip("Cold weather protection provided by this clothing")]
    [Range(0f, 100f)]
    public float warmthValue = 0f;

    [Tooltip("Rain and water resistance provided by this clothing")]
    [Range(0f, 100f)]
    public float rainResistance = 0f;

    [Header("Condition System")]
    [Tooltip("Maximum condition/durability of this clothing item")]
    [Range(0f, 100f)]
    public float maxCondition = 100f;

    [Tooltip("Current condition of this specific clothing item")]
    [Range(0f, 100f)]
    public float currentCondition = 100f;

    [Tooltip("How much condition is lost when damaged")]
    [Range(0f, 10f)]
    public float damageRate = 1f;

    [Header("Repair Settings")]
    [Tooltip("Can this clothing item be repaired?")]
    public bool canBeRepaired = true;

    [Tooltip("Minimum condition this item can be repaired to (wear and tear)")]
    [Range(0f, 100f)]
    public float minimumRepairCondition = 20f;

    /// <summary>
    /// Gets the condition as a percentage (0-1)
    /// </summary>
    public float ConditionPercentage => maxCondition > 0 ? currentCondition / maxCondition : 0f;

    /// <summary>
    /// Checks if the clothing item is in good condition (above 80%)
    /// </summary>
    public bool IsInGoodCondition => ConditionPercentage >= 0.8f;

    /// <summary>
    /// Checks if the clothing item is damaged (below 50%)
    /// </summary>
    public bool IsDamaged => ConditionPercentage < 0.5f;

    /// <summary>
    /// Checks if the clothing item is severely damaged (below 20%)
    /// </summary>
    public bool IsSeverelyDamaged => ConditionPercentage < 0.2f;

    /// <summary>
    /// Checks if this clothing can be equipped to the specified layer
    /// </summary>
    public bool CanEquipToLayer(ClothingLayer layer)
    {
        return System.Array.Exists(validLayers, l => l == layer);
    }

    /// <summary>
    /// Damages the clothing item by the specified amount
    /// </summary>
    public void TakeDamage(float damageAmount = -1f)
    {
        if (damageAmount < 0)
            damageAmount = damageRate;

        currentCondition = Mathf.Max(0f, currentCondition - damageAmount);
    }

    /// <summary>
    /// Repairs the clothing item by the specified amount
    /// </summary>
    public bool RepairCondition(float repairAmount)
    {
        if (!canBeRepaired || currentCondition >= maxCondition)
            return false;

        float maxRepairTo = Mathf.Max(minimumRepairCondition, maxCondition);
        currentCondition = Mathf.Min(maxRepairTo, currentCondition + repairAmount);
        return true;
    }

    /// <summary>
    /// Gets the effective defense value based on current condition
    /// </summary>
    public float GetEffectiveDefense()
    {
        return defenseValue * ConditionPercentage;
    }

    /// <summary>
    /// Gets the effective warmth value based on current condition
    /// </summary>
    public float GetEffectiveWarmth()
    {
        return warmthValue * ConditionPercentage;
    }

    /// <summary>
    /// Gets the effective rain resistance based on current condition
    /// </summary>
    public float GetEffectiveRainResistance()
    {
        return rainResistance * ConditionPercentage;
    }

    /// <summary>
    /// Gets a user-friendly condition description
    /// </summary>
    public string GetConditionDescription()
    {
        float percentage = ConditionPercentage;

        if (percentage >= 0.9f) return "Excellent";
        if (percentage >= 0.7f) return "Good";
        if (percentage >= 0.5f) return "Fair";
        if (percentage >= 0.3f) return "Poor";
        if (percentage >= 0.1f) return "Very Poor";
        return "Ruined";
    }

    /// <summary>
    /// Creates a copy of this clothing data for new instances
    /// </summary>
    public ClothingData CreateCopy()
    {
        return new ClothingData
        {
            clothingType = this.clothingType,
            validLayers = (ClothingLayer[])this.validLayers.Clone(),
            defenseValue = this.defenseValue,
            warmthValue = this.warmthValue,
            rainResistance = this.rainResistance,
            maxCondition = this.maxCondition,
            currentCondition = this.maxCondition, // New items start at max condition
            damageRate = this.damageRate,
            canBeRepaired = this.canBeRepaired,
            minimumRepairCondition = this.minimumRepairCondition
        };
    }
}

/// <summary>
/// Types of clothing based on body area
/// </summary>
public enum ClothingType
{
    Head,    // Hats, helmets, masks, scarves
    Torso,   // Shirts, jackets, vests, coats
    Hands,   // Gloves, mittens
    Legs,    // Pants, shorts, leggings
    Socks,   // Socks, stockings
    Shoes    // Boots, shoes, sandals
}

/// <summary>
/// Specific equipment layers for clothing items.
/// Allows for layering system (inner/outer layers).
/// </summary>
public enum ClothingLayer
{
    // Head layers (2 slots total)
    HeadUpper,   // Hat slot - hats, helmets, caps
    HeadLower,   // Scarf slot - scarves, face masks, neck warmers

    // Torso layers (2 slots total)  
    TorsoInner,  // Inner layer - t-shirts, tank tops, base layers
    TorsoOuter,  // Outer layer - jackets, coats, vests, hoodies

    // Leg layers (2 slots total)
    LegsInner,   // Inner layer - thermal underwear, leggings, base layers
    LegsOuter,   // Outer layer - pants, jeans, shorts, overalls

    // Single layers (1 slot each)
    Hands,       // Gloves, mittens
    Socks,       // Socks, stockings  
    Shoes        // Boots, shoes, sandals
}