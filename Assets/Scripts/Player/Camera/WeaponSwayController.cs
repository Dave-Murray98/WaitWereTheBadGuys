using UnityEngine;
using Sirenix.OdinInspector;
using System;
using RootMotion.FinalIK;

/// <summary>
/// Weapon Sway Controller that creates realistic weapon movement effects.
/// Works by applying rotation offsets to the Final IK aim transform to simulate weapon inertia and breathing.
/// Integrates with existing ADS, equipment, and player state systems.
/// </summary>
public class WeaponSwayController : MonoBehaviour
{
    [FoldoutGroup("Core Setup")]
    [Tooltip("The Final IK aim transform (child of head bone) that controls weapon aim direction")]
    [SerializeField] private Transform aimTransform;

    [Header("Base Sway Settings")]
    [FoldoutGroup("Sway Configuration")]
    [Tooltip("Base sway intensity multiplier (affects all sway sources)")]
    [SerializeField, Range(0f, 4f)] private float baseSwayIntensity = 2f;

    [FoldoutGroup("Sway Configuration")]
    [Tooltip("How much sway is reduced during ADS (0 = no sway, 1 = full sway)")]
    [SerializeField, Range(0f, 1f)] private float adsSwayReduction = 0.2f;

    [Header("Breathing Sway")]
    [FoldoutGroup("Breathing")]
    [Tooltip("Breathing sway intensity")]
    [SerializeField, Range(0f, 2f)] private float breathingSwayIntensity = 0.3f;

    [FoldoutGroup("Breathing")]
    [Tooltip("Breathing rate in breaths per minute")]
    [SerializeField, Range(5f, 30f)] private float breathingRate = 12f;

    [FoldoutGroup("Breathing")]
    [Tooltip("Maximum breathing sway angle in degrees")]
    [SerializeField, Range(0f, 5f)] private float maxBreathingAngle = 0.5f;

    [Header("Movement Sway")]
    [FoldoutGroup("Movement")]
    [Tooltip("Movement sway intensity")]
    [SerializeField, Range(0f, 3f)] private float movementSwayIntensity = 1f;

    [FoldoutGroup("Movement")]
    [Tooltip("Maximum movement sway angle in degrees")]
    [SerializeField, Range(0f, 10f)] private float maxMovementAngle = 2f;

    [FoldoutGroup("Movement")]
    [Tooltip("How quickly movement sway responds to velocity changes")]
    [SerializeField, Range(0.1f, 5f)] private float movementResponseSpeed = 2f;

    [Header("Look Input Lag")]
    [FoldoutGroup("Look Lag")]
    [Tooltip("Look input lag intensity")]
    [SerializeField, Range(0f, 2f)] private float lookLagIntensity = 0.8f;

    [FoldoutGroup("Look Lag")]
    [Tooltip("Maximum look lag angle in degrees")]
    [SerializeField, Range(0f, 15f)] private float maxLookLagAngle = 3f;

    [FoldoutGroup("Look Lag")]
    [Tooltip("How quickly weapon catches up to look direction")]
    [SerializeField, Range(0.1f, 10f)] private float lookLagRecoverySpeed = 4f;

    [Header("Player State Multipliers")]
    [FoldoutGroup("State Multipliers")]
    [Tooltip("Sway multiplier when standing")]
    [SerializeField, Range(0f, 2f)] private float standingSwayMultiplier = 1f;

    [FoldoutGroup("State Multipliers")]
    [Tooltip("Sway multiplier when crouching")]
    [SerializeField, Range(0f, 2f)] private float crouchingSwayMultiplier = 0.6f;

    [FoldoutGroup("State Multipliers")]
    [Tooltip("Sway multiplier when swimming")]
    [SerializeField, Range(0f, 2f)] private float swimmingSwayMultiplier = 1.5f;

    [Header("Performance")]
    [FoldoutGroup("Performance")]
    [Tooltip("Sway update rate (updates per second)")]
    [SerializeField, Range(10f, 60f)] private float swayUpdateRate = 30f;

    [FoldoutGroup("Performance")]
    [Tooltip("Use smoothed rotation application")]
    [SerializeField] private bool useSmoothRotation = true;

    [FoldoutGroup("Performance")]
    [Tooltip("Rotation smoothing speed")]
    [SerializeField, Range(1f, 10f)] private float rotationSmoothSpeed = 4f;

