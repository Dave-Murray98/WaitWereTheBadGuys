using UnityEditor.EditorTools;
using UnityEngine;

/// <summary>
/// Item type enumeration for different item categories
/// UPDATED: Added Clothing type
/// </summary>
public enum ItemType
{
    Unarmed,
    Consumable, // Food, meds, water - can be consumed to affect player stats
    RangedWeapon,     // Guns, bows - ranged combat items
    MeleeWeapon,   // Knives, clubs - close combat items
    Throwable, // grenades, throwing knives - Items that can be thrown
    Tool,  // Lock-picks, scanners - interaction tools
    KeyItem,    // Quest items, keys - cannot be dropped
    Ammo,       // Ammunition - stackable
    Clothing,    // Clothing items - can be worn
    Bow // Special case for bows, which are ranged but have unique handling
}

/// <summary>
/// Degradation type for items
/// </summary>
public enum DegradationType
{
    None,       // Item doesn't degrade
    OverTime,   // Degrades gradually over time (food spoilage)
    OnUse,      // Degrades when used (weapon wear)
    Both        // Degrades both over time and on use
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic Item Info")]
    public string itemName;
    [TextArea(2, 4)]
    public string description;

    [Header("Item Type")]
    public ItemType itemType = ItemType.Consumable;

    [Header("Shape Configuration")]
    public TetrominoType shapeType = TetrominoType.Single;

    [Header("Visual Configuration")]
    public Sprite itemSprite;
    [Range(.1f, 5.0f)]
    public float spriteScaleX = 1.0f;
    [Range(.1f, 5.0f)]
    public float spriteScaleY = 1.0f;
    public Vector2 spritePositionOffset = Vector2.zero;
    public Color cellColor = Color.gray;

    [Header("Equipped Item Configuration")]
    [Tooltip("Prefab to use when item is equipped in hand (with bones, animations, etc.)")]
    public GameObject equippedItemPrefab;

    [Tooltip("Position offset when equipped in hand")]
    public Vector3 equippedPositionOffset = Vector3.zero;

    [Tooltip("Rotation offset when equipped in hand")]
    public Vector3 equippedRotationOffset = Vector3.zero;

    [Tooltip("Scale override to apply to the equipped item (Vector3.zero = use prefab's scale)")]
    public Vector3 equippedScaleOverride = Vector3.zero;

    [Header("World Visual Configuration")]
    [Tooltip("Visual prefab to spawn when item is dropped in the world")]
    public GameObject visualPrefab;
    [Tooltip("Scale override to apply to the visual prefab (Vector3.zero = use prefab's scale)")]
    public Vector3 visualPrefabScale = Vector3.zero;
    [Tooltip("Height offset from ground when dropped")]
    public float groundHeightOffset = 0.1f;
    [Tooltip("Whether this item can be physically simulated when dropped")]
    public bool usePhysicsOnDrop = true;
    [Tooltip("Mass of the item when dropped (affects physics simulation)")]
    [Range(0.1f, 100f)]
    public float objectMass = 1f;
    [Tooltip("Whether this item should float on water")]
    public bool shouldFloat = false;

    [Tooltip("Custom interaction collider size (if zero, auto-calculated from visual bounds)")]
    public Vector3 interactionColliderSize = Vector3.zero;
    public bool isRotatable = true;

    [Header("State Useability Settings")]
    [Tooltip("Can this item be used/equipped when player is on ground (walking/running)?")]
    public bool canUseOnGround = true;

    [Tooltip("Can this item be used/equipped when player is in water (swimming)?")]
    public bool canUseInWater = false; // Default to false since most items shouldn't work underwater

    [Tooltip("Can this item be used/equipped when player is in/on a vehicle?")]
    public bool canUseInVehicle = false; //For now, I'm disabling all vehicle items by default

    [Header("Degradation System")]
    public DegradationType degradationType = DegradationType.None;
    [Range(0f, 100f)]
    public float maxDurability = 100f;
    [Range(0f, 10f)]
    public float degradationRate = 1f; // Units per use or per hour
    [Tooltip("Time in hours for one degradation tick (for OverTime degradation)")]
    public float degradationInterval = 1f;

