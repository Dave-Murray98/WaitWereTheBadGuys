using UnityEngine;
using Unity.Cinemachine;
using Sirenix.OdinInspector;
using System;

/// <summary>
/// ENHANCED: Camera controller with vehicle horizontal aiming support.
/// Now follows both horizontal and vertical aiming when in vehicle mode,
/// while maintaining existing ground/swimming behavior.
/// </summary>
public class CameraController : MonoBehaviour
{
    [FoldoutGroup("Core Components")]
    [Tooltip("The Cinemachine virtual camera")]
    [SerializeField] private CinemachineVirtualCameraBase virtualCamera;

    [FoldoutGroup("Core Components")]
    [Tooltip("The camera that renders the player's body and equipped in front of the world (prevents hands and equipment from clipping through walls) by only rendering the player's body and item (while the main camera renders everything else) and then outputting it's background as a transparent render texture that is rendered as a raw image in the canvas and scaled to fit the screen")]
    [SerializeField] private Camera firstPersonRenderCamera;

    [FoldoutGroup("Core Components")]
    [Tooltip("Transform representing the player's eye position (child of head bone)")]
    [SerializeField] private Transform cameraRoot;

    [FoldoutGroup("Subsystems")]
    [Tooltip("Handles vertical aiming and IK integration")]
    public AimController aimController;

    [FoldoutGroup("Subsystems")]
    [Tooltip("Handles aiming down sights functionality")]
    [SerializeField] private ADSController adsController;

    [FoldoutGroup("Subsystems")]
    [Tooltip("Handles field of view effects")]
    [SerializeField] private FOVController fovController;

    [FoldoutGroup("Subsystems")]
    [Tooltip("Handles camera effects like shake, etc.")]
    [SerializeField] private CameraEffectsController effectsController;

    [FoldoutGroup("Subsystems")]
    [Tooltip("Handles weapon sway")]
    [SerializeField] private WeaponSwayController swayController;

    [FoldoutGroup("Swimming Integration")]
    [Tooltip("Swimming body rotation controller for state awareness")]
    [SerializeField] private SwimmingBodyRotationController swimmingBodyRotation;

    [FoldoutGroup("Transform Smoothing")]
    [Tooltip("Smoothing factor for vertical camera rotation (pitch and roll)")]
    [SerializeField] private float cameraVerticalRotationSmoothing = 10f;
    [FoldoutGroup("Transform Smoothing")]
    [Tooltip("Smoothing factor for camera position updates")]
    [SerializeField] private float cameraPositionSmoothing = 10f;

    [FoldoutGroup("ENHANCED: Vehicle Integration")]
    [Tooltip("Smoothing time for vehicle camera rotation transitions")]
    [SerializeField] private float vehicleCameraTransitionTime = 0.15f;

