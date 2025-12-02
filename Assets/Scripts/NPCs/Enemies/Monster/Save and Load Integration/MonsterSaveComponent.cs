using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections;

/// <summary>
/// Save component for the underwater monster that integrates with the existing save system.
/// Handles persistence of monster health, position, and state across scene transitions and save files.
/// Designed for single monster scenarios - follows established SaveComponentBase patterns.
/// </summary>
public class MonsterSaveComponent : SaveComponentBase
{
    [Header("Component References")]
    [SerializeField] private UnderwaterMonsterController monsterController;
    [SerializeField] private MonsterHealth monsterHealth;
    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private Rigidbody rb;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    [Header("Restoration Settings")]
    [SerializeField] private float physicsSettleDelay = 0.1f;
    [SerializeField] private float stateRestorationDelay = 0.2f;

    // Monster is scene-dependent (doesn't follow player between scenes)
    public override SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    protected override void Awake()
    {
        // Use consistent ID for single monster
        saveID = "Monster_Main";
        autoGenerateID = false;
        base.Awake();

        if (autoFindReferences)
        {
            FindMonsterReferences();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    #region Reference Management

    /// <summary>
    /// Automatically finds monster component references
    /// </summary>
    private void FindMonsterReferences()
    {
        if (monsterController == null)
            monsterController = GetComponent<UnderwaterMonsterController>();

        if (monsterHealth == null)
            monsterHealth = GetComponent<MonsterHealth>();

        if (stateMachine == null)
            stateMachine = GetComponent<EnemyStateMachine>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        DebugLog($"Auto-found references - Controller: {monsterController != null}, " +
                $"Health: {monsterHealth != null}, StateMachine: {stateMachine != null}, Rigidbody: {rb != null}");
    }

    /// <summary>
    /// Validates that all required references are present
    /// </summary>
    private void ValidateReferences()
    {
        if (monsterController == null)
            Debug.LogError($"[{name}] UnderwaterMonsterController reference missing! Monster won't save properly.");

        if (monsterHealth == null)
            Debug.LogError($"[{name}] MonsterHealth reference missing! Health data won't be saved.");

        if (stateMachine == null)
            Debug.LogError($"[{name}] EnemyStateMachine reference missing! State data won't be saved.");

        if (rb == null)
            Debug.LogWarning($"[{name}] Rigidbody reference missing! Physics cleanup won't work during restoration.");
    }

    #endregion

    #region ISaveable Implementation

    /// <summary>
    /// Collects current monster state for saving
    /// </summary>
    public override object GetDataToSave()
    {
        var saveData = new MonsterSaveData();

        if (monsterController != null)
        {
            saveData.UpdateFromMonster(monsterController);
            DebugLog($"Collected monster data: {saveData}");
        }
        else
        {
            DebugLog("MonsterController not found - using default values");
            saveData.SetDefaults();
        }

        return saveData;
    }

    /// <summary>
    /// Extracts relevant monster data from save containers
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        if (saveContainer is MonsterSaveData monsterData)
        {
            DebugLog($"Extracted monster save data: {monsterData}");
            return monsterData;
        }

        DebugLog($"Invalid save data type - expected MonsterSaveData, got {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    /// <summary>
    /// Context-aware monster data restoration
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not MonsterSaveData monsterData)
        {
            DebugLog($"Invalid save data type - expected MonsterSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== MONSTER DATA RESTORATION (Context: {context}) ===");
        DebugLog($"Received data: {monsterData.GetDebugInfo()}");

        // Refresh references in case they changed after scene load
        if (autoFindReferences)
        {
            FindMonsterReferences();
        }

        // Validate data integrity
        if (!monsterData.IsValid())
        {
            Debug.LogWarning($"[MonsterSaveComponent] Invalid monster data detected - using defaults");
            monsterData.SetDefaults();
        }

        // All contexts now restore complete monster state (position, health, state)
        StartCoroutine(RestoreCompleteMonsterState(monsterData, context));
    }

    #endregion

    #region Restoration Coroutines

    /// <summary>
    /// Restores complete monster state including position, health, and state
    /// </summary>
    private IEnumerator RestoreCompleteMonsterState(MonsterSaveData monsterData, RestoreContext context)
    {
        if (monsterController == null)
        {
            DebugLog("Cannot restore - monster controller missing");
            yield break;
        }

        DebugLog($"Starting monster state restoration: {monsterData}");

        // STEP 1: Prepare monster for restoration
        PrepareMonsterForRestoration();

        // STEP 2: Wait for systems to settle
        yield return new WaitForEndOfFrame();

        // STEP 3: Apply all saved data (position, health, state)
        monsterData.ApplyToMonster(monsterController, context);

        DebugLog($"Applied monster data - Position: {transform.position}, Health: {monsterHealth?.CurrentHealth ?? 0}");

        // STEP 4: Wait for physics to settle
        yield return new WaitForSeconds(physicsSettleDelay);

        // STEP 5: Complete state restoration
        yield return new WaitForSeconds(stateRestorationDelay);
        CompleteMonsterRestoration();

        DebugLog($"✅ Monster restoration finished - Final position: {transform.position}");
    }

    #endregion

    #region Restoration Helpers

    /// <summary>
    /// Prepares monster for state restoration
    /// </summary>
    private void PrepareMonsterForRestoration()
    {
        DebugLog("Preparing monster for restoration");

        // Stop movement if active
        if (monsterController?.movement != null)
        {
            monsterController.movement.DeactivateMovement();
        }

        // Clear physics state
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            DebugLog("Cleared monster physics state");
        }
    }

    /// <summary>
    /// Completes monster restoration process
    /// </summary>
    private void CompleteMonsterRestoration()
    {
        DebugLog("Completing monster restoration");

        // Reactivate movement if monster is alive
        if (monsterController?.movement != null && monsterHealth != null && monsterHealth.isAlive)
        {
            monsterController.movement.ActivateMovement();
            DebugLog("Reactivated monster movement");
        }
    }

    #endregion

    #region Lifecycle Callbacks

    public override void OnBeforeSave()
    {
        DebugLog("Preparing monster data for save");

        if (autoFindReferences)
        {
            FindMonsterReferences();
        }
    }

    public override void OnAfterLoad()
    {
        DebugLog("Monster data load completed");
    }

    #endregion

    #region Debug and Utility

    /// <summary>
    /// Returns debug information about current monster state
    /// </summary>
    [Button("Show Monster Debug Info"), DisableInEditorMode]
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Monster Save Component Debug Info ===");
        info.AppendLine($"Save ID: {SaveID}");
        info.AppendLine($"Save Category: {SaveCategory}");
        info.AppendLine();

        if (monsterController != null)
        {
            info.AppendLine("Monster Controller: ✓");
            info.AppendLine($"Position: {monsterController.transform.position}");
            info.AppendLine($"Rotation: {monsterController.transform.rotation.eulerAngles}");
        }
        else
        {
            info.AppendLine("Monster Controller: ✗");
        }

        if (monsterHealth != null)
        {
            info.AppendLine($"Health: {monsterHealth.CurrentHealth} (Alive: {monsterHealth.isAlive})");
        }
        else
        {
            info.AppendLine("Monster Health: ✗");
        }

        if (stateMachine != null && stateMachine.currentState != null)
        {
            info.AppendLine($"Current State: {stateMachine.currentState.name}");
        }
        else
        {
            info.AppendLine("State Machine: ✗");
        }

        return info.ToString();
    }

