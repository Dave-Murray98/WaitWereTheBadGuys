using UnityEngine;

/// <summary>
/// Data structure for interaction events
/// </summary>
[System.Serializable]
public class InteractionEventData
{
    public string interactableID;
    public string interactableName;
    public Vector3 interactionPosition;
    public System.DateTime interactionTime;
    public bool wasSuccessful;

    public InteractionEventData(IInteractable interactable, bool successful)
    {
        interactableID = interactable.InteractableID;
        interactableName = interactable.Transform.name;
        interactionPosition = interactable.Transform.position;
        interactionTime = System.DateTime.Now;
        wasSuccessful = successful;
    }
}