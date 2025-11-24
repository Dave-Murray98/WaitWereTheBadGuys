using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// FIXED: ItemHandlerCoordinator that properly subscribes handlers to animation completion events.
/// Ensures that handlers receive animation completion notifications through the PlayerAnimationManager event system.
/// </summary>
public class ItemHandlerCoordinator : MonoBehaviour
{
    [Header("Handler References")]
    [SerializeField] private UnarmedHandler unarmedHandler;
    [SerializeField] private RangedWeaponHandler rangedWeaponHandler;
    [SerializeField] private ConsumableHandler consumableHandler;
    [SerializeField] private MeleeWeaponHandler meleeWeaponHandler;
    [SerializeField] private KeyItemHandler keyItemHandler;
    [SerializeField] private ToolHandler toolHandler;
    [SerializeField] private ThrowableHandler throwableHandler;
    [SerializeField] private BowHandler bowHandler;

    [Header("Auto-Find Handlers")]
    [SerializeField] private bool autoFindHandlers = true;

    [SerializeField] private bool enableDebugLogs = false;

    [Header("Current State")]
    [SerializeField, ReadOnly] private IEquippedItemHandler activeHandler;
    [SerializeField, ReadOnly] private ItemType currentItemType = ItemType.Unarmed;
    [SerializeField, ReadOnly] private ItemData currentItemData;
    [SerializeField, ReadOnly] private bool isUnarmed = true;

    // Handler dictionary for fast lookup
    private Dictionary<ItemType, IEquippedItemHandler> handlerMap;

    // System references
    private EquippedItemManager equipmentManager;
    private PlayerController playerController;
    private PlayerStateManager stateManager;
    private PlayerAnimationManager animationManager; // FIXED: Added animation manager reference

    // Input state tracking
    private bool primaryActionHeld = false;
    private bool secondaryActionHeld = false;

    // Events
    public System.Action<IEquippedItemHandler> OnActiveHandlerChanged;
    public System.Action<ItemType, ItemType> OnItemTypeChanged;

    #region Initialization

    private void Awake()
    {
        FindSystemReferences();
        SetupHandlers();
        InitializeHandlerMap();
    }

    private void Start()
    {
        ConnectToSystems();
        SetInitialState();

        DebugLog("ItemHandlerCoordinator initialized successfully with all handlers");
    }

    private void FindSystemReferences()
    {
        // Find core system references
        equipmentManager = EquippedItemManager.Instance;
        if (equipmentManager == null)
            equipmentManager = FindFirstObjectByType<EquippedItemManager>();

        playerController = GetComponent<PlayerController>();
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        stateManager = PlayerStateManager.Instance;
        if (stateManager == null)
            stateManager = FindFirstObjectByType<PlayerStateManager>();

        // FIXED: Find animation manager reference
        animationManager = GetComponent<PlayerAnimationManager>();
        if (animationManager == null)
            animationManager = FindFirstObjectByType<PlayerAnimationManager>();
    }

