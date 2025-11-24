using UnityEngine;
using System;
using Sirenix.OdinInspector;
using DistantLands.Cozy;

/// <summary>
/// Time manager that uses Cozy Weather 3's native time system and calculates seasons
/// based on Cozy's perennial profile configuration. Uses Cozy's day/year system directly
/// and derives season information without interfering with Cozy's time progression.
/// 
/// PURE COZY APPROACH: Uses Cozy's MeridiemTime and day system, calculates our own
/// season tracking as supplementary data for other game systems.
/// </summary>
public class InGameTimeManager : MonoBehaviour, IManager
{
    public static InGameTimeManager Instance { get; private set; }

    [Header("Cozy Integration")]
    [SerializeField] private bool useCozyTimeSystem = true;
    [SerializeField] private float cozyReadInterval = 0.1f; // How often to read from Cozy
    [SerializeField] private bool enableEventTracking = true;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Cozy connection state
    [ShowInInspector, ReadOnly] private bool isCozyConnected = false;
    [ShowInInspector, ReadOnly] private CozyTimeModule timeModule;
    [ShowInInspector, ReadOnly] private CozyClimateModule climateModule;
    [ShowInInspector, ReadOnly] private float lastCozyReadTime = 0f;

    // Current time state (using Cozy's native format)
    [ShowInInspector, ReadOnly] private MeridiemTime currentTime = new MeridiemTime(6, 0);
    [ShowInInspector, ReadOnly] private int currentDay = 1; // Cozy's day system
    [ShowInInspector, ReadOnly] private float currentTemperature = 20f;

    // Season calculation data (derived from Cozy's perennial profile)
    [ShowInInspector, ReadOnly] private int daysPerYear = 48; // From Cozy's perennial profile
    [ShowInInspector, ReadOnly] private int daysPerSeason = 12; // Calculated (daysPerYear / 4)
    [ShowInInspector, ReadOnly] private SeasonType currentSeason = SeasonType.Winter;
    [ShowInInspector, ReadOnly] private int currentDayOfSeason = 1; // Our season tracking (1-based)

    // Change tracking for events
    [ShowInInspector, ReadOnly] private MeridiemTime previousTime = new MeridiemTime(-1, 0);
    [ShowInInspector, ReadOnly] private int previousDay = -1;
    [ShowInInspector, ReadOnly] private SeasonType previousSeason = (SeasonType)(-1);

