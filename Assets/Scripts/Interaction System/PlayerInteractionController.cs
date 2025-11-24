using UnityEngine;

/// <summary>
/// Handles player interactions - integrates with existing PlayerController system
/// UPDATED: Added input cooldown and UI-aware interaction blocking
/// </summary>
[RequireComponent(typeof(PlayerInteractionDetector))]
public class PlayerInteractionController : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private bool canInteract = true;
    [SerializeField] private float interactionCooldown = 0.15f; // Cooldown after each interaction

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Components
    private PlayerController playerController;
    private PlayerInteractionDetector interactionDetector;

    // Current state
    private bool isInteracting = false;
    private float lastInteractionTime = -999f; // Time of last successful interaction

    // Events
    public System.Action<IInteractable> OnInteractionStarted;
    public System.Action<IInteractable, bool> OnInteractionCompleted;

    public bool CanInteract => canInteract && !isInteracting;
    public bool IsInteracting => isInteracting;
    public IInteractable CurrentInteractable => interactionDetector?.CurrentBestInteractable;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        interactionDetector = GetComponent<PlayerInteractionDetector>();
    }

    private void Start()
    {
        // Subscribe to events
        GameManager.OnManagersRefreshed += RefreshReferences;
        InputManager.OnInputManagerReady += OnInputManagerReady;

        RefreshReferences();
    }

    private void RefreshReferences()
    {
        ConnectToInputManager();
    }

    private void OnInputManagerReady(InputManager newInputManager)
    {
        ConnectToInputManager();
    }

    private void ConnectToInputManager()
    {
        InputManager.Instance.OnInteractPressed -= HandleInteractInput;

        // Connect to current input manager
        InputManager.Instance.OnInteractPressed += HandleInteractInput;
    }

    private void HandleInteractInput()
    {
        if (!CanInteract)
        {
            DebugLog("Interaction input received but interaction is disabled");
            return;
        }

        // Check if we're paused
        if (GameManager.Instance != null && GameManager.Instance.isPaused)
        {
            DebugLog("Interaction blocked - game is paused");
            return;
        }

        // NEW: Check if interaction is on cooldown
        if (Time.time < lastInteractionTime + interactionCooldown)
        {
            DebugLog($"Interaction blocked - cooldown active ({Time.time - lastInteractionTime:F2}s / {interactionCooldown}s)");
            return;
        }

        // NEW: Check if any UI is currently open
        if (IsAnyInteractionUIOpen())
        {
            DebugLog("Interaction blocked - UI is open (UI will handle the input)");
            return;
        }

        DebugLog("Interaction input received - attempting interaction");
        TryInteract();
    }

    /// <summary>
    /// NEW: Check if any interaction-related UI is currently open
    /// </summary>
    private bool IsAnyInteractionUIOpen()
    {
        // Check storage container UI
        if (StorageContainerUI.Instance != null && StorageContainerUI.Instance.IsOpen)
        {
            DebugLog("Storage container UI is open");
            return true;
        }

        // Check pickup overflow UI
        if (PickupOverflowUI.Instance != null && PickupOverflowUI.Instance.IsOpen)
        {
            DebugLog("Pickup overflow UI is open");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempt to interact with the current best interactable
    /// </summary>
    public bool TryInteract()
    {
        if (!CanInteract || interactionDetector == null)
        {
            DebugLog("Cannot interact - disabled or no detector");
            return false;
        }

        var targetInteractable = interactionDetector.CurrentBestInteractable;
        if (targetInteractable == null)
        {
            DebugLog("No interactable in range");
            return false;
        }

        DebugLog($"Attempting interaction with: {targetInteractable.InteractableID}");

        // Start interaction
        isInteracting = true;
        OnInteractionStarted?.Invoke(targetInteractable);

        // Perform the interaction
        bool success = interactionDetector.TryInteract();

        // Complete interaction
        isInteracting = false;
        OnInteractionCompleted?.Invoke(targetInteractable, success);

        // NEW: Set cooldown timer if successful
        if (success)
        {
            lastInteractionTime = Time.time;
            DebugLog($"Interaction succeeded - cooldown started ({interactionCooldown}s)");
        }

        return success;
    }

    /// <summary>
    /// Enable or disable interaction capability
    /// </summary>
    public void SetInteractionEnabled(bool enabled)
    {
        canInteract = enabled;
        DebugLog($"Interaction {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Get the current interaction prompt text
    /// </summary>
    public string GetCurrentInteractionPrompt()
    {
        if (!CanInteract || interactionDetector == null)
            return "";

        return interactionDetector.GetCurrentInteractionPrompt();
    }

    /// <summary>
    /// Check if there's an interactable in range
    /// </summary>
    public bool HasInteractableInRange()
    {
        return interactionDetector != null && interactionDetector.HasInteractableInRange;
    }

    /// <summary>
    /// Force refresh the interaction detection
    /// </summary>
    public void RefreshInteractionDetection()
    {
        interactionDetector?.ForceUpdate();
    }

    /// <summary>
    /// NEW: Reset the interaction cooldown (useful for special cases)
    /// </summary>
    public void ResetInteractionCooldown()
    {
        lastInteractionTime = -999f;
        DebugLog("Interaction cooldown reset");
    }

    /// <summary>
    /// NEW: Trigger the interaction cooldown (prevents interaction for cooldown duration)
    /// </summary>
    public void TriggerInteractionCooldown()
    {
        lastInteractionTime = Time.time;
        DebugLog($"Interaction cooldown triggered manually ({interactionCooldown}s)");
    }

    /// <summary>
    /// NEW: Check if interaction is currently on cooldown
    /// </summary>
    public bool IsOnCooldown()
    {
        return Time.time < lastInteractionTime + interactionCooldown;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerInteractionController] {message}");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        GameManager.OnManagersRefreshed -= RefreshReferences;
        InputManager.OnInputManagerReady -= OnInputManagerReady;

        InputManager.Instance.OnInteractPressed -= HandleInteractInput;
    }
}