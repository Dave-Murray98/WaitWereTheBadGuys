using System;
using System.Text.RegularExpressions;
using NWH.Common.Vehicles;
using RootMotion;
using RootMotion.FinalIK;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// REFACTORED: PlayerController now works seamlessly with PlayerStateManager.
/// UPDATED: Added unified crouching system with single source of truth.
/// </summary>
[RequireComponent(typeof(CameraController))]
[RequireComponent(typeof(PlayerAudio))]
[RequireComponent(typeof(PlayerWaterDetector))]
public class PlayerController : MonoBehaviour
{
    [FoldoutGroup("Core Components")]
    public Rigidbody rb;
    [FoldoutGroup("Core Components")]
    public CapsuleCollider capsuleCollider;

    [FoldoutGroup("Core Components")]
    [SerializeField] private FullBodyBipedIK fbbIK;

    [FoldoutGroup("Core Components")]
    public CameraController cameraController;
    [FoldoutGroup("Core Components")]
    public PlayerAudio playerAudio;
    [FoldoutGroup("Core Components")]
    public PlayerWaterDetector waterDetector;
    [FoldoutGroup("Core Components")]
    public PlayerLedgeDetector ledgeDetector;

    [FoldoutGroup("Movement Controllers")]
    public GroundMovementController groundMovementController;
    [FoldoutGroup("Movement Controllers")]
    public SwimmingMovementController swimmingMovementController;

    [FoldoutGroup("Movement Controllers")]
    public PlayerSwimmingDepthManager swimmingDepthManager;

    [FoldoutGroup("Movement Controllers")]
    public SwimmingBodyRotationController swimmingBodyRotation;

    [FoldoutGroup("Movement Controllers")]
    public VehicleMovementController vehicleMovementController;

    [Header("Vehicle Entry Handler")]
    [SerializeField] private PlayerVehicleEntryExitHandler vehicleEntryExitHandler;

    // Current vehicle reference  
    public VehicleController currentVehicle;

    [FoldoutGroup("Movement Controllers")]
    public ClimbingMovementController climbingMovementController;


    [FoldoutGroup("Animation Handler")]
    [SerializeField] private PlayerAnimationManager animationManager;

    [FoldoutGroup("ItemHandlers")]
    [SerializeField] private ItemHandlerCoordinator itemHandlerCoordinator;

    [FoldoutGroup("Character Model")]
    [SerializeField] private Transform characterModel;

    [FoldoutGroup("Movement Feel")]
    public float waterDrag = 5f;
    [FoldoutGroup("Movement Feel")]
    public float groundDrag = 0.05f;

    [FoldoutGroup("Look Controls")]
    public float horizontalLookSensitivity = 2f;
    public bool invertHorizontalLook = false;
    public float verticalLookSensitivity = 2f;
    public bool invertVerticalLook = false;

    [FoldoutGroup("Look Controls")]
    [SerializeField] private float rotationSmoothTime = 0.1f;
    [SerializeField] private bool useSmoothedRotation = false;

    [Header("Abilities")]
    public bool canMove = true;
    public bool canJump = true;
    public bool canSprint = true;
    public bool canCrouch = true;
    public bool canLook = true;
    public bool canSwim = true;

    [Header("Interaction System")]
    public PlayerInteractionController interactionController;
    public bool canInteract = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // UNIFIED CROUCHING SYSTEM - Single source of truth
    private bool isCrouching = false;

    // Rotation state for smooth horizontal rotation
    private float targetYRotation;
    private float yRotationVelocity;
    private float currentYRotation;

    // Current active movement controller
    private IMovementController currentMovementController;
    private MovementMode currentMovementMode = MovementMode.Ground;

    // Component references
    private PlayerData playerData;

    // Events - simplified to only what's needed
    public event Action<MovementMode, MovementMode> OnMovementModeChanged;
    public event Action<PlayerStateType, PlayerStateType> OnPlayerStateChanged;

    // CROUCHING EVENTS - for systems that need to know about crouch state changes
    public event Action OnStartedCrouching;
    public event Action OnStoppedCrouching;

    // Properties for movement direction calculation
    public Vector3 Forward => transform.forward;
    public Vector3 Right => transform.right;

    // Public accessors for the new state system
    public PlayerStateType CurrentPlayerState => PlayerStateManager.Instance?.CurrentStateType ?? PlayerStateType.Ground;
    public bool IsStateTransitioning => PlayerStateManager.Instance?.IsTransitioning ?? false;
    public MovementMode CurrentMovementMode => currentMovementMode;
    public Transform CharacterModel => characterModel;

    // UNIFIED CROUCHING PROPERTY - This is the single source of truth
    public bool IsCrouching => isCrouching;

    #region Input Events
    public event Action OnPrimaryActionPressed;
    public event Action OnPrimaryActionReleased;
    public event Action OnSecondaryActionPressed;
    public event Action OnSecondaryActionReleased;
    public event Action OnReloadPressed;
    public event Action OnCancelActionPressed;
    #endregion

