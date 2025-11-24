using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: MeleeWeaponHandler using enum-based animation system with chargeable attacks.
/// Primary action: Chargeable melee attack (held - charge → release)
/// Secondary action: Chargeable melee attack (held - charge → release)
/// Uses weapon damage as base instead of meleeDamage
/// </summary>
public class MeleeWeaponHandler : BaseEquippedItemHandler
{
    [Header("Weapon Audio")]
    [SerializeField] private AudioClip lightAttackSound;
    [SerializeField] private AudioClip heavyAttackSound;

    [Header("Weapon State")]
    [SerializeField, ReadOnly] private float lastAttackTime = 0f;
    [SerializeField, ReadOnly] private int attackCount = 0;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask targetLayers = -1;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackRadius = 0.5f;

    // Components
    private AudioSource audioSource;
    private Camera playerCamera;

    // Quick access to weapon data
    private MeleeWeaponData WeaponData => currentItemData?.MeleeWeaponData;

    // Events
    public System.Action<ItemData> OnWeaponEquipped;
    public System.Action OnWeaponUnequipped;
    public System.Action<float, bool> OnMeleePerformed; // damage, isHeavy

    public override ItemType HandledItemType => ItemType.MeleeWeapon;

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
    }

    #endregion

    #region Base Handler Implementation

    protected override void OnItemEquippedInternal(ItemData itemData)
    {
        if (itemData?.itemType != ItemType.MeleeWeapon)
        {
            Debug.LogError($"MeleeWeaponHandler received invalid item: {itemData?.itemName ?? "null"}");
            return;
        }

        // Reset weapon state
        lastAttackTime = 0f;
        attackCount = 0;

        DebugLog($"Equipped melee weapon: {itemData.itemName} - Damage: {GetWeaponDamage()}");
        OnWeaponEquipped?.Invoke(itemData);
    }

    protected override void OnItemUnequippedInternal()
    {
        base.OnItemUnequippedInternal();

        // Reset attack state
        attackCount = 0;

        DebugLog("Unequipped melee weapon");
        OnWeaponUnequipped?.Invoke();
    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // Primary action is handled by unified system as held melee
        HandleMeleeAction(context);
    }

    protected override void HandleSecondaryActionInternal(InputContext context)
    {
        // Secondary action is also handled by unified system as held melee
        HandleMeleeAction(context);
    }

    protected override void HandleReloadActionInternal(InputContext context)
    {
        // Not applicable for melee weapons
        DebugLog("Reload not applicable for melee weapons");
    }

    protected override bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
            case PlayerAnimationType.SecondaryAction:
            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                return CanAttack(playerState);

            default:
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        // Base class handles held action continuation automatically
    }

    #endregion

    #region Unified System Configuration

    /// <summary>
    /// Primary action should NOT be held - we handle it manually as melee
    /// </summary>
    protected override bool ShouldPrimaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Secondary action should NOT be held - we handle it manually as melee
    /// </summary>
    protected override bool ShouldSecondaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Override melee damage to use weapon damage instead of base damage
    /// </summary>
    protected override float GetMeleeDamage() => GetWeaponDamage();

    #endregion

    #region Held Action Events (Chargeable Attacks)

    /// <summary>
    /// Called when starting melee action for weapon
    /// </summary>
    protected override void OnHeldActionStarting(HeldActionType heldType, PlayerAnimationType actionType, InputContext context)
    {
        base.OnHeldActionStarting(heldType, actionType, context);

        if (heldType == HeldActionType.Melee)
        {
            attackCount++;
            lastAttackTime = Time.time;
            DebugLog($"Starting weapon charge attack #{attackCount}");
        }
    }

    /// <summary>
    /// Called when melee becomes ready (charge started)
    /// </summary>
    protected override void OnMeleeReady()
    {
        base.OnMeleeReady();
        DebugLog("Weapon charge ready - holding to build power");
    }

    /// <summary>
    /// Called when melee is executed (attack released)
    /// </summary>
    protected override void OnMeleeExecuted(float damage, bool isHeavy)
    {
        base.OnMeleeExecuted(damage, isHeavy);

        DebugLog($"Weapon attack executed - Damage: {damage}, Heavy: {isHeavy}, Charge Time: {currentMeleeChargeTimer:F2}s");

        // Play appropriate attack sound
        PlayAttackSound(isHeavy);

        // Perform hit detection
        bool hitTarget = PerformWeaponHitDetection(damage);

        // Fire event
        OnMeleePerformed?.Invoke(damage, isHeavy);

        DebugLog($"Weapon attack completed - Hit Target: {hitTarget}");
    }

    /// <summary>
    /// Called when melee is completed (animation finished)
    /// </summary>
    protected override void OnMeleeCompleted()
    {
        base.OnMeleeCompleted();
        DebugLog("Weapon attack animation completed - ready for next action");
    }

    /// <summary>
    /// Apply weapon-specific melee effects
    /// </summary>
    protected override void ApplyMeleeEffects(float damage, bool isHeavy)
    {
        base.ApplyMeleeEffects(damage, isHeavy);

        // Add weapon-specific effects here if needed
        DebugLog($"Weapon melee effects applied - Damage: {damage}, Heavy: {isHeavy}");
    }

    #endregion

    #region Weapon Hit Detection

    /// <summary>
    /// Perform weapon hit detection with weapon-specific parameters
    /// </summary>
    private bool PerformWeaponHitDetection(float damage)
    {
        if (playerCamera == null) return false;

        Vector3 attackOrigin = playerCamera.transform.position;
        Vector3 attackDirection = playerCamera.transform.forward;

        // Use sphere cast for weapon attack area
        if (Physics.SphereCast(attackOrigin, attackRadius, attackDirection, out RaycastHit hit, attackRange, targetLayers))
        {
            DebugLog($"Weapon hit: {hit.collider.name} at distance {hit.distance:F2}");

            // Apply damage if target can take damage
            var damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
                DebugLog($"Dealt {damage} weapon damage to {hit.collider.name}");
                return true;
            }
        }

        DebugLog("Weapon attack missed - no targets hit");
        return false;
    }

    #endregion

    #region State Validation

    /// <summary>
    /// Check if can perform weapon attack
    /// </summary>
    private bool CanAttack(PlayerStateType playerState)
    {
        if (WeaponData == null)
        {
            DebugLog("Cannot attack - no weapon data");
            return false;
        }

        if (!currentItemData.CanUseInState(playerState))
        {
            DebugLog($"Cannot attack - weapon cannot be used in state: {playerState}");
            return false;
        }

        // Different logic for different action states
        switch (currentActionState)
        {
            case ActionState.None:
                // Can start any action when not performing any action
                return true;

            case ActionState.Starting:
            case ActionState.Looping:
                // During charge phases, allow continuation if it's a melee action
                return currentHeldActionType == HeldActionType.Melee;

            case ActionState.Ending:
            case ActionState.Cancelling:
            case ActionState.Instant:
                // During end/cancel/instant phases, can't start new actions
                return false;

            default:
                return false;
        }
    }

    #endregion

    #region Weapon Data Access

    /// <summary>Get current weapon damage</summary>
    public float GetWeaponDamage() => WeaponData?.damage ?? meleeDamage;

    /// <summary>Get current weapon data</summary>
    public MeleeWeaponData GetCurrentWeaponData() => WeaponData;

    #endregion

    #region Audio System

    /// <summary>
    /// Play appropriate attack sound based on attack type
    /// </summary>
    private void PlayAttackSound(bool isHeavy)
    {
        if (audioSource == null) return;

        AudioClip soundToPlay = isHeavy ? heavyAttackSound : lightAttackSound;

        if (soundToPlay != null)
        {
            audioSource.PlayOneShot(soundToPlay);
        }
        else
        {
            DebugLog($"Missing attack sound - Heavy: {isHeavy}");
        }
    }

    #endregion

    #region Public API

    /// <summary>Check if currently attacking (charging or executing)</summary>
    public bool IsAttacking() => isMeleeing;

    /// <summary>Check if currently charging an attack</summary>
    public bool IsChargingAttack() => isMeleeing &&
        (currentActionState == ActionState.Starting || currentActionState == ActionState.Looping);

    /// <summary>Check if ready to release attack</summary>
    public bool IsReadyToReleaseAttack() => isReadyForAction && currentActionState == ActionState.Looping;

    /// <summary>Get current charge progress (0-1)</summary>
    public float GetChargeProgress()
    {
        if (!isMeleeing || meleeChargeTime <= 0f) return 0f;
        return Mathf.Clamp01(currentMeleeChargeTimer / meleeChargeTime);
    }

    /// <summary>Get charge progress as percentage string</summary>
    public string GetChargeProgressString() => $"{GetChargeProgress():P0}";

    /// <summary>Check if attack is fully charged</summary>
    public bool IsFullyCharged() => currentMeleeChargeTimer >= meleeChargeTime;

    /// <summary>Get current charge time</summary>
    public float GetCurrentChargeTime() => currentMeleeChargeTimer;

    /// <summary>Get charge threshold for heavy attack</summary>
    public float GetChargeThreshold() => meleeChargeTime;

    /// <summary>Check if next attack will be heavy</summary>
    public bool WillBeHeavyAttack() => IsChargingAttack() && IsFullyCharged();

    /// <summary>Get estimated damage for current charge</summary>
    public float GetEstimatedDamage()
    {
        float baseDamage = GetWeaponDamage();
        bool wouldBeHeavy = WillBeHeavyAttack();
        return baseDamage * (wouldBeHeavy ? 2.0f : 1.0f);
    }

    /// <summary>Get attack count</summary>
    public int GetAttackCount() => attackCount;

    /// <summary>Reset attack count</summary>
    public void ResetAttackCount()
    {
        attackCount = 0;
        DebugLog("Attack count reset");
    }

    /// <summary>Get time since last attack</summary>
    public float GetTimeSinceLastAttack() => Time.time - lastAttackTime;

    /// <summary>Debug info</summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Action State: {currentActionState} ({currentActionAnimation}), " +
               $"Held Action Type: {currentHeldActionType}, " +
               $"Is Attacking: {IsAttacking()}, " +
               $"Is Charging: {IsChargingAttack()}, " +
               $"Charge Progress: {GetChargeProgressString()}, " +
               $"Will Be Heavy: {WillBeHeavyAttack()}, " +
               $"Estimated Damage: {GetEstimatedDamage():F1}, " +
               $"Attack Count: {attackCount}";
    }

    #endregion
}