    [Header("Animation System")]
    [Tooltip("Animation database containing all the player body animations for this item")]
    public PlayerBodyAnimationDatabase playerBodyAnimationDatabase;

    [Tooltip("Item animation database containing all animations that play on the equipped item itself")]
    public PlayerItemAnimationDatabase playerItemAnimationDatabase;

    [Header("Weapon Sway Settings")]
    [Tooltip("Multiplier for weapon sway effect (1.0 = normal, higher = more sway, lower = less sway)")]
    [SerializeField, Range(0f, 3f)] private float weaponSwayMultiplier = 1f;

    [Header("Item Type Specific Settings")]
    [Header("Consumable Settings")]
    [SerializeField] private ConsumableData consumableData;

    [Header("Weapon Settings")]
    [SerializeField] private RangedWeaponData rangedWeaponData;

    [Header("Ammo Settings")]
    [SerializeField] private AmmoData ammoData;

    [Header("Clothing Settings")]
    [SerializeField] private ClothingData clothingData;

    [Header("Melee Weapon Settings")]
    [SerializeField] private MeleeWeaponData meleeWeaponData;

    [Header("Tool Settings")]
    [SerializeField] private ToolData toolData;

    [Header("Explosive Settings")]
    [SerializeField] private ThrowableData throwableData;

    [Header("Bow Settings")]
    [SerializeField] private BowData bowData;

    [Header("Key Item Settings")]
    [SerializeField] private KeyItemData keyItemData;


    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Properties for accessing type-specific data
    public ConsumableData ConsumableData => consumableData;
    public RangedWeaponData RangedWeaponData => rangedWeaponData;
    public AmmoData AmmoData => ammoData;
    public ClothingData ClothingData => clothingData;
    public MeleeWeaponData MeleeWeaponData => meleeWeaponData;
    public ToolData ToolData => toolData;
    public ThrowableData ThrowableData => throwableData;
    public BowData BowData => bowData;
    public KeyItemData KeyItemData => keyItemData;


    // Get the color for this item (custom color instead of shape-based)
    public Color CellColor => cellColor;

    // Check if item can be dropped
    public bool CanDrop => itemType != ItemType.KeyItem;

    // Check if item degrades
    public bool CanDegrade => degradationType != DegradationType.None && maxDurability > 0;

    // Check if item is clothing
    public bool IsClothing => itemType == ItemType.Clothing;

    // Check if item has visual prefab
    public bool HasVisualPrefab => visualPrefab != null;

    /// <summary>Weapon sway multiplier for this item</summary>
    public float WeaponSwayMultiplier => weaponSwayMultiplier;

    // Get the effective scale for the visual prefab
    public Vector3 GetVisualPrefabScale()
    {
        return visualPrefabScale == Vector3.zero ? Vector3.one : visualPrefabScale;
    }