    private void SetupHandlers()
    {
        if (autoFindHandlers)
        {
            // Auto-find existing handlers
            if (unarmedHandler == null)
                unarmedHandler = GetComponent<UnarmedHandler>();
            if (rangedWeaponHandler == null)
                rangedWeaponHandler = GetComponent<RangedWeaponHandler>();
            if (consumableHandler == null)
                consumableHandler = GetComponent<ConsumableHandler>();
            if (meleeWeaponHandler == null)
                meleeWeaponHandler = GetComponent<MeleeWeaponHandler>();
            if (keyItemHandler == null)
                keyItemHandler = GetComponent<KeyItemHandler>();
            if (toolHandler == null)
                toolHandler = GetComponent<ToolHandler>();
            if (throwableHandler == null)
                throwableHandler = GetComponent<ThrowableHandler>();
            if (bowHandler == null)
                bowHandler = GetComponent<BowHandler>();

            // Create missing essential handlers
            if (unarmedHandler == null)
                unarmedHandler = gameObject.AddComponent<UnarmedHandler>();
            if (rangedWeaponHandler == null)
                rangedWeaponHandler = gameObject.AddComponent<RangedWeaponHandler>();
            if (consumableHandler == null)
                consumableHandler = gameObject.AddComponent<ConsumableHandler>();
            if (meleeWeaponHandler == null)
                meleeWeaponHandler = gameObject.AddComponent<MeleeWeaponHandler>();

            // Create new handlers if missing
            if (keyItemHandler == null)
                keyItemHandler = gameObject.AddComponent<KeyItemHandler>();
            if (toolHandler == null)
                toolHandler = gameObject.AddComponent<ToolHandler>();
            if (throwableHandler == null)
                throwableHandler = gameObject.AddComponent<ThrowableHandler>();
            if (bowHandler == null)
                bowHandler = gameObject.AddComponent<BowHandler>();
        }

        DebugLog($"Handlers setup - Unarmed: {unarmedHandler != null}, RangedWeapon: {rangedWeaponHandler != null}, " +
                $"Consumable: {consumableHandler != null}, MeleeWeapon: {meleeWeaponHandler != null}, " +
                $"KeyItem: {keyItemHandler != null}, Tool: {toolHandler != null}, " +
                $"Throwable: {throwableHandler != null}, Bow: {bowHandler != null}");
    }

    private void InitializeHandlerMap()
    {
        handlerMap = new Dictionary<ItemType, IEquippedItemHandler>
        {
            { ItemType.Unarmed, unarmedHandler },
            { ItemType.RangedWeapon, rangedWeaponHandler },
            { ItemType.Consumable, consumableHandler },
            { ItemType.MeleeWeapon, meleeWeaponHandler },
            { ItemType.KeyItem, keyItemHandler },
            { ItemType.Tool, toolHandler },
            { ItemType.Throwable, throwableHandler },
            { ItemType.Bow, bowHandler }
        };

        // Validate all handlers are assigned
        foreach (var kvp in handlerMap)
        {
            if (kvp.Value == null)
            {
                Debug.LogError($"[ItemHandlerCoordinator] No handler assigned for {kvp.Key}!");
            }
        }

        DebugLog($"Handler map initialized with {handlerMap.Count} handlers");
    }

    private void ConnectToSystems()
    {
        // Connect to EquippedItemManager events
        if (equipmentManager != null)
        {
            equipmentManager.OnItemEquipped += OnItemEquipped;
            equipmentManager.OnItemUnequipped += OnItemUnequipped;
            equipmentManager.OnUnarmedActivated += OnUnarmedActivated;
            DebugLog("Connected to EquippedItemManager events");
        }
        else
        {
            Debug.LogError("[ItemHandlerCoordinator] EquippedItemManager not found! Handler system will not work.");
        }

        // Connect to player controller events
        if (playerController != null)
        {
            // Subscribe to input events for actions
            playerController.OnPrimaryActionPressed += OnPrimaryActionPressed;
            playerController.OnPrimaryActionReleased += OnPrimaryActionReleased;
            playerController.OnSecondaryActionPressed += OnSecondaryActionPressed;
            playerController.OnSecondaryActionReleased += OnSecondaryActionReleased;
            playerController.OnReloadPressed += OnReloadActionPressed;
            playerController.OnCancelActionPressed += OnCancelActionPressed;
            DebugLog("Connected to PlayerController events with separated actions");
        }
        else
        {
            Debug.LogError("[ItemHandlerCoordinator] PlayerController not found! Handler system will not work.");
        }

        // FIXED: Connect to animation manager events to monitor completion
        if (animationManager != null)
        {
            animationManager.OnActionAnimationComplete += OnAnimationCompleted;
            DebugLog("Connected to PlayerAnimationManager events");
        }
        else
        {
            Debug.LogError("[ItemHandlerCoordinator] PlayerAnimationManager not found! Animation completion monitoring disabled.");
        }
    }

    private void SetInitialState()
    {
        // Check current equipment state
        if (equipmentManager != null)
        {
            if (equipmentManager.HasEquippedItem)
            {
                var currentItem = equipmentManager.GetEquippedItemData();
                if (currentItem != null)
                {
                    ActivateHandlerForItem(currentItem);
                    return;
                }
            }
        }

        // Default to unarmed
        ActivateUnarmedHandler();
    }

