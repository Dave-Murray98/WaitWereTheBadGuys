using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Manages the UI for interaction prompts
/// Integrates with the existing UIManager system
/// </summary>
public class InteractionUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject interactionPromptPanel;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private Image promptBackground;
    [SerializeField] private CanvasGroup promptCanvasGroup;

    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.15f;
    [SerializeField] private Ease fadeEase = Ease.OutQuad;

    [Header("Positioning")]
    [SerializeField] private bool followTarget = false;
    [SerializeField] private Vector3 worldOffset = Vector3.up * 2f;
    [SerializeField] private Vector2 screenOffset = new Vector2(0, 100);

    [Header("Styling")]
    [SerializeField] private string promptPrefix = "Press E to ";
    [SerializeField] private Color defaultTextColor = Color.white;
    [SerializeField] private Color defaultBackgroundColor = new Color(0, 0, 0, 0.7f);

    // State
    // private PlayerInteractionController playerInteractionController;
    private PlayerInteractionDetector interactionDetector;
    private IInteractable currentDisplayedInteractable;
    private Tweener currentTween;
    private Camera playerCamera;
    private bool isVisible = true;

    private void Awake()
    {
        SetupUI();
    }

    private void Start()
    {
        // Find player components directly
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            // // Direct access to interaction components
            // playerInteractionController = player.interactionController;
            interactionDetector = player.GetComponent<PlayerInteractionDetector>();
            playerCamera = Camera.main; // or get from PlayerCamera component
        }

        // Subscribe to interaction events
        if (interactionDetector != null)
        {
            interactionDetector.OnBestInteractableChanged += OnBestInteractableChanged;
        }

        // Initially hide the prompt
        HidePrompt(true);
    }

    private void SetupUI()
    {
        // Create UI elements if they don't exist
        if (interactionPromptPanel == null)
        {
            CreatePromptUI();
        }

        // Ensure we have required components
        if (promptCanvasGroup == null && interactionPromptPanel != null)
        {
            promptCanvasGroup = interactionPromptPanel.GetComponent<CanvasGroup>();
            if (promptCanvasGroup == null)
            {
                promptCanvasGroup = interactionPromptPanel.AddComponent<CanvasGroup>();
            }
        }
    }

    private void CreatePromptUI()
    {
        // Find or create canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No Canvas found for interaction UI");
            return;
        }

        // Create prompt panel
        GameObject panel = new GameObject("InteractionPromptPanel");
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(200, 60);
        panelRect.anchoredPosition = Vector2.zero;

        // Add background
        Image background = panel.AddComponent<Image>();
        background.color = defaultBackgroundColor;
        background.raycastTarget = false;

        // Add canvas group for fading
        CanvasGroup canvasGroup = panel.AddComponent<CanvasGroup>();

        // Create text
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(panel.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Press E to interact";
        text.color = defaultTextColor;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 16;
        text.raycastTarget = false;

        // Assign references
        interactionPromptPanel = panel;
        promptBackground = background;
        promptCanvasGroup = canvasGroup;
        promptText = text;
    }

    private void Update()
    {
        if (isVisible && followTarget && currentDisplayedInteractable != null)
        {
            UpdatePromptPosition();
        }
    }

    private void OnBestInteractableChanged(IInteractable newInteractable)
    {
        if (newInteractable != currentDisplayedInteractable)
        {
            if (newInteractable != null)
            {
                ShowPromptForInteractable(newInteractable);
            }
            else
            {
                HidePrompt();
            }
        }
    }

    private void ShowPromptForInteractable(IInteractable interactable)
    {
        currentDisplayedInteractable = interactable;

        // Update prompt text
        string prompt = interactable.GetInteractionPrompt();
        if (promptText != null)
        {
            // Add prefix if the prompt doesn't already have it
            if (!prompt.ToLower().Contains("press") && !string.IsNullOrEmpty(promptPrefix))
            {
                prompt = promptPrefix + prompt.ToLower();
            }
            promptText.text = prompt;
        }

        // Position the prompt
        UpdatePromptPosition();

        // Show with animation
        ShowPrompt();
    }

    private void UpdatePromptPosition()
    {
        if (currentDisplayedInteractable == null || interactionPromptPanel == null) return;

        if (followTarget)
        {
            // Position relative to the interactable in world space
            Vector3 worldPosition = currentDisplayedInteractable.Transform.position + worldOffset;
            Vector3 screenPosition = playerCamera.WorldToScreenPoint(worldPosition);

            // Add screen offset
            screenPosition += new Vector3(screenOffset.x, screenOffset.y, 0);

            // Convert to canvas space
            RectTransform canvasRect = interactionPromptPanel.transform.parent as RectTransform;
            if (canvasRect != null)
            {
                Vector2 canvasPosition;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPosition, playerCamera, out canvasPosition);

                RectTransform promptRect = interactionPromptPanel.GetComponent<RectTransform>();
                promptRect.anchoredPosition = canvasPosition;
            }
        }
        else
        {
            // Fixed position with screen offset
            RectTransform promptRect = interactionPromptPanel.GetComponent<RectTransform>();
            promptRect.anchoredPosition = screenOffset;
        }
    }

    private void ShowPrompt()
    {
        if (interactionPromptPanel == null || isVisible) return;

        isVisible = true;
        interactionPromptPanel.SetActive(true);

        // Kill any existing tween
        currentTween?.Kill();

        // Fade in
        if (promptCanvasGroup != null)
        {
            promptCanvasGroup.alpha = 0f;
            currentTween = promptCanvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeEase);
        }
    }

    private void HidePrompt(bool immediate = false)
    {
        if (!isVisible || interactionPromptPanel == null) return;

        currentDisplayedInteractable = null;

        // Kill any existing tween
        currentTween?.Kill();

        if (immediate)
        {
            isVisible = false;
            interactionPromptPanel.SetActive(false);
            if (promptCanvasGroup != null)
            {
                promptCanvasGroup.alpha = 0f;
            }
        }
        else
        {
            // Fade out
            if (promptCanvasGroup != null)
            {
                currentTween = promptCanvasGroup.DOFade(0f, fadeOutDuration)
                    .SetEase(fadeEase)
                    .OnComplete(() =>
                    {
                        isVisible = false;
                        interactionPromptPanel.SetActive(false);
                    });
            }
            else
            {
                isVisible = false;
                interactionPromptPanel.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Manually show a prompt with custom text
    /// </summary>
    public void ShowCustomPrompt(string text, Vector3? worldPosition = null)
    {
        if (promptText != null)
        {
            promptText.text = text;
        }

        if (worldPosition.HasValue)
        {
            // Position at specific world position
            Vector3 screenPosition = playerCamera.WorldToScreenPoint(worldPosition.Value + worldOffset);
            screenPosition += new Vector3(screenOffset.x, screenOffset.y, 0);

            RectTransform canvasRect = interactionPromptPanel.transform.parent as RectTransform;
            if (canvasRect != null)
            {
                Vector2 canvasPosition;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPosition, playerCamera, out canvasPosition);

                RectTransform promptRect = interactionPromptPanel.GetComponent<RectTransform>();
                promptRect.anchoredPosition = canvasPosition;
            }
        }

        ShowPrompt();
    }

    /// <summary>
    /// Hide any currently displayed prompt
    /// </summary>
    public void HideCurrentPrompt()
    {
        HidePrompt();
    }

    /// <summary>
    /// Check if a prompt is currently visible
    /// </summary>
    public bool IsPromptVisible => isVisible;

    private void OnDestroy()
    {
        // Clean up tweens
        currentTween?.Kill();

        // Unsubscribe from events
        if (interactionDetector != null)
        {
            interactionDetector.OnBestInteractableChanged -= OnBestInteractableChanged;
        }
    }
}