    // Get bounding size for this item's shape
    public Vector2Int GetBoundingSize()
    {
        var shapeData = TetrominoDefinitions.GetRotationState(shapeType, 0);
        if (shapeData.cells.Length == 0)
            return Vector2Int.one;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var cell in shapeData.cells)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        return new Vector2Int(maxX - minX + 1, maxY - minY + 1);
    }

    // Get interaction collider size (auto-calculated if not specified)
    public Vector3 GetInteractionColliderSize()
    {
        if (interactionColliderSize != Vector3.zero)
            return interactionColliderSize;

        // If we have a visual prefab, we'll calculate bounds at runtime
        // For now, return a default size
        return Vector3.one;
    }

    // Calculate height for proper ground placement (will be calculated from visual prefab at runtime)
    public float GetItemHeight()
    {
        // This will be calculated from the visual prefab bounds at runtime
        return 1f; // Default height
    }

    #region State Usability

    /// <summary>
    /// Check if this item can be used in the specified player state
    /// </summary>
    /// <param name="playerState">The state to check against</param>
    /// <returns>True if the item can be used in this state</returns>
    public bool CanUseInState(PlayerStateType playerState)
    {
        return playerState switch
        {
            PlayerStateType.Ground => canUseOnGround,
            PlayerStateType.Water => canUseInWater,
            PlayerStateType.Vehicle => canUseInVehicle,
            _ => false // Unknown states default to not usable
        };
    }

    /// <summary>
    /// Get a list of states where this item can be used (for UI display)
    /// </summary>
    /// <returns>Array of usable states</returns>
    public PlayerStateType[] GetUsableStates()
    {
        var usableStates = new System.Collections.Generic.List<PlayerStateType>();

        if (canUseOnGround) usableStates.Add(PlayerStateType.Ground);
        if (canUseInWater) usableStates.Add(PlayerStateType.Water);
        if (canUseInVehicle) usableStates.Add(PlayerStateType.Vehicle);

        return usableStates.ToArray();
    }

    #endregion

    #region ANIMATION SYSTEM INTEGRATION

    /// <summary>
    /// Get the item type ID for animator parameters (matches the animation system mapping)
    /// </summary>
    /// <returns>Integer ID for animator itemType parameter</returns>
    public int GetItemTypeAnimationID()
    {
        return itemType switch
        {
            ItemType.Unarmed => 0,
            ItemType.Consumable => 1,
            ItemType.RangedWeapon => 2,
            ItemType.MeleeWeapon => 3,
            ItemType.Throwable => 4,
            ItemType.Tool => 5,
            ItemType.KeyItem => 6,
            _ => 0 // Default to Unarmed for unknown types
        };
    }

    /// <summary>
    /// Get the player body animation database
    /// </summary>
    public PlayerBodyAnimationDatabase GetPlayerBodyAnimationDatabase()
    {
        return playerBodyAnimationDatabase;
    }

    /// <summary>
    /// Get the player item animation database
    /// </summary>
    public PlayerItemAnimationDatabase GetPlayerItemAnimationDatabase()
    {
        return playerItemAnimationDatabase;
    }


    #endregion

    // Validation
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(itemName))
            itemName = name;

        spriteScaleX = Mathf.Clamp(spriteScaleX, .1f, 5.0f);
        spriteScaleY = Mathf.Clamp(spriteScaleY, .1f, 5.0f);

        // Validate degradation settings
        if (degradationType != DegradationType.None)
        {
            if (maxDurability <= 0)
                maxDurability = 100f;
            if (degradationRate <= 0)
                degradationRate = 1f;
        }

        // Validate visual prefab scale
        if (visualPrefabScale != Vector3.zero && (visualPrefabScale.x <= 0 || visualPrefabScale.y <= 0 || visualPrefabScale.z <= 0))
        {
            Debug.LogWarning($"Item {itemName} has invalid visual prefab scale. Resetting to Vector3.zero (use prefab scale).");
            visualPrefabScale = Vector3.zero;
        }

        // Validate object mass
        if (objectMass <= 0f)
        {
            objectMass = 1f;
        }

        if (groundHeightOffset < 0f)
            groundHeightOffset = 0.1f;

        // Warn about missing visual prefab for droppable items
        if (CanDrop && !HasVisualPrefab && showDebugInfo)
        {
            Debug.LogWarning($"Droppable item {itemName} has no visual prefab assigned!");
        }


        if (itemType == ItemType.Clothing)
        {
            if (clothingData == null)
            {
                Debug.LogWarning($"Clothing item {itemName} has no ClothingData assigned!");
            }
            else if (clothingData.validLayers == null || clothingData.validLayers.Length == 0)
            {
                Debug.LogWarning($"Clothing item {itemName} has no valid layers assigned!");
            }
        }

        if (showDebugInfo && Application.isPlaying)
        {
            Debug.Log($"Item: {itemName}, Type: {itemType}, Shape: {shapeType}, Can Drop: {CanDrop}, Has Visual Prefab: {HasVisualPrefab}");
        }
    }
}