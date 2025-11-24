using UnityEngine;

/// <summary>
/// FIXED: Enhanced clothing save component that properly handles equipped items
/// Now saves ItemData references directly and recreates items during load instead of relying on inventory
/// </summary>
public class ClothingSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private ClothingManager clothingManager;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        base.Awake();
        saveID = "Clothing_Main";
        autoGenerateID = false;

        if (autoFindReferences)
        {
            FindClothingReferences();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Automatically locates clothing-related components.
    /// </summary>
    private void FindClothingReferences()
    {
        if (clothingManager == null)
            clothingManager = GetComponent<ClothingManager>() ??
                              ClothingManager.Instance ??
                              FindFirstObjectByType<ClothingManager>();

        //        DebugLog($"Auto-found clothing reference: {clothingManager != null}");
    }

    /// <summary>
    /// Validates that necessary references are available.
    /// </summary>
    private void ValidateReferences()
    {
        if (clothingManager == null)
        {
            Debug.LogError($"[{name}] ClothingManager reference missing! Clothing won't be saved/loaded.");
        }
        else
        {
            DebugLog($"ClothingManager reference validated: {clothingManager.name}");
        }
    }

    /// <summary>
    /// ENHANCED: Extracts complete clothing state including ItemData for equipped items
    /// </summary>
    public override object GetDataToSave()
    {
        if (clothingManager == null)
        {
            DebugLog("Cannot save clothing - ClothingManager not found");
            return new ClothingSaveData();
        }

        var saveData = ExtractClothingDataFromManager();
        DebugLog($"Extracted clothing data: {saveData.GetEquippedCount()} equipped items");
        return saveData;
    }

    /// <summary>
    /// ENHANCED: Extracts clothing data including ItemData references for equipped items
    /// </summary>
    private ClothingSaveData ExtractClothingDataFromManager()
    {
        var saveData = new ClothingSaveData();
        var allSlots = clothingManager.GetAllSlots();

        foreach (var slot in allSlots)
        {
            var slotSaveData = new ClothingSlotSaveData
            {
                layer = slot.layer,
                equippedItemId = slot.equippedItemId
            };

            // ENHANCED: Store ItemData reference for equipped items
            if (!slot.IsEmpty && slot.GetEquippedItemData() != null)
            {
                slotSaveData.equippedItemDataName = slot.GetEquippedItemData().name;
                DebugLog($"Saving equipped item: {slot.equippedItemId} ({slotSaveData.equippedItemDataName}) in {slot.layer}");
            }

            saveData.AddSlot(slotSaveData);
        }

        return saveData;
    }

    /// <summary>
    /// Extracts clothing data from various save container formats.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("Extracting clothing save data for persistence");

        if (saveContainer == null)
        {
            DebugLog("ExtractRelevantData: saveContainer is null");
            return new ClothingSaveData();
        }

        // Check PlayerPersistentData first (where rebuilt data is stored)
        if (saveContainer is PlayerPersistentData persistentData)
        {
            var clothingData = persistentData.GetComponentData<ClothingSaveData>(SaveID);
            if (clothingData != null)
            {
                DebugLog($"Extracted clothing from persistent data: {clothingData.GetEquippedCount()} equipped items");
                return clothingData;
            }
            else
            {
                DebugLog("No clothing data in persistent data - returning empty clothing");
                return new ClothingSaveData();
            }
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Check custom stats for clothing data
            if (playerSaveData.customStats.TryGetValue(SaveID, out object clothingDataObj) &&
                clothingDataObj is ClothingSaveData clothingData)
            {
                DebugLog($"Extracted clothing from PlayerSaveData by SaveID: {clothingData.GetEquippedCount()} equipped items");
                return clothingData;
            }

            DebugLog("No clothing data found in PlayerSaveData");
            return new ClothingSaveData();
        }
        else if (saveContainer is ClothingSaveData directClothingData)
        {
            DebugLog($"Extracted direct ClothingSaveData: {directClothingData.GetEquippedCount()} equipped items");
            return directClothingData;
        }

        DebugLog($"Invalid save data type - got {saveContainer?.GetType().Name ?? "null"}");
        return new ClothingSaveData();
    }

    /// <summary>
    /// ENHANCED: Restores clothing data with proper ItemData reconstruction
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (!(data is ClothingSaveData clothingData))
        {
            DebugLog($"Invalid save data type for clothing. Data type: {data?.GetType()}");
            return;
        }

        DebugLog($"=== RESTORING CLOTHING DATA (Context: {context}) ===");

        // Refresh references after scene load
        if (autoFindReferences)
        {
            FindClothingReferences();
        }

        if (clothingManager == null)
        {
            DebugLog("Cannot load clothing - ClothingManager not found");
            return;
        }

        DebugLog($"Loading clothing: {clothingData.GetEquippedCount()} equipped items");

        try
        {
            RestoreClothingDataToManager(clothingData);
            DebugLog("Clothing restored successfully to manager");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load clothing: {e.Message}");
        }
    }

    /// <summary>
    /// ENHANCED: Restores clothing state by recreating ItemData for equipped items
    /// </summary>
    private void RestoreClothingDataToManager(ClothingSaveData saveData)
    {
        if (saveData == null || !saveData.IsValid())
        {
            DebugLog("Invalid clothing save data - clearing clothing");
            clothingManager.ClearAllClothing();
            return;
        }

        // Get current slots and clear them
        var currentSlots = clothingManager.GetAllSlots();

        // Clear all current equipment
        foreach (var slot in currentSlots)
        {
            slot.UnequipItem();
        }

        // ENHANCED: Restore equipment by recreating ItemData
        foreach (var slotSaveData in saveData.slots)
        {
            var slot = clothingManager.GetSlot(slotSaveData.layer);
            if (slot != null && !string.IsNullOrEmpty(slotSaveData.equippedItemId))
            {
                // ENHANCED: Try to load ItemData from save data
                ItemData itemData = null;

                if (!string.IsNullOrEmpty(slotSaveData.equippedItemDataName))
                {
                    itemData = LoadItemDataByName(slotSaveData.equippedItemDataName);
                }

                if (itemData != null)
                {
                    // Equip the item with the ItemData reference
                    slot.EquipItem(slotSaveData.equippedItemId, itemData);
                    DebugLog($"Restored {slotSaveData.equippedItemId} ({itemData.itemName}) to {slotSaveData.layer}");
                }
                else
                {
                    DebugLog($"Warning: Could not load ItemData for {slotSaveData.equippedItemId} - ItemData name: {slotSaveData.equippedItemDataName}");
                }
            }
        }

        DebugLog($"Restored clothing: {saveData.GetEquippedCount()} items processed");
    }

    /// <summary>
    /// ENHANCED: Load ItemData by name from Resources
    /// </summary>
    private ItemData LoadItemDataByName(string itemDataName)
    {
        if (string.IsNullOrEmpty(itemDataName))
            return null;

        // Try to load from Resources using the standard path
        string resourcePath = $"{SaveManager.Instance?.itemDataPath ?? "Data/Items/"}{itemDataName}";
        ItemData itemData = Resources.Load<ItemData>(resourcePath);

        if (itemData != null)
        {
            DebugLog($"Loaded ItemData from Resources: {resourcePath}");
            return itemData;
        }

        // Fallback: Search all ItemData assets
        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        foreach (var data in allItemData)
        {
            if (data.name == itemDataName)
            {
                DebugLog($"Found ItemData via search: {itemDataName}");
                return data;
            }
        }

        Debug.LogWarning($"Could not find ItemData: {itemDataName}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts clothing data from unified save structure for modular loading.
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data");

        var clothingData = unifiedData.GetComponentData<ClothingSaveData>(SaveID);
        if (clothingData != null)
        {
            DebugLog($"Extracted clothing from dynamic storage: {clothingData.GetEquippedCount()} equipped items");
            return clothingData;
        }

        DebugLog("No clothing data found in unified save - returning empty clothing");
        return new ClothingSaveData();
    }

    /// <summary>
    /// Creates default empty clothing for new games.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default clothing data for new game");

        var defaultData = new ClothingSaveData();

        // Initialize with empty slots for all layers
        foreach (ClothingLayer layer in System.Enum.GetValues(typeof(ClothingLayer)))
        {
            defaultData.AddSlot(new ClothingSlotSaveData
            {
                layer = layer,
                equippedItemId = "",
                equippedItemDataName = ""
            });
        }

        DebugLog($"Default clothing data created: {defaultData.slots.Count} empty slots");
        return defaultData;
    }

    /// <summary>
    /// Contributes clothing data to unified save structure for save file creation.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is ClothingSaveData clothingData && unifiedData != null)
        {
            DebugLog($"Contributing clothing data to unified save: {clothingData.GetEquippedCount()} equipped items");

            unifiedData.SetComponentData(SaveID, clothingData);

            DebugLog($"Clothing data contributed: {clothingData.GetEquippedCount()} items stored");
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected ClothingSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Called before save operations to ensure current references.
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing clothing for save");

        if (autoFindReferences)
        {
            FindClothingReferences();
        }
    }

    /// <summary>
    /// Called after load operations. Clothing UI updates automatically via events.
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Clothing load completed");
    }
}