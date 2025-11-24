using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// UI component that shows the currently equipped item
/// </summary>
public class EquippedItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject equippedItemPanel;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemTypeText;
    [SerializeField] private TextMeshProUGUI hotkeyText;

    [Header("Visual Settings")]
    [SerializeField] private Color equippedColor = Color.white;
    [SerializeField] private Color unequippedColor = Color.gray;
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.2f;

    [Header("Position")]
    [SerializeField] private bool hideWhenNoItem = true;

    private CanvasGroup canvasGroup;
    private bool isVisible = false;

    private void Awake()
    {
        // Get or add canvas group for fading
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Start hidden
        if (hideWhenNoItem)
        {
            SetVisible(false, true);
        }
    }

    private void Start()
    {
        // Subscribe to equipment events
        if (EquippedItemManager.Instance != null)
        {
            EquippedItemManager.Instance.OnItemEquipped += OnItemEquipped;
            EquippedItemManager.Instance.OnItemUnequipped += OnItemUnequipped;
        }

        // Update display with current state
        RefreshDisplay();
    }

    private void OnItemEquipped(EquippedItemData equippedItem)
    {
        RefreshDisplay();

        if (hideWhenNoItem)
        {
            SetVisible(true);
        }
    }

    private void OnItemUnequipped()
    {
        RefreshDisplay();

        if (hideWhenNoItem)
        {
            SetVisible(false);
        }
    }

    private void RefreshDisplay()
    {
        if (EquippedItemManager.Instance == null) return;

        var equippedItem = EquippedItemManager.Instance.CurrentEquippedItem;

        if (equippedItem.isEquipped)
        {
            var itemData = equippedItem.GetItemData();
            if (itemData != null)
            {
                UpdateItemDisplay(itemData, equippedItem);
            }
        }
        else
        {
            ClearItemDisplay();
        }
    }

    private void UpdateItemDisplay(ItemData itemData, EquippedItemData equippedItem)
    {
        // Update item icon
        if (itemIcon != null)
        {
            itemIcon.sprite = itemData.itemSprite;
            itemIcon.color = equippedColor;
            itemIcon.gameObject.SetActive(itemData.itemSprite != null);
        }

        // Update item name
        if (itemNameText != null)
        {
            itemNameText.text = itemData.itemName;
            itemNameText.color = equippedColor;
        }

        // Update item type
        if (itemTypeText != null)
        {
            itemTypeText.text = GetItemTypeDisplay(itemData.itemType);
            itemTypeText.color = equippedColor;
        }

        // Update hotkey display
        if (hotkeyText != null)
        {
            if (equippedItem.isEquippedFromHotkey)
            {
                hotkeyText.text = GetHotkeyDisplayText(equippedItem.sourceHotkeySlot);
                hotkeyText.gameObject.SetActive(true);
            }
            else
            {
                hotkeyText.gameObject.SetActive(false);
            }
        }
    }

    private void ClearItemDisplay()
    {
        // Clear item icon
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.color = unequippedColor;
            itemIcon.gameObject.SetActive(false);
        }

        // Clear item name
        if (itemNameText != null)
        {
            itemNameText.text = "No Item Equipped";
            itemNameText.color = unequippedColor;
        }

        // Clear item type
        if (itemTypeText != null)
        {
            itemTypeText.text = "";
        }

        // Hide hotkey
        if (hotkeyText != null)
        {
            hotkeyText.gameObject.SetActive(false);
        }
    }

    private void SetVisible(bool visible, bool immediate = false)
    {
        if (isVisible == visible && !immediate) return;

        isVisible = visible;

        if (immediate)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            if (equippedItemPanel != null)
            {
                equippedItemPanel.SetActive(visible);
            }
        }
        else
        {
            if (visible && equippedItemPanel != null)
            {
                equippedItemPanel.SetActive(true);
            }

            canvasGroup.DOFade(visible ? 1f : 0f, visible ? fadeInDuration : fadeOutDuration)
                .OnComplete(() =>
                {
                    if (!visible && equippedItemPanel != null)
                    {
                        equippedItemPanel.SetActive(false);
                    }
                });
        }
    }

    private string GetItemTypeDisplay(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.RangedWeapon => "WEAPON",
            ItemType.Consumable => "CONSUMABLE",
            ItemType.Tool => "EQUIPMENT",
            ItemType.KeyItem => "KEY ITEM",
            ItemType.Ammo => "AMMO",
            _ => "ITEM"
        };
    }

    private string GetHotkeyDisplayText(int slotNumber)
    {
        return slotNumber == 10 ? "[0]" : $"[{slotNumber}]";
    }

    private void OnDestroy()
    {
        if (EquippedItemManager.Instance != null)
        {
            EquippedItemManager.Instance.OnItemEquipped -= OnItemEquipped;
            EquippedItemManager.Instance.OnItemUnequipped -= OnItemUnequipped;
        }
    }
}