using UnityEngine;
using RootMotion.FinalIK;
using Sirenix.OdinInspector;
using System;

/// <summary>
/// ENHANCED: AimController with vehicle horizontal aiming support.
/// Now supports both vertical aiming (all states) and horizontal aiming (vehicle state only).
/// Automatically adapts based on PlayerStateManager state changes.
/// Maintains full compatibility with existing swimming body rotation and ground movement.
/// </summary>
public class AimController : MonoBehaviour
{
    [FoldoutGroup("Final IK Setup")]
    [Tooltip("The Final IK AimIK component that controls upper body aiming")]
    [SerializeField] private AimIK aimIK;

    [FoldoutGroup("Final IK Setup")]
    [Tooltip("Target transform that the AimIK system will aim towards (child of player root)")]
    [SerializeField] private Transform aimTarget;
    [FoldoutGroup("Final IK Setup")]
    [Tooltip("FullBodyBipedIK component - as it's solver has the latest update, we'll use it to update camera position after IK calculations are complete")]
    [SerializeField] private FullBodyBipedIK fbbIK;

    [FoldoutGroup("Aim Settings")]
    [Tooltip("Distance from camera to aim target")]
    [SerializeField] private float aimDistance = 10f;

    [FoldoutGroup("Aim Settings")]
    [Tooltip("Mouse sensitivity for vertical aiming - this is set by the player controller as the playerData.lookSensitivity value")]
    [HideInInspector] public float verticalAimSensitivity = 2f;

    [FoldoutGroup("Aim Settings")]
    [Tooltip("Sensitivity multiplier for vertical aiming")]
    [SerializeField] private float verticalAimSensitivityMultiplier = 40f;

    [FoldoutGroup("Horizontal Aiming Settings")]
    [Tooltip("Mouse sensitivity for horizontal aiming (vehicle mode only)")]
    [SerializeField] private float horizontalAimSensitivity = 2f;

    [FoldoutGroup("Horizontal Aiming Settings")]
    [Tooltip("Sensitivity multiplier for horizontal aiming")]
    [SerializeField] private float horizontalAimSensitivityMultiplier = 40f;

    [FoldoutGroup("Ground Aiming Controls")]
    [Tooltip("Maximum upward aim angle in degrees when on ground")]
    public float groundMaxVerticalAngle = 60f;

    [FoldoutGroup("Ground Aiming Controls")]
    [Tooltip("Maximum downward aim angle in degrees when on ground")]
    public float groundMinVerticalAngle = -60f;

    [FoldoutGroup("Swimming Aiming Controls")]
    [Tooltip("Maximum upward aim angle in degrees when swimming")]
    [SerializeField] private float swimmingMaxVerticalAngle = 75f;

    [FoldoutGroup("Swimming Aiming Controls")]
    [Tooltip("Maximum downward aim angle in degrees when swimming")]
    [SerializeField] private float swimmingMinVerticalAngle = -75f;

    [FoldoutGroup("Vertical Aiming Controls")]
    [Tooltip("Clamp aiming to prevent over-rotation")]
    [SerializeField] private bool clampAiming = true;

    [FoldoutGroup("Vertical Aiming Controls")]
    [Tooltip("Input deadzone to reduce micro-jitter")]
    [SerializeField] private float inputDeadzone = 0.02f;

    [FoldoutGroup("Aim Smoothing")]
    [Tooltip("Smoothing time for aim target movement")]
    [SerializeField] private float aimSmoothTime = 0.1f;

