using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Base class for all interactable objects
/// Provides common functionality and integrates with the save system
/// UPDATED: Now uses context-aware loading
/// </summary>
public abstract class InteractableBase : MonoBehaviour, IPersistentInteractable
{
    [Header("Interaction Settings")]
    [SerializeField] protected string interactableID;
    [SerializeField] protected bool autoGenerateID = true;
    [SerializeField] protected float interactionRange = 2f;
    [SerializeField] protected string interactionPrompt = "Press E to interact";
    [SerializeField] protected bool canInteract = true;
    [SerializeField] protected bool hasBeenUsed = false;

    [Header("Visual Feedback")]
    [SerializeField] protected bool showInteractionRange = false;
    [SerializeField] protected Color rangeColor = Color.yellow;

    [Header("Audio")]
    [SerializeField] protected AudioClip interactionSound;
    [SerializeField] protected AudioClip failureSound;

    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;

    // Events
    public System.Action<InteractionEventData> OnInteractionAttempted;
    public System.Action<GameObject> OnPlayerEnteredRange;
    public System.Action<GameObject> OnPlayerExitedRange;

    // ISaveable implementation
    public virtual string SaveID => interactableID;
    public virtual SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    // IInteractable implementation
    public virtual string InteractableID => interactableID;
    public virtual Transform Transform => transform;
    public virtual bool CanInteract => canInteract && gameObject.activeInHierarchy;
    public virtual float InteractionRange => interactionRange;
    public virtual bool HasBeenUsed => hasBeenUsed;

    protected virtual void Awake()
    {
        if (autoGenerateID && string.IsNullOrEmpty(interactableID))
        {
            GenerateUniqueID();
        }
    }

    protected virtual void Start()
    {
        // Register with save system if needed
        RegisterWithSaveSystem();
    }

    protected virtual void GenerateUniqueID()
    {
        // Generate unique ID based on object type, position, and scene
        string typeName = GetType().Name;
        string position = transform.position.ToString("F2");
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        interactableID = $"{typeName}_{sceneName}_{position}";
    }

    protected virtual void RegisterWithSaveSystem()
    {
        // Automatically register with save system if SaveManager exists
        if (SaveManager.Instance != null)
        {
            DebugLog("Registered with save system");
        }
    }

    #region IInteractable Implementation

    public virtual string GetInteractionPrompt()
    {
        if (!CanInteract)
            return "";

        return interactionPrompt;
    }

    public virtual bool Interact(GameObject player)
    {
        if (!CanInteract)
        {
            DebugLog("Interaction attempted but CanInteract is false");
            return false;
        }

        // Check distance
        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance > interactionRange)
        {
            DebugLog($"Player too far away: {distance:F2} > {interactionRange}");
            return false;
        }

        DebugLog($"Processing interaction from player: {player.name}");

        // Perform the actual interaction (implemented by derived classes)
        bool success = PerformInteraction(player);

        // Mark as used if successful
        if (success && !hasBeenUsed)
        {
            hasBeenUsed = true;
        }

        // Play audio feedback
        PlayInteractionAudio(success);

        // Fire events
        var eventData = new InteractionEventData(this, success);
        OnInteractionAttempted?.Invoke(eventData);

        return success;
    }

    public virtual void OnPlayerEnterRange(GameObject player)
    {
        DebugLog($"Player {player.name} entered interaction range");
        OnPlayerEnteredRange?.Invoke(player);
    }

    public virtual void OnPlayerExitRange(GameObject player)
    {
        DebugLog($"Player {player.name} exited interaction range");
        OnPlayerExitedRange?.Invoke(player);
    }

    #endregion

    #region Abstract Methods

    /// <summary>
    /// Implement the specific interaction behavior in derived classes
    /// </summary>
    /// <param name="player">The player performing the interaction</param>
    /// <returns>True if interaction was successful</returns>
    protected abstract bool PerformInteraction(GameObject player);

    #endregion

    #region ISaveable Implementation

    public virtual object GetDataToSave()
    {
        return new InteractableSaveData
        {
            interactableID = this.interactableID,
            hasBeenUsed = this.hasBeenUsed,
            canInteract = this.canInteract,
            customData = GetCustomSaveData()
        };
    }

    public virtual object ExtractRelevantData(object saveContainer)
    {
        if (saveContainer is SceneSaveData sceneData)
        {
            return sceneData.GetObjectData<InteractableSaveData>(SaveID);
        }
        return saveContainer;
    }

    /// <summary>
    /// UPDATED: Now uses context-aware loading
    /// Context doesn't matter much for interactables - they behave the same regardless
    /// </summary>
    public virtual void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is InteractableSaveData saveData)
        {
            DebugLog($"Loading save data (Context: {context}) - old state: hasBeenUsed={hasBeenUsed}, canInteract={canInteract}");

            hasBeenUsed = saveData.hasBeenUsed;
            canInteract = saveData.canInteract;
            LoadCustomSaveData(saveData.customData);

            DebugLog($"Loaded save data - new state: hasBeenUsed={hasBeenUsed}, canInteract={canInteract}");
        }
        else
        {
            DebugLog($"LoadSaveDataWithContext called with invalid data type: {data?.GetType()}");
        }
    }

    public virtual void OnBeforeSave()
    {
        DebugLog("Preparing for save");
    }

    public virtual void OnAfterLoad()
    {
        DebugLog("Save data loaded");
        // Refresh visual state after loading
        RefreshVisualState();
    }

    #endregion

    #region Virtual Methods for Derived Classes

    /// <summary>
    /// Override to provide custom save data specific to derived classes
    /// </summary>
    protected virtual object GetCustomSaveData()
    {
        return null;
    }

    /// <summary>
    /// Override to load custom save data specific to derived classes
    /// </summary>
    protected virtual void LoadCustomSaveData(object customData)
    {
        // Default implementation does nothing
    }

    /// <summary>
    /// Override to refresh visual state after loading
    /// </summary>
    protected virtual void RefreshVisualState()
    {
        // Default implementation does nothing
    }

    #endregion

    #region Utility Methods

    protected virtual void PlayInteractionAudio(bool success)
    {
        AudioClip clipToPlay = success ? interactionSound : failureSound;
        if (clipToPlay != null)
        {
            // You can integrate this with your AudioManager if you have one
            AudioSource.PlayClipAtPoint(clipToPlay, transform.position);
        }
    }

    protected void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[{GetType().Name}:{interactableID}] {message}");
        }
    }

    public virtual void SetInteractable(bool interactable)
    {
        canInteract = interactable;
    }

    public virtual void ResetInteractable()
    {
        hasBeenUsed = false;
        canInteract = true;
        RefreshVisualState();
    }

    #endregion

    #region Editor Helpers

    [Button("Generate New ID")]
    private void RegenerateID()
    {
        GenerateUniqueID();
    }

    [Button("Test Interaction")]
    private void TestInteraction()
    {
        if (Application.isPlaying)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                Interact(player.gameObject);
            }
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (showInteractionRange)
        {
            Gizmos.color = rangeColor;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }

    #endregion
}

/// <summary>
/// Save data structure for interactable objects
/// </summary>
[System.Serializable]
public class InteractableSaveData
{
    public string interactableID;
    public bool hasBeenUsed;
    public bool canInteract;
    public object customData; // For derived classes to store additional data
}