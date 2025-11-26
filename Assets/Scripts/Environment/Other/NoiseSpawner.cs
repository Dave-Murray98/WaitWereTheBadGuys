using Sirenix.OdinInspector;
using UnityEngine;

public class NoiseSpawner : MonoBehaviour
{
    [Button()]
    public void SpawnNoiseAtPosition(float volume) => NoisePool.Instance.GetNoise(transform.position, volume);
}
