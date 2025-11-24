using UnityEngine;
using Sirenix.OdinInspector;
using System;

/// <summary>
/// Enhanced ADS Setup system for configuring weapon sight alignment.
/// Handles real-time adjustment of ADS position and rotation offsets with immediate visual feedback.
/// Now supports both RangedWeapons (guns) and Bows with unified ADS configuration.
/// </summary>
public class ADSSetup : MonoBehaviour
{
    [FoldoutGroup("Setup References")]
    [Tooltip("The ADS position transform to manipulate during setup")]
    [SerializeField] private Transform adsPosition;

    [FoldoutGroup("Setup References")]
    [Tooltip("The ADS controller that handles runtime ADS behavior")]
    [SerializeField] private ADSController adsController;

    [FoldoutGroup("Setup References")]
    [Tooltip("Equipment visual manager to get current weapon data")]
    [SerializeField] private EquippedItemVisualManager equipmentVisualManager;

    [FoldoutGroup("Setup References")]
    [Tooltip("Camera controller for camera manipulation during setup")]
    [SerializeField] private CameraController cameraController;

    [FoldoutGroup("Setup Configuration")]
    [Tooltip("How often to update camera during real-time setup (updates per second)")]
    [SerializeField] private float setupUpdateRate = 30f;

    [FoldoutGroup("Setup Configuration")]
    [Tooltip("Sensitivity for transform manipulation during setup")]
    [SerializeField] private float manipulationSensitivity = 1f;

    [FoldoutGroup("Current Setup State")]
    [SerializeField, ReadOnly] private bool isInSetupMode = false;
    [SerializeField, ReadOnly] private ItemData currentWeaponData;
    [SerializeField, ReadOnly] private RangedWeaponData currentRangedWeaponData;
    [SerializeField, ReadOnly] private BowData currentBowData;
    [SerializeField, ReadOnly] private ItemType currentWeaponType;
    [SerializeField, ReadOnly] private Vector3 originalADSPosition;
    [SerializeField, ReadOnly] private Vector3 originalADSRotation;