    [FoldoutGroup("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Component references
    private CameraController cameraController;
    private PlayerController playerController;
    private SwimmingBodyRotationController swimmingBodyRotation;
    private PlayerStateManager playerStateManager; // ENHANCED: New reference

    // Dynamic angle limits (existing)
    private float currentMaxVerticalAngle;
    private float currentMinVerticalAngle;

    // ENHANCED: Horizontal aiming support
    private float currentMaxHorizontalAngle;
    private float currentMinHorizontalAngle;
    private float currentHorizontalAngle = 0f;
    private bool horizontalAimingEnabled = false;

    // Aim state (enhanced)
    private float currentVerticalAngle = 0f;
    private Vector3 currentAimOffset = Vector3.zero;
    private Vector3 targetAimOffset = Vector3.zero;
    private Vector3 aimOffsetVelocity = Vector3.zero;

    // Input state
    private Vector2 currentLookInput;
    private bool hasVerticalInput = false;
    private bool hasHorizontalInput = false; // ENHANCED: New horizontal input tracking
    private float currentSensitivityMultiplier = 1f;

    // Base position calculation
    private Vector3 aimTargetBasePosition;

    // Player state tracking (enhanced)
    //private bool isSwimming = false;
    private bool isSwimmingBodyRotationActive = false;
    // private bool isInVehicle = false; // ENHANCED: New vehicle state tracking
    private PlayerStateType currentPlayerState = PlayerStateType.Ground;

    // Events (enhanced)
    public event Action<float> OnVerticalAngleChanged;
    public event Action<float> OnHorizontalAngleChanged; // ENHANCED: New event

    #region Properties

    public float CurrentVerticalAngle => currentVerticalAngle;
    public float CurrentHorizontalAngle => currentHorizontalAngle; // ENHANCED: New property
    public bool IsAiming => hasVerticalInput || hasHorizontalInput || currentAimOffset.magnitude > 0.01f;
    public Transform AimTarget => aimTarget;
    public float CurrentMaxVerticalAngle => currentMaxVerticalAngle;
    public float CurrentMinVerticalAngle => currentMinVerticalAngle;
    public float CurrentMaxHorizontalAngle => currentMaxHorizontalAngle; // ENHANCED: New property
    public float CurrentMinHorizontalAngle => currentMinHorizontalAngle; // ENHANCED: New property
    public bool IsHorizontalAimingEnabled => horizontalAimingEnabled; // ENHANCED: New property

    #endregion

    #region Initialization

    public void Initialize(CameraController controller, PlayerController player)
    {
        cameraController = controller;
        playerController = player;

        if (fbbIK == null)
            fbbIK = playerController.GetComponent<FullBodyBipedIK>();

        // Find swimming body rotation controller
        swimmingBodyRotation = playerController.GetComponent<SwimmingBodyRotationController>();

        // ENHANCED: Find and connect to PlayerStateManager
        playerStateManager = PlayerStateManager.Instance;

        SetupAimIK();
        SetupPlayerStateTracking(); // ENHANCED: Enhanced state tracking
        UpdateAngleLimits(groundMinVerticalAngle, groundMaxVerticalAngle);
        ValidateSetup();

        DebugLog("Enhanced AimController initialized with vehicle horizontal aiming support");
    }

    /// <summary>Setup Final IK integration</summary>
    private void SetupAimIK()
    {
        if (aimIK == null)
        {
            Debug.LogError("[AimController] AimIK component not assigned!");
            return;
        }

        if (aimTarget == null)
        {
            Debug.LogError("[AimController] Aim target not assigned!");
            return;
        }

        // Subscribe to Final IK's post-update callback
        fbbIK.solver.OnPostUpdate += OnIKPostUpdate;

        DebugLog("AimIK integration setup complete");
    }

    /// <summary>ENHANCED: Setup comprehensive player state tracking</summary>
    private void SetupPlayerStateTracking()
    {

        if (swimmingBodyRotation != null)
        {
            // Subscribe to swimming body rotation state changes
            swimmingBodyRotation.OnRotationStateChanged += OnSwimmingRotationStateChanged;
            isSwimmingBodyRotationActive = swimmingBodyRotation.IsRotationActive;
        }

        // ENHANCED: Subscribe to PlayerStateManager for vehicle state changes
        if (playerStateManager != null)
        {
            currentPlayerState = playerStateManager.CurrentStateType;
        }
        else
        {
            Debug.LogWarning("[AimController] PlayerStateManager not found! Vehicle horizontal aiming will not work.");
        }

        DebugLog("Enhanced player state tracking setup complete");
    }

    /// <summary>Validate the aim controller setup</summary>
    private void ValidateSetup()
    {
        bool isValid = true;

        if (aimIK == null)
        {
            Debug.LogError("[AimController] AimIK component not assigned!");
            isValid = false;
        }

        if (aimTarget == null)
        {
            Debug.LogError("[AimController] Aim target not assigned!");
            isValid = false;
        }
        else
        {
            // Verify aim target is child of player root (not scaled bones)
            Transform playerRoot = playerController.transform;
            bool isChildOfPlayerRoot = false;
            Transform current = aimTarget.parent;

            while (current != null)
            {
                if (current == playerRoot)
                {
                    isChildOfPlayerRoot = true;
                    break;
                }
                current = current.parent;
            }

            if (!isChildOfPlayerRoot)
            {
                Debug.LogWarning("[AimController] Aim target should be a child of the player root for best results!");
            }
        }

        if (cameraController?.VirtualCamera == null)
        {
            Debug.LogError("[AimController] Virtual camera not found!");
            isValid = false;
        }

        if (!isValid)
        {
            Debug.LogError("[AimController] Setup validation failed!");
        }
        else
        {
            DebugLog("Enhanced AimController setup validation passed");
        }
    }

    #endregion

    #region ENHANCED: Dynamic Angle Limit System

    /// <summary>ENHANCED: Update angle limits based on current player state</summary>
    public void UpdateAngleLimits(float minVerticalLimit, float maxVerticalLimit, bool horizontalAimingEnabled = false, float minHorizontalLimit = 0f, float maxHorizontalLimit = 0f)
    {
        // Update vertical limits based on state
        UpdateVerticalAngleLimits(minVerticalLimit, maxVerticalLimit);

        // ENHANCED: Update horizontal limits based on state
        UpdateHorizontalAngleLimits(minHorizontalLimit, maxHorizontalLimit);

        // ENHANCED: Enable/disable horizontal aiming based on state
        UpdateHorizontalAimingState(horizontalAimingEnabled);

        // Clamp current angles to new limits if needed
        ClampCurrentAnglesToNewLimits();
    }

    /// <summary>Update vertical angle limits (existing logic enhanced)</summary>
    private void UpdateVerticalAngleLimits(float minVerticalLimit, float maxVerticalLimit)
    {
        currentMinVerticalAngle = minVerticalLimit;
        currentMaxVerticalAngle = maxVerticalLimit;

    }

    /// <summary>ENHANCED: Update horizontal angle limits based on state</summary>
    private void UpdateHorizontalAngleLimits(float minHorizontalLimit, float maxHorizontalLimit)
    {
        currentMinHorizontalAngle = minHorizontalLimit;
        currentMaxHorizontalAngle = maxHorizontalLimit;
    }

    /// <summary>ENHANCED: Enable/disable horizontal aiming based on current state</summary>
    private void UpdateHorizontalAimingState(bool shouldEnableHorizontal)
    {
        if (!shouldEnableHorizontal)
        {
            horizontalAimingEnabled = false;

            // Reset horizontal angle when disabling
            currentHorizontalAngle = 0f;
            targetAimOffset.x = 0f;

            DebugLog("Horizontal aiming disabled - reset to center");
        }
        else
        {
            horizontalAimingEnabled = true;
            DebugLog("Horizontal aiming enabled for vehicle mode");
        }

    }

    /// <summary>ENHANCED: Clamp current angles to new limits</summary>
    private void ClampCurrentAnglesToNewLimits()
    {
        if (!clampAiming) return;

        // Clamp vertical angle
        float clampedVertical = Mathf.Clamp(currentVerticalAngle, currentMinVerticalAngle, currentMaxVerticalAngle);
        if (Mathf.Abs(clampedVertical - currentVerticalAngle) > 0.1f)
        {
            SetVerticalAngle(clampedVertical);
            DebugLog($"Clamped vertical angle to new limits: {clampedVertical:F1}°");
        }

        // ENHANCED: Clamp horizontal angle
        float clampedHorizontal = Mathf.Clamp(currentHorizontalAngle, currentMinHorizontalAngle, currentMaxHorizontalAngle);
        if (Mathf.Abs(clampedHorizontal - currentHorizontalAngle) > 0.1f)
        {
            SetHorizontalAngle(clampedHorizontal);
            DebugLog($"Clamped horizontal angle to new limits: {clampedHorizontal:F1}°");
        }
    }

    /// <summary>Handle swimming body rotation state changes</summary>
    private void OnSwimmingRotationStateChanged(SwimmingDepthState previousState, SwimmingDepthState newState)
    {
        bool wasActive = isSwimmingBodyRotationActive;
        isSwimmingBodyRotationActive = newState == SwimmingDepthState.UnderwaterSwimming ||
                                       newState == SwimmingDepthState.SurfaceSwimming ||
                                       newState == SwimmingDepthState.EnteringSwimming;

        if (wasActive != isSwimmingBodyRotationActive)
        {
            UpdateAngleLimits(swimmingMinVerticalAngle, swimmingMaxVerticalAngle);
            DebugLog($"Swimming body rotation state changed: {previousState} -> {newState}, Active: {isSwimmingBodyRotationActive}");
        }
    }

    #endregion

    #region Update Methods

    /// <summary>ENHANCED: Update input processing with horizontal aiming support</summary>
    public void UpdateInput(Vector2 lookInput)
    {
        // Apply sensitivity multiplier from ADS system
        currentLookInput = lookInput * currentSensitivityMultiplier;

        // Process vertical input (existing)
        ProcessVerticalAiming();

        // ENHANCED: Process horizontal input (vehicle mode only)
        ProcessHorizontalAiming();
    }

    /// <summary>LateUpdate processing</summary>
    public void LateUpdate()
    {
        // Update aim target base position first
        UpdateAimTargetBasePosition();

        // Then update the final aim target position
        UpdateAimTargetPosition();
    }

    /// <summary>Called after Final IK processing to update camera</summary>
    private void OnIKPostUpdate()
    {
        // Update camera transform after IK calculations are complete
        cameraController?.UpdateCameraTransform();
    }

    #endregion

    #region ENHANCED: Aim Target Positioning

    /// <summary>
    /// ENHANCED: Update the base position for aim target calculations using camera position
    /// Now supports both horizontal and vertical offsets for vehicle mode
    /// </summary>
    private void UpdateAimTargetBasePosition()
    {
        var virtualCamera = cameraController?.VirtualCamera;
        if (virtualCamera == null) return;

        Vector3 cameraPosition = virtualCamera.transform.position;

        if (horizontalAimingEnabled)
        {
            // ENHANCED: Vehicle mode - use camera position as base, apply offsets in camera-relative space
            aimTargetBasePosition = cameraPosition;
        }
        else
        {
            // Ground/Swimming mode - use player forward direction (existing behavior)
            Vector3 playerForward = playerController.transform.forward;
            playerForward.y = 0f;
            playerForward = playerForward.normalized;
            aimTargetBasePosition = cameraPosition + (playerForward * aimDistance);
        }

        if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            DebugLog($"Base position updated - Camera: {cameraPosition}, Base: {aimTargetBasePosition}, " +
                    $"HorizontalAiming: {horizontalAimingEnabled}");
        }
    }

    /// <summary>ENHANCED: Process vertical aiming input with dynamic limits</summary>
    private void ProcessVerticalAiming()
    {
        float verticalInput = currentLookInput.y;
        hasVerticalInput = Mathf.Abs(verticalInput) > inputDeadzone;

        if (!hasVerticalInput) return;

        // Calculate angle delta from mouse input
        float mouseDelta = verticalInput * verticalAimSensitivity * verticalAimSensitivityMultiplier * Time.deltaTime;

        // Apply delta to current angle
        float proposedAngle = currentVerticalAngle + mouseDelta;

        // Apply dynamic clamping based on current state
        if (clampAiming)
        {
            currentVerticalAngle = Mathf.Clamp(proposedAngle, currentMinVerticalAngle, currentMaxVerticalAngle);
        }
        else
        {
            currentVerticalAngle = proposedAngle;
        }

        // ENHANCED: Calculate Y offset differently for vehicle vs ground/swimming
        if (horizontalAimingEnabled)
        {
            // Vehicle mode - direct offset from camera position
            targetAimOffset.y = Mathf.Tan(currentVerticalAngle * Mathf.Deg2Rad) * aimDistance;
        }
        else
        {
            // Ground/Swimming mode - offset from base position (existing behavior)
            targetAimOffset.y = Mathf.Tan(currentVerticalAngle * Mathf.Deg2Rad) * aimDistance;
        }

        OnVerticalAngleChanged?.Invoke(currentVerticalAngle);

        if (enableDebugLogs)
        {
            DebugLog($"Vertical Input: {verticalInput:F3}, Delta: {mouseDelta:F3}, Angle: {currentVerticalAngle:F1}° " +
                    $"(Limits: {currentMinVerticalAngle:F1}° to {currentMaxVerticalAngle:F1}°), Y Offset: {targetAimOffset.y:F3}");
        }
    }

    /// <summary>ENHANCED: Process horizontal aiming input (vehicle mode only) with vehicle-relative tracking</summary>
    private void ProcessHorizontalAiming()
    {
        if (!horizontalAimingEnabled)
        {
            hasHorizontalInput = false;
            return;
        }

        float horizontalInput = currentLookInput.x;
        hasHorizontalInput = Mathf.Abs(horizontalInput) > inputDeadzone;

        if (hasHorizontalInput)
        {
            // Calculate angle delta from mouse input
            float mouseDelta = horizontalInput * horizontalAimSensitivity * horizontalAimSensitivityMultiplier * Time.deltaTime;

            // Apply delta to current horizontal angle
            float proposedAngle = currentHorizontalAngle + mouseDelta;

            // Apply clamping for vehicle head rotation limits
            if (clampAiming)
            {
                currentHorizontalAngle = Mathf.Clamp(proposedAngle, currentMinHorizontalAngle, currentMaxHorizontalAngle);
            }
            else
            {
                currentHorizontalAngle = proposedAngle;
            }

            OnHorizontalAngleChanged?.Invoke(currentHorizontalAngle);

        }

        // ENHANCED FIX: Always calculate X offset based on CURRENT vehicle orientation
        // This ensures the head stays at the same relative angle to the vehicle even when it rotates
        if (playerController != null)
        {
            // Calculate the direction the head should look relative to current vehicle forward
            Vector3 currentVehicleForward = playerController.transform.forward;
            Vector3 currentVehicleRight = playerController.transform.right;

            // Create the target look direction by rotating vehicle forward by horizontal angle
            Vector3 targetLookDirection = Quaternion.AngleAxis(currentHorizontalAngle, Vector3.up) * currentVehicleForward;

            // Calculate world position for aim target
            Vector3 cameraPosition = cameraController?.VirtualCamera?.transform.position ?? transform.position;
            Vector3 targetPosition = cameraPosition + targetLookDirection * aimDistance;

            // Apply vertical offset
            targetPosition += currentVehicleRight.normalized * 0f; // No additional right offset needed
            targetPosition += Vector3.up * targetAimOffset.y; // Add vertical offset

            // Set target directly instead of using offset calculation
            aimTarget.position = targetPosition;

            return; // Skip the standard position calculation below
        }

        // Fallback to offset-based calculation if no player controller
        targetAimOffset.x = Mathf.Tan(currentHorizontalAngle * Mathf.Deg2Rad) * aimDistance;
    }

    /// <summary>ENHANCED: Update the actual aim target position with smoothing</summary>
    private void UpdateAimTargetPosition()
    {
        if (aimTarget == null) return;

        // Smooth the aim offset to reduce jerkiness
        currentAimOffset = Vector3.SmoothDamp(
            currentAimOffset,
            targetAimOffset,
            ref aimOffsetVelocity,
            aimSmoothTime
        );

        // ENHANCED: Calculate final position differently for vehicle vs ground/swimming
        Vector3 finalPosition;

        if (horizontalAimingEnabled)
        {
            // Vehicle mode - apply offsets relative to camera position in camera space
            var virtualCamera = cameraController?.VirtualCamera;
            if (virtualCamera != null)
            {
                Vector3 cameraPosition = virtualCamera.transform.position;
                Vector3 cameraForward = virtualCamera.transform.forward;
                Vector3 cameraRight = virtualCamera.transform.right;
                Vector3 cameraUp = virtualCamera.transform.up;

                // Calculate position at aim distance from camera
                Vector3 basePosition = cameraPosition + cameraForward * aimDistance;

                // Apply horizontal and vertical offsets in camera-relative space
                finalPosition = basePosition +
                               (cameraRight * currentAimOffset.x) +
                               (cameraUp * currentAimOffset.y);
            }
            else
            {
                // Fallback if camera not available
                finalPosition = aimTargetBasePosition + currentAimOffset;
            }
        }
        else
        {
            // Ground/Swimming mode - use existing logic
            finalPosition = aimTargetBasePosition + currentAimOffset;
        }

        aimTarget.position = finalPosition;

        if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            DebugLog($"Aim target updated - Base: {aimTargetBasePosition}, Offset: {currentAimOffset}, " +
                    $"Final: {finalPosition}, HorizontalMode: {horizontalAimingEnabled}");
        }
    }

