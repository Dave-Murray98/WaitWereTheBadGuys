using UnityEngine;

/// <summary>
/// Simple doorway component for scene transitions. Handles player interaction and
/// delegates actual scene loading to SceneTransitionManager for coordinated transitions.
/// Each doorway has a unique ID and specifies target scene and destination doorway.
/// </summary>
public class Doorway : MonoBehaviour
{
    [Header("Doorway Settings")]
    public string doorwayID = "";
    public string targetScene = "";
    public string targetDoorwayID = "";

    [Header("Interaction")]
    public bool requiresInteraction = true;
    public string interactionPrompt = "Press E to enter";

    private void Start()
    {
        if (string.IsNullOrEmpty(doorwayID))
        {
            Debug.LogWarning("Doorway ID not set, generating based on position.");
            doorwayID = $"Door_{transform.position.x}_{transform.position.z}";
        }
    }

    /// <summary>
    /// Initiates doorway transition by delegating to SceneTransitionManager.
    /// SceneTransitionManager handles data persistence, loading screens, and restoration.
    /// </summary>
    public void UseDoorway()
    {
        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogError("Target scene is not set for doorway: " + doorwayID);
            return;
        }

        Debug.Log($"[Doorway] Using doorway {doorwayID} to transition to {targetScene}");

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionThroughDoorway(targetScene, targetDoorwayID);
        }
        else
        {
            Debug.LogError("[Doorway] SceneTransitionManager not found!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (!requiresInteraction)
            {
                UseDoorway();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Could hide interaction UI here if needed
        }
    }

    /// <summary>
    /// Returns doorway information for debugging and validation.
    /// </summary>
    public string GetDoorwayInfo()
    {
        return $"Doorway '{doorwayID}' -> Scene: {targetScene}, Target: {targetDoorwayID}";
    }

    /// <summary>
    /// Validates that doorway configuration is complete.
    /// </summary>
    public bool IsValid()
    {
        bool isValid = !string.IsNullOrEmpty(doorwayID) &&
                      !string.IsNullOrEmpty(targetScene) &&
                      !string.IsNullOrEmpty(targetDoorwayID);

        if (!isValid)
        {
            Debug.LogWarning($"Doorway validation failed: ID='{doorwayID}', Scene='{targetScene}', Target='{targetDoorwayID}'");
        }

        return isValid;
    }

    private void OnValidate()
    {
        // Auto-generate doorway ID in editor if empty
        if (string.IsNullOrEmpty(doorwayID))
        {
            doorwayID = $"Door_{name}";
        }
    }

    private void OnDrawGizmos()
    {
        // Visual representation in scene view
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position, new Vector3(2f, 3f, 0.5f));

        // Arrow pointing forward
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        // Display doorway info
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
            $"ID: {doorwayID}\n→ {targetScene}:{targetDoorwayID}");
#endif
    }
}