    [FoldoutGroup("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showCameraCalculations = false;
    [SerializeField] private bool showADSOffsetInfo = false;
    [SerializeField] private bool showSwimmingStateInfo = false;
    [SerializeField] private bool showVehicleAimingInfo = false; // ENHANCED: New debug option



    // Component references
    private PlayerController playerController;

    // Input state
    private Vector2 currentLookInput;
    private bool isInitialized = false;

    // Component-wise rotation system to prevent somersaults
    private Vector3 currentADSRotationOffset = Vector3.zero;
    private Vector3 targetADSRotationOffset = Vector3.zero;
    private bool isADSOffsetTransitioning = false;

    // Component-wise rotation tracking
    private float currentCameraPitch = 0f;
    private float currentCameraYaw = 0f;
    private float currentCameraRoll = 0f;
    private float targetCameraPitch = 0f;
    private float targetCameraYaw = 0f;
    private float targetCameraRoll = 0f;

    // Camera state tracking
    private bool isADSActive = false;
    private float currentADSSensitivityMultiplier = 1f;

    // Swimming state tracking
    private bool isSwimming = false;
    private bool isSwimmingBodyRotationActive = false;
    private MovementMode lastMovementMode = MovementMode.Ground;

    // ENHANCED: Vehicle state tracking
    private bool isInVehicle = false;
    private PlayerStateType currentPlayerState = PlayerStateType.Ground;
    private bool vehicleHorizontalAimingEnabled = false;

    // ENHANCED: Vehicle camera rotation tracking
    private float vehicleCameraYawVelocity = 0f;
    private bool isVehicleCameraTransitioning = false;

    // Events
    public event Action<Vector2> OnLookInputChanged;
    public event Action<Vector3> OnADSRotationOffsetChanged;
    public event Action<bool> OnADSStateChanged;
    public event Action<bool> OnVehicleAimingStateChanged; // ENHANCED: New event

    #region Public Properties

    /// <summary>Player's forward direction (horizontal only)</summary>
    public Vector3 Forward => playerController != null ? playerController.Forward : transform.forward;

    /// <summary>Player's right direction (horizontal only)</summary>
    public Vector3 Right => playerController != null ? playerController.Right : transform.right;

    /// <summary>Camera's actual forward direction</summary>
    public Vector3 CameraForward => virtualCamera ? virtualCamera.transform.forward : transform.forward;

    /// <summary>Camera's actual right direction</summary>
    public Vector3 CameraRight => virtualCamera ? virtualCamera.transform.right : transform.right;

    /// <summary>The virtual camera component</summary>
    public CinemachineVirtualCameraBase VirtualCamera => virtualCamera;

    /// <summary>The first person render camera (a separate camera that renders the player and equipped items
    /// as the main camera doesn't render them and only renders the environment and everything else
    ///  - to prevent the player and equipped items from clipping through the environment, walls, etc)
    /// </summary>
    public Camera FirstPersonRenderCamera => firstPersonRenderCamera;

    /// <summary>The camera root transform</summary>
    public Transform CameraRoot => cameraRoot;

    /// <summary>Whether the camera system is initialized</summary>
    public bool IsInitialized => isInitialized;

    /// <summary>Current ADS rotation offset being applied</summary>
    public Vector3 CurrentADSRotationOffset => currentADSRotationOffset;

    /// <summary>Whether ADS is currently active</summary>
    public bool IsADSActive => isADSActive;

    /// <summary>Whether swimming body rotation is currently active</summary>
    public bool IsSwimmingBodyRotationActive => isSwimmingBodyRotationActive;

    /// <summary>ENHANCED: Whether vehicle horizontal aiming is currently active</summary>
    public bool IsVehicleAimingActive => isInVehicle && vehicleHorizontalAimingEnabled;

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize the camera system with all subsystems
    /// </summary>
    public void Initialize(PlayerController controller)
    {
        playerController = controller;

        SetupCinemachine();
        InitializeSubsystems();
        SetupADSIntegration();
        SetupSwimmingIntegration();
        SetupVehicleIntegration(); // ENHANCED: New vehicle integration
        InitializeRotationSystem();

        isInitialized = true;
        DebugLog("Enhanced CameraController initialized with vehicle aiming support");
    }

    /// <summary>Setup Cinemachine components</summary>
    private void SetupCinemachine()
    {
        if (virtualCamera == null)
            virtualCamera = GetComponentInChildren<CinemachineVirtualCameraBase>();

        if (firstPersonRenderCamera == null)
        {
            //get the first person render camera by it's tag: "FirstPersonRenderCamera"
            firstPersonRenderCamera = GameObject.FindWithTag("FirstPersonRenderCamera").GetComponent<Camera>();
            DebugLog("First person render camera reference established");
        }
    }

    /// <summary>Initialize all camera subsystems</summary>
    private void InitializeSubsystems()
    {
        // Auto-find subsystems if not assigned
        if (aimController == null) aimController = GetComponent<AimController>();
        if (adsController == null) adsController = GetComponent<ADSController>();
        if (fovController == null) fovController = GetComponent<FOVController>();
        if (effectsController == null) effectsController = GetComponent<CameraEffectsController>();
        if (swayController == null) swayController = GetComponent<WeaponSwayController>();

        // Auto-find swimming body rotation controller
        if (swimmingBodyRotation == null)
            swimmingBodyRotation = GetComponent<SwimmingBodyRotationController>();

        // Initialize subsystems
        aimController?.Initialize(this, playerController);
        adsController?.Initialize(this, playerController);
        fovController?.Initialize(this, playerController);
        effectsController?.Initialize(this, playerController);
        swayController?.Initialize(playerController);

        DebugLog("Camera subsystems initialized");
    }

    /// <summary>Setup ADS integration and event subscriptions</summary>
    private void SetupADSIntegration()
    {
        if (adsController != null)
        {
            // Subscribe to ADS events
            adsController.OnADSStateChanged += OnADSStateChangedInternal;
            adsController.OnADSRotationOffsetChanged += OnADSRotationOffsetChangedInternal;

            DebugLog("ADS integration established");
        }
        else
        {
            Debug.LogWarning("[CameraController] No ADS controller found - ADS rotation offsets will not work");
        }
    }

    /// <summary>Setup swimming integration and state tracking</summary>
    private void SetupSwimmingIntegration()
    {
        if (playerController != null)
        {
            // Subscribe to movement mode changes
            playerController.OnMovementModeChanged += OnMovementModeChanged;

            // Track initial swimming state
            lastMovementMode = playerController.CurrentMovementMode;
            isSwimming = lastMovementMode == MovementMode.Swimming;
        }
    }

    /// <summary>ENHANCED: Setup vehicle integration and state tracking</summary>
    private void SetupVehicleIntegration()
    {

        if (PlayerStateManager.Instance != null)
        {
            // Subscribe to player state changes for vehicle detection
            PlayerStateManager.Instance.OnStateChanged += OnPlayerStateChanged;

            // Track initial vehicle state
            currentPlayerState = PlayerStateManager.Instance.CurrentStateType;
            isInVehicle = currentPlayerState == PlayerStateType.Vehicle;
        }
        else
        {
            Debug.LogWarning("[CameraController] PlayerStateManager not found! Vehicle camera following will not work.");
        }

        // Subscribe to AimController horizontal angle changes
        if (aimController != null)
        {
            aimController.OnHorizontalAngleChanged += OnHorizontalAngleChanged;
        }

    }

    /// <summary>Initialize the component-wise rotation system</summary>
    private void InitializeRotationSystem()
    {
        if (virtualCamera != null)
        {
            // Initialize current rotation from camera's current state
            Vector3 currentEuler = virtualCamera.transform.eulerAngles;
            currentCameraPitch = NormalizeAngle(currentEuler.x);
            currentCameraYaw = NormalizeAngle(currentEuler.y);
            currentCameraRoll = NormalizeAngle(currentEuler.z);

            // Initialize targets to current values
            targetCameraPitch = currentCameraPitch;
            targetCameraYaw = currentCameraYaw;
            targetCameraRoll = currentCameraRoll;

            DebugLog($"Rotation system initialized - Current angles: P{currentCameraPitch:F1}° Y{currentCameraYaw:F1}° R{currentCameraRoll:F1}°");
        }
    }

    #endregion

    #region ENHANCED: Vehicle State Management

    /// <summary>ENHANCED: Handle player state changes for vehicle detection</summary>
    private void OnPlayerStateChanged(PlayerStateType previousState, PlayerStateType newState)
    {
        currentPlayerState = newState;
        bool wasInVehicle = isInVehicle;
        isInVehicle = newState == PlayerStateType.Vehicle;

        if (wasInVehicle != isInVehicle)
        {
            // Update vehicle horizontal aiming state
            bool wasVehicleAimingEnabled = vehicleHorizontalAimingEnabled;
            vehicleHorizontalAimingEnabled = isInVehicle;

            if (wasVehicleAimingEnabled != vehicleHorizontalAimingEnabled)
            {
                OnVehicleAimingStateChanged?.Invoke(vehicleHorizontalAimingEnabled);

                if (showVehicleAimingInfo && enableDebugLogs)
                {
                    DebugLog($"Vehicle aiming state changed: {wasVehicleAimingEnabled} -> {vehicleHorizontalAimingEnabled}");
                }

                // ENHANCED: Handle vehicle aiming configuration properly
                if (vehicleHorizontalAimingEnabled)
                {
                    // Entering vehicle mode - but wait for vehicle to configure aiming
                    StartCoroutine(DelayedVehicleAimingSetup());
                }
                else
                {
                    // Leaving vehicle mode - reset camera state
                    StartCoroutine(DelayedVehicleCameraReset());
                    DebugLog("Started delayed vehicle camera reset");
                }
            }

            if (showVehicleAimingInfo && enableDebugLogs)
            {
                DebugLog($"Player state changed: {previousState} -> {newState}, " +
                        $"Vehicle state: {isInVehicle}, Camera following: {vehicleHorizontalAimingEnabled}");
            }
        }
    }

    /// <summary>
    /// NEW: Wait for vehicle to properly configure aiming before enabling horizontal aiming
    /// </summary>
    private System.Collections.IEnumerator DelayedVehicleAimingSetup()
    {
        DebugLog("Waiting for vehicle aiming configuration...");

        // Wait for restoration to complete
        yield return new WaitForSeconds(0.3f);

        // Ensure we still should be in vehicle mode
        if (vehicleHorizontalAimingEnabled && playerController != null && playerController.IsInVehicle)
        {
            // Get the current vehicle and ensure aiming is configured
            var currentVehicle = playerController.currentVehicle;
            if (currentVehicle is VehicleController vehicleController)
            {
                DebugLog("Configuring vehicle aiming from camera controller");

                // Configure aim controller with vehicle limits
                if (aimController != null)
                {
                    aimController.UpdateAngleLimits(
                        vehicleController.vehicleMinVerticalAngle,
                        vehicleController.vehicleMaxVerticalAngle,
                        true, // Enable horizontal aiming
                        vehicleController.vehicleMinHorizontalAngle,
                        vehicleController.vehicleMaxHorizontalAngle
                    );

                    DebugLog($"Vehicle aiming configured - H: {vehicleController.vehicleMinHorizontalAngle}° to {vehicleController.vehicleMaxHorizontalAngle}°");
                }
            }
        }
    }

    private System.Collections.IEnumerator DelayedVehicleCameraReset()
    {
        // Wait a frame to ensure all vehicle exit logic has completed
        yield return new WaitForEndOfFrame();

        // Additional small delay to ensure state is stable
        yield return new WaitForSeconds(0.1f);

        // Now reset the camera state
        ResetVehicleCameraState();
    }

    /// <summary>ENHANCED: Handle horizontal angle changes from AimController</summary>
    private void OnHorizontalAngleChanged(float newHorizontalAngle)
    {
        if (!vehicleHorizontalAimingEnabled) return;

        // Calculate target camera yaw based on horizontal aim angle
        // Use player body's current yaw as base and add the horizontal aim offset
        float playerBodyYaw = playerController.transform.eulerAngles.y;
        targetCameraYaw = NormalizeAngle(playerBodyYaw + newHorizontalAngle);

        if (showVehicleAimingInfo && enableDebugLogs)
        {
            DebugLog($"Horizontal angle changed: {newHorizontalAngle:F1}°, " +
                    $"Player yaw: {playerBodyYaw:F1}°, Target camera yaw: {targetCameraYaw:F1}°");
        }
    }

    #endregion

    #region Update Loop

    /// <summary>
    /// Update: Handle input collection and subsystem updates
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;

        // Update subsystems
        aimController?.UpdateInput(currentLookInput);
        fovController?.UpdateEffects();
        effectsController?.UpdateEffects();
    }

    /// <summary>
    /// ENHANCED: Component-wise camera transform update with vehicle horizontal following
    /// </summary>
    public void UpdateCameraTransform()
    {
        if (virtualCamera == null || cameraRoot == null) return;

        // Update camera position to match the camera root position
        virtualCamera.transform.position = Vector3.Lerp(virtualCamera.transform.position, cameraRoot.position, cameraPositionSmoothing);

        // Calculate target rotation components
        CalculateTargetRotation();

        // Apply component-wise rotation interpolation
        ApplyComponentWiseRotation();

        // Update ADS rotation offset smoothing
        UpdateADSRotationOffsetSmoothing();

        // Debug logging for setup and troubleshooting
        if (showCameraCalculations && enableDebugLogs && Time.frameCount % 60 == 0)
        {
            DebugLog($"Camera Transform - Current: P{currentCameraPitch:F1}° Y{currentCameraYaw:F1}° R{currentCameraRoll:F1}°, " +
                    $"Target: P{targetCameraPitch:F1}° Y{targetCameraYaw:F1}° R{targetCameraRoll:F1}°, " +
                    $"Vehicle Following: {vehicleHorizontalAimingEnabled}, ADS Offset: {currentADSRotationOffset}");
        }
    }

    /// <summary>
    /// LateUpdate processing
    /// </summary>
    private void LateUpdate()
    {
        if (!isInitialized) return;

        // Late update for subsystems that need it
        aimController?.LateUpdate();
    }

    #endregion

    #region ENHANCED: Component-wise Rotation System

    /// <summary>
    /// ENHANCED: Calculate target rotation components with vehicle horizontal following
    /// </summary>
    private void CalculateTargetRotation()
    {
        // Get base angles
        float basePitch = 0f;
        if (aimController != null)
        {
            basePitch = -aimController.CurrentVerticalAngle;
        }

        float baseYaw = 0f;

        // ENHANCED: Handle yaw calculation based on vehicle state
        if (vehicleHorizontalAimingEnabled)
        {
            // Vehicle mode - yaw follows horizontal aiming (this is set by OnHorizontalAngleChanged)
            baseYaw = targetCameraYaw;
        }
        else
        {
            // Ground/Swimming mode - yaw follows player body (existing behavior)
            if (playerController != null)
            {
                baseYaw = playerController.transform.eulerAngles.y;
            }
            else
            {
                baseYaw = currentCameraYaw;
            }
        }

        // Normalize base angles
        basePitch = NormalizeAngle(basePitch);
        baseYaw = NormalizeAngle(baseYaw);

        // Apply ADS offset in camera-relative space
        if (Vector3.Distance(currentADSRotationOffset, Vector3.zero) > 0.001f)
        {
            // Create base rotation quaternion
            Quaternion baseRotation = Quaternion.Euler(basePitch, baseYaw, 0f);

            // Create ADS offset as a LOCAL rotation relative to the base
            Quaternion adsOffsetRotation = Quaternion.Euler(
                currentADSRotationOffset.x,
                currentADSRotationOffset.y,
                currentADSRotationOffset.z
            );

            // Apply offset RELATIVE to base rotation (not additive in world space)
            Quaternion finalRotation = baseRotation * adsOffsetRotation;

            // Extract the final Euler angles
            Vector3 finalEuler = CameraRotationUtilities.SafeQuaternionToEuler(finalRotation);

            targetCameraPitch = NormalizeAngle(finalEuler.x);
            targetCameraYaw = NormalizeAngle(finalEuler.y);
            targetCameraRoll = NormalizeAngle(finalEuler.z);
        }
        else
        {
            // No ADS offset - use base angles directly
            targetCameraPitch = basePitch;
            targetCameraYaw = baseYaw;
            targetCameraRoll = 0f;
        }
    }

    /// <summary>
    /// ENHANCED: Apply component-wise rotation interpolation with vehicle transitions
    /// </summary>
    private void ApplyComponentWiseRotation()
    {
        // Direct application for pitch and roll for best responsiveness
        currentCameraPitch = Mathf.Lerp(currentCameraPitch, targetCameraPitch, cameraVerticalRotationSmoothing * Time.deltaTime);
        currentCameraRoll = Mathf.Lerp(currentCameraRoll, targetCameraRoll, cameraVerticalRotationSmoothing * Time.deltaTime);

        // ENHANCED: Handle yaw differently for vehicle mode with safety checks
        if (vehicleHorizontalAimingEnabled || (isVehicleCameraTransitioning && isInVehicle))
        {
            // Vehicle mode or transitioning while in vehicle - smooth yaw interpolation
            currentCameraYaw = Mathf.Lerp(currentCameraYaw, targetCameraYaw, cameraVerticalRotationSmoothing * Time.deltaTime);
            vehicleCameraYawVelocity = (targetCameraYaw - currentCameraYaw) * cameraVerticalRotationSmoothing;
        }
        else
        {
            // Ground/Swimming mode - direct yaw application (existing behavior)
            currentCameraYaw = targetCameraYaw;
        }

        // Apply final rotation to camera
        virtualCamera.transform.rotation = Quaternion.Euler(currentCameraPitch, currentCameraYaw, currentCameraRoll);
    }

    /// <summary>
    /// Normalize angle to -180 to 180 range to prevent interpolation issues
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    #endregion

    #region ADS Rotation Offset System

    /// <summary>
    /// Update ADS rotation offset - simplified for direct application
    /// </summary>
    private void UpdateADSRotationOffsetSmoothing()
    {
        // Check if we need to update to target offset
        if (Vector3.Distance(currentADSRotationOffset, targetADSRotationOffset) > 0.01f)
        {
            isADSOffsetTransitioning = true;

            // Direct application - works best with component-wise rotation
            currentADSRotationOffset = targetADSRotationOffset;

            // Notify listeners of offset change
            OnADSRotationOffsetChanged?.Invoke(currentADSRotationOffset);

            // Debug info for setup mode
            if (showADSOffsetInfo && enableDebugLogs)
            {
                DebugLog($"ADS Offset Applied - Current: {currentADSRotationOffset}, Target: {targetADSRotationOffset}");
            }
        }
        else if (isADSOffsetTransitioning)
        {
            // Transition complete
            isADSOffsetTransitioning = false;

            if (showADSOffsetInfo && enableDebugLogs)
            {
                DebugLog($"ADS Offset Application Complete - Final: {currentADSRotationOffset}");
            }
        }
    }

    /// <summary>
    /// Set target ADS rotation offset (called by ADS controller)
    /// </summary>
    private void SetTargetADSRotationOffset(Vector3 offset)
    {
        targetADSRotationOffset = offset;

        if (showADSOffsetInfo && enableDebugLogs)
        {
            DebugLog($"Target ADS Rotation Offset Set: {offset}");
        }
    }

    /// <summary>
    /// Force immediate ADS rotation offset (used by setup systems)
    /// </summary>
    public void SetADSRotationOffsetImmediate(Vector3 offset)
    {
        currentADSRotationOffset = offset;
        targetADSRotationOffset = offset;
        isADSOffsetTransitioning = false;

        // Update current rotation immediately when in component-wise mode
        if (virtualCamera != null)
        {
            CalculateTargetRotation();
            currentCameraPitch = targetCameraPitch;
            currentCameraYaw = targetCameraYaw;
            currentCameraRoll = targetCameraRoll;

            virtualCamera.transform.rotation = Quaternion.Euler(currentCameraPitch, currentCameraYaw, currentCameraRoll);
        }

        OnADSRotationOffsetChanged?.Invoke(currentADSRotationOffset);

        if (enableDebugLogs)
        {
            DebugLog($"ADS Rotation Offset Set Immediately: {offset}");
        }
    }

    #endregion

    #region Input Handling

    /// <summary>Set look input from input system</summary>
    public void SetLookInput(Vector2 input)
    {
        currentLookInput = input;
        OnLookInputChanged?.Invoke(currentLookInput);
    }

    #endregion

    #region Event Handlers

    /// <summary>Handle movement mode changes for swimming state tracking</summary>
    private void OnMovementModeChanged(MovementMode previousMode, MovementMode newMode)
    {
        lastMovementMode = newMode;
        bool wasSwimming = isSwimming;
        isSwimming = newMode == MovementMode.Swimming;

        // if (wasSwimming != isSwimming && enableSwimmingAngleAdjustment)
        // {
        // Refresh angle limits in aim controller when transitioning to/from swimming
        if (aimController != null)
        {
            aimController.RefreshAngleLimits(newMode);
            DebugLog($"Refreshed aim controller angle limits for swimming state change: {isSwimming}");
        }
        //}

        if (showSwimmingStateInfo && enableDebugLogs)
        {
            DebugLog($"Movement mode changed: {previousMode} -> {newMode}, Swimming: {isSwimming}");
        }
    }

    /// <summary>Handle ADS state changes from ADS controller</summary>
    private void OnADSStateChangedInternal(bool isADS, float sensitivityMultiplier)
    {
        isADSActive = isADS;
        currentADSSensitivityMultiplier = sensitivityMultiplier;

        // Forward event to external listeners
        OnADSStateChanged?.Invoke(isADS);

        if (enableDebugLogs)
        {
            DebugLog($"ADS State Changed: {isADS}, Sensitivity: {sensitivityMultiplier}");
        }
    }

    /// <summary>Handle ADS rotation offset changes from ADS controller</summary>
    private void OnADSRotationOffsetChangedInternal(Vector3 newOffset)
    {
        SetTargetADSRotationOffset(newOffset);
    }

    #endregion

    #region Public API

    /// <summary>Set vertical look sensitivity</summary>
    public void SetVerticalLookSensitivity(float newVerticalSensitivity)
    {
        if (aimController != null)
        {
            aimController.verticalAimSensitivity = newVerticalSensitivity;
        }
    }

    /// <summary>ENHANCED: Set horizontal look sensitivity (for vehicle mode)</summary>
    public void SetHorizontalLookSensitivity(float newHorizontalSensitivity)
    {
        if (aimController != null)
        {
            aimController.SetAimSensitivity(aimController.verticalAimSensitivity, newHorizontalSensitivity);
        }
    }

    /// <summary>Get current field of view</summary>
    public float GetCurrentFOV()
    {
        return fovController != null ? fovController.CurrentFOV : 60f;
    }

    /// <summary>Set field of view</summary>
    public void SetFOV(float fov)
    {
        fovController?.SetFOV(fov);
    }

    /// <summary>Get camera forward direction (horizontal only)</summary>
    public Vector3 GetCameraForward()
    {
        Vector3 forward = Forward;
        forward.y = 0f;
        return forward.normalized;
    }

    /// <summary>Get camera right direction (horizontal only)</summary>
    public Vector3 GetCameraRight()
    {
        Vector3 right = Right;
        right.y = 0f;
        return right.normalized;
    }

    /// <summary>Get the direction the camera is actually looking (including vertical aim)</summary>
    public Vector3 GetCameraLookDirection()
    {
        return aimController != null ? aimController.GetLookDirection() : CameraForward;
    }

    /// <summary>Handle movement state changes</summary>
    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        fovController?.OnMovementStateChanged(previousState, newState);
        effectsController?.OnMovementStateChanged(previousState, newState);
    }

