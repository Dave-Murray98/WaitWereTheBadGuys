using UnityEngine;

/// <summary>
/// REFACTORED: LowerBodyAnimationController now uses enum-based animation system for maximum performance.
/// UPDATED: Added support for vehicle seat type detection for standing vs sitting animations.
/// Controls lower body animations (locomotion only) on layer 0.
/// Uses a dedicated LocomotionAnimationDatabase for all movement animations.
/// Completely independent of equipped items - only responds to player state and movement input.
/// </summary>
public class LowerBodyAnimationController : MonoBehaviour
{
    [Header("Locomotion Animation Database")]
    [SerializeField] private LocomotionAnimationDatabase locomotionDatabase;

    [Header("Animation Settings")]
    [SerializeField] private float locomotionCrossfadeDuration = 0.1f;
    [SerializeField] private float stateCrossfadeDuration = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Core references
    private PlayerAnimationManager animationManager;
    private Animator animator;
    private int layerIndex;

    // Current state tracking
    private PlayerStateType currentPlayerState = PlayerStateType.Ground;
    private PlayerAnimationType currentAnimationType = PlayerAnimationType.Idle;
    private AnimationClip currentClip = null;

    // Movement state tracking
    private Vector2 lastMovementInput = Vector2.zero;
    private bool lastIsCrouching = false;
    private bool lastIsRunning = false;

    // UPDATED: Vehicle state tracking for seat type detection
    private bool lastIsVehicleSeated = false;

    // Events
    public System.Action<AnimationClip> OnAnimationStarted;

    #region Initialization

    /// <summary>
    /// Initialize the lower body controller with animator and layer references
    /// </summary>
    public void Initialize(PlayerAnimationManager manager, Animator anim, int layer)
    {
        animationManager = manager;
        animator = anim;
        layerIndex = layer;

        // Find locomotion database if auto-find is enabled
        if (locomotionDatabase == null)
        {
            Debug.LogError("Locomotion database not assigned");
        }

        // Validate setup
        ValidateSetup();

        // Reset state
        ResetControllerState();

        DebugLog($"LowerBodyAnimationController initialized on layer {layerIndex} with database: {locomotionDatabase?.displayName ?? "None"}");
    }

    /// <summary>
    /// Validate the controller setup
    /// </summary>
    private void ValidateSetup()
    {
        if (locomotionDatabase == null)
        {
            Debug.LogError("[LowerBodyAnimationController] No LocomotionAnimationDatabase assigned! Lower body animations will not work. Please assign one or enable auto-find.");
            return;
        }

        if (animator == null)
        {
            Debug.LogError("[LowerBodyAnimationController] No Animator assigned!");
            return;
        }

        if (layerIndex >= animator.layerCount)
        {
            Debug.LogError($"[LowerBodyAnimationController] Layer index {layerIndex} exceeds animator layer count ({animator.layerCount})");
            return;
        }

        DebugLog("Lower body controller setup validation passed");
    }

    /// <summary>
    /// Reset controller state to defaults
    /// </summary>
    private void ResetControllerState()
    {
        currentPlayerState = PlayerStateType.Ground;
        currentAnimationType = PlayerAnimationType.Idle;
        currentClip = null;
        lastMovementInput = Vector2.zero;
        lastIsCrouching = false;
        lastIsRunning = false;
        lastIsVehicleSeated = false; // NEW: Reset vehicle seat tracking
    }

