using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// UI controller for the pickup overflow system.
/// Provides a two-panel interface: pickup item on the left, player inventory on the right.
/// Allows players to reorganize their inventory and transfer the pickup item when space is available.
/// UPDATED: Now responds to interact input ("E") to open/close with cooldown protection
/// </summary>
public class PickupOverflowUI : MonoBehaviour
{
    [Header("UI Panel References")]
    [SerializeField] private GameObject overflowUIPanel;
    [SerializeField] private Transform pickupGridParent;
    [SerializeField] private Transform playerGridParent;

    [Header("UI Text Elements")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI itemNameText;

    [Header("UI Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button quickTransferButton;
    [SerializeField] private Button dropItemButton;

    [Header("Grid Visual Prefabs")]
    [SerializeField] private GameObject pickupGridVisualPrefab;
    [SerializeField] private GameObject playerGridVisualPrefab;

    [Header("Animation Settings")]
    [SerializeField] private float openAnimationDuration = 0.3f;
    [SerializeField] private float closeAnimationDuration = 0.2f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("Interaction Settings")]
    [SerializeField] private float maxInteractionDistance = 1f; // Auto-close if player moves this far away

    [SerializeField] private float inputCooldown = 0.2f; // Cooldown after opening before allowing close

    [Header("Layout Settings")]
    [SerializeField] private float panelSpacing = 20f;
    [SerializeField] private Vector2 minPickupPanelSize = new Vector2(100f, 100f);

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Component references
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    // Grid visual instances
    private PickupOverflowGridVisual pickupGridVisual;
    private PlayerInventoryGridVisual playerGridVisual;

    // Managers
    private PickupOverflowManager pickupManager;

    // Current item
    private ItemData currentPickupItemData;
    private Vector3 pickupItemWorldPosition; // NEW: Track where the item was in the world


    // UI state
    private bool isOpen = false;
    private float openTime = -999f; // Time when UI was opened

    // Player controller reference (for interaction cooldown)
    private PlayerController playerController;

    // Events
    public System.Action<ItemData> OnPickupOverflowOpened;
    public System.Action<ItemData, bool> OnPickupOverflowClosed; // bool: was transferred
    public System.Action<ItemData> OnPickupItemTransferred;
    public System.Action<ItemData> OnPickupItemDropped;

    // Static instance for easy access
    public static PickupOverflowUI Instance { get; private set; }

    #region Lifecycle

    private void Awake()
    {
        SetupSingleton();
        InitializeComponents();
        SetupEventListeners();
        CreateGridVisuals();
        CreatePickupManager();

        // Start closed
        SetUIActive(false, true);
    }

    private void Start()
    {
        // NEW: Subscribe to interact input for closing
        ConnectToInputManager();
    }

    private void OnDestroy()
    {
        CleanupEventListeners();
        DisconnectFromInputManager();
        DOTween.Kill(this);

        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Set up singleton pattern.
    /// </summary>
    private void SetupSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            DebugLogWarning("Multiple PickupOverflowUI instances found - destroying duplicate");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Initialize core components.
    /// </summary>
    private void InitializeComponents()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (overflowUIPanel == null)
        {
            overflowUIPanel = gameObject;
        }

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
            if (playerController == null)
            {
                DebugLogWarning("No PlayerController found in scene");
            }
        }


        DebugLog("Pickup overflow UI components initialized");
    }

    /// <summary>
    /// Set up button event listeners.
    /// </summary>
    private void SetupEventListeners()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseWithoutTransfer);
        }

        if (quickTransferButton != null)
        {
            quickTransferButton.onClick.AddListener(TryQuickTransfer);
        }

        if (dropItemButton != null)
        {
            dropItemButton.onClick.AddListener(DropPickupItem);
        }
    }

    /// <summary>
    /// Clean up event listeners.
    /// </summary>
    private void CleanupEventListeners()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseWithoutTransfer);
        }

        if (quickTransferButton != null)
        {
            quickTransferButton.onClick.RemoveListener(TryQuickTransfer);
        }

        if (dropItemButton != null)
        {
            dropItemButton.onClick.RemoveListener(DropPickupItem);
        }
    }

    /// <summary>
    /// Create grid visual instances.
    /// </summary>
    private void CreateGridVisuals()
    {
        CreatePickupGridVisual();
        CreatePlayerGridVisual();

        DebugLog("Grid visuals created");
    }

    /// <summary>
    /// Create the pickup grid visual.
    /// </summary>
    private void CreatePickupGridVisual()
    {
        GameObject pickupGridObj;

        if (pickupGridVisualPrefab != null)
        {
            pickupGridObj = Instantiate(pickupGridVisualPrefab, pickupGridParent);
        }
        else
        {
            // Create default pickup grid visual
            pickupGridObj = new GameObject("PickupGridVisual");
            pickupGridObj.transform.SetParent(pickupGridParent, false);
            pickupGridObj.AddComponent<RectTransform>();
            pickupGridObj.AddComponent<PickupOverflowGridVisual>();
        }

        pickupGridVisual = pickupGridObj.GetComponent<PickupOverflowGridVisual>();

        if (pickupGridVisual == null)
        {
            DebugLogError("Failed to get PickupOverflowGridVisual component");
        }
    }

    /// <summary>
    /// Create the player grid visual (separate instance from main inventory UI).
    /// </summary>
    private void CreatePlayerGridVisual()
    {
        GameObject playerGridObj;


        playerGridObj = Instantiate(playerGridVisualPrefab, playerGridParent);

        if (playerGridVisualPrefab == null)
            DebugLogError("Failed to instantiate player grid visual prefab as it is null");


        playerGridVisual = playerGridObj.GetComponent<PlayerInventoryGridVisual>();
        //playerGridVisual.isOpenedViaExternalInventory = true; // Mark as opened via external UI so the drop down menu's buttons shows transfer option

        if (playerGridVisual == null)
        {
            DebugLogError("Failed to get PlayerInventoryGridVisual component");
        }
    }

    /// <summary>
    /// Create the pickup overflow manager.
    /// </summary>
    private void CreatePickupManager()
    {
        GameObject managerObj = new GameObject("PickupOverflowManager");
        managerObj.transform.SetParent(transform, false);
        pickupManager = managerObj.AddComponent<PickupOverflowManager>();

        // Connect pickup grid visual to manager
        if (pickupGridVisual != null)
        {
            pickupGridVisual.ConnectToSpecificManager(pickupManager);
        }

        DebugLog("Pickup overflow manager created and connected");
    }

    #endregion

    #region NEW: Input Management

    /// <summary>
    /// NEW: Connect to InputManager for interact input
    /// </summary>
    private void ConnectToInputManager()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnInteractPressed -= HandleInteractInput;
            InputManager.Instance.OnInteractPressed += HandleInteractInput;
            DebugLog("Connected to InputManager for interact input");
        }
    }

    /// <summary>
    /// NEW: Disconnect from InputManager
    /// </summary>
    private void DisconnectFromInputManager()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnInteractPressed -= HandleInteractInput;
            DebugLog("Disconnected from InputManager");
        }
    }

    /// <summary>
    /// NEW: Handle interact input - close overflow if open
    /// </summary>
    private void HandleInteractInput()
    {
        // Early exit if not open
        if (!isOpen)
        {
            return;
        }

        // Check cooldown - prevents the opening input from immediately closing
        if (Time.time < openTime + inputCooldown)
        {
            DebugLog($"Close input blocked - cooldown active ({Time.time - openTime:F2}s / {inputCooldown}s)");
            return;
        }

        // Close the overflow UI
        DebugLog("Interact input received - closing pickup overflow UI");
        CloseWithoutTransfer();
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Show the pickup overflow UI for the specified item.
    /// </summary>
    public void ShowPickupOverflow(ItemData itemData)
    {
        if (itemData == null)
        {
            DebugLogError("Cannot show pickup overflow for null item");
            return;
        }

        if (isOpen)
        {
            DebugLog("Pickup overflow UI already open - closing previous and opening new");
            CloseWithoutTransfer();
        }

        currentPickupItemData = itemData;

        // Set the pickup item in the manager
        if (pickupManager.SetPickupItem(itemData))
        {
            // NEW: Store the player's position as the "item world position" since the item is about to be picked up
            // This allows us to check if player moves away from where they tried to pick up the item

            pickupItemWorldPosition = playerController.transform.position;

            // NEW: Set open state and time IMMEDIATELY (before animation)
            isOpen = true;
            openTime = Time.time;

            UpdateUI();
            ShowUI();

            DebugLog($"Opened pickup overflow UI for: {itemData.itemName} at time {openTime:F2}");
            OnPickupOverflowOpened?.Invoke(itemData);
        }
        else
        {
            DebugLogError($"Failed to set pickup item: {itemData.itemName}");
        }
    }

    /// <summary>
    /// Close the pickup overflow UI without transferring the item.
    /// </summary>
    public void CloseWithoutTransfer()
    {
        if (!isOpen) return;

        bool wasTransferred = false;
        var closingItem = currentPickupItemData;

        // NEW: Set closed state IMMEDIATELY (before animation)
        isOpen = false;

        // NEW: Trigger player interaction cooldown to prevent immediate re-open
        TriggerPlayerInteractionCooldown();

        // NEW: Clear distance tracking
        pickupItemWorldPosition = Vector3.zero;

        HideUI();
        CleanupPickupItem();

        DebugLog($"Closed pickup overflow UI without transfer: {closingItem?.itemName}");
        OnPickupOverflowClosed?.Invoke(closingItem, wasTransferred);
    }

    /// <summary>
    /// Called when pickup item is successfully transferred to player inventory.
    /// </summary>
    public void OnPickupTransferComplete()
    {
        if (!isOpen) return;

        bool wasTransferred = true;
        var transferredItem = currentPickupItemData;

        DebugLog($"Pickup item transferred: {transferredItem?.itemName}");

        // NEW: Set closed state IMMEDIATELY
        isOpen = false;

        // NEW: Trigger player interaction cooldown to prevent immediate re-open
        TriggerPlayerInteractionCooldown();

        HideUI();
        CleanupPickupItem();

        OnPickupItemTransferred?.Invoke(transferredItem);
        OnPickupOverflowClosed?.Invoke(transferredItem, wasTransferred);
    }

    /// <summary>
    /// Check if the pickup overflow UI is currently open.
    /// </summary>
    public bool IsOpen => isOpen;

    /// <summary>
    /// Get the current pickup item data.
    /// </summary>
    public ItemData GetCurrentPickupItem()
    {
        return currentPickupItemData;
    }

    #endregion

    #region UI Management

    /// <summary>
    /// Show the UI with animation.
    /// </summary>
    private void ShowUI()
    {
        SetUIActive(true, false);

        // Animate in
        canvasGroup.alpha = 0f;
        rectTransform.localScale = Vector3.zero;

        var sequence = DOTween.Sequence();
        sequence.Append(canvasGroup.DOFade(1f, openAnimationDuration));
        sequence.Join(rectTransform.DOScale(Vector3.one, openAnimationDuration).SetEase(openEase));
        sequence.SetTarget(this);

        DebugLog("Pickup overflow UI shown");
    }

    /// <summary>
    /// Hide the UI with animation.
    /// </summary>
    private void HideUI()
    {
        var sequence = DOTween.Sequence();
        sequence.Append(canvasGroup.DOFade(0f, closeAnimationDuration));
        sequence.Join(rectTransform.DOScale(Vector3.zero, closeAnimationDuration).SetEase(closeEase));
        sequence.OnComplete(() =>
        {
            if (this != null) // Safety check for scene transitions
            {
                SetUIActive(false, true);
            }
        });
        sequence.SetTarget(this);

        DebugLog("Pickup overflow UI hidden");
    }

    /// <summary>
    /// Set UI panel active state.
    /// </summary>
    private void SetUIActive(bool active, bool immediate = false)
    {
        if (overflowUIPanel != null)
        {
            overflowUIPanel.SetActive(active);
        }

        if (immediate)
        {
            canvasGroup.alpha = active ? 1f : 0f;
            rectTransform.localScale = active ? Vector3.one : Vector3.zero;
            canvasGroup.interactable = active;
            canvasGroup.blocksRaycasts = active;
        }
        else
        {
            canvasGroup.interactable = active;
            canvasGroup.blocksRaycasts = active;
        }
    }

    /// <summary>
    /// Update UI text and elements.
    /// </summary>
    private void UpdateUI()
    {
        UpdateTextElements();
        UpdateButtonStates();
    }

    /// <summary>
    /// Update text elements with current item information.
    /// </summary>
    private void UpdateTextElements()
    {
        if (titleText != null)
        {
            titleText.text = "Inventory Full";
        }

        if (instructionText != null)
        {
            instructionText.text = "Make space in your inventory and drag the item to transfer it, or drop it to leave it behind.";
        }

        if (itemNameText != null && currentPickupItemData != null)
        {
            itemNameText.text = currentPickupItemData.itemName;
        }
    }

    /// <summary>
    /// Update button states based on current situation.
    /// </summary>
    private void UpdateButtonStates()
    {
        // Quick transfer button - enable if player has space
        if (quickTransferButton != null)
        {
            bool hasSpace = PlayerInventoryManager.Instance?.HasSpaceForItem(currentPickupItemData) ?? false;
            quickTransferButton.interactable = hasSpace;

            var buttonText = quickTransferButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = hasSpace ? "Quick Transfer" : "No Space";
            }
        }

        // Drop button - enable if item can be dropped
        if (dropItemButton != null)
        {
            bool canDrop = currentPickupItemData?.CanDrop ?? false;
            dropItemButton.interactable = canDrop;

            var buttonText = dropItemButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = canDrop ? "Drop Item" : "Cannot Drop";
            }
        }
    }

    /// <summary>
    /// Clean up pickup item data.
    /// </summary>
    private void CleanupPickupItem()
    {
        if (pickupManager != null)
        {
            pickupManager.ClearPickupItem();
        }

        currentPickupItemData = null;
    }

    #endregion

    #region Button Actions

    /// <summary>
    /// Try to quickly transfer the pickup item to player inventory.
    /// </summary>
    private void TryQuickTransfer()
    {
        if (pickupManager == null)
        {
            DebugLogError("No pickup manager - cannot transfer");
            return;
        }

        bool success = pickupManager.TryTransferToPlayer();

        if (success)
        {
            DebugLog("Quick transfer successful");
            OnPickupTransferComplete();
        }
        else
        {
            DebugLog("Quick transfer failed - updating button states");
            UpdateButtonStates(); // Refresh button states in case space availability changed
        }
    }

    /// <summary>
    /// Drop the pickup item back into the world.
    /// </summary>
    private void DropPickupItem()
    {
        if (currentPickupItemData == null)
        {
            DebugLogError("No pickup item to drop");
            return;
        }

        if (!currentPickupItemData.CanDrop)
        {
            DebugLogWarning($"Cannot drop {currentPickupItemData.itemName} - it's a key item");
            return;
        }

        DebugLog($"Dropping pickup item: {currentPickupItemData.itemName}");

        // Use ItemDropSystem to drop the item back into the world
        if (ItemDropSystem.Instance != null)
        {
            bool success = ItemDropSystem.Instance.DropItem(currentPickupItemData);

            if (success)
            {
                var droppedItem = currentPickupItemData;

                // NEW: Set closed state IMMEDIATELY
                isOpen = false;

                // NEW: Trigger player interaction cooldown to prevent immediate re-open
                TriggerPlayerInteractionCooldown();

                // Close the UI
                HideUI();
                CleanupPickupItem();

                DebugLog($"Successfully dropped {droppedItem.itemName} into world");
                OnPickupItemDropped?.Invoke(droppedItem);
                OnPickupOverflowClosed?.Invoke(droppedItem, false);
            }
            else
            {
                DebugLogError($"Failed to drop {currentPickupItemData.itemName} into world");
            }
        }
        else
        {
            DebugLogError("ItemDropSystem not found - cannot drop item");
        }
    }

    /// <summary>
    /// NEW: Trigger the player interaction controller's cooldown when UI closes
    /// This prevents immediately re-opening after closing
    /// </summary>
    private void TriggerPlayerInteractionCooldown()
    {
        if (playerController != null && playerController.interactionController != null)
        {
            // Trigger the cooldown to prevent immediate re-interaction
            playerController.interactionController.TriggerInteractionCooldown();
            DebugLog("Triggered player interaction cooldown to prevent immediate re-open");
        }
    }

    #endregion

    #region Input Handling

    private void Update()
    {
        // NEW: Check distance if UI is open
        if (isOpen)
        {
            CheckDistanceToPickupLocation();
            UpdateButtonStates();
        }
    }

    /// <summary>
    /// NEW: Check distance to pickup location and auto-close if too far
    /// </summary>
    private void CheckDistanceToPickupLocation()
    {
        // Calculate distance from where player tried to pick up the item
        float distance = Vector3.Distance(playerController.transform.position, pickupItemWorldPosition);

        // Check if beyond max distance - auto-close
        if (distance > maxInteractionDistance)
        {
            DebugLog($"Player too far from pickup location ({distance:F2}m > {maxInteractionDistance}m) - auto-closing");
            CloseWithoutTransfer();
            return;
        }
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Show pickup overflow UI using the static instance.
    /// </summary>
    public static void ShowPickupOverflowStatic(ItemData itemData)
    {
        if (Instance != null)
        {
            Instance.ShowPickupOverflow(itemData);
        }
        else
        {
            Debug.LogError("No PickupOverflowUI instance available");
        }
    }

    /// <summary>
    /// Close pickup overflow UI using the static instance.
    /// </summary>
    public static void ClosePickupOverflowStatic()
    {
        if (Instance != null)
        {
            Instance.CloseWithoutTransfer();
        }
    }

    /// <summary>
    /// Check if pickup overflow UI is open using the static instance.
    /// </summary>
    public static bool IsPickupOverflowOpen()
    {
        return Instance != null && Instance.IsOpen;
    }

    #endregion

    #region Debug Methods

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PickupOverflowUI] {message}");
    }

    private void DebugLogError(string message)
    {
        Debug.LogError($"[PickupOverflowUI] {message}");
    }

    private void DebugLogWarning(string message)
    {
        if (enableDebugLogs)
            Debug.LogWarning($"[PickupOverflowUI] {message}");
    }

    #endregion
}