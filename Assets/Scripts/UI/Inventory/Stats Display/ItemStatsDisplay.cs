using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// Displays detailed stats for selected inventory items
/// Shows stats when items are clicked, hovered, or being dragged
/// FIXED: Better timing for drag handler registration and proper initialization
/// </summary>
public class ItemStatsDisplay : MonoBehaviour
{
    [Header("UI References - Assign from your prefab")]
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;
    [SerializeField] private TextMeshProUGUI itemStatsText;
    [SerializeField] private Image itemIconImage;
    [SerializeField] private GameObject noItemSelectedPanel;

    [Header("Styling")]
    [SerializeField] private Color defaultTextColor = Color.white;
    [SerializeField] private Color statValueColor = Color.cyan;
    [SerializeField] private Color degradationGoodColor = Color.green;
    [SerializeField] private Color degradationFairColor = Color.yellow;
    [SerializeField] private Color degradationPoorColor = new Color(195, 165, 0); //orange
    [SerializeField] private Color degradationCriticalColor = Color.red;

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.15f;

    [Header("Debug/Testing")]
    [SerializeField] private ItemData testItem;

    private InventoryItemData currentDisplayedItem;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private bool isVisible = false;
    private bool hasInitialized = false;

    // Events
    public System.Action<InventoryItemData> OnItemDisplayed;
    public System.Action OnItemCleared;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // IMPORTANT: Keep GameObject active so child panels can be controlled
        gameObject.SetActive(true);

        // Initialize child panels properly
        InitializeChildPanels();

