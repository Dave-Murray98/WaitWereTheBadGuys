using UnityEngine;
using VHierarchy.Libs;

/// <summary>
/// UPDATED: Enhanced item pickup interactable integrated with simplified unified tracking system.
/// Now works seamlessly with the unified SceneItemStateManager for both original scene items
/// and dropped inventory items. Includes automatic position tracking for original items.
/// </summary>
public class ItemPickupInteractable : MonoBehaviour, IInteractable
{
    [Header("Item Settings")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private int quantity = 1;
    [SerializeField] private string interactableID;
    [SerializeField] private bool autoGenerateID = true;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 2f;
    [SerializeField] private string interactionPrompt = "";

    [Header("Component References")]
    [SerializeField] private Transform rootTransform;
    [SerializeField] private Rigidbody itemRigidbody;
    [SerializeField] private Collider itemCollider;

    [Header("Feedback")]
    [SerializeField] private GameObject pickupEffect;
    [SerializeField] private AudioClip pickupSound;

    [Header("Position Tracking")]
    [SerializeField] private bool enablePositionTracking = true;
    [SerializeField] private float trackingUpdateInterval = 2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Item type tracking
    private bool isDroppedInventoryItem = false;
    private bool isOriginalSceneItem = true;

    // Position tracking for original items
    private Vector3 lastTrackedPosition;
    private Vector3 lastTrackedRotation;
    private float lastTrackingUpdate;

    // IInteractable implementation
    public string InteractableID => interactableID;
    public Transform Transform => transform;
    public bool CanInteract => enabled && gameObject.activeInHierarchy && itemData != null;
    public float InteractionRange => interactionRange;

    private void Awake()
    {
        SetupComponentReferences();

        if (autoGenerateID && string.IsNullOrEmpty(interactableID))
        {
            GenerateUniqueID();
        }

        if (string.IsNullOrEmpty(interactionPrompt) && itemData != null)
        {
            string quantityText = quantity > 1 ? $" ({quantity})" : "";
            interactionPrompt = $"pick up {itemData.itemName}{quantityText}";
        }

        // Initialize position tracking for original items
        InitializePositionTracking();
    }

    private void Start()
    {
        // Check if this original scene item was already collected
        if (isOriginalSceneItem && SceneItemStateManager.Instance != null)
        {
            if (SceneItemStateManager.Instance.IsItemCollected(interactableID))
            {
                DebugLog($"Original scene item {interactableID} was previously collected - destroying immediately");
                Destroy(rootTransform ? rootTransform.gameObject : gameObject);
                return;
            }
        }

        DebugLog($"Item pickup {interactableID} initialized (Original: {isOriginalSceneItem}, Dropped: {isDroppedInventoryItem})");
    }

    private void Update()
    {
        // Update position tracking for original items
        UpdatePositionTracking();
    }

    #region Position Tracking System

    /// <summary>
    /// Initialize position tracking for original scene items
    /// </summary>
    private void InitializePositionTracking()
    {
        if (isOriginalSceneItem)
        {
            Transform root = rootTransform ?? transform;
            lastTrackedPosition = root.position;
            lastTrackedRotation = root.eulerAngles;
            DebugLog($"Initialized position tracking for original item {interactableID}");
        }
    }

    /// <summary>
    /// Update position tracking periodically
    /// </summary>
    private void UpdatePositionTracking()
    {
        if (isOriginalSceneItem && enablePositionTracking && Time.time > lastTrackingUpdate + trackingUpdateInterval)
        {
            CheckForPositionChanges();
            lastTrackingUpdate = Time.time;
        }
    }

    /// <summary>
    /// Checks if the item has moved or rotated since last check
    /// </summary>
    private void CheckForPositionChanges()
    {
        Transform root = rootTransform ?? transform;

        bool positionChanged = Vector3.Distance(root.position, lastTrackedPosition) > 0.01f;
        bool rotationChanged = Vector3.Distance(root.eulerAngles, lastTrackedRotation) > 1f;

        if (positionChanged || rotationChanged)
        {
            DebugLog($"Item {interactableID} moved - notifying SceneItemStateManager");
            DebugLog($"  Position: {lastTrackedPosition} -> {root.position}");
            DebugLog($"  Rotation: {lastTrackedRotation} -> {root.eulerAngles}");

            // Notify the unified manager about the change
            SceneItemStateManager.NotifyItemMoved(interactableID);

            // Update our tracking
            lastTrackedPosition = root.position;
            lastTrackedRotation = root.eulerAngles;
        }
    }

    /// <summary>
    /// Force an immediate tracking update
    /// </summary>
    private void ForceTrackingUpdate()
    {
        if (isOriginalSceneItem)
        {
            DebugLog($"Force updating tracking for {interactableID}");
            SceneItemStateManager.NotifyItemMoved(interactableID);

            Transform root = rootTransform ?? transform;
            lastTrackedPosition = root.position;
            lastTrackedRotation = root.eulerAngles;
        }
    }

    /// <summary>
    /// Detect significant physics impacts and update tracking
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (isOriginalSceneItem && enablePositionTracking)
        {
            // If item was hit by something significant, update tracking
            if (collision.relativeVelocity.magnitude > 2f)
            {
                DebugLog($"Item {interactableID} hit by {collision.gameObject.name} with velocity {collision.relativeVelocity.magnitude:F2}");
                Invoke(nameof(ForceTrackingUpdate), 0.1f); // Small delay for physics to settle
            }
        }
    }