    [FoldoutGroup("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showSetupGizmos = true;

    // Setup state tracking
    private Vector3 setupStartPosition;
    private Vector3 setupStartRotation;
    private float lastUpdateTime;
    private bool hasUnsavedChanges = false;

    // Events
    public event Action<bool> OnSetupModeChanged;
    public event Action<ItemData> OnADSConfigurationSaved;

    #region Properties

    public bool IsInSetupMode => isInSetupMode;
    public bool HasUnsavedChanges => hasUnsavedChanges;
    public ItemData CurrentWeaponData => currentWeaponData;
    public ItemType CurrentWeaponType => currentWeaponType;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        FindReferences();
        ValidateSetup();
    }

    private void Update()
    {
        if (isInSetupMode)
        {
            HandleSetupModeUpdate();
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Auto-find required references if not manually assigned
    /// </summary>
    private void FindReferences()
    {
        if (adsController == null)
            adsController = GetComponent<ADSController>();

        if (equipmentVisualManager == null)
            equipmentVisualManager = GetComponent<EquippedItemVisualManager>();

        if (cameraController == null)
            cameraController = GetComponent<CameraController>();

        DebugLog("References found and assigned");
    }

    /// <summary>
    /// Validate that all required components are present
    /// </summary>
    private void ValidateSetup()
    {
        bool isValid = true;

        if (adsPosition == null)
        {
            Debug.LogError("[ADSSetup] ADS Position transform not assigned!");
            isValid = false;
        }

        if (adsController == null)
        {
            Debug.LogError("[ADSSetup] ADS Controller not found!");
            isValid = false;
        }

        if (equipmentVisualManager == null)
        {
            Debug.LogError("[ADSSetup] Equipment Visual Manager not found!");
            isValid = false;
        }

        if (cameraController == null)
        {
            Debug.LogError("[ADSSetup] Camera Controller not found!");
            isValid = false;
        }

        if (!isValid)
        {
            Debug.LogError("[ADSSetup] Setup validation failed! ADS setup will not work properly.");
        }
        else
        {
            DebugLog("ADSSetup validation passed");
        }
    }

    #endregion

    #region Setup Mode Management

    /// <summary>
    /// Enter ADS setup mode for the currently equipped weapon (gun or bow)
    /// </summary>
    [FoldoutGroup("Setup Controls")]
    [Button("Enter Setup Mode")]
    public void EnterSetupMode()
    {
        if (isInSetupMode)
        {
            DebugLog("Already in setup mode");
            return;
        }

        // Get current weapon data
        if (!GetCurrentWeaponData())
        {
            Debug.LogError("[ADSSetup] Cannot enter setup mode: No compatible weapon equipped!");
            return;
        }

        // Store original values
        StoreOriginalValues();

        // Apply current weapon's ADS settings to the ADS position transform
        ApplyWeaponSettingsToTransform();

        // Enable setup mode
        isInSetupMode = true;
        hasUnsavedChanges = false;

        // Enable real-time updates in ADS controller
        if (adsController != null)
        {
            adsController.SetRealTimePositionUpdates(true);
            // Force ADS state for immediate preview
            adsController.ForceADSState(true);
        }

        OnSetupModeChanged?.Invoke(true);
        DebugLog($"Entered setup mode for {GetWeaponTypeString()}: {currentWeaponData.itemName}");
    }

    /// <summary>
    /// Exit ADS setup mode
    /// </summary>
    [FoldoutGroup("Setup Controls")]
    [Button("Exit Setup Mode")]
    public void ExitSetupMode()
    {
        if (!isInSetupMode)
        {
            DebugLog("Not in setup mode");
            return;
        }

        // Warn about unsaved changes
        if (hasUnsavedChanges)
        {
            DebugLog("Warning: Exiting setup mode with unsaved changes!");
        }

        // Disable real-time updates
        if (adsController != null)
        {
            adsController.SetRealTimePositionUpdates(false);
            adsController.ForceADSState(false);
        }

        // Reset to original values if not saved
        if (hasUnsavedChanges)
        {
            RestoreOriginalValues();
        }

        isInSetupMode = false;
        hasUnsavedChanges = false;
        currentWeaponData = null;
        currentRangedWeaponData = null;
        currentBowData = null;
        currentWeaponType = ItemType.Consumable; // Reset to default

        OnSetupModeChanged?.Invoke(false);
        DebugLog("Exited setup mode");
    }

    /// <summary>
    /// Handle real-time updates during setup mode
    /// </summary>
    private void HandleSetupModeUpdate()
    {
        if (Time.time - lastUpdateTime < (1f / setupUpdateRate))
            return;

        lastUpdateTime = Time.time;

        // Check if transform has changed
        if (HasTransformChanged())
        {
            hasUnsavedChanges = true;

            // Send immediate update to camera controller to prevent interpolation issues
            if (cameraController != null && adsPosition != null)
            {
                cameraController.SetADSRotationOffsetImmediate(adsPosition.localEulerAngles);
            }

            DebugLog($"ADS transform updated - Position: {adsPosition.localPosition}, Rotation: {adsPosition.localEulerAngles}");
        }
    }

    #endregion

    #region Weapon Data Management

    /// <summary>
    /// Get current equipped weapon data (supports both guns and bows)
    /// </summary>
    private bool GetCurrentWeaponData()
    {
        if (equipmentVisualManager == null)
            return false;

        currentWeaponData = equipmentVisualManager.GetCurrentEquippedItemData();

        if (currentWeaponData == null)
        {
            DebugLog("No item currently equipped");
            return false;
        }

        currentWeaponType = currentWeaponData.itemType;

        // Handle ranged weapons (guns)
        if (currentWeaponType == ItemType.RangedWeapon)
        {
            currentRangedWeaponData = currentWeaponData.RangedWeaponData;
            currentBowData = null;

            if (currentRangedWeaponData == null)
            {
                Debug.LogError($"Ranged weapon {currentWeaponData.itemName} has no RangedWeaponData!");
                return false;
            }

            DebugLog($"Got ranged weapon data for: {currentWeaponData.itemName}");
            return true;
        }
        // Handle bows
        else if (currentWeaponType == ItemType.Bow)
        {
            currentBowData = currentWeaponData.BowData;
            currentRangedWeaponData = null;

            if (currentBowData == null)
            {
                Debug.LogError($"Bow {currentWeaponData.itemName} has no BowData!");
                return false;
            }

            DebugLog($"Got bow data for: {currentWeaponData.itemName}");
            return true;
        }
        else
        {
            DebugLog($"Equipped item {currentWeaponData.itemName} is not a ranged weapon or bow (Type: {currentWeaponType})");
            return false;
        }
    }

    /// <summary>
    /// Apply current weapon's ADS settings to the transform
    /// </summary>
    private void ApplyWeaponSettingsToTransform()
    {
        if (adsPosition == null)
            return;

        Vector3 positionOffset = Vector3.zero;
        Vector3 rotationOffset = Vector3.zero;

        // Get offsets based on weapon type
        if (currentWeaponType == ItemType.RangedWeapon && currentRangedWeaponData != null)
        {
            positionOffset = currentRangedWeaponData.adsPositionOffset;
            rotationOffset = currentRangedWeaponData.adsRotationOffset;
        }
        else if (currentWeaponType == ItemType.Bow && currentBowData != null)
        {
            positionOffset = currentBowData.adsPositionOffset;
            rotationOffset = currentBowData.adsRotationOffset;
        }

        // Apply to transform
        adsPosition.localPosition = positionOffset;
        adsPosition.localEulerAngles = rotationOffset;

        DebugLog($"Applied {GetWeaponTypeString()} settings to transform - Pos: {adsPosition.localPosition}, Rot: {adsPosition.localEulerAngles}");
    }

    /// <summary>
    /// Get current ADS configuration from the appropriate weapon data
    /// </summary>
    private (Vector3 position, Vector3 rotation) GetCurrentADSConfiguration()
    {
        if (currentWeaponType == ItemType.RangedWeapon && currentRangedWeaponData != null)
        {
            return (currentRangedWeaponData.adsPositionOffset, currentRangedWeaponData.adsRotationOffset);
        }
        else if (currentWeaponType == ItemType.Bow && currentBowData != null)
        {
            return (currentBowData.adsPositionOffset, currentBowData.adsRotationOffset);
        }

        return (Vector3.zero, Vector3.zero);
    }

    /// <summary>
    /// Check if current weapon has ADS configuration
    /// </summary>
    private bool IsCurrentWeaponConfigured()
    {
        if (currentWeaponType == ItemType.RangedWeapon && currentRangedWeaponData != null)
        {
            return currentRangedWeaponData.IsADSConfigured();
        }
        else if (currentWeaponType == ItemType.Bow && currentBowData != null)
        {
            return currentBowData.IsADSConfigured();
        }

        return false;
    }

    /// <summary>
    /// Store original transform values
    /// </summary>
    private void StoreOriginalValues()
    {
        if (adsPosition == null)
            return;

        originalADSPosition = adsPosition.localPosition;
        originalADSRotation = adsPosition.localEulerAngles;
        setupStartPosition = originalADSPosition;
        setupStartRotation = originalADSRotation;

        DebugLog($"Stored original values - Pos: {originalADSPosition}, Rot: {originalADSRotation}");
    }

    /// <summary>
    /// Restore original transform values
    /// </summary>
    private void RestoreOriginalValues()
    {
        if (adsPosition == null)
            return;

        adsPosition.localPosition = originalADSPosition;
        adsPosition.localEulerAngles = originalADSRotation;

        DebugLog("Restored original transform values");
    }

    /// <summary>
    /// Check if transform has changed since last update
    /// </summary>
    private bool HasTransformChanged()
    {
        if (adsPosition == null)
            return false;

        bool positionChanged = Vector3.Distance(adsPosition.localPosition, setupStartPosition) > 0.001f;
        bool rotationChanged = Vector3.Distance(adsPosition.localEulerAngles, setupStartRotation) > 0.1f;

        if (positionChanged || rotationChanged)
        {
            setupStartPosition = adsPosition.localPosition;
            setupStartRotation = adsPosition.localEulerAngles;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get weapon type as a readable string
    /// </summary>
    private string GetWeaponTypeString()
    {
        return currentWeaponType switch
        {
            ItemType.RangedWeapon => "Gun",
            ItemType.Bow => "Bow",
            _ => "Unknown"
        };
    }

    #endregion

    #region Save/Load Operations

    /// <summary>
    /// Save current ADS settings to the weapon data (supports both guns and bows)
    /// </summary>
    [FoldoutGroup("Setup Controls")]
    [Button("Save ADS Configuration")]
    public void SaveADSConfiguration()
    {
        if (!isInSetupMode)
        {
            Debug.LogError("[ADSSetup] Cannot save: Not in setup mode!");
            return;
        }

        if (currentWeaponData == null)
        {
            Debug.LogError("[ADSSetup] Cannot save: No weapon data available!");
            return;
        }

        // Calculate offsets from current transform
        Vector3 positionOffset = adsPosition.localPosition;
        Vector3 rotationOffset = adsPosition.localEulerAngles;

        // Create setup info
        string setupInfo = $"Configured on {DateTime.Now:yyyy-MM-dd HH:mm:ss} for {currentWeaponData.itemName}";

        // Update weapon data based on type
        bool saveSuccessful = false;

        if (currentWeaponType == ItemType.RangedWeapon && currentRangedWeaponData != null)
        {
            currentRangedWeaponData.UpdateADSConfiguration(positionOffset, rotationOffset, setupInfo);
            saveSuccessful = true;
        }
        else if (currentWeaponType == ItemType.Bow && currentBowData != null)
        {
            currentBowData.UpdateADSConfiguration(positionOffset, rotationOffset, setupInfo);
            saveSuccessful = true;
        }

        if (saveSuccessful)
        {
            // Mark asset as dirty for saving
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(currentWeaponData);
#endif

            hasUnsavedChanges = false;

            OnADSConfigurationSaved?.Invoke(currentWeaponData);
            DebugLog($"Saved ADS configuration for {GetWeaponTypeString()} {currentWeaponData.itemName}: Pos={positionOffset}, Rot={rotationOffset}");
        }
        else
        {
            Debug.LogError("[ADSSetup] Failed to save: Invalid weapon data!");
        }
    }

    /// <summary>
    /// Reset current weapon's ADS configuration to defaults
    /// </summary>
    [FoldoutGroup("Setup Controls")]
    [Button("Reset ADS Configuration")]
    public void ResetADSConfiguration()
    {
        if (!isInSetupMode)
        {
            Debug.LogError("[ADSSetup] Cannot reset: Not in setup mode!");
            return;
        }

        if (currentWeaponData == null)
        {
            Debug.LogError("[ADSSetup] Cannot reset: No weapon data available!");
            return;
        }

        // Reset weapon data based on type
        if (currentWeaponType == ItemType.RangedWeapon && currentRangedWeaponData != null)
        {
            currentRangedWeaponData.ResetADSConfiguration();
        }
        else if (currentWeaponType == ItemType.Bow && currentBowData != null)
        {
            currentBowData.ResetADSConfiguration();
        }

        // Reset transform
        adsPosition.localPosition = Vector3.zero;
        adsPosition.localEulerAngles = Vector3.zero;

        // Update tracking
        setupStartPosition = Vector3.zero;
        setupStartRotation = Vector3.zero;
        hasUnsavedChanges = true;

        DebugLog($"Reset ADS configuration for {GetWeaponTypeString()} {currentWeaponData.itemName}");
    }

    #endregion

    #region Manual Adjustment Helpers

    /// <summary>
    /// Adjust ADS position by a specific delta (useful for fine-tuning)
    /// </summary>
    public void AdjustPosition(Vector3 delta)
    {
        if (!isInSetupMode || adsPosition == null)
            return;

        adsPosition.localPosition += delta * manipulationSensitivity;
        hasUnsavedChanges = true;
        DebugLog($"Adjusted position by {delta * manipulationSensitivity}");
    }

    /// <summary>
    /// Adjust ADS rotation by a specific delta (useful for fine-tuning)
    /// </summary>
    public void AdjustRotation(Vector3 delta)
    {
        if (!isInSetupMode || adsPosition == null)
            return;

        adsPosition.localEulerAngles += delta * manipulationSensitivity;
        hasUnsavedChanges = true;
        DebugLog($"Adjusted rotation by {delta * manipulationSensitivity}");
    }

    /// <summary>
    /// Set manipulation sensitivity for fine-tuning
    /// </summary>
    public void SetManipulationSensitivity(float sensitivity)
    {
        manipulationSensitivity = Mathf.Max(0.01f, sensitivity);
        DebugLog($"Set manipulation sensitivity to {manipulationSensitivity}");
    }

    #endregion

    #region Public API and Utilities

    /// <summary>
    /// Check if a specific weapon has ADS configuration (supports both guns and bows)
    /// </summary>
    public bool IsWeaponConfigured(ItemData weaponData)
    {
        if (weaponData == null) return false;

        if (weaponData.itemType == ItemType.RangedWeapon && weaponData.RangedWeaponData != null)
        {
            return weaponData.RangedWeaponData.IsADSConfigured();
        }
        else if (weaponData.itemType == ItemType.Bow && weaponData.BowData != null)
        {
            return weaponData.BowData.IsADSConfigured();
        }

        return false;
    }

    /// <summary>
    /// Get ADS configuration info for a weapon (supports both guns and bows)
    /// </summary>
    public string GetWeaponADSInfo(ItemData weaponData)
    {
        if (weaponData == null) return "No weapon data";

        if (weaponData.itemType == ItemType.RangedWeapon && weaponData.RangedWeaponData != null)
        {
            return weaponData.RangedWeaponData.GetADSDebugInfo();
        }
        else if (weaponData.itemType == ItemType.Bow && weaponData.BowData != null)
        {
            return weaponData.BowData.GetADSDebugInfo();
        }

        return "Unsupported weapon type";
    }

    /// <summary>
    /// Force refresh of current weapon data (useful after weapon switching)
    /// </summary>
    public void RefreshCurrentWeapon()
    {
        if (isInSetupMode)
        {
            DebugLog("Refreshing weapon data during setup mode");
            GetCurrentWeaponData();
            ApplyWeaponSettingsToTransform();
        }
    }

    /// <summary>
    /// Get setup status information
    /// </summary>
    public string GetSetupStatusInfo()
    {
        if (!isInSetupMode)
            return "Not in setup mode";

        string weaponInfo = currentWeaponData != null ? $"{GetWeaponTypeString()}: {currentWeaponData.itemName}" : "Unknown";
        string changesInfo = hasUnsavedChanges ? "Has unsaved changes" : "No changes";
        string posInfo = adsPosition != null ? $"Pos: {adsPosition.localPosition:F3}" : "No position";
        string rotInfo = adsPosition != null ? $"Rot: {adsPosition.localEulerAngles:F1}" : "No rotation";

        return $"Setup Mode Active - {weaponInfo}, {changesInfo}, {posInfo}, {rotInfo}";
    }

    #endregion

    #region Debug and Gizmos

    private void OnDrawGizmos()
    {
        if (!showSetupGizmos || !isInSetupMode || adsPosition == null)
            return;

        // Draw ADS position and orientation with color coding for weapon type
        Color weaponColor = currentWeaponType switch
        {
            ItemType.RangedWeapon => Color.cyan,
            ItemType.Bow => Color.yellow,
            _ => Color.white
        };

        Gizmos.color = weaponColor;
        Gizmos.DrawWireSphere(adsPosition.position, 0.02f);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(adsPosition.position, adsPosition.forward * 0.1f);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(adsPosition.position, adsPosition.up * 0.05f);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(adsPosition.position, adsPosition.right * 0.05f);
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ADSSetup] {message}");
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (isInSetupMode)
        {
            ExitSetupMode();
        }
    }

    #endregion
}