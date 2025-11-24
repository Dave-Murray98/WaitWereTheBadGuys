using UnityEngine;

/// <summary>
/// UPDATED: Settings for the unified visual prefab item drop system
/// Removed old single-mesh settings and added pickup interaction prefab reference
/// </summary>
[CreateAssetMenu(fileName = "ItemDropSettings", menuName = "Inventory/Item Drop Settings")]
public class ItemDropSettings : ScriptableObject
{
    [Header("Drop Behavior")]
    [Tooltip("Maximum number of items that can be dropped in the scene")]
    public int maxDroppedItems = 50;

    [Tooltip("Distance from player to drop items")]
    public float dropDistanceFromPlayer = 2f;

    [Tooltip("Whether dropped items should automatically settle after physics simulation")]
    public bool enablePhysicsSettling = true;

    [Tooltip("Time after which items stop physics simulation")]
    public float physicsSettleTime = 5f;

    [Header("Pickup Interaction")]
    [Tooltip("Prefab containing interaction components (collider + ItemPickupInteractable)")]
    public GameObject pickupInteractionPrefab;

    [Header("Physics Settings")]
    [Tooltip("Linear drag applied to dropped items")]
    public float itemDrag = 2f;

    [Tooltip("Angular drag applied to dropped items")]
    public float itemAngularDrag = 1f;

    [Tooltip("Whether to use physics simulation for dropped items")]
    public bool usePhysicsSimulation = true;

    [Tooltip("Force applied when dropping items (random within range)")]
    public Vector3 dropForceMin = new Vector3(-1f, 0f, -1f);
    public Vector3 dropForceMax = new Vector3(1f, 2f, 1f);

    [Header("Position Validation")]
    [Tooltip("Maximum distance to search for valid drop position")]
    public float maxSearchRadius = 5f;

    [Tooltip("Maximum distance to check for ground below drop position")]
    public float maxGroundCheckDistance = 10f;

    [Tooltip("Maximum slope angle for valid drop positions (degrees)")]
    public float maxDropSlope = 45f;

    [Tooltip("Additional height offset from ground")]
    public float additionalGroundOffset = 0.05f;

    [Tooltip("Radius for obstacle detection")]
    public float obstacleCheckRadius = 0.5f;

    [Header("Layer Masks")]
    [Tooltip("What layers count as ground for dropping items")]
    public LayerMask groundLayerMask = 1; // Default layer

    [Tooltip("What layers count as obstacles that block item drops")]
    public LayerMask obstacleLayerMask = 1; // Default layer

    [Header("Visual Effects")]
    [Tooltip("Particle effect to play when dropping items")]
    public GameObject dropEffect;

    [Tooltip("Sound to play when dropping items")]
    public AudioClip dropSound;

    [Tooltip("Whether items should bob up and down when on the ground")]
    public bool enableItemBobbing = true;

    [Tooltip("Height of bobbing animation")]
    public float bobbingHeight = 0.1f;

    [Tooltip("Speed of bobbing animation")]
    public float bobbingSpeed = 1f;

    [Header("Debug")]
    [Tooltip("Enable debug logging")]
    public bool enableDebugLogs = false;

    [Tooltip("Show debug visualization in scene view")]
    public bool showDebugVisualization = false;

    [Tooltip("Color for debug visualization")]
    public Color debugColor = Color.yellow;

    // Cached search pattern for performance
    private Vector2[] searchPattern;
    private bool searchPatternGenerated = false;

    /// <summary>
    /// Gets a random drop force within the specified range
    /// </summary>
    public Vector3 GetRandomDropForce()
    {
        return new Vector3(
            Random.Range(dropForceMin.x, dropForceMax.x),
            Random.Range(dropForceMin.y, dropForceMax.y),
            Random.Range(dropForceMin.z, dropForceMax.z)
        );
    }

    /// <summary>
    /// Gets the spiral search pattern for finding valid drop positions
    /// </summary>
    public Vector2[] GetSearchPattern()
    {
        if (!searchPatternGenerated || searchPattern == null)
        {
            GenerateSearchPattern();
        }
        return searchPattern;
    }

