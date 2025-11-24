using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: BowHandler implementation using enum-based animation system.
/// Primary action: Held draw/shoot (start → loop → end)
/// Secondary action: Toggle ADS (instant)
/// Reload action: Manual reload (instant)
/// Melee: Available through unified system
/// </summary>
public class BowHandler : BaseEquippedItemHandler
{
    [Header("Bow Audio")]
    [SerializeField] private AudioClip drawSound;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip reloadSound;

    [Header("Bow State")]
    [SerializeField, ReadOnly] private bool isDrawn = false;
    [SerializeField, ReadOnly] private bool isReloading = false;
    [SerializeField, ReadOnly] private bool isAiming = false;

    [Header("ADS System")]
    [SerializeField] private ADSController adsController;

    // Components
    private AudioSource audioSource;
    private Camera playerCamera;

    // Quick access to bow data
    private BowData BowData => currentItemData?.BowData;

    // Events
    public System.Action<ItemData> OnBowEquipped;
    public System.Action OnBowUnequipped;
    public System.Action OnBowShot;
    public System.Action OnBowReloaded;

    public override ItemType HandledItemType => ItemType.Bow;

    #region Initialization

    protected override void Awake()
    {
        base.Awake();
        SetupComponents();
    }

    private void SetupComponents()
    {
        // Audio setup
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
        }

        // Camera reference
        playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();