    /// <summary>ENHANCED: Reset camera to default state with vehicle awareness</summary>
    public void ResetCamera()
    {
        aimController?.ResetAim();
        adsController?.ForceADSState(false);
        fovController?.SetNormalFOV();

        // Reset ADS offsets
        SetADSRotationOffsetImmediate(Vector3.zero);

        // ENHANCED: Reset vehicle camera state
        if (vehicleHorizontalAimingEnabled)
        {
            // Reset to player body yaw
            targetCameraYaw = playerController.transform.eulerAngles.y;
            isVehicleCameraTransitioning = true;
        }

        DebugLog("Camera reset to default state");
    }


    /// <summary>ENHANCED: Set vehicle camera transition time</summary>
    public void SetVehicleCameraTransitionTime(float transitionTime)
    {
        vehicleCameraTransitionTime = Mathf.Max(0.05f, transitionTime);
        DebugLog($"Vehicle camera transition time set to: {vehicleCameraTransitionTime:F2}s");
    }

    /// <summary>
    /// ENHANCED: Reset vehicle camera state when exiting vehicle
    /// </summary>
    public void ResetVehicleCameraState()
    {
        if (!vehicleHorizontalAimingEnabled && !isInVehicle)
        {
            // Reset vehicle-specific camera state
            isVehicleCameraTransitioning = false;
            vehicleCameraYawVelocity = 0f;

            // Ensure target yaw matches current player body yaw
            if (playerController != null)
            {
                targetCameraYaw = playerController.transform.eulerAngles.y;
                currentCameraYaw = targetCameraYaw;
            }

            DebugLog("Vehicle camera state reset - direct yaw control restored");
        }
    }

    #endregion



    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EnhancedCameraController] {message}");
        }
    }

    #region Cleanup

    private void OnDestroy()
    {
        // Unsubscribe from ADS events
        if (adsController != null)
        {
            adsController.OnADSStateChanged -= OnADSStateChangedInternal;
            adsController.OnADSRotationOffsetChanged -= OnADSRotationOffsetChangedInternal;
        }

        // Unsubscribe from swimming events
        if (playerController != null)
        {
            playerController.OnMovementModeChanged -= OnMovementModeChanged;
        }

        // ENHANCED: Unsubscribe from vehicle events
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateChanged -= OnPlayerStateChanged;
        }

        if (aimController != null)
        {
            aimController.OnHorizontalAngleChanged -= OnHorizontalAngleChanged;
        }

        // Cleanup subsystems
        aimController?.Cleanup();
        adsController?.Cleanup();
        fovController?.Cleanup();
        effectsController?.Cleanup();

        DebugLog("Enhanced CameraController cleaned up");
    }

    #endregion
}