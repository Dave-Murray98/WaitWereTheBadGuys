using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// Complete UI controller for storage container interface.
/// Manages both the container inventory grid and a separate player inventory grid,
/// allowing seamless item transfer between them. Provides a clean, user-friendly
/// interface for storage container interactions.
/// UPDATED: Now responds to interact input ("E") to open/close with cooldown protection
/// </summary>
public class StorageContainerUI : MonoBehaviour
{
    [Header("UI Panel References")]
    [SerializeField] private GameObject containerUIPanel;
    [SerializeField] private Transform containerGridParent;
    [SerializeField] private Transform playerGridParent;

    [Header("UI Text Elements")]
    [SerializeField] private TextMeshProUGUI containerTitleText;

    [Header("UI Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button transferAllButton;
    [SerializeField] private Button sortContainerButton;
    [SerializeField] private Button sortPlayerButton;

    [Header("Grid Visual Prefabs")]
    [SerializeField] private GameObject containerGridVisualPrefab;
    [SerializeField] private GameObject playerGridVisualPrefab;

    [Header("Animation Settings")]
    [SerializeField] private float openAnimationDuration = 0.3f;
    [SerializeField] private float closeAnimationDuration = 0.2f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("Interaction Settings")]
    [SerializeField] private float maxInteractionDistance = 2f; // Auto-close if player moves this far away
    [SerializeField] private float inputCooldown = 0.2f; // Cooldown after opening before allowing close

    [Header("Layout Settings")]
    [SerializeField] private float gridSpacing = 20f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Component references
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    // Grid visual instances
    private StorageContainerGridVisual containerGridVisual;
    private PlayerInventoryGridVisual playerGridVisual;

    // Current container
    public StorageContainer currentContainer;

    //playerController reference
    private PlayerController playerController;

    // UI state
    private bool isOpen = false;
    private float openTime = -999f; // Time when UI was opened

    // Events
    public System.Action<StorageContainer> OnContainerUIOpened;
    public System.Action<StorageContainer> OnContainerUIClosed;

    // Static instance
    public static StorageContainerUI Instance { get; private set; }

    #region Lifecycle

    private void Awake()
    {
        SetupSingleton();
        InitializeComponents();
        SetupEventListeners();
        CreateGridVisuals();

        // Start closed
        SetUIActive(false, true);
    }

    private void Start()
    {
        // Subscribe to interact input for closing
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
            DebugLogWarning("Multiple StorageContainerUI instances found - destroying duplicate");
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

        if (containerUIPanel == null)
        {
            containerUIPanel = gameObject;
        }

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
            if (playerController == null)
            {
                DebugLogWarning("No PlayerController found in scene");
            }
        }

        DebugLog("Storage container UI components initialized");
    }

