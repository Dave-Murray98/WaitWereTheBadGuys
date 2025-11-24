using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using DistantLands.Cozy;

/// <summary>
/// Enhanced Weather Manager that properly integrates with Cozy Weather 3's native save system.
/// Uses Cozy's built-in CozySaveLoadModule module for state persistence rather than trying to read
/// internal state. This provides reliable weather restoration across scene transitions and save/load.
/// 
/// COZY-NATIVE APPROACH: Leverages Cozy's own save/load module for maximum compatibility
/// and reliability. Weather state is handled by Cozy itself, we just coordinate the timing.
/// </summary>
public class WeatherManager : MonoBehaviour, IManager
{
    public static WeatherManager Instance { get; private set; }

    [Header("Cozy Integration Settings")]
    [SerializeField] private bool enableCozyIntegration = true;
    [SerializeField] private float cozyReadInterval = 0.2f; // How often to read from Cozy
    [SerializeField] private bool trackWeatherChanges = true;
    [SerializeField] private bool trackTemperatureChanges = true;

    [Header("Save Integration Settings")]
    [SerializeField] private bool useCozySaveModule = true;
    [SerializeField] private float significantTempChange = 0.5f; // °C change to trigger events

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Cozy module references
    [ShowInInspector, ReadOnly] private CozySaveLoadModule cozySaveModule;
    [ShowInInspector, ReadOnly] private CozyWeatherModule weatherModule;
    [ShowInInspector, ReadOnly] private CozyClimateModule climateModule;
    [ShowInInspector, ReadOnly] private bool isCozyConnected = false;

    // Current state read from Cozy
    [ShowInInspector, ReadOnly] private string currentWeatherName = "Clear";
    [ShowInInspector, ReadOnly] private float currentTemperature = 20f;
    [ShowInInspector, ReadOnly] private float currentHumidity = 0.5f;
    [ShowInInspector, ReadOnly] private float currentPrecipitation = 0f;

    // Change tracking
    [ShowInInspector, ReadOnly] private string previousWeatherName = "";
    [ShowInInspector, ReadOnly] private float previousTemperature = 20f;
    [ShowInInspector, ReadOnly] private float lastCozyReadTime = 0f;

    // Events for external systems
    public static event System.Action<string> OnWeatherChanged; // Weather profile name
    public static event System.Action<float> OnTemperatureChanged; // Current temperature
    public static event System.Action<CozyWeatherData> OnCozyWeatherUpdated; // Complete weather data
    public static event System.Action OnWeatherSaveComplete;
    public static event System.Action<bool> OnWeatherLoadComplete; // bool = success

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DebugLog("WeatherManager initialized with Cozy Weather 3 integration");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #region IManager Implementation

    public void Initialize()
    {
        RefreshReferences();
        ConnectToCozy();

        if (isCozyConnected)
        {
            ReadInitialCozyState();
        }

        DebugLog("WeatherManager initialized - using Cozy Weather 3 native save system");
    }

    public void RefreshReferences()
    {
        ConnectToCozy();
        DebugLog("References refreshed");
    }

    public void Cleanup()
    {
        DebugLog("Cleanup completed");
    }

    #endregion

    private void Update()
    {
        if (enableCozyIntegration && isCozyConnected)
        {
            ReadFromCozy();
        }
    }

    #region Cozy Connection and Data Reading

