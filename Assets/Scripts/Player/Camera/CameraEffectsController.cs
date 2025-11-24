using UnityEngine;
using Unity.Cinemachine;
using Sirenix.OdinInspector;
using System;

/// <summary>
/// Handles camera effects like screen shake, breathing, sway, and other visual effects.
/// </summary>
public class CameraEffectsController : MonoBehaviour
{
    [FoldoutGroup("Screen Shake")]
    [Tooltip("Enable screen shake effects")]
    [SerializeField] private bool enableScreenShake = true;

    [FoldoutGroup("Screen Shake")]
    [Tooltip("Default shake intensity")]
    [SerializeField] private float defaultShakeIntensity = 1f;

    [FoldoutGroup("Screen Shake")]
    [Tooltip("Default shake duration")]
    [SerializeField] private float defaultShakeDuration = 0.3f;

    [FoldoutGroup("Breathing Effect")]
    [Tooltip("Enable subtle breathing effect")]
    [SerializeField] private bool enableBreathingEffect = false;

    [FoldoutGroup("Breathing Effect")]
    [Tooltip("Breathing effect intensity")]
    [SerializeField] private float breathingIntensity = 0.02f;

    [FoldoutGroup("Breathing Effect")]
    [Tooltip("Breathing rate (breaths per minute)")]
    [SerializeField] private float breathingRate = 15f;

    [FoldoutGroup("Camera Sway")]
    [Tooltip("Enable camera sway when moving")]
    [SerializeField] private bool enableCameraSway = false;

    [FoldoutGroup("Camera Sway")]
    [Tooltip("Sway intensity when walking")]
    [SerializeField] private float walkSwayIntensity = 0.01f;

    [FoldoutGroup("Camera Sway")]
    [Tooltip("Sway intensity when running")]
    [SerializeField] private float runSwayIntensity = 0.02f;

    [FoldoutGroup("Impact Effects")]
    [Tooltip("Enable landing impact effects")]
    [SerializeField] private bool enableLandingEffects = true;

    [FoldoutGroup("Impact Effects")]
    [Tooltip("Landing shake intensity")]
    [SerializeField] private float landingShakeIntensity = 0.5f;

