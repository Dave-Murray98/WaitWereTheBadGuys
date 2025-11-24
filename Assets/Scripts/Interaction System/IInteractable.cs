using UnityEngine;

/// <summary>
/// Core interface for all interactable objects
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Unique identifier for this interactable (for save system)
    /// </summary>
    string InteractableID { get; }

    /// <summary>
    /// The transform of this interactable (for distance calculations)
    /// </summary>
    Transform Transform { get; }

    /// <summary>
    /// Whether this object can currently be interacted with
    /// </summary>
    bool CanInteract { get; }

    /// <summary>
    /// Maximum distance at which this object can be interacted with
    /// </summary>
    float InteractionRange { get; }

    /// <summary>
    /// Text to display in the interaction prompt (e.g., "Press E to open door")
    /// </summary>
    string GetInteractionPrompt();

    /// <summary>
    /// Called when the player interacts with this object
    /// </summary>
    /// <param name="player">The player performing the interaction</param>
    /// <returns>True if interaction was successful</returns>
    bool Interact(GameObject player);

    /// <summary>
    /// Called when player enters interaction range
    /// </summary>
    void OnPlayerEnterRange(GameObject player);

    /// <summary>
    /// Called when player exits interaction range
    /// </summary>
    void OnPlayerExitRange(GameObject player);
}
