using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;
using Sirenix.OdinInspector;
using System;

/// <summary>
/// Handles field of view changes and effects for different movement states and ADS.
/// </summary>
public class FOVController : MonoBehaviour
{
    [FoldoutGroup("FOV Settings")]
    [Tooltip("Default field of view")]
    [SerializeField] private float normalFOV = 85f;

    [FoldoutGroup("FOV Settings")]
    [Tooltip("FOV when aiming down sights")]
    [SerializeField] private float adsFOV = 45f;

    [FoldoutGroup("FOV Settings")]
    [Tooltip("FOV increase when sprinting")]
    [SerializeField] private float sprintingFOVIncrease = 5f;

    [FoldoutGroup("FOV Settings")]
    [Tooltip("FOV decrease when crouching")]
    [SerializeField] private float crouchingFOVDecrease = 5f;

    [FoldoutGroup("FOV Settings")]
    [Tooltip("Duration for FOV transitions")]
    [SerializeField] private float fovTransitionDuration = 0.3f;

    [FoldoutGroup("Camera Effects")]
    [Tooltip("Enable FOV changes for different movement states")]
    [SerializeField] private bool enableFOVEffects = true;

    [FoldoutGroup("Camera Effects")]
    [Tooltip("Enable camera effects when crouching")]
    [SerializeField] private bool enableCrouchEffects = true;

