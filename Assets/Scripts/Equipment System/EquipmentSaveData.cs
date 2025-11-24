using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Save data for the equipment system
/// FIXED: Added copy constructor for scene transitions
/// </summary>
[System.Serializable]
public class EquipmentSaveData
{
    [Header("Hotkey Assignments")]
    public List<HotkeyBinding> hotkeyBindings = new List<HotkeyBinding>();

    [Header("Equipped Item")]
    public EquippedItemData equippedItem = new EquippedItemData();

    [Header("FIXED: Current State Tracking")]
    public int currentActiveSlot = 1; // Which slot is currently selected (1-10)
    public bool hasEquippedItem = false; // Whether player actually has a usable item equipped

    // Default constructor
    public EquipmentSaveData()
    {
        // Initialize 10 hotkey slots (1-0)
        hotkeyBindings = new List<HotkeyBinding>();
        for (int i = 1; i <= 10; i++)
        {
            hotkeyBindings.Add(new HotkeyBinding(i));
        }

        equippedItem = new EquippedItemData();
        currentActiveSlot = 1;
        hasEquippedItem = false;
    }

    // CRITICAL FIX: Copy constructor for scene transitions
    public EquipmentSaveData(EquipmentSaveData other)
    {
        // Deep copy equipped item
        equippedItem = new EquippedItemData(other.equippedItem);

        // Deep copy hotkey bindings using copy constructor
        hotkeyBindings = new List<HotkeyBinding>();
        foreach (var binding in other.hotkeyBindings)
        {
            hotkeyBindings.Add(new HotkeyBinding(binding)); // Uses HotkeyBinding copy constructor
        }

        // FIXED: Copy current state tracking
        currentActiveSlot = other.currentActiveSlot;
        hasEquippedItem = other.hasEquippedItem;

        // Debug log to verify copy worked
        var assignedCount = hotkeyBindings.FindAll(h => h.isAssigned).Count;
        //        Debug.Log($"[EquipmentSaveData] Copy constructor: Copied {hotkeyBindings.Count} hotkey slots, {assignedCount} assigned, active slot: {currentActiveSlot}, equipped: {hasEquippedItem}");

        // Debug active slot specifically
        if (currentActiveSlot >= 1 && currentActiveSlot <= 10)
        {
            var activeBinding = GetHotkeyBinding(currentActiveSlot);
            if (activeBinding?.isAssigned == true)
            {
                DebugLog($"[EquipmentSaveData] Copy constructor: Active slot {currentActiveSlot} = {activeBinding.itemDataName} (ID: {activeBinding.itemId})");
            }
            else
            {
                DebugLog($"[EquipmentSaveData] Copy constructor: Active slot {currentActiveSlot} is empty");
            }
        }
    }

    /// <summary>
    /// Get hotkey binding for a specific slot
    /// </summary>
    public HotkeyBinding GetHotkeyBinding(int slotNumber)
    {
        return hotkeyBindings.Find(h => h.slotNumber == slotNumber);
    }

    /// <summary>
    /// FIXED: Update current state tracking
    /// </summary>
    public void UpdateCurrentState(int activeSlot, bool hasEquipped)
    {
        currentActiveSlot = activeSlot;
        hasEquippedItem = hasEquipped;
    }

    /// <summary>
    /// Validate that save data is consistent
    /// </summary>
    public bool IsValid()
    {
        bool basicValid = hotkeyBindings != null && hotkeyBindings.Count == 10;
        bool slotValid = currentActiveSlot >= 1 && currentActiveSlot <= 10;

        return basicValid && slotValid;
    }

    public void DebugLog(string message)
    {
        if (EquippedItemManager.Instance.enableDebugLogs)
            Debug.Log($"[EquipmentSaveData] {message}");
    }
}