    /// <summary>
    /// Manual test of save data collection
    /// </summary>
    [Button("Test Save Data Collection"), DisableInEditorMode]
    public void TestSaveDataCollection()
    {
        var saveData = GetDataToSave() as MonsterSaveData;
        if (saveData != null)
        {
            Debug.Log($"[MonsterSaveComponent] Test Save Data:\n{saveData.GetDebugInfo()}");
        }
        else
        {
            Debug.LogError("[MonsterSaveComponent] Failed to collect save data!");
        }
    }

    /// <summary>
    /// Forces reference refresh for testing
    /// </summary>
    [Button("Refresh References"), DisableInEditorMode]
    public void ForceRefreshReferences()
    {
        FindMonsterReferences();
        ValidateReferences();
    }

    #endregion

    protected override void OnValidate()
    {
        base.OnValidate();

        // Auto-find references in editor
        if (autoFindReferences && Application.isPlaying == false)
        {
            if (monsterController == null)
                monsterController = GetComponent<UnderwaterMonsterController>();
            if (monsterHealth == null)
                monsterHealth = GetComponent<MonsterHealth>();
            if (stateMachine == null)
                stateMachine = GetComponent<EnemyStateMachine>();
            if (rb == null)
                rb = GetComponent<Rigidbody>();
        }
    }
}