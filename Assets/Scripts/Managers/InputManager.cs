using System;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// FIXED: InputManager with properly separated movement actions and gameplay actions.
/// Movement actions (jump, crouch, surface, dive) are separate from gameplay actions (primary, secondary, reload).
/// </summary>
public class InputManager : MonoBehaviour, IManager
{
    public static InputManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    #region Fields
    [Header("Input Actions")]
    public InputActionAsset inputActions;

    [Header("UI Actions")]
    private InputAction pauseAction;

    [Header("Core Movement Actions")]
    private InputAction moveAction;
    private InputAction lookAction;

    [Header("Ground Locomotion Actions")]
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;

    [Header("Swimming Actions")]
    private InputAction surfaceAction;
    private InputAction swimSpeedAction;
    private InputAction diveAction;

    [Header("Vehicle Actions")]
    private InputAction exitVehicleAction;
    private InputAction brakeAction;

    [Header("Gameplay Actions")]
    private InputAction primaryAction;      // Separate primary action (shoot, use, etc.)
    private InputAction secondaryAction;    // Separate secondary action (aim, alt use, etc.)
    private InputAction reloadAction;       // Reload action
    private InputAction cancelAction;      // Cancel action (for held inputs)
    private InputAction interactAction;
    private InputAction leftClickAction;
    private InputAction rightClickAction;
    private InputAction scrollWheelAction;
    private InputAction[] hotkeyActions = new InputAction[10];
    private InputAction adsAction;

    [Header("Inventory Actions")]
    private InputAction toggleInventoryAction;
    private InputAction rotateInventoryItemAction;

    #endregion

    #region Public Properties
    public Vector2 MovementInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    // Movement action states (jump, crouch, surface, dive)
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool CrouchPressed { get; private set; }
    public bool CrouchHeld { get; private set; }
    public bool SurfacePressed { get; private set; }
    public bool SurfaceHeld { get; private set; }
    public bool DivePressed { get; private set; }
    public bool DiveHeld { get; private set; }
    public bool BrakePressed { get; private set; }
    public bool BrakeHeld { get; private set; }

    // Gameplay action states (primary, secondary, reload)
    public bool PrimaryActionPressed { get; private set; }
    public bool PrimaryActionHeld { get; private set; }
    public bool SecondaryActionPressed { get; private set; }
    public bool SecondaryActionHeld { get; private set; }
    public bool ReloadPressed { get; private set; }
    public bool CancelActionPressed { get; private set; }

    public bool SpeedModifierHeld { get; private set; }
    #endregion

    #region Events

    // Movement action events (jump, crouch, surface, dive)
    public event Action OnJumpPressed;
    public event Action OnJumpReleased;
    public event Action OnCrouchPressed;
    public event Action OnCrouchReleased;
    public event Action OnSurfacePressed;
    public event Action OnSurfaceReleased;
    public event Action OnDivePressed;
    public event Action OnDiveReleased;

    // Gameplay action events (primary, secondary, reload)
    public event Action OnPrimaryActionPressed;
    public event Action OnPrimaryActionReleased;
    public event Action OnSecondaryActionPressed;
    public event Action OnSecondaryActionReleased;
    public event Action OnReloadPressed;
    public event Action OnCancelActionPressed; // For cancelling held actions

    // Other gameplay events
    public event Action OnInteractPressed;
    public event Action OnRotateInventoryItemPressed;
    public event Action OnLeftClickPressed;
    public event Action OnRightClickPressed;
    public System.Action<Vector2> OnScrollWheelInput;
    public System.Action<int> OnHotkeyPressed;
    public event Action OnADSHeldStart;
    public event Action OnADSReleased;
    public static event Action<InputManager> OnInputManagerReady;

    // Vehicle events
    public event Action OnExitVehiclePressed;
    public event Action OnBrakePressed;
    public event Action OnBrakeReleased;

    #endregion

