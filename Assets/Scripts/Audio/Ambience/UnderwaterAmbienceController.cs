using UnityEngine;
using DG.Tweening;
using UnityEngine.Audio;
using Sirenix.OdinInspector;

/// <summary>
/// Controls underwater ambience audio based on player's water depth and position.
/// Plays different ambient loops for surface vs deep underwater environments.
/// Attach this to a dedicated GameObject in your scene.
/// </summary>
public class UnderwaterAmbienceController : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource surfaceAmbienceSource;
    [SerializeField] private AudioSource deepAmbienceSource;

    [Header("Ambience Audio Clips")]
    [SerializeField] private AudioClip surfaceAmbienceClip;
    [SerializeField] private AudioClip deepAmbienceClip;

    [Header("Depth Thresholds")]
    [SerializeField] private float surfaceDepthThreshold = 1f; // Depth at which we start transitioning to deep
    [SerializeField] private float deepDepthThreshold = 3f;    // Depth at which we're fully in deep ambience
    [SerializeField] private float headSubmersionThreshold = 0.5f; // Head depth to start ambience

    [Header("Volume Settings")]
    [SerializeField] private float maxSurfaceVolume = 0.6f;
    [SerializeField] private float maxDeepVolume = 0.8f;

    [Header("Underwater Audio Effects")]
    [SerializeField] private bool enableUnderwaterFiltering = true;
    [SerializeField] private AudioLowPassFilter globalLowPassFilter; // Will be created automatically if null
    [SerializeField] private AudioReverbFilter globalReverbFilter; // Will be created automatically if null

    [Header("Underwater Filter Settings")]
    [Range(500f, 22000f)][SerializeField] private float underwaterLowPassCutoff = 1500f;
    [Range(500f, 22000f)][SerializeField] private float surfaceLowPassCutoff = 22000f;
    [Range(0f, 1f)][SerializeField] private float underwaterVolume = 0.7f;
    [Range(0f, 1f)][SerializeField] private float surfaceVolume = 1f;
    [Range(0.1f, 3f)][SerializeField] private float filterTransitionSpeed = 1.5f;

    [Header("Underwater Reverb Settings")]
    [SerializeField] private bool enableUnderwaterReverb = true;
    [SerializeField] private AudioReverbPreset underwaterReverbPreset = AudioReverbPreset.Underwater;
    [Range(0f, 1f)][SerializeField] private float underwaterReverbLevel = 0.3f;

    [Header("Transition Settings")]
    [SerializeField] private float crossfadeDuration = 2f;

    [Header("Audio Filtering")]
    [SerializeField] private Camera playerCamera; // For adding audio listener effects
    [SerializeField] private AudioListener audioListener; // Main audio listener

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    // Component references
    private PlayerWaterDetector waterDetector;
    private PlayerController playerController;

    // Audio filter references
    private AudioLowPassFilter activeLowPassFilter;
    private AudioReverbFilter activeReverbFilter;
    private Tweener filterTransitionTween;
    private Tweener volumeTransitionTween;

    // State tracking
    private bool isPlayerInWater = false;
    private bool isHeadSubmerged = false;
    private float currentWaterDepth = 0f;
    private AmbienceState currentState = AmbienceState.Silent;
    private AmbienceState targetState = AmbienceState.Silent;

    // Tween references for smooth transitions
    private Tweener surfaceVolumeTween;
    private Tweener deepVolumeTween;

    private enum AmbienceState
    {
        Silent,         // No ambience (not in water or head above water)
        Surface,        // Surface ambience only
        Transitioning,  // Crossfading between surface and deep
        Deep           // Deep ambience only
    }

    private void Start()
    {
        FindPlayerReferences();
        SetupAudioSources();
        SetupUnderwaterFiltering();
        SubscribeToEvents();
        InitializeAmbience();
    }

    private void FindPlayerReferences()
    {
        // Find player components
        waterDetector = FindFirstObjectByType<PlayerWaterDetector>();
        playerController = FindFirstObjectByType<PlayerController>();

        // if (waterDetector == null)
        // {
        //     Debug.LogError("[UnderwaterAmbience] PlayerWaterDetector not found! Ambience will not function.");
        // }

        if (playerController == null)
        {
            Debug.LogWarning("[UnderwaterAmbience] PlayerController not found. Some features may be limited.");
        }

        // Find audio components for filtering
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (audioListener == null)
        {
            audioListener = FindFirstObjectByType<AudioListener>();
        }

        DebugLog($"References found - WaterDetector: {waterDetector != null}, PlayerController: {playerController != null}, Camera: {playerCamera != null}, AudioListener: {audioListener != null}");
    }

    private void SetupAudioSources()
    {
        // Create audio sources if they don't exist
        if (surfaceAmbienceSource == null)
        {
            GameObject surfaceObj = new GameObject("SurfaceAmbienceSource");
            surfaceObj.transform.SetParent(transform);
            surfaceAmbienceSource = surfaceObj.AddComponent<AudioSource>();
        }

        if (deepAmbienceSource == null)
        {
            GameObject deepObj = new GameObject("DeepAmbienceSource");
            deepObj.transform.SetParent(transform);
            deepAmbienceSource = deepObj.AddComponent<AudioSource>();
        }

        // Configure audio sources for looping ambience
        ConfigureAudioSource(surfaceAmbienceSource, surfaceAmbienceClip);
        ConfigureAudioSource(deepAmbienceSource, deepAmbienceClip);

        DebugLog("Audio sources configured for underwater ambience");
    }

    /// <summary>
    /// NEW: Sets up underwater audio filtering system
    /// </summary>
    private void SetupUnderwaterFiltering()
    {
        if (!enableUnderwaterFiltering) return;

        // Find the best GameObject to attach audio filters to
        GameObject filterTarget = GetAudioFilterTarget();

        if (filterTarget == null)
        {
            Debug.LogError("[UnderwaterAmbience] Could not find suitable target for audio filters!");
            return;
        }

        // Setup Low Pass Filter
        if (globalLowPassFilter == null)
        {
            activeLowPassFilter = filterTarget.GetComponent<AudioLowPassFilter>();
            if (activeLowPassFilter == null)
            {
                activeLowPassFilter = filterTarget.AddComponent<AudioLowPassFilter>();
            }
        }
        else
        {
            activeLowPassFilter = globalLowPassFilter;
        }

        // Setup Reverb Filter
        if (enableUnderwaterReverb)
        {
            if (globalReverbFilter == null)
            {
                activeReverbFilter = filterTarget.GetComponent<AudioReverbFilter>();
                if (activeReverbFilter == null)
                {
                    activeReverbFilter = filterTarget.AddComponent<AudioReverbFilter>();
                }
            }
            else
            {
                activeReverbFilter = globalReverbFilter;
            }
        }

        // Initialize filters to surface settings
        InitializeAudioFilters();

        DebugLog($"Underwater filtering setup complete on {filterTarget.name}");
    }

    /// <summary>
    /// Finds the best GameObject to attach audio filters to
    /// </summary>
    private GameObject GetAudioFilterTarget()
    {
        // Priority order: AudioListener > Main Camera > Create new GameObject
        if (audioListener != null)
        {
            return audioListener.gameObject;
        }

        if (playerCamera != null)
        {
            return playerCamera.gameObject;
        }

        if (Camera.main != null)
        {
            return Camera.main.gameObject;
        }

        // Create a dedicated audio filter GameObject
        GameObject filterObj = new GameObject("UnderwaterAudioFilters");
        filterObj.transform.SetParent(transform);
        return filterObj;
    }

    /// <summary>
    /// Initializes audio filters to surface (normal) settings
    /// </summary>
    private void InitializeAudioFilters()
    {
        if (activeLowPassFilter != null)
        {
            activeLowPassFilter.cutoffFrequency = surfaceLowPassCutoff;
            activeLowPassFilter.lowpassResonanceQ = 1f;
        }

        if (activeReverbFilter != null)
        {
            activeReverbFilter.reverbPreset = AudioReverbPreset.Off;
            activeReverbFilter.dryLevel = 0f;
        }

        // Set initial volume
        AudioListener.volume = surfaceVolume;
    }

    private void ConfigureAudioSource(AudioSource source, AudioClip clip)
    {
        if (source == null) return;

        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        source.spatialBlend = 0f; // 2D audio for ambience
        source.priority = 128; // Lower priority than effects
    }

    private void SubscribeToEvents()
    {
        if (waterDetector != null)
        {
            waterDetector.OnWaterStateEntered += HandleWaterEntered;
            waterDetector.OnWaterStateExited += HandleWaterExited;
            waterDetector.OnHeadSubmerged += HandleHeadSubmerged;
            waterDetector.OnHeadSurfaced += HandleHeadSurfaced;
        }
    }

    private void InitializeAmbience()
    {
        // Start with silent state
        currentState = AmbienceState.Silent;
        targetState = AmbienceState.Silent;

        // Ensure audio sources are stopped initially
        if (surfaceAmbienceSource != null) surfaceAmbienceSource.Stop();
        if (deepAmbienceSource != null) deepAmbienceSource.Stop();
    }

    private void Update()
    {
        if (waterDetector == null) return;

        UpdateWaterState();
        UpdateAmbienceState();
        UpdateAudioLevels();
    }

    private void UpdateWaterState()
    {
        isPlayerInWater = waterDetector.IsInWaterState;
        isHeadSubmerged = waterDetector.IsHeadUnderwater;
        currentWaterDepth = waterDetector.HeadDepth; // Use head depth for ambience
    }

    private void UpdateAmbienceState()
    {
        AmbienceState newTargetState = DetermineTargetState();

        if (newTargetState != targetState)
        {
            DebugLog($"Ambience state changing: {targetState} -> {newTargetState} (Depth: {currentWaterDepth:F2}m)");
            targetState = newTargetState;
            TransitionToState(targetState);
        }
    }

    private AmbienceState DetermineTargetState()
    {
        // No ambience if not in water or head above water
        if (!isPlayerInWater || !isHeadSubmerged || currentWaterDepth < headSubmersionThreshold)
        {
            return AmbienceState.Silent;
        }

        // Determine state based on depth
        if (currentWaterDepth < surfaceDepthThreshold)
        {
            return AmbienceState.Surface;
        }
        else if (currentWaterDepth > deepDepthThreshold)
        {
            return AmbienceState.Deep;
        }
        else
        {
            // In transition zone between surface and deep
            return AmbienceState.Transitioning;
        }
    }

    private void TransitionToState(AmbienceState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case AmbienceState.Silent:
                TransitionToSilent();
                break;
            case AmbienceState.Surface:
                TransitionToSurface();
                break;
            case AmbienceState.Transitioning:
                TransitionToCrossfade();
                break;
            case AmbienceState.Deep:
                TransitionToDeep();
                break;
        }

        // NEW: Apply underwater filtering based on state
        ApplyUnderwaterFiltering(newState);
    }

    /// <summary>
    /// NEW: Applies underwater audio filtering based on ambience state
    /// </summary>
    private void ApplyUnderwaterFiltering(AmbienceState state)
    {
        if (!enableUnderwaterFiltering) return;

        bool shouldBeUnderwater = (state == AmbienceState.Surface || state == AmbienceState.Transitioning || state == AmbienceState.Deep);

        if (shouldBeUnderwater)
        {
            ApplyUnderwaterEffect();
        }
        else
        {
            ApplySurfaceEffect();
        }
    }

    /// <summary>
    /// NEW: Applies underwater filtering effects to all audio
    /// </summary>
    private void ApplyUnderwaterEffect()
    {
        DebugLog("Applying underwater audio filtering");

        // Transition low pass filter
        if (activeLowPassFilter != null)
        {
            TransitionLowPassFilter(underwaterLowPassCutoff);
        }

        // Apply reverb
        if (activeReverbFilter != null && enableUnderwaterReverb)
        {
            TransitionReverbFilter(true);
        }

        // Reduce overall volume
        TransitionMasterVolume(underwaterVolume);
    }

    /// <summary>
    /// NEW: Removes underwater filtering effects
    /// </summary>
    private void ApplySurfaceEffect()
    {
        DebugLog("Removing underwater audio filtering");

        // Restore low pass filter
        if (activeLowPassFilter != null)
        {
            TransitionLowPassFilter(surfaceLowPassCutoff);
        }

        // Remove reverb
        if (activeReverbFilter != null)
        {
            TransitionReverbFilter(false);
        }

        // Restore normal volume
        TransitionMasterVolume(surfaceVolume);
    }

    /// <summary>
    /// NEW: Smoothly transitions the low pass filter
    /// </summary>
    private void TransitionLowPassFilter(float targetCutoff)
    {
        if (activeLowPassFilter == null) return;

        // Kill existing tween
        filterTransitionTween?.Kill();

        // Animate the cutoff frequency
        filterTransitionTween = DOTween.To(
            () => activeLowPassFilter.cutoffFrequency,
            x => activeLowPassFilter.cutoffFrequency = x,
            targetCutoff,
            crossfadeDuration / filterTransitionSpeed
        ).SetEase(Ease.OutQuart);
    }

    /// <summary>
    /// NEW: Transitions reverb filter on/off
    /// </summary>
    private void TransitionReverbFilter(bool enable)
    {
        if (activeReverbFilter == null) return;

        if (enable)
        {
            activeReverbFilter.reverbPreset = underwaterReverbPreset;
            activeReverbFilter.dryLevel = -1000f + (underwaterReverbLevel * 1000f);
        }
        else
        {
            activeReverbFilter.reverbPreset = AudioReverbPreset.Off;
            activeReverbFilter.dryLevel = 0f;
        }
    }

    /// <summary>
    /// NEW: Smoothly transitions master volume
    /// </summary>
    private void TransitionMasterVolume(float targetVolume)
    {
        // Kill existing tween
        volumeTransitionTween?.Kill();

        // Animate the master volume
        volumeTransitionTween = DOTween.To(
            () => AudioListener.volume,
            x => AudioListener.volume = x,
            targetVolume,
            crossfadeDuration / filterTransitionSpeed
        ).SetEase(Ease.OutQuart);
    }

    private void TransitionToSilent()
    {
        // Fade out all ambience
        FadeAudioSource(surfaceAmbienceSource, 0f, crossfadeDuration, true);
        FadeAudioSource(deepAmbienceSource, 0f, crossfadeDuration, true);
    }

    private void TransitionToSurface()
    {
        // Start surface ambience, stop deep
        StartAudioSourceIfNeeded(surfaceAmbienceSource);
        FadeAudioSource(surfaceAmbienceSource, maxSurfaceVolume, crossfadeDuration);
        FadeAudioSource(deepAmbienceSource, 0f, crossfadeDuration, true);
    }

    private void TransitionToDeep()
    {
        // Start deep ambience, stop surface
        StartAudioSourceIfNeeded(deepAmbienceSource);
        FadeAudioSource(deepAmbienceSource, maxDeepVolume, crossfadeDuration);
        FadeAudioSource(surfaceAmbienceSource, 0f, crossfadeDuration, true);
    }

    private void TransitionToCrossfade()
    {
        // Both ambiences active, volumes based on depth ratio
        StartAudioSourceIfNeeded(surfaceAmbienceSource);
        StartAudioSourceIfNeeded(deepAmbienceSource);

        // Calculate crossfade ratio
        float depthRange = deepDepthThreshold - surfaceDepthThreshold;
        float depthProgress = (currentWaterDepth - surfaceDepthThreshold) / depthRange;
        depthProgress = Mathf.Clamp01(depthProgress);

        // Crossfade between surface and deep
        float surfaceVolume = maxSurfaceVolume * (1f - depthProgress);
        float deepVolume = maxDeepVolume * depthProgress;

        FadeAudioSource(surfaceAmbienceSource, surfaceVolume, crossfadeDuration * 0.5f);
        FadeAudioSource(deepAmbienceSource, deepVolume, crossfadeDuration * 0.5f);
    }

    private void UpdateAudioLevels()
    {
        // Dynamic volume updates for transitioning state
        if (currentState == AmbienceState.Transitioning)
        {
            float depthRange = deepDepthThreshold - surfaceDepthThreshold;
            float depthProgress = (currentWaterDepth - surfaceDepthThreshold) / depthRange;
            depthProgress = Mathf.Clamp01(depthProgress);

            // Update volumes in real-time during transition
            if (surfaceAmbienceSource != null && surfaceAmbienceSource.isPlaying)
            {
                surfaceAmbienceSource.volume = maxSurfaceVolume * (1f - depthProgress);
            }

            if (deepAmbienceSource != null && deepAmbienceSource.isPlaying)
            {
                deepAmbienceSource.volume = maxDeepVolume * depthProgress;
            }
        }
    }

    private void StartAudioSourceIfNeeded(AudioSource source)
    {
        if (source != null && !source.isPlaying)
        {
            source.Play();
            DebugLog($"Started audio source: {source.name}");
        }
    }

    private void FadeAudioSource(AudioSource source, float targetVolume, float duration, bool stopWhenZero = false)
    {
        if (source == null) return;

        // Kill existing tween
        if (source == surfaceAmbienceSource)
        {
            surfaceVolumeTween?.Kill();
        }
        else if (source == deepAmbienceSource)
        {
            deepVolumeTween?.Kill();
        }

        // Create new tween
        Tweener newTween = source.DOFade(targetVolume, duration).SetEase(Ease.OutQuart);

        if (stopWhenZero && targetVolume <= 0f)
        {
            newTween.OnComplete(() => source.Stop());
        }

        // Store tween reference
        if (source == surfaceAmbienceSource)
        {
            surfaceVolumeTween = newTween;
        }
        else if (source == deepAmbienceSource)
        {
            deepVolumeTween = newTween;
        }
    }

    #region Event Handlers

    private void HandleWaterEntered()
    {
        DebugLog("Player entered water");
    }

    private void HandleWaterExited()
    {
        DebugLog("Player exited water - transitioning to silent");
        // Will be handled by UpdateAmbienceState
    }

    private void HandleHeadSubmerged()
    {
        DebugLog("Player head submerged - ambience may start");
    }

    private void HandleHeadSurfaced()
    {
        DebugLog("Player head surfaced - transitioning to silent");
        // Will be handled by UpdateAmbienceState
    }

    #endregion

    #region Debug and Utility

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[UnderwaterAmbience] {message}");
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugAmbienceState()
    {
        Debug.Log("=== UNDERWATER AMBIENCE DEBUG ===");
        Debug.Log($"Player In Water: {isPlayerInWater}");
        Debug.Log($"Head Submerged: {isHeadSubmerged}");
        Debug.Log($"Water Depth: {currentWaterDepth:F2}m");
        Debug.Log($"Current State: {currentState}");
        Debug.Log($"Target State: {targetState}");

        if (surfaceAmbienceSource != null)
            Debug.Log($"Surface Ambience: Playing={surfaceAmbienceSource.isPlaying}, Volume={surfaceAmbienceSource.volume:F2}");

        if (deepAmbienceSource != null)
            Debug.Log($"Deep Ambience: Playing={deepAmbienceSource.isPlaying}, Volume={deepAmbienceSource.volume:F2}");

        // NEW: Debug audio filtering
        if (enableUnderwaterFiltering)
        {
            Debug.Log("=== AUDIO FILTERING ===");
            if (activeLowPassFilter != null)
                Debug.Log($"Low Pass Filter: Cutoff={activeLowPassFilter.cutoffFrequency:F0}Hz");
            if (activeReverbFilter != null)
                Debug.Log($"Reverb Filter: Preset={activeReverbFilter.reverbPreset}, DryLevel={activeReverbFilter.dryLevel:F1}");
            Debug.Log($"Master Volume: {AudioListener.volume:F2}");
        }

        Debug.Log("================================");
    }

    /// <summary>
    /// NEW: Manual controls for testing underwater effects
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [Button("Test Underwater Effect")]
    public void TestUnderwaterEffect()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Underwater effect test only works in play mode");
            return;
        }
        ApplyUnderwaterEffect();
        Debug.Log("Applied underwater effect manually");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [Button("Test Surface Effect")]
    public void TestSurfaceEffect()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Surface effect test only works in play mode");
            return;
        }
        ApplySurfaceEffect();
        Debug.Log("Applied surface effect manually");
    }

    #endregion

    private void OnDestroy()
    {
        // Kill any active tweens
        surfaceVolumeTween?.Kill();
        deepVolumeTween?.Kill();
        filterTransitionTween?.Kill();
        volumeTransitionTween?.Kill();

        // Restore normal audio settings
        if (enableUnderwaterFiltering)
        {
            AudioListener.volume = 1f;
            if (activeLowPassFilter != null)
            {
                activeLowPassFilter.cutoffFrequency = 22000f;
            }
        }

        // Unsubscribe from events
        if (waterDetector != null)
        {
            waterDetector.OnWaterStateEntered -= HandleWaterEntered;
            waterDetector.OnWaterStateExited -= HandleWaterExited;
            waterDetector.OnHeadSubmerged -= HandleHeadSubmerged;
            waterDetector.OnHeadSurfaced -= HandleHeadSurfaced;
        }
    }
}