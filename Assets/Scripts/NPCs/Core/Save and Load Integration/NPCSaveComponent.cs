using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// CLEANED: Simplified save component for NPCs.
/// Handles position and rotation persistence with proper state machine coordination.
/// </summary>
public class NPCSaveComponent : SaveComponentBase
{
    [Header("Component References")]
    [SerializeField] private NPCController npcController;
    [SerializeField] private Rigidbody rb;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    [Header("Restoration Settings")]
    [SerializeField] private float physicsSettleDelay = 0.1f;
    [SerializeField] private float movementReenableDelay = 0.2f;

    // Identity managed by SceneNPCStateManager
    private string assignedNPCID = "";

    public override SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    protected override void Awake()
    {
        // Don't auto-generate ID - SceneNPCStateManager will assign consistent IDs
        autoGenerateID = false;
        base.Awake();

        if (autoFindReferences)
        {
            FindNPCReferences();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    #region Reference Management

    private void FindNPCReferences()
    {
        if (npcController == null)
            npcController = GetComponent<NPCController>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        DebugLog($"Auto-found references - NPCController: {npcController != null}, Rigidbody: {rb != null}");
    }

    private void ValidateReferences()
    {
        if (npcController == null)
            Debug.LogError($"[{name}] NPCController reference missing! NPC won't be saved correctly.");

        if (rb == null)
            Debug.LogWarning($"[{name}] Rigidbody reference missing! Physics cleanup won't work.");
    }

    #endregion

    #region ID Management

    public void SetNPCID(string id)
    {
        assignedNPCID = id;
        saveID = id;
        DebugLog($"NPC ID assigned: {id}");
    }

    public string GetNPCID()
    {
        return assignedNPCID;
    }

    #endregion

    #region ISaveable Implementation

    public override object GetDataToSave()
    {
        var saveData = new NPCSaveData();

        if (npcController != null)
        {
            saveData.UpdateFromTransform(transform, assignedNPCID);
            DebugLog($"Save data collected - Position: {saveData.position}, Rotation: {saveData.rotation.eulerAngles}");
        }
        else
        {
            DebugLog("NPCController not found - using transform directly");
            saveData.UpdateFromTransform(transform, assignedNPCID);
        }

        return saveData;
    }

    public override object ExtractRelevantData(object saveContainer)
    {
        if (saveContainer is NPCSaveData npcData)
        {
            DebugLog($"Extracted NPC save data for {npcData.npcID}");
            return npcData;
        }

        DebugLog($"Invalid save data type - got {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    /// <summary>
    /// CLEANED: Simplified restoration with proper state machine coordination
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not NPCSaveData npcData)
        {
            DebugLog($"Invalid save data type - expected NPCSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== NPC DATA RESTORATION (Context: {context}) ===");
        DebugLog($"Received data: {npcData}");

        // Refresh references
        if (autoFindReferences)
        {
            FindNPCReferences();
        }

        // Handle restoration based on context
        switch (context)
        {
            case RestoreContext.SaveFileLoad:
            case RestoreContext.DoorwayTransition:
                StartCoroutine(RestorePositionAndState(npcData));
                break;

            case RestoreContext.NewGame:
                // Keep default spawn position for new games
                DebugLog("New game - using default spawn position");
                break;
        }
    }

    #endregion

    #region State Restoration - CLEANED

    /// <summary>
    /// CLEANED: Simple, clear restoration process
    /// 1. Disable movement
    /// 2. Apply position
    /// 3. Re-enable movement
    /// </summary>
    private System.Collections.IEnumerator RestorePositionAndState(NPCSaveData npcData)
    {
        if (npcData == null || npcController == null)
        {
            DebugLog("Cannot restore - missing data or controller");
            yield break;
        }

        DebugLog($"Starting restoration - Position: {npcData.position}, Rotation: {npcData.rotation.eulerAngles}");

        // STEP 1: Tell state machine to disable movement
        if (npcController.movementStateMachine != null)
        {
            npcController.PrepareForPositionRestoration();
            DebugLog("State machine prepared for restoration");
        }

        // STEP 2: Wait for systems to disable
        yield return new WaitForEndOfFrame();

        // STEP 3: Clear physics state
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            DebugLog("Cleared physics velocity");
        }

        // STEP 4: Apply saved position and rotation
        rb.Move(npcData.position, npcData.rotation);

        // if (rb != null)
        // {
        //     rb.position = npcData.position;
        //     rb.rotation = npcData.rotation;
        // }

        DebugLog($"Applied position: {transform.position}");

        // STEP 5: Wait for physics to settle at new position
        yield return new WaitForSeconds(physicsSettleDelay);

        // STEP 6: Additional stability wait
        yield return new WaitForSeconds(movementReenableDelay);

        // STEP 7: Tell state machine restoration is complete
        if (npcController.movementStateMachine != null)
        {
            npcController.CompletePositionRestoration();
            DebugLog("✅ State machine restoration complete");
        }

        DebugLog($"✅ Restoration finished - Final position: {transform.position}");
    }

    #endregion

    #region Lifecycle Callbacks

    public override void OnBeforeSave()
    {
        DebugLog("Preparing NPC data for save");

        if (autoFindReferences)
        {
            FindNPCReferences();
        }
    }

    public override void OnAfterLoad()
    {
        DebugLog("NPC data load completed");
    }

    #endregion

    #region Debug and Utility

    [Button("Show Debug Info"), DisableInEditorMode]
    public string GetDebugInfo()
    {
        return $"NPC Save Component Debug Info:\n" +
               $"Assigned ID: {assignedNPCID}\n" +
               $"Save ID: {SaveID}\n" +
               $"Position: {transform.position}\n" +
               $"Rotation: {transform.rotation.eulerAngles}\n" +
               $"Has NPCController: {npcController != null}\n" +
               $"Has Rigidbody: {rb != null}\n" +
               $"Save Category: {SaveCategory}";
    }

    #endregion

    protected override void OnValidate()
    {
        // Auto-find references in editor
        if (autoFindReferences && Application.isPlaying == false)
        {
            if (npcController == null)
                npcController = GetComponent<NPCController>();
            if (rb == null)
                rb = GetComponent<Rigidbody>();
        }
    }
}