    // Action maps
    private InputActionMap uiActionMap;
    private InputActionMap coreMovementActionMap;
    private InputActionMap groundLocomotionActionMap;
    private InputActionMap swimmingActionMap;
    private InputActionMap vehicleActionMap;
    private InputActionMap gameplayActionMap;
    private InputActionMap inventoryActionMap;

    // State tracking
    private InputActionMap currentMovementActionMap;
    private MovementMode currentMovementMode = MovementMode.Ground;
    private bool gameplayInputEnabled = true;
    private bool isCleanedUp = false;
    private bool isFullyInitialized = false;

    // Utility methods
    public bool IsMoving() => MovementInput.magnitude > 0.1f;
    public bool IsLooking() => LookInput.magnitude > 0.1f;
    public bool IsProperlyInitialized => isFullyInitialized && !isCleanedUp;

    #region Singleton Pattern

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // IMMEDIATE SETUP - Don't wait for Initialize()
            SetupInputActionsImmediate();

            DebugLog("[InputManager] Singleton created with immediate input setup");
        }
        else
        {
            DebugLog("[InputManager] Duplicate destroyed");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Complete the initialization process
        CompleteInitialization();
    }

    #endregion

    #region FIXED: Immediate Setup

    /// <summary>
    ///  Sets up input actions immediately in Awake() so input works from frame 1
    /// </summary>
    private void SetupInputActionsImmediate()
    {
        if (inputActions == null)
        {
            Debug.LogError("[InputManager] InputActionAsset is not assigned! Input will not work!");
            return;
        }

        // Get action maps
        uiActionMap = inputActions.FindActionMap("UI");
        coreMovementActionMap = inputActions.FindActionMap("CoreMovement");
        groundLocomotionActionMap = inputActions.FindActionMap("GroundLocomotion");
        swimmingActionMap = inputActions.FindActionMap("Swimming");
        vehicleActionMap = inputActions.FindActionMap("Vehicle");
        gameplayActionMap = inputActions.FindActionMap("Gameplay");
        inventoryActionMap = inputActions.FindActionMap("Inventory");

        // Validate critical action maps exist
        if (uiActionMap == null)
        {
            Debug.LogError("[InputManager] UI ActionMap not found! Pause won't work!");
            return;
        }

        if (coreMovementActionMap == null || groundLocomotionActionMap == null)
        {
            Debug.LogError("[InputManager] Core movement ActionMaps not found! Movement won't work!");
            return;
        }

        // Setup actions
        SetupUIInputActions();
        SetupCoreMovementInputActions();
        SetupGroundLocomotionInputActions();
        SetupSwimmingInputActions();
        SetupVehicleInputActions();
        SetupGameplayInputActions();
        SetupInventoryInputActions();

        // Subscribe to events
        SubscribeToInputActions();

        // CRITICAL: Enable essential ActionMaps immediately
        EnableEssentialActionMapsImmediate();

        // Set initial movement mode
        currentMovementMode = MovementMode.Ground;
        currentMovementActionMap = groundLocomotionActionMap;

        DebugLog("[InputManager] Immediate input setup complete - Input should work now!");
    }

    /// <summary>
    /// CRITICAL FIX: Enables essential ActionMaps immediately so input works from frame 1
    /// </summary>
    private void EnableEssentialActionMapsImmediate()
    {
        // UI ActionMap - MUST be enabled for pause to work
        if (uiActionMap != null)
        {
            uiActionMap.Enable();
            DebugLog("[InputManager] UI ActionMap enabled immediately");
        }

        // Core Movement ActionMap - MUST be enabled for movement input
        if (coreMovementActionMap != null)
        {
            coreMovementActionMap.Enable();
            DebugLog("[InputManager] Core Movement ActionMap enabled immediately");
        }

        // Ground Locomotion ActionMap - Default movement mode
        if (groundLocomotionActionMap != null)
        {
            groundLocomotionActionMap.Enable();
            DebugLog("[InputManager] Ground Locomotion ActionMap enabled immediately");
        }

        // Gameplay ActionMap - For interactions
        if (gameplayActionMap != null)
        {
            gameplayActionMap.Enable();
            DebugLog("[InputManager] Gameplay ActionMap enabled immediately");
        }

        // Inventory ActionMap - For inventory controls
        if (inventoryActionMap != null)
        {
            inventoryActionMap.Enable();
            DebugLog("[InputManager] Inventory ActionMap enabled immediately");
        }

        // Vehicle ActionMap - For vehicle controls
        if (vehicleActionMap != null)
        {
            vehicleActionMap.Enable();
            DebugLog("[InputManager] Vehicle ActionMap enabled immediately");
        }

        DebugLog("[InputManager] All essential ActionMaps enabled - Input is active!");
    }

    /// <summary>
    /// Completes initialization after immediate setup
    /// </summary>
    private void CompleteInitialization()
    {
        // Subscribe to game events
        GameEvents.OnGamePaused += DisableGameplayInput;
        GameEvents.OnGameResumed += EnableGameplayInput;

        isFullyInitialized = true;

        DebugLog("[InputManager] Full initialization complete");

        // Notify other systems
        OnInputManagerReady?.Invoke(this);
    }

    #endregion

    #region IManager Implementation

    public void Initialize()
    {
        if (isCleanedUp)
        {
            DebugLog("[InputManager] Reinitializing after cleanup");
            isCleanedUp = false;
            SetupInputActionsImmediate();
        }

        if (!isFullyInitialized)
        {
            CompleteInitialization();
        }

        DebugLog("[InputManager] Initialize called - already set up in Awake()");
    }

    public void RefreshReferences()
    {
        if (isCleanedUp || !isFullyInitialized)
        {
            DebugLog("[InputManager] Skipping RefreshReferences - not properly initialized");
            return;
        }

        DebugLog("[InputManager] RefreshReferences - ensuring ActionMaps are enabled");

        // Re-enable essential ActionMaps
        EnableEssentialActionMapsImmediate();

        // Notify systems that we're ready
        OnInputManagerReady?.Invoke(this);
    }

    public void Cleanup()
    {
        DebugLog("[InputManager] Starting cleanup");
        isCleanedUp = true;
        isFullyInitialized = false;

        // Clear events
        ClearAllEvents();

        // Unsubscribe from game events
        GameEvents.OnGamePaused -= DisableGameplayInput;
        GameEvents.OnGameResumed -= EnableGameplayInput;

        // Disable and clean up input actions
        DisableAllInputActions();
        UnsubscribeFromInputActions();
    }

    #endregion

    #region Movement Mode Management

    public void SetMovementMode(MovementMode mode)
    {
        if (currentMovementMode == mode && currentMovementActionMap != null && currentMovementActionMap.enabled)
        {
            return; // Already in correct mode and working
        }

        // Disable current movement ActionMap (but keep others enabled)
        if (currentMovementActionMap != null)
        {
            currentMovementActionMap.Disable();
        }

        // Set new movement ActionMap
        switch (mode)
        {
            case MovementMode.Ground:
                currentMovementActionMap = groundLocomotionActionMap;
                break;
            case MovementMode.Swimming:
                currentMovementActionMap = swimmingActionMap;
                break;
            case MovementMode.Vehicle:
                currentMovementActionMap = vehicleActionMap;
                break;
            case MovementMode.Climbing:
                currentMovementActionMap = groundLocomotionActionMap; // just copy ground for now, as player will most likely transition to ground when climbing finishes
                break;
            default:
                Debug.LogWarning($"[InputManager] Unknown movement mode {mode}, defaulting to Ground");
                currentMovementActionMap = groundLocomotionActionMap;
                mode = MovementMode.Ground;
                break;
        }

        currentMovementMode = mode;

        // Enable new movement ActionMap
        if (currentMovementActionMap != null && gameplayInputEnabled)
        {
            currentMovementActionMap.Enable();
        }
    }

    public MovementMode GetCurrentMovementMode() => currentMovementMode;

    public void ForceResetToGroundMode()
    {
        Debug.LogWarning("[InputManager] FORCE RESET: Resetting to Ground mode");

        // Disable all movement ActionMaps
        groundLocomotionActionMap?.Disable();
        swimmingActionMap?.Disable();

        // Force set to ground mode
        currentMovementMode = MovementMode.Ground;
        currentMovementActionMap = groundLocomotionActionMap;

        // Enable ground ActionMap
        if (currentMovementActionMap != null && gameplayInputEnabled)
        {
            currentMovementActionMap.Enable();
            DebugLog("[InputManager] Ground mode reset complete");
        }
    }

    #endregion

    #region Input State Management

    public void DisableGameplayInput()
    {
        if (isCleanedUp) return;

        DebugLog("[InputManager] Disabling gameplay input (keeping UI enabled)");
        gameplayInputEnabled = false;

        // Disable gameplay ActionMaps but KEEP UI enabled
        coreMovementActionMap?.Disable();
        currentMovementActionMap?.Disable();
        gameplayActionMap?.Disable();
        inventoryActionMap?.Disable();

        // UI ActionMap stays enabled for pause functionality
        DebugLog("[InputManager] Gameplay input disabled, UI remains active");
    }

    public void EnableGameplayInput()
    {
        if (isCleanedUp) return;

        DebugLog("[InputManager] Enabling gameplay input");
        gameplayInputEnabled = true;

        // Re-enable all essential ActionMaps
        EnableEssentialActionMapsImmediate();
    }

    private void DisableAllInputActions()
    {
        uiActionMap?.Disable();
        coreMovementActionMap?.Disable();
        groundLocomotionActionMap?.Disable();
        swimmingActionMap?.Disable();
        gameplayActionMap?.Disable();
        inventoryActionMap?.Disable();
        vehicleActionMap?.Disable();
    }

    #endregion

    #region Setup Methods

    private void SetupUIInputActions()
    {
        pauseAction = uiActionMap.FindAction("Pause");
        if (pauseAction == null)
        {
            Debug.LogError("[InputManager] Pause action not found in UI ActionMap!");
        }
    }

    private void SetupCoreMovementInputActions()
    {
        moveAction = coreMovementActionMap.FindAction("Move");
        lookAction = coreMovementActionMap.FindAction("Look");
    }

    private void SetupGroundLocomotionInputActions()
    {
        jumpAction = groundLocomotionActionMap.FindAction("Jump");
        sprintAction = groundLocomotionActionMap.FindAction("Sprint");
        crouchAction = groundLocomotionActionMap.FindAction("Crouch");
    }

    private void SetupSwimmingInputActions()
    {
        surfaceAction = swimmingActionMap.FindAction("Surface");
        swimSpeedAction = swimmingActionMap.FindAction("SwimSpeed");
        diveAction = swimmingActionMap.FindAction("Dive");
    }

    private void SetupVehicleInputActions()
    {
        exitVehicleAction = vehicleActionMap.FindAction("ExitVehicle");
        brakeAction = vehicleActionMap.FindAction("Brake");
    }

    private void SetupGameplayInputActions()
    {
        //Setup proper gameplay actions
        primaryAction = gameplayActionMap.FindAction("PrimaryAction");
        secondaryAction = gameplayActionMap.FindAction("SecondaryAction");
        reloadAction = gameplayActionMap.FindAction("Reload");
        cancelAction = gameplayActionMap.FindAction("CancelAction");

        interactAction = gameplayActionMap.FindAction("Interact");
        leftClickAction = gameplayActionMap.FindAction("LeftClick");
        rightClickAction = gameplayActionMap.FindAction("RightClick");
        scrollWheelAction = gameplayActionMap.FindAction("ScrollWheel");

        // Hotkey actions
        for (int i = 1; i <= 10; i++)
        {
            string actionName = i == 10 ? "Hotkey0" : $"Hotkey{i}";
            hotkeyActions[i - 1] = gameplayActionMap.FindAction(actionName);
        }

        adsAction = gameplayActionMap.FindAction("AimDownSights");

        // Validate that the new actions exist
        if (primaryAction == null)
            Debug.LogError("[InputManager] PrimaryAction not found in Gameplay ActionMap!");
        if (secondaryAction == null)
            Debug.LogError("[InputManager] SecondaryAction not found in Gameplay ActionMap!");
        if (reloadAction == null)
            Debug.LogError("[InputManager] Reload not found in Gameplay ActionMap!");

        if (cancelAction == null)
            Debug.LogError("[InputManager] CancelAction not found in Gameplay ActionMap!");
    }

    private void SetupInventoryInputActions()
    {
        toggleInventoryAction = inventoryActionMap.FindAction("ToggleInventory");
        rotateInventoryItemAction = inventoryActionMap.FindAction("RotateInventoryItem");
    }

    #endregion

    #region Event Management

    private void ClearAllEvents()
    {
        // FIXED: Clear all events properly
        OnJumpPressed = null;
        OnJumpReleased = null;
        OnCrouchPressed = null;
        OnCrouchReleased = null;
        OnSurfacePressed = null;
        OnSurfaceReleased = null;
        OnDivePressed = null;
        OnDiveReleased = null;
        OnPrimaryActionPressed = null;
        OnPrimaryActionReleased = null;
        OnSecondaryActionPressed = null;
        OnSecondaryActionReleased = null;
        OnReloadPressed = null;
        OnCancelActionPressed = null;
        OnInteractPressed = null;
        OnRotateInventoryItemPressed = null;
        OnLeftClickPressed = null;
        OnRightClickPressed = null;
        OnScrollWheelInput = null;
        OnHotkeyPressed = null;
        OnADSHeldStart = null;
        OnADSReleased = null;
    }

    #endregion

    #region Event Subscription

    private void SubscribeToInputActions()
    {
        SubscribeToUIInputActions();
        SubscribeToMovementActions(); // FIXED: Separate movement actions
        SubscribeToGameplayInputActions(); // FIXED: Separate gameplay actions
        SubscribeToInventoryInputActions();
        SubscribeToVehicleInputActions();
    }

    private void SubscribeToUIInputActions()
    {
        if (pauseAction != null)
        {
            pauseAction.performed += OnPausePerformed;
        }
    }

    /// <summary>
    /// FIXED: Subscribe to movement actions (jump, crouch, surface, dive)
    /// </summary>
    private void SubscribeToMovementActions()
    {
        // Ground locomotion actions
        if (jumpAction != null)
        {
            jumpAction.performed += OnJumpPerformed;
            jumpAction.canceled += OnJumpCanceled;
        }

        if (crouchAction != null)
        {
            crouchAction.performed += OnCrouchPerformed;
            crouchAction.canceled += OnCrouchCanceled;
        }

        // Swimming actions
        if (surfaceAction != null)
        {
            surfaceAction.performed += OnSurfacePerformed;
            surfaceAction.canceled += OnSurfaceCanceled;
        }

        if (diveAction != null)
        {
            diveAction.performed += OnDivePerformed;
            diveAction.canceled += OnDiveCanceled;
        }
    }

    /// <summary>
    /// FIXED: Subscribe to gameplay actions (primary, secondary, reload)
    /// </summary>
    private void SubscribeToGameplayInputActions()
    {
        // Primary and secondary actions
        if (primaryAction != null)
        {
            primaryAction.performed += OnPrimaryActionPerformed;
            primaryAction.canceled += OnPrimaryActionCanceled;
        }

        if (secondaryAction != null)
        {
            secondaryAction.performed += OnSecondaryActionPerformed;
            secondaryAction.canceled += OnSecondaryActionCanceled;
        }

        if (reloadAction != null)
        {
            reloadAction.performed += OnReloadPerformed;
        }

        if (cancelAction != null)
        {
            cancelAction.performed += OnCancelActionPerformed;
        }

        // Other gameplay actions
        if (interactAction != null)
        {
            interactAction.performed += OnInteractPerformed;
        }

        if (leftClickAction != null)
        {
            leftClickAction.performed += OnLeftClickPerformed;
        }

        if (rightClickAction != null)
        {
            rightClickAction.performed += OnRightClickPerformed;
        }

        if (scrollWheelAction != null)
        {
            scrollWheelAction.performed += OnScrollWheelPerformed;
        }

        // Subscribe to hotkey actions
        for (int i = 0; i < hotkeyActions.Length; i++)
        {
            if (hotkeyActions[i] != null)
            {
                int slotNumber = i + 1;
                hotkeyActions[i].performed += _ => OnHotkeyPerformed(slotNumber);
            }
        }

        if (adsAction != null)
        {
            adsAction.performed += OnADSPerformed;
            adsAction.canceled += OnADSCanceled;
        }
    }

    private void SubscribeToInventoryInputActions()
    {
        if (toggleInventoryAction != null)
        {
            toggleInventoryAction.performed += OnToggleInventoryPerformed;
        }

        if (rotateInventoryItemAction != null)
        {
            rotateInventoryItemAction.performed += OnRotateInventoryItemPerformed;
        }
    }

    private void SubscribeToVehicleInputActions()
    {
        if (exitVehicleAction != null)
        {
            exitVehicleAction.performed += OnExitVehiclePerformed;
        }

        if (brakeAction != null)
        {
            brakeAction.performed += OnBrakePerformed;
            brakeAction.canceled += OnBrakeCanceled;
        }

    }


    private void UnsubscribeFromInputActions()
    {
        // UI actions
        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePerformed;
        }

        // FIXED: Movement actions
        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPerformed;
            jumpAction.canceled -= OnJumpCanceled;
        }

        if (crouchAction != null)
        {
            crouchAction.performed -= OnCrouchPerformed;
            crouchAction.canceled -= OnCrouchCanceled;
        }

        if (surfaceAction != null)
        {
            surfaceAction.performed -= OnSurfacePerformed;
            surfaceAction.canceled -= OnSurfaceCanceled;
        }

        if (diveAction != null)
        {
            diveAction.performed -= OnDivePerformed;
            diveAction.canceled -= OnDiveCanceled;
        }

        // FIXED: Gameplay actions
        if (primaryAction != null)
        {
            primaryAction.performed -= OnPrimaryActionPerformed;
            primaryAction.canceled -= OnPrimaryActionCanceled;
        }

        if (secondaryAction != null)
        {
            secondaryAction.performed -= OnSecondaryActionPerformed;
            secondaryAction.canceled -= OnSecondaryActionCanceled;
        }

        if (reloadAction != null)
        {
            reloadAction.performed -= OnReloadPerformed;
        }

        if (cancelAction != null)
        {
            cancelAction.performed -= OnCancelActionPerformed;
        }

        // Other gameplay actions
        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPerformed;
        }

        if (leftClickAction != null)
        {
            leftClickAction.performed -= OnLeftClickPerformed;
        }

        if (rightClickAction != null)
        {
            rightClickAction.performed -= OnRightClickPerformed;
        }

        if (scrollWheelAction != null)
        {
            scrollWheelAction.performed -= OnScrollWheelPerformed;
        }

        // Hotkey actions
        for (int i = 0; i < hotkeyActions.Length; i++)
        {
            if (hotkeyActions[i] != null)
            {
                int slotNumber = i + 1;
                hotkeyActions[i].performed -= _ => OnHotkeyPerformed(slotNumber);
            }
        }

        if (adsAction != null)
        {
            adsAction.performed -= OnADSPerformed;
            adsAction.canceled -= OnADSCanceled;
        }

        // Inventory actions
        if (toggleInventoryAction != null)
        {
            toggleInventoryAction.performed -= OnToggleInventoryPerformed;
        }

        if (rotateInventoryItemAction != null)
        {
            rotateInventoryItemAction.performed -= OnRotateInventoryItemPerformed;
        }

        // Vehicle actions
        if (exitVehicleAction != null)
        {
            exitVehicleAction.performed -= OnExitVehiclePerformed;
        }

        if (brakeAction != null)
        {
            brakeAction.performed -= OnBrakePerformed;
            brakeAction.canceled -= OnBrakeCanceled;
        }
    }

    #endregion

    #region Event Handlers

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;

        DebugLog("[InputManager] Pause input detected!");

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.isPaused)
                GameManager.Instance.ResumeGame();
            else
                GameManager.Instance.PauseGame();
        }
        else
        {
            Debug.LogWarning("[InputManager] GameManager.Instance is null - cannot handle pause");
        }
    }

    // FIXED: Movement action handlers
    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        JumpPressed = true;
        OnJumpPressed?.Invoke();
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnJumpReleased?.Invoke();
    }

    private void OnCrouchPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        CrouchPressed = true;
        OnCrouchPressed?.Invoke();
    }

    private void OnCrouchCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnCrouchReleased?.Invoke();
    }

    private void OnSurfacePerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        SurfacePressed = true;
        OnSurfacePressed?.Invoke();
    }

    private void OnSurfaceCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnSurfaceReleased?.Invoke();
    }

    private void OnDivePerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        DivePressed = true;
        OnDivePressed?.Invoke();
    }

    private void OnDiveCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnDiveReleased?.Invoke();
    }

    // FIXED: Gameplay action handlers
    private void OnPrimaryActionPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        PrimaryActionPressed = true;
        OnPrimaryActionPressed?.Invoke();
    }

    private void OnPrimaryActionCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnPrimaryActionReleased?.Invoke();
    }

    private void OnSecondaryActionPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        SecondaryActionPressed = true;
        OnSecondaryActionPressed?.Invoke();
    }

    private void OnSecondaryActionCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnSecondaryActionReleased?.Invoke();
    }

    private void OnReloadPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        ReloadPressed = true;
        OnReloadPressed?.Invoke();
    }

    private void OnCancelActionPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        CancelActionPressed = true;
        OnCancelActionPressed?.Invoke();
    }

    // Other event handlers remain the same
    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnInteractPressed?.Invoke();
    }

    private void OnToggleInventoryPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;

        if (GameManager.Instance?.uiManager != null)
        {
            if (GameManager.Instance.uiManager.isInventoryOpen)
            {
                GameEvents.TriggerInventoryClosed();
            }
            else
            {
                GameEvents.TriggerInventoryOpened();
            }
        }
    }

    private void OnRotateInventoryItemPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnRotateInventoryItemPressed?.Invoke();
    }

    private void OnLeftClickPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnLeftClickPressed?.Invoke();
    }

    private void OnRightClickPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnRightClickPressed?.Invoke();
    }

    private void OnScrollWheelPerformed(InputAction.CallbackContext context)
    {
        Vector2 scrollValue = context.ReadValue<Vector2>();
        OnScrollWheelInput?.Invoke(scrollValue);
    }

    private void OnHotkeyPerformed(int slotNumber)
    {
        if (isCleanedUp) return;
        OnHotkeyPressed?.Invoke(slotNumber);
    }

    private void OnADSPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnADSHeldStart?.Invoke();
    }

    private void OnADSCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnADSReleased?.Invoke();
    }

    private void OnExitVehiclePerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        DebugLog("[InputManager] Exit vehicle input detected!");
        OnExitVehiclePressed?.Invoke();
    }

    private void OnBrakePerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        BrakePressed = true;
        OnBrakePressed?.Invoke();
    }

    private void OnBrakeCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnBrakeReleased?.Invoke();
    }

    #endregion



    #region Update Loop

    private void Update()
    {
        if (isCleanedUp) return;

        // Update input values
        if (coreMovementActionMap?.enabled == true)
            UpdateCoreMovementInputValues();

        UpdateContextualInputValues();
    }

    private void UpdateCoreMovementInputValues()
    {
        MovementInput = moveAction?.ReadValue<Vector2>().normalized ?? Vector2.zero;
        LookInput = lookAction?.ReadValue<Vector2>().normalized ?? Vector2.zero;
    }

    private void UpdateContextualInputValues()
    {
        // Update speed modifier based on current movement mode
        switch (currentMovementMode)
        {
            case MovementMode.Ground:
                SpeedModifierHeld = sprintAction?.IsPressed() ?? false;
                JumpHeld = jumpAction?.IsPressed() ?? false;
                CrouchHeld = crouchAction?.IsPressed() ?? false;
                break;
            case MovementMode.Swimming:
                SpeedModifierHeld = swimSpeedAction?.IsPressed() ?? false;
                SurfaceHeld = surfaceAction?.IsPressed() ?? false;
                DiveHeld = diveAction?.IsPressed() ?? false;
                break;
            case MovementMode.Vehicle:
                BrakeHeld = brakeAction?.IsPressed() ?? false;
                break;
            case MovementMode.Climbing:                                 // just copy ground for now, as player will most likely transition to ground when climbing finishes
                SpeedModifierHeld = sprintAction?.IsPressed() ?? false;
                JumpHeld = jumpAction?.IsPressed() ?? false;
                CrouchHeld = crouchAction?.IsPressed() ?? false;
                break;

        }

        // Update gameplay action held states
        PrimaryActionHeld = primaryAction?.IsPressed() ?? false;
        SecondaryActionHeld = secondaryAction?.IsPressed() ?? false;

        // Reset pressed states after they've been read
        if (JumpPressed) JumpPressed = false;
        if (CrouchPressed) CrouchPressed = false;
        if (SurfacePressed) SurfacePressed = false;
        if (DivePressed) DivePressed = false;
        if (BrakePressed) BrakePressed = false;
        if (PrimaryActionPressed) PrimaryActionPressed = false;
        if (SecondaryActionPressed) SecondaryActionPressed = false;
        if (ReloadPressed) ReloadPressed = false;
        if (CancelActionPressed) CancelActionPressed = false;
    }

    #endregion

    #region Utility Methods

    public void SetInputEnabled(string actionName, bool enabled)
    {
        if (isCleanedUp) return;

        var action = currentMovementActionMap?.FindAction(actionName);
        if (action != null)
        {
            if (enabled)
                action.Enable();
            else
                action.Disable();
        }
    }
    #endregion

    private void OnDestroy()
    {
        if (Instance == this)
        {
            DebugLog("[InputManager] Singleton destroyed");
            Instance = null;
        }
        Cleanup();
    }

    [Button]
    private void CheckWhichInputMapsAreEnabled()
    {
        DebugLog($"UI ActionMap enabled: {uiActionMap?.enabled}");
        DebugLog($"Core Movement ActionMap enabled: {coreMovementActionMap?.enabled}");
        DebugLog($"Ground Locomotion ActionMap enabled: {groundLocomotionActionMap?.enabled}");
        DebugLog($"Swimming ActionMap enabled: {swimmingActionMap?.enabled}");
        DebugLog($"Vehicle ActionMap enabled: {vehicleActionMap?.enabled}");
        DebugLog($"Gameplay ActionMap enabled: {gameplayActionMap?.enabled}");
        DebugLog($"Inventory ActionMap enabled: {inventoryActionMap?.enabled}");
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[InputManager] {message}");
        }
    }
}