    #endregion

    #region ENHANCED: Public API

    /// <summary>Set vertical aim angle directly</summary>
    public void SetVerticalAngle(float angle)
    {
        if (clampAiming)
        {
            angle = Mathf.Clamp(angle, currentMinVerticalAngle, currentMaxVerticalAngle);
        }

        currentVerticalAngle = angle;

        // Calculate offset based on current mode
        float targetOffset = Mathf.Tan(angle * Mathf.Deg2Rad) * aimDistance;
        targetAimOffset.y = targetOffset;
        currentAimOffset.y = targetOffset;

        OnVerticalAngleChanged?.Invoke(currentVerticalAngle);

        DebugLog($"Set vertical angle to: {angle:F1}° (offset: {targetOffset:F2})");
    }

    /// <summary>ENHANCED: Set horizontal aim angle directly</summary>
    public void SetHorizontalAngle(float angle)
    {
        if (!horizontalAimingEnabled)
        {
            DebugLog("Horizontal aiming not enabled - cannot set horizontal angle");
            return;
        }

        if (clampAiming)
        {
            angle = Mathf.Clamp(angle, currentMinHorizontalAngle, currentMaxHorizontalAngle);
        }

        currentHorizontalAngle = angle;

        // Calculate offset for horizontal head rotation
        float targetOffset = Mathf.Tan(angle * Mathf.Deg2Rad) * aimDistance;
        targetAimOffset.x = targetOffset;
        currentAimOffset.x = targetOffset;

        OnHorizontalAngleChanged?.Invoke(currentHorizontalAngle);

        DebugLog($"Set horizontal angle to: {angle:F1}° (offset: {targetOffset:F2})");
    }

