using UnityEngine;
using Sirenix.OdinInspector;
using System;

/// <summary>
/// UPDATED: Swimming Body Rotation Controller now integrates with PlayerSwimmingDepthManager.
/// Creates natural Subnautica-style body orientation with leaning effects.
/// Rotates the player's body to match vertical look direction while swimming, with smooth 
/// transitions and surface-aware behavior controlled by the depth manager.
/// ENHANCED: Includes AimIK pole target manipulation for realistic leaning during strafing.
/// </summary>
public class SwimmingBodyRotationController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private Transform playerBody;
    [SerializeField] private AimController aimController;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private SwimmingMovementController swimmingController;
    [SerializeField] private PlayerSwimmingDepthManager depthManager; // UPDATED: Use depth manager
    [SerializeField] private bool autoFindReferences = true;

    [Header("ENHANCED: IK Pole Target Reference")]
    [SerializeField, Tooltip("The AimIK pole target transform for creating leaning effects")]
    private Transform aimIKPoleTarget;

    [Header("Body Rotation Settings")]
    [SerializeField, Range(-75f, 0f)] private float maxDownwardRotation = -75f;
    [SerializeField, Range(0f, 75f)] private float maxUpwardRotation = 75f;
    [SerializeField, Range(0.5f, 5f)] private float rotationSpeed = 2f;
    [SerializeField, Range(0f, 15f)] private float rotationDeadZone = 3f;
    [SerializeField] private bool enableRotationLimiting = true;

    [Header("ENHANCED: Leaning System")]
    [SerializeField] private bool enableLeaningEffect = true;
    [SerializeField, Range(1f, 20f), Tooltip("Maximum local X offset for pole target when strafing")]
    private float maxLeaningOffset = 10f;
    [SerializeField, Range(0.5f, 5f), Tooltip("Speed at which pole target moves to strafe positions")]
    private float leaningSpeed = 2f;
    [SerializeField, Range(0f, 1f), Tooltip("Input threshold before leaning starts")]
    private float leaningInputDeadzone = 0.1f;
    [SerializeField, Tooltip("Enable debug logs for leaning system")]
    private bool debugLeaning = false;

    [Header("UPDATED: Depth-Based Rotation")]
    [SerializeField, Range(0f, 1f)] private float surfaceRotationReduction = 0.6f;
    [SerializeField, Range(0f, 1f)] private float surfaceRotationDamping = 0.75f;
    [SerializeField] private bool disableRotationWhenAtSurface = true;

    [Header("Angle Limiting")]
    [SerializeField, Range(5f, 30f)] private float softLimitRange = 15f;
    [SerializeField] private AnimationCurve limitResistanceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showAngleInfo = false;

    // UPDATED: Current state now comes from depth manager
    private float currentBodyRotationX = 0f;
    private float targetBodyRotationX = 0f;
    private float rotationStrengthMultiplier = 0f;

    // ENHANCED: Leaning system state
    private float currentPoleTargetX = 0f;
    private float targetPoleTargetX = 0f;
    private Vector3 originalPoleTargetLocalPosition = Vector3.zero;
    private bool poleTargetOriginalPositionStored = false;
    private float currentStrafeInput = 0f;
    private bool isStrafing = false;
    private LeanDirection currentLeanDirection = LeanDirection.None;

    // Original rotation for restoration
    private Quaternion originalBodyRotation = Quaternion.identity;
    private bool originalRotationStored = false;

    // UPDATED: Depth manager state tracking
    private SwimmingDepthState currentDepthState = SwimmingDepthState.Disabled;
    private float surfaceRotationMultiplier = 1f;

    // Angle limiting state
    private float lastValidTargetAngle = 0f;

    // Events
    public event Action<SwimmingDepthState, SwimmingDepthState> OnRotationStateChanged;
    public event Action<float> OnBodyRotationChanged;
    public event Action<LeanDirection, LeanDirection> OnLeanDirectionChanged;

    #region Properties

    public SwimmingDepthState CurrentState => currentDepthState;
    public float CurrentBodyRotationX => currentBodyRotationX;
    public float TargetBodyRotationX => targetBodyRotationX;
    public float RotationStrength => rotationStrengthMultiplier;
    public bool IsRotationActive => currentDepthState == SwimmingDepthState.UnderwaterSwimming ||
                                   currentDepthState == SwimmingDepthState.SurfaceSwimming;

    // ENHANCED: Leaning properties
    public bool IsLeaningActive => enableLeaningEffect && isStrafing && IsSwimmingStateActive();
    public LeanDirection CurrentLeanDirection => currentLeanDirection;
    public float CurrentLeanAmount => Mathf.Abs(currentPoleTargetX) / maxLeaningOffset;

    #endregion

    #region Initialization

    private void Awake()
    {
        if (autoFindReferences)
        {
            FindReferences();
        }

        SetupInitialState();
    }

    private void Start()
    {
        ValidateSetup();
        SubscribeToEvents();
        StorePoleTargetOriginalPosition();
    }

    /// <summary>
    /// UPDATED: Find required component references including depth manager
    /// </summary>
    private void FindReferences()
    {
        if (playerBody == null)
            playerBody = transform;

        if (aimController == null)
            aimController = GetComponent<AimController>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (swimmingController == null)
            swimmingController = GetComponent<SwimmingMovementController>();

        // UPDATED: Find depth manager
        if (depthManager == null)
            depthManager = GetComponent<PlayerSwimmingDepthManager>();

        DebugLog("Component references found automatically");
    }

    /// <summary>
    /// Setup initial state and store original rotation
    /// </summary>
    private void SetupInitialState()
    {
        if (playerBody != null)
        {
            originalBodyRotation = playerBody.rotation;
            originalRotationStored = true;
            currentBodyRotationX = GetBodyRotationX();
        }

        rotationStrengthMultiplier = 0f;

        // ENHANCED: Initialize leaning state
        currentPoleTargetX = 0f;
        targetPoleTargetX = 0f;
        currentLeanDirection = LeanDirection.None;

        DebugLog("Initial state setup complete");
    }

    /// <summary>
    /// ENHANCED: Store the pole target's original local position
    /// </summary>
    private void StorePoleTargetOriginalPosition()
    {
        if (aimIKPoleTarget != null)
        {
            originalPoleTargetLocalPosition = aimIKPoleTarget.localPosition;
            poleTargetOriginalPositionStored = true;
            DebugLog($"Stored original pole target position: {originalPoleTargetLocalPosition}");
        }
        else if (enableLeaningEffect)
        {
            Debug.LogWarning("[SwimmingBodyRotationController] Leaning effect is enabled but AimIK Pole Target is not assigned!");
        }
    }

    /// <summary>
    /// UPDATED: Subscribe to depth manager events instead of managing state internally
    /// </summary>
    private void SubscribeToEvents()
    {
        if (depthManager != null)
        {
            depthManager.OnSwimmingDepthStateChanged += OnDepthStateChanged;
            DebugLog("Subscribed to depth manager events");
        }
        else
        {
            Debug.LogWarning("[SwimmingBodyRotationController] No depth manager found! Rotation behavior may not work properly.");
        }
    }

    /// <summary>
    /// UPDATED: Validate that all required components including depth manager are available
    /// </summary>
    private void ValidateSetup()
    {
        bool isValid = true;

        if (playerBody == null)
        {
            Debug.LogError("[SwimmingBodyRotationController] Player body transform not assigned!");
            isValid = false;
        }

        if (aimController == null)
        {
            Debug.LogError("[SwimmingBodyRotationController] AimController not found!");
            isValid = false;
        }

        if (playerController == null)
        {
            Debug.LogWarning("[SwimmingBodyRotationController] PlayerController not found! State change events will not work.");
        }

        if (swimmingController == null)
        {
            Debug.LogWarning("[SwimmingBodyRotationController] SwimmingMovementController not found! Leaning effects may not work properly.");
        }

        // UPDATED: Validate depth manager
        if (depthManager == null)
        {
            Debug.LogError("[SwimmingBodyRotationController] PlayerSwimmingDepthManager not found! Rotation state management will not work!");
            isValid = false;
        }

        // ENHANCED: Validate leaning setup
        if (enableLeaningEffect && aimIKPoleTarget == null)
        {
            Debug.LogError("[SwimmingBodyRotationController] Leaning effect is enabled but AimIK Pole Target is not assigned!");
            isValid = false;
        }

        if (!isValid)
        {
            Debug.LogError("[SwimmingBodyRotationController] Setup validation failed! Disabling component.");
            enabled = false;
        }
        else
        {
            DebugLog("Setup validation passed");
        }
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        // UPDATED: Get state from depth manager instead of calculating internally
        UpdateStateFromDepthManager();
        UpdateTargetRotation();
        UpdateBodyRotation();
        UpdateLeaningSystem();
    }

    /// <summary>
    /// UPDATED: Get rotation state and multipliers from depth manager
    /// </summary>
    private void UpdateStateFromDepthManager()
    {
        if (depthManager == null) return;

        currentDepthState = depthManager.CurrentSwimmingDepthState;
        rotationStrengthMultiplier = depthManager.GetSwimmingDepthStateMultiplier();

        // Apply surface rotation reduction
        if (currentDepthState == SwimmingDepthState.SurfaceSwimming)
        {
            surfaceRotationMultiplier = surfaceRotationReduction * surfaceRotationDamping;
        }
        else
        {
            surfaceRotationMultiplier = 1f;
        }

        // Disable rotation completely if at surface and setting is enabled
        if (disableRotationWhenAtSurface && depthManager.IsSwimmingAtSurface)
        {
            surfaceRotationMultiplier = 0f;
        }
    }

    /// <summary>
    /// Update target rotation based on aim controller input
    /// </summary>
    private void UpdateTargetRotation()
    {
        if (aimController == null || rotationStrengthMultiplier <= 0f) return;

        // Get vertical angle from aim controller
        float aimVerticalAngle = aimController.CurrentVerticalAngle;

        // Invert vertical angle 
        aimVerticalAngle = -aimVerticalAngle;

        // Apply dead zone
        if (Mathf.Abs(aimVerticalAngle) <= rotationDeadZone)
        {
            aimVerticalAngle = 0f;
        }

        // Apply angle limiting
        float limitedAngle = ApplyAngleLimits(aimVerticalAngle);

        // Apply surface rotation multiplier
        limitedAngle *= surfaceRotationMultiplier;

        // Set target rotation
        targetBodyRotationX = limitedAngle;

        if (showAngleInfo && enableDebugLogs && Time.frameCount % 60 == 0)
        {
            DebugLog($"Angles - Aim: {aimVerticalAngle:F1}°, Limited: {limitedAngle:F1}°, " +
                    $"Surface Multi: {surfaceRotationMultiplier:F2}, Strength: {rotationStrengthMultiplier:F2}");
        }
    }

    /// <summary>
    /// Apply angle limits with soft resistance near limits
    /// </summary>
    private float ApplyAngleLimits(float targetAngle)
    {
        if (!enableRotationLimiting) return targetAngle;

        // Hard clamp to absolute limits
        targetAngle = Mathf.Clamp(targetAngle, maxDownwardRotation, maxUpwardRotation);

        // Apply soft resistance near limits
        float softLimitMin = maxDownwardRotation + softLimitRange;
        float softLimitMax = maxUpwardRotation - softLimitRange;

        if (targetAngle < softLimitMin)
        {
            // Approaching downward limit
            float limitProgress = (targetAngle - maxDownwardRotation) / softLimitRange;
            float resistance = limitResistanceCurve.Evaluate(1f - limitProgress);
            targetAngle = Mathf.Lerp(lastValidTargetAngle, targetAngle, resistance);
        }
        else if (targetAngle > softLimitMax)
        {
            // Approaching upward limit
            float limitProgress = (maxUpwardRotation - targetAngle) / softLimitRange;
            float resistance = limitResistanceCurve.Evaluate(1f - limitProgress);
            targetAngle = Mathf.Lerp(lastValidTargetAngle, targetAngle, resistance);
        }
        else
        {
            // In safe range, update last valid angle
            lastValidTargetAngle = targetAngle;
        }

        return targetAngle;
    }

    /// <summary>
    /// Apply rotation to player body
    /// </summary>
    private void UpdateBodyRotation()
    {
        if (playerBody == null) return;

        // Interpolate current rotation towards target
        float rotationDelta = targetBodyRotationX - currentBodyRotationX;
        float rotationStep = rotationSpeed * rotationStrengthMultiplier * Time.deltaTime;

        currentBodyRotationX = Mathf.MoveTowards(currentBodyRotationX, targetBodyRotationX,
            Mathf.Abs(rotationDelta) * rotationStep);

        // Apply rotation to player body
        ApplyRotationToBody();

        // Fire rotation change event
        OnBodyRotationChanged?.Invoke(currentBodyRotationX);
    }

    /// <summary>
    /// Apply the calculated rotation to the player body transform
    /// </summary>
    private void ApplyRotationToBody()
    {
        if (!originalRotationStored) return;

        // Apply to original body rotation (preserving Y rotation from PlayerController)
        Vector3 originalEuler = originalBodyRotation.eulerAngles;
        Vector3 currentPlayerEuler = playerBody.eulerAngles;

        // Preserve Y rotation (horizontal turning) from PlayerController
        Vector3 finalEuler = new Vector3(
            currentBodyRotationX,
            currentPlayerEuler.y,
            originalEuler.z
        );

        playerBody.rotation = Quaternion.Euler(finalEuler);
    }

    #endregion

    #region ENHANCED: Leaning System

    /// <summary>
    /// ENHANCED: Update the leaning system based on strafe input
    /// </summary>
    private void UpdateLeaningSystem()
    {
        if (!enableLeaningEffect || aimIKPoleTarget == null || !poleTargetOriginalPositionStored)
            return;

        // Get strafe input from swimming controller or input system
        UpdateStrafeInput();

        // Update target pole position based on strafe input
        UpdateLeaningTarget();

        // Apply leaning interpolation
        ApplyLeaningToTarget();

        // Update lean direction tracking
        UpdateLeanDirection();
    }

    /// <summary>
    /// ENHANCED: Get the current strafe input from swimming controller
    /// </summary>
    private void UpdateStrafeInput()
    {
        // Reset strafe input if not in valid swimming state
        if (!IsSwimmingStateActive())
        {
            currentStrafeInput = 0f;
            isStrafing = false;
            return;
        }

        // Get strafe input - check if swimming controller is available
        if (swimmingController != null && swimmingController.IsMoving)
        {
            // Access movement input through the SwimmingMovementController
            Vector3 playerVelocity = swimmingController.GetVelocity();
            Vector3 playerRight = playerController.Right;

            // Calculate strafe component by projecting velocity onto player's right vector
            float strafeVelocityComponent = Vector3.Dot(playerVelocity.normalized, playerRight);

            // Use velocity-based detection with some smoothing
            currentStrafeInput = Mathf.Clamp(strafeVelocityComponent, -1f, 1f);
        }
        else
        {
            currentStrafeInput = 0f;
        }

        // Apply input deadzone
        if (Mathf.Abs(currentStrafeInput) < leaningInputDeadzone)
        {
            currentStrafeInput = 0f;
            isStrafing = false;
        }
        else
        {
            isStrafing = true;
        }

        if (debugLeaning && isStrafing)
        {
            DebugLog($"Strafe Input: {currentStrafeInput:F2}, IsStrafing: {isStrafing}");
        }
    }

    /// <summary>
    /// ENHANCED: Update the target pole position based on strafe input
    /// </summary>
    private void UpdateLeaningTarget()
    {
        if (!IsSwimmingStateActive() || !isStrafing)
        {
            // Return to center when not strafing or not in swimming state
            targetPoleTargetX = 0f;
        }
        else
        {
            if (depthManager.CurrentSwimmingDepthState != SwimmingDepthState.UnderwaterSwimming)
            {
                // Return to center when not underwater
                targetPoleTargetX = 0f;
            }
            else
                // Set target position based on strafe direction
                targetPoleTargetX = currentStrafeInput * maxLeaningOffset;
        }

        if (debugLeaning && Time.frameCount % 30 == 0)
        {
            DebugLog($"Leaning Target: {targetPoleTargetX:F2}, Swimming State Active: {IsSwimmingStateActive()}");
        }
    }

    /// <summary>
    /// ENHANCED: Apply smooth interpolation to the pole target position
    /// </summary>
    private void ApplyLeaningToTarget()
    {
        // Smooth interpolation towards target position
        currentPoleTargetX = Mathf.MoveTowards(
            currentPoleTargetX,
            targetPoleTargetX,
            leaningSpeed * maxLeaningOffset * Time.deltaTime
        );

        // Calculate new local position for pole target
        Vector3 newLocalPosition = originalPoleTargetLocalPosition;
        newLocalPosition.x = currentPoleTargetX;

        // Apply the position to the pole target
        aimIKPoleTarget.localPosition = newLocalPosition;

        if (debugLeaning && Mathf.Abs(currentPoleTargetX) > 0.1f && Time.frameCount % 30 == 0)
        {
            DebugLog($"Pole Target Position Applied: {currentPoleTargetX:F2} (Original: {originalPoleTargetLocalPosition.x:F2})");
        }
    }

    /// <summary>
    /// ENHANCED: Update and track the current lean direction
    /// </summary>
    private void UpdateLeanDirection()
    {
        LeanDirection newLeanDirection;

        if (!isStrafing || Mathf.Abs(currentPoleTargetX) < 0.5f)
        {
            newLeanDirection = LeanDirection.None;
        }
        else if (currentPoleTargetX > 0f)
        {
            newLeanDirection = LeanDirection.Right;
        }
        else
        {
            newLeanDirection = LeanDirection.Left;
        }

        // Fire event if direction changed
        if (newLeanDirection != currentLeanDirection)
        {
            LeanDirection previousDirection = currentLeanDirection;
            currentLeanDirection = newLeanDirection;
            OnLeanDirectionChanged?.Invoke(previousDirection, newLeanDirection);

            if (debugLeaning)
            {
                DebugLog($"Lean direction changed: {previousDirection} -> {newLeanDirection}");
            }
        }
    }

    /// <summary>
    /// ENHANCED: Check if the swimming state allows for leaning
    /// </summary>
    private bool IsSwimmingStateActive()
    {
        // Allow leaning when swimming is active or near surface (but not when disabled or exiting)
        return currentDepthState == SwimmingDepthState.UnderwaterSwimming ||
               currentDepthState == SwimmingDepthState.SurfaceSwimming ||
               currentDepthState == SwimmingDepthState.EnteringSwimming;
    }

    /// <summary>
    /// ENHANCED: Reset pole target to original position
    /// </summary>
    private void ResetPoleTargetToOriginal()
    {
        if (aimIKPoleTarget != null && poleTargetOriginalPositionStored)
        {
            currentPoleTargetX = 0f;
            targetPoleTargetX = 0f;
            aimIKPoleTarget.localPosition = originalPoleTargetLocalPosition;
            currentLeanDirection = LeanDirection.None;

            DebugLog("Pole target reset to original position");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get current body rotation X angle
    /// </summary>
    private float GetBodyRotationX()
    {
        if (playerBody == null) return 0f;
        return NormalizeAngle(playerBody.eulerAngles.x);
    }

    /// <summary>
    /// Normalize angle to -180 to 180 range
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    #endregion

    #region UPDATED: Depth Manager Event Handlers

    /// <summary>
    /// UPDATED: Handle depth state changes from depth manager
    /// </summary>
    private void OnDepthStateChanged(SwimmingDepthState previousState, SwimmingDepthState newState)
    {
        DebugLog($"Depth state changed: {previousState} -> {newState}");

        // Update our current state
        currentDepthState = newState;

        // Handle specific state transitions
        switch (newState)
        {
            case SwimmingDepthState.Disabled:
                // Force upright when disabled
                targetBodyRotationX = 0f;
                ResetPoleTargetToOriginal();
                break;

            case SwimmingDepthState.ExitingSwimming:
                // Start returning to upright
                targetBodyRotationX = Mathf.Lerp(targetBodyRotationX, 0f, 0.5f);
                ResetPoleTargetToOriginal();
                break;

            case SwimmingDepthState.SurfaceSwimming:
                DebugLog("Near surface - applying reduced rotation strength");
                break;

            case SwimmingDepthState.UnderwaterSwimming:
                DebugLog("Deep swimming - full rotation strength");
                break;
        }

        // Fire our own event for external systems
        OnRotationStateChanged?.Invoke(previousState, newState);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Force the body to return to upright orientation
    /// </summary>
    public void ForceUprightOrientation()
    {
        targetBodyRotationX = 0f;
        currentBodyRotationX = 0f;
        ApplyRotationToBody();

        // ENHANCED: Also reset leaning
        ResetPoleTargetToOriginal();

        DebugLog("Forced upright orientation");
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SwimmingBodyRotationController] {message}");
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        // Draw current body orientation
        if (playerBody != null && IsRotationActive)
        {
            Vector3 bodyPosition = playerBody.position;

            // Draw body forward direction
            Gizmos.color = Color.cyan;
            Vector3 bodyForward = playerBody.forward * 2f;
            Gizmos.DrawRay(bodyPosition, bodyForward);

            // Draw rotation angle indicator
            Gizmos.color = Color.yellow;
            Vector3 rotationIndicator = Quaternion.AngleAxis(currentBodyRotationX, Vector3.right) * Vector3.forward;
            Gizmos.DrawRay(bodyPosition + Vector3.up * 0.5f, rotationIndicator * 1.5f);

            // Draw rotation limits
            if (enableRotationLimiting)
            {
                Gizmos.color = Color.red;
                Vector3 maxDownDir = Quaternion.AngleAxis(maxDownwardRotation, Vector3.right) * Vector3.forward;
                Vector3 maxUpDir = Quaternion.AngleAxis(maxUpwardRotation, Vector3.right) * Vector3.forward;

                Gizmos.DrawRay(bodyPosition + Vector3.up * 0.3f, maxDownDir * 1f);
                Gizmos.DrawRay(bodyPosition + Vector3.up * 0.3f, maxUpDir * 1f);
            }
        }

        // ENHANCED: Draw leaning visualization
        if (enableLeaningEffect && aimIKPoleTarget != null && IsLeaningActive)
        {
            // Draw pole target position
            Gizmos.color = currentLeanDirection == LeanDirection.Left ? Color.blue :
                          currentLeanDirection == LeanDirection.Right ? Color.green : Color.white;
            Gizmos.DrawWireSphere(aimIKPoleTarget.position, 0.1f);

            // Draw leaning direction indicator
            if (isStrafing)
            {
                Vector3 leanDirection = currentLeanDirection == LeanDirection.Left ? Vector3.left : Vector3.right;
                Gizmos.DrawRay(playerBody.position, leanDirection * CurrentLeanAmount);
            }

            // Draw original pole position for reference
            if (poleTargetOriginalPositionStored)
            {
                Gizmos.color = Color.gray;
                Vector3 originalWorldPos = aimIKPoleTarget.parent != null ?
                    aimIKPoleTarget.parent.TransformPoint(originalPoleTargetLocalPosition) :
                    originalPoleTargetLocalPosition;
                Gizmos.DrawWireCube(originalWorldPos, Vector3.one * 0.05f);
            }
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        // ENHANCED: Reset pole target before cleanup
        if (enableLeaningEffect)
        {
            ResetPoleTargetToOriginal();
        }

        // UPDATED: Unsubscribe from depth manager events
        if (depthManager != null)
        {
            depthManager.OnSwimmingDepthStateChanged -= OnDepthStateChanged;
        }
    }

    #endregion
}

/// <summary>
/// ENHANCED: Directions for the leaning system
/// </summary>
public enum LeanDirection
{
    None,    // No leaning - pole target at center
    Left,    // Leaning left - pole target moved to negative X
    Right    // Leaning right - pole target moved to positive X
}