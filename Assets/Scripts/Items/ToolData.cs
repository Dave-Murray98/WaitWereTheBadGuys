using UnityEngine;

[System.Serializable]
public class ToolData
{
    /// <summary>
    /// Whether the tool's action is triggered by a hold input (like a blowtorch) or a tap input (like placing C4).
    /// </summary>
    public bool isActionHeld = false;
}
