using UnityEngine;
using TMPro;
using Sirenix.OdinInspector;
using System;
using DistantLands.Cozy;


public class InGameTimeDebugUI : MonoBehaviour
{
    [Header("Text Components")]
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private TextMeshProUGUI seasonText;

    private void Start()
    {
        if (InGameTimeManager.Instance != null)
        {
            InGameTimeManager.OnTimeChanged += UpdateTimeText;
            InGameTimeManager.OnDayChanged += UpdateDateText;
            InGameTimeManager.OnSeasonChanged += UpdateSeasonText;

            UpdateTimeText(null);
            UpdateDateText(0);
            UpdateSeasonText(SeasonType.Summer, 0);
        }
        else
        {
            Debug.LogWarning("[InGameTimeDebugUI] InGameTimeManager not found!");
        }
    }

    // private void Update()
    // {
    //     if (InGameTimeManager.Instance != null)
    //     {
    //         UpdateTimeText(null);
    //         UpdateDateText(0);
    //         UpdateSeasonText(SeasonType.Summer, 0);
    //     }
    // }

    private void UpdateSeasonText(SeasonType season, int day)
    {
        if (seasonText != null)
        {
            seasonText.text = InGameTimeManager.Instance.GetCurrentSeason().ToString();
        }
    }

    private void UpdateDateText(int day)
    {
        if (dateText != null)
        {
            dateText.text = InGameTimeManager.Instance.GetCurrentDayOfSeason().ToString();
        }
    }

    private void UpdateTimeText(MeridiemTime time)
    {
        if (timeText != null)
        {
            timeText.text = InGameTimeManager.Instance.GetCurrentTime().ToString();
        }
    }
}