    // Events for external systems
    public static event Action<MeridiemTime> OnTimeChanged; // Cozy's time format
    public static event Action<int> OnDayChanged; // Cozy's day number
    public static event Action<SeasonType, int> OnSeasonChanged; // Our season + day of season
    public static event Action<TimeData> OnTimeDataUpdated; // Complete time data

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DebugLog("InGameTimeManager initialized for pure Cozy integration with season calculation");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (useCozyTimeSystem && isCozyConnected)
        {
            ReadFromCozy();
        }
    }

    #region IManager Implementation

    public void Initialize()
    {
        ConnectToCozy();

        if (isCozyConnected)
        {
            ReadCozyConfiguration();
            ReadInitialCozyState();
        }
        else
        {
            Debug.LogError("[InGameTimeManager] Cozy Weather 3 is required for this time system to function!");
        }

        DebugLog($"Initialized - {GetFormattedDateTime()}");
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

    #region Cozy Integration

    /// <summary>
    /// Connects to Cozy Weather 3's time and climate systems
    /// </summary>
    private void ConnectToCozy()
    {
        if (!useCozyTimeSystem)
        {
            isCozyConnected = false;
            DebugLog("Cozy time system disabled");
            return;
        }

        if (CozyWeather.instance?.timeModule != null)
        {
            timeModule = CozyWeather.instance.timeModule;
            climateModule = CozyWeather.instance.climateModule; // Optional
            isCozyConnected = true;
            DebugLog("Successfully connected to Cozy time system");
        }
        else
        {
            isCozyConnected = false;
            Debug.LogError("[InGameTimeManager] Cozy time module not found! Please ensure Cozy Weather 3 is properly set up.");
        }
    }

    /// <summary>
    /// Reads configuration from Cozy's perennial profile to set up season calculations
    /// </summary>
    private void ReadCozyConfiguration()
    {
        if (!isCozyConnected || timeModule == null) return;

        try
        {
            // Try to get days per year from Cozy's perennial profile
            var perennialProfile = timeModule.GetType().GetProperty("perennialProfile")?.GetValue(timeModule);
            if (perennialProfile != null)
            {
                var daysProperty = perennialProfile.GetType().GetProperty("daysPerYear");
                if (daysProperty != null)
                {
                    daysPerYear = (int)daysProperty.GetValue(perennialProfile);
                }
            }

            // Calculate days per season (divide by 4 seasons)
            daysPerSeason = Mathf.Max(1, daysPerYear / 4);

            DebugLog($"Cozy configuration - Days per year: {daysPerYear}, Days per season: {daysPerSeason}");
        }
        catch (System.Exception e)
        {
            DebugLog($"Could not read Cozy configuration: {e.Message}. Using defaults.");
            daysPerYear = 48;
            daysPerSeason = 12;
        }
    }

    /// <summary>
    /// Reads initial time state from Cozy and calculates initial season
    /// </summary>
    private void ReadInitialCozyState()
    {
        if (!isCozyConnected) return;

        try
        {
            ReadCozyTimeData();
            CalculateSeasonFromCozyDay();

            // Initialize previous values for change detection
            previousTime = new MeridiemTime(currentTime.hours, currentTime.minutes);
            previousDay = currentDay;
            previousSeason = currentSeason;

            DebugLog($"Initial Cozy time: {GetFormattedDateTime()}");
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading initial Cozy state: {e.Message}");
        }
    }

    /// <summary>
    /// Reads current time data from Cozy at regular intervals
    /// </summary>
    private void ReadFromCozy()
    {
        if (Time.time - lastCozyReadTime < cozyReadInterval) return;
        lastCozyReadTime = Time.time;

        if (!isCozyConnected)
        {
            ConnectToCozy();
            return;
        }

        try
        {
            ReadCozyTimeData();
            CalculateSeasonFromCozyDay();

            if (enableEventTracking)
            {
                CheckForTimeChanges();
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading from Cozy: {e.Message}");
            isCozyConnected = false;
        }
    }

    /// <summary>
    /// Reads time data from Cozy's native systems
    /// </summary>
    private void ReadCozyTimeData()
    {
        if (timeModule == null) return;

        // Read Cozy's native time format
        currentTime = timeModule.currentTime;
        currentDay = timeModule.currentDay;

        // Read additional data from climate module if available
        if (climateModule != null)
        {
            try
            {
                currentTemperature = climateModule.currentTemperature;
            }
            catch (System.Exception e)
            {
                DebugLog($"Error reading climate data: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Calculates current season and day of season based on Cozy's day and our season system.
    /// Assumes the year starts in winter (like January 1st), roughly halfway through.
    /// </summary>
    private void CalculateSeasonFromCozyDay()
    {
        if (daysPerSeason <= 0) return;

        // Cozy starts at day 1, which we consider to be roughly halfway through winter
        // So we offset by half a winter season
        int winterOffset = daysPerSeason / 2;
        int adjustedDay = currentDay + winterOffset - 1; // -1 for 0-based calculation

        // Calculate which season we're in
        int seasonIndex = (adjustedDay / daysPerSeason) % 4;
        currentSeason = (SeasonType)seasonIndex;

        // Calculate day within the current season (1-based)
        currentDayOfSeason = (adjustedDay % daysPerSeason) + 1;

        // Ensure day of season is within valid range
        currentDayOfSeason = Mathf.Clamp(currentDayOfSeason, 1, daysPerSeason);
    }

    #endregion

    #region Change Detection and Events

    /// <summary>
    /// Checks for time changes and fires appropriate events
    /// </summary>
    private void CheckForTimeChanges()
    {
        // Check for time changes (compare total minutes)
        int currentTotalMinutes = (currentTime.hours * 60) + currentTime.minutes;
        int previousTotalMinutes = (previousTime.hours * 60) + previousTime.minutes;

        if (Mathf.Abs(currentTotalMinutes - previousTotalMinutes) >= 1) // At least 1 minute change
        {
            OnTimeChanged?.Invoke(currentTime);
            previousTime = new MeridiemTime(currentTime.hours, currentTime.minutes);
        }

        // Check for day changes
        if (currentDay != previousDay)
        {
            DebugLog($"Day changed: {previousDay} → {currentDay}");
            OnDayChanged?.Invoke(currentDay);
            previousDay = currentDay;
        }

        // Check for season changes
        if (currentSeason != previousSeason)
        {
            DebugLog($"Season changed: {previousSeason} → {currentSeason} (Day {currentDayOfSeason} of season)");
            OnSeasonChanged?.Invoke(currentSeason, currentDayOfSeason);
            previousSeason = currentSeason;
        }

        // Fire comprehensive update event
        OnTimeDataUpdated?.Invoke(GetCurrentTimeData());
    }

    #endregion

    #region Public Time Control API

    /// <summary>
    /// Manually sets the time (updates Cozy if connected)
    /// </summary>
    [Button("Set Time")]
    public void SetTime(int hours, int minutes)
    {
        var newTime = new MeridiemTime(hours, minutes);

        if (isCozyConnected && timeModule != null)
        {
            // Set time in Cozy
            timeModule.currentTime = newTime;
            DebugLog($"Set Cozy time to: {newTime}");
        }
        else
        {
            Debug.LogError("[InGameTimeManager] Cannot set time - not connected to Cozy!");
            return;
        }

        // Fire event
        OnTimeChanged?.Invoke(currentTime);
    }

    /// <summary>
    /// Manually sets the time using float hours (for convenience)
    /// </summary>
    public void SetTimeOfDay(float hours)
    {
        int h = Mathf.FloorToInt(hours);
        int m = Mathf.FloorToInt((hours - h) * 60f);
        SetTime(h, m);
    }

    /// <summary>
    /// Manually sets the current day (updates Cozy if connected)
    /// </summary>
    [Button("Set Day")]
    public void SetDay(int day)
    {
        day = Mathf.Max(1, day);

        if (isCozyConnected && timeModule != null)
        {
            // Set day in Cozy
            timeModule.currentDay = day;
            DebugLog($"Set Cozy day to: {day}");
        }
        else
        {
            Debug.LogError("[InGameTimeManager] Cannot set day - not connected to Cozy!");
            return;
        }

        // Fire event
        OnDayChanged?.Invoke(currentDay);
    }

    /// <summary>
    /// Sets complete date and time using Cozy's native format
    /// </summary>
    [Button("Set DateTime")]
    public void SetDateTime(int hours, int minutes, int day)
    {
        SetTime(hours, minutes);
        SetDay(day);
        DebugLog($"Set complete date/time to: {GetFormattedDateTime()}");
    }

    #endregion

    #region Internal Time Management (Save Component Interface)

    /// <summary>
    /// Internal method for save component to set time data during restoration
    /// </summary>
    internal void RestoreTimeData(TimeData timeData)
    {
        if (timeData == null) return;

        DebugLog($"Restoring time data: {timeData.GetFormattedDateTime()}");

        // Apply the time data using our public API
        SetDateTime(timeData.time.hours, timeData.time.minutes, timeData.day);

        DebugLog($"Time data restored successfully");
    }

    /// <summary>
    /// Internal method for save component to get current time data
    /// </summary>
    internal TimeData GetTimeDataForSaving()
    {
        var timeData = new TimeData
        {
            time = new MeridiemTime(currentTime.hours, currentTime.minutes),
            day = currentDay,
            temperature = currentTemperature,
            season = currentSeason,
            dayOfSeason = currentDayOfSeason,
            daysPerYear = daysPerYear,
            daysPerSeason = daysPerSeason,
            wasCozyDriven = isCozyConnected,
            saveTimestamp = System.DateTime.Now
        };

        DebugLog($"Providing time data for saving: {timeData.GetFormattedDateTime()}");
        return timeData;
    }

    #endregion

    #region Public API for External Scripts

    /// <summary>
    /// Gets current time in Cozy's MeridiemTime format
    /// </summary>
    public MeridiemTime GetCurrentTime() => currentTime;

    /// <summary>
    /// Gets current time as float (0-24 hours) for compatibility
    /// </summary>
    public float GetCurrentTimeOfDay() => currentTime.hours + (currentTime.minutes / 60f);

    /// <summary>
    /// Gets current day using Cozy's day system
    /// </summary>
    public int GetCurrentDay() => currentDay;

    /// <summary>
    /// Gets current season (our calculated season)
    /// </summary>
    public SeasonType GetCurrentSeason() => currentSeason;

    /// <summary>
    /// Gets current day within the season (our calculated value)
    /// </summary>
    public int GetCurrentDayOfSeason() => currentDayOfSeason;

    /// <summary>
    /// Gets days per season (calculated from Cozy's year length)
    /// </summary>
    public int GetDaysPerSeason() => daysPerSeason;

    /// <summary>
    /// Gets days per year (from Cozy's perennial profile)
    /// </summary>
    public int GetDaysPerYear() => daysPerYear;

    /// <summary>
    /// Gets current temperature from Cozy
    /// </summary>
    public float GetCurrentTemperature() => currentTemperature;

    /// <summary>
    /// Checks if it's currently daytime (6 AM to 6 PM)
    /// </summary>
    public bool IsDaytime() => currentTime.hours >= 6 && currentTime.hours < 18;

    /// <summary>
    /// Checks if it's currently nighttime
    /// </summary>
    public bool IsNighttime() => !IsDaytime();

    /// <summary>
    /// Checks if Cozy time system is connected and active
    /// </summary>
    public bool IsCozyConnected() => isCozyConnected;

    /// <summary>
    /// Gets complete time data structure for external use
    /// </summary>
    public TimeData GetCurrentTimeData()
    {
        return new TimeData
        {
            time = new MeridiemTime(currentTime.hours, currentTime.minutes),
            day = currentDay,
            temperature = currentTemperature,
            season = currentSeason,
            dayOfSeason = currentDayOfSeason,
            daysPerYear = daysPerYear,
            daysPerSeason = daysPerSeason,
            wasCozyDriven = isCozyConnected,
            saveTimestamp = System.DateTime.Now
        };
    }

    /// <summary>
    /// Gets formatted time string (HH:MM format)
    /// </summary>
    public string GetFormattedTime()
    {
        return $"{currentTime.hours:D2}:{currentTime.minutes:D2}";
    }

    /// <summary>
    /// Gets formatted date string using our season system
    /// </summary>
    public string GetFormattedDate()
    {
        return $"Day {currentDayOfSeason} of {currentSeason} (Day {currentDay})";
    }

    /// <summary>
    /// Gets formatted date and time string
    /// </summary>
    public string GetFormattedDateTime()
    {
        return $"{GetFormattedDate()} at {GetFormattedTime()}";
    }

    #endregion


    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[InGameTimeManager] {message}");
        }
    }

}

public enum SeasonType { Winter, Spring, Summer, Autumn };