    private void Awake()
    {
        // Find core components
        FindCoreComponents();

        // Initialize rotation state
        currentYRotation = transform.eulerAngles.y;
        targetYRotation = currentYRotation;
    }

    private void Start()
    {
        Initialize();
        StartCoroutine(DelayedInitializationComplete());
    }

    /// <summary>
    /// Find and setup all core components
    /// </summary>
    private void FindCoreComponents()
    {
        // Auto-find core components if not assigned
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider>();
        if (fbbIK == null) fbbIK = GetComponent<FullBodyBipedIK>();
        if (cameraController == null) cameraController = GetComponent<CameraController>();
        if (playerAudio == null) playerAudio = GetComponent<PlayerAudio>();
        if (waterDetector == null) waterDetector = GetComponent<PlayerWaterDetector>();
        if (ledgeDetector == null) ledgeDetector = GetComponent<PlayerLedgeDetector>();
        if (interactionController == null) interactionController = GetComponent<PlayerInteractionController>();

        // Find movement controllers
        if (groundMovementController == null) groundMovementController = GetComponent<GroundMovementController>();
        if (swimmingMovementController == null) swimmingMovementController = GetComponent<SwimmingMovementController>();
        if (swimmingDepthManager == null) swimmingDepthManager = GetComponent<PlayerSwimmingDepthManager>();
        if (swimmingBodyRotation == null) swimmingBodyRotation = GetComponent<SwimmingBodyRotationController>();
        if (vehicleMovementController == null) vehicleMovementController = GetComponent<VehicleMovementController>();
        if (climbingMovementController == null) climbingMovementController = GetComponent<ClimbingMovementController>();


        // Find animation manager if not assigned
        if (animationManager == null)
            animationManager = GetComponent<PlayerAnimationManager>();

        if (itemHandlerCoordinator == null)
            itemHandlerCoordinator = GetComponent<ItemHandlerCoordinator>();

        // Vehicle components
        if (vehicleMovementController == null)
            vehicleMovementController = GetComponent<VehicleMovementController>();

        DebugLog("Core components found and setup");
    }

    private void Initialize()
    {
        RefreshComponentReferences();

        // Initialize components
        InitializeMovementControllers();
        cameraController.Initialize(this);
        playerAudio.Initialize(this);

        // Initialize state manager integration
        InitializeStateManager();

        // Set initial movement mode
        SetMovementMode(MovementMode.Ground);

        // Subscribe to manager events
        GameManager.OnManagersRefreshed += RefreshComponentReferences;
        InputManager.OnInputManagerReady += OnInputManagerReady;

        // Ensure interaction components exist
        EnsureInteractionComponents();

        DebugLog("PlayerController initialized successfully");
    }

    /// <summary>
    /// Initialize state manager integration
    /// </summary>
    private void InitializeStateManager()
    {
        if (PlayerStateManager.Instance != null)
        {
            // Subscribe to state manager events
            PlayerStateManager.Instance.OnStateChanged += OnStateManagerStateChanged;

            DebugLog("PlayerController integrated with PlayerStateManager");
        }
        else
        {
            Debug.LogError("[PlayerController] PlayerStateManager not found! State-dependent equipment restrictions will not work.");
        }
    }

    /// <summary>
    /// Handle state changes from PlayerStateManager
    /// </summary>
    private void OnStateManagerStateChanged(PlayerStateType previousState, PlayerStateType newState)
    {
        DebugLog($"Player state changed: {previousState} -> {newState}");

        // Forward the event to other systems
        OnPlayerStateChanged?.Invoke(previousState, newState);

        // Update movement mode to match state if needed
        MovementMode targetMode = newState switch
        {
            PlayerStateType.Ground => MovementMode.Ground,
            PlayerStateType.Water => MovementMode.Swimming,
            PlayerStateType.Vehicle => MovementMode.Vehicle,
            PlayerStateType.Climbing => MovementMode.Climbing,
            _ => MovementMode.Ground
        };

        if (targetMode != currentMovementMode)
        {
            SetMovementMode(targetMode);
        }
    }

    /// <summary>
    /// Initialize movement controllers with proper enable/disable management
    /// </summary>
    private void InitializeMovementControllers()
    {
        // Initialize all movement controllers
        groundMovementController?.Initialize(this);
        swimmingMovementController?.Initialize(this);
        vehicleMovementController?.Initialize(this);
        groundMovementController?.Initialize(this);

        // Disable all controllers initially for clean state
        if (groundMovementController is MonoBehaviour groundComp)
            groundComp.enabled = false;
        if (swimmingMovementController is MonoBehaviour swimmingComp)
            swimmingComp.enabled = false;
        if (vehicleMovementController is MonoBehaviour vehicleComp)
            vehicleComp.enabled = false;
        if (climbingMovementController is MonoBehaviour climbingComp)
            climbingComp.enabled = false;

        // Set initial controller and enable only that one
        currentMovementController = groundMovementController;
        if (currentMovementController is MonoBehaviour activeComp)
        {
            activeComp.enabled = true;
            currentMovementController.OnControllerActivated();
        }

        DebugLog("Movement controllers initialized");
    }