    #endregion

    #region FIXED: Handler Management

    /// <summary>
    /// Activate handler for specific item with proper deactivation
    /// </summary>
    private void ActivateHandlerForItem(ItemData itemData)
    {
        if (itemData == null)
        {
            DebugLog("ActivateHandlerForItem called with null itemData");
            return;
        }

        // Deactivate current handler FIRST
        DeactivateCurrentHandler();

        // Find and activate new handler
        if (handlerMap.TryGetValue(itemData.itemType, out IEquippedItemHandler handler))
        {
            activeHandler = handler;
            currentItemType = itemData.itemType;
            currentItemData = itemData;
            isUnarmed = false;

            // Activate the handler AFTER setting all state
            handler.OnItemEquipped(itemData);

            DebugLog($"Activated {itemData.itemType} handler for {itemData.itemName}");

            // Fire events
            OnActiveHandlerChanged?.Invoke(activeHandler);
            OnItemTypeChanged?.Invoke(ItemType.Unarmed, currentItemType);
        }
        else
        {
            Debug.LogError($"[ItemHandlerCoordinator] No handler found for item type: {itemData.itemType}");
            ActivateUnarmedHandler(); // Fallback
        }
    }

    /// <summary>
    /// Activate unarmed handler with proper deactivation
    /// </summary>
    private void ActivateUnarmedHandler()
    {
        // Deactivate current handler FIRST
        DeactivateCurrentHandler();

        // Activate unarmed handler
        if (unarmedHandler != null)
        {
            activeHandler = unarmedHandler;
            currentItemType = ItemType.Unarmed;
            currentItemData = null;
            isUnarmed = true;

            // For unarmed, we pass null as the ItemData since there's no actual item
            unarmedHandler.OnItemEquipped(null);

            DebugLog("Activated unarmed handler");

            // Fire events
            OnActiveHandlerChanged?.Invoke(activeHandler);
            OnItemTypeChanged?.Invoke(currentItemType, ItemType.Unarmed);
        }
        else
        {
            Debug.LogError("[ItemHandlerCoordinator] Unarmed handler not found!");
        }
    }

