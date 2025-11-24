using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;


/// <summary>
/// Manages loading screen display during scene transitions and save operations.
/// Provides smooth progress updates, contextual messages, and coordinated timing
/// with SceneTransitionManager for seamless user experience.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    [Header("Loading Screen UI")]
    public Canvas loadingScreenCanvas;
    public GameObject loadingPanel;
    public Slider progressSlider;
    public TextMeshProUGUI loadingText;
    public TextMeshProUGUI progressText;

    [Header("Loading Settings")]
    public float minimumLoadTime = 1f;
    public float progressSmoothSpeed = 2f;

    [Header("Loading Messages")]
    public string[] doorwayMessages = {
        "Transitioning...",
        "Loading new area...",
        "Preparing environment..."
    };

    public string[] saveLoadMessages = {
        "Loading save data...",
        "Restoring game state...",
        "Rebuilding world..."
    };

    // Internal state tracking
    private float targetProgress = 0f;
    private float currentProgress = 0f;
    private bool isLoading = false;
    private Coroutine loadingCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeLoadingScreen();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Initializes loading screen to hidden state with reset progress.
    /// </summary>
    private void InitializeLoadingScreen()
    {
        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.gameObject.SetActive(false);
        }

        if (progressSlider != null)
        {
            progressSlider.value = 0f;
        }
    }

    private void Update()
    {
        if (isLoading && progressSlider != null)
        {
            // Smooth progress bar animation
            currentProgress = Mathf.Lerp(currentProgress, targetProgress, progressSmoothSpeed * Time.unscaledDeltaTime);
            progressSlider.value = currentProgress;

            // Update progress percentage text
            if (progressText != null)
            {
                progressText.text = $"{Mathf.RoundToInt(currentProgress * 100)}%";
            }
        }
    }

    /// <summary>
    /// Shows loading screen with doorway-specific messaging.
    /// </summary>
    public void ShowLoadingScreenForDoorway(string targetScene)
    {
        ShowLoadingScreen(GetRandomMessage(doorwayMessages), $"Entering {targetScene}");
    }

    /// <summary>
    /// Shows loading screen with save/load specific messaging.
    /// </summary>
    public void ShowLoadingScreenForSaveLoad(string targetScene)
    {
        ShowLoadingScreen(GetRandomMessage(saveLoadMessages), $"Loading {targetScene}");
    }

    /// <summary>
    /// Shows loading screen with custom messages and resets progress.
    /// </summary>
    public void ShowLoadingScreen(string mainMessage = "Loading...", string subMessage = "")
    {
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
        }

        isLoading = true;
        currentProgress = 0f;
        targetProgress = 0f;

        // Show UI elements
        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.gameObject.SetActive(true);
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        // Set display messages
        if (loadingText != null)
        {
            loadingText.text = string.IsNullOrEmpty(subMessage) ? mainMessage : $"{mainMessage}\n{subMessage}";
        }

        // Reset progress display
        if (progressSlider != null)
        {
            progressSlider.value = 0f;
        }

        if (progressText != null)
        {
            progressText.text = "0%";
        }

        Debug.Log($"Loading screen shown: {mainMessage}");
    }

    /// <summary>
    /// Updates the target progress value (0-1). Actual progress smoothly interpolates to this value.
    /// </summary>
    public void SetProgress(float progress)
    {
        targetProgress = Mathf.Clamp01(progress);
    }

    /// <summary>
    /// Hides the loading screen with final progress display and timing.
    /// </summary>
    public void HideLoadingScreen()
    {
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
        }

        loadingCoroutine = StartCoroutine(HideLoadingScreenCoroutine());
    }

    /// <summary>
    /// Coroutine that ensures 100% is briefly displayed before hiding the loading screen.
    /// </summary>
    private IEnumerator HideLoadingScreenCoroutine()
    {
        // Show 100% completion briefly
        SetProgress(1f);
        yield return new WaitForSecondsRealtime(0.3f);

        // Hide UI elements
        isLoading = false;

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.gameObject.SetActive(false);
        }

        Debug.Log("Loading screen hidden");
    }

    /// <summary>
    /// Returns whether a loading operation is currently active.
    /// </summary>
    public bool IsLoading => isLoading;

    /// <summary>
    /// Selects a random message from the provided array, with fallback.
    /// </summary>
    private string GetRandomMessage(string[] messages)
    {
        if (messages == null || messages.Length == 0)
            return "Loading...";

        return messages[Random.Range(0, messages.Length)];
    }
}