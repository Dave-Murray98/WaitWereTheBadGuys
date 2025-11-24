using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: RangedWeaponHandler implementation using enum-based animation system.
/// Primary action: Shoot (instant with auto-fire support)
/// Secondary action: Toggle ADS (instant)
/// Reload action: Manual reload (instant)
/// Melee: Available through unified system
/// </summary>
public class RangedWeaponHandler : BaseEquippedItemHandler
{
    [Header("Weapon Audio")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip emptyFireSound;
    [SerializeField] private AudioClip reloadSound;

    [Header("Auto Fire Settings")]
    [SerializeField] private bool enableAutoFire = true;
    [SerializeField] private float autoFireRate = 10f; // shots per second

    [Header("Weapon State")]
    [SerializeField, ReadOnly] private bool isReloading = false;
    [SerializeField, ReadOnly] private bool isAiming = false;
    [SerializeField, ReadOnly] private bool isAutoFiring = false;
    [SerializeField, ReadOnly] private float lastShotTime = 0f;

    [Header("ADS System")]
    [SerializeField] private ADSController adsController;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask targetLayers = -1;

    // Components
    private AudioSource audioSource;
    private Camera playerCamera;

    // Quick access to weapon data
    private RangedWeaponData WeaponData => currentItemData?.RangedWeaponData;

    // Events
    public System.Action<ItemData> OnWeaponEquipped;
    public System.Action OnWeaponUnequipped;
    public System.Action OnWeaponFired;
    public System.Action OnWeaponReloaded;
    public System.Action<int, int> OnAmmoChanged; // current, max

    public override ItemType HandledItemType => ItemType.RangedWeapon;

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
        if (itemData?.itemType != ItemType.RangedWeapon)
        {
            Debug.LogError($"RangedWeaponHandler received invalid item: {itemData?.itemName ?? "null"}");
            return;
        }

        // Reset weapon state
        isReloading = false;
        isAiming = false;
        isAutoFiring = false;
        lastShotTime = 0f;

        DebugLog($"Equipped weapon: {itemData.itemName} - Ammo: {GetCurrentAmmo()}/{GetMaxAmmo()}");
        OnWeaponEquipped?.Invoke(itemData);
        OnAmmoChanged?.Invoke(GetCurrentAmmo(), GetMaxAmmo());
    }

    protected override void OnItemUnequippedInternal()
    {
        base.OnItemUnequippedInternal();

        // Clean up weapon state
        if (isAiming) StopAiming();
        isAutoFiring = false;
        isReloading = false;

        DebugLog("Unequipped weapon");
        OnWeaponUnequipped?.Invoke();
    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // Primary = Shoot
        if (context.isPressed)
        {
            StartFiring();
        }
        else if (context.isReleased)
        {
            StopFiring();
        }
        // Auto-fire continuation handled in Update
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
                return CanShoot(playerState);

            case PlayerAnimationType.SecondaryAction:
                return CanAim();

            case PlayerAnimationType.ReloadAction:
                return CanReload();

            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                return currentActionState == ActionState.None && !isAutoFiring;

            default:
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        // Handle auto-fire
        if (isAutoFiring && CanShoot(GetCurrentPlayerState()))
        {
            HandleAutoFire();
        }
    }

    #endregion

    #region Unified System Configuration

    /// <summary>
    /// Primary action is instant (not held) for ranged weapons
    /// </summary>
    protected override bool ShouldPrimaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Secondary action is instant (ADS toggle)
    /// </summary>
    protected override bool ShouldSecondaryActionBeHeld(InputContext context) => false;

    #endregion

    #region Shooting System

    /// <summary>
    /// Start firing process
    /// </summary>
    private void StartFiring()
    {
        if (!CanShoot(GetCurrentPlayerState()))
        {
            HandleEmptyFire();
            return;
        }

        // Fire first shot immediately
        FireSingleShot();

        // Start auto-fire if enabled
        if (enableAutoFire && GetFireRate() > 1f)
        {
            isAutoFiring = true;
        }
    }

