using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine.Android;

/// <summary>
/// Enhanced visual manager that handles different equipment sockets based on item type.
/// Bows are equipped in the left hand, while other items use the right hand.
/// </summary>
public class EquippedItemVisualManager : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private Transform propSocketR; // Right hand socket for most items
    [SerializeField] private Transform propSocketL; // Left hand socket for bows

    [Header("Component References")]
    [SerializeField] private ADSSetup adsSetup; // For weapon ADS configuration
    [SerializeField] private RangedWeaponHandler weaponManager; // For weapon stat management

    [Header("Current State")]
    [SerializeField, ReadOnly] private GameObject currentActiveItem;
    [SerializeField, ReadOnly] private ItemData currentEquippedItemData;
    [SerializeField, ReadOnly] private int currentActiveSlotNumber = -1;
    [SerializeField, ReadOnly] private Transform currentActiveSocket; // Track which socket is being used

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Track all hotbar items by slot number with their socket information
    [ShowInInspector] private Dictionary<int, HotbarSlotInfo> hotbarSlotObjects = new Dictionary<int, HotbarSlotInfo>();

    #region Data Structures

    /// <summary>
    /// Contains information about a hotbar slot including the GameObject and which socket it uses
    /// </summary>
    private class HotbarSlotInfo
    {
        public GameObject gameObject;
        public Transform socket;
        public ItemData itemData;

        public HotbarSlotInfo(GameObject obj, Transform socketTransform, ItemData data)
        {
            gameObject = obj;
            socket = socketTransform;
            itemData = data;
        }
    }

    #endregion

    #region Events

    public System.Action<GameObject, ItemData, int, Transform> OnItemVisualEquipped; // GameObject, ItemData, SlotNumber, Socket
    public System.Action<int> OnItemVisualUnequipped; // SlotNumber
    public System.Action<Dictionary<int, ItemData>> OnHotbarVisualsPopulated;

    #endregion

    #region Initialization

    private void Awake()
    {
        ValidateReferences();
    }

    private void Start()
    {
        FindReferences();
    }

    /// <summary>
    /// Validate that required references are assigned
    /// </summary>
    private void ValidateReferences()
    {
        if (propSocketR == null)
        {
            Debug.LogError("[EquippedItemVisualManager] PropSocketR (right hand) is not assigned!");
        }

        if (propSocketL == null)
        {
            Debug.LogError("[EquippedItemVisualManager] PropSocketL (left hand) is not assigned!");
        }
    }

    /// <summary>
    /// Auto-find references if not manually assigned
    /// </summary>
    private void FindReferences()
    {
        // Try to find ADS setup if not assigned
        if (adsSetup == null)
        {
            adsSetup = GetComponent<ADSSetup>();
            if (adsSetup != null)
            {
                DebugLog("Auto-found ADSSetup component");
            }
        }

        // Try to find weapon manager if not assigned
        if (weaponManager == null)
        {
            weaponManager = GetComponent<RangedWeaponHandler>();
            if (weaponManager != null)
            {
                DebugLog("Auto-found WeaponManager component");
            }
        }
    }

    #endregion

    #region Socket Selection Logic

    /// <summary>
    /// Determines which socket to use based on item type
    /// </summary>
    /// <param name="itemData">The item to determine socket for</param>
    /// <returns>The appropriate socket transform</returns>
    private Transform GetSocketForItem(ItemData itemData)
    {
        if (itemData == null)
        {
            DebugLog("ItemData is null, defaulting to right hand socket");
            return propSocketR;
        }

        // Bows go in the left hand
        if (itemData.itemType == ItemType.Bow)
        {
            if (propSocketL == null)
            {
                Debug.LogWarning($"[EquippedItemVisualManager] Left hand socket not assigned, but trying to equip bow {itemData.itemName}. Using right hand instead.");
                return propSocketR;
            }

            DebugLog($"Using left hand socket for bow: {itemData.itemName}");
            return propSocketL;
        }

        // All other items go in the right hand
        DebugLog($"Using right hand socket for {itemData.itemType}: {itemData.itemName}");
        return propSocketR;
    }

    /// <summary>
    /// Check if an item should use the left hand socket
    /// </summary>
    /// <param name="itemData">The item to check</param>
    /// <returns>True if item should use left hand</returns>
    private bool ShouldUseLeftHand(ItemData itemData)
    {
        return itemData != null && itemData.itemType == ItemType.Bow;
    }

    #endregion

    #region Hotbar Population System

    /// <summary>
    /// Populates all hotbar slots with their visual objects using appropriate sockets
    /// </summary>
    public void PopulateHotbarEquippedItemPrefabs(Dictionary<int, ItemData> hotbarItems)
    {
        DebugLog("=== POPULATING HOTBAR VISUALS WITH SOCKET SELECTION ===");

        // Clear existing hotbar objects
        ClearAllHotbarObjects();

        // Create objects for each hotbar item in the appropriate socket
        foreach (var kvp in hotbarItems)
        {
            int slotNumber = kvp.Key;
            ItemData itemData = kvp.Value;

            if (itemData?.equippedItemPrefab != null)
            {
                CreateHotbarSlotObject(slotNumber, itemData);
            }
            else
            {
                DebugLog($"Slot {slotNumber} has no equipped prefab: {itemData?.itemName ?? "null"}");
            }
        }

        DebugLog($"Populated {hotbarSlotObjects.Count} hotbar visual objects with appropriate socket assignments");
        OnHotbarVisualsPopulated?.Invoke(hotbarItems);
    }

    /// <summary>
    /// Creates and configures a visual object for a specific hotbar slot using the appropriate socket
    /// </summary>
    private void CreateHotbarSlotObject(int slotNumber, ItemData itemData)
    {
        if (itemData?.equippedItemPrefab == null)
        {
            Debug.LogError($"Cannot create slot object for slot {slotNumber}: No equipped prefab assigned");
            return;
        }

        try
        {
            // Determine which socket to use for this item
            Transform targetSocket = GetSocketForItem(itemData);

            if (targetSocket == null)
            {
                Debug.LogError($"No valid socket found for item {itemData.itemName} in slot {slotNumber}");
                return;
            }

            // Instantiate the equipped prefab in the appropriate socket
            GameObject slotObject = Instantiate(itemData.equippedItemPrefab, targetSocket);
            slotObject.name = $"Slot{slotNumber}_{itemData.itemName}_{(ShouldUseLeftHand(itemData) ? "L" : "R")}";

            // Apply positioning from item data
            slotObject.transform.localPosition = itemData.equippedPositionOffset;
            slotObject.transform.localEulerAngles = itemData.equippedRotationOffset;
            slotObject.transform.localScale = slotObject.transform.localScale + itemData.equippedScaleOverride;

            // Start inactive (will be activated when slot is selected)
            slotObject.SetActive(false);

            // Store reference with socket information
            hotbarSlotObjects[slotNumber] = new HotbarSlotInfo(slotObject, targetSocket, itemData);

            // Configure item-specific components
            ConfigureSlotObject(slotObject, itemData, slotNumber);

            string socketName = ShouldUseLeftHand(itemData) ? "left hand" : "right hand";
            DebugLog($"Created hotbar object for slot {slotNumber} in {socketName}: {itemData.itemName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create hotbar object for slot {slotNumber} ({itemData.itemName}): {e.Message}");
        }
    }

    /// <summary>
    /// Configure item-specific components based on item type
    /// </summary>
    private void ConfigureSlotObject(GameObject slotObject, ItemData itemData, int slotNumber)
    {
        switch (itemData.itemType)
        {
            case ItemType.RangedWeapon:
                ConfigureWeaponObject(slotObject, itemData.RangedWeaponData, slotNumber);
                break;
            case ItemType.Bow:
                ConfigureBowObject(slotObject, itemData.BowData, slotNumber);
                break;
            case ItemType.Tool:
                ConfigureToolObject(slotObject, itemData.ToolData, slotNumber);
                break;
            case ItemType.Consumable:
                ConfigureConsumableObject(slotObject, itemData.ConsumableData, slotNumber);
                break;
            default:
                DebugLog($"No specific configuration needed for {itemData.itemType} in slot {slotNumber}");
                break;
        }
    }

    /// <summary>
    /// Configure ranged weapon-specific components
    /// </summary>
    private void ConfigureWeaponObject(GameObject weaponObject, RangedWeaponData weaponData, int slotNumber)
    {
        // Add ranged weapon-specific configuration here
        DebugLog($"Configured ranged weapon object for slot {slotNumber}");
    }

    /// <summary>
    /// Configure bow-specific components
    /// </summary>
    private void ConfigureBowObject(GameObject bowObject, BowData bowData, int slotNumber)
    {
        if (bowData == null) return;

        // Bow-specific configuration
        DebugLog($"Configured bow object for slot {slotNumber} with left hand placement");

        // You can add bow-specific component setup here, such as:
        // - String tension components
        // - Arrow nocking points
        // - Draw mechanics
        // - Sight adjustments
    }

    /// <summary>
    /// Configure tool-specific components
    /// </summary>
    private void ConfigureToolObject(GameObject equipmentObject, ToolData toolData, int slotNumber)
    {
        if (toolData == null) return;

        // Add any equipment-specific configuration here
        DebugLog($"Configured tool object for slot {slotNumber}");
    }

    /// <summary>
    /// Configure consumable-specific components
    /// </summary>
    private void ConfigureConsumableObject(GameObject consumableObject, ConsumableData consumableData, int slotNumber)
    {
        if (consumableData == null) return;

        // Add any consumable-specific configuration here
        DebugLog($"Configured consumable object for slot {slotNumber}");
    }

    #endregion

    #region Item Switching System

    /// <summary>
    /// Switch to a specific hotbar slot (instant activation/deactivation) with proper socket handling
    /// </summary>
    public void EquipHotbarSlot(int slotNumber, ItemData itemData)
    {
        DebugLog($"Equipping hotbar slot {slotNumber}: {itemData?.itemName ?? "null"}");

        // Deactivate current item
        if (currentActiveItem != null)
        {
            currentActiveItem.SetActive(false);
            DebugLog($"Deactivated previous item in slot {currentActiveSlotNumber}");
        }

        if (hotbarSlotObjects != null)
        {
            DebugLog($"Hotbar slot objects found: {hotbarSlotObjects.Count}");
        }

        // Activate the slot object if it exists
        if (hotbarSlotObjects.TryGetValue(slotNumber, out HotbarSlotInfo slotInfo))
        {
            currentActiveItem = slotInfo.gameObject;
            currentEquippedItemData = itemData;
            currentActiveSlotNumber = slotNumber;
            currentActiveSocket = slotInfo.socket;

            currentActiveItem.SetActive(true);

            // Configure systems for this item type
            ConfigureSystemsForActiveItem(itemData, slotNumber);

            string socketName = ShouldUseLeftHand(itemData) ? "left hand" : "right hand";
            OnItemVisualEquipped?.Invoke(currentActiveItem, itemData, slotNumber, currentActiveSocket);
            DebugLog($"Successfully equipped slot {slotNumber} in {socketName}: {itemData.itemName}");
        }
        else
        {
            Debug.LogWarning($"No visual object found for hotbar slot {slotNumber}. Item may not have equipped prefab.");

            // Still update tracking variables
            currentActiveItem = null;
            currentEquippedItemData = itemData;
            currentActiveSlotNumber = slotNumber;
            currentActiveSocket = null;
        }
    }

    /// <summary>
    /// Configure external systems when an item becomes active
    /// </summary>
    private void ConfigureSystemsForActiveItem(ItemData itemData, int slotNumber)
    {
        switch (itemData.itemType)
        {
            case ItemType.RangedWeapon:
                ConfigureWeaponSystems(itemData.RangedWeaponData, slotNumber);
                break;
            case ItemType.Bow:
                ConfigureBowSystems(itemData.BowData, slotNumber);
                break;
            case ItemType.Tool:
                ConfigureEquipmentSystems(itemData, slotNumber);
                break;
        }
    }

    /// <summary>
    /// Configure ranged weapon-specific systems (ADS, weapon manager, etc.)
    /// </summary>
    private void ConfigureWeaponSystems(RangedWeaponData weaponData, int slotNumber)
    {
        if (weaponData == null) return;

        // Configure ADS settings for ranged weapons
        if (adsSetup != null)
        {
            //TODO: Implement ranged weapon-specific ADS configuration
            //DebugLog($"Applied ADS settings for ranged weapon {itemData.itemName}");
        }
    }

    /// <summary>
    /// Configure bow-specific systems
    /// </summary>
    private void ConfigureBowSystems(BowData bowData, int slotNumber)
    {
        if (bowData == null) return;

        // Configure ADS settings for bows (they might have different ADS behavior)
        if (adsSetup != null)
        {
            //TODO: Implement bow-specific ADS configuration
        }
    }

    /// <summary>
    /// Configure equipment-specific systems
    /// </summary>
    private void ConfigureEquipmentSystems(ItemData itemData, int slotNumber)
    {
        // Add equipment-specific system configuration here
        DebugLog($"Configured equipment systems for {itemData.itemName}");
    }

    /// <summary>
    /// Unequip current item (go to unarmed)
    /// </summary>
    public void UnequipCurrentItem()
    {
        if (currentActiveItem != null)
        {
            currentActiveItem.SetActive(false);
            string socketName = currentActiveSocket == propSocketL ? "left hand" : "right hand";
            DebugLog($"Unequipped item from slot {currentActiveSlotNumber} ({socketName})");
        }

        int previousSlot = currentActiveSlotNumber;

        currentActiveItem = null;
        currentEquippedItemData = null;
        currentActiveSlotNumber = -1;
        currentActiveSocket = null;

        OnItemVisualUnequipped?.Invoke(previousSlot);
        DebugLog("Switched to unarmed state");
    }

    #endregion

    #region Single Slot Management

    /// <summary>
    /// Add a single item to a specific hotbar slot using appropriate socket
    /// </summary>
    public void AddHotbarSlotObject(int slotNumber, ItemData itemData)
    {
        DebugLog($"Adding item to slot {slotNumber}: {itemData?.itemName ?? "null"}");

        // Remove existing object in this slot if any
        RemoveHotbarSlotObject(slotNumber);

        // Create new object in appropriate socket
        if (itemData?.equippedItemPrefab != null)
        {
            CreateHotbarSlotObject(slotNumber, itemData);
            string socketName = ShouldUseLeftHand(itemData) ? "left hand" : "right hand";
            DebugLog($"Added item to slot {slotNumber} in {socketName}: {itemData.itemName}");
        }
        else
        {
            DebugLog($"Item {itemData?.itemName ?? "null"} has no equipped prefab - skipping visual creation");
        }
    }

    /// <summary>
    /// Remove item from a specific hotbar slot
    /// </summary>
    public void RemoveHotbarSlotObject(int slotNumber)
    {
        if (hotbarSlotObjects.TryGetValue(slotNumber, out HotbarSlotInfo slotInfo))
        {
            // If this was the currently active item, unequip it
            if (currentActiveItem == slotInfo.gameObject)
            {
                UnequipCurrentItem();
            }

            // Destroy the object
            if (slotInfo.gameObject != null)
            {
                DebugLog("Destroying hotbar slot object for slot " + slotNumber);

                Destroy(slotInfo.gameObject);
            }
            hotbarSlotObjects.Remove(slotNumber);

            DebugLog($"Removed item from slot {slotNumber}");
        }
        else
        {
            DebugLog($"No item to remove from slot {slotNumber}");
        }
    }

    /// <summary>
    /// Update an existing hotbar slot with new item data (used for stacking, etc.)
    /// </summary>
    public void UpdateHotbarSlotObject(int slotNumber, ItemData newItemData)
    {
        if (hotbarSlotObjects.ContainsKey(slotNumber))
        {
            // Check if the item type or prefab changed (this might require socket change)
            bool needsRecreation = false;

            if (currentActiveSlotNumber == slotNumber && currentEquippedItemData != null)
            {
                bool oldLeftHand = ShouldUseLeftHand(currentEquippedItemData);
                bool newLeftHand = ShouldUseLeftHand(newItemData);

                needsRecreation = currentEquippedItemData.equippedItemPrefab != newItemData?.equippedItemPrefab ||
                                  oldLeftHand != newLeftHand; // Socket change needed
            }

            if (needsRecreation)
            {
                DebugLog($"Recreating slot {slotNumber} due to prefab or socket change");
                RemoveHotbarSlotObject(slotNumber);
                AddHotbarSlotObject(slotNumber, newItemData);

                // If this was the active slot, re-equip it
                if (currentActiveSlotNumber == slotNumber)
                {
                    EquipHotbarSlot(slotNumber, newItemData);
                }
            }
            else
            {
                DebugLog($"Updated slot {slotNumber} data without visual changes");
            }
        }
        else
        {
            // Slot doesn't exist yet, create it
            AddHotbarSlotObject(slotNumber, newItemData);
        }
    }

    #endregion

    #region Cleanup and Utility

    /// <summary>
    /// Clear all hotbar objects (called when hotbar is rebuilt)
    /// </summary>
    private void ClearAllHotbarObjects()
    {
        DebugLog("Clearing all hotbar objects from both sockets");

        foreach (var kvp in hotbarSlotObjects)
        {
            if (kvp.Value?.gameObject != null)
            {
                DestroyImmediate(kvp.Value.gameObject);
            }
        }

        hotbarSlotObjects.Clear();
        currentActiveItem = null;
        currentEquippedItemData = null;
        currentActiveSlotNumber = -1;
        currentActiveSocket = null;

        DebugLog("Cleared all hotbar objects");
    }

    /// <summary>
    /// Force cleanup (useful for scene transitions)
    /// </summary>
    public void ForceCleanup()
    {
        ClearAllHotbarObjects();
        DebugLog("Force cleanup completed");
    }

    #endregion

    #region Public API and Getters

    /// <summary>
    /// Check if a specific slot has a visual object
    /// </summary>
    public bool HasSlotObject(int slotNumber)
    {
        return hotbarSlotObjects.ContainsKey(slotNumber);
    }

    /// <summary>
    /// Get the visual object for a specific slot
    /// </summary>
    public GameObject GetSlotObject(int slotNumber)
    {
        if (hotbarSlotObjects.TryGetValue(slotNumber, out HotbarSlotInfo slotInfo))
        {
            return slotInfo.gameObject;
        }
        return null;
    }

    /// <summary>
    /// Get the socket being used for a specific slot
    /// </summary>
    public Transform GetSlotSocket(int slotNumber)
    {
        if (hotbarSlotObjects.TryGetValue(slotNumber, out HotbarSlotInfo slotInfo))
        {
            return slotInfo.socket;
        }
        return null;
    }

    /// <summary>
    /// Get currently active item data
    /// </summary>
    public ItemData GetCurrentEquippedItemData()
    {
        return currentEquippedItemData;
    }

    /// <summary>
    /// Get currently active slot number
    /// </summary>
    public int GetCurrentActiveSlotNumber()
    {
        return currentActiveSlotNumber;
    }

    /// <summary>
    /// Get currently active visual object
    /// </summary>
    public GameObject GetCurrentActiveObject()
    {
        return currentActiveItem;
    }

    /// <summary>
    /// Get currently active socket
    /// </summary>
    public Transform GetCurrentActiveSocket()
    {
        return currentActiveSocket;
    }

    /// <summary>
    /// Check if current item is equipped in left hand
    /// </summary>
    public bool IsCurrentItemInLeftHand()
    {
        return currentActiveSocket == propSocketL;
    }

    /// <summary>
    /// Check if any item is currently equipped
    /// </summary>
    public bool HasActiveItem()
    {
        return currentActiveItem != null;
    }

    /// <summary>
    /// Get count of populated hotbar slots
    /// </summary>
    public int GetPopulatedSlotCount()
    {
        return hotbarSlotObjects.Count;
    }

    /// <summary>
    /// Get all populated slot numbers
    /// </summary>
    public int[] GetPopulatedSlotNumbers()
    {
        var slots = new int[hotbarSlotObjects.Count];
        hotbarSlotObjects.Keys.CopyTo(slots, 0);
        return slots;
    }

    /// <summary>
    /// Get debug information about current equipment state
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== EQUIPPED ITEM VISUAL MANAGER DEBUG ===");
        info.AppendLine($"Current Active Item: {(currentActiveItem ? currentActiveItem.name : "None")}");
        info.AppendLine($"Current Slot: {currentActiveSlotNumber}");
        info.AppendLine($"Current Socket: {(currentActiveSocket ? currentActiveSocket.name : "None")}");
        info.AppendLine($"Is Left Hand: {IsCurrentItemInLeftHand()}");
        info.AppendLine($"Total Hotbar Objects: {hotbarSlotObjects.Count}");

        foreach (var kvp in hotbarSlotObjects)
        {
            var slotInfo = kvp.Value;
            string socketName = slotInfo.socket == propSocketL ? "L" : "R";
            info.AppendLine($"  Slot {kvp.Key}: {slotInfo.itemData.itemName} [{socketName}]");
        }

        return info.ToString();
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EquippedItemVisualManager] {message}");
        }
    }
}