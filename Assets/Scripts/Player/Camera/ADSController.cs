using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;
using System;

/// <summary>
/// Enhanced ADS Controller with improved rotation offset handling for component-wise rotation.
/// ENHANCED: Better integration with component-wise rotation system to prevent somersaults.
/// Provides smooth, predictable ADS transitions with proper rotation offset management.
/// </summary>
public class ADSController : MonoBehaviour
{
    [FoldoutGroup("ADS Setup")]
    [Tooltip("Transform representing the ADS camera position")]
    [SerializeField] private Transform adsPosition;

    [FoldoutGroup("ADS Settings")]
    [Tooltip("Duration for ADS transition in seconds")]
    [SerializeField] private float adsTransitionDuration = 0.25f;

    [FoldoutGroup("ADS Settings")]
    [Tooltip("Easing curve for ADS transition")]
    [SerializeField] private Ease adsTransitionEase = Ease.OutCubic;

    [FoldoutGroup("ADS Settings")]
    [Tooltip("Look sensitivity multiplier when aiming")]
    [SerializeField] private float adsLookSensitivityMultiplier = 0.5f;

    [FoldoutGroup("ADS Rotation")]
    [Tooltip("Smoothing factor for rotation offset transitions")]
    [SerializeField] private float rotationSmoothness = 0.1f;

    [FoldoutGroup("ADS Rotation")]
    [Tooltip("Maximum angular velocity for rotation changes (deg/sec)")]
    [SerializeField] private float maxAngularVelocity = 360f;