    /// <summary>
    /// Stop firing process
    /// </summary>
    private void StopFiring()
    {
        isAutoFiring = false;
        DebugLog("Stopped firing");
    }

    /// <summary>
    /// Handle auto-fire timing
    /// </summary>
    private void HandleAutoFire()
    {
        if (!InputManager.Instance?.PrimaryActionHeld == true)
        {
            StopFiring();
            return;
        }

        float fireInterval = 1f / GetFireRate();
        if (Time.time - lastShotTime >= fireInterval)
        {
            FireSingleShot();
        }
    }

    /// <summary>
    /// Fire a single shot
    /// </summary>
    private void FireSingleShot()
    {
        if (!CanShoot(GetCurrentPlayerState()) || currentActionState != ActionState.None)
        {
            HandleEmptyFire();
            return;
        }

        lastShotTime = Time.time;
        DebugLog($"Firing shot - Ammo: {GetCurrentAmmo()}/{GetMaxAmmo()}");

        // Trigger shoot animation
        TriggerInstantAction(PlayerAnimationType.PrimaryAction);
    }

    /// <summary>
    /// Execute weapon fire effects (called by animation completion)
    /// </summary>
    protected override void OnActionCompletedInternal(PlayerAnimationType actionType)
    {
        base.OnActionCompletedInternal(actionType);

        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
                ExecuteWeaponFire();
                break;

            case PlayerAnimationType.ReloadAction:
                CompleteReload();
                break;
        }
    }

    /// <summary>
    /// Execute weapon fire and hit detection
    /// </summary>
    private void ExecuteWeaponFire()
    {
        // Play shoot sound
        PlaySound(shootSound);

        // Consume ammo
        if (!ConsumeAmmo(1))
        {
            DebugLog("Failed to consume ammo");
            return;
        }

        // Perform hit detection
        PerformHitDetection();

        // Fire events
        OnWeaponFired?.Invoke();
        OnAmmoChanged?.Invoke(GetCurrentAmmo(), GetMaxAmmo());

        DebugLog($"Weapon fired - Remaining ammo: {GetCurrentAmmo()}/{GetMaxAmmo()}");

        // Auto-reload if empty
        if (GetCurrentAmmo() <= 0)
        {
            StopFiring();
            Invoke(nameof(AutoReload), 0.2f);
        }
    }

    /// <summary>
    /// Perform weapon hit detection
    /// </summary>
    private void PerformHitDetection()
    {
        if (playerCamera == null) return;

        Vector3 rayOrigin = playerCamera.transform.position;
        Vector3 rayDirection = playerCamera.transform.forward;
        float range = GetRange();

        Debug.DrawRay(rayOrigin, rayDirection * range, Color.red, 0.1f);

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, range, targetLayers))
        {
            DebugLog($"Hit: {hit.collider.name} at distance {hit.distance:F2}");

            // Apply damage if target can take damage
            var damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float damage = GetDamage();
                damageable.TakeDamage(damage);
                DebugLog($"Dealt {damage} damage to {hit.collider.name}");
            }
        }
        else
        {
            DebugLog("Shot missed - no targets hit");
        }
    }

    /// <summary>
    /// Handle empty fire attempt
    /// </summary>
    private void HandleEmptyFire()
    {
        DebugLog("Empty fire - no ammo or cannot shoot");
        PlaySound(emptyFireSound);

        // Auto-reload if empty
        if (GetCurrentAmmo() <= 0 && CanReload())
        {
            DebugLog("Auto-reloading empty weapon");
            Invoke(nameof(AutoReload), 0.1f);
        }
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
            DebugLog("Cannot reload weapon");
            return;
        }

        // Stop auto-fire during reload
        StopFiring();

        isReloading = true;
        DebugLog($"Starting reload - Current ammo: {GetCurrentAmmo()}/{GetMaxAmmo()}");

        PlaySound(reloadSound);
        TriggerInstantAction(PlayerAnimationType.ReloadAction);
    }

    /// <summary>
    /// Auto-reload after emptying weapon
    /// </summary>
    private void AutoReload()
    {
        if (currentActionState == ActionState.None && !isReloading && GetCurrentAmmo() <= 0)
        {
            DebugLog("Starting auto-reload");
            StartReload();
        }
    }

    /// <summary>
    /// Complete reload process
    /// </summary>
    private void CompleteReload()
    {
        if (!isReloading) return;

        // Reload weapon to full
        if (WeaponData != null)
        {
            WeaponData.currentAmmo = WeaponData.maxAmmo;
        }

        isReloading = false;
        DebugLog($"Reload complete - Ammo: {GetCurrentAmmo()}/{GetMaxAmmo()}");

        OnWeaponReloaded?.Invoke();
        OnAmmoChanged?.Invoke(GetCurrentAmmo(), GetMaxAmmo());
    }

    #endregion

    #region ADS System

    /// <summary>
    /// Start aiming down sights
    /// </summary>
    private void StartAiming()
    {
        if (!CanAim()) return;

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
    /// Check if weapon can shoot
    /// </summary>
    private bool CanShoot(PlayerStateType playerState)
    {
        if (WeaponData == null) return false;
        if (GetCurrentAmmo() <= 0) return false;
        if (isReloading) return false;
        if (!currentItemData.CanUseInState(playerState)) return false;
        return true;
    }

    /// <summary>
    /// Check if can aim
    /// </summary>
    private bool CanAim()
    {
        if (WeaponData == null) return false;
        if (isReloading) return false;
        return IsPlayerInValidState();
    }

    /// <summary>
    /// Check if can reload
    /// </summary>
    private bool CanReload()
    {
        if (WeaponData == null) return false;
        if (isReloading) return false;
        if (currentActionState != ActionState.None) return false;
        if (GetCurrentAmmo() >= GetMaxAmmo()) return false;
        return true;
    }

    #endregion

    #region Weapon Data Access

    /// <summary>Get current weapon damage</summary>
    public float GetDamage() => WeaponData?.damage ?? 10f;

    /// <summary>Get current weapon range</summary>
    public float GetRange() => WeaponData?.range ?? 100f;

    /// <summary>Get current weapon fire rate</summary>
    public float GetFireRate() => WeaponData?.fireRate ?? autoFireRate;

    /// <summary>Get current ammo count</summary>
    public int GetCurrentAmmo() => WeaponData?.currentAmmo ?? 0;

    /// <summary>Get maximum ammo capacity</summary>
    public int GetMaxAmmo() => WeaponData?.maxAmmo ?? 30;

    /// <summary>Consume ammo</summary>
    public bool ConsumeAmmo(int amount)
    {
        if (WeaponData == null || WeaponData.currentAmmo < amount) return false;
        WeaponData.currentAmmo -= amount;
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

    /// <summary>Check if currently firing</summary>
    public bool IsFiring() => currentActionState == ActionState.Instant &&
                              currentActionAnimation == PlayerAnimationType.PrimaryAction;

    /// <summary>Check if auto-firing</summary>
    public bool IsAutoFiring() => isAutoFiring;

    /// <summary>Check if currently reloading</summary>
    public bool IsReloading() => isReloading;

    /// <summary>Check if currently aiming</summary>
    public bool IsAiming() => isAiming;

    /// <summary>Check if weapon has ammo</summary>
    public bool HasAmmo() => GetCurrentAmmo() > 0;

    /// <summary>Force stop all weapon actions</summary>
    public void ForceStopAllActions()
    {
        if (isAiming) StopAiming();
        StopFiring();
        if (currentActionState != ActionState.None) ForceCompleteAction();
        isReloading = false;
        DebugLog("Force stopped all weapon actions");
    }

    /// <summary>Debug info</summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Action State: {currentActionState} ({currentActionAnimation}), " +
               $"Ammo: {GetCurrentAmmo()}/{GetMaxAmmo()}, " +
               $"Auto-Fire: {isAutoFiring}, Reloading: {isReloading}, Aiming: {isAiming}";
    }

    #endregion
}