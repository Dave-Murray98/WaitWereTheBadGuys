using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Save component that integrates Cozy Weather 3's native save system with your modular
/// save architecture. Uses Cozy's built-in CozySaveLoad module for reliable weather
/// persistence while providing fallback data extraction for compatibility.
/// 
/// This component coordinates with Cozy's timing to ensure weather state is properly
/// saved before scene transitions and restored after loads. It handles both doorway
/// transitions and save file loads seamlessly.
/// </summary>
public class CozyWeatherSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private WeatherManager weatherManager;
    [SerializeField] private bool autoFindManager = true;

    [Header("Integration Settings")]
    [SerializeField] private bool useCozyNativeSave = true;
    [SerializeField] private bool provideFallbackData = true;
    [SerializeField] private bool coordinateWithSceneTransitions = true;

    [Header("Timing Settings")]
    [SerializeField] private float saveDelaySeconds = 0.1f; // Delay before saving to let Cozy stabilize
    [SerializeField] private float loadDelaySeconds = 0.2f; // Delay after loading to let Cozy update

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "CozyWeather_Native";
        autoGenerateID = false;
        base.Awake();

        if (autoFindManager)
        {
            FindWeatherManager();
        }
    }

    private void Start()
    {
        ValidateReferences();
        SetupEventListeners();
    }


    /// <summary>
    /// Automatically locates the weather manager in the scene.
    /// </summary>
    private void FindWeatherManager()
    {
        if (weatherManager == null)
        {
            weatherManager = WeatherManager.Instance;
        }

        if (weatherManager == null)
        {
            weatherManager = FindFirstObjectByType<WeatherManager>();
        }
    }

    /// <summary>
    /// Validates that the manager reference is available for saving/loading.
    /// </summary>
    private void ValidateReferences()
    {
        if (weatherManager == null)
        {
            Debug.LogError($"[{name}] WeatherManager reference missing! Cozy weather state won't be saved.");
        }
        else
        {
            DebugLog("WeatherManager reference validated successfully");
        }
    }

    /// <summary>
    /// Sets up event listeners for coordinated save/load timing
    /// </summary>
    private void SetupEventListeners()
    {
        if (coordinateWithSceneTransitions)
        {
            // Listen for weather save completion
            WeatherManager.OnWeatherSaveComplete += OnCozyWeatherSaved;
            WeatherManager.OnWeatherLoadComplete += OnCozyWeatherLoaded;
        }
    }

    /// <summary>
    /// Removes event listeners to prevent memory leaks
    /// </summary>
    private void RemoveEventListeners()
    {
        WeatherManager.OnWeatherSaveComplete -= OnCozyWeatherSaved;
        WeatherManager.OnWeatherLoadComplete -= OnCozyWeatherLoaded;
    }

    /// <summary>
    /// Gets current weather state for saving. Uses Cozy's native save when available,
    /// falls back to basic data extraction for compatibility.
    /// </summary>
    public override object GetDataToSave()
    {
        if (weatherManager == null)
        {
            DebugLog("Cannot save - WeatherManager reference missing");
            return CreateDefaultSaveData();
        }

        if (!weatherManager.IsCozyConnected())
        {
            DebugLog("Cozy not connected - saving default state");
            return CreateDefaultSaveData();
        }

        // Always provide fallback data for compatibility
        var fallbackData = weatherManager.GetBasicWeatherData();
        fallbackData.usesNativeSave = useCozyNativeSave && weatherManager.IsUsingNativeSave();

        DebugLog($"Saving Cozy weather state: {fallbackData.GetDebugInfo()}");
        return fallbackData;
    }

    /// <summary>
    /// Creates default save data when Cozy isn't available
    /// </summary>
    private CozyWeatherSaveData CreateDefaultSaveData()
    {
        return new CozyWeatherSaveData
        {
            weatherName = "Clear",
            temperature = 20f,
            humidity = 0.5f,
            precipitation = 0f,
            saveTimestamp = System.DateTime.Now,
            cozyConnected = false,
            usesNativeSave = false
        };
    }

    /// <summary>
    /// Extracts weather data from various save container formats.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog($"Extracting Cozy weather data from container type: {saveContainer?.GetType().Name ?? "null"}");

        if (saveContainer is CozyWeatherSaveData weatherData)
        {
            DebugLog($"Direct extraction - {weatherData.GetDebugInfo()}");
            return weatherData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            var extractedData = persistentData.GetComponentData<CozyWeatherSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from persistent data - {extractedData.GetDebugInfo()}");
            }
            else
            {
                DebugLog("No Cozy weather data found in persistent data");
            }
            return extractedData;
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            var extractedData = playerSaveData.GetCustomData<CozyWeatherSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from player save data - {extractedData.GetDebugInfo()}");
            }
            else
            {
                DebugLog("No Cozy weather data found in player save data");
            }
            return extractedData;
        }

        DebugLog($"Unsupported save container type: {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts Cozy weather data from the unified save structure.
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null)
        {
            DebugLog("Cannot extract from unified save - unifiedData is null");
            return null;
        }

        DebugLog("Using modular extraction from unified save data");
        var extractedData = unifiedData.GetComponentData<CozyWeatherSaveData>(SaveID);

        if (extractedData != null)
        {
            DebugLog($"Modular extraction successful - {extractedData.GetDebugInfo()}");
        }
        else
        {
            DebugLog("No Cozy weather data found in unified save structure");
        }

        return extractedData;
    }

    /// <summary>
    /// Creates default Cozy weather data for new games.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default Cozy weather data for new game");

        var defaultData = new CozyWeatherSaveData
        {
            weatherName = "Clear",
            temperature = 20f,
            humidity = 0.5f,
            precipitation = 0f,
            saveTimestamp = System.DateTime.Now,
            cozyConnected = false,
            usesNativeSave = useCozyNativeSave
        };

        DebugLog($"Created default Cozy weather data: {defaultData.GetDebugInfo()}");
        return defaultData;
    }

    /// <summary>
    /// Stores Cozy weather data into the unified save structure.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is CozyWeatherSaveData weatherData && unifiedData != null)
        {
            DebugLog($"Contributing Cozy weather data to unified save: {weatherData.GetDebugInfo()}");
            unifiedData.SetComponentData(SaveID, weatherData);
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected CozyWeatherSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Context-aware data restoration. Coordinates with Cozy's native save system
    /// for reliable weather restoration across all contexts.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        DebugLog($"=== LOADING COZY WEATHER DATA (Context: {context}) ===");

        if (!(data is CozyWeatherSaveData weatherData))
        {
            DebugLog($"Invalid save data type - expected CozyWeatherSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"Received valid data - {weatherData.GetDebugInfo()}");

        // Refresh manager reference in case it changed after scene load
        if (autoFindManager && weatherManager == null)
        {
            FindWeatherManager();
        }

        if (weatherManager == null)
        {
            Debug.LogError("WeatherManager not found - cannot restore Cozy weather data!");
            return;
        }

        // Validate data before applying
        if (!weatherData.IsValid())
        {
            Debug.LogWarning("Cozy weather save data failed validation - applying anyway with corrections");
        }

        // Choose restoration method based on capabilities and context
        if (weatherData.usesNativeSave && weatherManager.IsUsingNativeSave())
        {
            RestoreUsingCozyNativeSave(weatherData, context);
        }
        else
        {
            RestoreUsingFallbackMethod(weatherData, context);
        }

        DebugLog($"Cozy weather data restoration complete for context: {context}");
    }

    /// <summary>
    /// Restores weather using Cozy's native save/load system for maximum reliability
    /// </summary>
    private void RestoreUsingCozyNativeSave(CozyWeatherSaveData weatherData, RestoreContext context)
    {
        DebugLog($"Restoring using Cozy native save system");

        // For doorway transitions, we don't need to load - weather should persist
        if (context == RestoreContext.DoorwayTransition)
        {
            DebugLog("Doorway transition - Cozy weather should persist naturally");
            return;
        }

        // For save file loads and new games, use Cozy's load system
        StartCoroutine(RestoreCozyNativeDelayed(weatherData, context));
    }

    /// <summary>
    /// Restores weather using fallback method when native save isn't available
    /// </summary>
    private void RestoreUsingFallbackMethod(CozyWeatherSaveData weatherData, RestoreContext context)
    {
        DebugLog($"Restoring using fallback method");

        // Wait a frame for Cozy to be ready, then apply the data
        StartCoroutine(RestoreFallbackDelayed(weatherData, context));
    }

    /// <summary>
    /// Delayed restoration using Cozy's native save system
    /// </summary>
    private System.Collections.IEnumerator RestoreCozyNativeDelayed(CozyWeatherSaveData weatherData, RestoreContext context)
    {
        // Wait for Cozy to be fully initialized
        yield return new WaitForSecondsRealtime(loadDelaySeconds);

        DebugLog("Attempting Cozy native load");

        // Ensure weather manager is connected to Cozy
        weatherManager.ReconnectToCozy();

        yield return new WaitForSecondsRealtime(0.1f);

        // Use Cozy's native load system
        if (weatherManager.IsCozyConnected() && weatherManager.IsUsingNativeSave())
        {
            try
            {
                weatherManager.LoadCozyWeatherState();
                DebugLog("Cozy native load initiated");
            }
            catch (System.Exception e)
            {
                DebugLog($"Error during Cozy native load: {e.Message}");
                // Fall back to basic restoration
                weatherManager.ApplyBasicWeatherData(weatherData);
            }
        }
        else
        {
            DebugLog("Cannot use Cozy native load - falling back to basic restoration");
            weatherManager.ApplyBasicWeatherData(weatherData);
        }

        DebugLog("Cozy native weather restoration completed");
    }

    /// <summary>
    /// Delayed restoration using fallback method
    /// </summary>
    private System.Collections.IEnumerator RestoreFallbackDelayed(CozyWeatherSaveData weatherData, RestoreContext context)
    {
        // Wait for Cozy to be ready
        yield return new WaitForSecondsRealtime(loadDelaySeconds);

        DebugLog("Applying fallback weather restoration");

        // Ensure weather manager is connected to Cozy
        weatherManager.ReconnectToCozy();

        yield return new WaitForSecondsRealtime(0.1f);

        // Apply the basic weather data
        if (weatherManager.IsCozyConnected())
        {
            weatherManager.ApplyBasicWeatherData(weatherData);
            DebugLog("Fallback weather data applied");
        }
        else
        {
            DebugLog("Cannot apply weather data - Cozy not connected");
        }

        DebugLog("Fallback weather restoration completed");
    }

    /// <summary>
    /// Called before save operations to coordinate with Cozy's save system
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing Cozy weather data for save operation");

        if (autoFindManager)
        {
            FindWeatherManager();
        }

        ValidateReferences();

        // If using native save, trigger Cozy save with delay
        if (useCozyNativeSave && weatherManager != null && weatherManager.IsUsingNativeSave())
        {
            StartCoroutine(TriggerCozySaveDelayed());
        }
    }

    /// <summary>
    /// Triggers Cozy's native save with a small delay to ensure state is stable
    /// </summary>
    private System.Collections.IEnumerator TriggerCozySaveDelayed()
    {
        yield return new WaitForSecondsRealtime(saveDelaySeconds);

        if (weatherManager != null && weatherManager.IsCozyConnected())
        {
            DebugLog("Triggering Cozy native save before data collection");
            weatherManager.SaveCozyWeatherState();
        }
    }

    /// <summary>
    /// Called after load operations to refresh connected systems.
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Cozy weather data load completed - refreshing connected systems");

        if (weatherManager != null)
        {
            // Force weather manager to reconnect and read current state
            weatherManager.ReconnectToCozy();

            // Update any weather debug UI components
            var weatherDebugUI = FindFirstObjectByType<WeatherDebugUI>();
            if (weatherDebugUI != null)
            {
                // Note: WeatherDebugUI is commented out in your code, so this might not be available
                // weatherDebugUI.ForceUpdate();
                DebugLog("Weather debug UI would be refreshed here");
            }
        }
    }

    #region Event Handlers

    /// <summary>
    /// Handles Cozy weather save completion
    /// </summary>
    private void OnCozyWeatherSaved()
    {
        DebugLog("Cozy native weather save completed");
    }

    /// <summary>
    /// Handles Cozy weather load completion
    /// </summary>
    private void OnCozyWeatherLoaded(bool success)
    {
        DebugLog($"Cozy native weather load completed - Success: {success}");
    }

    #endregion

    #region Manual Controls (For Testing)

    /// <summary>
    /// Manual method to force complete restoration from saved data (for debugging)
    /// </summary>
    [Button("Force Restore Test")]
    public void ForceRestoreTest()
    {
        if (weatherManager == null)
        {
            DebugLog("Cannot test - WeatherManager reference missing");
            return;
        }

        // Get current Cozy state
        var currentData = GetDataToSave() as CozyWeatherSaveData;
        if (currentData != null)
        {
            DebugLog("Testing restoration with current Cozy data");
            LoadSaveDataWithContext(currentData, RestoreContext.SaveFileLoad);
        }
    }

    /// <summary>
    /// Toggles Cozy native save usage
    /// </summary>
    [Button("Toggle Cozy Native Save")]
    public void ToggleCozyNativeSave()
    {
        useCozyNativeSave = !useCozyNativeSave;
        DebugLog($"Cozy native save {(useCozyNativeSave ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Forces immediate save of current Cozy state (for debugging)
    /// </summary>
    [Button("Force Save Current State")]
    public void ForceSaveCurrentState()
    {
        if (weatherManager != null && weatherManager.IsCozyConnected())
        {
            if (useCozyNativeSave && weatherManager.IsUsingNativeSave())
            {
                weatherManager.SaveCozyWeatherState();
                DebugLog("Triggered Cozy native save");
            }
            else
            {
                var saveData = GetDataToSave() as CozyWeatherSaveData;
                if (saveData != null)
                {
                    DebugLog($"Current fallback state: {saveData.GetDebugInfo()}");
                }
            }
        }
        else
        {
            DebugLog("Cannot save - WeatherManager not connected to Cozy");
        }
    }

    /// <summary>
    /// Gets detailed information about current save state
    /// </summary>
    [Button("Show Save Info")]
    public void ShowSaveInfo()
    {
        DebugLog("=== COZY WEATHER SAVE INFO ===");
        DebugLog($"Native Save Enabled: {useCozyNativeSave}");
        DebugLog($"Fallback Data Enabled: {provideFallbackData}");
        DebugLog($"Manager Connected: {weatherManager != null}");

        if (weatherManager != null)
        {
            DebugLog($"Cozy Connected: {weatherManager.IsCozyConnected()}");
            DebugLog($"Using Native Save: {weatherManager.IsUsingNativeSave()}");

            if (weatherManager.IsCozyConnected())
            {
                var currentData = weatherManager.GetCurrentWeatherData();
                DebugLog($"Current State: {currentData.GetDebugInfo()}");
            }
        }
    }

    /// <summary>
    /// Tests the complete save/load cycle
    /// </summary>
    [Button("Test Save/Load Cycle")]
    public void TestSaveLoadCycle()
    {
        if (weatherManager == null || !weatherManager.IsCozyConnected())
        {
            DebugLog("Cannot test - WeatherManager not available or not connected");
            return;
        }

        StartCoroutine(TestSaveLoadCycleCoroutine());
    }

    /// <summary>
    /// Coroutine that tests the complete save/load cycle
    /// </summary>
    private System.Collections.IEnumerator TestSaveLoadCycleCoroutine()
    {
        DebugLog("Starting save/load cycle test...");

        // Get initial state
        var initialData = weatherManager.GetCurrentWeatherData();
        DebugLog($"Initial state: {initialData.GetDebugInfo()}");

        // Trigger save
        if (useCozyNativeSave && weatherManager.IsUsingNativeSave())
        {
            weatherManager.SaveCozyWeatherState();
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // Get save data
        var saveData = GetDataToSave() as CozyWeatherSaveData;
        DebugLog($"Save data: {saveData?.GetDebugInfo() ?? "null"}");

        yield return new WaitForSecondsRealtime(1f);

        // Test restoration
        if (saveData != null)
        {
            LoadSaveDataWithContext(saveData, RestoreContext.SaveFileLoad);
            yield return new WaitForSecondsRealtime(1f);

            // Check final state
            var finalData = weatherManager.GetCurrentWeatherData();
            DebugLog($"Final state: {finalData.GetDebugInfo()}");
        }

        DebugLog("Save/load cycle test completed");
    }

    #endregion

    private void OnDestroy()
    {
        RemoveEventListeners();

        // Unregister from persistence manager
        if (PlayerPersistenceManager.Instance != null)
        {
            PlayerPersistenceManager.Instance.UnregisterComponent(this);
        }
    }
}