using UnityEngine;
using System.Text;
using UnityEditor;
using Unity.VisualScripting;

/// <summary>
/// Static utility class for formatting item stats into readable text
/// Handles all item types and provides consistent formatting
/// </summary>
public static class ItemStatsFormatter
{
    // Color tags for rich text formatting
    private const string STAT_VALUE_COLOR = "<color=#00FFFF>";  // Cyan
    private const string DEGRADATION_EXCELLENT_COLOR = "<color=#00FF00>";  // Green
    private const string DEGRADATION_GOOD_COLOR = "<color=#FFFF00>";       // Yellow
    private const string DEGRADATION_FAIR_COLOR = "<color=#FFA500>";       // Orange
    private const string DEGRADATION_POOR_COLOR = "<color=#FF4500>";       // Red-Orange
    private const string DEGRADATION_CRITICAL_COLOR = "<color=#FF0000>";   // Red
    private const string POSITIVE_STAT_COLOR = "<color=#00FF00>";          // Green
    private const string NEGATIVE_STAT_COLOR = "<color=#FF6666>";          // Light Red
    private const string COLOR_END = "</color>";

    /// <summary>
    /// Format complete item stats for display
    /// </summary>
    public static string FormatItemStats(ItemData itemData, float currentCondition = -1f)
    {
        if (itemData == null) return "No item data available.";

        StringBuilder stats = new StringBuilder();

        // Add item type
        stats.AppendLine($"<b>Type:</b> {STAT_VALUE_COLOR}{GetItemTypeDisplayName(itemData.itemType)}{COLOR_END}");
        stats.AppendLine();

        // Add degradation info if applicable
        if (itemData.CanDegrade)
        {
            AddDegradationStats(stats, itemData, currentCondition);
            stats.AppendLine();
        }

        // Add type-specific stats
        switch (itemData.itemType)
        {
            case ItemType.Consumable:
                AddConsumableStats(stats, itemData.ConsumableData);
                break;
            case ItemType.RangedWeapon:
                AddWeaponStats(stats, itemData.RangedWeaponData);
                break;
            case ItemType.Tool:
                AddToolStats(stats, itemData.ToolData);
                break;
            case ItemType.Clothing:
                AddClothingStats(stats, itemData);
                break;
            case ItemType.Ammo:
                AddAmmoStats(stats, itemData.AmmoData);
                break;
            case ItemType.KeyItem:
                AddKeyItemStats(stats, itemData);
                break;
        }

        return stats.ToString().TrimEnd();
    }

    private static void AddDegradationStats(StringBuilder stats, ItemData itemData, float currentCondition)
    {
        float condition = currentCondition >= 0 ? currentCondition : itemData.maxDurability;
        float conditionPercent = (condition / itemData.maxDurability) * 100f;

        string conditionColor = GetConditionColor(conditionPercent);
        string conditionText = GetConditionText(conditionPercent);

        stats.AppendLine("<b>Condition:</b>");
        stats.AppendLine($"  {conditionColor}{condition:F0}/{itemData.maxDurability:F0} ({conditionPercent:F0}%) - {conditionText}{COLOR_END}");

        if (itemData.degradationType != DegradationType.None)
        {
            stats.AppendLine($"  <b>Degrades:</b> {STAT_VALUE_COLOR}{GetDegradationTypeText(itemData.degradationType)}{COLOR_END}");
            if (itemData.degradationRate > 0)
            {
                stats.AppendLine($"  <b>Rate:</b> {STAT_VALUE_COLOR}{itemData.degradationRate:F1}/use{COLOR_END}");
            }
        }
    }

    private static void AddConsumableStats(StringBuilder stats, ConsumableData consumableData)
    {
        if (consumableData == null) return;

        stats.AppendLine("<b>Effects:</b>");

        if (consumableData.healthRestore > 0)
        {
            stats.AppendLine($"  • Health: {POSITIVE_STAT_COLOR}+{consumableData.healthRestore:F0}{COLOR_END}");
        }

        if (consumableData.hungerRestore > 0)
        {
            stats.AppendLine($"  • Hunger: {POSITIVE_STAT_COLOR}+{consumableData.hungerRestore:F0}{COLOR_END}");
        }

        if (consumableData.thirstRestore > 0)
        {
            stats.AppendLine($"  • Thirst: {POSITIVE_STAT_COLOR}+{consumableData.thirstRestore:F0}{COLOR_END}");
        }

    }