    /// <summary>
    /// Connects to Cozy Weather 3's modules and save system
    /// </summary>
    private void ConnectToCozy()
    {
        if (!enableCozyIntegration)
        {
            isCozyConnected = false;
            DebugLog("Cozy integration disabled");
            return;
        }

        if (CozyWeather.instance == null)
        {
            isCozyConnected = false;
            DebugLog("CozyWeather.instance not found");
            return;
        }

        try
        {
            // Connect to weather module
            weatherModule = CozyWeather.instance.weatherModule;

            // Connect to climate module
            climateModule = CozyWeather.instance.climateModule;

            // Connect to save/load module
            if (useCozySaveModule)
            {
                cozySaveModule = CozyWeather.instance.GetModule<CozySaveLoadModule>();
                if (cozySaveModule == null)
                {
                    Debug.LogWarning("[WeatherManager] CozySaveLoadModule module not found! Add the Save & Load module to Cozy Weather 3");
                }
            }

            bool hasWeatherModule = weatherModule != null;
            bool hasClimateModule = climateModule != null;
            bool hasSaveModule = !useCozySaveModule || cozySaveModule != null;

            isCozyConnected = hasWeatherModule && hasSaveModule;

            if (isCozyConnected)
            {
                DebugLog($"Connected to Cozy - Weather: {hasWeatherModule}, Climate: {hasClimateModule}, Save: {hasSaveModule}");
            }
            else
            {
                DebugLog("Failed to connect to required Cozy modules");
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Error connecting to Cozy: {e.Message}");
            isCozyConnected = false;
        }
    }

    /// <summary>
    /// Reads initial state from Cozy when connecting
    /// </summary>
    private void ReadInitialCozyState()
    {
        if (!isCozyConnected) return;

        try
        {
            ReadCurrentWeather();
            ReadCurrentTemperature();
            ReadCurrentClimateData();

            // Initialize previous values
            previousWeatherName = currentWeatherName;
            previousTemperature = currentTemperature;

            DebugLog($"Initial Cozy state - Weather: {currentWeatherName}, Temp: {currentTemperature:F1}°C");
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading initial Cozy state: {e.Message}");
        }
    }

    /// <summary>
    /// Reads current state from Cozy at regular intervals
    /// </summary>
    private void ReadFromCozy()
    {
        // Read at intervals to avoid excessive processing
        if (Time.time - lastCozyReadTime < cozyReadInterval) return;
        lastCozyReadTime = Time.time;

        if (!isCozyConnected)
        {
            ConnectToCozy();
            return;
        }

        try
        {
            ReadCurrentWeather();
            ReadCurrentTemperature();
            ReadCurrentClimateData();

            CheckForWeatherChanges();
            CheckForTemperatureChanges();
            FireWeatherDataUpdateEvent();
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading from Cozy: {e.Message}");
            isCozyConnected = false;
        }
    }

    /// <summary>
    /// Reads current weather from Cozy's weather module
    /// </summary>
    private void ReadCurrentWeather()
    {
        if (weatherModule?.ecosystem == null) return;

        try
        {
            var currentWeather = weatherModule.ecosystem.currentWeather;
            if (currentWeather != null)
            {
                currentWeatherName = currentWeather.name ?? "Unknown";
            }
            else
            {
                currentWeatherName = "Clear";
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading weather: {e.Message}");
            currentWeatherName = "Error";
        }
    }

    /// <summary>
    /// Reads current temperature from Cozy's climate module
    /// </summary>
    private void ReadCurrentTemperature()
    {
        try
        {
            if (climateModule != null)
            {
                // Use Cozy's temperature system
                currentTemperature = climateModule.currentTemperature;
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading temperature: {e.Message}");
        }
    }

    /// <summary>
    /// Reads additional climate data from Cozy
    /// </summary>
    private void ReadCurrentClimateData()
    {
        try
        {
            if (climateModule != null)
            {
                // Read humidity if available
                var humidityProperty = climateModule.GetType().GetProperty("humidity") ??
                                     climateModule.GetType().GetProperty("currentHumidity");
                if (humidityProperty != null)
                {
                    currentHumidity = (float)humidityProperty.GetValue(climateModule);
                }

                // Read precipitation if available
                var precipProperty = climateModule.GetType().GetProperty("precipitation") ??
                                   climateModule.GetType().GetProperty("currentPrecipitation");
                if (precipProperty != null)
                {
                    currentPrecipitation = (float)precipProperty.GetValue(climateModule);
                }
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading climate data: {e.Message}");
        }
    }

    #endregion

    #region Change Detection and Events

    /// <summary>
    /// Checks for weather changes and fires events
    /// </summary>
    private void CheckForWeatherChanges()
    {
        if (!trackWeatherChanges) return;

        if (currentWeatherName != previousWeatherName)
        {
            DebugLog($"Weather changed: {previousWeatherName} → {currentWeatherName}");
            OnWeatherChanged?.Invoke(currentWeatherName);
            previousWeatherName = currentWeatherName;
        }
    }

    /// <summary>
    /// Checks for temperature changes and fires events
    /// </summary>
    private void CheckForTemperatureChanges()
    {
        if (!trackTemperatureChanges) return;

        float tempDifference = Mathf.Abs(currentTemperature - previousTemperature);
        if (tempDifference >= significantTempChange)
        {
            DebugLog($"Temperature changed: {previousTemperature:F1}°C → {currentTemperature:F1}°C");
            OnTemperatureChanged?.Invoke(currentTemperature);
            previousTemperature = currentTemperature;
        }
    }

    /// <summary>
    /// Fires comprehensive weather data update event
    /// </summary>
    private void FireWeatherDataUpdateEvent()
    {
        var weatherData = new CozyWeatherData
        {
            weatherName = currentWeatherName,
            weatherProfile = weatherModule?.ecosystem?.currentWeather,
            temperature = currentTemperature,
            humidity = currentHumidity,
            precipitation = currentPrecipitation,
            isConnected = isCozyConnected,
            timestamp = Time.time
        };

        OnCozyWeatherUpdated?.Invoke(weatherData);
    }

    #endregion

    #region Cozy Native Save/Load Integration

    /// <summary>
    /// Saves current Cozy weather state using Cozy's native save system
    /// </summary>
    public void SaveCozyWeatherState()
    {
        if (!isCozyConnected || cozySaveModule == null)
        {
            DebugLog("Cannot save Cozy state - not connected or save module missing");
            return;
        }

        try
        {
            DebugLog("Saving Cozy weather state using native save module");
            cozySaveModule.Save();
            OnWeatherSaveComplete?.Invoke();
            DebugLog("Cozy weather state saved successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WeatherManager] Failed to save Cozy state: {e.Message}");
        }
    }

    /// <summary>
    /// Loads Cozy weather state using Cozy's native save system
    /// </summary>
    public void LoadCozyWeatherState()
    {
        if (!isCozyConnected || cozySaveModule == null)
        {
            DebugLog("Cannot load Cozy state - not connected or save module missing");
            OnWeatherLoadComplete?.Invoke(false);
            return;
        }

        try
        {
            DebugLog("Loading Cozy weather state using native save module");
            cozySaveModule.Load();

            // Wait a frame then read the updated state
            StartCoroutine(PostLoadStateUpdate());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WeatherManager] Failed to load Cozy state: {e.Message}");
            OnWeatherLoadComplete?.Invoke(false);
        }
    }

    /// <summary>
    /// Updates our cached state after Cozy load completes
    /// </summary>
    private System.Collections.IEnumerator PostLoadStateUpdate()
    {
        yield return new WaitForEndOfFrame();

        // Force immediate state read
        lastCozyReadTime = 0f;
        ReadFromCozy();

        DebugLog("Cozy weather state loaded and updated successfully");
        OnWeatherLoadComplete?.Invoke(true);
    }

    /// <summary>
    /// Gets basic weather data for external save systems (fallback)
    /// </summary>
    public CozyWeatherSaveData GetBasicWeatherData()
    {
        return new CozyWeatherSaveData
        {
            weatherName = currentWeatherName,
            temperature = currentTemperature,
            humidity = currentHumidity,
            precipitation = currentPrecipitation,
            saveTimestamp = System.DateTime.Now,
            cozyConnected = isCozyConnected,
            usesNativeSave = useCozySaveModule
        };
    }

    /// <summary>
    /// Applies basic weather data (fallback for when native save isn't available)
    /// </summary>
    public void ApplyBasicWeatherData(CozyWeatherSaveData saveData)
    {
        if (saveData == null)
        {
            DebugLog("Cannot apply weather data - data is null");
            return;
        }

        DebugLog($"Applying fallback weather data - Weather: {saveData.weatherName}, Temp: {saveData.temperature:F1}°C");

        // This is a fallback - we can't directly set Cozy's state without the save module
        // But we can try to find and set a weather profile with the saved name
        if (weatherModule?.ecosystem != null && !string.IsNullOrEmpty(saveData.weatherName))
        {
            TrySetWeatherByName(saveData.weatherName);
        }

        // Force immediate read to sync our state
        ReadInitialCozyState();
    }

    /// <summary>
    /// Attempts to set weather by finding a profile with the given name
    /// </summary>
    private void TrySetWeatherByName(string weatherName)
    {
        try
        {
            // This would require access to Cozy's weather profile list
            // Implementation depends on Cozy's specific API for weather profiles
            DebugLog($"Attempting to set weather to: {weatherName} (implementation depends on Cozy API)");

            // You would need to implement profile lookup based on Cozy's available API
            // For example: weatherModule.ecosystem.SetWeather(foundProfile);
        }
        catch (System.Exception e)
        {
            DebugLog($"Error setting weather by name: {e.Message}");
        }
    }

    #endregion

    #region Public API for Game Logic

    /// <summary>
    /// Gets current weather name from Cozy
    /// </summary>
    public string GetCurrentWeatherName() => currentWeatherName;

    /// <summary>
    /// Gets current temperature from Cozy (°C)
    /// </summary>
    public float GetCurrentTemperature() => currentTemperature;

    /// <summary>
    /// Gets current humidity from Cozy (0-1)
    /// </summary>
    public float GetCurrentHumidity() => currentHumidity;

    /// <summary>
    /// Gets current precipitation level from Cozy
    /// </summary>
    public float GetCurrentPrecipitation() => currentPrecipitation;

    /// <summary>
    /// Gets current weather profile object from Cozy
    /// </summary>
    public object GetCurrentWeatherProfile() => weatherModule?.ecosystem?.currentWeather;

    /// <summary>
    /// Checks if currently connected to Cozy
    /// </summary>
    public bool IsCozyConnected() => isCozyConnected;

    /// <summary>
    /// Checks if using Cozy's native save system
    /// </summary>
    public bool IsUsingNativeSave() => useCozySaveModule && cozySaveModule != null;

    /// <summary>
    /// Gets complete weather data structure
    /// </summary>
    public CozyWeatherData GetCurrentWeatherData()
    {
        return new CozyWeatherData
        {
            weatherName = currentWeatherName,
            weatherProfile = weatherModule?.ecosystem?.currentWeather,
            temperature = currentTemperature,
            humidity = currentHumidity,
            precipitation = currentPrecipitation,
            isConnected = isCozyConnected,
            timestamp = Time.time
        };
    }

    /// <summary>
    /// Checks if specific weather is currently active (by name)
    /// </summary>
    public bool IsWeatherActive(string weatherName)
    {
        return currentWeatherName.Contains(weatherName, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if it's currently raining/snowing (precipitation > 0)
    /// </summary>
    public bool IsPrecipitating() => currentPrecipitation > 0.1f;

    /// <summary>
    /// Gets temperature in Fahrenheit for UI display
    /// </summary>
    public float GetTemperatureFahrenheit() => (currentTemperature * 9f / 5f) + 32f;

    #endregion

    #region Manual Controls (For Testing)

    /// <summary>
    /// Forces immediate read from Cozy
    /// </summary>
    [Button("Force Read From Cozy")]
    public void ForceReadFromCozy()
    {
        if (isCozyConnected)
        {
            lastCozyReadTime = 0f;
            ReadFromCozy();
            DebugLog("Forced read from Cozy completed");
        }
        else
        {
            DebugLog("Not connected to Cozy - cannot force read");
        }
    }

    /// <summary>
    /// Reconnects to Cozy modules
    /// </summary>
    [Button("Reconnect to Cozy")]
    public void ReconnectToCozy()
    {
        ConnectToCozy();
        if (isCozyConnected)
        {
            ReadInitialCozyState();
            DebugLog("Reconnected to Cozy");
        }
    }

    /// <summary>
    /// Tests Cozy native save functionality
    /// </summary>
    [Button("Test Cozy Save")]
    public void TestCozySave()
    {
        if (IsUsingNativeSave())
        {
            SaveCozyWeatherState();
        }
        else
        {
            DebugLog("Cozy native save not available");
        }
    }

    /// <summary>
    /// Tests Cozy native load functionality
    /// </summary>
    [Button("Test Cozy Load")]
    public void TestCozyLoad()
    {
        if (IsUsingNativeSave())
        {
            LoadCozyWeatherState();
        }
        else
        {
            DebugLog("Cozy native load not available");
        }
    }

    /// <summary>
    /// Toggles Cozy integration on/off
    /// </summary>
    [Button("Toggle Cozy Integration")]
    public void ToggleCozyIntegration()
    {
        enableCozyIntegration = !enableCozyIntegration;
        if (enableCozyIntegration)
        {
            ConnectToCozy();
            if (isCozyConnected)
            {
                ReadInitialCozyState();
            }
        }
        else
        {
            isCozyConnected = false;
        }
        DebugLog($"Cozy integration {(enableCozyIntegration ? "enabled" : "disabled")}");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[WeatherManager] {message}");
        }
    }

    private void OnValidate()
    {
        cozyReadInterval = Mathf.Max(0.01f, cozyReadInterval);
        significantTempChange = Mathf.Max(0.1f, significantTempChange);
    }
}

/// <summary>
/// Enhanced data structure for current weather information from Cozy
/// </summary>
[System.Serializable]
public class CozyWeatherData
{
    public string weatherName;
    public object weatherProfile;
    public float temperature;
    public float humidity;
    public float precipitation;
    public bool isConnected;
    public float timestamp;

    public string GetDebugInfo()
    {
        return $"Weather: {weatherName}, Temp: {temperature:F1}°C, Humidity: {humidity:F2}, Precip: {precipitation:F2}, Connected: {isConnected}";
    }
}

/// <summary>
/// Enhanced save data structure for Cozy weather state
/// </summary>
[System.Serializable]
public class CozyWeatherSaveData
{
    public string weatherName = "Clear";
    public float temperature = 20f;
    public float humidity = 0.5f;
    public float precipitation = 0f;
    public System.DateTime saveTimestamp;
    public bool cozyConnected = false;
    public bool usesNativeSave = false;

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(weatherName) &&
               temperature > -100f && temperature < 100f &&
               precipitation >= 0f &&
               humidity >= 0f && humidity <= 1f;
    }

    public string GetDebugInfo()
    {
        return $"Weather: {weatherName}, Temp: {temperature:F1}°C, Humidity: {humidity:F2}, Precip: {precipitation:F2}, Native Save: {usesNativeSave}, Saved: {saveTimestamp:yyyy-MM-dd HH:mm}";
    }
}