    #endregion

    #region Component Setup

    /// <summary>
    /// Automatically sets up component references for the unified visual prefab system
    /// </summary>
    public void SetupComponentReferences()
    {
        // In the unified system, this component is a child of the visual prefab root
        // The root transform is always the parent (visual prefab)
        rootTransform = transform.parent;

        // If somehow this component is on the root itself, use self as root
        if (rootTransform == null)
        {
            rootTransform = transform;
        }

        // Find rigidbody on root (visual prefab should have physics)
        if (itemRigidbody == null)
        {
            itemRigidbody = rootTransform.GetComponent<Rigidbody>();
        }

        // Find collider on this GameObject (interaction collider)
        if (itemCollider == null)
        {
            itemCollider = GetComponent<Collider>();
        }

        DebugLog($"Component references setup - Root: {rootTransform != null}, Rigidbody: {itemRigidbody != null}, Collider: {itemCollider != null}");
    }

    #endregion

    #region IInteractable Implementation

    public string GetInteractionPrompt()
    {
        if (!CanInteract) return "";
        return interactionPrompt;
    }

    public bool Interact(GameObject player)
    {
        DebugLog($"Interact called by {player.name} for item {itemData?.itemName ?? "null"}");

        if (!CanInteract)
        {
            DebugLog("Cannot interact - item disabled or no data");
            return false;
        }

        // Try to add to inventory
        var inventory = PlayerInventoryManager.Instance;
        if (inventory == null)
        {
            DebugLog("No InventoryManager found");
            return false;
        }

        // ENHANCED: First try normal pickup
        if (inventory.HasSpaceForItem(itemData))
        {
            DebugLog($"Attempting to add {itemData.itemName} to inventory...");
            if (inventory.AddItem(itemData))
            {
                DebugLog($"Successfully added {itemData.itemName} to inventory - calling HandleSuccessfulPickup");
                HandleSuccessfulPickup();
                return true;
            }
            else
            {
                DebugLog($"Failed to add {itemData.itemName} to inventory despite having space");
                return false;
            }
        }
        else
        {
            // ENHANCED: Inventory is full - show pickup overflow UI
            DebugLog($"Inventory is full for {itemData.itemName} - showing pickup overflow UI");
            ShowPickupOverflowUI();
            return true; // Return true because we handled the interaction (even though item wasn't picked up)
        }
    }

    public void OnPlayerEnterRange(GameObject player)
    {
        DebugLog($"Player entered range of {interactableID}");
    }

    public void OnPlayerExitRange(GameObject player)
    {
        DebugLog($"Player exited range of {interactableID}");
    }

    #endregion

    #region Pickup Handling

    /// <summary>
    /// UPDATED: Simplified pickup handling using unified SceneItemStateManager
    /// </summary>
    private void HandleSuccessfulPickup()
    {
        // Play pickup effects
        PlayPickupEffects();

        // Notify the unified manager (works for both original and dropped items)
        SceneItemStateManager.OnItemPickedUp(interactableID);
        DebugLog($"Item {interactableID} picked up and reported to SceneItemStateManager");

        // Remove from ItemDropSystem tracking if it's a dropped item
        if (ItemDropSystem.Instance != null && isDroppedInventoryItem)
        {
            ItemDropSystem.Instance.RemoveDroppedItem(rootTransform ? rootTransform.gameObject : gameObject);
        }

        // Destroy the object
        rootTransform.gameObject.Destroy();
    }

    private void PlayPickupEffects()
    {
        // Spawn pickup effect
        if (pickupEffect != null)
        {
            Vector3 effectPosition = rootTransform ? rootTransform.position : transform.position;
            Instantiate(pickupEffect, effectPosition, Quaternion.identity);
        }

        // Play pickup sound
        if (pickupSound != null)
        {
            Vector3 soundPosition = rootTransform ? rootTransform.position : transform.position;
            AudioSource.PlayClipAtPoint(pickupSound, soundPosition);
        }
    }