    /// <summary>
    /// Ensure interaction components exist
    /// </summary>
    private void EnsureInteractionComponents()
    {
        if (interactionController == null)
            interactionController = gameObject.AddComponent<PlayerInteractionController>();

        PlayerInteractionDetector interactionDetector = GetComponent<PlayerInteractionDetector>();
        if (interactionDetector == null)
            interactionDetector = gameObject.AddComponent<PlayerInteractionDetector>();
    }

    /// <summary>
    /// Complete initialization after all systems are ready
    /// </summary>
    private System.Collections.IEnumerator DelayedInitializationComplete()
    {
        yield return new WaitForSecondsRealtime(0.2f);
        DebugLog("PlayerController fully initialized");
    }

    private void OnInputManagerReady(InputManager newInputManager)
    {
        ConnectToInputManager(newInputManager);
    }

    private void RefreshComponentReferences()
    {
        playerData = GameManager.Instance?.playerData;

        if (playerData != null)
        {
            RefreshCameraControllerReferences();
        }

        // Connect to InputManager
        if (InputManager.Instance != null)
        {
            ConnectToInputManager(InputManager.Instance);
        }
    }

    private void RefreshCameraControllerReferences()
    {
        horizontalLookSensitivity = playerData.lookSensitivity;
        cameraController.SetVerticalLookSensitivity(playerData.lookSensitivity);
    }

    /// <summary>
    /// Connect to InputManager with separated action subscriptions
    /// </summary>
    private void ConnectToInputManager(InputManager inputManager)
    {
        DisconnectFromInputManager();

        if (inputManager != null)
        {
            // Subscribe to separated movement actions
            inputManager.OnJumpPressed += HandleJumpInput;
            inputManager.OnJumpReleased += HandleJumpReleased;
            inputManager.OnCrouchPressed += HandleCrouchInput;
            inputManager.OnCrouchReleased += HandleCrouchReleased;
            inputManager.OnSurfacePressed += HandleSurfaceInput;
            inputManager.OnSurfaceReleased += HandleSurfaceReleased;
            inputManager.OnDivePressed += HandleDiveInput;
            inputManager.OnDiveReleased += HandleDiveReleased;
            inputManager.OnBrakePressed += HandleBrakeInput;
            inputManager.OnBrakeReleased += HandleBrakeReleased;

            // Subscribe to separated gameplay actions
            inputManager.OnPrimaryActionPressed += HandlePrimaryActionInput;
            inputManager.OnPrimaryActionReleased += HandlePrimaryActionReleased;
            inputManager.OnSecondaryActionPressed += HandleSecondaryActionInput;
            inputManager.OnSecondaryActionReleased += HandleSecondaryActionReleased;
            inputManager.OnReloadPressed += HandleReloadInput;
            inputManager.OnCancelActionPressed += HandleCancelInput;
            inputManager.OnExitVehiclePressed += HandleExitVehicleInput;

            DebugLog("Connected to InputManager with separated actions");
        }
    }

    /// <summary>
    /// Disconnect from InputManager
    /// </summary>
    private void DisconnectFromInputManager()
    {
        if (InputManager.Instance != null)
        {
            // FIXED: Unsubscribe from separated movement actions
            InputManager.Instance.OnJumpPressed -= HandleJumpInput;
            InputManager.Instance.OnJumpReleased -= HandleJumpReleased;
            InputManager.Instance.OnCrouchPressed -= HandleCrouchInput;
            InputManager.Instance.OnCrouchReleased -= HandleCrouchReleased;
            InputManager.Instance.OnSurfacePressed -= HandleSurfaceInput;
            InputManager.Instance.OnSurfaceReleased -= HandleSurfaceReleased;
            InputManager.Instance.OnDivePressed -= HandleDiveInput;
            InputManager.Instance.OnDiveReleased -= HandleDiveReleased;
            InputManager.Instance.OnBrakePressed -= HandleBrakeInput;
            InputManager.Instance.OnBrakeReleased -= HandleBrakeReleased;

            // FIXED: Unsubscribe from separated gameplay actions
            InputManager.Instance.OnPrimaryActionPressed -= HandlePrimaryActionInput;
            InputManager.Instance.OnPrimaryActionReleased -= HandlePrimaryActionReleased;
            InputManager.Instance.OnSecondaryActionPressed -= HandleSecondaryActionInput;
            InputManager.Instance.OnSecondaryActionReleased -= HandleSecondaryActionReleased;
            InputManager.Instance.OnReloadPressed -= HandleReloadInput;
            InputManager.Instance.OnCancelActionPressed -= HandleCancelInput;
            InputManager.Instance.OnExitVehiclePressed -= HandleExitVehicleInput;
        }
    }

    private void Update()
    {
        if (!GameManager.Instance || GameManager.Instance.isPaused) return;

        HandleInput();
        UpdateHorizontalRotation();

        // UPDATED: Pass unified crouch state to animation system
        if (animationManager != null && animationManager.IsInitialized)
        {
            Vector2 input = InputManager.Instance.MovementInput;
            bool isRunning = IsSprinting;

            // Use our unified crouching state instead of InputManager's held state
            animationManager.UpdateLocomotion(input, isCrouching, isRunning);
        }
    }

