using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: ToolHandler implementation using enum-based animation system.
/// Primary action: Melee attack (through unified system)
/// Secondary action: Use tool (instant or held based on ToolData.isActionHeld)
/// Supports both instant tools (like placing C4) and held tools (like blowtorch)
/// </summary>
public class ToolHandler : BaseEquippedItemHandler
{
    [Header("Tool Audio")]
    [SerializeField] private AudioClip toolStartSound;
    [SerializeField] private AudioClip toolCompleteSound;
    [SerializeField] private AudioClip toolLoopSound;

    [Header("Tool State")]
    [SerializeField, ReadOnly] private bool isUsingTool = false;
    [SerializeField, ReadOnly] private bool isToolReady = false;

    // Components
    private AudioSource audioSource;

    // Quick access to tool data
    private ToolData ToolData => currentItemData?.ToolData;

    // Events
    public System.Action<ItemData> OnToolEquipped;
    public System.Action OnToolUnequipped;
    public System.Action OnToolUsed;
    public System.Action OnToolCompleted;

    public override ItemType HandledItemType => ItemType.Tool;

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
    }

    #endregion

    #region Base Handler Implementation

    protected override void OnItemEquippedInternal(ItemData itemData)
    {
        if (itemData?.itemType != ItemType.Tool)
        {
            Debug.LogError($"ToolHandler received invalid item: {itemData?.itemName ?? "null"}");
            return;
        }

        // Reset tool state
        isUsingTool = false;
        isToolReady = false;

        bool isHeldTool = ToolData?.isActionHeld ?? false;
        DebugLog($"Equipped tool: {itemData.itemName} (Type: {(isHeldTool ? "Held" : "Instant")})");
        OnToolEquipped?.Invoke(itemData);
    }

    protected override void OnItemUnequippedInternal()
    {
        base.OnItemUnequippedInternal();

        // Reset tool state
        isUsingTool = false;
        isToolReady = false;

        DebugLog("Unequipped tool");
        OnToolUnequipped?.Invoke();
    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // Primary = Melee attack (using tool as improvised weapon)
        if (context.isPressed)
        {
            HandleMeleeAction(context);
        }
    }

    protected override void HandleSecondaryActionInternal(InputContext context)
    {
        // Secondary = Use tool (instant version only)
        // Held tools are handled by the unified system automatically
        if (context.isPressed && !IsCurrentToolHeld())
        {
            UseInstantTool();
        }
    }

    protected override void HandleReloadActionInternal(InputContext context)
    {
        // Not applicable for tools
        DebugLog("Reload not applicable for tools");
    }

    protected override bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                // Can melee if not using tool (or if using held tool and ready)
                return !isUsingTool || (IsCurrentToolHeld() && currentActionState == ActionState.None);

            case PlayerAnimationType.SecondaryAction:
            case >= PlayerAnimationType.HeldSecondaryActionStart and <= PlayerAnimationType.CancelHeldSecondaryAction:
                return CanUseTool(playerState);

            default:
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        // Base class handles held action continuation for held tools
    }

    #endregion

    #region Unified System Configuration

    /// <summary>
    /// Primary action is instant (melee with tool)
    /// </summary>
    protected override bool ShouldPrimaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Secondary action depends on tool type
    /// </summary>
    protected override bool ShouldSecondaryActionBeHeld(InputContext context) => IsCurrentToolHeld();

    /// <summary>
    /// Override melee damage for tool (weaker than dedicated weapons)
    /// </summary>
    protected override float GetMeleeDamage() => meleeDamage * 0.85f; // 85% of base damage

    #endregion

    #region Instant Tool Usage

    /// <summary>
    /// Use instant tool (like placing C4, using scanner, etc.)
    /// </summary>
    private void UseInstantTool()
    {
        if (!CanUseTool(GetCurrentPlayerState()))
        {
            DebugLog("Cannot use instant tool");
            return;
        }

        isUsingTool = true;
        DebugLog($"Using instant tool: {currentItemData.itemName}");

        PlaySound(toolStartSound);
        TriggerInstantAction(PlayerAnimationType.SecondaryAction);
    }

    /// <summary>
    /// Complete instant tool usage (called by animation completion)
    /// </summary>
    protected override void OnActionCompletedInternal(PlayerAnimationType actionType)
    {
        base.OnActionCompletedInternal(actionType);

        if (actionType == PlayerAnimationType.SecondaryAction)
        {
            CompleteInstantToolUse();
        }
    }

    /// <summary>
    /// Complete instant tool use and apply effects
    /// </summary>
    private void CompleteInstantToolUse()
    {
        if (!isUsingTool || IsCurrentToolHeld())
        {
            return; // Not using instant tool
        }

        DebugLog($"Instant tool completed: {currentItemData.itemName}");

        // Apply tool effects
        ApplyToolEffects();

        // Play completion sound
        PlaySound(toolCompleteSound);

        // Fire events
        OnToolUsed?.Invoke();
        OnToolCompleted?.Invoke();

        // Reset state
        isUsingTool = false;
    }

    #endregion

    #region Held Tool Events

    /// <summary>
    /// Called when starting to use a held tool (like blowtorch)
    /// </summary>
    protected override void OnHeldActionStarting(HeldActionType heldType, PlayerAnimationType actionType, InputContext context)
    {
        base.OnHeldActionStarting(heldType, actionType, context);

        if (heldType == HeldActionType.Secondary)
        {
            isUsingTool = true;
            DebugLog($"Starting held tool: {currentItemData.itemName}");
            PlaySound(toolStartSound);
        }
    }

    /// <summary>
    /// Called when held tool is ready for use
    /// </summary>
    protected override void OnSecondaryActionReady()
    {
        base.OnSecondaryActionReady();
        isToolReady = true;
        DebugLog("Held tool ready - applying continuous effects");

        // Start loop sound for held tools
        PlaySound(toolLoopSound);
    }

    /// <summary>
    /// Called when held tool action is executed (on release)
    /// </summary>
    protected override void ExecuteSecondaryAction(PlayerAnimationType actionType)
    {
        base.ExecuteSecondaryAction(actionType);
        DebugLog("Held tool action executed");

        // Apply final tool effects
        ApplyToolEffects();
        OnToolUsed?.Invoke();
    }

    /// <summary>
    /// Called when held tool action completes
    /// </summary>
    protected override void OnSecondaryActionCompleted(PlayerAnimationType actionType)
    {
        base.OnSecondaryActionCompleted(actionType);

        // Reset tool state
        isUsingTool = false;
        isToolReady = false;
        DebugLog("Held tool completed");

        PlaySound(toolCompleteSound);
        OnToolCompleted?.Invoke();
    }

    /// <summary>
    /// Called when held tool action is cancelled
    /// </summary>
    protected override void OnHeldActionCancelled(PlayerAnimationType actionType)
    {
        base.OnHeldActionCancelled(actionType);

        if (currentHeldActionType == HeldActionType.Secondary)
        {
            isUsingTool = false;
            isToolReady = false;
            DebugLog($"Held tool cancelled: {currentItemData.itemName}");
        }
    }

    #endregion

    #region Tool Effects System

    /// <summary>
    /// Apply tool effects based on tool type
    /// </summary>
    private void ApplyToolEffects()
    {
        if (ToolData == null) return;

        string toolType = ToolData.isActionHeld ? "held" : "instant";
        DebugLog($"Applying {toolType} tool effects: {currentItemData.itemName}");

        // TODO: Implement specific tool effects based on tool type and name:

        // INSTANT TOOLS:
        // - "C4" or "Explosive": Place explosive device at current location
        // - "Scanner": Scan area for items, enemies, or secrets
        // - "GPS": Update map or add waypoint
        // - "Lockpick": Attempt to unlock nearby door/chest
        // - "Hacking Device": Interact with nearby terminal/computer
        // - "Medical Kit": Apply healing over time
        // - "Repair Kit": Fix nearby damaged objects

        // HELD TOOLS:
        // - "Blowtorch": Continuous damage/melting to objects in front
        // - "Drill": Continuous drilling through materials
        // - "Welder": Continuous repair of objects
        // - "Metal Detector": Continuous scanning while held
        // - "Flashlight": Continuous illumination
        // - "Radio": Continuous communication channel

        // Example implementation:
        string toolName = currentItemData.itemName.ToLower();

        if (toolName.Contains("scanner"))
        {
            PerformScannerEffect();
        }
        else if (toolName.Contains("c4") || toolName.Contains("explosive"))
        {
            PlaceExplosive();
        }
        else if (toolName.Contains("blowtorch"))
        {
            ApplyBlowtorchEffect();
        }
        else if (toolName.Contains("lockpick"))
        {
            AttemptLockpicking();
        }
        else if (toolName.Contains("medical") || toolName.Contains("medkit"))
        {
            ApplyMedicalKit();
        }
        else if (toolName.Contains("repair"))
        {
            PerformRepair();
        }
        else if (toolName.Contains("flashlight"))
        {
            ToggleFlashlight();
        }
        else
        {
            // Generic tool effect
            DebugLog($"Generic tool effect applied for: {currentItemData.itemName}");
        }
    }

    /// <summary>
    /// Example: Scanner tool effect
    /// </summary>
    private void PerformScannerEffect()
    {
        DebugLog("Scanner activated - revealing nearby objects");

        // TODO: Implement scanner logic:
        // - Raycast or sphere overlap to find nearby objects
        // - Highlight items, enemies, or interactables
        // - Add markers to UI or minimap
        // - Play scanning visual effects

        // Example simple implementation:
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, 10f);
        int itemCount = 0;

        foreach (var obj in nearbyObjects)
        {
            if (obj.GetComponent<IInteractable>() != null)
            {
                itemCount++;
                // Could highlight the object here
                DebugLog($"Scanner detected interactable: {obj.name}");
            }
        }

        DebugLog($"Scanner found {itemCount} interactable objects nearby");
    }

    /// <summary>
    /// Example: C4 placement effect
    /// </summary>
    private void PlaceExplosive()
    {
        DebugLog("C4 placed at current location");

        // TODO: Implement explosive placement:
        // - Spawn explosive GameObject at player position
        // - Set up detonation trigger (timer, remote, proximity)
        // - Add to list of placed explosives
        // - Play placement visual/audio effects

        Vector3 placementPosition = transform.position + transform.forward * 1f;
        DebugLog($"Explosive placed at position: {placementPosition}");
    }

    /// <summary>
    /// Example: Blowtorch continuous effect
    /// </summary>
    private void ApplyBlowtorchEffect()
    {
        DebugLog("Blowtorch effect applied - heating/melting objects");

        // TODO: Implement blowtorch logic:
        // - Raycast forward to find objects in range
        // - Apply heat/damage over time to objects
        // - Create particle effects (flames, sparks)
        // - Play continuous audio loop
        // - Consume fuel/battery over time

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 3f))
        {
            DebugLog($"Blowtorch targeting: {hit.collider.name}");

            // Could apply damage to destructible objects
            var destructible = hit.collider.GetComponent<IDestructible>();
            if (destructible != null)
            {
                destructible.TakeDamage(Time.deltaTime * 50f); // 50 damage per second
            }
        }
    }

    /// <summary>
    /// Example: Lockpicking effect
    /// </summary>
    private void AttemptLockpicking()
    {
        DebugLog("Attempting to pick lock");

        // TODO: Implement lockpicking logic:
        // - Check for nearby locked doors/chests
        // - Start lockpicking minigame
        // - Success/failure based on player skill or timing

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 2f))
        {
            var lockable = hit.collider.GetComponent<ILockable>();
            if (lockable != null && lockable.IsLocked())
            {
                DebugLog($"Attempting to pick lock on: {hit.collider.name}");
                // Could start lockpicking minigame here
            }
            else
            {
                DebugLog("No locked object found to pick");
            }
        }
    }

    /// <summary>
    /// Example: Medical kit effect
    /// </summary>
    private void ApplyMedicalKit()
    {
        DebugLog("Applying medical kit - restoring health");

        // TODO: Implement medical kit logic:
        // - Restore player health over time
        // - Remove status effects (poison, bleeding)
        // - Play healing visual effects

        if (GameManager.Instance?.playerManager != null)
        {
            float healAmount = 25f; // Heal 25 HP
            GameManager.Instance.playerManager.ModifyHealth(healAmount);
            DebugLog($"Medical kit restored {healAmount} health");
        }
    }

    /// <summary>
    /// Example: Repair kit effect
    /// </summary>
    private void PerformRepair()
    {
        DebugLog("Using repair kit on nearby objects");

        // TODO: Implement repair logic:
        // - Find nearby damaged objects
        // - Restore their durability/health
        // - Play repair visual/audio effects

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 2f))
        {
            var repairable = hit.collider.GetComponent<IRepairable>();
            if (repairable != null && repairable.NeedsRepair())
            {
                repairable.Repair(50f); // Repair 50 durability
                DebugLog($"Repaired object: {hit.collider.name}");
            }
            else
            {
                DebugLog("No repairable object found");
            }
        }
    }

    /// <summary>
    /// Example: Flashlight effect
    /// </summary>
    private void ToggleFlashlight()
    {
        DebugLog("Toggling flashlight");

        // TODO: Implement flashlight logic:
        // - Toggle light component on/off
        // - Consume battery over time
        // - Adjust light intensity based on battery level

        Light flashlight = GetComponentInChildren<Light>();
        if (flashlight != null)
        {
            flashlight.enabled = !flashlight.enabled;
            DebugLog($"Flashlight {(flashlight.enabled ? "ON" : "OFF")}");
        }
        else
        {
            DebugLog("No flashlight component found");
        }
    }

    #endregion

    #region State Validation

    /// <summary>
    /// Check if can use tool in current state
    /// </summary>
    private bool CanUseTool(PlayerStateType playerState)
    {
        if (ToolData == null)
        {
            DebugLog("Cannot use tool - no tool data");
            return false;
        }

        // For held tools, allow during Starting/Looping if already using
        if (ToolData.isActionHeld)
        {
            if (!isUsingTool)
            {
                return currentActionState == ActionState.None;
            }
            else
            {
                return currentActionState == ActionState.Starting ||
                       currentActionState == ActionState.Looping ||
                       currentActionState == ActionState.None;
            }
        }
        else
        {
            // For instant tools, must not be performing any action
            if (currentActionState != ActionState.None)
            {
                DebugLog("Cannot use instant tool - already performing action");
                return false;
            }
        }

        if (!currentItemData.CanUseInState(playerState))
        {
            DebugLog($"Cannot use tool - item cannot be used in state: {playerState}");
            return false;
        }

        return true;
    }

    #endregion

    #region Utility

    /// <summary>
    /// Check if current tool is held type
    /// </summary>
    private bool IsCurrentToolHeld() => ToolData?.isActionHeld ?? false;

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

    /// <summary>Check if currently using any tool</summary>
    public bool IsUsingTool() => isUsingTool;

    /// <summary>Check if performing held tool action</summary>
    public bool IsPerformingHeldToolAction() => isUsingTool && IsCurrentToolHeld() &&
                                                (currentActionState == ActionState.Starting ||
                                                 currentActionState == ActionState.Looping);

    /// <summary>Check if tool is ready (held tools only)</summary>
    public bool IsToolReady() => isToolReady;

    /// <summary>Get current tool data</summary>
    public ToolData GetCurrentToolData() => ToolData;

    /// <summary>Check if current tool is instant type</summary>
    public bool IsCurrentToolInstant() => !IsCurrentToolHeld();

    /// <summary>Get tool name for effect routing</summary>
    public string GetToolName() => currentItemData?.itemName ?? "";

    /// <summary>Force stop all tool actions</summary>
    public void ForceStopAllActions()
    {
        if (currentActionState != ActionState.None) ForceCompleteAction();
        isUsingTool = false;
        isToolReady = false;
        DebugLog("Force stopped all tool actions");
    }

    /// <summary>Debug info</summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Action State: {currentActionState} ({currentActionAnimation}), " +
               $"Held Action: {currentHeldActionType}, " +
               $"Using Tool: {isUsingTool}, Tool Ready: {isToolReady}, " +
               $"Tool Type: {(IsCurrentToolHeld() ? "Held" : "Instant")}";
    }

    #endregion
}

public interface IDestructible
{
    void TakeDamage(float damage);
    bool IsDestroyed();
}

public interface ILockable
{
    bool IsLocked();
    void Unlock();
    void Lock();
}

public interface IRepairable
{
    bool NeedsRepair();
    void Repair(float amount);
    float GetDurability();
}