        // ADS controller
        if (adsController == null)
            adsController = GetComponent<ADSController>() ?? FindFirstObjectByType<ADSController>();
    }

    #endregion

    #region Base Handler Implementation

    protected override void OnItemEquippedInternal(ItemData itemData)
    {
        if (itemData?.itemType != ItemType.Bow)
        {
            Debug.LogError($"BowHandler received invalid item: {itemData?.itemName ?? "null"}");
            return;
        }

        // Reset bow state
        isDrawn = false;
        isReloading = false;
        isAiming = false;

        DebugLog($"Equipped bow: {itemData.itemName}");
        OnBowEquipped?.Invoke(itemData);
    }

    protected override void OnItemUnequippedInternal()
    {
        base.OnItemUnequippedInternal();

        // Clean up bow state
        if (isAiming) StopAiming();
        isDrawn = false;
        isReloading = false;

        DebugLog("Unequipped bow");
        OnBowUnequipped?.Invoke();
    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // This won't be called since we return true for ShouldPrimaryActionBeHeld
        DebugLog("HandlePrimaryActionInternal - should not be called for bow");
    }

    protected override void HandleSecondaryActionInternal(InputContext context)
    {
        // Secondary = Toggle ADS
        if (context.isPressed)
        {
            if (isAiming) StopAiming();
            else StartAiming();
        }
    }

    protected override void HandleReloadActionInternal(InputContext context)
    {
        // Manual reload
        if (context.isPressed)
        {
            StartReload();
        }
    }

    protected override bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
            case >= PlayerAnimationType.HeldPrimaryActionStart and <= PlayerAnimationType.CancelHeldPrimaryAction:
                return CanDrawBow(playerState);

            case PlayerAnimationType.SecondaryAction:
                return CanAim();

            case PlayerAnimationType.ReloadAction:
                return CanReload();

            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                return currentActionState == ActionState.None;

            default:
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        // Base class handles held action continuation
    }

    #endregion

    #region Unified System Configuration

    /// <summary>
    /// Primary action is held for bow (draw and shoot)
    /// </summary>
    protected override bool ShouldPrimaryActionBeHeld(InputContext context) => true;

    /// <summary>
    /// Secondary action is instant (ADS toggle)
    /// </summary>
    protected override bool ShouldSecondaryActionBeHeld(InputContext context) => false;

    #endregion

    #region Held Action Events

    /// <summary>
    /// Called when starting to draw the bow
    /// </summary>
    protected override void OnHeldActionStarting(HeldActionType heldType, PlayerAnimationType actionType, InputContext context)
    {
        base.OnHeldActionStarting(heldType, actionType, context);

        if (heldType == HeldActionType.Primary)
        {
            DebugLog("Starting bow draw");
            PlaySound(drawSound);
        }
    }

    /// <summary>
    /// Called when bow draw is complete and ready to shoot
    /// </summary>
    protected override void OnPrimaryActionReady()
    {
        base.OnPrimaryActionReady();
        isDrawn = true;
        DebugLog("Bow fully drawn - ready to shoot");
    }

    /// <summary>
    /// Called when bow is shot (held action executed)
    /// </summary>
    protected override void ExecutePrimaryAction(PlayerAnimationType actionType)
    {
        base.ExecutePrimaryAction(actionType);
        DebugLog("Executing bow shot");

        // Play shoot sound
        PlaySound(shootSound);

        // Execute the shot
        FireArrow();

        // Fire events
        OnBowShot?.Invoke();
    }

    /// <summary>
    /// Called when primary action completes
    /// </summary>
    protected override void OnPrimaryActionCompleted(PlayerAnimationType actionType)
    {
        base.OnPrimaryActionCompleted(actionType);

        // Reset bow state
        isDrawn = false;
        DebugLog("Bow shot completed - ready for new actions");

        // Start auto-reload after a short delay
        Invoke(nameof(AutoReload), 0.1f);
    }

    /// <summary>
    /// Called when held action is cancelled
    /// </summary>
    protected override void OnHeldActionCancelled(PlayerAnimationType actionType)
    {
        base.OnHeldActionCancelled(actionType);

        if (currentHeldActionType == HeldActionType.Primary)
        {
            isDrawn = false;
            DebugLog("Bow draw cancelled");
        }
    }

    #endregion

    #region Bow Shooting

    /// <summary>
    /// Fire an arrow from the bow
    /// </summary>
    private void FireArrow()
    {
        if (BowData == null)
        {
            DebugLog("Cannot fire - no bow data");
            return;
        }

        DebugLog($"Firing arrow - Damage: {BowData.damage}, Range: {BowData.range}");

        // TODO: Implement arrow projectile spawning
        // TODO: Apply damage to targets
        // TODO: Consume ammo when ammo system is implemented

        // For now, just log the shot
        DebugLog("Arrow fired successfully");
    }

    #endregion

    #region Reload System

    /// <summary>
    /// Start reload process
    /// </summary>
    private void StartReload()
    {
        if (!CanReload())
        {
            DebugLog("Cannot reload bow");
            return;
        }

        isReloading = true;
        DebugLog("Starting bow reload");

        PlaySound(reloadSound);
        TriggerInstantAction(PlayerAnimationType.ReloadAction);
    }

    /// <summary>
    /// Auto-reload after shooting
    /// </summary>
    private void AutoReload()
    {
        // Only auto-reload if we're not busy
        if (currentActionState == ActionState.None && !isReloading)
        {
            DebugLog("Starting auto-reload");
            StartReload();
        }
    }

    /// <summary>
    /// Complete reload process
    /// </summary>
    protected override void OnActionCompletedInternal(PlayerAnimationType actionType)
    {
        base.OnActionCompletedInternal(actionType);

        if (actionType == PlayerAnimationType.ReloadAction)
        {
            isReloading = false;
            DebugLog("Bow reload completed");
            OnBowReloaded?.Invoke();
        }
    }

    #endregion

    #region ADS System

    /// <summary>
    /// Start aiming down sights
    /// </summary>
    private void StartAiming()
    {
        if (!CanAim())
        {
            DebugLog("Cannot start aiming");
            return;
        }

        isAiming = true;
        DebugLog("Started aiming down sights");

        if (adsController != null)
            adsController.StartAimingDownSights();
    }

    /// <summary>
    /// Stop aiming down sights
    /// </summary>
    private void StopAiming()
    {
        if (!isAiming) return;

        isAiming = false;
        DebugLog("Stopped aiming down sights");

        if (adsController != null)
            adsController.StopAimingDownSights();
    }

    #endregion

    #region State Validation

    /// <summary>
    /// Check if bow can be drawn
    /// </summary>
    private bool CanDrawBow(PlayerStateType playerState)
    {
        if (BowData == null) return false;
        if (isReloading) return false;
        if (!currentItemData.CanUseInState(playerState)) return false;

        // Allow drawing during Starting and Looping for held actions
        if (currentActionState != ActionState.None &&
            currentActionState != ActionState.Starting &&
            currentActionState != ActionState.Looping)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if can aim
    /// </summary>
    private bool CanAim()
    {
        if (BowData == null) return false;
        if (isReloading) return false;
        return IsPlayerInValidState();
    }

    /// <summary>
    /// Check if can reload
    /// </summary>
    private bool CanReload()
    {
        if (BowData == null) return false;
        if (isReloading) return false;
        if (currentActionState != ActionState.None) return false;

        // TODO: Check if reload is needed when ammo system is implemented
        return true;
    }

    #endregion

    #region Utility

    /// <summary>
    /// Play audio clip
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    #endregion

    #region Public API

    /// <summary>Check if currently drawing</summary>
    public bool IsDrawing() => currentHeldActionType == HeldActionType.Primary &&
                               (currentActionState == ActionState.Starting || currentActionState == ActionState.Looping);

    /// <summary>Check if ready to shoot</summary>
    public bool IsReadyToShoot() => isDrawn && currentActionState == ActionState.Looping;

    /// <summary>Check if currently reloading</summary>
    public bool IsReloading() => isReloading;

    /// <summary>Check if currently aiming</summary>
    public bool IsAiming() => isAiming;

    /// <summary>Get current bow damage</summary>
    public float GetBowDamage() => BowData?.damage ?? 0f;

    /// <summary>Get current bow range</summary>
    public float GetBowRange() => BowData?.range ?? 50f;

    /// <summary>Force stop all bow actions</summary>
    public void ForceStopAllActions()
    {
        if (isAiming) StopAiming();
        if (currentActionState != ActionState.None) ForceCompleteAction();
        isDrawn = false;
        isReloading = false;
        DebugLog("Force stopped all bow actions");
    }

    /// <summary>Debug info</summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Action State: {currentActionState} ({currentActionAnimation}), " +
               $"Held Action: {currentHeldActionType}, " +
               $"Is Drawn: {isDrawn}, Reloading: {isReloading}, Aiming: {isAiming}";
    }

    #endregion
}