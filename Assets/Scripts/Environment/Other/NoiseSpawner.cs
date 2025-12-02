using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

public class NoiseSpawner : MonoBehaviour
{
    [SerializeField] private Key spawnNoiseKey = Key.L;


    [Button()]
    public void SpawnNoiseAtPosition(float volume) => NoisePool.Instance.GetNoise(transform.position, volume);

    void Update()
    {
        if (Keyboard.current[spawnNoiseKey].wasPressedThisFrame)
        {
            SpawnNoiseAtPosition(100f);
        }
    }
}
