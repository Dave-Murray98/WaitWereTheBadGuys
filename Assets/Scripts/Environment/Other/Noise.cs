using UnityEngine;
using System.Collections;

/// <summary>
/// Noise component that represents a sound source in the game world.
/// Now works with NoisePool for efficient object reuse instead of constant creation/destruction.
/// </summary>
public class Noise : MonoBehaviour
{
    [Header("Noise Settings")]
    [SerializeField, Range(0f, 20f)]
    [Tooltip("How loud this noise is. 2 = Opening a container, 10 = Smashing Something")]
    private float volume = 1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Pool management
    private NoisePool parentPool;
    private Coroutine lifetimeCoroutine;
    private bool isPooled = false;

    // Public properties
    public float Volume => volume;
    public bool IsActive => gameObject.activeInHierarchy;

    #region Pooling Interface
    /// <summary>
    /// Initialize this noise for use with object pooling
    /// </summary>
    public void InitializeForPooling(NoisePool pool)
    {
        parentPool = pool;
        isPooled = true;

        if (enableDebugLogs)
        {
            Debug.Log($"[Noise] Initialized for pooling with {pool.name}");
        }
    }

    /// <summary>
    /// Activate this noise with specific settings (called when retrieved from pool)
    /// </summary>
    public void ActivateNoise(float newVolume, float lifetime)
    {
        SetVolume(newVolume);

        // Start the lifetime countdown
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
        }
        lifetimeCoroutine = StartCoroutine(LifetimeCoroutine(lifetime));

        if (enableDebugLogs)
        {
            Debug.Log($"[Noise] Activated with volume {volume} at {transform.position}. Lifetime: {lifetime}s");
        }
    }

    /// <summary>
    /// Deactivate this noise (called when returned to pool)
    /// </summary>
    public void DeactivateNoise()
    {
        // Stop any running coroutines
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }

        // Reset properties
        volume = 1f;

        if (enableDebugLogs)
        {
            Debug.Log($"[Noise] Deactivated and reset");
        }
    }
    #endregion

    #region Lifetime Management
    /// <summary>
    /// Coroutine that handles the noise lifetime and returns it to pool
    /// </summary>
    private IEnumerator LifetimeCoroutine(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        ReturnToPool();
    }

    /// <summary>
    /// Return this noise to the pool (or destroy if not pooled)
    /// </summary>
    private void ReturnToPool()
    {
        if (isPooled && parentPool != null)
        {
            // Return to pool
            parentPool.ReturnNoise(gameObject);
        }
        else
        {
            // Not pooled, destroy normally
            Destroy(gameObject);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[Noise] Lifetime expired, returned to pool");
        }
    }
    #endregion

    #region Public Interface
    /// <summary>
    /// Set the volume of this noise at runtime
    /// </summary>
    /// <param name="newVolume">New volume value (0-10)</param>
    public void SetVolume(float newVolume)
    {
        volume = newVolume;

        if (enableDebugLogs)
        {
            Debug.Log($"[Noise] Volume changed to {volume}");
        }
    }

    /// <summary>
    /// Manually end this noise early (return to pool immediately)
    /// </summary>
    public void EndNoise()
    {
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }
        ReturnToPool();
    }

    /// <summary>
    /// Extend the lifetime of this noise
    /// </summary>
    public void ExtendLifetime(float additionalTime)
    {
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = StartCoroutine(LifetimeCoroutine(additionalTime));

            if (enableDebugLogs)
            {
                Debug.Log($"[Noise] Lifetime extended by {additionalTime}s");
            }
        }
    }
    #endregion

    #region Static Utility Methods
    /// <summary>
    /// Create a noise using the pool system (preferred method)
    /// </summary>
    /// <param name="position">Where to create the noise</param>
    /// <param name="volume">How loud the noise should be</param>
    /// <param name="lifetime">How long the noise should last (optional)</param>
    /// <returns>The noise GameObject from the pool</returns>
    public static GameObject CreatePooledNoise(Vector3 position, float volume, float lifetime = 2f)
    {
        return NoisePool.CreateNoise(position, volume, lifetime);
    }

    /// <summary>
    /// Create a noise without using the pool (fallback method - not recommended for frequent use)
    /// </summary>
    /// <param name="position">Where to create the noise</param>
    /// <param name="volume">How loud the noise should be</param>
    /// <param name="lifetime">How long the noise should last (optional)</param>
    /// <returns>The created noise GameObject</returns>
    public static GameObject CreateNonPooledNoise(Vector3 position, float volume, float lifetime = 2f)
    {
        // Create a new GameObject for the noise
        GameObject noiseObject = new GameObject($"NonPooled_Noise_Vol{volume:F1}");
        noiseObject.transform.position = position;

        // Set it to the Noise layer
        noiseObject.layer = LayerMask.NameToLayer("Noise");

        // Add a box collider for detection
        BoxCollider collider = noiseObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = Vector3.one * 0.5f;

        // Add the Noise component
        Noise noiseComponent = noiseObject.AddComponent<Noise>();
        noiseComponent.SetVolume(volume);

        // Start lifetime management without pooling
        noiseComponent.StartCoroutine(noiseComponent.NonPooledLifetimeCoroutine(lifetime));

        return noiseObject;
    }

    /// <summary>
    /// Lifetime coroutine for non-pooled noises
    /// </summary>
    private IEnumerator NonPooledLifetimeCoroutine(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        Destroy(gameObject);
    }
    #endregion

    #region Debug and Gizmos
    private void OnDrawGizmosSelected()
    {
        // Draw a sphere representing the noise with size based on volume
        Gizmos.color = isPooled ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, volume * 0.5f);

        // Draw the actual collider bounds
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = gameObject.activeInHierarchy ? Color.yellow : Color.gray;
            Gizmos.DrawWireCube(transform.position, col.bounds.size);
        }

        // Draw connection to pool if pooled
        if (isPooled && parentPool != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, parentPool.transform.position);
        }
    }
    #endregion
}