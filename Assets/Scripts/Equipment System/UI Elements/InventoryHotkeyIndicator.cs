using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component to add to inventory item visuals to show hotkey assignments
/// This goes on the DraggableGridItem prefab
/// </summary>
public class InventoryHotkeyIndicator : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject hotkeyIndicator;
    [SerializeField] private TextMeshProUGUI hotkeyText;
    [SerializeField] private Image hotkeyBackground;

    [Header("Visual Settings")]
    [SerializeField] private Color assignedColor = new Color(1f, 1f, 0f, 0.8f); // Yellow
    [SerializeField] private Color equippedColor = new Color(0f, 1f, 0f, 0.8f); // Green
    [SerializeField] private Vector2 indicatorOffset = new Vector2(5f, -5f);

    private InventoryItemData itemData;
    private int assignedHotkeySlot = -1;
    private bool isCurrentlyEquipped = false;

    private void Start()
    {
        // Subscribe to equipment events
        if (EquippedItemManager.Instance != null)
        {
            EquippedItemManager.Instance.OnItemEquipped += OnItemEquipped;
            EquippedItemManager.Instance.OnItemUnequipped += OnItemUnequipped;
            EquippedItemManager.Instance.OnHotkeyAssigned += OnHotkeyAssigned;
            EquippedItemManager.Instance.OnHotkeyCleared += OnHotkeyCleared;
        }

        //GameEvents.OnInventoryOpened += RefreshDisplay;

        //Debug.Log("InventoryHotkeyIndicator started");

        // Start hidden
        //SetIndicatorVisible(false);
    }

    /// <summary>
    /// Initialize with item data (called by the inventory visual system)
    /// </summary>
    public void Initialize(InventoryItemData inventoryItem)
    {
        itemData = inventoryItem;
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        //  Debug.Log($"Refreshing hotkey indicator display on {itemData.ItemData.itemName}");

        if (itemData?.ID == null)
        {
            Debug.LogWarning("Cannot refresh display - no item data");
            return;
        }

        // Check if this item is assigned to any hotkey
        assignedHotkeySlot = FindAssignedHotkeySlot();

        // Check if this item is currently equipped
        isCurrentlyEquipped = CheckIfCurrentlyEquipped();

        // Update display
        if (assignedHotkeySlot != -1 || isCurrentlyEquipped)
        {
            //  Debug.Log($"Found hotkey assignment for {itemData.ItemData.itemName}");
            UpdateIndicatorDisplay();
            SetIndicatorVisible(true);
        }
        else
        {
            //            Debug.Log($"No hotkey assignment found for {itemData.ItemData.itemName}");
            SetIndicatorVisible(false);
        }

        // Move to the top so it's always rendered on top
        MoveHotkeyIndicatorVisualToFront();
    }

    private int FindAssignedHotkeySlot()
    {
        if (EquippedItemManager.Instance == null || itemData?.ID == null) return -1;

        var bindings = EquippedItemManager.Instance.GetAllHotkeyBindings();
        foreach (var binding in bindings)
        {
            if (binding.isAssigned && binding.stackedItemIds.Contains(itemData.ID))
            {
                return binding.slotNumber;
            }
        }

        return -1;
    }

    private bool CheckIfCurrentlyEquipped()
    {
        if (EquippedItemManager.Instance == null || itemData?.ID == null) return false;

        return EquippedItemManager.Instance.CurrentEquippedItem.IsEquipped(itemData.ID);
    }

    private void UpdateIndicatorDisplay()
    {
        if (hotkeyText != null)
        {
            if (isCurrentlyEquipped)
            {
                hotkeyText.text = "E"; // E for Equipped
            }
            else if (assignedHotkeySlot != -1)
            {
                hotkeyText.text = assignedHotkeySlot == 10 ? "0" : assignedHotkeySlot.ToString();
            }
        }

        if (hotkeyBackground != null)
        {
            hotkeyBackground.color = isCurrentlyEquipped ? equippedColor : assignedColor;
        }
    }

    private void SetIndicatorVisible(bool visible)
    {
        if (hotkeyIndicator != null)
        {
            hotkeyIndicator.SetActive(visible);
        }
    }

    #region Event Handlers

    private void OnItemEquipped(EquippedItemData equippedItem)
    {
        RefreshDisplay();
    }

    private void OnItemUnequipped()
    {
        RefreshDisplay();
    }

    private void OnHotkeyAssigned(int slotNumber, HotkeyBinding binding)
    {
        RefreshDisplay();
    }

    private void OnHotkeyCleared(int slotNumber)
    {
        RefreshDisplay();
    }

    public void MoveHotkeyIndicatorVisualToFront()
    {
        hotkeyIndicator.transform.SetAsLastSibling();
    }

    #endregion

    private void OnDestroy()
    {
        if (EquippedItemManager.Instance != null)
        {
            EquippedItemManager.Instance.OnItemEquipped -= OnItemEquipped;
            EquippedItemManager.Instance.OnItemUnequipped -= OnItemUnequipped;
            EquippedItemManager.Instance.OnHotkeyAssigned -= OnHotkeyAssigned;
            EquippedItemManager.Instance.OnHotkeyCleared -= OnHotkeyCleared;
        }

        //GameEvents.OnInventoryOpened -= RefreshDisplay;
        //Debug.Log("InventoryHotkeyIndicator destroyed");
    }
}