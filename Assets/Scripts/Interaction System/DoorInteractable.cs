using UnityEngine;
using DG.Tweening;

/// <summary>
/// Enhanced door interactable that works with the existing Doorway system
/// Supports both immediate transitions and animated doors
/// </summary>
public class DoorInteractable : InteractableBase, IConditionalInteractable
{
    [Header("Door Settings")]
    [SerializeField] private Doorway doorwayComponent;
    [SerializeField] private bool requiresAnimation = false;
    [SerializeField] private bool isLocked = false;
    [SerializeField] private string requiredKeyID = "";

    [Header("Animation (if required)")]
    [SerializeField] private GameObject doorModel;
    [SerializeField] private Vector3 openRotation = new Vector3(0, 90, 0);
    [SerializeField] private float animationDuration = 1f;
    //[SerializeField] private Ease animationEase = Ease.OutQuad;
    [SerializeField] private bool closeAfterDelay = false;
    [SerializeField] private float autoCloseDelay = 3f;

    [Header("Door State")]
    [SerializeField] private bool isOpen = false;

    // Animation state
    private Vector3 closedRotation;
    private Tweener currentTween;
    private bool isAnimating = false;

    protected override void Awake()
    {
        base.Awake();

        // Get doorway component if not assigned
        if (doorwayComponent == null)
        {
            doorwayComponent = GetComponent<Doorway>();
        }

        // Store initial rotation as closed position
        if (doorModel != null)
        {
            closedRotation = doorModel.transform.localEulerAngles;
        }

        // Set default interaction prompt
        if (string.IsNullOrEmpty(interactionPrompt))
        {
            interactionPrompt = isLocked ? "Locked" : "open door";
        }
    }

    #region IInteractable Implementation

    public override bool CanInteract
    {
        get
        {
            return base.CanInteract && !isAnimating && doorwayComponent != null;
        }
    }

    public override string GetInteractionPrompt()
    {
        if (isLocked)
        {
            return "Locked";
        }

        if (requiresAnimation)
        {
            return isOpen ? "close door" : "open door";
        }

        return "enter door";
    }

    protected override bool PerformInteraction(GameObject player)
    {
        // Check requirements first
        if (!MeetsInteractionRequirements(player))
        {
            DebugLog($"Interaction requirements not met: {GetRequirementFailureMessage()}");
            return false;
        }

        if (requiresAnimation)
        {
            return HandleAnimatedDoor();
        }
        else
        {
            return HandleImmediateTransition();
        }
    }

    #endregion

    #region IConditionalInteractable Implementation

    public bool MeetsInteractionRequirements(GameObject player)
    {
        // Check if player is in vehicle, as players can't open doors while in a vehicle
        if (PlayerStateManager.Instance.CurrentStateType == PlayerStateType.Vehicle)
        {
            Debug.Log("Cannot open door while in vehicle");
            return false;
        }

        if (!isLocked)
            return true;

        // Check if player has the required key
        if (string.IsNullOrEmpty(requiredKeyID))
            return true;

        // You can integrate this with your inventory system
        var inventory = PlayerInventoryManager.Instance;
        if (inventory != null)
        {
            // Check if player has the key item
            // This is a simplified example - you'd need to implement key checking in your inventory system
            return HasKey(player, requiredKeyID);
        }

        return false;
    }

    public string GetRequirementFailureMessage()
    {
        if (isLocked)
        {
            return string.IsNullOrEmpty(requiredKeyID) ? "This door is locked" : $"Requires {requiredKeyID}";
        }

        return "";
    }

    #endregion

    private bool HandleAnimatedDoor()
    {
        if (isAnimating)
        {
            DebugLog("Door is already animating");
            return false;
        }

        //  DebugLog($"Animating door: {(isOpen ? "closing" : "opening")}");

        // Toggle door state
        isOpen = !isOpen;

        // Animate the door
        AnimateDoor();

        return true;
    }

    private bool HandleImmediateTransition()
    {
        if (doorwayComponent == null)
        {
            DebugLog("No doorway component found");
            return false;
        }

        //  DebugLog("Using doorway for immediate transition");
        doorwayComponent.UseDoorway();
        return true;
    }

    private void AnimateDoor()
    {
        if (doorModel == null)
        {
            DebugLog("No door model to animate");
            return;
        }

        isAnimating = true;

        // TODO: Add your door animation logic here
        // Examples:
        // - Rotate the door open/closed
        // - Move sliding doors
        // - Scale or fade doors
        // - Trigger animation controllers

        // For now, just simulate animation completion
        Invoke(nameof(CompleteAnimation), animationDuration);

        // DebugLog($"Door animation started - Door will be {(isOpen ? "open" : "closed")} in {animationDuration} seconds");
    }

    private void CompleteAnimation()
    {
        isAnimating = false;
        // DebugLog($"Door animation completed - Door is now {(isOpen ? "open" : "closed")}");

        // Auto-close if enabled and door is now open
        if (isOpen && closeAfterDelay)
        {
            Invoke(nameof(AutoCloseDoor), autoCloseDelay);
        }
    }

    private void AutoCloseDoor()
    {
        if (isOpen && !isAnimating)
        {
            DebugLog("Auto-closing door");
            isOpen = false;
            AnimateDoor();
        }
    }

    private bool HasKey(GameObject player, string keyID)
    {
        // This is a placeholder - you'll need to implement key checking in your inventory system
        // For now, let's assume the player always has the key

        // Example integration with your inventory:
        // var inventory = PersistentInventoryManager.Instance;
        // return inventory.HasItem(keyID);

        DebugLog($"Checking for key: {keyID} (placeholder implementation)");
        return true; // Placeholder - always allow access
    }

    #region Save/Load Implementation

    protected override object GetCustomSaveData()
    {
        return new DoorSaveData
        {
            isOpen = this.isOpen,
            isLocked = this.isLocked
        };
    }

    protected override void LoadCustomSaveData(object customData)
    {
        if (customData is DoorSaveData doorData)
        {
            isOpen = doorData.isOpen;
            isLocked = doorData.isLocked;
        }
    }

    protected override void RefreshVisualState()
    {
        // Update door visual state after loading
        if (requiresAnimation && doorModel != null)
        {
            // TODO: Set door model to correct visual state based on isOpen
            // Examples:
            // - Set rotation: doorModel.transform.localEulerAngles = isOpen ? openRotation : closedRotation;
            // - Set position for sliding doors
            // - Set animation state
        }

        // Update interaction prompt
        interactionPrompt = isLocked ? "Locked" : (requiresAnimation ? (isOpen ? "close door" : "open door") : "enter door");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Lock or unlock the door
    /// </summary>
    public void SetLocked(bool locked)
    {
        isLocked = locked;
        // DebugLog($"Door {(locked ? "locked" : "unlocked")}");
        RefreshVisualState();
    }

    /// <summary>
    /// Force the door to a specific open/closed state
    /// </summary>
    public void SetDoorState(bool open, bool animate = true)
    {
        if (isOpen == open) return;

        isOpen = open;

        if (requiresAnimation && animate)
        {
            AnimateDoor();
        }
        else
        {
            RefreshVisualState();
        }
    }

    /// <summary>
    /// Check if the door is currently open
    /// </summary>
    public bool IsOpen => isOpen;

    /// <summary>
    /// Check if the door is currently locked
    /// </summary>
    public bool IsLocked => isLocked;

    #endregion

    private void OnDestroy()
    {
        // Clean up any pending invokes
        CancelInvoke();
    }
}

/// <summary>
/// Save data for door state
/// </summary>
[System.Serializable]
public class DoorSaveData
{
    public bool isOpen;
    public bool isLocked;
}