    private void ShowInventoryFullMessage()
    {
        DebugLog("Inventory is full!");

        // Try to show a more user-friendly message
        if (itemData != null)
        {
            Debug.Log($"Cannot pick up {itemData.itemName} - Inventory is full! Make space and try again.");
        }
        else
        {
            Debug.Log("Cannot pick up item - Inventory is full!");
        }

        // TODO: Integrate with your UI notification system to show a proper message to the player
    }

    #endregion

    #region Pickup Overflow UI

    private void ShowPickupOverflowUI()
    {
        if (itemData == null)
        {
            DebugLog("Cannot show pickup overflow - no item data");
            return;
        }

        // Check if pickup overflow UI is available
        if (PickupOverflowUI.Instance == null)
        {
            DebugLog("PickupOverflowUI not found - falling back to full inventory message");
            ShowInventoryFullMessage();
            return;
        }

        // Check if pickup overflow UI is already open
        if (PickupOverflowUI.IsPickupOverflowOpen())
        {
            DebugLog("Pickup overflow UI already open - closing previous and opening new");
        }

        DebugLog($"Opening pickup overflow UI for {itemData.itemName}");

        // Subscribe to pickup overflow events to handle item pickup when transferred
        SubscribeToPickupOverflowEvents();

        // Show the pickup overflow UI
        PickupOverflowUI.Instance.ShowPickupOverflow(itemData);
    }

    private void SubscribeToPickupOverflowEvents()
    {
        if (PickupOverflowUI.Instance != null)
        {
            // Subscribe to transfer complete event
            PickupOverflowUI.Instance.OnPickupItemTransferred += OnPickupOverflowTransferred;
            PickupOverflowUI.Instance.OnPickupOverflowClosed += OnPickupOverflowClosed;
        }
    }

    private void UnsubscribeFromPickupOverflowEvents()
    {
        if (PickupOverflowUI.Instance != null)
        {
            PickupOverflowUI.Instance.OnPickupItemTransferred -= OnPickupOverflowTransferred;
            PickupOverflowUI.Instance.OnPickupOverflowClosed -= OnPickupOverflowClosed;
        }
    }

    /// <summary>
    /// NEW: Handle pickup overflow transfer completion.
    /// </summary>
    private void OnPickupOverflowTransferred(ItemData transferredItem)
    {
        if (transferredItem == itemData)
        {
            DebugLog($"Pickup overflow transfer completed for {itemData.itemName} - removing from world");

            // Unsubscribe from events
            UnsubscribeFromPickupOverflowEvents();

            // Handle successful pickup (remove from world, play effects, etc.)
            HandleSuccessfulPickup();
        }
    }

    /// <summary>
    /// NEW: Handle pickup overflow UI closing.
    /// </summary>
    private void OnPickupOverflowClosed(ItemData closedItem, bool wasTransferred)
    {
        if (closedItem == itemData)
        {
            DebugLog($"Pickup overflow closed for {itemData.itemName} - transferred: {wasTransferred}");

            // Unsubscribe from events
            UnsubscribeFromPickupOverflowEvents();

            if (!wasTransferred)
            {
                DebugLog($"Item {itemData.itemName} was not transferred - remains in world");
                // Item remains in world for future pickup attempts
            }
        }
    }

    #endregion

    #region ID Generation

