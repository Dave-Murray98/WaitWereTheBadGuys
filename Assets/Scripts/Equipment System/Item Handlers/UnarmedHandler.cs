using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: UnarmedHandler using the new unified held action system with enum-based animations.
/// Both primary and secondary actions are held melee (chargeable punches).
/// Much simpler since all held action logic is now in the base class with enum support.
/// </summary>
public class UnarmedHandler : BaseEquippedItemHandler
{
    [Header("Audio")]
    [SerializeField] private AudioClip punchSound;
    [SerializeField] private AudioClip heavyPunchSound;

    // Component references
    private AudioSource audioSource;

    // Events
    public System.Action<float, bool> OnPunchPerformed; // damage, isHeavy

    public override ItemType HandledItemType => ItemType.Unarmed;

    #region Initialization

    protected override void Awake()
    {
        base.Awake();
        SetupAudioSource();
    }

    private void SetupAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; // 2D sound
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }
    }

    #endregion

    #region Handler Implementation

    protected override void OnItemEquippedInternal(ItemData itemData)
    {
        // ItemData is null for unarmed - this is normal
        DebugLog($"ItemData is null: {itemData == null} (this is normal for unarmed)");

        DebugLog("Entered unarmed combat mode - state reset");
    }

    protected override void OnItemUnequippedInternal()
    {
        base.OnItemUnequippedInternal();

        DebugLog("Exited unarmed combat mode");
    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // For unarmed, primary action is melee - handle it manually
        HandleMeleeAction(context);
    }

    protected override void HandleSecondaryActionInternal(InputContext context)
    {
        // For unarmed, secondary action is melee - handle it manually
        HandleMeleeAction(context);
    }

    protected override void HandleReloadActionInternal(InputContext context)
    {
        DebugLog("Reload action called - not applicable for unarmed combat");
    }

    protected override bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        if (stateManager.CurrentStateType == PlayerStateType.Vehicle)
        {
            DebugLog("Cannot perform action while in vehicle");
            return false;
        }

        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
            case PlayerAnimationType.SecondaryAction:
            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                // Can perform melee if not already in an incompatible state
                if (currentActionState == ActionState.None)
                    return true;

                // Allow continuation during valid held action states
                if (currentHeldActionType == HeldActionType.Melee &&
                    (currentActionState == ActionState.Starting || currentActionState == ActionState.Looping))
                    return true;

                return false;

            case PlayerAnimationType.ReloadAction:
                DebugLog("Cannot perform reload - not applicable for unarmed");
                return false;

            default:
                DebugLog($"Unknown action type: {actionType}");
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        // Base class handles held action continuation automatically
        // No specific logic needed for unarmed
    }

    #endregion

    #region Unified System Overrides

    /// <summary>
    /// For unarmed, primary action should NOT be held - we handle it manually as melee
    /// </summary>
    protected override bool ShouldPrimaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// For unarmed, secondary action should NOT be held - we handle it manually as melee
    /// </summary>
    protected override bool ShouldSecondaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Called when starting melee action for unarmed
    /// </summary>
    protected override void OnHeldActionStarting(HeldActionType heldType, PlayerAnimationType actionType, InputContext context)
    {
        base.OnHeldActionStarting(heldType, actionType, context);
    }

    /// <summary>
    /// Called when melee becomes ready (charge started)
    /// </summary>
    protected override void OnMeleeReady()
    {
        base.OnMeleeReady();
        DebugLog("Punch charge ready - holding to build power");
    }

    /// <summary>
    /// Called when melee is executed (punch released)
    /// </summary>
    protected override void OnMeleeExecuted(float damage, bool isHeavy)
    {
        base.OnMeleeExecuted(damage, isHeavy);

        DebugLog($"Punch executed - Damage: {damage}, Heavy: {isHeavy}, Charge Time: {currentMeleeChargeTimer:F2}s");

        // Play appropriate punch sound
        PlayPunchSound(isHeavy);

        // Fire event
        OnPunchPerformed?.Invoke(damage, isHeavy);
    }

    /// <summary>
    /// Called when melee is completed (animation finished)
    /// </summary>
    protected override void OnMeleeCompleted()
    {
        base.OnMeleeCompleted();
        DebugLog("Punch animation completed - ready for next action");
    }

    /// <summary>
    /// Override melee damage calculation for unarmed (uses base meleeDamage)
    /// </summary>
    protected override float GetMeleeDamage() => meleeDamage;

    /// <summary>
    /// Apply unarmed-specific melee effects
    /// </summary>
    protected override void ApplyMeleeEffects(float damage, bool isHeavy)
    {
        base.ApplyMeleeEffects(damage, isHeavy);

        // Add unarmed-specific effects here if needed
        // For example: screen shake, camera punch, etc.
        DebugLog($"Unarmed punch effects applied - Damage: {damage}, Heavy: {isHeavy}");

        // TODO: Implement actual punch damage application logic
        // This would typically involve checking for targets in front of the player
        // and applying damage to any IDamageable components found
    }

    #endregion

    #region Audio System

    /// <summary>
    /// Play appropriate punch sound based on attack type
    /// </summary>
    private void PlayPunchSound(bool isHeavy)
    {
        if (audioSource == null) return;

        AudioClip soundToPlay = isHeavy ? heavyPunchSound : punchSound;

        if (soundToPlay != null)
        {
            audioSource.PlayOneShot(soundToPlay);
        }
        else
        {
            DebugLog($"Missing punch sound - Heavy: {isHeavy}");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Check if currently punching (charging or executing)
    /// </summary>
    public bool IsPunching() => isMeleeing;

    /// <summary>
    /// Check if currently charging a punch
    /// </summary>
    public bool IsChargingPunch() => isMeleeing &&
        (currentActionState == ActionState.Starting || currentActionState == ActionState.Looping);

    /// <summary>
    /// Check if ready to release punch
    /// </summary>
    public bool IsReadyToReleasePunch() => isReadyForAction && currentActionState == ActionState.Looping;

    /// <summary>
    /// Get current charge progress (0-1)
    /// </summary>
    public float GetChargeProgress()
    {
        if (!isMeleeing || meleeChargeTime <= 0f) return 0f;
        return Mathf.Clamp01(currentMeleeChargeTimer / meleeChargeTime);
    }

    /// <summary>
    /// Get charge progress as percentage string
    /// </summary>
    public string GetChargeProgressString() => $"{GetChargeProgress():P0}";

    /// <summary>
    /// Check if punch is fully charged
    /// </summary>
    public bool IsFullyCharged() => currentMeleeChargeTimer >= meleeChargeTime;

    /// <summary>
    /// Get current charge time
    /// </summary>
    public float GetCurrentChargeTime() => currentMeleeChargeTimer;

    /// <summary>
    /// Get charge threshold for heavy punch
    /// </summary>
    public float GetChargeThreshold() => meleeChargeTime;

    /// <summary>
    /// Check if next punch will be heavy
    /// </summary>
    public bool WillBeHeavyPunch() => IsChargingPunch() && IsFullyCharged();

    /// <summary>
    /// Get estimated damage for current charge
    /// </summary>
    public float GetEstimatedDamage()
    {
        float baseDamage = GetMeleeDamage();
        bool wouldBeHeavy = WillBeHeavyPunch();
        return baseDamage * (wouldBeHeavy ? 2.0f : 1.0f);
    }

    /// <summary>
    /// Override to provide specific debug info for unarmed
    /// </summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Action State: {currentActionState} ({currentActionAnimation}), " +
               $"Held Action Type: {currentHeldActionType}, " +
               $"Is Punching: {IsPunching()}, " +
               $"Is Charging: {IsChargingPunch()}, " +
               $"Charge Progress: {GetChargeProgressString()}, " +
               $"Will Be Heavy: {WillBeHeavyPunch()}, " +
               $"Estimated Damage: {GetEstimatedDamage():F1}";
    }

    #endregion
}

/// <summary>
/// Interface for objects that can take damage
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage);
}