    /// <summary>
    /// Set up button event listeners.
    /// </summary>
    private void SetupEventListeners()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseContainer);
        }

        if (transferAllButton != null)
        {
            transferAllButton.onClick.AddListener(TransferAllToPlayer);
        }

        if (sortContainerButton != null)
        {
            sortContainerButton.onClick.AddListener(SortContainer);
        }

        if (sortPlayerButton != null)
        {
            sortPlayerButton.onClick.AddListener(SortPlayerInventory);
        }
    }

    /// <summary>
    /// Clean up event listeners.
    /// </summary>
    private void CleanupEventListeners()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseContainer);
        }

        if (transferAllButton != null)
        {
            transferAllButton.onClick.RemoveListener(TransferAllToPlayer);
        }

        if (sortContainerButton != null)
        {
            sortContainerButton.onClick.RemoveListener(SortContainer);
        }

        if (sortPlayerButton != null)
        {
            sortPlayerButton.onClick.RemoveListener(SortPlayerInventory);
        }
    }

    /// <summary>
    /// Create grid visual instances for both container and player inventory.
    /// </summary>
    private void CreateGridVisuals()
    {
        CreateContainerGridVisual();
        CreatePlayerGridVisual();

        DebugLog("Grid visuals created");
    }

    /// <summary>
    /// Create the container grid visual.
    /// </summary>
    private void CreateContainerGridVisual()
    {
        GameObject containerGridObj;
        DebugLog("Creating container grid visual");

        if (containerGridVisualPrefab != null)
        {
            containerGridObj = Instantiate(containerGridVisualPrefab, containerGridParent);
            DebugLog("Instantiated container grid visual prefab");
        }
        else
        {
            DebugLog("Creating new object for default container grid visual");
            // Create default container grid visual
            containerGridObj = new GameObject("ContainerGridVisual");
            containerGridObj.transform.SetParent(containerGridParent, false);
            containerGridObj.AddComponent<RectTransform>();
            containerGridObj.AddComponent<StorageContainerGridVisual>();
        }

        containerGridVisual = containerGridObj.GetComponent<StorageContainerGridVisual>();

        if (containerGridVisual == null)
        {
            DebugLogError("Failed to get StorageContainerGridVisual component");
        }
    }

    /// <summary>
    /// Create the player grid visual (separate instance from main inventory UI).
    /// </summary>
    private void CreatePlayerGridVisual()
    {
        GameObject playerGridObj;

        // if (playerGridVisualPrefab != null)
        // {
        playerGridObj = Instantiate(playerGridVisualPrefab, playerGridParent);

        if (playerGridObj == null)
        {
            DebugLogError("Failed to instantiate player grid visual prefab as it is null");
            return;
        }

        playerGridVisual = playerGridObj.GetComponent<PlayerInventoryGridVisual>();

        // Set the flag indicating this grid visual is opened via an external inventory
        // so that each item's drop down menu can adjust accordingly (ie show transfer to storage container option)
        playerGridVisual.isOpenedViaExternalInventory = true;

        if (playerGridVisual == null)
        {
            DebugLogError("Failed to get InventoryGridVisual component");
        }
    }

    #endregion

    #region Input Management

    /// <summary>
    /// Connect to InputManager for interact input
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
    /// Disconnect from InputManager
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
    /// Handle interact input - close container if open
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

        // Close the container
        DebugLog("Interact input received - closing container UI");
        CloseContainer();
    }

    #endregion

    #region Container Management

    private void Update()
    {
        // Check distance if UI is open
        if (isOpen)
        {
            CheckDistanceToContainer();
        }
    }

    /// <summary>
    ///  Check distance to container and auto-close if too far
    /// </summary>
    private void CheckDistanceToContainer()
    {
        if (currentContainer == null)
        {
            return;
        }

        // Calculate distance
        float distance = Vector3.Distance(playerController.transform.position, currentContainer.transform.position);

        // Check if beyond max distance - auto-close
        if (distance > maxInteractionDistance)
        {
            DebugLog($"Player too far from container ({distance:F2}m > {maxInteractionDistance}m) - auto-closing");
            CloseContainer();
            return;
        }
    }

    /// <summary>
    /// Open the container UI for the specified container.
    /// </summary>
    public void OpenContainer(StorageContainer container)
    {
        if (container == null)
        {
            DebugLogError("Cannot open null container");
            return;
        }

        if (isOpen && currentContainer == container)
        {
            DebugLog("Container already open");
            return;
        }

        // Close current container if different
        if (isOpen && currentContainer != container)
        {
            CloseContainer();
        }

        currentContainer = container;

        // Set open state and time IMMEDIATELY (before animation)
        isOpen = true;
        openTime = Time.time;

        ConnectToContainer();
        UpdateUI();
        ShowUI();

        DebugLog($"Opened container UI for: {container.DisplayName} at time {openTime:F2}");
        OnContainerUIOpened?.Invoke(container);
    }

    /// <summary>
    /// Close the container UI.
    /// </summary>
    public void CloseContainer()
    {
        if (!isOpen)
        {
            return;
        }

        var closingContainer = currentContainer;

        // Set closed state IMMEDIATELY (before animation)
        isOpen = false;

        // Reset the player interaction cooldown to prevent immediate re-opening
        ResetPlayerInteractionCooldown();

        HideUI();
        DisconnectFromContainer();
        currentContainer = null;

        DebugLog($"Closed container UI for: {closingContainer?.DisplayName ?? "Unknown"}");
        OnContainerUIClosed?.Invoke(closingContainer);
    }

    /// <summary>
    /// Trigger the player interaction controller's cooldown when UI closes
    /// This prevents immediately re-opening the container after closing
    /// </summary>
    private void ResetPlayerInteractionCooldown()
    {
        // Find the player's interaction controller

        if (playerController != null && playerController.interactionController != null)
        {
            // Trigger the cooldown to prevent immediate re-interaction
            playerController.interactionController.TriggerInteractionCooldown();
            DebugLog("Triggered player interaction cooldown to prevent immediate re-open");
        }
    }

    /// <summary>
    /// Connect grid visuals to the current container.
    /// </summary>
    private void ConnectToContainer()
    {
        if (currentContainer == null) return;

        // Connect container grid visual
        if (containerGridVisual != null)
        {
            containerGridVisual.ConnectToSpecificContainer(currentContainer);
        }

        // Player grid visual should already be connected to player inventory
        // No additional connection needed

        DebugLog($"Connected to container: {currentContainer.DisplayName}");
    }

    /// <summary>
    /// Disconnect grid visuals from container.
    /// </summary>
    private void DisconnectFromContainer()
    {
        // Container grid visual will handle its own cleanup
        // Player grid visual remains connected to player inventory

        DebugLog("Disconnected from container");
    }

    #endregion

    #region UI Management

    /// <summary>
    /// Show the container UI with animation.
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

        DebugLog("Container UI shown");
    }

    /// <summary>
    /// Hide the container UI with animation.
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

        DebugLog("Container UI hidden");
    }

    /// <summary>
    /// Set UI panel active state.
    /// </summary>
    private void SetUIActive(bool active, bool immediate = false)
    {
        if (containerUIPanel != null)
        {
            containerUIPanel.SetActive(active);
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
        UpdateContainerInfo();
    }

    /// <summary>
    /// Update container title and information.
    /// </summary>
    private void UpdateContainerInfo()
    {
        if (containerTitleText != null && currentContainer != null)
        {
            containerTitleText.text = currentContainer.DisplayName;
        }
    }

    #endregion

    #region Transfer Operations

    /// <summary>
    /// Transfer all possible items from container to player inventory.
    /// </summary>
    private void TransferAllToPlayer()
    {
        if (currentContainer == null || PlayerInventoryManager.Instance == null)
        {
            DebugLogError("Cannot transfer - missing references");
            return;
        }

        DebugLog("Transferring all items from container to player inventory");

        var containerItems = currentContainer.InventoryGridData.GetAllItems();
        int transferredCount = 0;
        int failedCount = 0;

        // Create a copy of the list to avoid modification during iteration
        var itemsToTransfer = new System.Collections.Generic.List<InventoryItemData>(containerItems);

        foreach (var item in itemsToTransfer)
        {
            if (PlayerInventoryManager.Instance.HasSpaceForItem(item.ItemData))
            {
                if (containerGridVisual != null && containerGridVisual.TransferItemToPlayer(item.ID))
                {
                    transferredCount++;
                }
                else
                {
                    failedCount++;
                }
            }
            else
            {
                failedCount++;
            }
        }

        DebugLog($"Transfer complete: {transferredCount} items transferred, {failedCount} items could not be transferred");

        // Show feedback to player
        if (transferredCount > 0)
        {
            ShowTransferFeedback($"Transferred {transferredCount} items to inventory");
        }

        if (failedCount > 0)
        {
            ShowTransferFeedback($"{failedCount} items could not be transferred (no space)");
        }
    }

    /// <summary>
    /// Show transfer feedback to the player.
    /// </summary>
    private void ShowTransferFeedback(string message)
    {
        DebugLog($"Transfer feedback: {message}");
        // In a full implementation, you could show a UI notification here
        // For now, just log the message
    }

    #endregion

    #region Sorting Operations

    /// <summary>
    /// Sort items in the container (placeholder implementation).
    /// </summary>
    private void SortContainer()
    {
        DebugLog("Container sorting not yet implemented");
        // TODO: Implement container sorting logic
    }

    /// <summary>
    /// Sort items in player inventory (placeholder implementation).
    /// </summary>
    private void SortPlayerInventory()
    {
        DebugLog("Player inventory sorting not yet implemented");
        // TODO: Implement player inventory sorting logic
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Check if the container UI is currently open.
    /// </summary>
    public bool IsOpen => isOpen;

    /// <summary>
    /// Get the currently open container.
    /// </summary>
    public StorageContainer GetCurrentContainer()
    {
        return currentContainer;
    }

    /// <summary>
    /// Get the container grid visual.
    /// </summary>
    public StorageContainerGridVisual GetContainerGridVisual()
    {
        return containerGridVisual;
    }

    /// <summary>
    /// Get the player grid visual.
    /// </summary>
    public PlayerInventoryGridVisual GetPlayerGridVisual()
    {
        return playerGridVisual;
    }

    /// <summary>
    /// Force update of UI elements.
    /// </summary>
    [Button("Refresh UI")]
    public void RefreshUI()
    {
        UpdateUI();
        Debug.Log("Storage container UI refreshed");
    }

    #endregion

    #region Static Access

    /// <summary>
    /// Open a container using the static instance.
    /// </summary>
    public static void OpenContainerStatic(StorageContainer container)
    {
        if (Instance != null)
        {
            Instance.OpenContainer(container);
        }
        else
        {
            Debug.LogError("No StorageContainerUI instance available");
        }
    }

    #endregion

    #region Debug Helpers

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[StorageContainerUI] {message}");
    }

    private void DebugLogError(string message)
    {
        Debug.LogError($"[StorageContainerUI] {message}");
    }

    private void DebugLogWarning(string message)
    {
        if (enableDebugLogs)
            Debug.LogWarning($"[StorageContainerUI] {message}");
    }

    #endregion
}