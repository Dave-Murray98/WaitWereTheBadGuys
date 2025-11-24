using System.Collections;

/// <summary>
/// Information about a save file for UI display
/// </summary>
[System.Serializable]
public class SaveFileInfo
{
    public System.DateTime saveTime;
    public string sceneName;
    public float playTime;
    public int playerLevel;

    public string GetFormattedSaveTime()
    {
        return saveTime.ToString("MM/dd/yyyy HH:mm");
    }

    public string GetFormattedPlayTime()
    {
        System.TimeSpan time = System.TimeSpan.FromSeconds(playTime);
        return string.Format("{0:D2}:{1:D2}:{2:D2}", time.Hours, time.Minutes, time.Seconds);
    }
}