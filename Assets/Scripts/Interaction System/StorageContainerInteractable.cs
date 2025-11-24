using UnityEngine;

public class StorageContainerInteractable : InteractableBase
{
    [Header("Vehicle Settings")]
    [SerializeField] private StorageContainer containerManager;
    [SerializeField] private bool autoFindContiner = true;
    public StorageContainerUI containerUI;


    protected override void Awake()
    {
        base.Awake();

        if (autoFindContiner && containerManager == null)
        {
            containerManager = GetComponent<StorageContainer>();
            if (containerManager == null)
            {
                containerManager = GetComponentInParent<StorageContainer>();
            }
        }

        if (containerUI == null)
        {
            containerUI = StorageContainerUI.Instance;
        }

        // Set interaction properties
        base.interactionRange = interactionRange;
    }

    protected override bool PerformInteraction(GameObject player)
    {
        if (containerManager == null)
        {
            DebugLog("No vehicle controller found!");
            return false;
        }

        OpenContainer();
        return true;
    }

    private void OpenContainer()
    {
        if (containerManager != null && containerUI != null)
        {
            containerUI.OpenContainer(containerManager);
        }
    }

}