/// <summary>
/// Extended interface for interactables that need save/load functionality
/// </summary>
public interface IPersistentInteractable : IInteractable, ISaveable
{
    /// <summary>
    /// Whether this interactable has been used/activated
    /// </summary>
    bool HasBeenUsed { get; }

    /// <summary>
    /// Reset the interactable to its initial state
    /// </summary>
    void ResetInteractable();
}