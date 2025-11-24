using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// FIXED: UI component for individual hotkey slots with proper selection handling.
/// 
/// KEY FIXES:
/// - Each slot maintains its own selection state independently
/// - Clear visual distinction between selected/unselected states
/// - Restricted items show as greyed out but can still be selected
/// - Empty slots show as selectable for unarmed combat
/// - Proper visual feedback for different slot states
/// </summary>
public class HotkeySlotUI : MonoBehaviour
{
    [Header("Core UI References")]
    [SerializeField] private Image slotBackground;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI keyNumberText;
    [SerializeField] private TextMeshProUGUI stackCountText;

    [Header("Selection Visual Elements")]
    [SerializeField] private GameObject selectedIndicator;  // Shows when this slot is selected
    [SerializeField] private Image selectionBorder;         // Border that highlights when selected
    [SerializeField] private GameObject restrictionOverlay; // Red X or disabled indicator
    [SerializeField] private Image restrictionIcon;         // Icon showing restriction type
    [SerializeField] private GameObject unarmedIndicator;   // Shows when empty slot is selected
    [SerializeField] private CanvasGroup slotCanvasGroup;   // For opacity changes

    [Header("Visual Settings")]
    [SerializeField] private Color normalSlotColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
    [SerializeField] private Color emptySlotColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private Color selectedSlotColor = new Color(0f, 1f, 0f, 0.8f);      // Green when selected
    [SerializeField] private Color restrictedSlotColor = new Color(0.5f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color selectedEmptyColor = new Color(0.4f, 0.4f, 0.6f, 0.7f);  // Blue when empty but selected

    [Header("Selection Settings")]
    [SerializeField] private Color selectedBorderColor = new Color(0f, 1f, 0f, 0.4f);      // Green when selected
    [SerializeField] private float restrictedOpacity = 0.5f;
    [SerializeField] private Color restrictedIconColor = new Color(1f, 0f, 0f, 0.4f); // Red for restricted items 
    [SerializeField] private float visualTransitionTime = 0.3f;
    [SerializeField] private bool enableSmoothTransitions = true;

    [Header("Tooltip Settings")]
    [SerializeField] private bool enableTooltips = true;

    // Slot state
    private int slotNumber;
    private HotkeyBinding currentBinding;
    private ItemData currentItemData;
    private bool isCurrentlySelected = false;      // FIXED: Individual selection state
    private bool isCurrentlyRestricted = false;
    private bool isEmpty = true;

    // Visual transition tweeners
    private Tweener backgroundTweener;
    private Tweener opacityTweener;
    private Tweener borderTweener;

    // Tooltip system
    private SimpleTooltipTrigger tooltipTrigger;

    public int SlotNumber => slotNumber;

    #region Initialization

    public void Initialize(int slot)
    {
        slotNumber = slot;
        SetupUIReferences();
        SetupTooltipSystem();
        SetKeyNumberDisplay();
        ClearSlot();
        selectionBorder.color = new Color(selectedBorderColor.r, selectedBorderColor.g, selectedBorderColor.b, 0f); // Start invisible
        slotBackground.color = emptySlotColor; // Set initial background color
    }

    private void SetupUIReferences()
    {
        // Auto-find missing references
        if (slotCanvasGroup == null)
            slotCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        if (restrictionOverlay == null)
            restrictionOverlay = transform.Find("RestrictionOverlay")?.gameObject;

        if (restrictionIcon == null && restrictionOverlay != null)
            restrictionIcon = restrictionOverlay.GetComponentInChildren<Image>();

        if (unarmedIndicator == null)
            unarmedIndicator = transform.Find("UnarmedIndicator")?.gameObject;

        if (selectedIndicator == null)
            selectedIndicator = transform.Find("SelectedIndicator")?.gameObject;

        if (selectionBorder == null)
            selectionBorder = transform.Find("SelectionBorder")?.GetComponent<Image>();

        // Create missing visual elements if they don't exist
        CreateMissingVisualElements();
    }

    private void CreateMissingVisualElements()
    {
        // Create selection border if missing
        if (selectionBorder == null)
        {
            GameObject borderObj = new GameObject("SelectionBorder");
            borderObj.transform.SetParent(transform);
            borderObj.transform.localPosition = Vector3.zero;
            borderObj.transform.localScale = Vector3.one;

            selectionBorder = borderObj.AddComponent<Image>();
            selectionBorder.color = selectedBorderColor;

            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;

            borderObj.SetActive(false); // Start hidden
        }

        // Create restriction overlay if missing
        if (restrictionOverlay == null)
        {
            restrictionOverlay = new GameObject("RestrictionOverlay");
            restrictionOverlay.transform.SetParent(transform);
            restrictionOverlay.transform.localPosition = Vector3.zero;
            restrictionOverlay.transform.localScale = Vector3.one;

            var overlayImage = restrictionOverlay.AddComponent<Image>();
            overlayImage.color = new Color(0, 0, 0, 0.6f);
            var overlayRect = restrictionOverlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            // Add restriction icon (X)
            var iconObj = new GameObject("RestrictionIcon");
            iconObj.transform.SetParent(restrictionOverlay.transform);
            iconObj.transform.localPosition = Vector3.zero;
            iconObj.transform.localScale = Vector3.one;

            restrictionIcon = iconObj.AddComponent<Image>();
            restrictionIcon.color = restrictedIconColor;

            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(24, 24);

            SetupRestrictionIcon();
            restrictionOverlay.SetActive(false); // Start hidden
        }

        // Create unarmed indicator if missing
        if (unarmedIndicator == null)
        {
            unarmedIndicator = new GameObject("UnarmedIndicator");
            unarmedIndicator.transform.SetParent(transform);
            unarmedIndicator.transform.localPosition = Vector3.zero;
            unarmedIndicator.transform.localScale = Vector3.one;

            var unarmedText = unarmedIndicator.AddComponent<TextMeshProUGUI>();
            unarmedText.text = "FIST";
            unarmedText.fontSize = 8;
            unarmedText.color = Color.white;
            unarmedText.alignment = TextAlignmentOptions.Center;

            var unarmedRect = unarmedIndicator.GetComponent<RectTransform>();
            unarmedRect.anchorMin = new Vector2(0.1f, 0.1f);
            unarmedRect.anchorMax = new Vector2(0.9f, 0.3f);
            unarmedRect.offsetMin = Vector2.zero;
            unarmedRect.offsetMax = Vector2.zero;

            unarmedIndicator.SetActive(false); // Start hidden
        }

        // Ensure proper layering
        if (restrictionOverlay != null)
            restrictionOverlay.transform.SetAsLastSibling();
        if (selectionBorder != null)
            selectionBorder.transform.SetAsLastSibling();
    }

    private void SetupRestrictionIcon()
    {
        if (restrictionIcon == null) return;

        // Create a simple X using text if no sprite is available
        GameObject iconTextGO = new GameObject("RestrictionIconText");
        iconTextGO.transform.SetParent(restrictionIcon.transform);

        TextMeshProUGUI iconText = iconTextGO.AddComponent<TextMeshProUGUI>();
        iconText.text = "X";
        iconText.fontSize = 18;
        iconText.color = restrictedIconColor;
        iconText.alignment = TextAlignmentOptions.Center;

        // Position the text
        var textRect = iconTextGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // Disable the Image component since we're using text
        restrictionIcon.enabled = false;
    }

    private void SetupTooltipSystem()
    {
        if (!enableTooltips) return;

        tooltipTrigger = GetComponent<SimpleTooltipTrigger>();
        if (tooltipTrigger == null && enableTooltips)
        {
            gameObject.AddComponent<SimpleTooltipTrigger>();
        }
    }

    private void SetKeyNumberDisplay()
    {
        if (keyNumberText != null)
        {
            keyNumberText.text = slotNumber == 10 ? "0" : slotNumber.ToString();
        }
    }

    #endregion

    #region Main Interface Methods

    /// <summary>
    /// FIXED: Sets an assigned item with restriction state support
    /// </summary>
    public void SetAssignedItem(HotkeyBinding binding, ItemData itemData, bool isUsable = true)
    {
        currentBinding = binding;
        currentItemData = itemData;
        isCurrentlyRestricted = !isUsable;
        isEmpty = false;

        UpdateItemVisuals();
        UpdateRestrictionVisuals();
        UpdateTooltip();
    }

    /// <summary>
    /// FIXED: Clears the slot and shows as empty
    /// </summary>
    public void ClearSlot()
    {
        currentBinding = null;
        currentItemData = null;
        isCurrentlyRestricted = false;
        isEmpty = true;

        ClearItemVisuals();
        UpdateRestrictionVisuals();
        UpdateTooltip();
    }

    /// <summary>
    /// FIXED: Sets the selection state for this individual slot
    /// </summary>
    public void SetSelectedState(bool isSelected)
    {
        if (isCurrentlySelected == isSelected) return;

        isCurrentlySelected = isSelected;
        UpdateSelectionVisuals();
        UpdateSlotBackground();
    }

    #endregion

    #region Visual Update Methods

    private void UpdateItemVisuals()
    {
        if (currentItemData == null) return;

        // Update item icon
        if (itemIcon != null)
        {
            itemIcon.sprite = currentItemData.itemSprite;
            itemIcon.gameObject.SetActive(currentItemData.itemSprite != null);
        }

        // Update stack count
        if (stackCountText != null && currentBinding != null)
        {
            string stackInfo = currentBinding.GetStackInfo();
            stackCountText.text = stackInfo;
            stackCountText.gameObject.SetActive(!string.IsNullOrEmpty(stackInfo));
        }
    }

    private void ClearItemVisuals()
    {
        // Clear item icon
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.gameObject.SetActive(false);
        }

        // Clear stack count
        if (stackCountText != null)
        {
            stackCountText.text = "";
            stackCountText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// FIXED: Updates restriction visual elements based on current state
    /// </summary>
    private void UpdateRestrictionVisuals()
    {
        // Show/hide restriction overlay
        if (restrictionOverlay != null)
        {
            restrictionOverlay.SetActive(isCurrentlyRestricted);
        }

        // Show/hide unarmed indicator for empty selected slots
        if (unarmedIndicator != null)
        {
            unarmedIndicator.SetActive(isEmpty && isCurrentlySelected);
        }

        // Update opacity based on restriction state
        float targetOpacity = isCurrentlyRestricted ? restrictedOpacity : .4f;
        UpdateSlotOpacity(targetOpacity);
    }

    /// <summary>
    /// FIXED: Updates selection visual elements
    /// </summary>
    private void UpdateSelectionVisuals()
    {
        // Show/hide selection border
        if (selectionBorder != null)
        {
            selectionBorder.gameObject.SetActive(isCurrentlySelected);

            if (isCurrentlySelected && enableSmoothTransitions)
            {
                // Animate border appearance
                selectionBorder.color = selectedBorderColor;
                borderTweener?.Kill();
                borderTweener = selectionBorder.DOFade(selectedBorderColor.a, visualTransitionTime);
            }
        }

        // Show/hide selected indicator
        if (selectedIndicator != null)
        {
            selectedIndicator.SetActive(isCurrentlySelected);
        }

        // Update unarmed indicator visibility (only show when selected and empty)
        if (unarmedIndicator != null)
        {
            unarmedIndicator.SetActive(isEmpty && isCurrentlySelected);
        }
    }

    /// <summary>
    /// FIXED: Updates slot background color with selection awareness
    /// </summary>
    private void UpdateSlotBackground(Color? overrideColor = null)
    {
        if (slotBackground == null) return;

        Color newColor;
        if (overrideColor.HasValue)
        {
            newColor = overrideColor.Value;
        }
        else if (isCurrentlySelected)
        {
            // Different colors based on slot state when selected
            if (isEmpty)
                newColor = selectedEmptyColor;      // Blue for empty selected slots
            else if (isCurrentlyRestricted)
                newColor = restrictedSlotColor;     // Red for restricted items (even when selected)
            else
                newColor = selectedSlotColor;       // Green for usable selected items
        }
        else
        {
            // Normal unselected colors
            if (isEmpty)
                newColor = emptySlotColor;
            else if (isCurrentlyRestricted)
                newColor = restrictedSlotColor;
            else
                newColor = normalSlotColor;
        }

        // Apply color with smooth transition
        if (enableSmoothTransitions)
        {
            backgroundTweener?.Kill();
            backgroundTweener = slotBackground.DOColor(newColor, visualTransitionTime);
        }
        else
        {
            slotBackground.color = newColor;
        }
    }

    /// <summary>
    /// Updates slot opacity for restriction effects
    /// </summary>
    private void UpdateSlotOpacity(float targetOpacity)
    {
        if (slotCanvasGroup == null) return;

        if (enableSmoothTransitions)
        {
            opacityTweener?.Kill();
            opacityTweener = slotCanvasGroup.DOFade(targetOpacity, visualTransitionTime);
        }
        else
        {
            slotCanvasGroup.alpha = targetOpacity;
        }
    }

    /// <summary>
    /// Updates tooltip content based on current state
    /// </summary>
    private void UpdateTooltip()
    {
        if (!enableTooltips) return;

        string tooltipText = GetTooltipText();

        // Update simple tooltip trigger
        var simpleTooltip = GetComponent<SimpleTooltipTrigger>();
        if (simpleTooltip != null)
        {
            simpleTooltip.SetTooltipText(tooltipText);
        }
    }

    /// <summary>
    /// FIXED: Generates tooltip text based on current slot state
    /// </summary>
    private string GetTooltipText()
    {
        var tooltip = new System.Text.StringBuilder();

        // Slot number and selection state
        string selectionStatus = isCurrentlySelected ? " (SELECTED)" : "";
        tooltip.AppendLine($"Hotkey {slotNumber}{selectionStatus}");

        if (isEmpty)
        {
            tooltip.AppendLine("Empty slot");
            if (isCurrentlySelected)
            {
                tooltip.AppendLine("Using unarmed combat");
            }
            else
            {
                tooltip.AppendLine("Click to use unarmed combat");
            }
        }
        else if (currentItemData != null)
        {
            tooltip.AppendLine($"Item: {currentItemData.itemName}");

            if (currentBinding != null && currentBinding.HasMultipleItems)
            {
                tooltip.AppendLine($"Stack: {currentBinding.GetStackInfo()}");
            }

            tooltip.AppendLine();
            if (isCurrentlyRestricted)
            {
                tooltip.AppendLine("⚠ RESTRICTED");
                tooltip.AppendLine(GetRestrictionReason());
                if (isCurrentlySelected)
                {
                    tooltip.AppendLine("Acting as unarmed combat");
                }
            }
            else
            {
                tooltip.AppendLine("✓ Available");
                if (isCurrentlySelected)
                {
                    tooltip.AppendLine("Currently equipped");
                }
                else
                {
                    tooltip.AppendLine("Click to equip");
                }
            }
        }

        return tooltip.ToString();
    }

    /// <summary>
    /// Gets the reason why this item is restricted
    /// </summary>
    private string GetRestrictionReason()
    {
        if (currentItemData == null) return "Unknown restriction";

        var usableStates = currentItemData.GetUsableStates();
        if (usableStates.Length == 0)
        {
            return "This item cannot be used anywhere";
        }

        var stateNames = new System.Collections.Generic.List<string>();
        foreach (var state in usableStates)
        {
            stateNames.Add(state switch
            {
                PlayerStateType.Ground => "on land",
                PlayerStateType.Water => "in water",
                PlayerStateType.Vehicle => "in vehicles",
                _ => state.ToString().ToLower()
            });
        }

        return $"Only usable {string.Join(" or ", stateNames)}";
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// FIXED: Forces a visual refresh of all elements
    /// </summary>
    public void ForceVisualRefresh()
    {
        if (currentItemData != null)
        {
            UpdateItemVisuals();
        }
        else
        {
            ClearItemVisuals();
        }

        UpdateRestrictionVisuals();
        UpdateSelectionVisuals();
        UpdateSlotBackground();
        UpdateTooltip();
    }

    /// <summary>
    /// Sets restriction state without changing item assignment
    /// </summary>
    public void SetRestrictionState(bool isRestricted)
    {
        if (isCurrentlyRestricted == isRestricted) return;

        isCurrentlyRestricted = isRestricted;
        UpdateRestrictionVisuals();
        UpdateSlotBackground();
        UpdateTooltip();
    }

    /// <summary>
    /// FIXED: Gets current visual state for debugging
    /// </summary>
    public string GetVisualStateInfo()
    {
        return $"Slot {slotNumber}: Item={currentItemData?.itemName ?? "None"}, " +
               $"Selected={isCurrentlySelected}, Restricted={isCurrentlyRestricted}, " +
               $"Empty={isEmpty}";
    }

    /// <summary>
    /// FIXED: Check if this slot is currently selected
    /// </summary>
    public bool IsSelected => isCurrentlySelected;

    /// <summary>
    /// Check if this slot has an item assigned
    /// </summary>
    public bool HasItem => !isEmpty && currentItemData != null;

    /// <summary>
    /// Check if this slot's item is restricted
    /// </summary>
    public bool IsRestricted => isCurrentlyRestricted;

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        // Clean up any active tweeners
        backgroundTweener?.Kill();
        opacityTweener?.Kill();
        borderTweener?.Kill();
    }

    #endregion
}

/// <summary>
/// Simple tooltip trigger component for basic tooltip functionality
/// (Same as before, but included for completeness)
/// </summary>
public class SimpleTooltipTrigger : MonoBehaviour
{
    private string tooltipText = "";
    private bool isHovering = false;
    private float hoverStartTime;
    private const float TOOLTIP_DELAY = 0.5f;

    public void SetTooltipText(string text)
    {
        tooltipText = text;
    }

    private void OnMouseEnter()
    {
        isHovering = true;
        hoverStartTime = Time.time;
    }

    private void OnMouseExit()
    {
        isHovering = false;
        HideTooltip();
    }

    private void Update()
    {
        if (isHovering && Time.time - hoverStartTime > TOOLTIP_DELAY)
        {
            ShowTooltip();
        }
    }

    private void ShowTooltip()
    {
        if (string.IsNullOrEmpty(tooltipText)) return;
        Debug.Log($"Tooltip: {tooltipText}");
    }

    private void HideTooltip()
    {
        // Hide tooltip in your actual tooltip system
    }
}