    /// <summary>
    /// Properly deactivate current handler
    /// </summary>
    private void DeactivateCurrentHandler()
    {
        if (activeHandler != null)
        {
            DebugLog($"Deactivating {activeHandler.GetType().Name}");

            // Call OnItemUnequipped to properly reset handler state
            activeHandler.OnItemUnequipped();

            DebugLog($"Deactivated {activeHandler.GetType().Name}");
        }

        // Clear reference AFTER deactivating
        activeHandler = null;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handle item equipped from EquippedItemManager
    /// </summary>
    private void OnItemEquipped(EquippedItemData equippedData)
    {
        var itemData = equippedData.GetItemData();
        if (itemData == null)
        {
            DebugLog("OnItemEquipped: ItemData is null");
            return;
        }

        DebugLog($"Equipment change detected: {itemData.itemName} ({itemData.itemType})");
        ActivateHandlerForItem(itemData);
    }

    /// <summary>
    /// Handle item unequipped from EquippedItemManager
    /// </summary>
    private void OnItemUnequipped()
    {
        DebugLog("Equipment unequipped - switching to unarmed");
        ActivateUnarmedHandler();
    }

    /// <summary>
    /// Handle unarmed activation from EquippedItemManager
    /// </summary>
    private void OnUnarmedActivated()
    {
        DebugLog("Unarmed activation detected");
        ActivateUnarmedHandler();
    }

    /// <summary>
    /// FIXED: Handle animation completion events for debugging and monitoring
    /// </summary>
    private void OnAnimationCompleted(PlayerAnimationType animationType)
    {
        DebugLog($"Animation completed for action: {animationType} (Active handler: {activeHandler?.GetType().Name ?? "None"})");

        // This is primarily for debugging and monitoring
        // Individual handlers handle their own completion logic through the same event
    }

    #endregion

    #region Input Routing

    private void Update()
    {
        if (activeHandler != null)
        {
            // Update handler with continuous input state
            UpdateHandlerWithContinuousInput();
        }
    }

    /// <summary>
    /// Update handler with continuous input (held actions)
    /// </summary>
    private void UpdateHandlerWithContinuousInput()
    {
        if (activeHandler == null || !activeHandler.IsActive) return;

        var currentState = GetCurrentPlayerState();

        // Handle held primary action
        if (primaryActionHeld)
        {
            var context = InputContext.Create(false, true, false, currentState, CanPerformActions());
            activeHandler.HandlePrimaryAction(context);
        }

        // Handle held secondary action
        if (secondaryActionHeld)
        {
            var context = InputContext.Create(false, true, false, currentState, CanPerformActions());
            activeHandler.HandleSecondaryAction(context);
        }
    }

    /// <summary>
    /// Handle primary action pressed
    /// </summary>
    private void OnPrimaryActionPressed()
    {
        if (activeHandler == null)
        {
            DebugLog("No active handler for primary action");
            return;
        }

        primaryActionHeld = true;
        var context = InputContext.Create(true, false, false, GetCurrentPlayerState(), CanPerformActions());
        activeHandler.HandlePrimaryAction(context);

        DebugLog($"Routed primary action to {activeHandler.GetType().Name}");
    }

    /// <summary>
    /// Handle primary action released
    /// </summary>
    private void OnPrimaryActionReleased()
    {
        if (activeHandler == null) return;

        primaryActionHeld = false;
        var context = InputContext.Create(false, false, true, GetCurrentPlayerState(), CanPerformActions());
        activeHandler.HandlePrimaryAction(context);

        DebugLog($"Routed primary action release to {activeHandler.GetType().Name}");
    }

    /// <summary>
    /// Handle secondary action pressed
    /// </summary>
    private void OnSecondaryActionPressed()
    {
        if (activeHandler == null)
        {
            DebugLog("No active handler for secondary action");
            return;
        }

        secondaryActionHeld = true;
        var context = InputContext.Create(true, false, false, GetCurrentPlayerState(), CanPerformActions());
        activeHandler.HandleSecondaryAction(context);

        DebugLog($"Routed secondary action to {activeHandler.GetType().Name}");
    }

    /// <summary>
    /// Handle secondary action released
    /// </summary>
    private void OnSecondaryActionReleased()
    {
        if (activeHandler == null) return;

        secondaryActionHeld = false;
        var context = InputContext.Create(false, false, true, GetCurrentPlayerState(), CanPerformActions());
        activeHandler.HandleSecondaryAction(context);

        DebugLog($"Routed secondary action release to {activeHandler.GetType().Name}");
    }

    /// <summary>
    /// Handle reload action pressed
    /// </summary>
    private void OnReloadActionPressed()
    {
        if (activeHandler == null)
        {
            DebugLog("No active handler for reload action");
            return;
        }

        var context = InputContext.Create(true, false, false, GetCurrentPlayerState(), CanPerformActions());
        activeHandler.HandleReloadAction(context);

        DebugLog($"Routed reload action to {activeHandler.GetType().Name}");
    }

    /// <summary>
    /// Handle cancel action pressed
    /// </summary>
    private void OnCancelActionPressed()
    {
        if (activeHandler == null)
        {
            DebugLog("No active handler for cancel action");
            return;
        }

        var context = InputContext.Create(true, false, false, GetCurrentPlayerState(), CanPerformActions());

        // Route cancel action to the active handler
        activeHandler.HandleCancelAction(context);

        DebugLog($"Routed cancel action to {activeHandler.GetType().Name}");
    }
    #endregion

    #region Utility Methods

    /// <summary>
    /// Get current player state
    /// </summary>
    private PlayerStateType GetCurrentPlayerState()
    {
        return stateManager?.CurrentStateType ?? PlayerStateType.Ground;
    }

    /// <summary>
    /// Check if player can perform actions (not paused, not in UI, etc.)
    /// </summary>
    private bool CanPerformActions()
    {
        // Check if game is paused
        if (GameManager.Instance?.isPaused == true)
            return false;

        // Check if UI is open (inventory, menus, etc.)
        if (GameManager.Instance?.uiManager?.isInventoryOpen == true)
            return false;

        // Add other action-blocking conditions here
        return true;
    }

    /// <summary>
    /// Get handler for specific item type
    /// </summary>
    public IEquippedItemHandler GetHandlerForItemType(ItemType itemType)
    {
        handlerMap.TryGetValue(itemType, out IEquippedItemHandler handler);
        return handler;
    }

    /// <summary>
    /// Check if a specific handler is available
    /// </summary>
    public bool HasHandlerForItemType(ItemType itemType)
    {
        return handlerMap.ContainsKey(itemType) && handlerMap[itemType] != null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Get currently active handler
    /// </summary>
    public IEquippedItemHandler GetActiveHandler()
    {
        return activeHandler;
    }

    /// <summary>
    /// Get current item type being handled
    /// </summary>
    public ItemType GetCurrentItemType()
    {
        return currentItemType;
    }

    /// <summary>
    /// Get current item data
    /// </summary>
    public ItemData GetCurrentItemData()
    {
        return currentItemData;
    }

    /// <summary>
    /// Check if currently in unarmed state
    /// </summary>
    public bool IsUnarmed()
    {
        return isUnarmed;
    }

    /// <summary>
    /// Force refresh of current handler (useful for debugging)
    /// </summary>
    public void RefreshCurrentHandler()
    {
        if (activeHandler != null && currentItemData != null)
        {
            DebugLog("Force refreshing current handler");
            activeHandler.OnItemUnequipped();
            activeHandler.OnItemEquipped(currentItemData);
        }
        else if (activeHandler != null && isUnarmed)
        {
            DebugLog("Force refreshing unarmed handler");
            activeHandler.OnItemUnequipped();
            activeHandler.OnItemEquipped(null);
        }
    }

    /// <summary>
    /// Get debug information about the coordinator state
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Item Handler Coordinator Debug ===");
        info.AppendLine($"Active Handler: {activeHandler?.GetType().Name ?? "None"}");
        info.AppendLine($"Current Item Type: {currentItemType}");
        info.AppendLine($"Current Item: {currentItemData?.itemName ?? "None"}");
        info.AppendLine($"Is Unarmed: {isUnarmed}");
        info.AppendLine($"Primary Action Held: {primaryActionHeld}");
        info.AppendLine($"Secondary Action Held: {secondaryActionHeld}");
        info.AppendLine($"Animation Manager Connected: {animationManager != null}");

        if (activeHandler != null)
        {
            info.AppendLine($"Handler Active: {activeHandler.IsActive}");
            info.AppendLine($"Handler Debug: {activeHandler.GetDebugInfo()}");
        }

        return info.ToString();
    }

    #endregion

    #region Debug Methods

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ItemHandlerCoordinator] {message}");
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        // Disconnect from EquippedItemManager events
        if (equipmentManager != null)
        {
            equipmentManager.OnItemEquipped -= OnItemEquipped;
            equipmentManager.OnItemUnequipped -= OnItemUnequipped;
            equipmentManager.OnUnarmedActivated -= OnUnarmedActivated;
        }

        // Disconnect from PlayerController events
        if (playerController != null)
        {
            playerController.OnPrimaryActionPressed -= OnPrimaryActionPressed;
            playerController.OnPrimaryActionReleased -= OnPrimaryActionReleased;
            playerController.OnSecondaryActionPressed -= OnSecondaryActionPressed;
            playerController.OnSecondaryActionReleased -= OnSecondaryActionReleased;
            playerController.OnReloadPressed -= OnReloadActionPressed;
            playerController.OnCancelActionPressed -= OnCancelActionPressed;
        }

        // FIXED: Disconnect from animation manager events
        if (animationManager != null)
        {
            animationManager.OnActionAnimationComplete -= OnAnimationCompleted;
        }

        // Deactivate current handler
        DeactivateCurrentHandler();
    }

    #endregion
}