    /// <summary>
    /// Generates a spiral search pattern for finding valid drop positions
    /// </summary>
    private void GenerateSearchPattern()
    {
        var pattern = new System.Collections.Generic.List<Vector2>();

        // Start with center position
        pattern.Add(Vector2.zero);

        // Generate spiral pattern
        int rings = Mathf.CeilToInt(maxSearchRadius);
        for (int ring = 1; ring <= rings; ring++)
        {
            float radius = ring * 0.5f;
            int pointsInRing = ring * 8; // More points in outer rings

            for (int i = 0; i < pointsInRing; i++)
            {
                float angle = (float)i / pointsInRing * 2f * Mathf.PI;
                Vector2 point = new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );

                if (point.magnitude <= maxSearchRadius)
                {
                    pattern.Add(point);
                }
            }
        }

        searchPattern = pattern.ToArray();
        searchPatternGenerated = true;
    }

    /// <summary>
    /// Validates the settings and fixes invalid values
    /// </summary>
    private void OnValidate()
    {
        // Ensure positive values
        maxDroppedItems = Mathf.Max(1, maxDroppedItems);
        dropDistanceFromPlayer = Mathf.Max(0.5f, dropDistanceFromPlayer);
        physicsSettleTime = Mathf.Max(0f, physicsSettleTime);
        itemDrag = Mathf.Max(0f, itemDrag);
        itemAngularDrag = Mathf.Max(0f, itemAngularDrag);
        maxSearchRadius = Mathf.Max(1f, maxSearchRadius);
        maxGroundCheckDistance = Mathf.Max(1f, maxGroundCheckDistance);
        maxDropSlope = Mathf.Clamp(maxDropSlope, 0f, 90f);
        additionalGroundOffset = Mathf.Max(0f, additionalGroundOffset);
        obstacleCheckRadius = Mathf.Max(0.1f, obstacleCheckRadius);
        bobbingHeight = Mathf.Max(0f, bobbingHeight);
        bobbingSpeed = Mathf.Max(0.1f, bobbingSpeed);

        // Validate force ranges
        if (dropForceMax.x < dropForceMin.x) dropForceMax.x = dropForceMin.x;
        if (dropForceMax.y < dropForceMin.y) dropForceMax.y = dropForceMin.y;
        if (dropForceMax.z < dropForceMin.z) dropForceMax.z = dropForceMin.z;

        // Regenerate search pattern if radius changed
        if (searchPatternGenerated)
        {
            searchPatternGenerated = false;
        }

        // Warn about missing pickup interaction prefab
        if (pickupInteractionPrefab == null && enableDebugLogs)
        {
            Debug.LogWarning("ItemDropSettings: No pickup interaction prefab assigned!");
        }
    }

    /// <summary>
    /// Creates default settings values
    /// </summary>
    public void SetDefaults()
    {
        maxDroppedItems = 50;
        dropDistanceFromPlayer = 2f;
        enablePhysicsSettling = true;
        physicsSettleTime = 5f;
        itemDrag = 2f;
        itemAngularDrag = 1f;
        usePhysicsSimulation = true;
        dropForceMin = new Vector3(-1f, 0f, -1f);
        dropForceMax = new Vector3(1f, 2f, 1f);
        maxSearchRadius = 5f;
        maxGroundCheckDistance = 10f;
        maxDropSlope = 45f;
        additionalGroundOffset = 0.05f;
        obstacleCheckRadius = 0.5f;
        groundLayerMask = 1;
        obstacleLayerMask = 1;
        enableItemBobbing = true;
        bobbingHeight = 0.1f;
        bobbingSpeed = 1f;
        enableDebugLogs = false;
        showDebugVisualization = false;
        debugColor = Color.yellow;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Inventory/Item Drop Settings")]
    private static void CreateItemDropSettingsAsset()
    {
        var settings = CreateInstance<ItemDropSettings>();
        settings.SetDefaults();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (string.IsNullOrEmpty(path))
        {
            path = "Assets";
        }
        else if (System.IO.Path.GetExtension(path) != "")
        {
            path = path.Replace(System.IO.Path.GetFileName(UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject)), "");
        }

        string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path + "/ItemDropSettings.asset");
        UnityEditor.AssetDatabase.CreateAsset(settings, assetPathAndName);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
        UnityEditor.EditorUtility.FocusProjectWindow();
        UnityEditor.Selection.activeObject = settings;
    }
#endif
}