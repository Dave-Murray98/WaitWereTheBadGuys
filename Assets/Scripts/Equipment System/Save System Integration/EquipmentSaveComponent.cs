using UnityEngine;
using System.Collections;

/// <summary>
/// FIXED: EquipmentSaveComponent with proper save/load restoration order
/// KEY FIXES:
/// - Ensures visual objects are created before equipment state is restored
/// - Properly restores the active slot and equipped item state
/// - Enhanced UI refresh timing after load operations
/// </summary>
public class EquipmentSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private EquippedItemManager equippedItemManager;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    [Header("FIXED: Restoration Settings")]
    [SerializeField] private float visualCreationDelay = 0.1f;
    [SerializeField] private float stateRestorationDelay = 0.2f;
    [SerializeField] private float uiRefreshDelay = 0.3f;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        base.Awake();

        // Fixed ID for equipment
        saveID = "Equipment_Main";
        autoGenerateID = false;

        // Auto-find references if enabled
        if (autoFindReferences)
        {
            FindEquipmentReferences();
        }
    }

    private void Start()
    {
        // Ensure we have equipment reference
        ValidateReferences();
    }

    /// <summary>
    /// Automatically find equipment-related components
    /// </summary>
    private void FindEquipmentReferences()
    {
        // Try to find on same GameObject first
        if (equippedItemManager == null)
            equippedItemManager = GetComponent<EquippedItemManager>();

        // If not found on same GameObject, get from Instance
        if (equippedItemManager == null)
            equippedItemManager = EquippedItemManager.Instance;

        // If still not found, search scene
        if (equippedItemManager == null)
            equippedItemManager = FindFirstObjectByType<EquippedItemManager>();

        DebugLog($"Auto-found equipment reference: {equippedItemManager != null}");
    }

    /// <summary>
    /// Validate that we have necessary references
    /// </summary>
    private void ValidateReferences()
    {
        if (equippedItemManager == null)
        {
            Debug.LogError($"[{name}] EquippedItemManager reference is missing! Equipment won't be saved/loaded.");
        }
        else
        {
            DebugLog($"EquippedItemManager reference validated: {equippedItemManager.name}");
        }
    }

    /// <summary>
    /// EXTRACT equipment data from EquippedItemManager
    /// </summary>
    public override object GetDataToSave()
    {
        DebugLog("=== EXTRACTING EQUIPMENT DATA FOR SAVE ===");

        if (equippedItemManager == null)
        {
            DebugLog("Cannot save equipment - EquippedItemManager not found");
            return new EquipmentSaveData(); // Return empty but valid data
        }

        // Extract data from the manager
        var saveData = ExtractEquipmentDataFromManager();

        var assignedCount = saveData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
        DebugLog($"Extracted equipment data: equipped={saveData.hasEquippedItem}, hotkeys={assignedCount}, activeSlot={saveData.currentActiveSlot}");
        return saveData;
    }

    /// <summary>
    /// Extract equipment data from the manager
    /// </summary>
    private EquipmentSaveData ExtractEquipmentDataFromManager()
    {
        // Use the helper method to get data directly (this will update current state before copying)
        return equippedItemManager.GetEquipmentDataDirect();
    }

    /// <summary>
    /// FIXED: Extract equipment data from unified save structure
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("EquipmentSaveComponent: Extracting equipment save data for persistence");

        if (saveContainer == null)
        {
            DebugLog("ExtractRelevantData: saveContainer is null");
            return new EquipmentSaveData();
        }

        // Check PlayerPersistentData FIRST since that's where the rebuilt data is stored
        if (saveContainer is PlayerPersistentData persistentData)
        {
            var equipmentData = persistentData.GetComponentData<EquipmentSaveData>(SaveID);
            if (equipmentData != null)
            {
                var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                DebugLog($"Extracted equipment from persistent data: {assignedCount} hotkey assignments, equipped: {equipmentData.hasEquippedItem}, activeSlot: {equipmentData.currentActiveSlot}");
                return equipmentData;
            }
            else
            {
                DebugLog("No equipment data in persistent data - returning empty equipment");
                return new EquipmentSaveData();
            }
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Check custom stats for equipment data
            if (playerSaveData.customStats.TryGetValue(SaveID, out object equipmentDataObj) &&
                equipmentDataObj is EquipmentSaveData equipmentSaveData)
            {
                var assignedCount = equipmentSaveData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                DebugLog($"Extracted equipment data from PlayerSaveData: {assignedCount} hotkey assignments, equipped: {equipmentSaveData.hasEquippedItem}, activeSlot: {equipmentSaveData.currentActiveSlot}");
                return equipmentSaveData;
            }

            DebugLog("No equipment data found in PlayerSaveData - returning empty equipment");
            return new EquipmentSaveData();
        }
        else if (saveContainer is EquipmentSaveData equipmentSaveData)
        {
            var assignedCount = equipmentSaveData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            DebugLog($"Extracted direct EquipmentSaveData: {assignedCount} hotkey assignments, equipped: {equipmentSaveData.hasEquippedItem}, activeSlot: {equipmentSaveData.currentActiveSlot}");
            return equipmentSaveData;
        }
        else
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData, EquipmentSaveData, or PlayerPersistentData, got {saveContainer.GetType()}");
            return new EquipmentSaveData();
        }
    }

    /// <summary>
    /// FIXED: Restore data with proper timing and coordination
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (!(data is EquipmentSaveData equipmentData))
        {
            DebugLog($"Invalid save data type for equipment. Data type: {data?.GetType()}");
            return;
        }

        DebugLog($"=== RESTORING EQUIPMENT DATA TO MANAGER (Context: {context}) ===");

        // Ensure we have current references (they might have changed after scene load)
        if (autoFindReferences)
        {
            FindEquipmentReferences();
        }

        if (equippedItemManager == null)
        {
            DebugLog("Cannot load equipment - EquippedItemManager not found");
            return;
        }

        // Debug what we're about to load
        var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
        DebugLog($"Loading equipment: {assignedCount} hotkey assignments, equipped: {equipmentData.hasEquippedItem}, activeSlot: {equipmentData.currentActiveSlot}");

        try
        {
            // FIXED: Use staged restoration for proper timing
            StartCoroutine(StagedEquipmentRestoration(equipmentData, context));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load equipment: {e.Message}");
        }
    }

    /// <summary>
    /// CRITICAL FIX: Staged restoration process with proper timing
    /// </summary>
    private System.Collections.IEnumerator StagedEquipmentRestoration(EquipmentSaveData saveData, RestoreContext context)
    {
        DebugLog("Starting staged equipment restoration...");

        // STAGE 1: Wait for other systems to be ready
        yield return new WaitForEndOfFrame();

        // Ensure inventory manager is available
        yield return new WaitUntil(() => PlayerInventoryManager.Instance != null);

        // STAGE 2: Create visual objects first
        DebugLog("Stage 1: Creating visual objects...");
        yield return new WaitForSecondsRealtime(visualCreationDelay);

        // Force refresh references again in case they changed
        if (autoFindReferences)
        {
            FindEquipmentReferences();
        }

        if (equippedItemManager == null)
        {
            Debug.LogError("EquippedItemManager lost during restoration!");
            yield break;
        }

        // STAGE 3: Restore equipment data to manager
        DebugLog("Stage 2: Restoring equipment data to manager...");
        equippedItemManager.SetEquipmentData(saveData);

        // STAGE 4: Wait for restoration to complete
        yield return new WaitForSecondsRealtime(stateRestorationDelay);

        // STAGE 5: Force UI refresh
        DebugLog("Stage 3: Refreshing UI systems...");
        yield return StartCoroutine(RefreshEquipmentUIAfterLoad());

        DebugLog("Staged equipment restoration completed successfully");
    }

    /// <summary>
    /// ENHANCED: Force refresh equipment UI after load with better timing
    /// </summary>
    private System.Collections.IEnumerator RefreshEquipmentUIAfterLoad()
    {
        // Wait for UI systems to be ready
        yield return new WaitForSecondsRealtime(uiRefreshDelay);

        if (equippedItemManager != null)
        {
            DebugLog("Forcing comprehensive equipment UI refresh after load");

            // Fire UI events for all slots
            var allBindings = equippedItemManager.GetAllHotkeyBindings();
            for (int i = 0; i < allBindings.Count; i++)
            {
                var binding = allBindings[i];
                if (binding.isAssigned)
                {
                    equippedItemManager.OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);
                }
                else
                {
                    equippedItemManager.OnHotkeyCleared?.Invoke(binding.slotNumber);
                }
            }

            // CRITICAL: Fire slot selection event for current active slot
            var currentSlot = equippedItemManager.GetCurrentActiveSlot();
            var currentBinding = equippedItemManager.GetHotkeyBinding(currentSlot);
            bool isUsable = equippedItemManager.IsCurrentEquipmentValid();

            DebugLog($"Firing slot selection event for slot {currentSlot}, usable: {isUsable}");
            equippedItemManager.OnSlotSelected?.Invoke(currentSlot, currentBinding, isUsable);

            // Force refresh equipped item UI
            if (equippedItemManager.HasEquippedItem)
            {
                DebugLog("Firing item equipped event for UI refresh");
                equippedItemManager.OnItemEquipped?.Invoke(equippedItemManager.CurrentEquippedItem);
            }
            else
            {
                DebugLog("Firing item unequipped event for UI refresh");
                equippedItemManager.OnItemUnequipped?.Invoke();
                equippedItemManager.OnUnarmedActivated?.Invoke();
            }

            DebugLog("Equipment UI refresh completed - all events fired");
        }
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extract equipment data from unified save structure
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data");

        var equipmentData = unifiedData.GetComponentData<EquipmentSaveData>(SaveID);
        if (equipmentData != null)
        {
            var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            DebugLog($"Extracted equipment from dynamic storage: {assignedCount} hotkey assignments, equipped: {equipmentData.hasEquippedItem}, activeSlot: {equipmentData.currentActiveSlot}");
            return equipmentData;
        }
        else
        {
            DebugLog("No equipment data in unified save - returning empty equipment");
            return new EquipmentSaveData();
        }
    }

    /// <summary>
    /// Create default equipment data for new games
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default equipment data for new game");

        var defaultData = new EquipmentSaveData();
        // Default constructor already sets up 10 empty hotkey slots, slot 1 active, and no equipped item

        DebugLog($"Default equipment data created: {defaultData.hotkeyBindings.Count} hotkey slots, active slot: {defaultData.currentActiveSlot}, no equipped item");
        return defaultData;
    }

    /// <summary>
    /// Contribute equipment data to unified save structure
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is EquipmentSaveData equipmentData && unifiedData != null)
        {
            var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            DebugLog($"Contributing equipment data to unified save: {assignedCount} hotkey assignments, equipped: {equipmentData.hasEquippedItem}, activeSlot: {equipmentData.currentActiveSlot}");

            // Store in dynamic storage
            unifiedData.SetComponentData(SaveID, equipmentData);

            DebugLog($"Equipment data contributed successfully");
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected EquipmentSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    #region Lifecycle and Utility Methods

    /// <summary>
    /// Called before save operations
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing equipment for save");

        // Refresh references in case they changed
        if (autoFindReferences)
        {
            FindEquipmentReferences();
        }
    }

    /// <summary>
    /// Called after load operations
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Equipment load completed - final validation");

        // Give a final moment for everything to settle
        StartCoroutine(FinalValidation());
    }

    /// <summary>
    /// FIXED: Final validation after all load operations complete
    /// </summary>
    private System.Collections.IEnumerator FinalValidation()
    {
        yield return new WaitForSecondsRealtime(0.5f);

        if (equippedItemManager != null)
        {
            // Force validate current equipment state
            equippedItemManager.ValidateEquipmentForCurrentState();

            // Log final state for debugging
            var currentSlot = equippedItemManager.GetCurrentActiveSlot();
            var hasEquipped = equippedItemManager.HasEquippedItem;
            var equippedItemName = equippedItemManager.GetEquippedItemData()?.itemName ?? "None";

            DebugLog($"FINAL VALIDATION - Active Slot: {currentSlot}, Has Equipped: {hasEquipped}, Item: {equippedItemName}");
        }
    }

    /// <summary>
    /// Public method to manually set equipment manager reference
    /// </summary>
    public void SetEquippedItemManager(EquippedItemManager manager)
    {
        equippedItemManager = manager;
        autoFindReferences = false; // Disable auto-find when manually set
        DebugLog("Equipment manager reference manually set");
    }

    /// <summary>
    /// Get current equipped item name (useful for other systems)
    /// </summary>
    public string GetCurrentEquippedItemName()
    {
        if (equippedItemManager?.HasEquippedItem == true)
        {
            return equippedItemManager.GetEquippedItemData()?.itemName ?? "Unknown";
        }
        return "None";
    }

    /// <summary>
    /// Get count of assigned hotkeys (useful for other systems)
    /// </summary>
    public int GetAssignedHotkeyCount()
    {
        if (equippedItemManager == null) return 0;

        var bindings = equippedItemManager.GetAllHotkeyBindings();
        return bindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
    }

    /// <summary>
    /// Check if equipment manager reference is valid
    /// </summary>
    public bool HasValidReference()
    {
        return equippedItemManager != null;
    }

    /// <summary>
    /// Force refresh of equipment manager reference
    /// </summary>
    public void RefreshReference()
    {
        if (autoFindReferences)
        {
            FindEquipmentReferences();
            ValidateReferences();
        }
    }

    /// <summary>
    /// Check if any item is currently equipped
    /// </summary>
    public bool HasEquippedItem()
    {
        return equippedItemManager?.HasEquippedItem == true;
    }

    /// <summary>
    /// Get equipped item type (useful for other systems)
    /// </summary>
    public ItemType? GetEquippedItemType()
    {
        if (!HasEquippedItem()) return null;

        return equippedItemManager.GetEquippedItemData()?.itemType;
    }

    /// <summary>
    /// Get debug information about current equipment state
    /// </summary>
    public string GetEquipmentDebugInfo()
    {
        if (equippedItemManager == null)
            return "EquippedItemManager: null";

        var equippedItemName = GetCurrentEquippedItemName();
        var hotkeyCount = GetAssignedHotkeyCount();
        var currentSlot = equippedItemManager.GetCurrentActiveSlot();

        return $"Equipment: {equippedItemName} equipped, {hotkeyCount}/10 hotkeys assigned, active slot: {currentSlot}";
    }

    /// <summary>
    /// Check if a specific hotkey slot is assigned
    /// </summary>
    public bool IsHotkeySlotAssigned(int slotNumber)
    {
        if (equippedItemManager == null) return false;

        var binding = equippedItemManager.GetHotkeyBinding(slotNumber);
        return binding?.isAssigned == true;
    }

    /// <summary>
    /// Get the item assigned to a specific hotkey slot
    /// </summary>
    public string GetHotkeySlotItemName(int slotNumber)
    {
        if (equippedItemManager == null) return null;

        var binding = equippedItemManager.GetHotkeyBinding(slotNumber);
        if (binding?.isAssigned == true)
        {
            return binding.GetCurrentItemData()?.itemName;
        }

        return null;
    }

    #endregion
}