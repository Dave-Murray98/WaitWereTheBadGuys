using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Main clothing UI controller that manages all clothing slot UIs.
/// Positioned to the left of the inventory UI and handles clothing display and interaction.
/// Follows the same event-driven pattern as InventoryGridVisual.
/// </summary>
public class ClothingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject clothingPanel;
    [SerializeField] private Transform slotContainer;

    [Header("Clothing Slot UI References")]
    [SerializeField] private ClothingSlotUI[] slotUIs;

    [Header("Auto-Setup")]
    [SerializeField] private bool autoFindSlotUIs = true;
    [SerializeField] private GameObject slotUIPrefab;

    // [Header("Layout Settings")]
    // [SerializeField] private float slotSpacing = 10f;

    // Reference to clothing system
    private ClothingManager clothingManager;

    private void Awake()
    {
        if (autoFindSlotUIs)
        {
            FindOrCreateSlotUIs();
        }
    }

    private void Start()
    {
        InitializeFromClothingManager();
    }

    private void OnEnable()
    {
        SubscribeToClothingEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromClothingEvents();
    }

    /// <summary>
    /// Initialize from the clothing manager and setup UI
    /// </summary>
    private void InitializeFromClothingManager()
    {
        clothingManager = ClothingManager.Instance;
        if (clothingManager == null)
        {
            Debug.LogError("ClothingManager not found! Make sure it exists in the scene.");
            return;
        }

        SubscribeToClothingEvents();
        RefreshAllSlotUIs();

        //        Debug.Log("ClothingUI initialized successfully");
    }

    /// <summary>
    /// Subscribe to clothing manager events for UI updates
    /// </summary>
    private void SubscribeToClothingEvents()
    {
        if (clothingManager != null)
        {
            clothingManager.OnItemEquipped -= OnItemEquipped;
            clothingManager.OnItemUnequipped -= OnItemUnequipped;
            clothingManager.OnClothingConditionChanged -= OnClothingConditionChanged;
            clothingManager.OnClothingDataChanged -= OnClothingDataChanged;

            clothingManager.OnItemEquipped += OnItemEquipped;
            clothingManager.OnItemUnequipped += OnItemUnequipped;
            clothingManager.OnClothingConditionChanged += OnClothingConditionChanged;
            clothingManager.OnClothingDataChanged += OnClothingDataChanged;
        }

        // Also subscribe to inventory events to handle item removal

        GameEvents.OnInventoryOpened += OnInventoryOpened;
        GameEvents.OnInventoryClosed += OnInventoryClosed;

    }

    /// <summary>
    /// Unsubscribe from clothing manager events
    /// </summary>
    private void UnsubscribeFromClothingEvents()
    {
        if (clothingManager != null)
        {
            clothingManager.OnItemEquipped -= OnItemEquipped;
            clothingManager.OnItemUnequipped -= OnItemUnequipped;
            clothingManager.OnClothingConditionChanged -= OnClothingConditionChanged;
            clothingManager.OnClothingDataChanged -= OnClothingDataChanged;
        }

        GameEvents.OnInventoryOpened -= OnInventoryOpened;
        GameEvents.OnInventoryClosed -= OnInventoryClosed;

    }

    /// <summary>
    /// Find existing slot UIs or create them if needed
    /// </summary>
    private void FindOrCreateSlotUIs()
    {
        if (slotContainer == null)
        {
            Debug.LogWarning("No slot container assigned - cannot setup clothing slot UIs");
            return;
        }

        // Try to find existing slot UIs first
        var existingSlotUIs = slotContainer.GetComponentsInChildren<ClothingSlotUI>();

        if (existingSlotUIs.Length > 0)
        {
            slotUIs = existingSlotUIs;
            //            Debug.Log($"Found {slotUIs.Length} existing clothing slot UIs");

            foreach (var slotUI in slotUIs)
            {
                slotUI.Initialize(slotUI.TargetLayer);
            }
            return;
        }

        // Create slot UIs for each clothing layer if none exist
        CreateSlotUIs();
    }

    /// <summary>
    /// Create UI slots for all clothing layers
    /// </summary>
    private void CreateSlotUIs()
    {
        var clothingLayers = System.Enum.GetValues(typeof(ClothingLayer));
        var createdSlots = new List<ClothingSlotUI>();

        foreach (ClothingLayer layer in clothingLayers)
        {
            GameObject slotObj = CreateSlotUI(layer);
            if (slotObj != null)
            {
                var slotUI = slotObj.GetComponent<ClothingSlotUI>();
                if (slotUI != null)
                {
                    createdSlots.Add(slotUI);
                }
            }
        }

        slotUIs = createdSlots.ToArray();
        Debug.Log($"Created {slotUIs.Length} clothing slot UIs");
    }

    /// <summary>
    /// Create a single slot UI for the specified layer
    /// </summary>
    private GameObject CreateSlotUI(ClothingLayer layer)
    {
        GameObject slotObj;

        if (slotUIPrefab != null)
        {
            slotObj = Instantiate(slotUIPrefab, slotContainer);
        }
        else
        {
            slotObj = CreateDefaultSlotUI(layer);
        }

        // Configure the slot UI
        var slotUI = slotObj.GetComponent<ClothingSlotUI>();
        if (slotUI == null)
        {
            slotUI = slotObj.AddComponent<ClothingSlotUI>();
        }

        slotUI.Initialize(layer);
        slotObj.name = $"ClothingSlot_{layer}";

        return slotObj;
    }

    /// <summary>
    /// Create a default slot UI when no prefab is provided
    /// </summary>
    private GameObject CreateDefaultSlotUI(ClothingLayer layer)
    {
        GameObject slotObj = new GameObject($"ClothingSlot_{layer}");
        slotObj.transform.SetParent(slotContainer, false);

        // Add RectTransform
        var rectTransform = slotObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(60f, 60f);

        // Add background image
        var backgroundImage = slotObj.AddComponent<UnityEngine.UI.Image>();
        backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Add outline
        var outline = slotObj.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(1, 1);

        return slotObj;
    }

    #region Event Handlers

    /// <summary>
    /// Handle item equipped to clothing slot
    /// </summary>
    private void OnItemEquipped(ClothingSlot slot, InventoryItemData item)
    {
        var slotUI = GetSlotUI(slot.layer);
        if (slotUI != null)
        {
            slotUI.RefreshDisplay();
        }

        Debug.Log($"[ClothingUI] Item {item.ItemData?.itemName} equipped to {slot.layer}");
    }

    /// <summary>
    /// Handle item unequipped from clothing slot
    /// </summary>
    private void OnItemUnequipped(ClothingSlot slot, string itemId)
    {
        var slotUI = GetSlotUI(slot.layer);
        if (slotUI != null)
        {
            slotUI.RefreshDisplay();
        }

        Debug.Log($"[ClothingUI] Item {itemId} unequipped from {slot.layer}");
    }

    /// <summary>
    /// Handle clothing condition changes
    /// </summary>
    private void OnClothingConditionChanged(string itemId, float newCondition)
    {
        // Find which slot this item is in and refresh its display
        foreach (var slotUI in slotUIs)
        {
            if (slotUI != null && slotUI.GetEquippedItemId() == itemId)
            {
                slotUI.RefreshDisplay();
                break;
            }
        }
    }

    /// <summary>
    /// Handle general clothing data changes
    /// </summary>
    private void OnClothingDataChanged()
    {
        RefreshAllSlotUIs();
    }

    /// <summary>
    /// Handle inventory opened (show clothing UI)
    /// </summary>
    private void OnInventoryOpened()
    {
        ShowClothingUI();
    }

    /// <summary>
    /// Handle inventory closed (hide clothing UI)
    /// </summary>
    private void OnInventoryClosed()
    {
        HideClothingUI();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Show the clothing UI panel
    /// </summary>
    public void ShowClothingUI()
    {
        if (clothingPanel != null)
        {
            clothingPanel.SetActive(true);
            RefreshAllSlotUIs();
        }
    }

    /// <summary>
    /// Hide the clothing UI panel
    /// </summary>
    public void HideClothingUI()
    {
        if (clothingPanel != null)
        {
            clothingPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Refresh all slot UI displays
    /// </summary>
    public void RefreshAllSlotUIs()
    {
        if (slotUIs == null) return;

        foreach (var slotUI in slotUIs)
        {
            if (slotUI != null)
            {
                slotUI.RefreshDisplay();
            }
        }
    }

    /// <summary>
    /// Get the slot UI for the specified layer
    /// </summary>
    public ClothingSlotUI GetSlotUI(ClothingLayer layer)
    {
        if (slotUIs == null) return null;

        foreach (var slotUI in slotUIs)
        {
            if (slotUI != null && slotUI.TargetLayer == layer)
            {
                return slotUI;
            }
        }

        return null;
    }

    /// <summary>
    /// Check if the clothing UI is currently visible
    /// </summary>
    public bool IsVisible => clothingPanel != null && clothingPanel.activeInHierarchy;

    #endregion

    #region Debug Methods

    [Button("Refresh All Slots")]
    private void DebugRefreshAllSlots()
    {
        RefreshAllSlotUIs();
    }

    [Button("Show Clothing Stats")]
    private void DebugShowClothingStats()
    {
        if (clothingManager == null) return;

        Debug.Log("=== CLOTHING STATS ===");
        Debug.Log($"Total Defense: {clothingManager.GetTotalDefense():F1}");
        Debug.Log($"Total Warmth: {clothingManager.GetTotalWarmth():F1}");
        Debug.Log($"Total Rain Resistance: {clothingManager.GetTotalRainResistance():F1}");
    }

    [Button("Force Create Slot UIs")]
    private void DebugForceCreateSlotUIs()
    {
        // Clear existing slot UIs
        if (slotContainer != null)
        {
            foreach (Transform child in slotContainer)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        CreateSlotUIs();
    }

    #endregion
}