    private static void AddWeaponStats(StringBuilder stats, RangedWeaponData weaponData)
    {
        if (weaponData == null) return;

        stats.AppendLine("<b>Combat Stats:</b>");
        stats.AppendLine($"  • Damage: {STAT_VALUE_COLOR}{weaponData.damage:F0}{COLOR_END}");
        stats.AppendLine($"  • Range: {STAT_VALUE_COLOR}{weaponData.range:F1}m{COLOR_END}");
        stats.AppendLine($"  • Fire Rate: {STAT_VALUE_COLOR}{weaponData.fireRate:F1}/sec{COLOR_END}");

        if (weaponData.requiredAmmoType != null)
        {
            stats.AppendLine();
            stats.AppendLine("<b>Ammunition:</b>");
            stats.AppendLine($"  • Type: {STAT_VALUE_COLOR}{weaponData.requiredAmmoType.itemName}{COLOR_END}");
            stats.AppendLine($"  • Loaded: {STAT_VALUE_COLOR}{weaponData.currentAmmo}/{weaponData.maxAmmo}{COLOR_END}");
        }
    }

    private static void AddToolStats(StringBuilder stats, ToolData equipmentData)
    {
        if (equipmentData == null) return;

        //TODO: Add tool-specific stats when implemented
    }

    private static void AddClothingStats(StringBuilder stats, ItemData itemData)
    {
        // For now, we'll add placeholder clothing stats
        // This will be replaced when we implement the full clothing system
        stats.AppendLine("<b>Clothing Properties:</b>");
        stats.AppendLine($"  • Warmth: {POSITIVE_STAT_COLOR}+15°C{COLOR_END}");
        stats.AppendLine($"  • Defense: {POSITIVE_STAT_COLOR}+8{COLOR_END}");
        stats.AppendLine($"  • Speed: {NEGATIVE_STAT_COLOR}-5%{COLOR_END}");
        stats.AppendLine($"  • Status: {STAT_VALUE_COLOR}Dry{COLOR_END}");

        // Note: This will be replaced with actual ClothingData when implemented
        stats.AppendLine();
    }

    private static void AddAmmoStats(StringBuilder stats, AmmoData ammoData)
    {
        if (ammoData == null) return;

        stats.AppendLine("<b>Ammunition Properties:</b>");
        stats.AppendLine($"  • Damage Modifier: {GetModifierColor(ammoData.damageModifier)}{FormatModifier(ammoData.damageModifier)}{COLOR_END}");
    }

    private static void AddKeyItemStats(StringBuilder stats, ItemData itemData)
    {
        stats.AppendLine("<b>Key Item Properties:</b>");
        stats.AppendLine($"  • Cannot be dropped");

        if (!string.IsNullOrEmpty(itemData.description))
        {
            stats.AppendLine();
            stats.AppendLine("<b>Special Notes:</b>");
            stats.AppendLine($"  {itemData.description}");
        }
    }

    #region Helper Methods

    private static string GetItemTypeDisplayName(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Consumable: return "Consumable";
            case ItemType.RangedWeapon: return "Weapon";
            case ItemType.Tool: return "Equipment";
            case ItemType.Clothing: return "Clothing";
            case ItemType.Ammo: return "Ammunition";
            case ItemType.KeyItem: return "Key Item";
            default: return itemType.ToString();
        }
    }

    private static string GetConditionColor(float conditionPercent)
    {
        if (conditionPercent >= 80f) return DEGRADATION_EXCELLENT_COLOR;
        if (conditionPercent >= 60f) return DEGRADATION_GOOD_COLOR;
        if (conditionPercent >= 40f) return DEGRADATION_FAIR_COLOR;
        if (conditionPercent >= 20f) return DEGRADATION_POOR_COLOR;
        return DEGRADATION_CRITICAL_COLOR;
    }

    private static string GetConditionText(float conditionPercent)
    {
        if (conditionPercent >= 80f) return "Excellent";
        if (conditionPercent >= 60f) return "Good";
        if (conditionPercent >= 40f) return "Fair";
        if (conditionPercent >= 20f) return "Poor";
        return "Critical";
    }

    private static string GetDegradationTypeText(DegradationType degradationType)
    {
        switch (degradationType)
        {
            case DegradationType.OverTime: return "Over time";
            case DegradationType.OnUse: return "When used";
            case DegradationType.Both: return "Over time & when used";
            default: return "Never";
        }
    }

    private static string GetModifierColor(float modifier)
    {
        return modifier >= 1f ? POSITIVE_STAT_COLOR :
               modifier < 1f ? NEGATIVE_STAT_COLOR :
               STAT_VALUE_COLOR;
    }

    private static string FormatModifier(float modifier)
    {
        if (modifier > 1f)
        {
            return $"+{((modifier - 1f) * 100f):F0}%";
        }
        else if (modifier < 1f)
        {
            return $"-{((1f - modifier) * 100f):F0}%";
        }
        else
        {
            return "±0%";
        }
    }

    #endregion
}