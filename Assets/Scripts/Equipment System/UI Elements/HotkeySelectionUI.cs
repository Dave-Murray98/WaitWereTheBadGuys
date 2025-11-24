using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// UI for selecting which hotkey slot to assign an item to
/// UPDATED: Now works with HotkeySelectionButton component
/// </summary>
public class HotkeySelectionUI : MonoBehaviour
{
    public static HotkeySelectionUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private GameObject slotButtonPrefab;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Settings")]
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.15f;

    private InventoryItemData itemToAssign;
    private List<HotkeySelectionButton> slotButtons = new List<HotkeySelectionButton>();
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        CreateSlotButtons();
        HideSelection(true);

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(() => HideSelection());
        }
    }

    private void CreateSlotButtons()
    {
        if (slotsContainer == null || slotButtonPrefab == null)
        {
            Debug.LogError("HotkeySelectionUI: slotsContainer or slotButtonPrefab is null!");
            return;
        }

        // Create buttons for slots 1-10
        for (int i = 1; i <= 10; i++)
        {
            GameObject buttonObj = Instantiate(slotButtonPrefab, slotsContainer);
            HotkeySelectionButton selectionButton = buttonObj.GetComponent<HotkeySelectionButton>();

            if (selectionButton != null)
            {
                int slotNumber = i; // Capture for closure

                // Setup the button with click listener
                selectionButton.SetupButton(this, slotNumber);

                // Set slot number text
                if (selectionButton.slotNumberText != null)
                {
                    selectionButton.slotNumberText.text = slotNumber == 10 ? "0" : slotNumber.ToString();
                }

                slotButtons.Add(selectionButton);
            }
            else
            {
                Debug.LogError($"HotkeySelectionUI: HotkeySelectionButton component missing on slot {i}!");
            }
        }
    }

    /// <summary>
    /// Show the hotkey selection UI for the specified item
    /// </summary>
    public void ShowSelection(InventoryItemData item)
    {
        if (item?.ItemData == null) return;

        itemToAssign = item;

        // Update title
        if (titleText != null)
        {
            titleText.text = $"Assign {item.ItemData.itemName} to Hotkey";
        }

        // Update button states
        UpdateSlotButtonStates();

        // Show panel
        if (selectionPanel != null)
        {
            selectionPanel.SetActive(true);
        }

        // Fade in
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, fadeInDuration);
    }

    /// <summary>
    /// Hide the hotkey selection UI
    /// </summary>
    public void HideSelection(bool immediate = false)
    {
        itemToAssign = null;

        if (immediate)
        {
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }
            canvasGroup.alpha = 0f;
        }
        else
        {
            canvasGroup.DOFade(0f, fadeOutDuration)
                .OnComplete(() =>
                {
                    if (selectionPanel != null)
                    {
                        selectionPanel.SetActive(false);
                    }
                });
        }
    }

    private void UpdateSlotButtonStates()
    {
        if (EquippedItemManager.Instance == null || PlayerInventoryManager.Instance == null) return;

        var bindings = EquippedItemManager.Instance.GetAllHotkeyBindings();

        for (int i = 0; i < slotButtons.Count && i < bindings.Count; i++)
        {
            var button = slotButtons[i];
            var binding = bindings[i];
            var selectionButton = button.GetComponent<HotkeySelectionButton>();

            if (selectionButton == null) continue;

            // Update button appearance based on slot state
            var buttonImage = button.GetComponent<Image>();

            if (binding.isAssigned)
            {
                // Get the actual inventory item instead of loading from Resources
                var inventoryItem = PlayerInventoryManager.Instance.InventoryGridData.GetItem(binding.itemId);
                if (inventoryItem?.ItemData != null)
                {
                    var itemData = inventoryItem.ItemData;

                    // Show assigned item
                    if (selectionButton.itemIcon != null)
                    {
                        selectionButton.itemIcon.sprite = itemData.itemSprite;
                        selectionButton.itemIcon.gameObject.SetActive(itemData.itemSprite != null);
                    }

                    if (selectionButton.itemNameText != null)
                    {
                        selectionButton.itemNameText.text = itemData.itemName;
                    }

                    // Show stack info if multiple items
                    if (selectionButton.stackIndicator != null)
                    {
                        string stackInfo = binding.GetStackInfo();
                        if (!string.IsNullOrEmpty(stackInfo))
                        {
                            selectionButton.stackIndicator.text = stackInfo;
                            selectionButton.stackIndicator.gameObject.SetActive(true);
                        }
                        else
                        {
                            selectionButton.stackIndicator.gameObject.SetActive(false);
                        }
                    }

                    // Different color if same item type
                    if (buttonImage != null)
                    {
                        bool sameItemType = itemData.name == itemToAssign?.ItemData?.name;
                        buttonImage.color = sameItemType ? Color.yellow : Color.gray;
                    }
                }
                else
                {
                    // Item no longer exists in inventory - clear the binding
                    Debug.LogWarning($"Item {binding.itemId} no longer exists in inventory - clearing hotkey {binding.slotNumber}");
                    binding.ClearSlot();

                    // Show as empty slot
                    ShowEmptySlot(selectionButton, buttonImage);
                }
            }
            else
            {
                // Empty slot
                ShowEmptySlot(selectionButton, buttonImage);
            }
        }
    }

    private void ShowEmptySlot(HotkeySelectionButton selectionButton, Image buttonImage)
    {
        if (selectionButton.itemIcon != null)
        {
            selectionButton.itemIcon.sprite = null;
            selectionButton.itemIcon.gameObject.SetActive(false);
        }

        if (selectionButton.itemNameText != null)
        {
            selectionButton.itemNameText.text = "Empty";
        }

        if (selectionButton.stackIndicator != null)
        {
            selectionButton.stackIndicator.gameObject.SetActive(false);
        }

        if (buttonImage != null)
        {
            buttonImage.color = Color.white;
        }
    }

    public void OnSlotSelected(int slotNumber)
    {
        if (itemToAssign?.ID == null || EquippedItemManager.Instance == null) return;

        bool success = EquippedItemManager.Instance.AssignItemToHotkey(itemToAssign.ID, slotNumber);

        if (success)
        {
            Debug.Log($"Assigned {itemToAssign.ItemData.itemName} to hotkey {slotNumber}");
        }
        else
        {
            Debug.LogWarning($"Failed to assign {itemToAssign.ItemData.itemName} to hotkey {slotNumber}");
        }

        HideSelection();
    }

    private void Update()
    {
        // Close on escape key
        if (Input.GetKeyDown(KeyCode.Escape) && selectionPanel != null && selectionPanel.activeSelf)
        {
            HideSelection();
        }
    }
}