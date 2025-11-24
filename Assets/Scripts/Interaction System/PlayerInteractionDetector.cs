using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Detects interactable objects near the player and manages interaction prompts
/// Integrates with the existing PlayerController system
/// </summary>
public class PlayerInteractionDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float detectionRadius = 3f;
    [SerializeField] private LayerMask interactableLayers = -1;
    [SerializeField] private float updateInterval = 0.1f; // How often to check for interactables

    [Header("Prioritization")]
    [SerializeField] private bool useLineOfSight = true;
    [SerializeField] private LayerMask obstacleLayerMask = 1; // What blocks line of sight
    [SerializeField] private bool preferClosestInteractable = true;
    [SerializeField] private bool preferFacingDirection = true;
    [SerializeField] private float facingAngleThreshold = 45f;

    [Header("Debug")]
    [SerializeField] private bool showDebugVisuals = false;
    [SerializeField] private bool enableDebugLogs = false;

    // Components
    private PlayerController playerController;
    private CameraController cameraController;

    // Current state
    [ShowInInspector, ReadOnly] private List<IInteractable> interactablesInRange = new List<IInteractable>();
    private IInteractable currentBestInteractable;
    private float lastUpdateTime;

    // Events
    public System.Action<IInteractable> OnBestInteractableChanged;
    public System.Action<IInteractable> OnInteractableEntered;
    public System.Action<IInteractable> OnInteractableExited;

    public IInteractable CurrentBestInteractable => currentBestInteractable;
    public bool HasInteractableInRange => currentBestInteractable != null;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        cameraController = GetComponent<CameraController>();
    }

    private void Update()
    {
        // Update detection at specified intervals for performance
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateInteractableDetection();
            lastUpdateTime = Time.time;
        }
    }

    private void UpdateInteractableDetection()
    {
        // Find all potential interactables in range
        var previousInteractables = new List<IInteractable>(interactablesInRange);
        FindInteractablesInRange();

        // Check for new interactables that entered range
        foreach (var interactable in interactablesInRange)
        {
            if (!previousInteractables.Contains(interactable))
            {
                OnInteractableEnteredRange(interactable);
            }
        }

        // Check for interactables that left range
        foreach (var interactable in previousInteractables)
        {
            if (!interactablesInRange.Contains(interactable))
            {
                OnInteractableExitedRange(interactable);
            }
        }

        // Determine the best interactable to show prompt for
        UpdateBestInteractable();
    }

    private void FindInteractablesInRange()
    {
        interactablesInRange.Clear();

        // Use OverlapSphere to find all potential interactables
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, interactableLayers);

        foreach (var collider in colliders)
        {
            // Try to get IInteractable from the collider or its parent
            var interactable = GetInteractableFromCollider(collider);

            if (interactable != null && interactable.CanInteract)
            {
                // Check if within the interactable's specific range
                float distance = Vector3.Distance(transform.position, interactable.Transform.position);
                if (distance <= interactable.InteractionRange)
                {
                    // Check line of sight if enabled
                    if (!useLineOfSight || HasLineOfSight(interactable))
                    {
                        interactablesInRange.Add(interactable);
                    }
                }
            }
        }

        //DebugLog($"Found {interactablesInRange.Count} interactables in range");
    }

    private IInteractable GetInteractableFromCollider(Collider collider)
    {
        // First check the collider's GameObject
        var interactable = collider.GetComponent<IInteractable>();
        if (interactable != null) return interactable;

        // Then check parent objects
        return collider.GetComponentInParent<IInteractable>();
    }

    private bool HasLineOfSight(IInteractable interactable)
    {
        Vector3 origin = cameraController.CameraRoot.position;
        Vector3 target = interactable.Transform.position;
        Vector3 direction = target - origin;

        if (Physics.Raycast(cameraController.CameraForward, direction, out RaycastHit hit, direction.magnitude, obstacleLayerMask))
        {
            // Check if the hit object is the interactable itself or part of it
            var hitInteractable = GetInteractableFromCollider(hit.collider);
            return hitInteractable == interactable;
        }

        return true; // No obstacle in the way
    }

    private void UpdateBestInteractable()
    {
        IInteractable newBest = null;

        if (interactablesInRange.Count > 0)
        {
            if (preferClosestInteractable || preferFacingDirection)
            {
                newBest = GetPrioritizedInteractable();
            }
            else
            {
                newBest = interactablesInRange[0];
            }
        }

        // Update current best if it changed
        if (newBest != currentBestInteractable)
        {
            var previousBest = currentBestInteractable;
            currentBestInteractable = newBest;

            //DebugLog($"Best interactable changed: {previousBest?.InteractableID} -> {newBest?.InteractableID}");
            OnBestInteractableChanged?.Invoke(currentBestInteractable);
        }
    }

    private IInteractable GetPrioritizedInteractable()
    {
        if (interactablesInRange.Count == 1)
            return interactablesInRange[0];

        // Score each interactable based on distance and facing direction
        var scoredInteractables = interactablesInRange.Select(interactable =>
        {
            float score = 0f;

            // Distance score (closer is better)
            if (preferClosestInteractable)
            {
                float distance = Vector3.Distance(transform.position, interactable.Transform.position);
                float normalizedDistance = distance / detectionRadius;
                score += (1f - normalizedDistance) * 50f; // Weight distance heavily
            }

            // Facing direction score (in front of player is better)
            if (preferFacingDirection && cameraController != null)
            {
                Vector3 toInteractable = (interactable.Transform.position - transform.position).normalized;
                Vector3 playerForward = cameraController.Forward;
                float angle = Vector3.Angle(playerForward, toInteractable);

                if (angle <= facingAngleThreshold)
                {
                    float normalizedAngle = angle / facingAngleThreshold;
                    score += (1f - normalizedAngle) * 30f; // Weight facing direction
                }
            }

            return new { Interactable = interactable, Score = score };
        }).ToList();

        // Return the highest scored interactable
        return scoredInteractables.OrderByDescending(x => x.Score).First().Interactable;
    }

    private void OnInteractableEnteredRange(IInteractable interactable)
    {
        //DebugLog($"Interactable entered range: {interactable.InteractableID}");
        interactable.OnPlayerEnterRange(gameObject);
        OnInteractableEntered?.Invoke(interactable);
    }

    private void OnInteractableExitedRange(IInteractable interactable)
    {
        //DebugLog($"Interactable exited range: {interactable.InteractableID}");
        interactable.OnPlayerExitRange(gameObject);
        OnInteractableExited?.Invoke(interactable);

        // Clear current best if it's the one that left
        if (currentBestInteractable == interactable)
        {
            currentBestInteractable = null;
            OnBestInteractableChanged?.Invoke(null);
        }
    }

    /// <summary>
    /// Attempt to interact with the current best interactable
    /// Called by the input system
    /// </summary>
    public bool TryInteract()
    {
        if (currentBestInteractable == null)
        {
            DebugLog("No interactable to interact with");
            return false;
        }

        DebugLog($"Attempting interaction with: {currentBestInteractable.InteractableID}");
        return currentBestInteractable.Interact(gameObject);
    }

    /// <summary>
    /// Get the interaction prompt for the current best interactable
    /// </summary>
    public string GetCurrentInteractionPrompt()
    {
        return currentBestInteractable?.GetInteractionPrompt() ?? "";
    }

    /// <summary>
    /// Force a refresh of the detection system
    /// </summary>
    public void ForceUpdate()
    {
        UpdateInteractableDetection();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[InteractionDetector] {message}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugVisuals) return;

        // Draw detection radius
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Draw lines to all interactables in range
        Gizmos.color = Color.green;
        foreach (var interactable in interactablesInRange)
        {
            if (interactable != null)
            {
                Gizmos.DrawLine(transform.position, interactable.Transform.position);
            }
        }

        // Highlight current best interactable
        if (currentBestInteractable != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentBestInteractable.Transform.position, 0.5f);
        }

        // Draw facing direction if relevant
        if (preferFacingDirection && cameraController != null)
        {
            Gizmos.color = Color.red;
            Vector3 facingDirection = cameraController.Forward * detectionRadius;
            Gizmos.DrawRay(transform.position, facingDirection);
        }
    }
}