    /// <summary>Set aim sensitivity for both axes</summary>
    public void SetAimSensitivity(float verticalSensitivity, float horizontalSensitivity = -1f)
    {
        verticalAimSensitivity = Mathf.Max(0.1f, verticalSensitivity);

        if (horizontalSensitivity >= 0f)
        {
            horizontalAimSensitivity = Mathf.Max(0.1f, horizontalSensitivity);
        }
        else
        {
            // If not specified, use same as vertical
            horizontalAimSensitivity = verticalAimSensitivity;
        }

        DebugLog($"Aim sensitivity set - Vertical: {verticalAimSensitivity}, Horizontal: {horizontalAimSensitivity}");
    }

    /// <summary>Set sensitivity multiplier (used by ADS system)</summary>
    public void SetSensitivityMultiplier(float multiplier)
    {
        currentSensitivityMultiplier = multiplier;
    }

    /// <summary>ENHANCED: Reset aim to center position</summary>
    public void ResetAim()
    {
        SetVerticalAngle(0f);

        if (horizontalAimingEnabled)
        {
            SetHorizontalAngle(0f);
        }

        DebugLog("Aim reset to center");
    }

    /// <summary>Set custom angle limits for specific states</summary>
    public void SetCustomAngleLimits(float maxVertical, float minVertical, float maxHorizontal = 0f, float minHorizontal = 0f)
    {
        currentMaxVerticalAngle = maxVertical;
        currentMinVerticalAngle = minVertical;

        // ENHANCED: Set horizontal limits if provided
        if (horizontalAimingEnabled && (maxHorizontal != 0f || minHorizontal != 0f))
        {
            currentMaxHorizontalAngle = maxHorizontal;
            currentMinHorizontalAngle = minHorizontal;
        }

        // Clamp current angles if outside new limits
        ClampCurrentAnglesToNewLimits();

        DebugLog($"Custom angle limits set - Vertical: {minVertical:F1}° to {maxVertical:F1}°, " +
                $"Horizontal: {minHorizontal:F1}° to {maxHorizontal:F1}°");
    }

