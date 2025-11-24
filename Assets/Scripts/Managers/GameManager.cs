using System;
using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Interface for centralized manager coordination.
/// All core managers should implement this for lifecycle management.
/// </summary>
public interface IManager
{
    /// <summary>
    /// Initialize the manager's core functionality and state.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Refresh component references after scene changes.
    /// </summary>
    void RefreshReferences();

    /// <summary>
    /// Clean up resources and unsubscribe from events.
    /// </summary>
    void Cleanup();
}

/// <summary>
/// UPDATED: Central coordinator for all game managers and core systems.
/// Now properly handles both persistent singleton managers (InputManager, PlayerStateManager) 
/// and scene-based managers. Provides enhanced manager lifecycle management with proper 
/// scene transition coordination and unified access to save/load functionality.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }


    [Header("Configurations")]
    public PlayerData playerData;

    [Header("Scene-Based Managers")]
    public PlayerManager playerManager;
    public UIManager uiManager;
    public AudioManager audioManager;
    public InGameTimeManager timeManager;
    public WeatherManager weatherManager;

    [Header("Persistent Managers")]
    [ShowInInspector, ReadOnly] private InputManager inputManagerReference;
    [ShowInInspector, ReadOnly] private PlayerStateManager stateManagerReference;

    [Header("Game State")]
    public bool isPaused = false;

    [Header("Manager Tracking")]
    [ShowInInspector, ReadOnly] private int totalManagedManagers;
    [ShowInInspector, ReadOnly] private int persistentManagerCount;
    [ShowInInspector, ReadOnly] private int sceneBasedManagerCount;

    // Events for manager system coordination
    public static event Action OnManagersInitialized;
    public static event Action OnManagersRefreshed;

    // Manager tracking
    private List<IManager> sceneBasedManagers = new List<IManager>();
    private List<IManager> persistentManagers = new List<IManager>();
    private List<IManager> allManagers = new List<IManager>();

    // Public accessors for persistent managers
    public InputManager InputManager => InputManager.Instance;
    public PlayerStateManager PlayerStateManager => PlayerStateManager.Instance;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;


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
            return;
        }
    }

    private void Start()
    {
        InitializeManagers();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Handles scene loaded events with improved singleton manager handling
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        StartCoroutine(RefreshManagerReferencesCoroutine());
    }

    /// <summary>
    /// Enhanced manager initialization that handles both persistent and scene-based managers
    /// </summary>
    private void InitializeManagers()
    {
        DebugLog("[GameManager] Starting manager initialization");

        // STEP 1: Initialize or connect to persistent singleton managers
        InitializePersistentManagers();

        // STEP 2: Find and initialize scene-based managers
        FindAndRegisterSceneManagers();
        InitializeSceneBasedManagers();

        // STEP 3: Update tracking
        UpdateManagerCounts();

        OnManagersInitialized?.Invoke();
        DebugLog("[GameManager] Manager initialization complete");
    }

    /// <summary>
    /// Handles persistent singleton managers (InputManager, PlayerStateManager, etc.)
    /// </summary>
    private void InitializePersistentManagers()
    {
        DebugLog("[GameManager] Initializing persistent managers");
        persistentManagers.Clear();

        // InputManager initialization
        InitializeInputManager();

        // PlayerStateManager initialization
        InitializePlayerStateManager();

        DebugLog($"[GameManager] Initialized {persistentManagers.Count} persistent managers");
    }

    /// <summary>
    /// Initialize or refresh InputManager
    /// </summary>
    private void InitializeInputManager()
    {
        if (InputManager.Instance == null)
        {
            DebugLog("[GameManager] Creating InputManager singleton");
            var inputManagerGO = FindFirstObjectByType<InputManager>();
            if (inputManagerGO == null)
            {
                Debug.LogWarning("[GameManager] No InputManager found in scene! You need to add one.");
                inputManagerReference = null;
                return;
            }
            else
            {
                inputManagerGO.Initialize();
            }
        }
        else
        {
            DebugLog("[GameManager] InputManager singleton already exists - refreshing");
            InputManager.Instance.RefreshReferences();
        }

        // Update reference and add to persistent managers
        inputManagerReference = InputManager.Instance;
        if (inputManagerReference != null)
        {
            if (!persistentManagers.Contains(inputManagerReference))
            {
                persistentManagers.Add(inputManagerReference);
            }
            DebugLog("[GameManager] InputManager ready");
        }
    }

    /// <summary>
    /// Initialize or refresh PlayerStateManager
    /// </summary>
    private void InitializePlayerStateManager()
    {
        PlayerStateManager.Initialize();

        // Update reference and add to persistent managers
        stateManagerReference = PlayerStateManager.Instance;
        if (stateManagerReference != null)
        {
            if (!persistentManagers.Contains(stateManagerReference))
            {
                persistentManagers.Add(stateManagerReference);
            }
            DebugLog("[GameManager] PlayerStateManager ready");
        }
    }

    /// <summary>
    /// Finds and registers only scene-based managers
    /// </summary>
    private void FindAndRegisterSceneManagers()
    {
        sceneBasedManagers.Clear();

        // Find scene-based managers (not persistent singletons)
        playerManager = FindFirstObjectByType<PlayerManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        audioManager = FindFirstObjectByType<AudioManager>();
        timeManager = FindFirstObjectByType<InGameTimeManager>();
        weatherManager = FindFirstObjectByType<WeatherManager>();

        // Register scene-based managers that implement IManager
        if (playerManager != null) sceneBasedManagers.Add(playerManager);
        if (uiManager != null) sceneBasedManagers.Add(uiManager);
        if (audioManager != null) sceneBasedManagers.Add(audioManager);
        if (timeManager != null) sceneBasedManagers.Add(timeManager);
        if (weatherManager != null) sceneBasedManagers.Add(weatherManager);

        DebugLog($"[GameManager] Found {sceneBasedManagers.Count} scene-based managers");

        // Update the combined manager list
        UpdateAllManagersList();
    }

    /// <summary>
    /// Combines persistent and scene-based managers
    /// </summary>
    private void UpdateAllManagersList()
    {
        allManagers.Clear();

        // Add persistent managers
        allManagers.AddRange(persistentManagers);

        // Add scene-based managers
        allManagers.AddRange(sceneBasedManagers);

        DebugLog($"[GameManager] Total managers tracked: {allManagers.Count}");
    }

    /// <summary>
    /// Initializes only scene-based managers (persistent ones are already initialized)
    /// </summary>
    private void InitializeSceneBasedManagers()
    {
        DebugLog("[GameManager] Initializing scene-based managers");

        foreach (var manager in sceneBasedManagers)
        {
            try
            {
                manager.Initialize();
                DebugLog($"[GameManager] Initialized {manager.GetType().Name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] Failed to initialize {manager.GetType().Name}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Enhanced reference refresh with proper singleton handling
    /// </summary>
    private IEnumerator RefreshManagerReferencesCoroutine()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.1f);
        RefreshManagerReferences();
    }

    /// <summary>
    /// Refreshes all manager references with singleton awareness
    /// </summary>
    private void RefreshManagerReferences()
    {
        DebugLog("[GameManager] Refreshing manager references");

        // STEP 1: Handle persistent managers
        RefreshPersistentManagers();

        // STEP 2: Re-find scene-based managers (they may have changed)
        FindAndRegisterSceneManagers();

        // STEP 3: Refresh scene-based managers
        foreach (var manager in sceneBasedManagers)
        {
            try
            {
                manager.RefreshReferences();
                DebugLog($"[GameManager] Refreshed {manager.GetType().Name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] Failed to refresh {manager.GetType().Name}: {e.Message}");
            }
        }

        // STEP 4: Update tracking
        UpdateManagerCounts();

        OnManagersRefreshed?.Invoke();
        DebugLog("[GameManager] Manager refresh complete");
    }

    /// <summary>
    /// Handles refresh for persistent singleton managers
    /// </summary>
    private void RefreshPersistentManagers()
    {
        DebugLog("[GameManager] Refreshing persistent managers");

        // Refresh InputManager
        if (InputManager.Instance != null)
        {
            try
            {
                InputManager.Instance.RefreshReferences();
                inputManagerReference = InputManager.Instance;
                DebugLog("[GameManager] Refreshed InputManager singleton");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] Failed to refresh InputManager: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] InputManager singleton is null during refresh!");
            inputManagerReference = null;
        }

        // Refresh PlayerStateManager
        if (PlayerStateManager.Instance != null)
        {
            try
            {
                PlayerStateManager.Instance.RefreshReferences();
                stateManagerReference = PlayerStateManager.Instance;
                DebugLog("[GameManager] Refreshed PlayerStateManager singleton");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] Failed to refresh PlayerStateManager: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] PlayerStateManager singleton is null during refresh!");
            stateManagerReference = null;
        }

        // Update persistent managers list
        persistentManagers.Clear();
        if (inputManagerReference != null) persistentManagers.Add(inputManagerReference);
        if (stateManagerReference != null) persistentManagers.Add(stateManagerReference);
    }

    /// <summary>
    /// Updates manager count tracking for inspector display
    /// </summary>
    private void UpdateManagerCounts()
    {
        persistentManagerCount = persistentManagers.Count;
        sceneBasedManagerCount = sceneBasedManagers.Count;
        totalManagedManagers = allManagers.Count;
    }

    /// <summary>
    /// Initiates a game save operation via SaveManager.
    /// </summary>
    [Button]
    public void SaveGame()
    {
        SaveManager.Instance?.SaveGame();
    }

    /// <summary>
    /// Initiates a game load operation via SaveManager.
    /// </summary>
    [Button]
    public void LoadGame()
    {
        SaveManager.Instance?.LoadGame();
    }

    /// <summary>
    /// Initializes a fresh game state with default values.
    /// </summary>
    public void NewGame()
    {
        if (playerManager != null && playerData != null)
        {
            playerManager.currentHealth = playerData.maxHealth;
        }
        DebugLog("New game started");
    }

    /// <summary>
    /// Pauses the game by setting time scale to 0 and firing pause events.
    /// </summary>
    public void PauseGame()
    {
        if (!isPaused)
        {
            isPaused = true;
            Time.timeScale = 0f;
            GameEvents.TriggerGamePaused();
        }
    }

    /// <summary>
    /// Resumes the game by restoring time scale and firing resume events.
    /// </summary>
    public void ResumeGame()
    {
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f;
            GameEvents.TriggerGameResumed();
        }
    }

    /// <summary>
    /// Quits the game application.
    /// </summary>
    public void QuitGame()
    {
        DebugLog("Quitting Game");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Manually triggers manager reference refresh with singleton support
    /// </summary>
    [Button]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void RefreshReferences()
    {
        RefreshManagerReferences();
    }

    /// <summary>
    /// Gets the InputManager instance (singleton)
    /// </summary>
    public InputManager GetInputManager()
    {
        return InputManager.Instance;
    }

    /// <summary>
    /// Gets the PlayerStateManager instance (singleton)
    /// </summary>
    public PlayerStateManager GetPlayerStateManager()
    {
        return PlayerStateManager.Instance;
    }

    /// <summary>
    /// Checks if all critical managers are available and properly initialized
    /// </summary>
    public bool AreManagersReady()
    {
        bool inputManagerReady = InputManager.Instance != null && InputManager.Instance.IsProperlyInitialized;
        bool playerManagerReady = playerManager != null;
        bool stateManagerReady = PlayerStateManager.Instance != null && PlayerStateManager.Instance.IsProperlyInitialized;

        return inputManagerReady && playerManagerReady && stateManagerReady;
    }

    /// <summary>
    /// Returns detailed debug info about all manager states
    /// </summary>
    [Button]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugManagerStates()
    {
        DebugLog("=== GAMEMANAGER DEBUG INFO ===");
        DebugLog($"Total Managers: {totalManagedManagers}");
        DebugLog($"Persistent Managers: {persistentManagerCount}");
        DebugLog($"Scene-Based Managers: {sceneBasedManagerCount}");
        DebugLog("");

        // Persistent Managers
        DebugLog("=== PERSISTENT MANAGERS ===");
        DebugLog($"InputManager: {(InputManager.Instance != null ? "Available" : "NULL")}");
        if (InputManager.Instance != null)
        {
            DebugLog($"  - Initialized: {InputManager.Instance.IsProperlyInitialized}");
            DebugLog($"  - Current Mode: {InputManager.Instance.GetCurrentMovementMode()}");
        }

        DebugLog($"PlayerStateManager: {(PlayerStateManager.Instance != null ? "Available" : "NULL")}");
        if (PlayerStateManager.Instance != null)
        {
            DebugLog($"  - Initialized: {PlayerStateManager.Instance.IsProperlyInitialized}");
            DebugLog($"  - Current State: {PlayerStateManager.Instance.CurrentStateType}");
            DebugLog($"  - References Valid: {PlayerStateManager.Instance.IsProperlyInitialized}");
        }

        // Scene-Based Managers
        DebugLog("");
        DebugLog("=== SCENE-BASED MANAGERS ===");
        DebugLog($"PlayerManager: {(playerManager != null ? "Available" : "NULL")}");
        DebugLog($"UIManager: {(uiManager != null ? "Available" : "NULL")}");
        DebugLog($"AudioManager: {(audioManager != null ? "Available" : "NULL")}");
        DebugLog($"TimeManager: {(timeManager != null ? "Available" : "NULL")}");
        DebugLog($"WeatherManager: {(weatherManager != null ? "Available" : "NULL")}");
        DebugLog("==============================");
    }

    /// <summary>
    /// Debug method to force refresh all persistent managers
    /// </summary>
    [Button]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugRefreshPersistentManagers()
    {
        RefreshPersistentManagers();
        DebugLog("Persistent managers manually refreshed");
    }

    /// <summary>
    /// Get a summary of manager readiness for external systems
    /// </summary>
    public string GetManagerReadinessSummary()
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"Total Managers: {totalManagedManagers}");
        summary.AppendLine($"All Managers Ready: {AreManagersReady()}");
        summary.AppendLine($"InputManager Ready: {InputManager.Instance != null && InputManager.Instance.IsProperlyInitialized}");
        summary.AppendLine($"PlayerStateManager Ready: {PlayerStateManager.Instance != null && PlayerStateManager.Instance.IsProperlyInitialized}");
        summary.AppendLine($"PlayerManager Ready: {playerManager != null}");
        return summary.ToString();
    }

    private void OnDestroy()
    {
        // Only cleanup scene-based managers
        // Persistent managers handle their own cleanup
        foreach (var manager in sceneBasedManagers)
        {
            try
            {
                manager.Cleanup();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] Failed to cleanup {manager.GetType().Name}: {e.Message}");
            }
        }

        // Clear all lists
        sceneBasedManagers.Clear();
        persistentManagers.Clear();
        allManagers.Clear();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SwimmingBodyRotationController] {message}");
        }
    }
}