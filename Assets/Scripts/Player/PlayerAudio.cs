using System;
using UnityEngine;

/// <summary>
/// Modular player audio system that adapts to different movement contexts.
/// Handles footsteps, swimming sounds, and movement effects based on current environment.
/// </summary>
public class PlayerAudio : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource movementAudioSource;
    [SerializeField] private AudioSource effectAudioSource;

    [Header("Ground Movement Audio")]
    [SerializeField] private FootstepAudioSet defaultFootsteps;
    [SerializeField] private FootstepAudioSet grassFootsteps;
    [SerializeField] private FootstepAudioSet metalFootsteps;
    [SerializeField] private FootstepAudioSet woodFootsteps;
    [SerializeField] private FootstepAudioSet waterFootsteps; // For walking in shallow water

    [Header("Swimming Audio")]
    [SerializeField] private SwimmingAudioSet surfaceSwimming;
    [SerializeField] private SwimmingAudioSet underwaterSwimming;

    [Header("Effect Audio")]
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip landClip;
    [SerializeField] private AudioClip waterEnterClip;
    [SerializeField] private AudioClip waterExitClip;

    [Header("Audio Settings")]
    [SerializeField] private float footstepBaseRate = 0.5f;
    [SerializeField] private float swimmingBaseRate = 0.8f; // Increased from 0.6f for less frequent sounds
    [SerializeField] private float verticalSwimmingRateMultiplier = 1.3f; // Slower rate for vertical-only movement
    [SerializeField] private float combinedMovementRateMultiplier = 0.9f; // Slightly faster for combined movement
    [SerializeField] private bool enableDebugLogs = false;

    // Component references
    private PlayerController playerController;
    private PlayerWaterDetector waterDetector;
    private PlayerData playerData;

    // Audio state
    private float audioTimer;
    private MovementMode currentAudioMode;
    private AudioContext currentAudioContext;
    private float lastSwimmingAudioTime; // NEW: Track last swimming audio time
    private float minSwimmingAudioInterval = 0.3f; // NEW: Minimum time between swimming sounds

    // Audio context tracking
    private enum AudioContext
    {
        GroundDry,           // Normal ground movement
        GroundWet,           // Walking in shallow water
        SurfaceSwimming,     // Swimming at surface
        UnderwaterSwimming   // Swimming underwater
    }

    public void Initialize(PlayerController controller)
    {
        playerController = controller;
        playerData = GameManager.Instance?.playerData;
        waterDetector = GetComponent<PlayerWaterDetector>();

        SetupAudioSources();

        // Subscribe to water events
        if (waterDetector != null)
        {
            waterDetector.OnWaterStateEntered += HandleWaterEntered;
            waterDetector.OnWaterStateExited += HandleWaterExited;
            waterDetector.OnHeadSubmerged += HandleHeadSubmerged;
            waterDetector.OnHeadSurfaced += HandleHeadSurfaced;
        }

        DebugLog("PlayerAudio initialized with modular system");
    }

    private void SetupAudioSources()
    {
        // Ensure we have audio sources
        if (movementAudioSource == null)
            movementAudioSource = GetComponent<AudioSource>();

        if (effectAudioSource == null)
            effectAudioSource = movementAudioSource;
    }

    private void Update()
    {
        if (playerController == null) return;

        UpdateAudioContext();
        HandleMovementAudio();
    }

    /// <summary>
    /// Determines the current audio context based on player state
    /// </summary>
    private void UpdateAudioContext()
    {
        AudioContext newContext;

        // Determine audio context based on movement mode and water state
        switch (playerController.CurrentMovementMode)
        {
            case MovementMode.Ground:
                // Check if feet are in water for shallow water walking
                // if (waterDetector != null && !waterDetector.IsInWater)
                // {
                //     newContext = AudioContext.GroundWet;
                // }
                // else
                // {
                newContext = AudioContext.GroundDry;
                //}
                break;

            case MovementMode.Swimming:
                // Check if head is underwater for swimming context
                if (waterDetector != null && waterDetector.IsHeadUnderwater)
                {
                    newContext = AudioContext.UnderwaterSwimming;
                }
                else
                {
                    newContext = AudioContext.SurfaceSwimming;
                }
                break;

            default:
                newContext = AudioContext.GroundDry;
                break;
        }

        // Log context changes
        if (newContext != currentAudioContext)
        {
            DebugLog($"Audio context changed: {currentAudioContext} -> {newContext}");
            currentAudioContext = newContext;
        }

        currentAudioMode = playerController.CurrentMovementMode;
    }

    /// <summary>
    /// Handles movement audio based on current context
    /// REFACTORED: Better control over swimming audio frequency with minimum intervals
    /// </summary>
    private void HandleMovementAudio()
    {
        bool shouldPlayAudio = ShouldPlayMovementAudio();

        if (!shouldPlayAudio)
        {
            return;
        }

        float audioRate = GetCurrentAudioRate();
        audioTimer += Time.deltaTime;

        // REFACTORED: Additional check for swimming audio minimum interval
        bool canPlaySwimmingAudio = true;
        if (currentAudioMode == MovementMode.Swimming)
        {
            float timeSinceLastSwimmingAudio = Time.time - lastSwimmingAudioTime;
            canPlaySwimmingAudio = timeSinceLastSwimmingAudio >= minSwimmingAudioInterval;

            if (!canPlaySwimmingAudio)
            {
                DebugLog($"Swimming audio blocked - only {timeSinceLastSwimmingAudio:F2}s since last sound (min: {minSwimmingAudioInterval})");
                return;
            }
        }

        if (audioTimer >= audioRate && canPlaySwimmingAudio)
        {
            PlayMovementSound();
            audioTimer = 0f;

            // Track swimming audio timing
            if (currentAudioMode == MovementMode.Swimming)
            {
                lastSwimmingAudioTime = Time.time;
            }
        }
    }

    /// <summary>
    /// ENHANCED: Determines if movement audio should play based on horizontal OR vertical movement
    /// </summary>
    private bool ShouldPlayMovementAudio()
    {
        // For ground movement, use the standard IsMoving check
        if (currentAudioMode == MovementMode.Ground)
        {
            return playerController.IsMoving;
        }

        // For swimming, check both horizontal movement AND vertical actions
        if (currentAudioMode == MovementMode.Swimming)
        {
            // Check horizontal movement
            bool hasHorizontalMovement = playerController.IsMoving;

            // Check vertical movement (diving or surfacing)
            bool hasVerticalMovement = IsPlayerDivingOrSurfacing();

            // Play audio if there's ANY movement (horizontal OR vertical)
            return hasHorizontalMovement || hasVerticalMovement;
        }

        return playerController.IsMoving;
    }

    /// <summary>
    /// NEW: Checks if player is actively diving or surfacing
    /// </summary>
    private bool IsPlayerDivingOrSurfacing()
    {
        if (playerController.swimmingMovementController == null) return false;

        // Check if diving (secondary action active in swimming mode)
        bool isDiving = playerController.IsDiving;

        // Check if surfacing using reflection to access private field
        bool isSurfacing = GetSwimmingControllerSurfacingState();

        return isDiving || isSurfacing;
    }

    /// <summary>
    /// NEW: Gets the surfacing state from swimming controller using reflection
    /// </summary>
    private bool GetSwimmingControllerSurfacingState()
    {
        if (playerController.swimmingMovementController == null) return false;

        try
        {
            var surfacingField = typeof(SwimmingMovementController).GetField("isSurfacingActive",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (surfacingField != null)
            {
                return (bool)surfacingField.GetValue(playerController.swimmingMovementController);
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Could not access surfacing state: {e.Message}");
        }

        return false;
    }

    /// <summary>
    /// Gets the appropriate audio rate based on context and movement speed
    /// REFACTORED: More controllable swimming audio timing with separate rates for different movement types
    /// </summary>
    private float GetCurrentAudioRate()
    {
        float baseRate = currentAudioMode == MovementMode.Swimming ? swimmingBaseRate : footstepBaseRate;

        // Use player data if available, but with swimming-specific adjustments
        if (playerData != null)
        {
            if (currentAudioMode == MovementMode.Swimming)
            {
                // For swimming, use a separate multiplier to avoid too-frequent sounds
                baseRate = swimmingBaseRate; // Don't use playerData.footstepRate for swimming
            }
            else
            {
                baseRate = playerData.footstepRate;
            }
        }

        // REFACTORED: More nuanced swimming audio timing
        if (currentAudioMode == MovementMode.Swimming)
        {
            bool hasHorizontalMovement = playerController.IsMoving;
            bool hasVerticalMovement = IsPlayerDivingOrSurfacing();

            // Different rates for different types of swimming movement
            if (hasVerticalMovement && !hasHorizontalMovement)
            {
                // Pure vertical movement (diving/surfacing only) - slower rate
                baseRate *= verticalSwimmingRateMultiplier;
                DebugLog($"Pure vertical swimming - rate: {baseRate:F2}");
            }
            else if (hasVerticalMovement && hasHorizontalMovement)
            {
                // Combined movement - slightly faster but not too much
                baseRate *= combinedMovementRateMultiplier;
                DebugLog($"Combined swimming movement - rate: {baseRate:F2}");
            }
            // Horizontal only movement uses base rate (no multiplier)
        }

        // Apply speed modifiers (but less aggressive for swimming)
        if (playerController.IsSprinting)
        {
            if (currentAudioMode == MovementMode.Swimming)
            {
                baseRate *= 0.8f; // Less aggressive speed increase for swimming
            }
            else
            {
                baseRate *= 0.7f; // Normal for ground movement
            }
        }
        else if (playerController.IsCrouching)
        {
            baseRate *= 1.5f; // Slower sounds for crouching
        }

        return baseRate;
    }

    /// <summary>
    /// Plays the appropriate movement sound based on current context
    /// ENHANCED: Includes debug information for vertical movement
    /// </summary>
    private void PlayMovementSound()
    {
        if (movementAudioSource == null) return;

        // Debug vertical movement for swimming
        if (enableDebugLogs && currentAudioMode == MovementMode.Swimming)
        {
            bool horizontalMovement = playerController.IsMoving;
            bool verticalMovement = IsPlayerDivingOrSurfacing();
            DebugLog($"Swimming audio - Horizontal: {horizontalMovement}, Vertical: {verticalMovement} (Diving: {playerController.IsDiving}, Surfacing: {GetSwimmingControllerSurfacingState()})");
        }

        switch (currentAudioContext)
        {
            case AudioContext.GroundDry:
                PlayFootstepSound();
                break;
            case AudioContext.GroundWet:
                PlayWaterFootstepSound();
                break;
            case AudioContext.SurfaceSwimming:
                PlaySurfaceSwimmingSound();
                break;
            case AudioContext.UnderwaterSwimming:
                PlayUnderwaterSwimmingSound();
                break;
        }
    }

    /// <summary>
    /// Plays normal footstep sounds based on ground type
    /// </summary>
    private void PlayFootstepSound()
    {
        if (playerController.groundMovementController == null) return;

        GroundType groundType = playerController.groundMovementController.CurrentGroundType;
        FootstepAudioSet audioSet = GetFootstepSetForGroundType(groundType);

        if (audioSet != null && audioSet.clips.Length > 0)
        {
            PlayAudioFromSet(audioSet.clips, GetMovementVolume(), audioSet.pitchVariation);
        }
    }

    /// <summary>
    /// Plays water footstep sounds for shallow water walking
    /// </summary>
    private void PlayWaterFootstepSound()
    {
        if (waterFootsteps != null && waterFootsteps.clips.Length > 0)
        {
            PlayAudioFromSet(waterFootsteps.clips, GetMovementVolume(), waterFootsteps.pitchVariation);
        }
    }

    /// <summary>
    /// Plays surface swimming sounds
    /// </summary>
    private void PlaySurfaceSwimmingSound()
    {
        if (surfaceSwimming == null) return;

        AudioClip[] clipsToUse = playerController.IsSprinting ?
            surfaceSwimming.fastSwimmingClips : surfaceSwimming.normalSwimmingClips;

        if (clipsToUse.Length > 0)
        {
            PlayAudioFromSet(clipsToUse, GetSwimmingVolume(), surfaceSwimming.pitchVariation);
        }
    }

    /// <summary>
    /// Plays underwater swimming sounds
    /// </summary>
    private void PlayUnderwaterSwimmingSound()
    {
        if (underwaterSwimming == null) return;

        AudioClip[] clipsToUse = playerController.IsSprinting ?
            underwaterSwimming.fastSwimmingClips : underwaterSwimming.normalSwimmingClips;

        if (clipsToUse.Length > 0)
        {
            PlayAudioFromSet(clipsToUse, GetSwimmingVolume(), underwaterSwimming.pitchVariation);
        }
    }

    /// <summary>
    /// Plays audio from a set of clips with variations
    /// </summary>
    private void PlayAudioFromSet(AudioClip[] clips, float volume, Vector2 pitchRange)
    {
        if (clips.Length == 0) return;

        AudioClip clipToPlay = clips[UnityEngine.Random.Range(0, clips.Length)];
        float pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);

        movementAudioSource.pitch = pitch;
        movementAudioSource.PlayOneShot(clipToPlay, volume);
    }

    /// <summary>
    /// Gets the footstep set for the specified ground type
    /// </summary>
    private FootstepAudioSet GetFootstepSetForGroundType(GroundType groundType)
    {
        switch (groundType)
        {
            case GroundType.Grass: return grassFootsteps;
            case GroundType.Metal: return metalFootsteps;
            case GroundType.Wood: return woodFootsteps;
            case GroundType.Water: return waterFootsteps;
            default: return defaultFootsteps;
        }
    }

    /// <summary>
    /// Gets movement volume based on player state
    /// </summary>
    private float GetMovementVolume()
    {
        if (playerController.IsCrouching) return 0.3f;
        if (playerController.IsSprinting) return 0.8f;
        return 0.6f;
    }

    /// <summary>
    /// Gets swimming volume based on player state
    /// </summary>
    private float GetSwimmingVolume()
    {
        if (playerController.IsSprinting) return 0.8f;
        return 0.6f;
    }

    #region Effect Audio Methods

    public void PlayJumpSound()
    {
        PlayEffectSound(jumpClip, 0.7f);
    }

    public void PlayLandSound()
    {
        PlayEffectSound(landClip, 0.8f);
    }

    public void PlayWaterEnterSound()
    {
        PlayEffectSound(waterEnterClip, 0.8f);
    }

    public void PlayWaterExitSound()
    {
        PlayEffectSound(waterExitClip, 0.7f);
    }

    private void PlayEffectSound(AudioClip clip, float volume)
    {
        if (effectAudioSource != null && clip != null)
        {
            effectAudioSource.PlayOneShot(clip, volume);
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles movement state changes for effect sounds
    /// </summary>
    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        // Jump sound
        if (newState == MovementState.Jumping && previousState != MovementState.Jumping)
        {
            PlayJumpSound();
        }

        // Landing sound
        if ((newState == MovementState.Idle || newState == MovementState.Walking) &&
            previousState == MovementState.Falling)
        {
            PlayLandSound();
        }
    }

    private void HandleWaterEntered()
    {
        PlayWaterEnterSound();
        DebugLog("Water entered - played enter sound");
    }

    private void HandleWaterExited()
    {
        PlayWaterExitSound();
        DebugLog("Water exited - played exit sound");
    }

    private void HandleHeadSubmerged()
    {
        DebugLog("Head submerged - switching to underwater audio context");
    }

    private void HandleHeadSurfaced()
    {
        DebugLog("Head surfaced - switching to surface audio context");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerAudio] {message}");
        }
    }

    private void OnDestroy()
    {
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

/// <summary>
/// Serializable class for organizing footstep audio clips
/// </summary>
[System.Serializable]
public class FootstepAudioSet
{
    [Header("Audio Clips")]
    public AudioClip[] clips;

    [Header("Audio Variation")]
    public Vector2 pitchVariation = new Vector2(0.9f, 1.1f);

    [Header("Volume Settings")]
    public float baseVolume = 1f;
}

/// <summary>
/// Serializable class for organizing swimming audio clips
/// </summary>
[System.Serializable]
public class SwimmingAudioSet
{
    [Header("Swimming Audio Clips")]
    public AudioClip[] normalSwimmingClips;
    public AudioClip[] fastSwimmingClips;

    [Header("Audio Variation")]
    public Vector2 pitchVariation = new Vector2(0.9f, 1.1f);

    [Header("Volume Settings")]
    public float baseVolume = 1f;
}