    /// <summary>Force angle limit refresh</summary>
    public void RefreshAngleLimits(MovementMode newMovementMode)
    {
        switch (newMovementMode)
        {
            case MovementMode.Ground:
                UpdateAngleLimits(groundMinVerticalAngle, groundMaxVerticalAngle);
                break;
            case MovementMode.Swimming:
                if (isSwimmingBodyRotationActive)
                    UpdateAngleLimits(swimmingMinVerticalAngle, swimmingMaxVerticalAngle);
                else
                    UpdateAngleLimits(Mathf.Min(groundMinVerticalAngle, swimmingMinVerticalAngle * 0.7f),
                        Mathf.Max(groundMaxVerticalAngle, swimmingMaxVerticalAngle * 0.7f));
                break;
            case MovementMode.Vehicle:
                //Don't update the vehicle look limits yet as we'll have the VehicleEntryExitHandler Handle this
                break;
        }

    }

    /// <summary>Get the direction the camera is looking (including vertical aim)</summary>
    public Vector3 GetLookDirection()
    {
        var virtualCamera = cameraController?.VirtualCamera;
        if (aimTarget != null && virtualCamera != null)
        {
            Vector3 directionToAim = (aimTarget.position - virtualCamera.transform.position).normalized;
            return directionToAim;
        }

        return cameraController?.CameraForward ?? Vector3.forward;
    }


    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EnhancedAimController] {message}");
        }
    }

    #region Cleanup

    public void Cleanup()
    {
        // Unsubscribe from Final IK callback
        if (fbbIK != null && fbbIK.solver != null)
        {
            fbbIK.solver.OnPostUpdate -= OnIKPostUpdate;
        }

        if (swimmingBodyRotation != null)
        {
            swimmingBodyRotation.OnRotationStateChanged -= OnSwimmingRotationStateChanged;
        }

        DebugLog("Enhanced AimController cleaned up");
    }

    #endregion
}