    /// <summary>
    /// Handle all input processing
    /// </summary>
    private void HandleInput()
    {
        if (InputManager.Instance == null || currentMovementController == null) return;

        // Movement input
        if (canMove)
        {
            bool speedModifier = GetSpeedModifierForCurrentMode();
            currentMovementController.HandleMovement(InputManager.Instance.MovementInput, speedModifier);
        }

        // Look input
        if (canLook && InputManager.Instance.LookInput.magnitude > 0.01f)
        {
            cameraController.SetLookInput(InputManager.Instance.LookInput);
            HandleHorizontalLookInput(InputManager.Instance.LookInput.x);
        }
        else
        {
            cameraController.SetLookInput(Vector2.zero);
        }
    }

    /// <summary>
    /// Get appropriate speed modifier based on current movement mode
    /// </summary>
    private bool GetSpeedModifierForCurrentMode()
    {
        return currentMovementMode switch
        {
            MovementMode.Ground => canSprint && InputManager.Instance.SpeedModifierHeld,
            MovementMode.Swimming => canSwim && InputManager.Instance.SpeedModifierHeld,
            MovementMode.Vehicle => InputManager.Instance.SpeedModifierHeld, // For future vehicle system
            _ => false
        };
    }

    /// <summary>
    /// Handle horizontal look input for player body rotation
    /// </summary>
    private void HandleHorizontalLookInput(float horizontalInput)
    {
        if (Mathf.Abs(horizontalInput) < 0.01f) return;

        float sensitivity = playerData?.lookSensitivity ?? horizontalLookSensitivity;

        if (invertHorizontalLook)
            horizontalInput = -horizontalInput;

        float rotationDelta = horizontalInput * sensitivity * Time.deltaTime * 100f;
        targetYRotation += rotationDelta;
        targetYRotation = NormalizeAngle(targetYRotation);
    }

    /// <summary>
    /// FIXED: Update horizontal rotation with swimming body rotation preservation
    /// </summary>
    private void UpdateHorizontalRotation()
    {

        float targetRotationY;

        if (useSmoothedRotation)
        {
            targetRotationY = Mathf.SmoothDampAngle(
                currentYRotation,
                targetYRotation,
                ref yRotationVelocity,
                rotationSmoothTime
            );
        }
        else
        {
            targetRotationY = targetYRotation;
        }

        currentYRotation = targetRotationY;

        currentMovementController.HandleHorizontalRotation(currentYRotation);

    }

