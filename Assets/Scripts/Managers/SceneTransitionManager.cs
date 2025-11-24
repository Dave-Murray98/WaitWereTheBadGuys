using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UPDATED: SceneTransitionManager now works with the new PlayerStateManager system.
/// Removed references to old PlayerController validation methods and updated to use
/// PlayerStateManager for state validation after doorway transitions.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    private static WaitForSecondsRealtime _waitForSecondsRealtime0_2 = new WaitForSecondsRealtime(0.2f);

    public static SceneTransitionManager Instance { get; private set; }

    [Header("Transition Settings")]
    public float minLoadingTime = 1f;
    public bool showDebugLogs = true;

    [Header("Validation Settings")]
    [SerializeField] private bool enableDoorwayValidation = true;
    [SerializeField] private float doorwayValidationDelay = 0.2f;

    // Current transition state tracking
    private RestoreContext currentRestoreContext = RestoreContext.DoorwayTransition;
    private string pendingTargetDoorwayID = "";
    private Dictionary<string, object> pendingSaveData = null;

    // Events for external systems
    public System.Action<string> OnTransitionStarted;
    public System.Action<string> OnTransitionCompleted;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // This is the only system that should subscribe to scene loaded events
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// Handles player movement through doorways/portals between scenes.
    /// Preserves player stats and inventory but lets the doorway set position.
    /// </summary>
    public void TransitionThroughDoorway(string targetScene, string targetDoorwayID)
    {
        DebugLog($"Starting doorway transition to {targetScene}, doorway: {targetDoorwayID}");

        // Save current player state before transitioning
        if (PlayerPersistenceManager.Instance != null)
        {
            DebugLog("Saving player data before doorway transition");
            PlayerPersistenceManager.Instance.UpdatePersistentPlayerDataForTransition();
        }
        else
        {
            Debug.LogError("PlayerPersistenceManager not found - player data will not persist!");
        }

        currentRestoreContext = RestoreContext.DoorwayTransition;
        pendingTargetDoorwayID = targetDoorwayID;
        pendingSaveData = null;

        StartCoroutine(DoTransitionWithLoading(targetScene, RestoreContext.DoorwayTransition));
    }

    /// <summary>
    /// Loads a game from a save file, transitioning to the saved scene with complete state restoration.
    /// Restores everything including exact player position and world state.
    /// </summary>
    public void LoadSceneFromSave(string targetScene, Dictionary<string, object> saveData = null)
    {
        DebugLog($"Starting save file load to {targetScene}");

        currentRestoreContext = RestoreContext.SaveFileLoad;
        pendingTargetDoorwayID = "";
        pendingSaveData = saveData;

        StartCoroutine(DoTransitionWithLoading(targetScene, RestoreContext.SaveFileLoad));
    }

    /// <summary>
    /// Starts a new game in the specified starting scene with default values.
    /// Clears all persistent data and initializes fresh game state.
    /// </summary>
    public void StartNewGame(string startingScene)
    {
        DebugLog($"Starting new game in {startingScene}");

        currentRestoreContext = RestoreContext.NewGame;
        pendingTargetDoorwayID = "";
        pendingSaveData = null;

        StartCoroutine(DoTransitionWithLoading(startingScene, RestoreContext.NewGame));
    }

    /// <summary>
    /// Manages the complete scene loading process with loading screen and proper timing.
    /// Ensures minimum load time for smooth user experience.
    /// </summary>
    private System.Collections.IEnumerator DoTransitionWithLoading(string targetScene, RestoreContext context)
    {
        OnTransitionStarted?.Invoke(targetScene);
        DebugLog($"Starting {context} transition to {targetScene}");

        // Show appropriate loading screen based on context
        switch (context)
        {
            case RestoreContext.DoorwayTransition:
                LoadingScreenManager.Instance?.ShowLoadingScreenForDoorway(targetScene);
                break;
            case RestoreContext.SaveFileLoad:
                LoadingScreenManager.Instance?.ShowLoadingScreenForSaveLoad(targetScene);
                break;
            case RestoreContext.NewGame:
                LoadingScreenManager.Instance?.ShowLoadingScreen("Starting New Game...", "Loading world");
                break;
        }

        // Prepare systems for transition
        if (context == RestoreContext.DoorwayTransition)
        {
            SceneDataManager.Instance?.PrepareSceneTransition(targetScene, pendingTargetDoorwayID, RestoreContext.DoorwayTransition);
        }

        // Staged loading with progress updates
        LoadingScreenManager.Instance?.SetProgress(0.1f);
        yield return new WaitForSecondsRealtime(0.2f);

        LoadingScreenManager.Instance?.SetProgress(0.2f);
        yield return new WaitForSecondsRealtime(0.1f);

        LoadingScreenManager.Instance?.SetProgress(0.3f);

        // Start actual scene loading
        var sceneLoadOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(targetScene);
        sceneLoadOperation.allowSceneActivation = false;

        // Monitor loading progress
        while (sceneLoadOperation.progress < 0.9f)
        {
            float progress = Mathf.Lerp(0.3f, 0.8f, sceneLoadOperation.progress / 0.9f);
            LoadingScreenManager.Instance?.SetProgress(progress);
            yield return null;
        }

        LoadingScreenManager.Instance?.SetProgress(0.8f);

        // Ensure minimum loading time for UX
        float loadStartTime = Time.unscaledTime;
        while (Time.unscaledTime - loadStartTime < minLoadingTime)
        {
            float timeProgress = (Time.unscaledTime - loadStartTime) / minLoadingTime;
            float progress = Mathf.Lerp(0.8f, 0.95f, timeProgress);
            LoadingScreenManager.Instance?.SetProgress(progress);
            yield return null;
        }

        // Activate the scene
        LoadingScreenManager.Instance?.SetProgress(1f);
        sceneLoadOperation.allowSceneActivation = true;
        yield return sceneLoadOperation;

        OnTransitionCompleted?.Invoke(targetScene);
        DebugLog($"Completed {context} transition to {targetScene}");

        yield return _waitForSecondsRealtime0_2;
        LoadingScreenManager.Instance.HideLoadingScreen();
    }

    /// <summary>
    /// Central handler for all scene loaded events. Routes to appropriate restoration
    /// method based on the current transition context. This ensures proper data
    /// restoration order and prevents conflicts between systems.
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        DebugLog($"=== SCENE LOADED: {scene.name} (Context: {currentRestoreContext}) ===");
        StartCoroutine(OrchestateDataRestoration(scene.name));
    }

    /// <summary>
    /// Orchestrates data restoration based on transition context.
    /// Each context requires different restoration behavior and timing.
    /// </summary>
    private System.Collections.IEnumerator OrchestateDataRestoration(string sceneName)
    {
        yield return new WaitForSecondsRealtime(0.1f);

        DebugLog($"Starting data restoration for context: {currentRestoreContext}");

        switch (currentRestoreContext)
        {
            case RestoreContext.DoorwayTransition:
                yield return HandleDoorwayTransitionRestore(sceneName);
                break;

            case RestoreContext.SaveFileLoad:
                yield return HandleSaveFileLoadRestore(sceneName);
                break;

            case RestoreContext.NewGame:
                yield return HandleNewGameRestore(sceneName);
                break;
        }

        DebugLog($"Data restoration complete for context: {currentRestoreContext}");
    }

    /// <summary>
    /// UPDATED: Handles data restoration for doorway transitions with PlayerStateManager validation.
    /// Restores player stats but not position, restores scene objects, positions player,
    /// then validates movement mode consistency through PlayerStateManager.
    /// </summary>
    private System.Collections.IEnumerator HandleDoorwayTransitionRestore(string sceneName)
    {
        DebugLog("=== UPDATED DOORWAY TRANSITION RESTORATION ===");

        // 1. Restore player data (excluding position)
        if (PlayerPersistenceManager.Instance != null)
        {
            DebugLog("Restoring player data for doorway transition (no position)");
            PlayerPersistenceManager.Instance.RestoreForDoorwayTransition();
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 2. Restore scene-specific objects
        if (SceneDataManager.Instance != null)
        {
            DebugLog("Restoring scene data for doorway transition");
            SceneDataManager.Instance.RestoreSceneDataForTransition(sceneName);
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 3. Position player at target doorway
        if (!string.IsNullOrEmpty(pendingTargetDoorwayID))
        {
            DebugLog($"Positioning player at doorway: {pendingTargetDoorwayID}");
            PositionPlayerAtDoorway(pendingTargetDoorwayID);
        }

        // 4. UPDATED: Force validate movement state after positioning using PlayerStateManager
        if (enableDoorwayValidation)
        {
            yield return new WaitForSecondsRealtime(doorwayValidationDelay);
            ForceValidatePlayerStateAfterDoorway();
        }

        DebugLog("Updated doorway transition restoration complete");
    }

    /// <summary>
    /// Handles complete state restoration from save files.
    /// Restores all data including exact positions and scene states.
    /// </summary>
    private System.Collections.IEnumerator HandleSaveFileLoadRestore(string sceneName)
    {
        DebugLog("=== SAVE FILE LOAD RESTORATION ===");

        // 1. Restore complete player data including position
        if (PlayerPersistenceManager.Instance != null)
        {
            DebugLog("Restoring player data from save file (including position)");
            PlayerPersistenceManager.Instance.RestoreFromSaveFile(pendingSaveData);
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 2. Restore scene data from save file
        if (SceneDataManager.Instance != null && pendingSaveData != null)
        {
            DebugLog("Restoring scene data from save file");
            SceneDataManager.Instance.RestoreSceneDataFromSave(pendingSaveData);
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 3. Refresh UI systems
        if (GameManager.Instance?.uiManager != null)
        {
            DebugLog("Refreshing UI after save load");
            GameManager.Instance.uiManager.RefreshReferences();
        }

        // 4. Ensure game is unpaused
        if (GameManager.Instance != null && GameManager.Instance.isPaused)
        {
            DebugLog("Unpausing game after save load");
            GameManager.Instance.ResumeGame();
        }
    }

    /// <summary>
    /// Initializes fresh game state for new games.
    /// Sets default values and clears any existing persistent data.
    /// </summary>
    private System.Collections.IEnumerator HandleNewGameRestore(string sceneName)
    {
        DebugLog("=== NEW GAME INITIALIZATION ===");

        // 1. Initialize player with default values
        if (PlayerPersistenceManager.Instance != null)
        {
            DebugLog("Initializing player for new game");
            PlayerPersistenceManager.Instance.InitializeForNewGame();
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 2. Clear any existing scene data
        if (SceneDataManager.Instance != null)
        {
            DebugLog("Clearing scene data for new game");
            SceneDataManager.Instance.ClearAllSceneData();
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 3. Update UI systems
        if (GameManager.Instance?.uiManager != null)
        {
            DebugLog("Refreshing UI for new game");
            GameManager.Instance.uiManager.RefreshReferences();
        }
    }

    /// <summary>
    /// Locates and positions the player at the specified doorway in the current scene.
    /// Used after doorway transitions to place the player at the correct entrance.
    /// </summary>
    private void PositionPlayerAtDoorway(string doorwayID)
    {
        if (string.IsNullOrEmpty(doorwayID))
        {
            DebugLog("No target doorway specified");
            return;
        }

        Doorway[] doorways = FindObjectsByType<Doorway>(FindObjectsSortMode.None);
        Doorway targetDoorway = System.Array.Find(doorways, d => d.doorwayID == doorwayID);

        if (targetDoorway != null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                // Clean velocity before positioning
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    DebugLog("Cleared velocity before doorway positioning");
                }

                Vector3 doorwayPosition = targetDoorway.transform.position + Vector3.up * 0.1f;
                player.transform.position = doorwayPosition;
                DebugLog($"Positioned player at doorway: {doorwayID} ({doorwayPosition})");
            }
            else
            {
                Debug.LogWarning("Player not found - cannot position at doorway");
            }
        }
        else
        {
            Debug.LogWarning($"Target doorway not found: {doorwayID}");
        }
    }

    /// <summary>
    /// UPDATED: Forces validation of player state after doorway positioning using PlayerStateManager
    /// Ensures player state is consistent with the new environment
    /// </summary>
    private void ForceValidatePlayerStateAfterDoorway()
    {
        // Try to find PlayerStateManager first (preferred method)
        var playerStateManager = FindFirstObjectByType<PlayerStateManager>();
        if (playerStateManager != null)
        {
            DebugLog("Validating player state after doorway transition using PlayerStateManager");
            playerStateManager.ForceStateValidation();
            return;
        }

        // Fallback: try to validate through PlayerController
        var playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            DebugLog("Validating player state after doorway transition using PlayerController");
            playerController.ValidatePlayerState();
            return;
        }

        Debug.LogWarning("Neither PlayerStateManager nor PlayerController found - cannot validate player state");
    }

    /// <summary>
    /// Gets the current restoration context for debugging purposes.
    /// </summary>
    public RestoreContext GetCurrentRestoreContext() => currentRestoreContext;

    /// <summary>
    /// Checks if currently handling a specific restoration context.
    /// </summary>
    public bool IsHandlingContext(RestoreContext context) => currentRestoreContext == context;

    /// <summary>
    /// Returns debug information about current transition state
    /// </summary>
    public string GetTransitionDebugInfo()
    {
        return $"Context: {currentRestoreContext}, TargetDoorway: {pendingTargetDoorwayID ?? "None"}, " +
               $"HasSaveData: {pendingSaveData != null}, ValidationEnabled: {enableDoorwayValidation}";
    }

    /// <summary>
    /// UPDATED: Manually triggers player state validation (for testing/debugging)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugForcePlayerStateValidation()
    {
        if (!Application.isPlaying) return;

        DebugLog("Manual player state validation triggered");
        ForceValidatePlayerStateAfterDoorway();
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneTransitionManager] {message}");
        }
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    #region Editor Debug Methods

#if UNITY_EDITOR
    [ContextMenu("Debug Transition State")]
    private void DebugTransitionState()
    {
        Debug.Log($"=== SceneTransitionManager Debug Info ===");
        Debug.Log(GetTransitionDebugInfo());
        Debug.Log("========================================");
    }

    [ContextMenu("Force Player State Validation")]
    private void EditorForcePlayerStateValidation()
    {
        DebugForcePlayerStateValidation();
    }
#endif

    #endregion
}