    [FoldoutGroup("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Component references
    private CameraController cameraController;
    private PlayerController playerController;
    private CinemachineCamera cinemachineCamera;
    private Camera firstPersonRenderCamera;
    private ADSController adsController;

    // FOV state
    private float currentBaseFOV;
    private bool isInADS = false;

    // Animation tweener
    private Tweener fovTweener;

    #region Properties

    public float CurrentFOV => GetCurrentFOV();
    public float NormalFOV => normalFOV;
    public float ADSFOV => adsFOV;

    #endregion

    #region Initialization

    public void Initialize(CameraController controller, PlayerController player)
    {
        cameraController = controller;
        playerController = player;

        SetupCinemachine();
        SetupADSIntegration();

        currentBaseFOV = normalFOV;
        SetFOV(normalFOV);

        DebugLog("FOVController initialized");
    }

    /// <summary>Setup Cinemachine camera reference</summary>
    private void SetupCinemachine()
    {
        var virtualCamera = cameraController?.VirtualCamera;
        if (virtualCamera != null)
        {
            cinemachineCamera = virtualCamera as CinemachineCamera;
            DebugLog("Cinemachine camera reference established");
        }
        else
        {
            Debug.LogError("[FOVController] Virtual camera not found!");
        }

        Camera firstPersonRenderCamera = cameraController?.FirstPersonRenderCamera;
        if (firstPersonRenderCamera != null)
        {
            this.firstPersonRenderCamera = firstPersonRenderCamera;
            DebugLog("First person render camera reference established");
        }
        else
        {
            Debug.LogError("[FOVController] First person render camera not found!");
        }
    }

    /// <summary>Setup ADS integration</summary>
    private void SetupADSIntegration()
    {
        adsController = GetComponent<ADSController>();
        if (adsController != null)
        {
            adsController.OnADSStateChanged += OnADSStateChanged;
            DebugLog("ADS integration established");
        }
    }

    #endregion

    #region Update Methods

    public void UpdateEffects()
    {
        // Handle any per-frame FOV effects if needed
    }

    #endregion

    #region FOV Management

    /// <summary>Get current field of view from active camera</summary>
    private float GetCurrentFOV()
    {
        if (cinemachineCamera != null)
        {
            return cinemachineCamera.Lens.FieldOfView;
        }

        var brain = Camera.main?.GetComponent<CinemachineBrain>();
        if (brain != null && brain.OutputCamera != null)
        {
            return brain.OutputCamera.fieldOfView;
        }

        if (Camera.main != null)
        {
            return Camera.main.fieldOfView;
        }

        return normalFOV;
    }

    /// <summary>Set field of view on active camera</summary>
    public void SetFOV(float fov)
    {
        if (cinemachineCamera != null)
        {
            var lens = cinemachineCamera.Lens;
            lens.FieldOfView = fov;
            cinemachineCamera.Lens = lens;

            DebugLog($"FOV set to: {fov}");
        }

        if (firstPersonRenderCamera != null)
        {
            firstPersonRenderCamera.fieldOfView = fov;
            DebugLog($"First person render camera FOV set to: {fov}");
        }
    }

    /// <summary>Animate FOV change with smooth transition</summary>
    private void AnimateFOV(float targetFOV)
    {
        if (!enableFOVEffects) return;

        fovTweener?.Kill();

        fovTweener = DOTween.To(
            () => GetCurrentFOV(),
            x => SetFOV(x),
            targetFOV,
            fovTransitionDuration
        ).SetEase(Ease.OutQuart);

        DebugLog($"Animating FOV to: {targetFOV}");
    }

    #endregion

    #region Movement State Handling

    /// <summary>Handle movement state changes</summary>
    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        if (!enableFOVEffects) return;

        switch (newState)
        {
            case MovementState.Running:
                SetRunningFOV();
                break;

            case MovementState.Crouching:
                if (enableCrouchEffects)
                {
                    SetCrouchingFOV();
                }
                break;

            case MovementState.Walking:
            case MovementState.Idle:
                if (previousState == MovementState.Running || previousState == MovementState.Crouching)
                {
                    SetNormalFOV();
                }
                break;
        }
    }

    /// <summary>Set FOV for sprinting state</summary>
    public void SetRunningFOV()
    {
        currentBaseFOV = normalFOV + sprintingFOVIncrease;
        if (!isInADS)
        {
            AnimateFOV(currentBaseFOV);
        }
        DebugLog("Running FOV applied");
    }

    /// <summary>Set FOV for crouching state</summary>
    public void SetCrouchingFOV()
    {
        currentBaseFOV = normalFOV - crouchingFOVDecrease;
        if (!isInADS)
        {
            AnimateFOV(currentBaseFOV);
        }
        DebugLog("Crouching FOV applied");
    }

    /// <summary>Set normal FOV for idle/walking states</summary>
    public void SetNormalFOV()
    {
        currentBaseFOV = normalFOV;
        if (!isInADS)
        {
            AnimateFOV(currentBaseFOV);
        }
        DebugLog("Normal FOV applied");
    }

    #endregion

    #region ADS Integration

    /// <summary>Handle ADS state changes</summary>
    private void OnADSStateChanged(bool isADS, float sensitivityMultiplier)
    {
        isInADS = isADS;

        if (isADS)
        {
            // Switch to ADS FOV
            AnimateFOV(adsFOV);
            DebugLog("Switched to ADS FOV");
        }
        else
        {
            // Switch back to current base FOV
            AnimateFOV(currentBaseFOV);
            DebugLog("Switched back from ADS FOV");
        }
    }

    #endregion

    #region Public API

    /// <summary>Set normal FOV value</summary>
    public void SetNormalFOVValue(float fov)
    {
        normalFOV = fov;
        if (!isInADS)
        {
            SetNormalFOV();
        }
    }

    /// <summary>Set ADS FOV value</summary>
    public void SetADSFOVValue(float fov)
    {
        adsFOV = fov;
        if (isInADS)
        {
            AnimateFOV(adsFOV);
        }
    }

    /// <summary>Force immediate FOV change without animation</summary>
    public void SetFOVImmediate(float fov)
    {
        fovTweener?.Kill();
        SetFOV(fov);
    }

    /// <summary>Enable or disable FOV effects</summary>
    public void SetFOVEffectsEnabled(bool enabled)
    {
        enableFOVEffects = enabled;
        if (!enabled && !isInADS)
        {
            SetFOV(normalFOV);
        }
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[FOVController] {message}");
        }
    }

    #endregion

    #region Cleanup

    public void Cleanup()
    {
        // Unsubscribe from ADS events
        if (adsController != null)
        {
            adsController.OnADSStateChanged -= OnADSStateChanged;
        }

        // Kill any active tweens
        fovTweener?.Kill();

        DebugLog("FOVController cleaned up");
    }

    #endregion

}