using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using System.Collections;
using System;

/// <summary>
/// FIXED: Inventory dropdown menu system with removed direct equip functionality.
/// Now only allows hotkey assignment - no direct equipment bypassing the hotbar system.
/// </summary>
public class InventoryDropdownMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject dropdownPanel;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float fadeOutDuration = 0.1f;

    [Header("Button Settings")]
    [SerializeField] private Color normalButtonColor = Color.white;
    [SerializeField] private Color hoverButtonColor = new Color(0.8f, 0.8f, 1f);
    [SerializeField] private Color disabledButtonColor = Color.gray;

    [Header("Size Settings")]
    [SerializeField] private float buttonHeight = 35f;
    [SerializeField] private float buttonSpacing = 5f;
    [SerializeField] private float containerPadding = 15f;
    [SerializeField] private float minWidth = 140f;

    [Header("Click Detection")]
    [SerializeField] private bool detectClicksOutside = true;

    [Header("Position Settings")]
    public Vector2 positionOffset = new Vector2(10f, -10f);

    // Static reference for single dropdown policy
    private static InventoryDropdownMenu currentlyOpen = null;

    // State
    private InventoryItemData currentItem;
    private RectTransform rectTransform;
    private bool isVisible = false;
    private List<GameObject> currentButtons = new List<GameObject>();
    private bool ignoreNextClick = false; // To prevent immediate closure on opening

    // Events
    public System.Action<InventoryItemData, string> OnActionSelected;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        // Create default button prefab if none assigned
        if (buttonPrefab == null)
            CreateDefaultButtonPrefab();

        // Initially hidden
        HideMenu(true);
    }

    private void Start()
    {
        // Subscribe to inventory close events only
        GameEvents.OnInventoryClosed += OnInventoryClosed;
    }

    private void OnDestroy()
    {
        GameEvents.OnInventoryClosed -= OnInventoryClosed;

        // Clear static reference if this is the currently open dropdown
        if (currentlyOpen == this)
        {
            currentlyOpen = null;
        }
    }

    private void Update()
    {
        // Check for clicks outside the dropdown to close it (both left and right mouse buttons)
        if (isVisible && detectClicksOutside && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            CheckForClickOutside();
        }
    }

    private void OnInventoryClosed()
    {
        if (isVisible)
        {
            HideMenu();
        }
    }

    /// <summary>
    /// Check if the mouse click was outside the dropdown area
    /// </summary>
    private void CheckForClickOutside()
    {
        // Skip the check on the frame we just opened to prevent immediate closure
        if (ignoreNextClick)
        {
            ignoreNextClick = false;
            return;
        }

        Vector2 mousePosition = Input.mousePosition;

        // Convert mouse position to local coordinates relative to our canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector2 localPoint;

        // Convert screen point to canvas local point
        bool isInCanvas = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, mousePosition, canvas.worldCamera, out localPoint);

        if (!isInCanvas) return;

        // Convert canvas local point to dropdown local point
        Vector2 dropdownLocalPoint;
        bool isInDropdown = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, mousePosition, canvas.worldCamera, out dropdownLocalPoint);

        // If the click is not within our dropdown bounds, close the menu
        if (!isInDropdown || !rectTransform.rect.Contains(dropdownLocalPoint))
        {
            HideMenu();
        }
    }

    /// <summary>
    /// Show dropdown menu for the specified item at the specified screen position
    /// </summary>
    public void ShowMenu(InventoryItemData item, Vector2 screenPosition, ItemInventoryTypeContext inventoryTypeContext, bool canTransfer)
    {
        if (item?.ItemData == null) return;

        // Close any currently open dropdown first
        if (currentlyOpen != null)
        {
            currentlyOpen.HideMenu(true); // Immediate close
        }

        // Set this as the currently open dropdown
        currentlyOpen = this;
        currentItem = item;

        // Ensure the GameObject and dropdown panel are active before starting coroutine
        gameObject.SetActive(true);
        if (dropdownPanel != null)
        {
            dropdownPanel.SetActive(true);
        }

        // Clear and create buttons
        ClearButtons();
        CreateButtonsForItemType(item.ItemData.itemType, inventoryTypeContext, canTransfer);

        // Start the showing process with proper timing
        StartCoroutine(ShowMenuCoroutine(screenPosition));
    }

    /// <summary>
    /// Coroutine to handle proper timing for showing menu
    /// </summary>
    private IEnumerator ShowMenuCoroutine(Vector2 screenPosition)
    {
        // Set flag to ignore clicks during the opening process
        ignoreNextClick = true;

        // Set initial state - visible but transparent
        isVisible = true;
        canvasGroup.alpha = 0f;
        transform.localScale = Vector3.zero;

        // Wait for layout to calculate
        yield return new WaitForEndOfFrame();

        // Force layout rebuild
        if (buttonContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonContainer as RectTransform);
        }

        // Wait one more frame for layout to settle
        yield return new WaitForEndOfFrame();

        // Now calculate and set the proper size
        CalculateAndSetSize();

        // Position the dropdown
        PositionAtScreenPoint(screenPosition);

        // Finally animate in
        AnimateIn();

        // Wait for animation to complete, then allow click detection
        yield return new WaitForSeconds(fadeInDuration + 0.1f);

        // Reset the ignore flag after the dropdown is fully shown and animation is complete
        ignoreNextClick = false;
    }

    /// <summary>
    /// Calculate and set the proper size based on buttons
    /// </summary>
    private void CalculateAndSetSize()
    {
        if (currentButtons.Count == 0)
        {
            // Default size if no buttons
            rectTransform.sizeDelta = new Vector2(minWidth, 60f);
            return;
        }

        // Calculate height based on button count
        float totalHeight = (currentButtons.Count * buttonHeight) +
                           ((currentButtons.Count - 1) * buttonSpacing) +
                           (containerPadding * 2);

        // Calculate width - use minimum width or content width, whichever is larger
        float calculatedWidth = minWidth;

        // Check if we can get actual button widths
        foreach (var button in currentButtons)
        {
            var buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                // Use the preferred width if it's larger
                var textComponent = button.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    float textWidth = textComponent.preferredWidth + 20f; // Add padding
                    calculatedWidth = Mathf.Max(calculatedWidth, textWidth);
                }
            }
        }

        // Set the calculated size
        Vector2 newSize = new Vector2(calculatedWidth, totalHeight);
        rectTransform.sizeDelta = newSize;
    }

    /// <summary>
    /// Animate the dropdown in
    /// </summary>
    private void AnimateIn()
    {
        // Fade in
        canvasGroup.DOFade(1f, fadeInDuration);

        // Scale animation
        transform.DOScale(Vector3.one, fadeInDuration).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// Hide the dropdown menu
    /// </summary>
    public void HideMenu(bool immediate = false)
    {
        if (!isVisible && !immediate) return;

        isVisible = false;
        currentItem = null;

        // Clear static reference if this was the currently open dropdown
        if (currentlyOpen == this)
        {
            currentlyOpen = null;
        }

        if (immediate)
        {
            if (dropdownPanel != null)
            {
                dropdownPanel.SetActive(false);
            }
            canvasGroup.alpha = 0f;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(false);
        }
        else
        {
            canvasGroup.DOFade(0f, fadeOutDuration)
                .OnComplete(() =>
                {
                    if (dropdownPanel != null)
                    {
                        dropdownPanel.SetActive(false);
                    }
                    transform.localScale = Vector3.zero;
                    gameObject.SetActive(false);
                });
        }
    }

    /// <summary>
    /// Position the dropdown at the specified screen position with offset
    /// Direct screen space positioning for Screen Space Overlay canvas
    /// </summary>
    private void PositionAtScreenPoint(Vector2 screenPosition)
    {
        // Apply the position offset
        Vector2 adjustedScreenPosition = screenPosition + positionOffset;

        Debug.Log($"[DropdownMenu] Screen position: {screenPosition} + offset: {positionOffset} = {adjustedScreenPosition}");

        // For Screen Space Overlay, we can set the position directly
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Set position directly in screen space
            rectTransform.position = adjustedScreenPosition;

            Debug.Log($"[DropdownMenu] Set position directly to: {adjustedScreenPosition}");

            // Simple bounds checking
            ClampToScreen();
        }
        else
        {
            Debug.LogWarning("Dropdown positioning currently only supports Screen Space Overlay canvas");
        }
    }

    /// <summary>
    /// Simple screen clamping for Screen Space Overlay
    /// </summary>
    private void ClampToScreen()
    {
        Vector3 pos = rectTransform.position;
        Vector2 size = rectTransform.sizeDelta;

        // Get screen bounds
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // Clamp to screen bounds (assuming pivot at top-left)
        pos.x = Mathf.Clamp(pos.x, 0, screenWidth - size.x);
        pos.y = Mathf.Clamp(pos.y, size.y, screenHeight);

        rectTransform.position = pos;

        Debug.Log($"[DropdownMenu] Clamped to screen bounds: {pos}");
    }


    /// <summary>
    /// Create buttons based on item type
    /// FIXED: Removed all direct "Equip" actions - only hotkey assignment allowed
    /// </summary>
    private void CreateButtonsForItemType(ItemType itemType, ItemInventoryTypeContext inventoryTypeContext, bool canTransfer)
    {
        List<DropdownAction> actions = GetActionsForItemType(itemType, inventoryTypeContext, canTransfer);

        foreach (var action in actions)
        {
            CreateActionButton(action);
        }
    }

    /// <summary>
    /// Get available actions for the specified item type
    /// FIXED: Removed all direct equip functionality - equipment only through hotbar
    /// </summary>
    private List<DropdownAction> GetActionsForItemType(ItemType itemType, ItemInventoryTypeContext inventoryTypeContext, bool canTransfer)
    {

        if (inventoryTypeContext == ItemInventoryTypeContext.PlayerInventoryItem)
        {
            return CreateActionsForPlayerInventoryItem(itemType, canTransfer);
        }
        else
        {
            return CreateActionsForNonPlayerInventoryItem(itemType, canTransfer);
        }

    }

    private List<DropdownAction> CreateActionsForPlayerInventoryItem(ItemType itemType, bool canTransfer)
    {
        var actions = new List<DropdownAction>();

        if (canTransfer)
        {
            actions.Add(new DropdownAction("Transfer", "transfer", true));
        }

        switch (itemType)
        {
            case ItemType.Consumable:
                actions.Add(new DropdownAction("Consume", "consume", true));
                actions.Add(new DropdownAction("Assign to Hotkey", "assign_hotkey", true));
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;

            case ItemType.RangedWeapon:
                actions.Add(new DropdownAction("Assign to Hotkey", "assign_hotkey", true));
                actions.Add(new DropdownAction("Unload Ammo", "unload", true));
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;

            case ItemType.MeleeWeapon:
                actions.Add(new DropdownAction("Assign to Hotkey", "assign_hotkey", true));
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;

            case ItemType.Tool:
                actions.Add(new DropdownAction("Assign to Hotkey", "assign_hotkey", true));
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;

            case ItemType.KeyItem:
                actions.Add(new DropdownAction("Assign to Hotkey", "assign_hotkey", true));
                // Note: Key items cannot be dropped
                break;

            case ItemType.Ammo:
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;

            case ItemType.Clothing:
                // Get clothing data to determine valid layers
                var clothingData = currentItem?.ItemData?.ClothingData;
                if (clothingData != null)
                {
                    // Add "Wear in [Layer]" button for each valid layer
                    foreach (var validLayer in clothingData.validLayers)
                    {
                        string displayName = GetClothingWearButtonText(validLayer);
                        string actionId = $"wear_{validLayer}";
                        actions.Add(new DropdownAction(displayName, actionId, true));
                    }
                }
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;

            case ItemType.Throwable:
                actions.Add(new DropdownAction("Assign to Hotkey", "assign_hotkey", true));
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;

            case ItemType.Bow:
                actions.Add(new DropdownAction("Assign to Hotkey", "assign_hotkey", true));
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;
        }

        return actions;
    }

    private List<DropdownAction> CreateActionsForNonPlayerInventoryItem(ItemType itemType, bool canTransfer)
    {
        var actions = new List<DropdownAction>();

        if (canTransfer)
        {
            actions.Add(new DropdownAction("Transfer", "transfer", true));
        }

        actions.Add(new DropdownAction("Drop", "drop", true));

        switch (itemType)
        {
            case ItemType.Consumable:
                actions.Add(new DropdownAction("Consume", "consume", true));
                break;
        }

        return actions;
    }

    /// <summary>
    /// Gets user-friendly button text for clothing layers
    /// </summary>
    private string GetClothingWearButtonText(ClothingLayer layer)
    {
        return layer switch
        {
            ClothingLayer.HeadUpper => "Wear on Head (Upper)",
            ClothingLayer.HeadLower => "Wear on Head (Lower)",
            ClothingLayer.TorsoInner => "Wear on Torso (Inner)",
            ClothingLayer.TorsoOuter => "Wear on Torso (Outer)",
            ClothingLayer.LegsInner => "Wear on Legs (Inner)",
            ClothingLayer.LegsOuter => "Wear on Legs (Outer)",
            ClothingLayer.Hands => "Wear on Hands",
            ClothingLayer.Socks => "Wear as Socks",
            ClothingLayer.Shoes => "Wear as Shoes",
            _ => $"Wear as {layer}"
        };
    }

    /// <summary>
    /// Create a button for the specified action
    /// </summary>
    private void CreateActionButton(DropdownAction action)
    {
        GameObject buttonObj = Instantiate(buttonPrefab, buttonContainer);

        // Set the button size immediately
        var buttonRect = buttonObj.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            buttonRect.sizeDelta = new Vector2(minWidth - 10f, buttonHeight);
        }

        var button = buttonObj.GetComponent<Button>();
        var buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        var buttonImage = buttonObj.GetComponent<Image>();

        if (buttonText != null)
        {
            buttonText.text = action.displayName;
            buttonText.fontSize = 14f;
        }

        if (buttonImage != null)
            buttonImage.color = action.isEnabled ? normalButtonColor : disabledButtonColor;

        if (button != null)
        {
            button.interactable = action.isEnabled;

            if (action.isEnabled)
            {
                button.onClick.AddListener(() => OnActionButtonClicked(action.actionId));

                // Add hover effects
                AddHoverEffects(buttonObj, buttonImage);
            }
        }

        currentButtons.Add(buttonObj);
    }

    /// <summary>
    /// Add hover effects to button
    /// </summary>
    private void AddHoverEffects(GameObject buttonObj, Image buttonImage)
    {
        var trigger = buttonObj.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = buttonObj.AddComponent<EventTrigger>();

        // Mouse enter
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((data) =>
        {
            if (buttonImage != null)
                buttonImage.DOColor(hoverButtonColor, 0.1f);
        });
        trigger.triggers.Add(enterEntry);

        // Mouse exit
        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) =>
        {
            if (buttonImage != null)
                buttonImage.DOColor(normalButtonColor, 0.1f);
        });
        trigger.triggers.Add(exitEntry);
    }

    /// <summary>
    /// Handle button click
    /// </summary>
    private void OnActionButtonClicked(string actionId)
    {
        OnActionSelected?.Invoke(currentItem, actionId);
        HideMenu();
    }

    /// <summary>
    /// Clear all current buttons
    /// </summary>
    private void ClearButtons()
    {
        foreach (var button in currentButtons)
        {
            if (button != null)
                Destroy(button);
        }
        currentButtons.Clear();
    }

    /// <summary>
    /// Create default button prefab
    /// </summary>
    private void CreateDefaultButtonPrefab()
    {
        GameObject button = new GameObject("DropdownButton");

        var rect = button.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(minWidth, buttonHeight);

        var buttonImage = button.AddComponent<Image>();
        buttonImage.color = normalButtonColor;

        var buttonComponent = button.AddComponent<Button>();
        buttonComponent.targetGraphic = buttonImage;

        // Add Layout Element to control sizing
        var layoutElement = button.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = buttonHeight;
        layoutElement.preferredWidth = minWidth;

        // Add text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(button.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 5);
        textRect.offsetMax = new Vector2(-10, -5);

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Action";
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 14f;
        text.fontStyle = FontStyles.Normal;

        buttonPrefab = button;
    }

    /// <summary>
    /// Check if this dropdown is currently visible
    /// </summary>
    public bool IsVisible => isVisible;

    /// <summary>
    /// Static method to close any currently open dropdown
    /// </summary>
    public static void CloseAnyOpenDropdown()
    {
        if (currentlyOpen != null)
        {
            currentlyOpen.HideMenu(true);
        }
    }

    /// <summary>
    /// Public method to disable click-outside detection temporarily
    /// </summary>
    public void SetClickOutsideDetection(bool enabled)
    {
        detectClicksOutside = enabled;
    }

    /// <summary>
    /// Set the position offset for this dropdown menu
    /// </summary>
    public void SetPositionOffset(Vector2 offset)
    {
        positionOffset = offset;
    }

    /// <summary>
    /// Get the current position offset
    /// </summary>
    public Vector2 GetPositionOffset()
    {
        return positionOffset;
    }
}

/// <summary>
/// Simple dropdown action data
/// </summary>
[System.Serializable]
public class DropdownAction
{
    public string displayName;
    public string actionId;
    public bool isEnabled;

    public DropdownAction(string display, string id, bool enabled)
    {
        displayName = display;
        actionId = id;
        isEnabled = enabled;
    }
}