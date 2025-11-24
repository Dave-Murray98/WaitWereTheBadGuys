using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple manager that creates and configures the inventory dropdown menu
/// Much cleaner approach without unnecessary complexity
/// </summary>
public class InventoryDropdownManager : MonoBehaviour
{
    [Header("Dropdown Settings")]
    [SerializeField] private GameObject dropdownMenuPrefab;
    [SerializeField] private Transform dropdownMenuParent;


    [Header("Styling")]
    [SerializeField] private Color menuBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
    [SerializeField] private Color normalButtonColor = new Color(0.95f, 0.95f, 0.95f);
    [SerializeField] private Color hoverButtonColor = new Color(0.8f, 0.8f, 1f);
    [SerializeField] private Color disabledButtonColor = new Color(0.6f, 0.6f, 0.6f);

    [Header("Layout Settings")]
    [SerializeField] private float buttonHeight = 35f;
    [SerializeField] private float buttonSpacing = 5f;
    [SerializeField] private float containerPadding = 15f;
    [SerializeField] private float minWidth = 140f;

    [Header("Position Settings")]
    [SerializeField] private Vector2 positionOffset = new Vector2(10f, -10f);
    [Tooltip("Offset from the click position where the dropdown will appear. Positive X = right, Positive Y = up")]

    private InventoryDropdownMenu dropdownInstance;

    private void Start()
    {
        CreateDropdownMenu();
    }

    /// <summary>
    /// Create the dropdown menu with proper layout configuration
    /// </summary>
    private void CreateDropdownMenu()
    {
        GameObject dropdownObj;

        if (dropdownMenuPrefab != null)
        {
            dropdownObj = Instantiate(dropdownMenuPrefab, dropdownMenuParent);
        }
        else
        {
            dropdownObj = CreateDefaultDropdownMenu();
        }

        dropdownInstance = dropdownObj.GetComponent<InventoryDropdownMenu>();

        if (dropdownInstance == null)
        {
            dropdownInstance = dropdownObj.AddComponent<InventoryDropdownMenu>();
        }

        ConfigureDropdownMenu();
        //        Debug.Log("Fixed dropdown menu created and configured");
    }

    /// <summary>
    /// Create a properly configured dropdown menu
    /// FIXED: Better layout group configuration
    /// </summary>
    private GameObject CreateDefaultDropdownMenu()
    {
        // Main dropdown panel
        GameObject dropdownPanel = new GameObject("InventoryDropdownMenu");
        dropdownPanel.transform.SetParent(transform, false);

        var rectTransform = dropdownPanel.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(minWidth, 200f); // Start with reasonable size
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0, 1);

        // Background
        var backgroundImage = dropdownPanel.AddComponent<Image>();
        backgroundImage.color = menuBackgroundColor;

        // Canvas group for fading
        var canvasGroup = dropdownPanel.AddComponent<CanvasGroup>();

        // Button container with proper layout
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(dropdownPanel.transform, false);

        var containerRect = buttonContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = new Vector2(containerPadding, containerPadding);
        containerRect.offsetMax = new Vector2(-containerPadding, -containerPadding);

        // FIXED: Better layout group configuration
        var layoutGroup = buttonContainer.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = buttonSpacing;
        layoutGroup.childControlHeight = true;  // CHANGED: Allow height control
        layoutGroup.childControlWidth = true;   // Control width too
        layoutGroup.childForceExpandHeight = false; // Don't force expand
        layoutGroup.childForceExpandWidth = true;   // Do force expand width
        layoutGroup.childAlignment = TextAnchor.UpperCenter;

        // REMOVED: ContentSizeFitter that was causing issues
        // The dropdown will manage its own size

        // Set up the dropdown menu component
        var dropdownMenu = dropdownPanel.AddComponent<InventoryDropdownMenu>();

        // IMPORTANT: Start with the dropdown inactive
        dropdownPanel.SetActive(false);

        // Set references via reflection (temporary until we make fields public)
        SetPrivateField(dropdownMenu, "dropdownPanel", dropdownPanel);
        SetPrivateField(dropdownMenu, "buttonContainer", buttonContainer.transform);
        SetPrivateField(dropdownMenu, "canvasGroup", canvasGroup);
        SetPrivateField(dropdownMenu, "buttonHeight", buttonHeight);
        SetPrivateField(dropdownMenu, "buttonSpacing", buttonSpacing);
        SetPrivateField(dropdownMenu, "containerPadding", containerPadding);
        SetPrivateField(dropdownMenu, "minWidth", minWidth);
        SetPrivateField(dropdownMenu, "positionOffset", positionOffset);

        return dropdownPanel;
    }

    /// <summary>
    /// Configure the dropdown menu styling
    /// </summary>
    private void ConfigureDropdownMenu()
    {
        if (dropdownInstance == null) return;

        // Apply styling via reflection (temporary)
        SetPrivateField(dropdownInstance, "normalButtonColor", normalButtonColor);
        SetPrivateField(dropdownInstance, "hoverButtonColor", hoverButtonColor);
        SetPrivateField(dropdownInstance, "disabledButtonColor", disabledButtonColor);

        // Set layout properties
        dropdownInstance.positionOffset = positionOffset;

        // Debug log to verify offset is being set
        //        Debug.Log($"[DropdownManager] Setting position offset to: {positionOffset}");
    }

    /// <summary>
    /// Get the dropdown menu instance (creates if needed)
    /// </summary>
    public InventoryDropdownMenu DropdownMenu
    {
        get
        {
            if (dropdownInstance == null)
                CreateDropdownMenu();
            return dropdownInstance;
        }
    }

    /// <summary>
    /// Helper method to set private fields via reflection
    /// </summary>
    private void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(target, value);
    }

    /// <summary>
    /// Register dropdown with all existing drag handlers
    /// </summary>
    public void RegisterWithDragHandlers()
    {
        var dragHandlers = FindObjectsByType<PlayerInventoryItemDragHandler>(FindObjectsSortMode.None);

        foreach (var handler in dragHandlers)
        {
            SetPrivateField(handler, "dropdownMenu", DropdownMenu);
        }

        // Debug.Log($"Registered dropdown with {dragHandlers.Length} drag handlers");
    }

    private void OnEnable()
    {
        // Register with drag handlers when enabled
        if (dropdownInstance != null)
        {
            RegisterWithDragHandlers();
        }
    }

    /// <summary>
    /// Update the position offset for all existing dropdown instances
    /// </summary>
    public void UpdatePositionOffset(Vector2 newOffset)
    {
        positionOffset = newOffset;
        if (dropdownInstance != null)
        {
            SetPrivateField(dropdownInstance, "positionOffset", positionOffset);
            // Debug.Log($"[DropdownManager] Updated position offset to: {newOffset}");
        }
    }

    /// <summary>
    /// Debug method to check the current offset setting
    /// </summary>
    [Sirenix.OdinInspector.Button("Debug Current Offset")]
    private void DebugCurrentOffset()
    {
        Debug.Log($"[DropdownManager] Current position offset: {positionOffset}");
        if (dropdownInstance != null)
        {
            var currentOffset = GetPrivateField(dropdownInstance, "positionOffset");
            Debug.Log($"[DropdownManager] Dropdown instance offset: {currentOffset}");
        }
        else
        {
            Debug.Log("[DropdownManager] No dropdown instance exists yet");
        }
    }

    /// <summary>
    /// Helper method to get private fields via reflection
    /// </summary>
    private object GetPrivateField(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(target);
    }
}