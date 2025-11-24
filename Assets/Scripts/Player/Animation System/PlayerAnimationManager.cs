using UnityEngine;

/// <summary>
/// REFACTORED: PlayerAnimationManager now uses enum-based animation system for maximum performance.
/// Uses event-based communication with fast enum lookups instead of string comparisons.
/// The OnActionAnimationComplete event properly notifies handlers when animations finish.
/// </summary>
public class PlayerAnimationManager : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private Animator animator;

    [Header("Layer Controllers")]
    [SerializeField] private LowerBodyAnimationController lowerBodyController;
    [SerializeField] private UpperBodyAnimationController upperBodyController;

    [Header("Equipped Item Animation")]
    [SerializeField] private EquippedItemAnimationController equippedItemController;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Animation cache that contains all item animations for the player's body for the current equipped item
    private PlayerBodyAnimationSetCache currentBodyAnimationCache;

    // Current state tracking
    private PlayerStateType currentPlayerState = PlayerStateType.Ground;
    private ItemData currentEquippedItem = null;
    private bool isInitialized = false;

    // OPTIMIZED: Animation tracking using enums
    private bool isPlayingAnimation = false;
    private PlayerAnimationType currentAnimationType = PlayerAnimationType.Idle;

    // Component references
    public PlayerController playerController;
    private PlayerStateManager stateManager;
    private EquippedItemManager equippedItemManager;
    private EquippedItemVisualManager visualManager; // For getting equipped item GameObjects

    // Enhanced event system for both player body and item animations
    public System.Action<AnimationClip, int> OnAnimationStarted; // clip, layer
    //public System.Action<string> OnActionAnimationComplete; // actionType as string - THIS IS THE KEY EVENT
    public System.Action<PlayerAnimationType> OnActionAnimationComplete; // NEW: Enum-based event

    // Equipped item animation events
    public System.Action<AnimationClip> OnItemAnimationStarted;

    #region Initialization

    private void Awake()
    {
        FindComponents();
        ValidateSetup();
    }

    private void Start()
    {
        Initialize();
    }

    private void FindComponents()
    {
        // Find animator
        if (animator == null)
            animator = GetComponent<Animator>();

        // Find controllers
        if (lowerBodyController == null)
            lowerBodyController = GetComponent<LowerBodyAnimationController>();

        if (upperBodyController == null)
            upperBodyController = GetComponent<UpperBodyAnimationController>();

        if (equippedItemController == null)
            equippedItemController = GetComponent<EquippedItemAnimationController>();

        if (visualManager == null)
            visualManager = GetComponent<EquippedItemVisualManager>();

        // Find other components
        playerController = GetComponent<PlayerController>();
        stateManager = FindFirstObjectByType<PlayerStateManager>();
        equippedItemManager = FindFirstObjectByType<EquippedItemManager>();

        DebugLog("Components found and cached");
    }

    private void ValidateSetup()
    {
        if (animator == null)
        {
            Debug.LogError("[PlayerAnimationManager] No Animator found! Animation system will not work.");
            return;
        }

        if (animator.layerCount < 2)
        {
            Debug.LogError("[PlayerAnimationManager] Animator must have at least 2 layers (Lower Body=0, Upper Body=1)");
            return;
        }

        if (equippedItemController == null)
        {
            Debug.LogWarning("[PlayerAnimationManager] No EquippedItemAnimationController found! Item animations will not work.");
        }

        DebugLog("Animation manager setup validated successfully");
    }

    private void Initialize()
    {
        if (animator == null)
        {
            Debug.LogError("[PlayerAnimationManager] Cannot initialize - no Animator");
            return;
        }

        // Initialize animation cache
        currentBodyAnimationCache = new PlayerBodyAnimationSetCache(enableDebugLogs);

        // Initialize layer controllers
        InitializeLayerControllers();

        // Subscribe to events
        SubscribeToEvents();

        // Set initial state
        RefreshCurrentState();

        isInitialized = true;
        DebugLog("PlayerAnimationManager initialized successfully");
    }

    private void InitializeLayerControllers()
    {
        // Initialize layer controllers with animator and layer indices
        lowerBodyController.Initialize(this, animator, 0); // Layer 0 = Lower Body
        upperBodyController.Initialize(this, animator, 1); // Layer 1 = Upper Body

        // Initialize equipped item controller
        if (equippedItemController != null)
            equippedItemController.Initialize(this);

        // Set initial animation cache
        upperBodyController.SetAnimationCache(currentBodyAnimationCache);

        DebugLog("Player Animation controllers initialized");
    }

    private void SubscribeToEvents()
    {
        // Subscribe to state manager events
        if (stateManager != null)
        {
            stateManager.OnStateChanged += OnPlayerStateChanged;
            DebugLog("Subscribed to PlayerStateManager events");
        }

        // Subscribe to equipment events
        if (equippedItemManager != null)
        {
            equippedItemManager.OnItemEquipped += OnItemEquipped;
            equippedItemManager.OnItemUnequipped += OnItemUnequipped;
            DebugLog("Subscribed to EquippedItemManager events");
        }

        // Subscribe to layer controller events
        if (lowerBodyController != null)
            lowerBodyController.OnAnimationStarted += (clip) => OnAnimationStarted?.Invoke(clip, 0);

        if (upperBodyController != null)
            upperBodyController.OnAnimationStarted += (clip) => OnAnimationStarted?.Invoke(clip, 1);

        // Subscribe to equipped item animation events
        if (equippedItemController != null)
            equippedItemController.OnItemAnimationStarted += (clip) => OnItemAnimationStarted?.Invoke(clip);
    }

    private void RefreshCurrentState()
    {
        // Update player state
        if (stateManager != null)
        {
            currentPlayerState = stateManager.CurrentStateType;
        }
        else if (playerController != null)
        {
            currentPlayerState = playerController.CurrentPlayerState;
        }

        // Update equipped item
        if (equippedItemManager != null && equippedItemManager.HasEquippedItem)
        {
            currentEquippedItem = equippedItemManager.GetEquippedItemData();
            if (currentEquippedItem != null)
            {
                UpdatePlayerBodyAnimationCache(currentEquippedItem);
            }
        }

        DebugLog($"Initial state: {currentPlayerState}, Item: {currentEquippedItem?.itemName ?? "None"}");
    }

    #endregion

    #region Public API - OPTIMIZED with Enum Support

    /// <summary>
    /// UPDATED: Update locomotion animations based on movement input with vehicle seat support
    /// </summary>
    public void UpdateLocomotion(Vector2 movementInput, bool isCrouching, bool isRunning)
    {
        if (!isInitialized)
        {
            DebugLog("UpdateLocomotion called but system not initialized");
            return;
        }

        // Update lower body locomotion (always uses current player state, no item dependency)
        lowerBodyController?.UpdateLocomotion(currentPlayerState, movementInput, isCrouching, isRunning);

        // Update upper body locomotion (uses current equipped item) - only if not playing action
        if (upperBodyController != null && !isPlayingAnimation)
        {
            if (currentEquippedItem != null)
            {
                upperBodyController.UpdateLocomotion(currentPlayerState, currentEquippedItem, movementInput, isCrouching, isRunning);
            }
            else
            {
                // No item equipped - use unarmed animations
                upperBodyController.UpdateUnarmedLocomotion(currentPlayerState, movementInput, isCrouching, isRunning);
            }
        }
    }

    /// <summary>
    /// OPTIMIZED: Trigger an action animation using enum on both player body AND equipped item
    /// </summary>
    public void TriggerAction(PlayerAnimationType actionType, System.Action onCompleteCallback = null)
    {
        if (!isInitialized)
        {
            DebugLog($"Cannot trigger action {actionType} - system not initialized");
            return;
        }

        DebugLog($"=== Triggering dual animation: {actionType} ===");
        DebugLog($"Player Body Item: {currentEquippedItem?.itemName ?? "Unarmed"}");

        // CRITICAL: Don't store callback - we use events instead
        isPlayingAnimation = true;
        currentAnimationType = actionType;

        // 1. Trigger player body animation
        TriggerPlayerBodyAnimation(actionType);

        // 2. Trigger equipped item animation (if we have an equipped item)
        TriggerEquippedItemAnimation(actionType);

        DebugLog($"Dual animation triggered: {actionType}");
    }


    /// <summary>
    /// OPTIMIZED: Trigger animation on player body using enum
    /// </summary>
    private void TriggerPlayerBodyAnimation(PlayerAnimationType actionType)
    {
        if (upperBodyController == null)
        {
            DebugLog($"Cannot trigger player body action {actionType} - no upper body controller");
            return;
        }

        DebugLog($"Triggering player body animation: {actionType}");
        upperBodyController.TriggerActionWithoutCompletion(currentPlayerState, actionType, currentEquippedItem);
    }

    /// <summary>
    /// OPTIMIZED: Trigger animation on equipped item using enum
    /// </summary>
    private void TriggerEquippedItemAnimation(PlayerAnimationType actionType)
    {
        if (equippedItemController == null)
        {
            DebugLog("No equipped item controller - skipping item animation");
            return;
        }

        if (currentEquippedItem == null)
        {
            DebugLog("No equipped item - skipping item animation (unarmed)");
            return;
        }

        DebugLog($"Triggering equipped item animation: {actionType}");
        equippedItemController.TriggerItemAction(actionType);
    }

    /// <summary>
    /// OPTIMIZED: Called by animation events to signal animation completion using enum
    /// Uses event system to notify handlers properly
    /// </summary>
    public void OnActionComplete(PlayerAnimationType actionType)
    {
        DebugLog($"=== Animation completed: {actionType} ===");

        // Store the completed action type before resetting state
        PlayerAnimationType completedActionType = actionType != PlayerAnimationType.Idle ? actionType : currentAnimationType;

        // Reset our animation state FIRST
        isPlayingAnimation = false;
        currentAnimationType = PlayerAnimationType.Idle;

        // Fire BOTH events for backward compatibility
        DebugLog($"Firing OnActionAnimationComplete events for: {completedActionType}");

        // NEW: Enum-based event (faster for enum-aware subscribers)
        OnActionAnimationComplete?.Invoke(completedActionType);

        // Return to locomotion after event fires
        ReturnToLocomotion();

        // Return equipped item to idle
        if (equippedItemController != null)
        {
            equippedItemController.ReturnToIdle();
        }

        DebugLog($"Animation completion handling finished for: {completedActionType}");
    }

    /// <summary>
    /// Called by animation events when climb animation completes
    /// This method should be called by an Animation Event at the end of the climb animation
    /// </summary>
    public void OnClimbComplete()
    {
        DebugLog("=== Climb animation completed via Animation Event ===");

        // Call the standard action complete handler with climb type
        OnActionComplete(PlayerAnimationType.Climb);
    }

    /// <summary>
    /// OPTIMIZED: Start looping action using enum on both player body and equipped item
    /// </summary>
    public void StartLoopingAction(PlayerAnimationType actionType)
    {
        DebugLog($"Starting looping action: {actionType}");

        // Start loop on player body (if supported)
        TriggerPlayerBodyAnimation(actionType);

        // Start loop on equipped item
        if (equippedItemController != null && currentEquippedItem != null)
        {
            equippedItemController.StartLoopingAction(actionType);
        }
    }


    /// <summary>
    /// Stop looping action on both player body and equipped item
    /// </summary>
    public void StopLoopingAction()
    {
        DebugLog("Stopping looping action");

        // Stop loop on equipped item
        if (equippedItemController != null)
        {
            equippedItemController.StopLoopingAction();
        }

        // Return player body to locomotion
        ReturnToLocomotion();
    }

    /// <summary>
    /// Alternative method name for animation events (more descriptive)
    /// </summary>
    public void CompleteCurrentAction()
    {
        OnActionComplete(currentAnimationType);
    }

    /// <summary>
    /// OPTIMIZED: Complete specific action type using enum (for complex animation setups)
    /// </summary>
    public void CompleteAction(PlayerAnimationType actionType)
    {
        OnActionComplete(actionType);
    }


    /// <summary>
    /// Force stop all action animations and return to locomotion
    /// </summary>
    public void StopAllActions()
    {
        if (!isInitialized) return;

        if (isPlayingAnimation)
        {
            DebugLog($"Force stopping animation: {currentAnimationType}");

            // Store the action being stopped
            PlayerAnimationType stoppedAction = currentAnimationType;

            // Reset animation state
            isPlayingAnimation = false;
            currentAnimationType = PlayerAnimationType.Idle;

            // Fire completion events for the stopped action
            OnActionAnimationComplete?.Invoke(stoppedAction);
            //OnActionAnimationComplete?.Invoke(stoppedAction.ToDebugString());
        }

        upperBodyController?.StopCurrentAction();
        ReturnToLocomotion();

        DebugLog("Force stopped all action animations");
    }

    /// <summary>
    /// Check if an action animation is currently playing
    /// </summary>
    public bool IsPlayingAction()
    {
        return isPlayingAnimation;
    }

    /// <summary>
    /// OPTIMIZED: Get the currently playing action type as enum
    /// </summary>
    public PlayerAnimationType GetCurrentActionType()
    {
        return currentAnimationType;
    }

    /// <summary>
    /// Return upper body to locomotion after action completion
    /// </summary>
    private void ReturnToLocomotion()
    {
        if (upperBodyController != null && !isPlayingAnimation)
        {
            upperBodyController.ReturnToLocomotionFromAction();
        }
    }

    /// <summary>
    /// UPDATED: Force animation refresh when item changes with vehicle seat support
    /// </summary>
    public void RefreshAnimationSystem()
    {
        if (!isInitialized)
        {
            DebugLog("Cannot refresh - system not initialized");
            return;
        }

        DebugLog("Refreshing animation system");

        // Update current state and item
        RefreshCurrentState();

        // Refresh layer controllers
        lowerBodyController?.RefreshAnimations();
        upperBodyController?.RefreshAnimations();

        // Force immediate locomotion update if not playing animation
        if (!isPlayingAnimation && upperBodyController != null)
        {
            var playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                Vector2 currentInput = InputManager.Instance?.MovementInput ?? Vector2.zero;
                bool isCrouching = playerController.IsCrouching;
                bool isRunning = playerController.IsSprinting;

                if (currentEquippedItem != null)
                {
                    upperBodyController.UpdateLocomotion(
                        currentPlayerState,
                        currentEquippedItem,
                        currentInput,
                        isCrouching,
                        isRunning
                    );
                }
                else
                {
                    upperBodyController.UpdateUnarmedLocomotion(currentPlayerState, currentInput, isCrouching, isRunning);
                }
            }
        }

        DebugLog("Animation system refresh complete");
    }

    #endregion

    #region Climbing Utility
    /// <summary>
    /// Stop climb animation if playing (for cancellation)
    /// </summary>
    public void StopClimbAnimation()
    {
        if (currentAnimationType == PlayerAnimationType.Climb && isPlayingAnimation)
        {
            DebugLog("Stopping climb animation");

            // Complete the climb animation immediately
            OnClimbComplete();
        }
    }

    #endregion

    #region Event Handlers

    private void OnPlayerStateChanged(PlayerStateType previousState, PlayerStateType newState)
    {
        DebugLog($"Player state changed: {previousState} -> {newState}");

        currentPlayerState = newState;

        // Update animation cache if needed (different states might need different animations)
        if (currentEquippedItem != null)
        {
            UpdatePlayerBodyAnimationCache(currentEquippedItem);
        }

        // Notify layer controllers of state change
        lowerBodyController?.OnPlayerStateChanged(newState);
        upperBodyController?.OnPlayerStateChanged(newState);
    }

    private void OnItemEquipped(EquippedItemData equippedData)
    {
        var itemData = equippedData.GetItemData();
        if (itemData == null)
        {
            DebugLog("Item equipped but ItemData is null");
            return;
        }

        DebugLog($"Item equipped: {itemData.itemName}");

        currentEquippedItem = itemData;

        // Update player body animation cache
        UpdatePlayerBodyAnimationCache(itemData);

        // Set up equipped item animations
        SetupEquippedItemAnimations(itemData);

        // Notify upper body controller
        upperBodyController?.OnItemEquipped(itemData);
    }

    private void OnItemUnequipped()
    {
        DebugLog("Item unequipped - switching to unarmed");

        currentEquippedItem = null;

        // Clear player body cache and revert to unarmed
        if (currentBodyAnimationCache != null)
        {
            currentBodyAnimationCache.ClearCache();
        }

        // Clear equipped item animations
        if (equippedItemController != null)
        {
            equippedItemController.ClearCurrentItem();
        }

        // Notify upper body controller
        upperBodyController?.OnItemUnequipped();
    }

    /// <summary>
    /// Set up equipped item animations when item is equipped
    /// </summary>
    private void SetupEquippedItemAnimations(ItemData itemData)
    {
        if (equippedItemController == null || visualManager == null)
        {
            if (equippedItemController == null)
                DebugLog("No equipped item controller found");

            if (visualManager == null)
                DebugLog("No visual manager found");

            DebugLog("Cannot setup equipped item animations - missing controller or visual manager");
            return;
        }

        // Get the currently active equipped item GameObject
        GameObject equippedItemGameObject = visualManager.GetCurrentActiveObject();

        if (equippedItemGameObject == null)
        {
            DebugLog($"No equipped item GameObject found for {itemData.itemName}");
            return;
        }

        // Set up the equipped item animations
        equippedItemController.SetEquippedItem(itemData, equippedItemGameObject);

        DebugLog($"Set up equipped item animations for: {itemData.itemName}");
    }

    #endregion

    #region Internal Methods

    private void UpdatePlayerBodyAnimationCache(ItemData itemData)
    {
        if (currentBodyAnimationCache == null || itemData == null)
        {
            DebugLog("Cannot update player body animation cache - cache or item is null");
            return;
        }

        // Get the player body animation database
        var playerBodyDatabase = itemData.GetPlayerBodyAnimationDatabase();
        if (playerBodyDatabase == null)
        {
            DebugLog($"No player body animation database found for: {itemData.itemName}");
            return;
        }

        // Update cache with new item's player body animations
        currentBodyAnimationCache.CacheAnimationsForItem(itemData);

        // Update layer controllers with new cache
        upperBodyController?.SetAnimationCache(currentBodyAnimationCache);

        DebugLog($"Updated player body animation cache for: {itemData.itemName}");
    }


    #endregion

    #region Public Properties and Debug

    public bool IsInitialized => isInitialized;
    public PlayerStateType CurrentPlayerState => currentPlayerState;
    public ItemData CurrentEquippedItem => currentEquippedItem;
    public Animator Animator => animator;

    // Animation cache that contains all item animations for the player's body for the current equipped item
    public PlayerBodyAnimationSetCache AnimationCache => currentBodyAnimationCache;

    /// <summary>
    /// Get equipped item animation controller (for advanced use)
    /// </summary>
    public EquippedItemAnimationController GetEquippedItemController()
    {
        return equippedItemController;
    }

    /// <summary>
    /// UPDATED: Get debug information about the animation system with vehicle info
    /// </summary>
    public string GetDebugInfo()
    {
        if (!isInitialized) return "Animation Manager: Not Initialized";

        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Player Animation Manager Debug ===");
        info.AppendLine($"Initialized: {isInitialized}");
        info.AppendLine($"Current State: {currentPlayerState}");
        info.AppendLine($"Equipped Item: {currentEquippedItem?.itemName ?? "None"}");
        info.AppendLine($"Playing Animation: {isPlayingAnimation}");
        info.AppendLine($"Current Animation: {currentAnimationType} ({currentAnimationType.ToDebugString()})");
        // info.AppendLine($"String Event Subscribers: {OnActionAnimationComplete?.GetInvocationList()?.Length ?? 0}");
        info.AppendLine($"Enum Event Subscribers: {OnActionAnimationComplete?.GetInvocationList()?.Length ?? 0}");

        // NEW: Add vehicle information if in vehicle state
        if (currentPlayerState == PlayerStateType.Vehicle)
        {
            var playerController = FindFirstObjectByType<PlayerController>();
            var currentVehicle = playerController?.GetCurrentVehicle();
            if (currentVehicle != null)
            {
                info.AppendLine($"Current Vehicle: {currentVehicle.VehicleID} (Seated: {currentVehicle.IsVehicleSeated})");
            }
        }

        if (currentBodyAnimationCache != null)
        {
            var stats = currentBodyAnimationCache.GetCacheStats();
            info.AppendLine($"Animation Cache: {stats}");
        }

        return info.ToString();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerAnimationManager] {message}");
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (stateManager != null)
        {
            stateManager.OnStateChanged -= OnPlayerStateChanged;
        }

        if (equippedItemManager != null)
        {
            equippedItemManager.OnItemEquipped -= OnItemEquipped;
            equippedItemManager.OnItemUnequipped -= OnItemUnequipped;
        }

        // Clear animation cache
        currentBodyAnimationCache?.ClearCache();

        isInitialized = false;
    }

    #endregion
}