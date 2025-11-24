using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// FIXED: Enhanced clothing slot UI that properly integrates with the existing drag system
/// Now provides visual feedback without interfering with the drag handler's drop logic
/// </summary>
public class ClothingSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Components")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image itemImage;
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private Image conditionBar;
    [SerializeField] private GameObject conditionBarContainer;

    [Header("Visual Settings")]
    [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color occupiedSlotColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
    [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.5f, 0.9f);
    [SerializeField] private Color validDropColor = new Color(0.2f, 0.8f, 0.2f, 0.7f);
    [SerializeField] private Color invalidDropColor = new Color(0.8f, 0.2f, 0.2f, 0.7f);

    [Header("Condition Bar Colors")]
    [SerializeField] private Color goodConditionColor = Color.green;
    [SerializeField] private Color fairConditionColor = Color.yellow;
    [SerializeField] private Color poorConditionColor = Color.red;

    [Header("Animation Settings")]
    [SerializeField] private float hoverAnimationDuration = 0.2f;
    [SerializeField] private float equipAnimationDuration = 0.3f;
    [SerializeField] private float errorShakeDuration = 0.5f;
    [SerializeField] private float errorShakeStrength = 10f;

    // Configuration
    [SerializeField] private ClothingLayer targetLayer;
    private bool isInitialized = false;

    // References
    private ClothingManager clothingManager;
    private PlayerInventoryManager inventoryManager;

    // State
    private bool isHovering = false;
    private bool isDragOver = false;

    // Animation
    private Tween currentAnimation;

    public ClothingLayer TargetLayer => targetLayer;

    private void Awake()
    {
        SetupUIComponents();
    }

    private void Start()
    {
        clothingManager = ClothingManager.Instance;
        inventoryManager = PlayerInventoryManager.Instance;

        if (!isInitialized)
        {
            Debug.LogWarning($"ClothingSlotUI {name} was not properly initialized!");
        }

        SubscribeToClothingEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromClothingEvents();
    }

    /// <summary>
    /// Subscribe to clothing manager events for automatic UI updates
    /// </summary>
    private void SubscribeToClothingEvents()
    {
        if (clothingManager != null)
        {
            clothingManager.OnItemEquipped += OnItemEquipped;
            clothingManager.OnItemUnequipped += OnItemUnequipped;
            clothingManager.OnItemSwapped += OnItemSwapped;
            clothingManager.OnClothingConditionChanged += OnConditionChanged;
        }
    }

    /// <summary>
    /// Unsubscribe from clothing manager events
    /// </summary>
    private void UnsubscribeFromClothingEvents()
    {
        if (clothingManager != null)
        {
            clothingManager.OnItemEquipped -= OnItemEquipped;
            clothingManager.OnItemUnequipped -= OnItemUnequipped;
            clothingManager.OnItemSwapped -= OnItemSwapped;
            clothingManager.OnClothingConditionChanged -= OnConditionChanged;
        }
    }

    /// <summary>
    /// Initialize the clothing slot UI for a specific layer
    /// </summary>
    public void Initialize(ClothingLayer layer)
    {
        targetLayer = layer;
        isInitialized = true;

        SetupUIComponents();
        UpdateSlotLabel();
        RefreshDisplay();

        //        Debug.Log($"ClothingSlotUI initialized for layer: {layer}");
    }

    /// <summary>
    /// Setup UI components if they're not assigned
    /// </summary>
    private void SetupUIComponents()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (itemImage == null)
        {
            var images = GetComponentsInChildren<Image>();
            if (images.Length > 1)
                itemImage = images[1];
        }

        if (slotLabel == null)
            slotLabel = GetComponentInChildren<TextMeshProUGUI>();

        if (conditionBar == null)
        {
            var conditionBarObj = transform.Find("ConditionBar");
            if (conditionBarObj != null)
            {
                conditionBar = conditionBarObj.GetComponent<Image>();
                conditionBarContainer = conditionBarObj.gameObject;
            }
        }

        CreateMissingComponents();
    }

    /// <summary>
    /// Create missing UI components
    /// </summary>
    private void CreateMissingComponents()
    {
        if (backgroundImage == null)
        {
            backgroundImage = gameObject.GetComponent<Image>();
            if (backgroundImage == null)
            {
                Debug.LogWarning($"ClothingSlotUI {name} is missing a background image! creating new one");
                backgroundImage = gameObject.AddComponent<Image>();
            }
        }

        if (itemImage == null)
        {
            GameObject itemImageObj = new GameObject("ItemImage");
            itemImageObj.transform.SetParent(transform, false);

            var rectTransform = itemImageObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(5, 5);
            rectTransform.offsetMax = new Vector2(-5, -5);

            itemImage = itemImageObj.AddComponent<Image>();
            itemImage.raycastTarget = false;
            itemImage.preserveAspect = true;
            Debug.Log($"ClothingSlotUI {name} is missing an item image! creating new one");
        }

        if (slotLabel == null)
        {
            GameObject labelObj = new GameObject("SlotLabel");
            labelObj.transform.SetParent(transform, false);

            var rectTransform = labelObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 0.3f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            slotLabel = labelObj.AddComponent<TextMeshProUGUI>();
            slotLabel.text = "Slot";
            slotLabel.fontSize = 8f;
            slotLabel.color = Color.white;
            slotLabel.alignment = TextAlignmentOptions.Center;
            slotLabel.raycastTarget = false;
            Debug.Log($"ClothingSlotUI {name} is missing a slot label! creating new one");
        }

        if (conditionBar == null)
        {
            CreateConditionBar();
            Debug.Log($"ClothingSlotUI {name} is missing a condition bar! creating new one");
        }
    }

    /// <summary>
    /// Create the condition bar UI
    /// </summary>
    private void CreateConditionBar()
    {
        GameObject containerObj = new GameObject("ConditionBarContainer");
        containerObj.transform.SetParent(transform, false);

        var containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0.7f);
        containerRect.anchorMax = new Vector2(1, 0.8f);
        containerRect.offsetMin = new Vector2(2, 0);
        containerRect.offsetMax = new Vector2(-2, 0);

        conditionBarContainer = containerObj;

        GameObject backgroundBarObj = new GameObject("ConditionBarBackground");
        backgroundBarObj.transform.SetParent(containerObj.transform, false);

        var backgroundRect = backgroundBarObj.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var backgroundBarImage = backgroundBarObj.AddComponent<Image>();
        backgroundBarImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        backgroundBarImage.raycastTarget = false;

        GameObject conditionBarObj = new GameObject("ConditionBar");
        conditionBarObj.transform.SetParent(containerObj.transform, false);

        var conditionRect = conditionBarObj.AddComponent<RectTransform>();
        conditionRect.anchorMin = Vector2.zero;
        conditionRect.anchorMax = Vector2.one;
        conditionRect.offsetMin = Vector2.zero;
        conditionRect.offsetMax = Vector2.zero;

        conditionBar = conditionBarObj.AddComponent<Image>();
        conditionBar.color = goodConditionColor;
        conditionBar.type = Image.Type.Filled;
        conditionBar.fillMethod = Image.FillMethod.Horizontal;
        conditionBar.raycastTarget = false;
    }

    /// <summary>
    /// Update the slot label text
    /// </summary>
    private void UpdateSlotLabel()
    {
        if (slotLabel == null || !isInitialized) return;

        string labelText = GetShortLayerName(targetLayer);
        slotLabel.text = labelText;
    }

    /// <summary>
    /// Get a short display name for the clothing layer
    /// </summary>
    private string GetShortLayerName(ClothingLayer layer)
    {
        return layer switch
        {
            ClothingLayer.HeadUpper => "Hat",
            ClothingLayer.HeadLower => "Scarf",
            ClothingLayer.TorsoInner => "Shirt",
            ClothingLayer.TorsoOuter => "Jacket",
            ClothingLayer.LegsInner => "Under",
            ClothingLayer.LegsOuter => "Pants",
            ClothingLayer.Hands => "Gloves",
            ClothingLayer.Socks => "Socks",
            ClothingLayer.Shoes => "Shoes",
            _ => layer.ToString()
        };
    }

    /// <summary>
    /// Refresh the slot's visual display based on current state
    /// </summary>
    public void RefreshDisplay()
    {
        if (!isInitialized || clothingManager == null) return;

        var slot = clothingManager.GetSlot(targetLayer);
        if (slot == null) return;

        bool isEmpty = slot.IsEmpty;
        var equippedItem = slot.GetEquippedItem();

        // Update background color
        Color targetColor = isEmpty ? emptySlotColor : occupiedSlotColor;
        if (isHovering)
            targetColor = hoverColor;

        if (backgroundImage != null)
            backgroundImage.color = targetColor;

        // Update item image
        if (itemImage != null)
        {
            if (isEmpty || equippedItem?.ItemData?.itemSprite == null)
            {
                itemImage.sprite = null;
                itemImage.color = Color.clear;
            }
            else
            {
                itemImage.sprite = equippedItem.ItemData.itemSprite;
                itemImage.color = Color.white;
            }
        }

        // Update condition bar
        UpdateConditionBar(slot);
    }

    /// <summary>
    /// Update the condition bar based on equipped item
    /// </summary>
    private void UpdateConditionBar(ClothingSlot slot)
    {
        if (conditionBarContainer == null || conditionBar == null) return;

        bool isEmpty = slot.IsEmpty;
        conditionBarContainer.SetActive(!isEmpty);

        if (!isEmpty)
        {
            var clothingData = slot.GetEquippedClothingData();
            if (clothingData != null)
            {
                float conditionPercentage = clothingData.ConditionPercentage;
                conditionBar.fillAmount = conditionPercentage;

                if (conditionPercentage >= 0.7f)
                    conditionBar.color = goodConditionColor;
                else if (conditionPercentage >= 0.3f)
                    conditionBar.color = fairConditionColor;
                else
                    conditionBar.color = poorConditionColor;
            }
        }
    }

    /// <summary>
    /// Get the ID of the currently equipped item
    /// </summary>
    public string GetEquippedItemId()
    {
        if (!isInitialized || clothingManager == null) return "";

        var slot = clothingManager.GetSlot(targetLayer);
        return slot?.equippedItemId ?? "";
    }

    #region Event Handlers

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            TryUnequipItem();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;

        if (backgroundImage != null)
        {
            StopCurrentAnimation();
            currentAnimation = backgroundImage.DOColor(hoverColor, hoverAnimationDuration);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        if (!isDragOver && backgroundImage != null)
        {
            Color targetColor = GetCurrentTargetColor();
            StopCurrentAnimation();
            currentAnimation = backgroundImage.DOColor(targetColor, hoverAnimationDuration);
        }
    }

    /// <summary>
    /// ENHANCED: Drop handler with comprehensive validation and rejection feedback
    /// Properly rejects invalid clothing items and provides clear user feedback
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        // Clear drag over state
        isDragOver = false;
        ClearDragOverVisualFeedback();

        // Get the dragged object
        var draggedObject = eventData.pointerDrag;
        if (draggedObject == null)
        {
            DebugLog("Drop failed: No dragged object");
            return;
        }

        // Get the drag handler
        var dragHandler = draggedObject.GetComponent<PlayerInventoryItemDragHandler>();
        if (dragHandler == null)
        {
            DebugLog("Drop failed: No drag handler found");
            return;
        }

        // Get the item data from the drag handler
        var itemData = GetItemDataFromDragHandler(dragHandler);
        if (itemData == null)
        {
            DebugLog("Drop failed: No item data found");
            //ShowErrorFeedback("No item data found");
            // Signal the drag handler that this was an invalid drop
            NotifyDragHandlerOfInvalidDrop(dragHandler);
            return;
        }

        // ENHANCED: Comprehensive validation with detailed rejection handling
        var validationResult = ValidateClothingDrop(itemData);

        if (validationResult.IsValid)
        {
            DebugLog($"Valid drop detected: {itemData.ItemData?.itemName} -> {targetLayer}");

            // Attempt to equip using the ClothingManager
            bool success = ClothingDragDropHelper.HandleClothingSlotDrop(itemData, this);

            if (success)
            {
                DebugLog($"Successfully equipped {itemData.ItemData?.itemName} to {targetLayer}");
                ShowSuccessFeedback();
            }
            else
            {
                DebugLog($"Failed to equip {itemData.ItemData?.itemName} to {targetLayer}");
                //ShowErrorFeedback("Equipment failed");
                // Signal the drag handler to revert since equipment failed
                NotifyDragHandlerOfInvalidDrop(dragHandler);
            }
        }
        else
        {
            // ENHANCED: Detailed rejection with specific feedback
            DebugLog($"Invalid drop rejected: {validationResult.Message}");
            ShowRejectionFeedback(validationResult.Message, itemData);

            // Signal the drag handler that this was an invalid drop so it can revert
            NotifyDragHandlerOfInvalidDrop(dragHandler);
        }
    }

    /// <summary>
    /// ENHANCED: Comprehensive validation for clothing drops with detailed error messages
    /// </summary>
    private ValidationResult ValidateClothingDrop(InventoryItemData itemData)
    {
        // Check if it's a clothing item at all
        if (itemData?.ItemData?.itemType != ItemType.Clothing)
        {
            if (itemData?.ItemData != null)
            {
                string itemTypeName = itemData.ItemData.itemType.ToString();
                return new ValidationResult(false, $"{itemData.ItemData.itemName} is {itemTypeName}, not clothing");
            }
            return new ValidationResult(false, "Not a clothing item");
        }

        var clothingData = itemData.ItemData.ClothingData;
        if (clothingData == null)
        {
            return new ValidationResult(false, $"{itemData.ItemData.itemName} has no clothing data");
        }

        // Check if it can be equipped to this specific layer
        if (!clothingData.CanEquipToLayer(targetLayer))
        {
            string itemName = itemData.ItemData.itemName;
            string slotName = ClothingInventoryUtilities.GetFriendlyLayerName(targetLayer);

            // Get the valid layers for this item for better error messages
            string validLayersText = GetValidLayersText(clothingData.validLayers);

            return new ValidationResult(false, $"{itemName} cannot be worn on {slotName}. Can be worn on: {validLayersText}");
        }

        // Additional validation using the utility system
        var utilityValidation = ClothingInventoryUtilities.ValidateClothingEquip(itemData, targetLayer);
        if (!utilityValidation.IsValid)
        {
            return utilityValidation;
        }

        return new ValidationResult(true, "Valid clothing drop");
    }

    /// <summary>
    /// ENHANCED: Get user-friendly text for valid clothing layers
    /// </summary>
    private string GetValidLayersText(ClothingLayer[] validLayers)
    {
        if (validLayers == null || validLayers.Length == 0)
            return "nowhere";

        string[] layerNames = new string[validLayers.Length];
        for (int i = 0; i < validLayers.Length; i++)
        {
            layerNames[i] = ClothingInventoryUtilities.GetFriendlyLayerName(validLayers[i]);
        }

        if (layerNames.Length == 1)
            return layerNames[0];
        else if (layerNames.Length == 2)
            return $"{layerNames[0]} or {layerNames[1]}";
        else
            return string.Join(", ", layerNames, 0, layerNames.Length - 1) + $", or {layerNames[layerNames.Length - 1]}";
    }

    /// <summary>
    /// ENHANCED: Notify the drag handler that the drop was invalid so it can revert
    /// </summary>
    private void NotifyDragHandlerOfInvalidDrop(PlayerInventoryItemDragHandler dragHandler)
    {
        // Use reflection to call a method on the drag handler to indicate invalid drop
        var method = dragHandler.GetType().GetMethod("HandleInvalidClothingDrop",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            method.Invoke(dragHandler, null);
        }
        else
        {
            // Fallback: try to access the revert method directly
            var revertMethod = dragHandler.GetType().GetMethod("RevertToOriginalState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (revertMethod != null)
            {
                revertMethod.Invoke(dragHandler, null);
                DebugLog("Invoked drag handler revert method as fallback");
            }
            else
            {
                Debug.LogWarning("[ClothingSlotUI] Could not notify drag handler of invalid drop - methods not found");
            }
        }
    }

    #endregion

    #region Drag Detection (for visual feedback only)

    /// <summary>
    /// FIXED: Static method to detect when inventory items are being dragged over clothing slots
    /// This provides visual feedback without interfering with the actual drop handling
    /// </summary>
    public static void HandleDragOverClothingSlot(PointerEventData eventData, InventoryItemData itemData)
    {
        // Find clothing slot under pointer
        var clothingSlot = ClothingDragDropHelper.GetClothingSlotUnderPointer(eventData);

        if (clothingSlot != null)
        {
            // Check if this is a valid drop target
            bool isValidDrop = ClothingDragDropHelper.CanEquipToSlot(itemData, clothingSlot);

            // Set visual feedback
            clothingSlot.SetDragOverVisualFeedback(isValidDrop);
        }
    }

    /// <summary>
    /// FIXED: Static method to clear drag feedback from all clothing slots
    /// </summary>
    public static void ClearAllDragFeedback()
    {
        var allClothingSlots = FindObjectsByType<ClothingSlotUI>(FindObjectsSortMode.None);
        foreach (var slot in allClothingSlots)
        {
            slot.ClearDragOverVisualFeedback();
        }
    }

    /// <summary>
    /// FIXED: Set visual feedback during drag-over operations
    /// </summary>
    private void SetDragOverVisualFeedback(bool isValidDrop)
    {
        isDragOver = true;

        if (backgroundImage != null)
        {
            StopCurrentAnimation();
            Color feedbackColor = isValidDrop ? validDropColor : invalidDropColor;
            currentAnimation = backgroundImage.DOColor(feedbackColor, hoverAnimationDuration);
        }
    }

    /// <summary>
    /// FIXED: Clear drag-over visual feedback
    /// </summary>
    private void ClearDragOverVisualFeedback()
    {
        isDragOver = false;

        if (backgroundImage != null)
        {
            Color targetColor = GetCurrentTargetColor();
            StopCurrentAnimation();
            currentAnimation = backgroundImage.DOColor(targetColor, hoverAnimationDuration);
        }
    }

    #endregion

    /// <summary>
    /// Get item data from drag handler using reflection
    /// </summary>
    private InventoryItemData GetItemDataFromDragHandler(PlayerInventoryItemDragHandler dragHandler)
    {
        var field = dragHandler.GetType().GetField("itemData",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        return field?.GetValue(dragHandler) as InventoryItemData;
    }

    #region User Feedback Methods

    /// <summary>
    /// Show visual feedback for successful operations
    /// </summary>
    private void ShowSuccessFeedback()
    {
        if (backgroundImage != null)
        {
            StopCurrentAnimation();

            var originalColor = backgroundImage.color;
            backgroundImage.color = validDropColor;

            currentAnimation = backgroundImage.DOColor(originalColor, 0.3f).SetDelay(0.1f);
        }
    }

    /// <summary>
    /// ENHANCED: Show enhanced rejection feedback with detailed messaging
    /// </summary>
    private void ShowRejectionFeedback(string message, InventoryItemData itemData)
    {
        Debug.LogWarning($"ClothingSlotUI Rejection: {message}");

        if (backgroundImage != null)
        {
            StopCurrentAnimation();

            var originalColor = backgroundImage.color;
            var originalPosition = transform.localPosition;

            // Enhanced rejection animation - more noticeable shake and color
            backgroundImage.color = invalidDropColor;
            backgroundImage.DOColor(originalColor, 0.5f);

            // More pronounced shake for rejections vs regular errors
            currentAnimation = transform.DOShakePosition(errorShakeDuration * 1.5f, errorShakeStrength * 1.5f, 15, 90, false, true)
                .OnComplete(() => transform.localPosition = originalPosition);
        }

        // Could also trigger a UI message or sound effect here for rejection
        if (itemData?.ItemData != null)
        {
            Debug.Log($"[ClothingSlotUI] Rejected {itemData.ItemData.itemName}: {message}");
        }
    }

    /// <summary>
    /// Stop any current animation to prevent conflicts
    /// </summary>
    private void StopCurrentAnimation()
    {
        if (currentAnimation != null && currentAnimation.IsActive())
        {
            currentAnimation.Kill();
            currentAnimation = null;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Try to unequip item with better error handling and user feedback
    /// </summary>
    private void TryUnequipItem()
    {
        if (!isInitialized || clothingManager == null) return;

        var slot = clothingManager.GetSlot(targetLayer);
        if (slot == null || slot.IsEmpty)
        {
            //ShowErrorFeedback("No item equipped");
            return;
        }

        var equippedItem = slot.GetEquippedItem();
        if (equippedItem?.ItemData != null && !inventoryManager.HasSpaceForItem(equippedItem.ItemData))
        {
            //ShowErrorFeedback("Inventory full");
            return;
        }

        bool success = clothingManager.UnequipItemFromLayer(targetLayer);
        if (success)
        {
            Debug.Log($"Unequipped item from {targetLayer}");
            ShowSuccessFeedback();
            RefreshDisplay();
        }
        else
        {
            //ShowErrorFeedback("Unequip failed");
        }
    }

    /// <summary>
    /// Get the current target color for the background
    /// </summary>
    private Color GetCurrentTargetColor()
    {
        if (!isInitialized || clothingManager == null) return emptySlotColor;

        var slot = clothingManager.GetSlot(targetLayer);
        return (slot?.IsEmpty ?? true) ? emptySlotColor : occupiedSlotColor;
    }

    /// <summary>
    /// Animate the item being equipped
    /// </summary>
    private void AnimateItemEquipped()
    {
        if (itemImage != null)
        {
            itemImage.transform.localScale = Vector3.zero;
            itemImage.transform.DOScale(Vector3.one, equipAnimationDuration)
                .SetEase(Ease.OutBack);
        }
    }

    #endregion

    #region Clothing Event Handlers

    /// <summary>
    /// Handle item equipped events
    /// </summary>
    private void OnItemEquipped(ClothingSlot slot, InventoryItemData item)
    {
        if (slot.layer == targetLayer)
        {
            RefreshDisplay();
            AnimateItemEquipped();
        }
    }

    /// <summary>
    /// Handle item unequipped events
    /// </summary>
    private void OnItemUnequipped(ClothingSlot slot, string itemId)
    {
        if (slot.layer == targetLayer)
        {
            RefreshDisplay();
        }
    }

    /// <summary>
    /// Handle item swapped events
    /// </summary>
    private void OnItemSwapped(ClothingSlot slot, string oldItemId, string newItemId)
    {
        if (slot.layer == targetLayer)
        {
            RefreshDisplay();
            AnimateItemEquipped();
        }
    }

    /// <summary>
    /// Handle condition changes for equipped items
    /// </summary>
    private void OnConditionChanged(string itemId, float newCondition)
    {
        var slot = clothingManager?.GetSlot(targetLayer);
        if (slot != null && slot.equippedItemId == itemId)
        {
            UpdateConditionBar(slot);
        }
    }

    #endregion

    /// <summary>
    /// Debug logging helper
    /// </summary>
    private void DebugLog(string message)
    {
        Debug.Log($"[ClothingSlotUI:{targetLayer}] {message}");
    }

    /// <summary>
    /// Get debug information about this slot
    /// </summary>
    public string GetDebugInfo()
    {
        if (!isInitialized) return $"Slot[{name}]: Not initialized";

        var slot = clothingManager?.GetSlot(targetLayer);
        if (slot == null) return $"Slot[{targetLayer}]: No clothing slot found";

        return slot.GetDebugInfo();
    }
}