    private void GenerateUniqueID()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Vector3 position = rootTransform ? rootTransform.position : transform.position;
        string positionString = position.ToString("F2");
        interactableID = $"Item_{sceneName}_{positionString}";
    }

    #endregion

    #region Public Configuration Methods

    /// <summary>
    /// Set the item data for this pickup
    /// </summary>
    public void SetItemData(ItemData newItemData, int newQuantity = 1)
    {
        itemData = newItemData;
        quantity = newQuantity;

        if (itemData != null)
        {
            string quantityText = quantity > 1 ? $" ({quantity})" : "";
            interactionPrompt = $"pick up {itemData.itemName}{quantityText}";
            DebugLog($"Item data set to {itemData.itemName}");
        }
    }

    /// <summary>
    /// Set a custom interactable ID
    /// </summary>
    public void SetInteractableID(string newID)
    {
        interactableID = newID;
        autoGenerateID = false;
    }

    /// <summary>
    /// Mark this as a dropped inventory item (not an original scene item)
    /// </summary>
    public void MarkAsDroppedItem()
    {
        isDroppedInventoryItem = true;
        isOriginalSceneItem = false;
        enablePositionTracking = false; // Dropped items don't need position tracking
        DebugLog($"Item {interactableID} marked as dropped inventory item");
    }

    /// <summary>
    /// Mark this as an original scene item (default behavior)
    /// </summary>
    public void MarkAsOriginalSceneItem()
    {
        isOriginalSceneItem = true;
        isDroppedInventoryItem = false;
        enablePositionTracking = true; // Original items need position tracking
        InitializePositionTracking(); // Reset tracking
        DebugLog($"Item {interactableID} marked as original scene item");
    }

    /// <summary>
    /// Configure the item for physics simulation
    /// </summary>
    public void ConfigurePhysics(ItemDropSettings settings)
    {
        if (itemRigidbody == null || settings == null)
            return;

        if (settings.usePhysicsSimulation && itemData.usePhysicsOnDrop)
        {
            itemRigidbody.isKinematic = false;
            itemRigidbody.mass = itemData.objectMass;
            itemRigidbody.linearDamping = settings.itemDrag;
            itemRigidbody.angularDamping = settings.itemAngularDrag;

            // Apply initial drop force
            Vector3 dropForce = settings.GetRandomDropForce();
            if (dropForce.magnitude > 0f)
            {
                itemRigidbody.AddForce(dropForce, ForceMode.Impulse);
            }

            DebugLog($"Physics configured for {interactableID} with mass: {itemRigidbody.mass}, drop force: {dropForce}");

            // Schedule physics settling if enabled
            if (settings.enablePhysicsSettling)
            {
                Invoke(nameof(SettlePhysics), settings.physicsSettleTime);
            }
        }
        else
        {
            itemRigidbody.isKinematic = true;
            DebugLog($"Physics disabled for {interactableID}");
        }
    }

    /// <summary>
    /// Settle physics after specified time
    /// </summary>
    private void SettlePhysics()
    {
        if (itemRigidbody != null)
        {
            itemRigidbody.isKinematic = true;
            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
            DebugLog($"Physics settled for {interactableID}");
        }
    }

    /// <summary>
    /// Freeze the item in place
    /// </summary>
    public void FreezePhysics()
    {
        if (itemRigidbody != null)
        {
            itemRigidbody.isKinematic = true;
            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }
    }

    #endregion

    #region Position Tracking Control

    /// <summary>
    /// Enable or disable position tracking
    /// </summary>
    public void SetPositionTracking(bool enabled)
    {
        enablePositionTracking = enabled;
        DebugLog($"Position tracking {(enabled ? "enabled" : "disabled")} for {interactableID}");
    }

    /// <summary>
    /// Reset tracking to current position (useful after programmatic moves)
    /// </summary>
    public void ResetPositionTracking()
    {
        if (isOriginalSceneItem)
        {
            Transform root = rootTransform ?? transform;
            lastTrackedPosition = root.position;
            lastTrackedRotation = root.eulerAngles;
            DebugLog($"Reset position tracking for {interactableID}");
        }
    }

    /// <summary>
    /// Check if this item has moved from its tracked position
    /// </summary>
    public bool HasMovedFromSpawn()
    {
        if (!isOriginalSceneItem) return false;

        Transform root = rootTransform ?? transform;
        return Vector3.Distance(root.position, lastTrackedPosition) > 0.01f;
    }

    #endregion

    #region Getters

    /// <summary>
    /// Get the item data
    /// </summary>
    public ItemData GetItemData() => itemData;

    /// <summary>
    /// Check if this is a dropped inventory item
    /// </summary>
    public bool IsDroppedInventoryItem => isDroppedInventoryItem;

    /// <summary>
    /// Check if this is an original scene item
    /// </summary>
    public bool IsOriginalSceneItem => isOriginalSceneItem;

    /// <summary>
    /// Get the root transform of this item (visual prefab root)
    /// </summary>
    public Transform GetRootTransform() => rootTransform;

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ItemPickupInteractable:{interactableID}] {message}");
        }
    }

    #endregion

    protected virtual void OnDestroy()
    {
        // Clean up pickup overflow event subscriptions
        UnsubscribeFromPickupOverflowEvents();
    }

    #region Editor Support

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure component references are setup in editor
        if (Application.isPlaying)
            return;

        SetupComponentReferences();

        // Auto-generate ID if needed
        if (autoGenerateID && string.IsNullOrEmpty(interactableID))
        {
            GenerateUniqueID();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw interaction range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        // Draw line to root if this is a child
        if (rootTransform != null && rootTransform != transform)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, rootTransform.position);
        }

        // Show tracking status for original items
        if (isOriginalSceneItem && enablePositionTracking)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.1f);
        }
    }
#endif

    #endregion
}