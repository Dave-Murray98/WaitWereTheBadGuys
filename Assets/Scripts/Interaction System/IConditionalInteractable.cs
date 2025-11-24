using UnityEngine;

/// <summary>
/// Interface for interactables that require specific conditions to be met
/// </summary>
public interface IConditionalInteractable : IInteractable
{
    /// <summary>
    /// Check if interaction requirements are met
    /// </summary>
    /// <param name="player">The player attempting to interact</param>
    /// <returns>True if requirements are met</returns>
    bool MeetsInteractionRequirements(GameObject player);

    /// <summary>
    /// Get message explaining why interaction is not available
    /// </summary>
    /// <returns>Message to show player (e.g., "You need a key")</returns>
    string GetRequirementFailureMessage();
}