using UnityEngine;
using TMPro;
using Sirenix.OdinInspector;


public class WeatherDebugUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    public TextMeshProUGUI weatherText;
    [FoldoutGroup("References")]
    public TextMeshProUGUI temperatureText;

    private void Start()
    {
        if (WeatherManager.Instance != null)
        {
            WeatherManager.OnWeatherChanged += UpdateWeatherText;
            WeatherManager.OnTemperatureChanged += UpdateTemperatureText;

            UpdateWeatherText();
            UpdateTemperatureText();
        }
        else
        {
            Debug.LogWarning("WeatherManager not found");
        }
    }


    // private void Update()
    // {
    //     if (WeatherManager.Instance != null)
    //     {
    //         UpdateWeatherText();
    //         UpdateTemperatureText();
    //     }

    // }

    private void UpdateTemperatureText(float temperature = 0)
    {
        if (temperatureText != null)
        {
            // Convert current temperature
            float currentTemperatureCelcius = (WeatherManager.Instance.GetCurrentTemperature() - 32) * 5 / 9;
            temperatureText.text = currentTemperatureCelcius.ToString();
        }
    }

    private void UpdateWeatherText(string weatherName = null)
    {
        if (weatherText != null)
        {
            weatherText.text = WeatherManager.Instance.GetCurrentWeatherName().ToString();
        }
    }
}