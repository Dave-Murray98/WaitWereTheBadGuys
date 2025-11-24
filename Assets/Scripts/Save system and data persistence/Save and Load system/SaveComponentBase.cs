using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Base class providing common functionality for all save components.
/// Handles ID generation, debug logging, and enforces consistent patterns.
/// </summary>
public abstract class SaveComponentBase : MonoBehaviour, ISaveable
{
    [Header("Save Component Settings")]
    [SerializeField] protected string saveID;
    [SerializeField] protected bool autoGenerateID = true;
    [SerializeField] protected bool enableDebugLogs = false;

    public virtual string SaveID
    {
        get
        {
            if (autoGenerateID && string.IsNullOrEmpty(saveID))
            {
                saveID = GenerateUniqueID();
            }
            return saveID;
        }
    }

    [ShowInInspector] public virtual SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    // Abstract methods that derived classes must implement
    public abstract object GetDataToSave();
    public abstract void LoadSaveDataWithContext(object data, RestoreContext context);

    public virtual void OnBeforeSave()
    {
        DebugLog("Preparing to save");
    }

    public virtual void OnAfterLoad()
    {
        DebugLog("Finished loading");
    }

    /// <summary>
    /// Generates a unique identifier based on object type, hierarchy position, and world position.
    /// Override for custom ID generation strategies.
    /// </summary>
    protected virtual string GenerateUniqueID()
    {
        string typeName = GetType().Name;
        string position = transform.position.ToString("F2");
        string sceneId = GetComponentInParent<Transform>()?.GetSiblingIndex().ToString() ?? "0";
        return $"{typeName}_{sceneId}_{position}";
    }

    protected void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[{GetType().Name}:{SaveID}] {message}");
        }
    }

    protected virtual void Awake()
    {
        // Components auto-register with save system if available
        if (SaveManager.Instance != null)
        {
            // Registration handled by persistence managers
        }
    }

    protected virtual void OnValidate()
    {
        // Auto-generate ID in editor for immediate feedback
        if (autoGenerateID && string.IsNullOrEmpty(saveID))
        {
            saveID = GenerateUniqueID();
        }
    }

    /// <summary>
    /// Default implementation returns the entire save container.
    /// Override to filter data for this specific component.
    /// </summary>
    public virtual object ExtractRelevantData(object saveContainer)
    {
        return saveContainer;
    }
}