    /// <summary>
    /// Set the locomotion animation database (for runtime assignment)
    /// </summary>
    public void SetLocomotionDatabase(LocomotionAnimationDatabase database)
    {
        locomotionDatabase = database;
        DebugLog($"Locomotion database updated: {database?.displayName ?? "None"}");

        // Refresh current animation if we have a valid setup
        if (database != null && animator != null)
        {
            RefreshCurrentAnimation();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// UPDATED: Update locomotion animation with vehicle seat type support
    /// </summary>
    public void UpdateLocomotion(PlayerStateType playerState, Vector2 movementInput, bool isCrouching, bool isRunning)
    {
        // Get vehicle seat type if in vehicle state
        bool isVehicleSeated = GetCurrentVehicleSeatType(playerState);

        // Check if we need to update the animation
        if (ShouldUpdateAnimation(playerState, movementInput, isCrouching, isRunning, isVehicleSeated))
        {
            PlayLocomotionAnimation(playerState, movementInput, isCrouching, isRunning, isVehicleSeated);
        }

        // Always update tracking variables
        UpdateTrackingVariables(playerState, movementInput, isCrouching, isRunning, isVehicleSeated);
    }

    /// <summary>
    /// Handle player state changes (Ground -> Water, etc.)
    /// </summary>
    public void OnPlayerStateChanged(PlayerStateType newState)
    {
        DebugLog($"Player state changed: {currentPlayerState} -> {newState}");

        PlayerStateType previousState = currentPlayerState;
        currentPlayerState = newState;

        // Get vehicle seat type if entering vehicle state
        bool isVehicleSeated = GetCurrentVehicleSeatType(newState);

        // Force animation update for state change (use longer crossfade)
        PlayLocomotionAnimation(newState, lastMovementInput, lastIsCrouching, lastIsRunning, isVehicleSeated, true);
    }

    /// <summary>
    /// Refresh animations (useful for database updates or system resets)
    /// </summary>
    public void RefreshAnimations()
    {
        DebugLog("Refreshing lower body animations");
        RefreshCurrentAnimation();
    }

    #endregion

    #region UPDATED: Animation Logic with Vehicle Seat Support

    /// <summary>
    /// UPDATED: Check if animation needs to be updated (now includes vehicle seat type)
    /// </summary>
    private bool ShouldUpdateAnimation(PlayerStateType playerState, Vector2 movementInput, bool isCrouching, bool isRunning, bool isVehicleSeated)
    {
        // Always update if player state changed
        if (playerState != currentPlayerState)
            return true;

        // NEW: Always update if vehicle seat type changed
        if (isVehicleSeated != lastIsVehicleSeated)
            return true;

        // Check movement input changes (with threshold to avoid jitter)
        if (Vector2.Distance(movementInput, lastMovementInput) > 0.1f)
            return true;

        // Check movement modifier changes
        if (isCrouching != lastIsCrouching || isRunning != lastIsRunning)
            return true;

        return false;
    }

    /// <summary>
    /// UPDATED: Play the appropriate locomotion animation with vehicle seat support
    /// </summary>
    private void PlayLocomotionAnimation(PlayerStateType playerState, Vector2 movementInput, bool isCrouching, bool isRunning, bool isVehicleSeated, bool forceStateChange = false)
    {
        if (locomotionDatabase == null || animator == null)
            return;

        // Get the animation type based on movement input and state using converter
        PlayerAnimationType animationType = GetLocomotionAnimationType(playerState, movementInput, isCrouching, isRunning, isVehicleSeated);

        // Don't replay the same animation unless forced (state change)
        if (!forceStateChange && animationType == currentAnimationType && playerState == currentPlayerState)
            return;

        // Get animation clip from database using enum
        AnimationClip clip = locomotionDatabase.GetLocomotionAnimation(animationType);

        if (clip != null)
        {
            PlayAnimationClip(clip, animationType, forceStateChange);
        }
        else
        {
            // Try fallback animations
            HandleMissingAnimation(playerState, animationType, forceStateChange);
        }
    }

    /// <summary>
    /// UPDATED: Determine the animation type based on input and player state with vehicle seat support
    /// </summary>
    private PlayerAnimationType GetLocomotionAnimationType(PlayerStateType playerState, Vector2 movementInput, bool isCrouching, bool isRunning, bool isVehicleSeated)
    {
        return playerState switch
        {
            PlayerStateType.Ground => MovementToAnimationConverter.GetGroundLocomotionAnimation(movementInput, isCrouching, isRunning),
            PlayerStateType.Water => MovementToAnimationConverter.GetWaterLocomotionAnimation(movementInput, isRunning),
            PlayerStateType.Vehicle => MovementToAnimationConverter.GetVehicleLocomotionAnimation(isVehicleSeated),
            PlayerStateType.Climbing => MovementToAnimationConverter.GetClimbingLocomotionAnimation(),
            _ => PlayerAnimationType.Idle // Default to idle for unknown states
        };
    }

    /// <summary>
    /// NEW: Get the current vehicle's seat type from PlayerController
    /// </summary>
    private bool GetCurrentVehicleSeatType(PlayerStateType playerState)
    {
        if (playerState != PlayerStateType.Vehicle)
            return false;

        // Access PlayerController through the animation manager
        var playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null)
            return false;

        var currentVehicle = playerController.GetCurrentVehicle();
        if (currentVehicle == null)
            return false;

        bool isSeated = currentVehicle.IsVehicleSeated;
        DebugLog($"Vehicle seat type detected: {(isSeated ? "Sitting" : "Standing")} for vehicle {currentVehicle.VehicleID}");

        return isSeated;
    }

    /// <summary>
    /// Play animation clip with appropriate crossfade duration
    /// </summary>
    private void PlayAnimationClip(AnimationClip clip, PlayerAnimationType animationType, bool isStateChange = false)
    {
        if (clip == null || animator == null) return;

        // Use different crossfade duration for state changes vs normal locomotion
        float crossfadeDuration = isStateChange ? stateCrossfadeDuration : locomotionCrossfadeDuration;

        // Play animation using the clip name as state name in animator
        string stateName = clip.name;
        animator.CrossFade(stateName, crossfadeDuration, layerIndex);

        // Update tracking
        currentAnimationType = animationType;
        currentClip = clip;

        // Fire event
        OnAnimationStarted?.Invoke(clip);

        DebugLog($"Playing lower body animation: {stateName} ({animationType}) with {crossfadeDuration:F2}s crossfade");
    }

    /// <summary>
    /// Handle missing animations with fallback system
    /// </summary>
    private void HandleMissingAnimation(PlayerStateType playerState, PlayerAnimationType animationType, bool forceStateChange)
    {
        DebugLog($"Animation not found: {animationType}, attempting fallback");

        // Try fallback to basic idle if requested animation not found
        if (animationType != PlayerAnimationType.Idle)
        {
            PlayerAnimationType fallbackType = GetFallbackAnimationType(playerState, animationType);
            AnimationClip fallbackClip = locomotionDatabase.GetLocomotionAnimation(fallbackType);

            if (fallbackClip != null)
            {
                PlayAnimationClip(fallbackClip, fallbackType, forceStateChange);
                DebugLog($"Used fallback animation: {fallbackType} for missing {animationType}");
                return;
            }
        }

        // If we get here, even fallback failed
        Debug.LogError($"[LowerBodyAnimationController] Critical animation missing and no fallback available: {animationType} in {playerState}");
    }

    /// <summary>
    /// UPDATED: Get appropriate fallback animation type with vehicle support
    /// </summary>
    private PlayerAnimationType GetFallbackAnimationType(PlayerStateType playerState, PlayerAnimationType originalType)
    {
        return playerState switch
        {
            PlayerStateType.Ground => originalType switch
            {
                >= PlayerAnimationType.CrouchIdle and <= PlayerAnimationType.CrouchWalkBackwardRight => PlayerAnimationType.CrouchIdle,
                _ => PlayerAnimationType.Idle
            },
            PlayerStateType.Water => PlayerAnimationType.SwimIdle,
            PlayerStateType.Vehicle => PlayerAnimationType.VehicleIdleStanding, // NEW: Fallback to standing if sitting animation missing
            _ => PlayerAnimationType.Idle
        };
    }

    /// <summary>
    /// Refresh current animation (useful after database changes)
    /// </summary>
    private void RefreshCurrentAnimation()
    {
        bool isVehicleSeated = GetCurrentVehicleSeatType(currentPlayerState);
        PlayLocomotionAnimation(currentPlayerState, lastMovementInput, lastIsCrouching, lastIsRunning, isVehicleSeated, true);
    }

    /// <summary>
    /// UPDATED: Update tracking variables with vehicle seat support
    /// </summary>
    private void UpdateTrackingVariables(PlayerStateType playerState, Vector2 movementInput, bool isCrouching, bool isRunning, bool isVehicleSeated)
    {
        currentPlayerState = playerState;
        lastMovementInput = movementInput;
        lastIsCrouching = isCrouching;
        lastIsRunning = isRunning;
        lastIsVehicleSeated = isVehicleSeated; // NEW: Track vehicle seat type
    }

    #endregion

    #region Public Properties and Debug

    /// <summary>
    /// Get current animation type for debugging
    /// </summary>
    public PlayerAnimationType GetCurrentAnimationType()
    {
        return currentAnimationType;
    }

    /// <summary>
    /// Get current animation clip for debugging
    /// </summary>
    public AnimationClip GetCurrentClip()
    {
        return currentClip;
    }

    /// <summary>
    /// UPDATED: Get debug information about the controller state with vehicle info
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Lower Body Animation Controller Debug ===");
        info.AppendLine($"Current State: {currentPlayerState}");
        info.AppendLine($"Current Animation: {currentAnimationType}");
        info.AppendLine($"Current Clip: {currentClip?.name ?? "None"}");
        info.AppendLine($"Last Movement Input: {lastMovementInput}");
        info.AppendLine($"Is Crouching: {lastIsCrouching}");
        info.AppendLine($"Is Running: {lastIsRunning}");
        info.AppendLine($"Is Vehicle Seated: {lastIsVehicleSeated}"); // NEW: Show vehicle seat state
        info.AppendLine($"Has Database: {locomotionDatabase != null}");

        if (locomotionDatabase != null)
        {
            var availableAnims = locomotionDatabase.GetAvailableAnimationTypesForState(currentPlayerState);
            info.AppendLine($"Available Animations for {currentPlayerState}: {availableAnims.Length}");
        }

        return info.ToString();
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[LowerBodyAnimationController] {message}");
        }
    }
}