using UnityEngine;

// Type-specific data structures
[System.Serializable]
public class ConsumableData
{
    [Header("Consumption Effects")]
    public float healthRestore = 0f;
    public float hungerRestore = 0f;
    public float thirstRestore = 0f;
    [Tooltip("Time it takes to consume this item in seconds")]
    public float consumeTime = 2f;
    [Tooltip("Can this item be consumed multiple times before being destroyed?")]
    public bool multiUse = false;
    public int maxUses = 1;
}