    [Header("Debug")]
    [FoldoutGroup("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showSwayGizmos = false;
    [SerializeField] private bool showSwayValues = false;

    // Component references
    private PlayerController playerController;
    private AimController aimController;
    private ADSController adsController;
    private EquippedItemManager equippedItemManager;

    // Sway state
    private Vector3 currentSwayRotation = Vector3.zero;
    private Vector3 targetSwayRotation = Vector3.zero;
    private Vector3 swayVelocity = Vector3.zero;

    // Breathing state
    private float breathingTime = 0f;
    private Vector3 breathingSway = Vector3.zero;

    // Movement state
    private Vector3 movementSway = Vector3.zero;
    private Vector3 lastVelocity = Vector3.zero;
    private Vector3 movementSwayVelocity = Vector3.zero;

    // Look lag state
    private Vector3 lookLagSway = Vector3.zero;
    private Vector3 lastLookInput = Vector3.zero;
    private Vector3 lookLagVelocity = Vector3.zero;

    // Update timing
    private float lastUpdateTime = 0f;
    private float updateInterval;

    // Current state tracking
    private bool isADSActive = false;
    private float currentItemSwayMultiplier = 1f;
    private float currentStateSwayMultiplier = 1f;
    private bool isInitialized = false;

    // Original aim transform rotation (for reference)
    private Vector3 originalAimRotation = Vector3.zero;

    // Events
    public event Action<Vector3> OnSwayRotationChanged;

    #region Properties

    public bool IsInitialized => isInitialized;
    public Vector3 CurrentSwayRotation => currentSwayRotation;
    public Vector3 TargetSwayRotation => targetSwayRotation;
    public float CurrentItemSwayMultiplier => currentItemSwayMultiplier;
    public bool IsSwayActive => targetSwayRotation.magnitude > 0.01f;

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize the weapon sway system
    /// </summary>
    public void Initialize(PlayerController controller)
    {
        playerController = controller;

        FindComponentReferences();
        SetupAimTransform();
        SetupEventSubscriptions();
        InitializeSwaySystem();

        isInitialized = true;
        DebugLog("WeaponSwayController initialized successfully");
    }

    /// <summary>
    /// Find all necessary component references
    /// </summary>
    private void FindComponentReferences()
    {
        // Find camera system components
        if (aimController == null)
            aimController = GetComponent<AimController>();

        if (adsController == null)
            adsController = GetComponent<ADSController>();

        // Find equipment manager
        equippedItemManager = EquippedItemManager.Instance;

        if (aimController == null)
            Debug.LogWarning("[WeaponSwayController] AimController not found! Look lag effects will not work.");

        if (adsController == null)
            Debug.LogWarning("[WeaponSwayController] ADSController not found! ADS sway reduction will not work.");

        if (equippedItemManager == null)
            Debug.LogWarning("[WeaponSwayController] EquippedItemManager not found! Item-specific sway will not work.");

        DebugLog("Component references established");
    }

    /// <summary>
    /// Setup aim transform reference
    /// </summary>
    private void SetupAimTransform()
    {
        if (aimTransform == null)
        {
            Debug.LogError("[WeaponSwayController] Aim transform not found! Please assign it manually in the inspector.");
            return;
        }

        // Store original rotation
        originalAimRotation = aimTransform.localEulerAngles;

        DebugLog($"Aim transform setup complete: {aimTransform.name}");
    }


    /// <summary>
    /// Setup event subscriptions
    /// </summary>
    private void SetupEventSubscriptions()
    {
        // Subscribe to ADS events
        if (adsController != null)
        {
            adsController.OnADSStateChanged += OnADSStateChanged;
        }

        // Subscribe to equipment events
        if (equippedItemManager != null)
        {
            equippedItemManager.OnItemEquipped += OnItemEquipped;
            equippedItemManager.OnItemUnequipped += OnItemUnequipped;
        }

        // Subscribe to camera input events for look lag
        if (aimController != null)
        {
            var cameraController = GetComponent<CameraController>();
            if (cameraController != null)
            {
                cameraController.OnLookInputChanged += OnLookInputChanged;
            }
        }

        DebugLog("Event subscriptions established");
    }

    /// <summary>
    /// Initialize sway system variables
    /// </summary>
    private void InitializeSwaySystem()
    {
        updateInterval = 1f / swayUpdateRate;
        lastUpdateTime = Time.time;

        // Initialize current state
        UpdateCurrentStateMultiplier();
        UpdateCurrentItemMultiplier();

        DebugLog("Sway system initialized");
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        if (!isInitialized || aimTransform == null) return;

        // Check if it's time for sway update
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateSwayCalculations();
            lastUpdateTime = Time.time;
        }

        // Apply rotation smoothing every frame for responsiveness
        ApplySwayRotation();
    }

    /// <summary>
    /// Update all sway calculations
    /// </summary>
    private void UpdateSwayCalculations()
    {
        // Update individual sway components
        UpdateBreathingSway();
        UpdateMovementSway();
        UpdateLookLagSway();

        // Combine all sway sources
        CombineSwayEffects();
    }

    /// <summary>
    /// Update breathing sway effect
    /// </summary>
    private void UpdateBreathingSway()
    {
        breathingTime += Time.time - lastUpdateTime;

        // Calculate breathing wave (sine wave for smooth motion)
        float breathingCycle = breathingTime * (breathingRate / 60f) * 2f * Mathf.PI;

        // Create breathing pattern (slight X and Y movement)
        float breathingX = Mathf.Sin(breathingCycle) * maxBreathingAngle;
        float breathingY = Mathf.Sin(breathingCycle * 0.7f) * maxBreathingAngle * 0.3f; // Slight Y component

        breathingSway = new Vector3(breathingX, breathingY, 0f) * breathingSwayIntensity;
    }

    /// <summary>
    /// Update movement-based sway
    /// </summary>
    private void UpdateMovementSway()
    {
        if (playerController == null) return;

        Vector3 currentVelocity = playerController.Velocity;
        Vector3 velocityDelta = currentVelocity - lastVelocity;
        lastVelocity = currentVelocity;

        // Calculate target movement sway based on velocity and acceleration
        Vector3 targetMovementSway = Vector3.zero;

        // Horizontal velocity sway
        float horizontalSpeed = new Vector2(currentVelocity.x, currentVelocity.z).magnitude;
        if (horizontalSpeed > 0.1f)
        {
            // Create sway based on movement direction
            Vector3 horizontalVel = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            Vector3 playerForward = playerController.transform.forward;
            Vector3 playerRight = playerController.transform.right;

            // Calculate forward/back and left/right movement components
            float forwardMovement = Vector3.Dot(horizontalVel.normalized, playerForward);
            float rightMovement = Vector3.Dot(horizontalVel.normalized, playerRight);

            // Apply sway based on movement (opposite to movement direction for realistic lag)
            targetMovementSway.x = -rightMovement * horizontalSpeed * maxMovementAngle;
            targetMovementSway.y = -forwardMovement * horizontalSpeed * maxMovementAngle * 0.5f;
        }

        // Apply velocity delta for additional responsiveness
        targetMovementSway += velocityDelta * movementSwayIntensity * 0.1f;

        // Smooth movement sway
        movementSway = Vector3.SmoothDamp(
            movementSway,
            targetMovementSway * movementSwayIntensity,
            ref movementSwayVelocity,
            1f / movementResponseSpeed
        );
    }

    /// <summary>
    /// Update look input lag sway
    /// </summary>
    private void UpdateLookLagSway()
    {
        // This will be updated by the OnLookInputChanged event
        // Here we just handle the recovery (weapon catching up to look direction)
        lookLagSway = Vector3.SmoothDamp(
            lookLagSway,
            Vector3.zero, // Always trying to return to zero (catch up to camera)
            ref lookLagVelocity,
            1f / lookLagRecoverySpeed
        );
    }

    /// <summary>
    /// Combine all sway effects into final target rotation
    /// </summary>
    private void CombineSwayEffects()
    {
        // Start with zero sway
        Vector3 combinedSway = Vector3.zero;

        // Add breathing sway
        combinedSway += breathingSway;

        // Add movement sway
        combinedSway += movementSway;

        // Add look lag sway
        combinedSway += lookLagSway;

        // Apply multipliers
        combinedSway *= baseSwayIntensity;
        combinedSway *= currentItemSwayMultiplier;
        combinedSway *= currentStateSwayMultiplier;

        // Apply ADS reduction
        if (isADSActive)
        {
            combinedSway *= adsSwayReduction;
        }

        // Set target sway rotation
        targetSwayRotation = combinedSway;

        // Debug output
        if (showSwayValues && enableDebugLogs && Time.frameCount % 60 == 0)
        {
            DebugLog($"Sway Components - Breathing: {breathingSway:F2}, Movement: {movementSway:F2}, " +
                    $"Look Lag: {lookLagSway:F2}, Final: {targetSwayRotation:F2}");
        }
    }

    /// <summary>
    /// Apply calculated sway rotation to aim transform
    /// </summary>
    private void ApplySwayRotation()
    {
        if (aimTransform == null)
        {
            Debug.LogWarning("[WeaponSwayController] Aim transform not found!");
            return;
        }

        // Smooth rotation application if enabled
        if (useSmoothRotation)
        {
            currentSwayRotation = Vector3.SmoothDamp(
                currentSwayRotation,
                targetSwayRotation,
                ref swayVelocity,
                1f / rotationSmoothSpeed
            );
        }
        else
        {
            currentSwayRotation = targetSwayRotation;
        }

        // Apply rotation to aim transform
        Vector3 finalRotation = originalAimRotation + currentSwayRotation;
        aimTransform.localEulerAngles = finalRotation;


        // Fire event
        OnSwayRotationChanged?.Invoke(currentSwayRotation);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handle ADS state changes
    /// </summary>
    private void OnADSStateChanged(bool isADS, float sensitivityMultiplier)
    {
        isADSActive = isADS;
        DebugLog("ADS State changed, affecting weaponsway");
    }

    /// <summary>
    /// Handle item equipped
    /// </summary>
    private void OnItemEquipped(EquippedItemData equippedItem)
    {
        UpdateCurrentItemMultiplier();
        DebugLog($"Item equipped: {equippedItem.GetItemData()?.itemName}, sway multiplier: {currentItemSwayMultiplier:F2}");
    }

    /// <summary>
    /// Handle item unequipped
    /// </summary>
    private void OnItemUnequipped()
    {
        UpdateCurrentItemMultiplier();
        DebugLog("Item unequipped, using default sway multiplier");
    }

    /// <summary>
    /// Handle look input changes for lag effect
    /// </summary>
    private void OnLookInputChanged(Vector2 lookInput)
    {
        // Calculate look input delta
        Vector3 currentInput = new Vector3(-lookInput.y, lookInput.x, 0f); // Invert Y for correct direction
        Vector3 inputDelta = currentInput - lastLookInput;
        lastLookInput = currentInput;

        // Apply look lag based on input delta
        if (inputDelta.magnitude > 0.01f)
        {
            Vector3 lagSway = inputDelta * lookLagIntensity * maxLookLagAngle;

            // Clamp to maximum angle
            lagSway.x = Mathf.Clamp(lagSway.x, -maxLookLagAngle, maxLookLagAngle);
            lagSway.y = Mathf.Clamp(lagSway.y, -maxLookLagAngle, maxLookLagAngle);

            lookLagSway += lagSway;
        }
    }

    #endregion

    #region State Management

    /// <summary>
    /// Update current state-based sway multiplier
    /// </summary>
    private void UpdateCurrentStateMultiplier()
    {
        if (playerController == null)
        {
            currentStateSwayMultiplier = standingSwayMultiplier;
            return;
        }

        if (playerController.IsCrouching)
        {
            currentStateSwayMultiplier = crouchingSwayMultiplier;
        }
        else if (playerController.IsSwimming)
        {
            currentStateSwayMultiplier = swimmingSwayMultiplier;
        }
        else
        {
            currentStateSwayMultiplier = standingSwayMultiplier;
        }
    }

    /// <summary>
    /// Update current item-based sway multiplier
    /// </summary>
    private void UpdateCurrentItemMultiplier()
    {
        currentItemSwayMultiplier = 1f; // Default multiplier

        if (equippedItemManager != null && equippedItemManager.HasEquippedItem)
        {
            ItemData currentItem = equippedItemManager.GetEquippedItemData();
            if (currentItem != null)
            {
                // Get weapon sway multiplier from item data
                // This will be added to ItemData in the next step
                currentItemSwayMultiplier = GetItemSwayMultiplier(currentItem);
            }
        }
    }

    /// <summary>
    /// Get sway multiplier from item data (placeholder until ItemData is updated)
    /// </summary>
    private float GetItemSwayMultiplier(ItemData itemData)
    {
        // TODO: Replace this with actual itemData.weaponSwayMultiplier when ItemData is updated
        // For now, provide reasonable defaults based on item type
        return itemData.itemType switch
        {
            ItemType.Unarmed => 0.8f,
            ItemType.Consumable => 0.9f,
            ItemType.RangedWeapon => 1.2f,
            ItemType.MeleeWeapon => 1.1f,
            ItemType.Tool => 1.0f,
            ItemType.KeyItem => 0.9f,
            ItemType.Bow => 1.3f,
            _ => 1f
        };
    }

    #endregion

    #region Public API

    /// <summary>
    /// Enable or disable weapon sway
    /// </summary>
    public void SetSwayEnabled(bool enabled)
    {
        this.enabled = enabled;

        if (!enabled)
        {
            // Reset to original rotation
            if (aimTransform != null)
            {
                aimTransform.localEulerAngles = originalAimRotation;
            }
            currentSwayRotation = Vector3.zero;
            targetSwayRotation = Vector3.zero;
        }

        DebugLog($"Weapon sway {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Set base sway intensity
    /// </summary>
    public void SetBaseSwayIntensity(float intensity)
    {
        baseSwayIntensity = Mathf.Clamp(intensity, 0f, 2f);
    }

    /// <summary>
    /// Set breathing rate
    /// </summary>
    public void SetBreathingRate(float rate)
    {
        breathingRate = Mathf.Clamp(rate, 5f, 30f);
    }

    /// <summary>
    /// Set ADS sway reduction
    /// </summary>
    public void SetADSSwayReduction(float reduction)
    {
        adsSwayReduction = Mathf.Clamp01(reduction);
    }

    /// <summary>
    /// Force update state multipliers (call when player state changes)
    /// </summary>
    public void RefreshStateMultipliers()
    {
        UpdateCurrentStateMultiplier();
        UpdateCurrentItemMultiplier();
    }

    /// <summary>
    /// Get current sway debug information
    /// </summary>
    public string GetSwayDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== WEAPON SWAY DEBUG ===");
        info.AppendLine($"Initialized: {isInitialized}");
        info.AppendLine($"Aim Transform: {(aimTransform != null ? aimTransform.name : "None")}");
        info.AppendLine($"ADS Active: {isADSActive}");
        info.AppendLine($"Base Intensity: {baseSwayIntensity:F2}");
        info.AppendLine($"Item Multiplier: {currentItemSwayMultiplier:F2}");
        info.AppendLine($"State Multiplier: {currentStateSwayMultiplier:F2}");
        info.AppendLine($"Current Sway: {currentSwayRotation:F2}");
        info.AppendLine($"Target Sway: {targetSwayRotation:F2}");
        info.AppendLine($"Breathing Sway: {breathingSway:F2}");
        info.AppendLine($"Movement Sway: {movementSway:F2}");
        info.AppendLine($"Look Lag Sway: {lookLagSway:F2}");

        return info.ToString();
    }

    #endregion

    #region Debug Buttons

    [FoldoutGroup("Debug")]
    [Button("Log Sway Debug Info")]
    public void LogSwayDebugInfo()
    {
        if (Application.isPlaying)
        {
            Debug.Log(GetSwayDebugInfo());
        }
    }

    [FoldoutGroup("Debug")]
    [Button("Test Breathing Sway")]
    public void TestBreathingSway()
    {
        if (Application.isPlaying)
        {
            breathingSwayIntensity = 2f;
            breathingRate = 20f;
            DebugLog("Testing increased breathing sway for 5 seconds");
            Invoke(nameof(ResetBreathingSway), 5f);
        }
    }

    private void ResetBreathingSway()
    {
        breathingSwayIntensity = 0.3f;
        breathingRate = 12f;
        DebugLog("Breathing sway reset to normal");
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!showSwayGizmos || aimTransform == null) return;

        // Draw aim transform position
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(aimTransform.position, 0.02f);

        // Draw sway direction
        if (currentSwayRotation.magnitude > 0.01f)
        {
            Gizmos.color = Color.red;
            Vector3 swayDirection = aimTransform.rotation * Vector3.forward;
            Gizmos.DrawRay(aimTransform.position, swayDirection * 0.1f);
        }

        // Draw original aim direction
        Gizmos.color = Color.green;
        Quaternion originalRotation = Quaternion.Euler(originalAimRotation);
        Vector3 originalDirection = originalRotation * Vector3.forward;
        Gizmos.DrawRay(aimTransform.position, originalDirection * 0.08f);
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[WeaponSwayController] {message}");
        }
    }

    #region Cleanup

    /// <summary>
    /// Cleanup the weapon sway controller
    /// </summary>
    public void Cleanup()
    {
        // Unsubscribe from events
        if (adsController != null)
        {
            adsController.OnADSStateChanged -= OnADSStateChanged;
        }

        if (equippedItemManager != null)
        {
            equippedItemManager.OnItemEquipped -= OnItemEquipped;
            equippedItemManager.OnItemUnequipped -= OnItemUnequipped;
        }

        var cameraController = GetComponent<CameraController>();
        if (cameraController != null)
        {
            cameraController.OnLookInputChanged -= OnLookInputChanged;
        }

        // Reset aim transform
        if (aimTransform != null)
        {
            aimTransform.localEulerAngles = originalAimRotation;
        }

        DebugLog("WeaponSwayController cleaned up");
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    #endregion
}