    /// <summary>
    /// Normalize angle to -180 to 180 range
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        angle = angle % 360f;
        if (angle > 180f) angle -= 360f;
        else if (angle < -180f) angle += 360f;
        return angle;
    }

    #region Unified Crouching System

    /// <summary>
    /// UNIFIED CROUCH CONTROL - Toggle crouch state (single source of truth)
    /// </summary>
    private void ToggleCrouch()
    {
        if (!canCrouch) return;

        if (!isCrouching)
        {
            StartCrouch();
        }
        else
        {
            StopCrouch();
        }
    }

    /// <summary>
    /// Start crouching - checks if it's possible first
    /// </summary>
    private void StartCrouch()
    {
        if (isCrouching) return;

        // Check if we can start crouching (ground movement controller validation)
        if (currentMovementMode == MovementMode.Ground && groundMovementController != null)
        {
            // For ground movement, we can always start crouching
            SetCrouchState(true);
        }
        else if (currentMovementMode == MovementMode.Swimming)
        {
            // For swimming, crouching acts as diving
            SetCrouchState(true);
        }
    }

    /// <summary>
    /// Stop crouching - checks if it's safe first
    /// </summary>
    private void StopCrouch()
    {
        if (!isCrouching) return;

        // Check if we can stand up (only relevant for ground movement)
        if (currentMovementMode == MovementMode.Ground)
        {
            // Ask the ground movement controller if we can stand up
            // We'll need to add a CanStandUp method to the interface
            if (groundMovementController != null)
            {
                // For now, always allow stopping crouch - the movement controller will handle safety
                SetCrouchState(false);
            }
        }
        else
        {
            // For swimming or other modes, always allow stopping crouch
            SetCrouchState(false);
        }
    }

    /// <summary>
    /// Set the crouch state and notify all systems
    /// </summary>
    private void SetCrouchState(bool crouchState)
    {
        if (isCrouching == crouchState) return;

        bool previousCrouchState = isCrouching;
        isCrouching = crouchState;

        DebugLog($"Crouch state changed: {previousCrouchState} -> {isCrouching}");

        // Fire events for other systems to react
        if (isCrouching)
        {
            OnStartedCrouching?.Invoke();
        }
        else
        {
            OnStoppedCrouching?.Invoke();
        }

        // Notify the current movement controller about the crouch state change
        // The movement controller will handle the actual physics/collider changes
        if (currentMovementController is GroundMovementController groundController)
        {
            if (isCrouching)
            {
                groundController.StartCrouch();
            }
            else
            {
                groundController.StopCrouch();
            }
        }
    }

    /// <summary>
    /// Force set crouch state (for save/load or external systems)
    /// </summary>
    public void SetCrouchingState(bool crouchState)
    {
        SetCrouchState(crouchState);
    }

    #endregion

    #region Movement Mode Management

    /// <summary>
    /// Switch between movement modes with proper controller management
    /// </summary>
    private void SetMovementMode(MovementMode newMode)
    {
        if (currentMovementMode == newMode) return;

        MovementMode previousMode = currentMovementMode;
        DebugLog($"Switching movement mode: {previousMode} -> {newMode}");

        // Deactivate current controller
        if (currentMovementController is MonoBehaviour currentComponent)
        {
            currentComponent.enabled = false;
            currentMovementController.OnControllerDeactivated();
        }

        // Switch to new controller
        currentMovementController = newMode switch
        {
            MovementMode.Ground => groundMovementController,
            MovementMode.Swimming => swimmingMovementController,
            MovementMode.Vehicle => vehicleMovementController,
            MovementMode.Climbing => climbingMovementController,
            _ => groundMovementController
        };

        currentMovementMode = newMode;

        // Activate new controller
        if (currentMovementController is MonoBehaviour newComponent)
        {
            newComponent.enabled = true;
            currentMovementController.OnControllerActivated();
        }

        // Update InputManager
        InputManager.Instance?.SetMovementMode(newMode);

        OnMovementModeChanged?.Invoke(previousMode, newMode);
        DebugLog($"Movement mode change complete: {previousMode} -> {newMode}");
    }

    /// <summary>
    /// Set initial movement mode for save/load operations
    /// </summary>
    public void SetInitialMovementMode(MovementMode mode)
    {
        SetMovementMode(mode);
    }

    #endregion

    #region Input Handlers

    private void HandleJumpInput()
    {
        if (currentMovementController == null || !canJump) return;

        Debug.Log("Handle Jump Input");
        // CLIMBING INTEGRATION: Check for ledge climb before normal jump
        if (CanStartClimbing())
        {
            InitiateClimbing();
            return;
        }

        // Normal jump handling for ground movement
        if (currentMovementMode == MovementMode.Ground)
        {
            currentMovementController.HandlePrimaryAction(); // Jump is primary action for ground movement
            DebugLog("Jump action triggered");
        }
    }

    private void HandleJumpReleased()
    {
        currentMovementController?.HandlePrimaryActionReleased();
    }

    /// <summary>
    /// UPDATED: Handle crouch input using unified toggle system
    /// </summary>
    private void HandleCrouchInput()
    {
        if (!canCrouch) return;

        if (currentMovementMode == MovementMode.Ground)
        {
            // Use unified toggle crouch instead of calling movement controller directly
            ToggleCrouch();
            DebugLog("Crouch toggle triggered");
        }
    }

    private void HandleCrouchReleased()
    {
        // For toggle-based crouching, we don't need to do anything on release
        // The next press will toggle the state
    }

    private void HandleSurfaceInput()
    {
        if (currentMovementController == null || !canSwim) return;

        Debug.Log("Handle Surface Input");
        // CLIMBING INTEGRATION: Check for ledge climb before normal surface
        if (CanStartClimbing())
        {
            InitiateClimbing();
            return;
        }

        if (currentMovementMode == MovementMode.Swimming)
        {
            currentMovementController.HandlePrimaryAction(); // Surface is primary action for swimming
            DebugLog("Surface action triggered");
        }
    }

    private void HandleSurfaceReleased()
    {
        currentMovementController?.HandlePrimaryActionReleased();
    }

    private void HandleDiveInput()
    {
        if (currentMovementController == null || !canSwim) return;

        if (currentMovementMode == MovementMode.Swimming)
        {
            // For swimming, diving could be toggle-based too, but let's keep it as is for now
            currentMovementController.HandleSecondaryAction(); // Dive is secondary action for swimming
            DebugLog("Dive action triggered");
        }
    }

    private void HandleDiveReleased()
    {
        currentMovementController?.HandleSecondaryActionReleased();
    }

    private void HandleExitVehicleInput()
    {
        DebugLog("Handle exit vehicle input triggered");

        if (CurrentPlayerState == PlayerStateType.Vehicle && currentVehicle != null)
        {
            // Check if handler can process the exit
            if (vehicleEntryExitHandler != null && vehicleEntryExitHandler.CanProcessVehicleOperations())
            {
                // Use the handler
                vehicleEntryExitHandler.BeginVehicleExit();
                DebugLog("Exit vehicle input triggered via handler");
            }
            else
            {
                Debug.LogWarning("[PlayerController] Vehicle exit handler not available");
            }

        }
        else
        {
            DebugLog("Either not in vehicle state or currentVehicle is null");
        }
    }
    private void HandleBrakeInput()
    {
        if (currentMovementController == null) return;

        if (currentMovementMode == MovementMode.Vehicle)
        {
            currentMovementController.HandleSecondaryAction();
            DebugLog("Brake action triggered");
        }
    }

    private void HandleBrakeReleased()
    {
        if (currentMovementController == null) return;

        if (currentMovementMode == MovementMode.Vehicle)
            currentMovementController.HandleSecondaryActionReleased();
        DebugLog("Brake action released");
    }

    private void HandlePrimaryActionInput()
    {
        // Trigger primary action animation (shoot, consume, use, etc.)
        // Notify item handler coordinator to handle primary action
        OnPrimaryActionPressed?.Invoke();
        DebugLog("Primary action triggered (shoot/use/consume)");
    }

    private void HandlePrimaryActionReleased()
    {
        // Handle primary action release if needed
        //notify item handler coordinator to release primary action
        OnPrimaryActionReleased?.Invoke();
        DebugLog("Primary action released");
    }

    private void HandleSecondaryActionInput()
    {
        // Trigger secondary action animation (aim, reload, alt use, etc.)
        // Notify item handler coordinator to handle secondary action
        OnSecondaryActionPressed?.Invoke();
        DebugLog("Secondary action triggered (aim/reload/alt use)");
    }

    private void HandleSecondaryActionReleased()
    {
        // Handle secondary action release if needed
        //notify item handler coordinator to release secondary action
        OnSecondaryActionReleased?.Invoke();
        DebugLog("Secondary action released");
    }

    private void HandleReloadInput()
    {
        // Trigger reload action animation
        // Notify item handler coordinator to handle reload
        OnReloadPressed?.Invoke();
        DebugLog("Reload action triggered");
    }

    private void HandleCancelInput()
    {
        // Notify item handler coordinator to handle cancel action
        OnCancelActionPressed?.Invoke();
        DebugLog("Cancel action triggered");
    }

    #endregion

    #region State System Integration

    /// <summary>
    /// Check if an item can be used in the current player state
    /// </summary>
    public bool CanUseItemInCurrentState(ItemData itemData)
    {
        return PlayerStateManager.Instance?.CanUseItem(itemData) ?? true;
    }

    /// <summary>
    /// Check if an item can be equipped in the current player state
    /// </summary>
    public bool CanEquipItemInCurrentState(ItemData itemData)
    {
        return PlayerStateManager.Instance?.CanEquipItem(itemData) ?? true;
    }

    /// <summary>
    /// Get movement restrictions for the current state
    /// </summary>
    public MovementRestrictions GetCurrentMovementRestrictions()
    {
        return PlayerStateManager.Instance?.GetMovementRestrictions() ?? MovementRestrictions.CreateGroundRestrictions();
    }

    /// <summary>
    /// Force a validation of the current player state
    /// </summary>
    public void ValidatePlayerState()
    {
        PlayerStateManager.Instance?.ForceStateValidation();
    }

    #endregion

    #region Vehicle Integration

    /// <summary>
    /// Called when player enters a vehicle
    /// This is called directly by the VehicleInteractable
    /// </summary>
    public bool EnterVehicle(VehicleController vehicle)
    {
        if (vehicle == null)
        {
            DebugLog("Vehicle reference is null");
            return false;
        }

        // Check if we can enter the vehicle
        if (currentVehicle != null)
        {
            DebugLog("Player already in a vehicle");
            return false;
        }

        if (!vehicle.IsOperational)
        {
            DebugLog("Vehicle is not operational");
            return false;
        }

        // Use the handler for proper state management
        if (vehicleEntryExitHandler != null && vehicleEntryExitHandler.CanProcessVehicleOperations())
        {
            DebugLog($"Delegating vehicle entry to handler for vehicle: {vehicle.VehicleID}");
            if (vehicleEntryExitHandler.BeginVehicleEntry(vehicle))
            {
                // Hook up vehicle exit handlers
                vehicle.OnPlayerExited += OnVehiclePlayerExited;
                return true;
            }
            else
            {
                DebugLog("Vehicle entry rejected");
                return false;
            }
        }
        else
        {
            Debug.LogWarning("[PlayerController] Vehicle entry handler not available");
            return false;
        }
    }
    /// <summary>
    /// Called when player exits vehicle
    /// </summary>
    public void ExitVehicle()
    {
        if (currentVehicle == null) return;

        // Use the handler for proper state management
        if (vehicleEntryExitHandler != null && vehicleEntryExitHandler.CanProcessVehicleOperations())
        {
            DebugLog($"Delegating vehicle exit to handler for vehicle: {currentVehicle.VehicleID}");
            vehicleEntryExitHandler.BeginVehicleExit();
        }
        else
        {
            Debug.LogWarning("[PlayerController] Vehicle exit handler not available - falling back to direct exit");
        }
    }

    /// <summary>
    /// Event handler when vehicle reports player exited
    /// </summary>
    public void OnVehiclePlayerExited(VehicleController vehicle, GameObject player)
    {
        if (vehicle == currentVehicle && player == gameObject)
        {
            // Vehicle initiated the exit (e.g., vehicle destroyed)
            // Clean up our references
            currentVehicle.OnPlayerExited -= OnVehiclePlayerExited;
            currentVehicle.OnVehicleDestroyed -= OnVehicleDestroyed;

            if (vehicleMovementController != null)
            {
                vehicleMovementController.ClearCurrentVehicle();
            }

            DebugLog($"PlayerController.OnVehiclePlayerExited: setting current vehicle to null");
            currentVehicle = null;

            DebugLog($"Vehicle {vehicle.VehicleID} initiated player exit");
        }
    }

    /// <summary>
    /// ADD TO PlayerController - Event handler when vehicle is destroyed
    /// </summary>
    private void OnVehicleDestroyed(VehicleController vehicle)
    {
        if (vehicle == currentVehicle)
        {
            DebugLog($"Current vehicle {vehicle.VehicleID} was destroyed");

            // Clean up references - the vehicle exit was already handled
            if (vehicleMovementController != null)
            {
                vehicleMovementController.ClearCurrentVehicle();
            }

            currentVehicle = null;
        }
    }

    #endregion

    #region Climbing Integration

    /// <summary>
    /// Check if player can start climbing
    /// </summary>
    private bool CanStartClimbing()
    {
        if (IsStateTransitioning)
        {
            Debug.Log("IsStateTransitioning, returning false");
            return false;
        }

        // Must be on ground and not already climbing
        if (CurrentPlayerState == PlayerStateType.Vehicle && CurrentPlayerState == PlayerStateType.Climbing)
        {
            Debug.Log("either is currently in vehicle or already climbing: returning false");
            return false;
        }

        // Must have valid ledge detected
        if (ledgeDetector == null || !ledgeDetector.HasValidLedge)
        {
            Debug.Log("ledge detector is either null or doesn't have ledge, returning false");
            return false;
        }

        // Check if climbing movement controller is available
        if (climbingMovementController == null)
        {
            return false;
        }

        return climbingMovementController.CanStartClimbing();
    }

    /// <summary>
    /// Initiate the climbing sequence
    /// </summary>
    private void InitiateClimbing()
    {
        DebugLog("=== INITIATING CLIMBING SEQUENCE ===");

        if (!CanStartClimbing())
        {
            DebugLog("Cannot start climbing - conditions not met");
            return;
        }

        // Request state change to climbing
        if (PlayerStateManager.Instance != null)
        {
            DebugLog("Requesting climb state transition");
            PlayerStateManager.Instance.ChangeToState(PlayerStateType.Climbing);

            // Subscribe to state change to start climb when state is ready
            PlayerStateManager.Instance.OnStateChanged += OnClimbStateChanged;
        }
        else
        {
            Debug.LogError("[PlayerController] PlayerStateManager not available for climbing");
        }
    }

    /// <summary>
    /// Handle state change for climbing initiation
    /// </summary>
    private void OnClimbStateChanged(PlayerStateType previousState, PlayerStateType newState)
    {
        if (newState == PlayerStateType.Climbing)
        {
            DebugLog("Climb state entered - starting climb sequence");

            // Unsubscribe from state change event
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnStateChanged -= OnClimbStateChanged;
            }

            // Start the actual climbing sequence
            StartClimbSequence();
        }
        else if (previousState == PlayerStateType.Climbing && newState == PlayerStateType.Ground)
        {
            DebugLog("Climb state exited - climb sequence completed");
            OnClimbSequenceCompleted();
        }
    }

    /// <summary>
    /// Start the climb sequence (called when climb state is active)
    /// </summary>
    private void StartClimbSequence()
    {
        DebugLog("Starting climb sequence");

        // Get the climbing state and start climbing
        if (PlayerStateManager.Instance?.CurrentState is ClimbingState climbState)
        {
            bool climbStarted = climbState.StartClimbing();

            if (!climbStarted)
            {
                Debug.LogError("[PlayerController] Failed to start climbing movement");
                // Return to ground state if climb failed
                PlayerStateManager.Instance?.ChangeToState(PlayerStateType.Ground, true);
            }
            else
            {
                DebugLog("Climb sequence started successfully");
                // The animation will be played automatically by the normal locomotion system
                // when UpdateLocomotion is called with PlayerStateType.Climbing
            }
        }
        else
        {
            Debug.LogError("[PlayerController] Not in climbing state when trying to start climb");
        }
    }

    /// <summary>
    /// Called when climb sequence is completed and back to ground
    /// </summary>
    private void OnClimbSequenceCompleted()
    {
        DebugLog("Climb sequence fully completed - player back on ground");

        // Could add any additional cleanup or effects here
        // The state machine and movement controllers handle the main restoration
    }

    /// <summary>
    /// Force cancel climbing (for edge cases)
    /// </summary>
    public void CancelClimbing()
    {
        if (CurrentPlayerState == PlayerStateType.Climbing)
        {
            DebugLog("Force canceling climb sequence");

            // Stop climb animation
            if (animationManager != null)
            {
                animationManager.StopClimbAnimation();
            }

            // Return to ground state
            PlayerStateManager.Instance?.ChangeToState(PlayerStateType.Ground);
        }
    }


    #endregion

    #region Updated Public Properties and Getters

    // State getters

    //ground getters
    public bool IsMoving => currentMovementController?.IsMoving ?? false;
    public bool IsGrounded => currentMovementController?.IsGrounded ?? false;
    public bool IsSprinting => currentMovementController?.IsSpeedModified ?? false;

    //swimming getters
    public bool IsSwimming => currentMovementMode == MovementMode.Swimming;

    public bool IsDiving =>
        (currentMovementMode == MovementMode.Swimming) &&
        (InputManager.Instance?.DiveHeld ?? false);

    public bool IsInWater => waterDetector?.IsInWaterState ?? false;

    // Vehicle getters
    public IVehicle GetCurrentVehicle() => currentVehicle;
    public bool IsInVehicle => currentVehicle != null;
    public Vector3 Velocity => currentMovementController?.GetVelocity() ?? Vector3.zero;

    //Climbing Getters
    public bool IsClimbing => CurrentPlayerState == PlayerStateType.Climbing;
    public bool CanClimb => CanStartClimbing();

    // Getters for gameplay actions
    public bool IsPrimaryActionHeld => InputManager.Instance?.PrimaryActionHeld ?? false;
    public bool IsSecondaryActionHeld => InputManager.Instance?.SecondaryActionHeld ?? false;

    // Ability controls
    public void SetMovementEnabled(bool enabled) => canMove = enabled;
    public void SetJumpEnabled(bool enabled) => canJump = enabled;
    public void SetSprintEnabled(bool enabled) => canSprint = enabled;
    public void SetCrouchEnabled(bool enabled) => canCrouch = enabled;
    public void SetLookEnabled(bool enabled) => canLook = enabled;
    public void SetSwimmingEnabled(bool enabled) => canSwim = enabled;

    // Look sensitivity controls
    public void SetHorizontalLookSensitivity(float sensitivity) => horizontalLookSensitivity = sensitivity;
    public float GetHorizontalLookSensitivity() => horizontalLookSensitivity;

    //physics controls
    public void SetRBUseGravity(bool useGravity)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.useGravity = useGravity;
    }

    public void SetRBIsKinematic(bool isKinematic)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        rb.isKinematic = isKinematic;
    }

    /// <summary>
    /// Sets the player's left and right hand effector targets and weights, can also be used to disable them
    /// </summary>
    /// <param name="leftHandEffector"></param>
    /// <param name="rightHandEffector"></param>
    /// <param name="enabled"></param>
    public void SetPlayerLeftHandAndRightHandIKEffectors(Transform leftHandEffector, Transform rightHandEffector, bool enabled, bool matchRotationToo = false)
    {
        if (enabled)
        {
            fbbIK.solver.leftHandEffector.target = leftHandEffector;
            fbbIK.solver.rightHandEffector.target = rightHandEffector;

            fbbIK.solver.leftHandEffector.positionWeight = 1f;
            fbbIK.solver.rightHandEffector.positionWeight = 1f;
            if (matchRotationToo)
            {
                fbbIK.solver.leftHandEffector.rotationWeight = 1f;
                fbbIK.solver.rightHandEffector.rotationWeight = 1f;
            }
        }
        else
        {

            fbbIK.solver.leftHandEffector.positionWeight = 0f;
            fbbIK.solver.leftHandEffector.rotationWeight = 0f;
            fbbIK.solver.rightHandEffector.positionWeight = 0f;
            fbbIK.solver.rightHandEffector.rotationWeight = 0f;
            fbbIK.solver.leftHandEffector.target = null;
            fbbIK.solver.rightHandEffector.target = null;
        }
    }

    /// <summary>
    /// Set whether the player should collide with a specific layer
    /// </summary>
    /// <param name="layer">The layer to modify collision for</param>
    /// <param name="collide">True to enable collision, false to exclude collision</param>
    public void SetLayerCollision(LayerMask layer, bool collide)
    {
        if (collide)
        {
            // Remove layer from exclude list (enable collision)
            rb.excludeLayers &= ~layer;
            capsuleCollider.excludeLayers &= ~layer;
        }
        else
        {
            // Add layer to exclude list (disable collision)
            rb.excludeLayers |= layer;
            capsuleCollider.excludeLayers |= layer;
        }

        DebugLog($"Layer collision updated - Layer: {layer}, Collide: {collide}");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerController] {message}");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        GameManager.OnManagersRefreshed -= RefreshComponentReferences;
        InputManager.OnInputManagerReady -= OnInputManagerReady;
        DisconnectFromInputManager();

        // Unsubscribe from state manager
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateChanged -= OnStateManagerStateChanged;
        }

        // Cleanup movement controllers
        currentMovementController?.Cleanup();
    }

    /// <summary>
    /// Get vehicle debug info
    /// </summary>
    public string GetVehicleDebugInfo()
    {
        if (currentVehicle == null)
            return "Not in vehicle";

        return $"Vehicle: {currentVehicle.VehicleID} ({currentVehicle.VehicleType}), " +
               $"Speed: {currentVehicle.Velocity.magnitude:F1}m/s, " +
               $"State: {CurrentPlayerState}";
    }
}

public enum GroundType
{
    Default,
    Grass,
    Stone,
    Metal,
    Wood,
    Water
}