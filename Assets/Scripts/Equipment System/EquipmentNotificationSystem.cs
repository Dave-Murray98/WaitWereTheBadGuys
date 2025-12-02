using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// PHASE 3: Equipment notification system that shows toast messages for restriction feedback.
/// 
/// FEATURES:
/// - Toast notifications for blocked equipment attempts
/// - Current player state display
/// - Restriction reason explanations  
/// - Audio/visual feedback coordination
/// - Queue system for multiple notifications
/// 
/// NOTIFICATION TYPES:
/// - Equipment Restricted: Shows when items can't be equipped
/// - State Changed: Shows when player state changes
/// - Unarmed Activated: Shows when switching to unarmed
/// - General Messages: Custom restriction messages
/// </summary>
public class EquipmentNotificationSystem : MonoBehaviour
{
    public static EquipmentNotificationSystem Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TextMeshProUGUI notificationText;

    [Header("State Display")]
    [SerializeField] private GameObject stateDisplayPanel;
    [SerializeField] private TextMeshProUGUI currentStateText;

    [Header("Visual Settings")]
    [SerializeField] private float notificationDuration = 2f;
    [SerializeField] private float fadeInTime = 0.3f;
    [SerializeField] private float fadeOutTime = 0.3f;
    [SerializeField] private Vector3 slideInOffset = new Vector3(0, -50, 0);

    [Header("Colors and Icons")]
    [SerializeField] private Color restrictedColor = Color.red;
    [SerializeField] private Color infoColor = Color.blue;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color successColor = Color.green;


    [Header("Settings")]
    [SerializeField] private bool enableNotifications = false;
    [SerializeField] private bool enableStateDisplay = false;
    [SerializeField] private int maxQueuedNotifications = 5;
    [SerializeField] private bool enableDebugLogs = false;

    // System references
    private EquippedItemManager equippedItemManager;
    private PlayerStateManager playerStateManager;

    // Notification queue
    private Queue<NotificationData> notificationQueue = new Queue<NotificationData>();
    private bool isShowingNotification = false;

    // Animation tweeners
    private Tweener notificationTweener;
    private Vector3 originalNotificationPosition;

    // Current state tracking
    private PlayerStateType currentDisplayedState = PlayerStateType.Ground;

