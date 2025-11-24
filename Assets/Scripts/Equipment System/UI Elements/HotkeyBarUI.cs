using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// FIXED: HotkeyBarUI now properly handles individual slot selection and restriction display.
/// 
/// KEY FIXES:
/// - Each slot is treated individually (no shared unarmed state)
/// - Only one slot can be highlighted/selected at a time
/// - Restricted items show as greyed out but still selectable
/// - Proper visual feedback for empty slots acting as unarmed
/// - Real-time updates when player state changes
/// </summary>
public class HotkeyBarUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform hotkeyContainer;
    [SerializeField] private GameObject hotkeySlotPrefab;



    [Header("FIXED: Visual Settings")]
    [SerializeField] private bool showStateRestrictions = true;
    [SerializeField] private bool showCurrentState = true;
    [SerializeField] private float restrictionUpdateDelay = 0.1f;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = false;

    // System references
    private EquippedItemManager equippedItemManager;
    private PlayerStateManager playerStateManager;

    // UI state
    private Dictionary<int, HotkeySlotUI> hotkeySlots = new Dictionary<int, HotkeySlotUI>();
    private bool isInitialized = false;
    private PlayerStateType lastDisplayedState = PlayerStateType.Ground;
    private int currentlySelectedSlot = 1; // Track which slot is currently selected

    private void Start()
    {
        StartCoroutine(InitializeWithDelay());
    }

    private void OnEnable()
    {
        if (isInitialized)
            StartCoroutine(RefreshOnEnable());
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// FIXED: Enhanced initialization with proper slot selection tracking
    /// </summary>
    private IEnumerator InitializeWithDelay()
    {
        yield return null;
        yield return new WaitUntil(() => EquippedItemManager.Instance != null && PlayerInventoryManager.Instance != null);

        FindSystemReferences();
        CreateHotkeySlots();
        SubscribeToEvents();

        yield return null;
        RefreshAllSlots();

        isInitialized = true;
        DebugLog("HotkeyBarUI initialized with fixed slot selection");
    }

    private void FindSystemReferences()
    {
        equippedItemManager = EquippedItemManager.Instance;
        playerStateManager = PlayerStateManager.Instance ?? FindFirstObjectByType<PlayerStateManager>();

        if (equippedItemManager == null)
            Debug.LogError("[HotkeyBarUI] EquippedItemManager not found!");

        if (playerStateManager == null)
            Debug.LogError("[HotkeyBarUI] PlayerStateManager not found! State restrictions won't work.");

        DebugLog($"System references found - Equipment: {equippedItemManager != null}, State: {playerStateManager != null}");
    }

    private IEnumerator RefreshOnEnable()
    {
        if (!isInitialized) yield break;

        yield return new WaitUntil(() => EquippedItemManager.Instance != null && PlayerInventoryManager.Instance != null);

        FindSystemReferences();
        UnsubscribeFromEvents();
        SubscribeToEvents();

        yield return null;
        RefreshAllSlots();

        DebugLog("HotkeyBarUI refreshed on enable");
    }

    private void CreateHotkeySlots()
    {
        if (hotkeyContainer == null || hotkeySlotPrefab == null)
        {
            Debug.LogError("[HotkeyBarUI] Missing references - hotkeyContainer or hotkeySlotPrefab is null!");
            return;
        }

        // Clear existing slots
        foreach (var slot in hotkeySlots.Values)
        {
            if (slot != null && slot.gameObject != null)
                DestroyImmediate(slot.gameObject);
        }
        hotkeySlots.Clear();

        // Create 10 slots (1-0)
        for (int i = 1; i <= 10; i++)
        {
            GameObject slotObj = Instantiate(hotkeySlotPrefab, hotkeyContainer);
            slotObj.name = $"HotkeySlot_{i}";

            var slotUI = slotObj.GetComponent<HotkeySlotUI>();
            if (slotUI == null)
                slotUI = slotObj.AddComponent<HotkeySlotUI>();

            slotUI.Initialize(i);
            hotkeySlots[i] = slotUI;
        }

        DebugLog($"Created {hotkeySlots.Count} hotkey slots");
    }

    /// <summary>
    /// FIXED: Enhanced event subscription with new slot selection event
    /// </summary>
    private void SubscribeToEvents()
    {
        if (equippedItemManager != null)
        {
            equippedItemManager.OnHotkeyAssigned -= OnHotkeyAssigned;
            equippedItemManager.OnHotkeyCleared -= OnHotkeyCleared;
            equippedItemManager.OnSlotSelected -= OnSlotSelected;  // NEW EVENT
            equippedItemManager.OnUnarmedActivated -= OnUnarmedActivated;

            equippedItemManager.OnHotkeyAssigned += OnHotkeyAssigned;
            equippedItemManager.OnHotkeyCleared += OnHotkeyCleared;
            equippedItemManager.OnSlotSelected += OnSlotSelected;  // NEW EVENT
            equippedItemManager.OnUnarmedActivated += OnUnarmedActivated;
        }

        if (playerStateManager != null)
        {
            playerStateManager.OnStateChanged -= OnPlayerStateChanged;
            playerStateManager.OnStateChanged += OnPlayerStateChanged;
        }

        DebugLog("Event subscriptions complete with slot selection tracking");
    }

    private void UnsubscribeFromEvents()
    {
        if (equippedItemManager != null)
        {
            equippedItemManager.OnHotkeyAssigned -= OnHotkeyAssigned;
            equippedItemManager.OnHotkeyCleared -= OnHotkeyCleared;
            equippedItemManager.OnSlotSelected -= OnSlotSelected;
            equippedItemManager.OnUnarmedActivated -= OnUnarmedActivated;
        }

        if (playerStateManager != null)
        {
            playerStateManager.OnStateChanged -= OnPlayerStateChanged;
        }
    }

    /// <summary>
    /// FIXED: Enhanced slot refresh with individual slot tracking
    /// </summary>
    private void RefreshAllSlots()
    {
        if (equippedItemManager == null)
        {
            DebugLog("Cannot refresh slots - EquippedItemManager not available");
            return;
        }

        var bindingsWithValidity = equippedItemManager.GetAllHotkeyBindingsWithValidity();
        int currentActiveSlot = equippedItemManager.GetCurrentActiveSlot();

        DebugLog($"Refreshing all slots - Current active: {currentActiveSlot}");

        foreach (var (binding, isUsable) in bindingsWithValidity)
        {
            if (hotkeySlots.TryGetValue(binding.slotNumber, out var slotUI))
            {
                // Update slot content
                if (binding.isAssigned)
                {
                    var inventoryItem = PlayerInventoryManager.Instance?.InventoryGridData.GetItem(binding.itemId);
                    if (inventoryItem?.ItemData != null)
                    {
                        slotUI.SetAssignedItem(binding, inventoryItem.ItemData, isUsable);
                        DebugLog($"Refreshed slot {binding.slotNumber}: {inventoryItem.ItemData.itemName} [{(isUsable ? "USABLE" : "RESTRICTED")}]");
                    }
                    else
                    {
                        // Item no longer exists
                        binding.ClearSlot();
                        slotUI.ClearSlot();
                        equippedItemManager.OnHotkeyCleared?.Invoke(binding.slotNumber);
                        DebugLog($"Item missing for slot {binding.slotNumber} - cleared");
                    }
                }
                else
                {
                    slotUI.ClearSlot();
                    DebugLog($"Cleared slot {binding.slotNumber}");
                }

                // FIXED: Update selection state for each slot individually
                bool isCurrentlySelected = (binding.slotNumber == currentActiveSlot);
                slotUI.SetSelectedState(isCurrentlySelected);
            }
        }

        currentlySelectedSlot = currentActiveSlot;
    }

    private IEnumerator DelayedRestrictionUpdate()
    {
        yield return new WaitForSeconds(restrictionUpdateDelay);

        if (showStateRestrictions)
        {
            RefreshAllSlots();
        }
    }

    #region FIXED: Event Handlers

    private void OnHotkeyAssigned(int slotNumber, HotkeyBinding binding)
    {
        DebugLog($"Hotkey assigned event for slot {slotNumber}");

        if (hotkeySlots.TryGetValue(slotNumber, out var slotUI) && PlayerInventoryManager.Instance != null)
        {
            var inventoryItem = PlayerInventoryManager.Instance.InventoryGridData.GetItem(binding.itemId);
            if (inventoryItem?.ItemData != null)
            {
                // Check if item is usable in current state
                var bindingsWithValidity = equippedItemManager.GetAllHotkeyBindingsWithValidity();
                var matchingBinding = bindingsWithValidity.Find(x => x.binding.slotNumber == slotNumber);
                bool isUsable = matchingBinding.isUsable;

                slotUI.SetAssignedItem(binding, inventoryItem.ItemData, isUsable);

                // Update selection state
                bool isCurrentlySelected = (slotNumber == equippedItemManager.GetCurrentActiveSlot());
                slotUI.SetSelectedState(isCurrentlySelected);

                DebugLog($"Updated slot {slotNumber} assignment: {inventoryItem.ItemData.itemName} [{(isUsable ? "USABLE" : "RESTRICTED")}]");
            }
        }
    }

    private void OnHotkeyCleared(int slotNumber)
    {
        DebugLog($"Hotkey cleared event for slot {slotNumber}");

        if (hotkeySlots.TryGetValue(slotNumber, out var slotUI))
        {
            slotUI.ClearSlot();

            // Update selection state
            bool isCurrentlySelected = (slotNumber == equippedItemManager.GetCurrentActiveSlot());
            slotUI.SetSelectedState(isCurrentlySelected);
        }
    }

    /// <summary>
    /// FIXED: NEW - Handle slot selection events
    /// </summary>
    private void OnSlotSelected(int slotNumber, HotkeyBinding binding, bool isUsable)
    {
        DebugLog($"Slot selected event: {slotNumber} (usable: {isUsable})");

        // Clear selection from all slots
        foreach (var slot in hotkeySlots.Values)
        {
            slot.SetSelectedState(false);
        }

        // Set selection on the new slot
        if (hotkeySlots.TryGetValue(slotNumber, out var selectedSlotUI))
        {
            selectedSlotUI.SetSelectedState(true);
        }

        currentlySelectedSlot = slotNumber;
        DebugLog($"Updated selection: slot {slotNumber} is now selected");
    }

    private void OnUnarmedActivated()
    {
        DebugLog("Unarmed activated event received - maintaining current slot selection");
        // Don't clear selection - just update the visual state of the current slot
        int currentSlot = equippedItemManager.GetCurrentActiveSlot();
        if (hotkeySlots.TryGetValue(currentSlot, out var slotUI))
        {
            slotUI.SetSelectedState(true); // Keep it selected even if unarmed
        }
    }

    private void OnPlayerStateChanged(PlayerStateType previousState, PlayerStateType newState)
    {
        DebugLog($"Player state changed: {previousState} -> {newState}");

        if (showStateRestrictions)
        {
            RefreshAllSlots();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// FIXED: Forces a complete refresh with proper selection tracking
    /// </summary>
    public void ForceRefreshAll()
    {
        if (!isInitialized)
        {
            DebugLog("Not initialized - cannot force refresh");
            return;
        }

        StartCoroutine(ForceRefreshCoroutine());
    }

    private IEnumerator ForceRefreshCoroutine()
    {
        yield return new WaitUntil(() => equippedItemManager != null && PlayerInventoryManager.Instance != null);

        FindSystemReferences();
        UnsubscribeFromEvents();
        SubscribeToEvents();

        RefreshAllSlots();

        DebugLog("Force refresh completed");
    }

    public void SetShowStateRestrictions(bool show)
    {
        showStateRestrictions = show;
        if (isInitialized)
        {
            RefreshAllSlots();
        }
        DebugLog($"State restrictions display: {(show ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// FIXED: Enhanced debug info with slot selection tracking
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("HotkeyBarUI Debug Info (FIXED):");
        info.AppendLine($"  Initialized: {isInitialized}");
        info.AppendLine($"  Slot Count: {hotkeySlots.Count}");
        info.AppendLine($"  Currently Selected Slot: {currentlySelectedSlot}");
        info.AppendLine($"  Show Restrictions: {showStateRestrictions}");
        info.AppendLine($"  Show Current State: {showCurrentState}");
        info.AppendLine($"  EquippedItemManager: {(equippedItemManager != null ? "Found" : "NULL")}");
        info.AppendLine($"  PlayerStateManager: {(playerStateManager != null ? "Found" : "NULL")}");
        info.AppendLine($"  Current State: {lastDisplayedState}");

        if (equippedItemManager != null)
        {
            info.AppendLine($"  Active Slot: {equippedItemManager.GetCurrentActiveSlot()}");
            info.AppendLine($"  Is Unarmed: {equippedItemManager.IsUnarmed}");
            info.AppendLine($"  Has Equipped Item: {equippedItemManager.HasEquippedItem}");
        }

        return info.ToString();
    }

    public bool IsSlotValid(int slotNumber)
    {
        return hotkeySlots.ContainsKey(slotNumber) && hotkeySlots[slotNumber] != null;
    }

    public HotkeySlotUI GetSlotUI(int slotNumber)
    {
        return hotkeySlots.GetValueOrDefault(slotNumber);
    }

    #endregion

    #region Debug Methods

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[HotkeyBarUI] {message}");
        }
    }

    [ContextMenu("Debug Current State")]
    private void DebugCurrentUIState()
    {
        Debug.Log("=== HOTKEY BAR UI DEBUG (FIXED) ===");
        Debug.Log(GetDebugInfo());

        if (equippedItemManager != null)
        {
            var bindingsWithValidity = equippedItemManager.GetAllHotkeyBindingsWithValidity();
            Debug.Log("Slot States:");
            foreach (var (binding, isUsable) in bindingsWithValidity)
            {
                string status = !binding.isAssigned ? "EMPTY" :
                               isUsable ? $"{binding.GetCurrentItemData()?.itemName} [USABLE]" :
                               $"{binding.GetCurrentItemData()?.itemName} [RESTRICTED]";

                string selection = binding.slotNumber == currentlySelectedSlot ? " <-- SELECTED" : "";
                Debug.Log($"  Slot {binding.slotNumber}: {status}{selection}");
            }
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        DebugLog("HotkeyBarUI destroyed");
    }

    #endregion
}