    [FoldoutGroup("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showADSGizmos = true;
    [SerializeField] private bool showRotationDebug = false;

    // Component references
    private CameraController cameraController;
    private AimController aimController;
    private EquippedItemVisualManager equipmentVisualManager;

    // ADS state
    private bool isAimingDownSights = false;
    private bool isTransitioningADS = false;
    private Vector3 originalCameraRootLocalPosition;
    private Vector3 originalCameraRootLocalRotation;

    // ENHANCED: Improved rotation offset handling
    private Vector3 currentADSRotationOffset = Vector3.zero;
    private Vector3 targetADSRotationOffset = Vector3.zero;
    private Vector3 lastValidRotationOffset = Vector3.zero;

    // Component-wise rotation velocity tracking
    private Vector3 rotationOffsetVelocity = Vector3.zero;
    private bool isRotationOffsetTransitioning = false;

    // Real-time testing
    private bool realTimePositionUpdates = false;
    private Vector3 lastADSPosition;
    private float realTimeUpdateTimer = 0f;
    private float realTimeUpdateRate = 30f;

    // Animation tweeners
    private Tweener adsPositionTweener;
    private Tweener adsRotationTweener;

    // Events
    public event Action<bool, float> OnADSStateChanged;
    public event Action<Vector3> OnADSRotationOffsetChanged;

    #region Properties

    public bool IsAimingDownSights => isAimingDownSights;
    public bool IsTransitioningADS => isTransitioningADS;
    public Vector3 CurrentADSRotationOffset => currentADSRotationOffset;
    public Vector3 TargetADSRotationOffset => targetADSRotationOffset;

    #endregion

    #region Initialization

    public void Initialize(CameraController controller, PlayerController player)
    {
        cameraController = controller;

        // Get aim controller reference
        aimController = GetComponent<AimController>();

        // Get equipment visual manager reference
        equipmentVisualManager = GetComponent<EquippedItemVisualManager>();

        SetupADSSystem();

        DebugLog("Enhanced ADSController initialized with component-wise rotation support");
    }

    /// <summary>Setup the ADS system</summary>
    private void SetupADSSystem()
    {
        var cameraRoot = cameraController?.CameraRoot;
        if (cameraRoot == null)
        {
            Debug.LogError("[ADSController] Camera root not assigned! ADS system will not work.");
            return;
        }

        // Store original camera root position and rotation
        originalCameraRootLocalPosition = cameraRoot.localPosition;
        originalCameraRootLocalRotation = cameraRoot.localEulerAngles;

        // Initialize real-time testing variables
        if (adsPosition != null)
        {
            lastADSPosition = adsPosition.localPosition;
        }

        // Validate setup
        ValidateADSSetup();

        DebugLog($"ADS system setup - Original camera: {originalCameraRootLocalPosition}, Original rotation: {originalCameraRootLocalRotation}");
    }

    /// <summary>Validate the ADS setup</summary>
    private void ValidateADSSetup()
    {
        if (adsPosition == null)
        {
            Debug.LogWarning("[ADSController] ADS position not assigned! Set this in the inspector.");
        }

        if (aimController?.AimTarget == null)
        {
            Debug.LogWarning("[ADSController] Aim target not found! Sight alignment will not work.");
        }

        if (equipmentVisualManager == null)
        {
            Debug.LogWarning("[ADSController] Equipment visual manager not found! Weapon-specific ADS settings will not work.");
        }

        if (cameraController == null)
        {
            Debug.LogError("[ADSController] Camera controller not found! ADS rotation offsets will not work.");
        }
    }

    #endregion

    #region ADS System

    private void Update()
    {
        // Handle real-time position updates during testing
        if (realTimePositionUpdates && isAimingDownSights)
        {
            HandleRealTimePositionUpdates();
        }

        // Update ADS rotation offset smoothing
        UpdateADSRotationOffset();
    }

    /// <summary>
    /// ENHANCED: Update ADS rotation offset with component-wise smoothing
    /// </summary>
    private void UpdateADSRotationOffset()
    {
        // Check if we need to smooth to target offset
        if (!CameraRotationUtilities.RotationApproximately(currentADSRotationOffset, targetADSRotationOffset, 0.01f))
        {
            isRotationOffsetTransitioning = true;

            // Use component-wise angle interpolation to prevent somersaults
            currentADSRotationOffset = CameraRotationUtilities.SmoothAngles(
                currentADSRotationOffset,
                targetADSRotationOffset,
                ref rotationOffsetVelocity,
                rotationSmoothness
            );


            // Clamp angular velocity if specified
            if (maxAngularVelocity > 0f)
            {
                rotationOffsetVelocity = Vector3.ClampMagnitude(rotationOffsetVelocity, maxAngularVelocity);
            }

            // Notify camera controller of offset change
            OnADSRotationOffsetChanged?.Invoke(currentADSRotationOffset);

            // Debug info
            if (showRotationDebug && enableDebugLogs && Time.frameCount % 30 == 0)
            {
                DebugLog($"Rotation Offset Transition - Current: {currentADSRotationOffset:F2}, " +
                        $"Target: {targetADSRotationOffset:F2}, Velocity: {rotationOffsetVelocity:F2}");
            }
        }
        else if (isRotationOffsetTransitioning)
        {
            // Transition complete
            isRotationOffsetTransitioning = false;
            currentADSRotationOffset = targetADSRotationOffset;
            rotationOffsetVelocity = Vector3.zero;

            OnADSRotationOffsetChanged?.Invoke(currentADSRotationOffset);

            if (showRotationDebug && enableDebugLogs)
            {
                DebugLog($"Rotation Offset Transition Complete - Final: {currentADSRotationOffset:F2}");
            }
        }
    }

    /// <summary>
    /// Handle real-time position updates for testing
    /// </summary>
    private void HandleRealTimePositionUpdates()
    {
        if (adsPosition == null) return;

        // Update at specified rate
        realTimeUpdateTimer += Time.deltaTime;
        if (realTimeUpdateTimer >= (1f / realTimeUpdateRate))
        {
            realTimeUpdateTimer = 0f;

            // Check if ADS position has changed
            if (Vector3.Distance(adsPosition.localPosition, lastADSPosition) > 0.001f)
            {
                lastADSPosition = adsPosition.localPosition;
                UpdateCameraToADSPosition();
                DebugLog($"Real-time ADS position update: {lastADSPosition}");
            }
        }
    }

    /// <summary>
    /// Update camera position to match current ADS position (for real-time testing)
    /// </summary>
    private void UpdateCameraToADSPosition()
    {
        if (!isAimingDownSights || adsPosition == null) return;

        var cameraRoot = cameraController?.CameraRoot;
        if (cameraRoot == null) return;

        // Immediately update camera position to match ADS position
        cameraRoot.localPosition = adsPosition.localPosition;
        cameraRoot.localEulerAngles = adsPosition.localEulerAngles;

        // For real-time preview, send rotation offset immediately to camera controller
        if (realTimePositionUpdates && cameraController != null)
        {
            // Send offset directly to camera controller for immediate application
            cameraController.SetADSRotationOffsetImmediate(adsPosition.localEulerAngles);
        }
    }

    /// <summary>
    /// Get current weapon's ADS configuration
    /// </summary>
    private void UpdateWeaponADSConfiguration()
    {
        if (equipmentVisualManager == null)
        {
            DebugLog("No equipment visual manager found, using default ADS settings");
            return;
        }

        ItemData currentWeapon = equipmentVisualManager.GetCurrentEquippedItemData();

        if (currentWeapon?.itemType == ItemType.RangedWeapon && currentWeapon.RangedWeaponData != null)
        {
            var weaponData = currentWeapon.RangedWeaponData;

            // Update position offset (only if not using real-time updates)
            if (!realTimePositionUpdates && adsPosition != null)
            {
                adsPosition.localPosition = weaponData.adsPositionOffset;
            }

            DebugLog($"Updated ADS configuration for {currentWeapon.itemName}: " +
                    $"Pos={weaponData.adsPositionOffset}, Rot={weaponData.adsRotationOffset}");
        }
        else
        {
            DebugLog("No ranged weapon equipped or no weapon data found, using default ADS settings");
        }
    }

    /// <summary>
    /// ENHANCED: Set ADS rotation offset with improved transition management
    /// </summary>
    private void SetADSRotationOffset(Vector3 rotationOffset)
    {
        // Normalize the rotation offset to prevent interpolation issues
        Vector3 normalizedOffset = CameraRotationUtilities.NormalizeAngles(rotationOffset);

        // Check if this is a large rotation change that might cause issues
        if (CameraRotationUtilities.IsLargeRotationTransition(currentADSRotationOffset, normalizedOffset))
        {
            if (showRotationDebug && enableDebugLogs)
            {
                DebugLog($"Large rotation transition detected: {CameraRotationUtilities.GetRotationTransitionDebugInfo(currentADSRotationOffset, normalizedOffset)}");
            }
        }

        targetADSRotationOffset = normalizedOffset;

        if (!isTransitioningADS && !realTimePositionUpdates)
        {
            // Normal smooth transition
            // The Update loop will handle the interpolation
        }
        else if (realTimePositionUpdates)
        {
            // In real-time mode, apply immediately for instant feedback
            currentADSRotationOffset = targetADSRotationOffset;
            rotationOffsetVelocity = Vector3.zero;
            OnADSRotationOffsetChanged?.Invoke(currentADSRotationOffset);
        }

        // Store as last valid offset
        lastValidRotationOffset = normalizedOffset;

        if (showRotationDebug && enableDebugLogs)
        {
            DebugLog($"Set ADS rotation offset - Target: {targetADSRotationOffset:F2}, Current: {currentADSRotationOffset:F2}");
        }
    }

    /// <summary>Start aiming down sights</summary>
    public void StartAimingDownSights()
    {
        if (isAimingDownSights || isTransitioningADS || adsPosition == null)
            return;

        var cameraRoot = cameraController?.CameraRoot;
        if (cameraRoot == null) return;

        DebugLog("Starting ADS transition");

        // Update weapon configuration before starting ADS
        UpdateWeaponADSConfiguration();

        isAimingDownSights = true;
        isTransitioningADS = true;

        // Kill any existing tweens
        KillADSTweens();

        // Calculate target position and rotation
        Vector3 targetPosition = adsPosition.localPosition;
        Vector3 targetRotation = adsPosition.localEulerAngles;

        // Store current ADS position for real-time updates
        lastADSPosition = targetPosition;

        // Get weapon's rotation offset
        ItemData currentWeapon = equipmentVisualManager?.GetCurrentEquippedItemData();
        Vector3 weaponRotationOffset = Vector3.zero;

        if (currentWeapon?.itemType == ItemType.RangedWeapon && currentWeapon.RangedWeaponData != null)
        {
            weaponRotationOffset = currentWeapon.RangedWeaponData.adsRotationOffset;
        }

        // Set rotation offset for camera system
        SetADSRotationOffset(weaponRotationOffset);

        // Animate camera root to ADS position (only if not in real-time mode)
        if (!realTimePositionUpdates)
        {
            adsPositionTweener = cameraRoot.DOLocalMove(targetPosition, adsTransitionDuration)
                .SetEase(adsTransitionEase)
                .OnComplete(() =>
                {
                    isTransitioningADS = false;
                    DebugLog("ADS transition completed");
                });

            // Animate camera root rotation if needed
            if (targetRotation != originalCameraRootLocalRotation)
            {
                adsRotationTweener = cameraRoot.DOLocalRotate(targetRotation, adsTransitionDuration)
                    .SetEase(adsTransitionEase);
            }
        }
        else
        {
            // Immediate position update for real-time testing
            cameraRoot.localPosition = targetPosition;
            cameraRoot.localEulerAngles = targetRotation;
            isTransitioningADS = false;
            DebugLog("ADS activated immediately for real-time testing");
        }

        // Update sensitivity for aim controller
        aimController?.SetSensitivityMultiplier(adsLookSensitivityMultiplier);

        // Fire event
        OnADSStateChanged?.Invoke(true, adsLookSensitivityMultiplier);

        DebugLog($"ADS started - transitioning to position: {targetPosition}, rotation offset: {weaponRotationOffset:F2}");
    }

    /// <summary>Stop aiming down sights and restore original state</summary>
    public void StopAimingDownSights()
    {
        if (!isAimingDownSights || isTransitioningADS)
            return;

        var cameraRoot = cameraController?.CameraRoot;
        if (cameraRoot == null) return;

        DebugLog("Stopping ADS transition");
        isAimingDownSights = false;
        isTransitioningADS = true;

        // Kill any existing tweens
        KillADSTweens();

        // Reset rotation offset
        SetADSRotationOffset(Vector3.zero);

        // Animate camera root back to original position (only if not in real-time mode)
        if (!realTimePositionUpdates)
        {
            adsPositionTweener = cameraRoot.DOLocalMove(originalCameraRootLocalPosition, adsTransitionDuration)
                .SetEase(adsTransitionEase)
                .OnComplete(() =>
                {
                    isTransitioningADS = false;
                    DebugLog("ADS exit transition completed");
                });

            // Animate camera root back to original rotation
            adsRotationTweener = cameraRoot.DOLocalRotate(originalCameraRootLocalRotation, adsTransitionDuration)
                .SetEase(adsTransitionEase);
        }
        else
        {
            // Immediate position restore for real-time testing
            cameraRoot.localPosition = originalCameraRootLocalPosition;
            cameraRoot.localEulerAngles = originalCameraRootLocalRotation;
            isTransitioningADS = false;
            DebugLog("ADS deactivated immediately for real-time testing");
        }

        // Reset sensitivity for aim controller
        aimController?.SetSensitivityMultiplier(1f);

        // Fire event
        OnADSStateChanged?.Invoke(false, 1f);

        DebugLog($"ADS stopped - transitioning back to position: {originalCameraRootLocalPosition}");
    }

    #endregion

    #region Real-Time Testing API

    /// <summary>
    /// Enable or disable real-time position updates for testing
    /// </summary>
    public void SetRealTimePositionUpdates(bool enabled)
    {
        realTimePositionUpdates = enabled;

        if (enabled)
        {
            DebugLog("Real-time position updates ENABLED - move ADSPosition to see live camera updates");
        }
        else
        {
            DebugLog("Real-time position updates DISABLED");
        }
    }

    /// <summary>
    /// Check if real-time updates are currently enabled
    /// </summary>
    public bool IsRealTimeUpdatesEnabled() => realTimePositionUpdates;

    #endregion

    #region ADS Rotation Offset API

    /// <summary>
    /// Get current ADS rotation offset being applied
    /// </summary>
    public Vector3 GetCurrentADSRotationOffset()
    {
        return currentADSRotationOffset;
    }

    /// <summary>
    /// Get target ADS rotation offset (what we're transitioning to)
    /// </summary>
    public Vector3 GetTargetADSRotationOffset()
    {
        return targetADSRotationOffset;
    }

    /// <summary>
    /// Check if ADS rotation offset is currently transitioning
    /// </summary>
    public bool IsRotationOffsetTransitioning()
    {
        return isRotationOffsetTransitioning;
    }

    /// <summary>
    /// Manually set ADS rotation offset (used by setup system)
    /// </summary>
    public void SetManualRotationOffset(Vector3 offset)
    {
        if (realTimePositionUpdates)
        {
            SetADSRotationOffset(offset);
        }
    }

    /// <summary>
    /// Set rotation smoothness factor
    /// </summary>
    public void SetRotationSmoothness(float smoothness)
    {
        rotationSmoothness = Mathf.Max(0.01f, smoothness);
        DebugLog($"Rotation smoothness set to {rotationSmoothness:F3}");
    }

    /// <summary>
    /// Set maximum angular velocity for rotation changes
    /// </summary>
    public void SetMaxAngularVelocity(float velocity)
    {
        maxAngularVelocity = Mathf.Max(0f, velocity);
        DebugLog($"Max angular velocity set to {maxAngularVelocity:F1}°/s");
    }

    #endregion

    #region Public API

    /// <summary>Force immediate ADS state without animation</summary>
    public void ForceADSState(bool adsState)
    {
        var cameraRoot = cameraController?.CameraRoot;
        if (cameraRoot == null) return;

        KillADSTweens();

        isAimingDownSights = adsState;
        isTransitioningADS = false;

        if (adsState && adsPosition != null)
        {
            // Update weapon configuration
            UpdateWeaponADSConfiguration();

            cameraRoot.localPosition = adsPosition.localPosition;
            cameraRoot.localEulerAngles = adsPosition.localEulerAngles;
            aimController?.SetSensitivityMultiplier(adsLookSensitivityMultiplier);

            // Apply weapon's rotation offset immediately
            ItemData currentWeapon = equipmentVisualManager?.GetCurrentEquippedItemData();
            Vector3 weaponRotationOffset = Vector3.zero;

            if (currentWeapon?.itemType == ItemType.RangedWeapon && currentWeapon.RangedWeaponData != null)
            {
                weaponRotationOffset = currentWeapon.RangedWeaponData.adsRotationOffset;
            }

            // Force immediate application of rotation offset
            targetADSRotationOffset = CameraRotationUtilities.NormalizeAngles(weaponRotationOffset);
            currentADSRotationOffset = targetADSRotationOffset;
            rotationOffsetVelocity = Vector3.zero;
            isRotationOffsetTransitioning = false;

            OnADSRotationOffsetChanged?.Invoke(currentADSRotationOffset);

            // Store position for real-time updates
            lastADSPosition = adsPosition.localPosition;
        }
        else
        {
            cameraRoot.localPosition = originalCameraRootLocalPosition;
            cameraRoot.localEulerAngles = originalCameraRootLocalRotation;
            aimController?.SetSensitivityMultiplier(1f);

            // Reset rotation offset immediately
            targetADSRotationOffset = Vector3.zero;
            currentADSRotationOffset = Vector3.zero;
            rotationOffsetVelocity = Vector3.zero;
            isRotationOffsetTransitioning = false;

            OnADSRotationOffsetChanged?.Invoke(currentADSRotationOffset);
        }

        OnADSStateChanged?.Invoke(adsState, adsState ? adsLookSensitivityMultiplier : 1f);

        DebugLog($"Force set ADS state to: {adsState}");
    }

    /// <summary>
    /// Get current weapon's ADS configuration info
    /// </summary>
    public string GetCurrentWeaponADSInfo()
    {
        if (equipmentVisualManager == null)
            return "No equipment manager";

        ItemData currentWeapon = equipmentVisualManager.GetCurrentEquippedItemData();

        if (currentWeapon?.itemType == ItemType.RangedWeapon && currentWeapon.RangedWeaponData != null)
        {
            return currentWeapon.RangedWeaponData.GetADSDebugInfo();
        }

        return "No ranged weapon equipped";
    }

    /// <summary>
    /// Refresh weapon configuration (call after weapon switching)
    /// </summary>
    public void RefreshWeaponConfiguration()
    {
        UpdateWeaponADSConfiguration();
        DebugLog("Weapon ADS configuration refreshed");
    }

    /// <summary>
    /// Get rotation transition debug information
    /// </summary>
    public string GetRotationTransitionDebugInfo()
    {
        return CameraRotationUtilities.GetRotationTransitionDebugInfo(currentADSRotationOffset, targetADSRotationOffset);
    }

    #endregion

    #region Utility Methods

    /// <summary>Kill all ADS-related tweens</summary>
    private void KillADSTweens()
    {
        adsPositionTweener?.Kill();
        adsRotationTweener?.Kill();
    }

    /// <summary>
    /// Validate current rotation offset for potential issues
    /// </summary>
    private bool ValidateRotationOffset(Vector3 offset)
    {
        // Check for extreme values that might cause issues
        if (Mathf.Abs(offset.x) > 180f || Mathf.Abs(offset.y) > 180f || Mathf.Abs(offset.z) > 180f)
        {
            Debug.LogWarning($"[ADSController] Extreme rotation offset detected: {offset}. This may cause visual issues.");
            return false;
        }

        // Check for NaN or infinity values
        if (float.IsNaN(offset.x) || float.IsNaN(offset.y) || float.IsNaN(offset.z) ||
            float.IsInfinity(offset.x) || float.IsInfinity(offset.y) || float.IsInfinity(offset.z))
        {
            Debug.LogError($"[ADSController] Invalid rotation offset (NaN/Infinity): {offset}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Apply safety checks and corrections to rotation offset
    /// </summary>
    private Vector3 SafetyCheckRotationOffset(Vector3 offset)
    {
        // Normalize angles to prevent interpolation issues
        Vector3 safeOffset = CameraRotationUtilities.NormalizeAngles(offset);

        // Validate the offset
        if (!ValidateRotationOffset(safeOffset))
        {
            Debug.LogWarning($"[ADSController] Using fallback rotation offset due to validation failure");
            return lastValidRotationOffset; // Use last known good offset
        }

        return safeOffset;
    }

    #endregion

    #region Debug and Diagnostics

    /// <summary>
    /// Get comprehensive debug information about ADS state
    /// </summary>
    public string GetADSDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== ADS CONTROLLER DEBUG ===");
        info.AppendLine($"Is Aiming: {isAimingDownSights}");
        info.AppendLine($"Is Transitioning: {isTransitioningADS}");
        info.AppendLine($"Real-time Updates: {realTimePositionUpdates}");
        info.AppendLine($"Current Rotation Offset: {currentADSRotationOffset:F2}");
        info.AppendLine($"Target Rotation Offset: {targetADSRotationOffset:F2}");
        info.AppendLine($"Rotation Offset Velocity: {rotationOffsetVelocity:F2}");
        info.AppendLine($"Is Rotation Transitioning: {isRotationOffsetTransitioning}");
        info.AppendLine($"Last Valid Offset: {lastValidRotationOffset:F2}");
        info.AppendLine($"Rotation Smoothness: {rotationSmoothness:F3}");
        info.AppendLine($"Max Angular Velocity: {maxAngularVelocity:F1}°/s");

        if (adsPosition != null)
        {
            info.AppendLine($"ADS Position: {adsPosition.localPosition:F3}");
            info.AppendLine($"ADS Rotation: {adsPosition.localEulerAngles:F1}");
        }

        return info.ToString();
    }

    /// <summary>
    /// Test rotation offset with safety checks
    /// </summary>
    [FoldoutGroup("Debug")]
    [Button("Test Rotation Offset")]
    public void TestRotationOffset()
    {
        if (Application.isPlaying)
        {
            Vector3 testOffset = new Vector3(5f, 2f, 1f);
            SetADSRotationOffset(testOffset);
            DebugLog($"Testing rotation offset: {testOffset:F1}");
        }
    }

    /// <summary>
    /// Reset rotation offset to zero
    /// </summary>
    [FoldoutGroup("Debug")]
    [Button("Reset Rotation Offset")]
    public void ResetRotationOffset()
    {
        if (Application.isPlaying)
        {
            SetADSRotationOffset(Vector3.zero);
            DebugLog("Rotation offset reset to zero");
        }
    }

    /// <summary>
    /// Log current rotation transition information
    /// </summary>
    [FoldoutGroup("Debug")]
    [Button("Log Rotation Transition Info")]
    public void LogRotationTransitionInfo()
    {
        if (Application.isPlaying)
        {
            DebugLog(GetRotationTransitionDebugInfo());
        }
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EnhancedADSController] {message}");
        }
    }

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!showADSGizmos || adsPosition == null) return;

        // Draw ADS position
        Gizmos.color = isAimingDownSights ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(adsPosition.position, 0.02f);

        // Draw orientation
        Gizmos.color = Color.red;
        Gizmos.DrawRay(adsPosition.position, adsPosition.forward * 0.1f);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(adsPosition.position, adsPosition.up * 0.05f);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(adsPosition.position, adsPosition.right * 0.05f);

        // Draw rotation offset info
        if (showRotationDebug && isRotationOffsetTransitioning)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(adsPosition.position, 0.03f);
        }
    }

    #endregion

    #region Cleanup

    public void Cleanup()
    {
        // Kill any active tweens
        KillADSTweens();

        // Reset state
        isAimingDownSights = false;
        isTransitioningADS = false;
        isRotationOffsetTransitioning = false;

        // Reset offsets
        currentADSRotationOffset = Vector3.zero;
        targetADSRotationOffset = Vector3.zero;
        rotationOffsetVelocity = Vector3.zero;

        DebugLog("Enhanced ADSController cleaned up");
    }

    #endregion
}