    #region Initialization

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        FindSystemReferences();
        SetupEventSubscriptions();
        InitializeUI();
    }

    private void InitializeSystem()
    {
        if (notificationPanel != null)
        {
            originalNotificationPosition = notificationPanel.transform.localPosition;
            notificationPanel.SetActive(false);
        }

        if (stateDisplayPanel != null)
        {
            stateDisplayPanel.SetActive(enableStateDisplay);
        }

        DebugLog("Equipment notification system initialized");
    }

    private void FindSystemReferences()
    {
        equippedItemManager = EquippedItemManager.Instance;
        playerStateManager = PlayerStateManager.Instance ?? FindFirstObjectByType<PlayerStateManager>();

        if (equippedItemManager == null)
            Debug.LogError("[EquipmentNotificationSystem] EquippedItemManager not found!");

        if (playerStateManager == null)
            Debug.LogError("[EquipmentNotificationSystem] PlayerStateManager not found!");

        DebugLog($"System references found - Equipment: {equippedItemManager != null}, State: {playerStateManager != null}");
    }

    private void SetupEventSubscriptions()
    {
        if (equippedItemManager != null)
        {
            equippedItemManager.OnEquipmentRestricted += OnEquipmentRestricted;
            equippedItemManager.OnStateRestrictionMessage += OnStateRestrictionMessage;
            equippedItemManager.OnUnarmedActivated += OnUnarmedActivated;
        }

        if (playerStateManager != null)
        {
            playerStateManager.OnStateChanged += OnPlayerStateChanged;
        }

        DebugLog("Event subscriptions complete");
    }

    private void InitializeUI()
    {
        UpdateStateDisplay();
    }

    #endregion

    #region Notification System

    /// <summary>
    /// Shows a notification with specified type and message
    /// </summary>
    public void ShowNotification(string message, NotificationType type = NotificationType.Info, float? duration = null)
    {
        if (!enableNotifications || string.IsNullOrEmpty(message)) return;

        var notification = new NotificationData
        {
            message = message,
            type = type,
            duration = duration ?? notificationDuration,
            timestamp = Time.time
        };

        QueueNotification(notification);
    }

    /// <summary>
    /// Queues a notification for display
    /// </summary>
    private void QueueNotification(NotificationData notification)
    {
        // Limit queue size
        while (notificationQueue.Count >= maxQueuedNotifications)
        {
            notificationQueue.Dequeue();
        }

        notificationQueue.Enqueue(notification);
        DebugLog($"Queued notification: {notification.message} [{notification.type}]");

        // Start processing if not already showing
        if (!isShowingNotification)
        {
            StartCoroutine(ProcessNotificationQueue());
        }
    }

    /// <summary>
    /// Processes the notification queue
    /// </summary>
    private IEnumerator ProcessNotificationQueue()
    {
        isShowingNotification = true;

        while (notificationQueue.Count > 0)
        {
            var notification = notificationQueue.Dequeue();
            yield return StartCoroutine(DisplayNotification(notification));
        }

        isShowingNotification = false;
    }

    /// <summary>
    /// Displays a single notification with animation
    /// </summary>
    private IEnumerator DisplayNotification(NotificationData notification)
    {
        if (notificationPanel == null || notificationText == null) yield break;

        // Setup notification content
        SetupNotificationVisuals(notification);

        // Show notification with animation
        yield return StartCoroutine(AnimateNotificationIn());

        // Wait for display duration
        yield return new WaitForSeconds(notification.duration);

        // Hide notification with animation
        yield return StartCoroutine(AnimateNotificationOut());

        DebugLog($"Displayed notification: {notification.message}");
    }

    /// <summary>
    /// Sets up the visual elements for a notification
    /// </summary>
    private void SetupNotificationVisuals(NotificationData notification)
    {
        // Set text
        if (notificationText != null)
        {
            notificationText.text = notification.message;
        }

        // Set colors and icon based on type
        Color backgroundColor = notification.type switch
        {
            NotificationType.Restricted => restrictedColor,
            NotificationType.Warning => warningColor,
            NotificationType.Success => successColor,
            _ => infoColor
        };
    }

    /// <summary>
    /// Animates notification sliding in
    /// </summary>
    private IEnumerator AnimateNotificationIn()
    {
        if (notificationPanel == null) yield break;

        notificationPanel.SetActive(true);

        // Start from offset position
        notificationPanel.transform.localPosition = originalNotificationPosition + slideInOffset;

        // Animate to original position
        notificationTweener?.Kill();
        notificationTweener = notificationPanel.transform.DOLocalMove(originalNotificationPosition, fadeInTime)
            .SetEase(Ease.OutBack);

        // Fade in
        var canvasGroup = notificationPanel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, fadeInTime);
        }

        yield return new WaitForSeconds(fadeInTime);
    }

    /// <summary>
    /// Animates notification sliding out
    /// </summary>
    private IEnumerator AnimateNotificationOut()
    {
        if (notificationPanel == null) yield break;

        // Animate to offset position
        notificationTweener?.Kill();
        notificationTweener = notificationPanel.transform.DOLocalMove(originalNotificationPosition + slideInOffset, fadeOutTime)
            .SetEase(Ease.InBack);

        // Fade out
        var canvasGroup = notificationPanel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, fadeOutTime);
        }

        yield return new WaitForSeconds(fadeOutTime);

        notificationPanel.SetActive(false);
    }

    #endregion

    #region State Display

    /// <summary>
    /// Updates the current player state display
    /// </summary>
    private void UpdateStateDisplay()
    {
        if (!enableStateDisplay || playerStateManager == null) return;

        var currentState = playerStateManager.CurrentStateType;
        if (currentState == currentDisplayedState) return;

        currentDisplayedState = currentState;

        // Update state text
        if (currentStateText != null)
        {
            string stateDisplayName = playerStateManager.GetCurrentStateDisplayName();
            currentStateText.text = stateDisplayName;
        }

        // Show state display panel
        if (stateDisplayPanel != null)
        {
            stateDisplayPanel.SetActive(true);
        }

        DebugLog($"Updated state display: {currentDisplayedState}");
    }

    #endregion

    #region Event Handlers

    private void OnEquipmentRestricted(string itemName, string reason)
    {
        string message = $"Cannot equip {itemName}\n{reason}";
        ShowNotification(message, NotificationType.Restricted);
        DebugLog($"Equipment restricted: {itemName} - {reason}");
    }

    private void OnStateRestrictionMessage(string message)
    {
        ShowNotification(message, NotificationType.Warning);
        DebugLog($"State restriction: {message}");
    }

    private void OnUnarmedActivated()
    {
        ShowNotification("Unarmed combat active", NotificationType.Success, 1.5f);
        DebugLog("Unarmed activated notification shown");
    }

    private void OnPlayerStateChanged(PlayerStateType previousState, PlayerStateType newState)
    {
        UpdateStateDisplay();

        // Show state change notification
        string stateDisplayName = playerStateManager?.GetCurrentStateDisplayName() ?? "Unknown State";
        ShowNotification($"Player state: {stateDisplayName}", NotificationType.Info, 1.5f);

        DebugLog($"Player state changed: {previousState} -> {newState}");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Shows a custom restriction message
    /// </summary>
    public void ShowRestrictionMessage(string itemName, string reason)
    {
        OnEquipmentRestricted(itemName, reason);
    }

    /// <summary>
    /// Shows a general information message
    /// </summary>
    public void ShowInfoMessage(string message)
    {
        ShowNotification(message, NotificationType.Info);
    }

    /// <summary>
    /// Shows a warning message
    /// </summary>
    public void ShowWarningMessage(string message)
    {
        ShowNotification(message, NotificationType.Warning);
    }

    /// <summary>
    /// Shows a success message
    /// </summary>
    public void ShowSuccessMessage(string message)
    {
        ShowNotification(message, NotificationType.Success);
    }

    /// <summary>
    /// Enables or disables notifications
    /// </summary>
    public void SetNotificationsEnabled(bool enabled)
    {
        enableNotifications = enabled;
        DebugLog($"Notifications {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Enables or disables state display
    /// </summary>
    public void SetStateDisplayEnabled(bool enabled)
    {
        enableStateDisplay = enabled;
        if (stateDisplayPanel != null)
        {
            stateDisplayPanel.SetActive(enabled);
        }
        DebugLog($"State display {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Clears all queued notifications
    /// </summary>
    public void ClearNotificationQueue()
    {
        notificationQueue.Clear();
        DebugLog("Notification queue cleared");
    }

    /// <summary>
    /// Forces an immediate state display update
    /// </summary>
    public void RefreshStateDisplay()
    {
        currentDisplayedState = PlayerStateType.Ground; // Force refresh
        UpdateStateDisplay();
    }

    #endregion

    #region Utility

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EquipmentNotificationSystem] {message}");
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        // Clean up tweeners
        notificationTweener?.Kill();

        // Unsubscribe from events
        if (equippedItemManager != null)
        {
            equippedItemManager.OnEquipmentRestricted -= OnEquipmentRestricted;
            equippedItemManager.OnStateRestrictionMessage -= OnStateRestrictionMessage;
            equippedItemManager.OnUnarmedActivated -= OnUnarmedActivated;
        }

        if (playerStateManager != null)
        {
            playerStateManager.OnStateChanged -= OnPlayerStateChanged;
        }
    }

    #endregion
}

/// <summary>
/// Data structure for notification information
/// </summary>
[System.Serializable]
public class NotificationData
{
    public string message;
    public NotificationType type;
    public float duration;
    public float timestamp;
}

/// <summary>
/// Types of notifications for different visual styles
/// </summary>
public enum NotificationType
{
    Info,       // General information (blue)
    Warning,    // Warnings and cautions (yellow)  
    Restricted, // Equipment restrictions (red)
    Success     // Successful actions (green)
}