    [FoldoutGroup("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Component references
    private CameraController cameraController;
    private PlayerController playerController;
    private CinemachineBasicMultiChannelPerlin noiseComponent;

    // Effect state
    private bool isShaking = false;
    private float breathingTime = 0f;
    private Vector3 originalCameraPosition;
    private MovementState currentMovementState = MovementState.Idle;

    // Shake coroutine tracking
    private Coroutine currentShakeCoroutine;

    #region Properties

    public bool IsShaking => isShaking;
    public bool BreathingEffectEnabled => enableBreathingEffect;
    public bool CameraSwayEnabled => enableCameraSway;

    #endregion

    #region Initialization

    public void Initialize(CameraController controller, PlayerController player)
    {
        cameraController = controller;
        playerController = player;

        SetupNoiseComponent();
        SetupEventSubscriptions();

        DebugLog("CameraEffectsController initialized");
    }

    /// <summary>Setup the noise component for screen shake</summary>
    private void SetupNoiseComponent()
    {
        var virtualCamera = cameraController?.VirtualCamera;
        if (virtualCamera != null)
        {
            noiseComponent = virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();

            if (noiseComponent != null)
            {
                noiseComponent.AmplitudeGain = 0f;
                noiseComponent.FrequencyGain = 1f;
                DebugLog("Noise component setup complete");
            }
            else
            {
                Debug.LogWarning("[CameraEffectsController] No CinemachineBasicMultiChannelPerlin component found. Screen shake will not work.");
            }
        }
    }

    /// <summary>Setup event subscriptions</summary>
    private void SetupEventSubscriptions()
    {
        // Subscribe to player events for effect triggers
        // Add specific event subscriptions here as needed
    }

    #endregion

    #region Update Methods

    public void UpdateEffects()
    {
        UpdateBreathingEffect();
        UpdateCameraSway();
    }

    /// <summary>Update breathing effect</summary>
    private void UpdateBreathingEffect()
    {
        if (!enableBreathingEffect) return;

        breathingTime += Time.deltaTime;

        // Calculate breathing wave
        float breathingWave = Mathf.Sin(breathingTime * breathingRate / 60f * 2f * Mathf.PI);
        float breathingOffset = breathingWave * breathingIntensity;

        // Apply breathing effect to camera
        ApplyBreathingOffset(breathingOffset);
    }

    /// <summary>Update camera sway based on movement</summary>
    private void UpdateCameraSway()
    {
        if (!enableCameraSway || playerController == null) return;

        bool isMoving = playerController.IsMoving;
        if (!isMoving) return;

        float swayIntensity = currentMovementState == MovementState.Running ? runSwayIntensity : walkSwayIntensity;

        // Calculate sway based on movement
        float swayX = Mathf.Sin(Time.time * 2f) * swayIntensity;
        float swayY = Mathf.Sin(Time.time * 4f) * swayIntensity * 0.5f;

        ApplySwayOffset(new Vector3(swayX, swayY, 0f));
    }

    #endregion

    #region Screen Shake

    /// <summary>Trigger screen shake with default settings</summary>
    public void TriggerScreenShake()
    {
        TriggerScreenShake(defaultShakeIntensity, defaultShakeDuration);
    }

    /// <summary>Trigger screen shake with custom settings</summary>
    public void TriggerScreenShake(float intensity, float duration)
    {
        if (!enableScreenShake || noiseComponent == null) return;

        // Stop any existing shake
        StopScreenShake();

        // Start new shake
        currentShakeCoroutine = StartCoroutine(ShakeCoroutine(intensity, duration));

        DebugLog($"Screen shake triggered - Intensity: {intensity}, Duration: {duration}");
    }

    /// <summary>Stop current screen shake</summary>
    public void StopScreenShake()
    {
        if (currentShakeCoroutine != null)
        {
            StopCoroutine(currentShakeCoroutine);
            currentShakeCoroutine = null;
        }

        if (noiseComponent != null)
        {
            noiseComponent.AmplitudeGain = 0f;
        }

        isShaking = false;
        DebugLog("Screen shake stopped");
    }

    /// <summary>Screen shake coroutine</summary>
    private System.Collections.IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        isShaking = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float currentIntensity = Mathf.Lerp(intensity, 0f, elapsed / duration);

            if (noiseComponent != null)
            {
                noiseComponent.AmplitudeGain = currentIntensity;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure shake is fully stopped
        if (noiseComponent != null)
        {
            noiseComponent.AmplitudeGain = 0f;
        }

        isShaking = false;
        currentShakeCoroutine = null;

        DebugLog("Screen shake completed");
    }

    #endregion

    #region Camera Offset Effects

    /// <summary>Apply breathing offset to camera</summary>
    private void ApplyBreathingOffset(float offset)
    {
        var cameraRoot = cameraController?.CameraRoot;
        if (cameraRoot == null) return;

        // Apply subtle vertical breathing movement
        Vector3 breathingOffset = Vector3.up * offset;

        // Store original position if not already stored
        if (originalCameraPosition == Vector3.zero)
        {
            originalCameraPosition = cameraRoot.localPosition;
        }

        // Apply breathing offset (additive to preserve other effects)
        // Note: This is a simple implementation - more sophisticated systems might use a layered approach
    }

    /// <summary>Apply sway offset to camera</summary>
    private void ApplySwayOffset(Vector3 swayOffset)
    {
        var cameraRoot = cameraController?.CameraRoot;
        if (cameraRoot == null) return;

        // Apply sway effect
        // Note: This is a simple implementation - in a real system you'd want to layer effects properly
    }

    #endregion

    #region Movement State Handling

    /// <summary>Handle movement state changes</summary>
    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        currentMovementState = newState;

        // Trigger effects based on state changes
        switch (newState)
        {
            case MovementState.Landing:
                if (enableLandingEffects)
                {
                    TriggerLandingEffect();
                }
                break;

            case MovementState.Running:
                // Increase breathing rate when running
                if (enableBreathingEffect)
                {
                    breathingRate = 20f; // Faster breathing
                }
                break;

            case MovementState.Idle:
            case MovementState.Walking:
                // Reset breathing rate
                if (enableBreathingEffect)
                {
                    breathingRate = 15f; // Normal breathing
                }
                break;
        }

        DebugLog($"Movement state changed to: {newState}");
    }

    /// <summary>Trigger landing effect</summary>
    private void TriggerLandingEffect()
    {
        TriggerScreenShake(landingShakeIntensity, 0.2f);
        DebugLog("Landing effect triggered");
    }

    #endregion

    #region Public API

    /// <summary>Enable or disable screen shake</summary>
    public void SetScreenShakeEnabled(bool enabled)
    {
        enableScreenShake = enabled;
        if (!enabled)
        {
            StopScreenShake();
        }
    }

    /// <summary>Enable or disable breathing effect</summary>
    public void SetBreathingEffectEnabled(bool enabled)
    {
        enableBreathingEffect = enabled;
        if (!enabled)
        {
            // Reset camera position
            var cameraRoot = cameraController?.CameraRoot;
            if (cameraRoot != null && originalCameraPosition != Vector3.zero)
            {
                cameraRoot.localPosition = originalCameraPosition;
            }
        }
    }

    /// <summary>Enable or disable camera sway</summary>
    public void SetCameraSwayEnabled(bool enabled)
    {
        enableCameraSway = enabled;
    }

    /// <summary>Set breathing rate</summary>
    public void SetBreathingRate(float rate)
    {
        breathingRate = Mathf.Max(1f, rate);
    }

    /// <summary>Set breathing intensity</summary>
    public void SetBreathingIntensity(float intensity)
    {
        breathingIntensity = Mathf.Max(0f, intensity);
    }

    /// <summary>Trigger weapon fire effect</summary>
    public void TriggerWeaponFireEffect(float intensity = 0.3f)
    {
        TriggerScreenShake(intensity, 0.1f);
    }

    /// <summary>Trigger explosion effect</summary>
    public void TriggerExplosionEffect(float intensity = 2f, float duration = 0.5f)
    {
        TriggerScreenShake(intensity, duration);
    }

    /// <summary>Trigger damage effect</summary>
    public void TriggerDamageEffect(float intensity = 0.8f)
    {
        TriggerScreenShake(intensity, 0.3f);
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CameraEffectsController] {message}");
        }
    }

    #endregion

    #region Cleanup

    public void Cleanup()
    {
        // Stop any active effects
        StopScreenShake();

        // Reset camera position
        var cameraRoot = cameraController?.CameraRoot;
        if (cameraRoot != null && originalCameraPosition != Vector3.zero)
        {
            cameraRoot.localPosition = originalCameraPosition;
        }

        DebugLog("CameraEffectsController cleaned up");
    }

    #endregion
}