        // Start with "no item selected" state
        SetVisible(false, true);
    }

    /// <summary>
    /// Initialize child panels and ensure they're properly set up
    /// </summary>
    private void InitializeChildPanels()
    {
        // Make sure child panels are properly initialized
        if (statsPanel != null)
        {
            statsPanel.SetActive(false);
        }

        if (noItemSelectedPanel != null)
        {
            noItemSelectedPanel.SetActive(true);
        }

        // If panels aren't assigned, try to find them automatically
        if (statsPanel == null)
        {
            statsPanel = transform.Find("StatsPanel")?.gameObject;
            if (statsPanel != null)
            {
                Debug.Log("[ItemStatsDisplay] Auto-found StatsPanel");
            }
        }

        if (noItemSelectedPanel == null)
        {
            noItemSelectedPanel = transform.Find("NoItemSelectedPanel")?.gameObject;
            if (noItemSelectedPanel != null)
            {
                Debug.Log("[ItemStatsDisplay] Auto-found NoItemSelectedPanel");
            }
        }
    }

    private void Start()
    {
        // Setup initial UI state first
        SetupInitialUI();

        // Subscribe to inventory system events
        SubscribeToInventoryEvents();

        // FIXED: Use a more robust registration approach
        StartCoroutine(DelayedInitialization());
    }

    /// <summary>
    /// FIXED: Better initialization timing that waits for inventory system to be ready
    /// </summary>
    private System.Collections.IEnumerator DelayedInitialization()
    {
        // Wait for inventory manager to be fully ready
        while (PlayerInventoryManager.Instance == null)
        {
            yield return new WaitForEndOfFrame();
        }

        // Wait an additional frame for inventory UI to initialize
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        // Now try to register existing drag handlers
        RegisterExistingDragHandlers();

        // Set up event listeners for new drag handlers being created
        SetupDragHandlerCreationListeners();

        hasInitialized = true;
        // Debug.Log("[ItemStatsDisplay] Initialization complete");
    }

    /// <summary>
    /// FIXED: Better event listening for when new drag handlers are created
    /// </summary>
    private void SetupDragHandlerCreationListeners()
    {
        if (PlayerInventoryManager.Instance != null)
        {
            // Listen for when items are added to inventory (which creates new drag handlers)
            PlayerInventoryManager.Instance.OnItemAdded += OnInventoryItemAdded;
        }
    }

    /// <summary>
    /// FIXED: When a new item is added, register its drag handler after a small delay
    /// Added null check to prevent MissingReferenceException
    /// </summary>
    private void OnInventoryItemAdded(InventoryItemData item)
    {
        // Null check to prevent MissingReferenceException during scene transitions
        if (this == null || gameObject == null)
        {
            // Debug.LogWarning("[ItemStatsDisplay] OnInventoryItemAdded called on destroyed object - unsubscribing");
            if (PlayerInventoryManager.Instance != null)
            {
                PlayerInventoryManager.Instance.OnItemAdded -= OnInventoryItemAdded;
            }
            return;
        }

        // Only try to register if GameObject is active
        if (gameObject.activeInHierarchy)
        {
            // Give the visual system time to create the drag handler
            StartCoroutine(RegisterNewDragHandlerAfterDelay(item.ID));
        }
        // else
        // {
        //     Debug.Log($"[ItemStatsDisplay] Item {item.ID} added but GameObject inactive - will register when activated");
        // }
    }

    private System.Collections.IEnumerator RegisterNewDragHandlerAfterDelay(string itemId)
    {
        // Wait a few frames for the visual to be created
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForEndOfFrame();
        }

        // Find and register the new drag handler
        var dragHandlers = FindObjectsByType<PlayerInventoryItemDragHandler>(FindObjectsSortMode.None);
        foreach (var handler in dragHandlers)
        {
            if (handler.GetComponent<InventoryItemVisualRenderer>()?.ItemData?.ID == itemId)
            {
                RegisterDragHandler(handler);
                //Debug.Log($"[ItemStatsDisplay] Auto-registered new drag handler for item {itemId}");
                break;
            }
        }
    }

    private void SubscribeToInventoryEvents()
    {
        // Subscribe to inventory manager events
        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemRemoved += OnInventoryItemRemoved;
            PlayerInventoryManager.Instance.OnInventoryCleared += OnInventoryCleared;
        }

        // Subscribe to game events
        GameEvents.OnInventoryClosed += OnInventoryClosed;
        GameEvents.OnInventoryOpened += OnInventoryOpened; // NEW: Listen for inventory opening
    }

    private void SetupInitialUI()
    {
        // Ensure child panels are in correct state
        ShowNoItemSelected();

        // FIXED: Make sure the main GameObject is visible and the "no item selected" panel is shown
        SetVisible(true, true);

        // Debug.Log("[ItemStatsDisplay] Initial UI setup complete - showing 'no item selected' state");
    }

    /// <summary>
    /// FIXED: When inventory opens, check if GameObject is active before starting coroutines
    /// </summary>
    private void OnInventoryOpened()
    {
        // Since this is called via events, the GameObject might not be active yet
        // We need to delay the initialization until the GameObject becomes active
        if (!gameObject.activeInHierarchy)
        {
            // Debug.Log("[ItemStatsDisplay] GameObject not active yet - will initialize when enabled");
            return; // OnEnable will handle initialization when the GameObject becomes active
        }

        PerformInventoryOpenedActions();
    }

    /// <summary>
    /// Perform the actual inventory opened logic
    /// </summary>
    private void PerformInventoryOpenedActions()
    {
        if (!hasInitialized)
        {
            StartCoroutine(DelayedInitialization());
        }
        else
        {
            // Re-register in case we missed any
            StartCoroutine(RegisterExistingDragHandlersCoroutine());
        }

        // Make sure we're visible when inventory opens
        SetVisible(true);
        ShowNoItemSelected(); // Start with "no item selected" state
    }

    /// <summary>
    /// Automatically register with any existing drag handlers in the scene
    /// FIXED: Better error handling and logging
    /// </summary>
    private void RegisterExistingDragHandlers()
    {
        StartCoroutine(RegisterExistingDragHandlersCoroutine());
    }

    private System.Collections.IEnumerator RegisterExistingDragHandlersCoroutine()
    {
        // Wait for inventory items to be fully set up
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        var dragHandlers = FindObjectsByType<PlayerInventoryItemDragHandler>(FindObjectsSortMode.None);

        int registeredCount = 0;
        foreach (var handler in dragHandlers)
        {
            if (handler != null)
            {
                RegisterDragHandler(handler);
                registeredCount++;
            }
        }

        // Debug.Log($"[ItemStatsDisplay] Registered {registeredCount} existing drag handlers");

        // if (registeredCount == 0)
        // {
        //     Debug.Log("[ItemStatsDisplay] No existing drag handlers found - will register new ones as they're created");
        // }
    }

    /// <summary>
    /// Display stats for the specified item
    /// Called when an item is clicked, hovered, or being dragged
    /// </summary>
    public void DisplayItemStats(InventoryItemData itemData)
    {
        if (itemData?.ItemData == null)
        {
            ClearDisplay();
            return;
        }

        currentDisplayedItem = itemData;

        // Update item name
        if (itemNameText != null)
        {
            itemNameText.text = itemData.ItemData.itemName;
            itemNameText.color = GetItemNameColor(itemData.ItemData);
        }

        // Update item description
        if (itemDescriptionText != null)
        {
            itemDescriptionText.text = itemData.ItemData.description;
        }

        // Update item icon
        if (itemIconImage != null)
        {
            itemIconImage.sprite = itemData.ItemData.itemSprite;
            itemIconImage.gameObject.SetActive(itemData.ItemData.itemSprite != null);
        }

        // Update item stats
        if (itemStatsText != null)
        {
            itemStatsText.text = ItemStatsFormatter.FormatItemStats(itemData.ItemData);
        }

        // Show the stats panel
        ShowStatsPanel();
        SetVisible(true);

        // Fire event
        OnItemDisplayed?.Invoke(itemData);

        //  Debug.Log($"[ItemStatsDisplay] Displaying stats for: {itemData.ItemData.itemName}");
    }

    /// <summary>
    /// Clear the stats display
    /// </summary>
    public void ClearDisplay()
    {
        currentDisplayedItem = null;
        ShowNoItemSelected();
        SetVisible(true); // Still show the panel, but with "no item selected"

        OnItemCleared?.Invoke();
    }

    /// <summary>
    /// Hide the entire stats display
    /// FIXED: Never deactivate the GameObject - just make it transparent
    /// </summary>
    public void HideDisplay()
    {
        currentDisplayedItem = null;
        // Don't actually hide - just make transparent and show "no item selected"
        ShowNoItemSelected();
        SetVisible(true); // Keep it visible but with "no item selected"
    }

    private void ShowStatsPanel()
    {
        if (statsPanel != null)
        {
            statsPanel.SetActive(true);
        }

        if (noItemSelectedPanel != null)
        {
            noItemSelectedPanel.SetActive(false);
        }
    }

    private void ShowNoItemSelected()
    {
        if (statsPanel != null)
        {
            statsPanel.SetActive(false);
        }

        if (noItemSelectedPanel != null)
        {
            noItemSelectedPanel.SetActive(true);
        }
    }

    private void SetVisible(bool visible, bool immediate = false)
    {
        // Don't try to change visibility if GameObject isn't active
        if (!gameObject.activeInHierarchy)
        {
            //  Debug.Log("[ItemStatsDisplay] SetVisible called but GameObject inactive - ignoring");
            return;
        }

        if (isVisible == visible && !immediate) return;

        isVisible = visible;

        if (immediate)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        else
        {
            if (visible)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.DOFade(1f, fadeInDuration);
            }
            else
            {
                canvasGroup.DOFade(0f, fadeOutDuration)
                    .OnComplete(() =>
                    {
                        if (gameObject.activeInHierarchy) // Safety check
                        {
                            canvasGroup.interactable = false;
                            canvasGroup.blocksRaycasts = false;
                        }
                    });
            }
        }
    }

    private Color GetItemNameColor(ItemData itemData)
    {
        // You can customize this based on item rarity, condition, etc.
        return defaultTextColor;
    }

    /// <summary>
    /// Register a drag handler to send events to this stats display
    /// FIXED: Better validation and error handling
    /// </summary>
    public void RegisterDragHandler(BaseInventoryDragHandler dragHandler)
    {
        if (dragHandler != null)
        {
            // Unregister first to prevent duplicate registrations
            dragHandler.OnItemSelected -= DisplayItemStats;
            dragHandler.OnItemDeselected -= ClearDisplay;

            // Then register
            dragHandler.OnItemSelected += DisplayItemStats;
            dragHandler.OnItemDeselected += ClearDisplay;

            //            Debug.Log($"[ItemStatsDisplay] Registered drag handler for item");
        }
    }

    /// <summary>
    /// Unregister a drag handler
    /// </summary>
    public void UnregisterDragHandler(BaseInventoryDragHandler dragHandler)
    {
        if (dragHandler != null)
        {
            dragHandler.OnItemSelected -= DisplayItemStats;
            dragHandler.OnItemDeselected -= ClearDisplay;
        }
    }

    /// <summary>
    /// Check if an item is currently being displayed
    /// </summary>
    public bool IsDisplayingItem(InventoryItemData itemData)
    {
        return currentDisplayedItem == itemData;
    }

    /// <summary>
    /// Get the currently displayed item
    /// </summary>
    public InventoryItemData GetCurrentDisplayedItem()
    {
        return currentDisplayedItem;
    }

    /// <summary>
    /// Update the display if the current item's data has changed
    /// </summary>
    public void RefreshCurrentItem()
    {
        if (currentDisplayedItem != null)
        {
            DisplayItemStats(currentDisplayedItem);
        }
    }

    /// <summary>
    /// Static reference for easy access from other systems
    /// </summary>
    public static ItemStatsDisplay Instance { get; private set; }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        // IMPORTANT: Handle initialization when GameObject becomes active
        // This covers the case where OnInventoryOpened was called before GameObject was active
        if (hasInitialized)
        {
            // If we're already initialized, just refresh the display
            ShowNoItemSelected();
            SetVisible(true);
        }
        else
        {
            // If not initialized yet, do a quick check if we should initialize now
            if (gameObject.activeInHierarchy)
            {
                // Start initialization process when GameObject becomes active
                StartCoroutine(OnEnableInitialization());
            }
        }
    }

    /// <summary>
    /// Handle initialization when OnEnable is called (GameObject becomes active)
    /// </summary>
    private System.Collections.IEnumerator OnEnableInitialization()
    {
        // Wait a frame to ensure everything is properly activated
        yield return new WaitForEndOfFrame();

        // Check if we should perform inventory opened actions
        if (GameManager.Instance?.uiManager?.isInventoryOpen == true)
        {
            PerformInventoryOpenedActions();
        }
        else if (!hasInitialized)
        {
            // Still do basic initialization even if inventory isn't open
            StartCoroutine(DelayedInitialization());
        }
    }

    private void OnDisable()
    {
        // Also unsubscribe when disabled (before destruction)
        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemAdded -= OnInventoryItemAdded;
            // Debug.Log("[ItemStatsDisplay] Unsubscribed from InventoryManager.OnItemAdded in OnDisable");
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Handle when an item is removed from inventory
    /// </summary>
    private void OnInventoryItemRemoved(string itemId)
    {
        if (currentDisplayedItem != null && currentDisplayedItem.ID == itemId)
        {
            ClearDisplay();
        }
    }

    /// <summary>
    /// Handle when inventory is cleared
    /// </summary>
    private void OnInventoryCleared()
    {
        ClearDisplay();
    }

    /// <summary>
    /// Handle when inventory UI is closed
    /// FIXED: Just clear the current item since GameObject will be deactivated anyway
    /// </summary>
    private void OnInventoryClosed()
    {
        // Clear the current item
        currentDisplayedItem = null;
        // Don't try to do UI updates since the GameObject will be deactivated
        //  Debug.Log("[ItemStatsDisplay] Inventory closed - cleared current item");
    }

    private void OnDestroy()
    {
        // Clean up any lingering tweens
        DOTween.Kill(this);

        // CRITICAL: Unsubscribe from all events to prevent MissingReferenceException
        UnsubscribeFromAllEvents();

        // Clear static instance if this was it
        if (Instance == this)
        {
            Instance = null;
        }

        //      Debug.Log("[ItemStatsDisplay] OnDestroy called - all events unsubscribed");
    }

    /// <summary>
    /// Comprehensive event cleanup to prevent MissingReferenceException during scene transitions
    /// </summary>
    private void UnsubscribeFromAllEvents()
    {
        // Unsubscribe from InventoryManager events (persistent across scenes)
        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemAdded -= OnInventoryItemAdded;
            PlayerInventoryManager.Instance.OnItemRemoved -= OnInventoryItemRemoved;
            PlayerInventoryManager.Instance.OnInventoryCleared -= OnInventoryCleared;
            //            Debug.Log("[ItemStatsDisplay] Unsubscribed from InventoryManager events");
        }

        // Unsubscribe from GameEvents (static events)
        GameEvents.OnInventoryClosed -= OnInventoryClosed;
        GameEvents.OnInventoryOpened -= OnInventoryOpened;
        //    Debug.Log("[ItemStatsDisplay] Unsubscribed from GameEvents");

        // Unsubscribe from any drag handlers that might still reference this
        var dragHandlers = FindObjectsByType<PlayerInventoryItemDragHandler>(FindObjectsSortMode.None);
        foreach (var handler in dragHandlers)
        {
            if (handler != null)
            {
                handler.OnItemSelected -= DisplayItemStats;
                handler.OnItemDeselected -= ClearDisplay;
            }
        }
        //  Debug.Log($"[ItemStatsDisplay] Unsubscribed from {dragHandlers.Length} drag handlers");
    }

    #region Setup and Testing Methods

    /// <summary>
    /// Test button to preview how stats will look
    /// </summary>
    [Button("Test Stats Display")]
    private void TestStatsDisplay()
    {
        if (testItem == null)
        {
            Debug.LogError("No test item assigned! Drag an ItemData into the Test Item field.");
            return;
        }

        var testInventoryItem = new InventoryItemData("test_item", testItem, Vector2Int.zero);
        DisplayItemStats(testInventoryItem);
        // Debug.Log($"Displaying stats for: {testItem.itemName}");
    }

    [Button("Clear Display")]
    private void TestClearDisplay()
    {
        ClearDisplay();
        Debug.Log("Stats display cleared");
    }

    [Button("Hide Display")]
    private void TestHideDisplay()
    {
        HideDisplay();
        Debug.Log("Stats display hidden");
    }

    /// <summary>
    /// IMPROVED: Better manual registration with more detailed feedback
    /// </summary>
    [Button("Re-Register All Drag Handlers")]
    public void RegisterAllDragHandlers()
    {
        var dragHandlers = FindObjectsByType<PlayerInventoryItemDragHandler>(FindObjectsSortMode.None);

        int registeredCount = 0;
        foreach (var handler in dragHandlers)
        {
            if (handler != null)
            {
                RegisterDragHandler(handler);
                registeredCount++;
            }
        }

        Debug.Log($"[ItemStatsDisplay] Manually registered {registeredCount} drag handlers");

        if (registeredCount == 0)
        {
            Debug.LogWarning("No drag handlers found in scene. Make sure your inventory items have InventoryItemDragHandler components.");

            // Check if inventory is even open
            if (GameManager.Instance?.uiManager?.isInventoryOpen == false)
            {
                Debug.LogWarning("Inventory appears to be closed. Try opening the inventory first.");
            }
        }
        else
        {
            // Show the panel after successful registration
            SetVisible(true);
        }
    }

    /// <summary>
    /// Static method to auto-register new drag handlers as they're created
    /// </summary>
    public static void AutoRegisterNewDragHandler(BaseInventoryDragHandler dragHandler)
    {
        if (Instance != null)
        {
            Instance.RegisterDragHandler(dragHandler);
        }
    }

    /// <summary>
    /// Validate that all UI references are properly assigned
    /// </summary>
    [Button("Validate UI References")]
    private void ValidateUIReferences()
    {
        bool allValid = true;

        Debug.Log("=== ItemStatsDisplay UI Validation ===");

        // Check main GameObject
        Debug.Log($"Main GameObject active: {gameObject.activeInHierarchy}");
        Debug.Log($"CanvasGroup present: {canvasGroup != null}");
        Debug.Log($"CanvasGroup alpha: {canvasGroup?.alpha ?? -1}");

        if (statsPanel == null)
        {
            Debug.LogError("Stats Panel is not assigned!");
            allValid = false;
        }
        else
        {
            Debug.Log($"Stats Panel: {statsPanel.name} (Active: {statsPanel.activeInHierarchy})");
        }

        if (noItemSelectedPanel == null)
        {
            Debug.LogError("No Item Selected Panel is not assigned!");
            allValid = false;
        }
        else
        {
            Debug.Log($"No Item Selected Panel: {noItemSelectedPanel.name} (Active: {noItemSelectedPanel.activeInHierarchy})");
        }

        if (itemNameText == null)
        {
            Debug.LogError("Item Name Text is not assigned!");
            allValid = false;
        }

        if (itemDescriptionText == null)
        {
            Debug.LogError("Item Description Text is not assigned!");
            allValid = false;
        }

        if (itemStatsText == null)
        {
            Debug.LogError("Item Stats Text is not assigned!");
            allValid = false;
        }

        if (itemIconImage == null)
        {
            Debug.LogWarning("Item Icon Image is not assigned (optional)");
        }

        // Check child hierarchy
        Debug.Log("=== Child Hierarchy ===");
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            Debug.Log($"Child {i}: {child.name} (Active: {child.gameObject.activeInHierarchy})");
        }

        if (allValid)
        {
            Debug.Log("✓ All essential UI references are properly assigned!");
        }

        Debug.Log("=== End Validation ===");
    }

    #endregion
}