using UnityEngine;
using Sirenix.OdinInspector;
using UnityEditor.EditorTools;
using UnityEngine.Rendering;

/// <summary>
/// Handles player movement during ledge climbing using animation events.
/// Instead of complex phase detection, this controller provides simple methods that are called
/// by animation events at the precise moments when forces should be applied.
/// Uses distance-based force calculations for consistent feel regardless of animation timing.
/// Enhanced with dynamic force multipliers based on climb distance and height.
/// NEW: Added hand IK positioning for realistic ledge gripping during climb animations.
/// </summary>
public class ClimbingMovementController : MonoBehaviour, IMovementController
{
    [Header("Base Force Settings")]
    [SerializeField, Tooltip("Base upward force for climbing")]
    private float baseUpwardForce = 1100;

    [SerializeField, Tooltip("Base forward force for climbing")]
    private float baseForwardForce = 400;

    [Tooltip("Multiplier for upward force when climbing out of water instead of ground")]
    [SerializeField] private float climbFromWaterMultiplier = 1.4f;

    [Tooltip("Multiplier for jump forces for when the player jumps to grab the ledge before climbing it")]
    [SerializeField] private float preClimbJumpForceMultiplier = 0.3f;

    [Header("Dynamic Force Multipliers")]
    [SerializeField, Tooltip("Multiplier curve for upward force based on vertical distance")]
    private AnimationCurve upwardForceMultiplierCurve = AnimationCurve.Linear(0.5f, 0.8f, 3.0f, 1.5f);

    [SerializeField, Tooltip("Multiplier curve for forward force based on horizontal distance")]
    private AnimationCurve forwardForceMultiplierCurve = AnimationCurve.Linear(0.3f, 0.7f, 2.5f, 1.3f);

    [SerializeField, Tooltip("Reference distances for force scaling")]
    [FoldoutGroup("Force Scaling")]
    private float referenceVerticalDistance = 2.0f; // Standard climb height

    [SerializeField, Tooltip("Reference horizontal distance for forward force scaling")]
    [FoldoutGroup("Force Scaling")]
    private float referenceHorizontalDistance = 1.5f; // Standard climb distance

    [SerializeField, Tooltip("Minimum multiplier to prevent forces from becoming too weak")]
    [FoldoutGroup("Force Scaling")]
    private float minimumForceMultiplier = 0.5f;

    [SerializeField, Tooltip("Maximum multiplier to prevent forces from becoming too strong")]
    [FoldoutGroup("Force Scaling")]
    private float maximumForceMultiplier = 2.0f;

    [SerializeField, Tooltip("Drag applied during climb for control")]
    private float climbDrag = 8f;

    [Header("Safety Settings")]
    [SerializeField, Tooltip("Maximum climb height to prevent infinite climbing")]
    private float maxClimbHeight = 4f;

    [SerializeField, Tooltip("Minimum distance player must be from ledge to climb")]
    private float minDistanceToLedge = 0.5f;

    [SerializeField, Tooltip("Maximum distance player can be from ledge to climb")]
    private float maxDistanceToLedge = 2f;

    [Header("Hand IK Settings")]
    [SerializeField, Tooltip("How wide apart the hands should be placed on the ledge")]
    [FoldoutGroup("Hand IK")]
    private float handSpacing = 0.6f;

    [SerializeField, Tooltip("How far forward from the ledge edge to place hands")]
    [FoldoutGroup("Hand IK")]
    private float handForwardOffset = 0.1f;

    [SerializeField, Tooltip("How far down from the ledge surface to place hands")]
    [FoldoutGroup("Hand IK")]
    private float handVerticalOffset = -0.1f;

    [SerializeField, Tooltip("Left hand IK target transform (created at runtime if null)")]
    [FoldoutGroup("Hand IK")]
    private Transform leftHandIKTarget;

    [SerializeField, Tooltip("Right hand IK target transform (created at runtime if null)")]
    [FoldoutGroup("Hand IK")]
    private Transform rightHandIKTarget;

    [SerializeField, Tooltip("Parent transform for IK targets (optional)")]
    [FoldoutGroup("Hand IK")]
    private Transform ikTargetParent;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showForceVectors = true;
    [SerializeField] private bool showForceCalculations = false;
    [SerializeField] private bool showHandIKDebug = true;

    // Interface Implementation
    public MovementMode MovementMode => MovementMode.Climbing;
    public bool IsGrounded => false; // Not grounded during climb
    public bool IsMoving => isClimbing;
    public bool IsSpeedModified => false; // No speed modification in climbing
    public bool IsSecondaryActive => false; // No secondary actions in climbing

    // Component references
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PlayerLedgeDetector ledgeDetector;

    // Climbing state
    private bool isClimbing = false;
    private bool climbCompleted = false;
    private PlayerLedgeDetector.LedgeInfo currentLedge;

    // Target positions for force calculations
    private Vector3 climbStartPosition;
    private Vector3 climbTargetPosition;

    // Force calculation data
    private float calculatedUpwardMultiplier = 1.0f;
    private float calculatedForwardMultiplier = 1.0f;
    private float verticalDistance = 0f;
    private float horizontalDistance = 0f;

    // Force tracking for debug
    private Vector3 lastAppliedForce;
    private Vector3 debugCurrentForce;

    // Hand IK state
    private bool handIKActive = false;
    private Vector3 calculatedLeftHandPosition;
    private Vector3 calculatedRightHandPosition;

    // Events for coordination with animation system
    public System.Action OnClimbStarted;
    public System.Action OnClimbCompleted;

    // Debug tracking
    private Vector3 debugStartPos;
    private Vector3 debugTargetPos;

    public void Initialize(PlayerController controller)
    {
        playerController = controller;
        rb = controller.rb;

        // Find ledge detector
        if (ledgeDetector == null)
            ledgeDetector = playerController.ledgeDetector;

        if (ledgeDetector == null)
        {
            Debug.LogError("[ClimbingMovementController] No PlayerLedgeDetector found! Climbing will not work.");
        }

        // Setup Hand IK targets
        SetupHandIKTargets();

        ValidateSettings();
        SetupDefaultCurves();
        DebugLog("Enhanced ClimbingMovementController initialized with Hand IK support");
    }

    /// <summary>
    /// Setup hand IK target transforms if they don't exist
    /// </summary>
    private void SetupHandIKTargets()
    {
        // Create parent for IK targets if not assigned
        if (ikTargetParent == null)
        {
            GameObject ikParent = new GameObject("Climbing_IK_Targets");
            ikTargetParent = ikParent.transform;
            ikTargetParent.SetParent(transform);
        }

        // Create left hand target if not assigned
        if (leftHandIKTarget == null)
        {
            GameObject leftHand = new GameObject("LeftHand_IK_Target");
            leftHandIKTarget = leftHand.transform;
            leftHandIKTarget.SetParent(ikTargetParent);
        }

        // Create right hand target if not assigned
        if (rightHandIKTarget == null)
        {
            GameObject rightHand = new GameObject("RightHand_IK_Target");
            rightHandIKTarget = rightHand.transform;
            rightHandIKTarget.SetParent(ikTargetParent);
        }

        DebugLog($"Hand IK targets setup - Left: {leftHandIKTarget.name}, Right: {rightHandIKTarget.name}");
    }

    private void ValidateSettings()
    {
        if (baseUpwardForce <= 0) baseUpwardForce = 900;
        if (baseForwardForce <= 0) baseForwardForce = 400;
        if (referenceVerticalDistance <= 0) referenceVerticalDistance = 2.0f;
        if (referenceHorizontalDistance <= 0) referenceHorizontalDistance = 1.5f;

        minimumForceMultiplier = Mathf.Max(0.1f, minimumForceMultiplier);
        maximumForceMultiplier = Mathf.Max(minimumForceMultiplier, maximumForceMultiplier);

        // Validate Hand IK settings
        handSpacing = Mathf.Max(0.2f, handSpacing);
        handForwardOffset = Mathf.Clamp(handForwardOffset, -0.2f, 0.5f);
        handVerticalOffset = Mathf.Clamp(handVerticalOffset, -0.2f, 0.2f);
    }

    private void SetupDefaultCurves()
    {
        // Set up default curves if they're not configured
        if (upwardForceMultiplierCurve == null || upwardForceMultiplierCurve.keys.Length == 0)
        {
            upwardForceMultiplierCurve = AnimationCurve.Linear(0.5f, 0.8f, 3.0f, 1.5f);
        }

        if (forwardForceMultiplierCurve == null || forwardForceMultiplierCurve.keys.Length == 0)
        {
            forwardForceMultiplierCurve = AnimationCurve.Linear(0.3f, 0.7f, 2.5f, 1.3f);
        }
    }

    public void OnControllerActivated()
    {
        DebugLog("Enhanced climbing controller activated");

        // Reset climb state
        isClimbing = false;
        climbCompleted = false;
        handIKActive = false;

        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    public void OnControllerDeactivated()
    {
        DebugLog("Enhanced climbing controller deactivated");

        // Stop any ongoing climb and clear IK
        StopClimbing();
        DisableHandIK();
    }

    /// <summary>
    /// Start the animation-driven climbing sequence using the currently detected ledge
    /// </summary>
    public bool StartClimbing()
    {
        if (isClimbing)
        {
            DebugLog("Already climbing");
            return false;
        }

        if (ledgeDetector == null || !ledgeDetector.HasValidLedge)
        {
            DebugLog("No valid ledge to climb");
            return false;
        }

        currentLedge = ledgeDetector.CurrentLedge;

        if (!ValidateClimbConditions())
        {
            DebugLog("Climb conditions not met");
            return false;
        }

        if (!CalculateClimbParameters())
        {
            DebugLog("Failed to calculate climb parameters");
            return false;
        }

        // Calculate force multipliers based on climb distance
        CalculateForceMultipliers();

        // Calculate hand IK positions
        CalculateHandIKPositions();

        // Start the climb
        BeginClimb();
        return true;
    }

    /// <summary>
    /// Validate that climbing is safe and possible
    /// </summary>
    private bool ValidateClimbConditions()
    {
        if (!currentLedge.IsValid)
            return false;

        Vector3 playerPos = transform.position;
        Vector3 ledgePos = currentLedge.LedgePosition;

        // Check height difference
        float heightDifference = ledgePos.y - playerPos.y;
        if (heightDifference <= 0f || heightDifference > maxClimbHeight)
        {
            DebugLog($"Invalid climb height: {heightDifference:F2}m (max: {maxClimbHeight}m)");
            return false;
        }

        // Check horizontal distance
        Vector3 horizontalOffset = ledgePos - playerPos;
        horizontalOffset.y = 0f;
        float horizontalDistanceCheck = horizontalOffset.magnitude;

        if (horizontalDistanceCheck < minDistanceToLedge || horizontalDistanceCheck > maxDistanceToLedge)
        {
            DebugLog($"Invalid distance to ledge: {horizontalDistanceCheck:F2}m (range: {minDistanceToLedge}-{maxDistanceToLedge}m)");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Calculate climb parameters for force application
    /// </summary>
    private bool CalculateClimbParameters()
    {
        if (!currentLedge.IsValid)
            return false;

        Vector3 playerPos = transform.position;
        Vector3 ledgePos = currentLedge.LedgePosition;
        Vector3 wallNormal = currentLedge.WallNormal;

        climbStartPosition = playerPos;

        // Calculate target position slightly forward on the ledge
        Vector3 forwardOffset = -wallNormal * 0.4f; // Negative to go onto the ledge
        climbTargetPosition = ledgePos + forwardOffset + Vector3.up * 0.1f;

        // Debug storage
        debugStartPos = climbStartPosition;
        debugTargetPos = climbTargetPosition;

        bool validParameters =
            climbTargetPosition.y > climbStartPosition.y &&
            Vector3.Distance(climbStartPosition, climbTargetPosition) > 0.5f;

        if (validParameters)
        {
            DebugLog($"Climb parameters calculated - Start: {climbStartPosition}, Target: {climbTargetPosition}");
        }

        return validParameters;
    }

    /// <summary>
    /// Calculate dynamic force multipliers based on climb distance and height
    /// </summary>
    private void CalculateForceMultipliers()
    {
        // Calculate distances
        Vector3 climbVector = climbTargetPosition - climbStartPosition;
        verticalDistance = climbVector.y;

        // Calculate horizontal distance (remove y component)
        Vector3 horizontalVector = climbVector;
        horizontalVector.y = 0f;
        horizontalDistance = horizontalVector.magnitude;

        // Calculate upward force multiplier based on vertical distance
        float normalizedVerticalDistance = verticalDistance / referenceVerticalDistance;
        calculatedUpwardMultiplier = upwardForceMultiplierCurve.Evaluate(normalizedVerticalDistance);
        calculatedUpwardMultiplier = Mathf.Clamp(calculatedUpwardMultiplier, minimumForceMultiplier, maximumForceMultiplier);

        // Calculate forward force multiplier based on horizontal distance
        float normalizedHorizontalDistance = horizontalDistance / referenceHorizontalDistance;
        calculatedForwardMultiplier = forwardForceMultiplierCurve.Evaluate(normalizedHorizontalDistance);
        calculatedForwardMultiplier = Mathf.Clamp(calculatedForwardMultiplier, minimumForceMultiplier, maximumForceMultiplier);

        if (showForceCalculations && enableDebugLogs)
        {
            DebugLog($"Force Multipliers Calculated:");
            DebugLog($"  Vertical Distance: {verticalDistance:F2}m (normalized: {normalizedVerticalDistance:F2})");
            DebugLog($"  Horizontal Distance: {horizontalDistance:F2}m (normalized: {normalizedHorizontalDistance:F2})");
            DebugLog($"  Upward Multiplier: {calculatedUpwardMultiplier:F2}");
            DebugLog($"  Forward Multiplier: {calculatedForwardMultiplier:F2}");
        }
    }

    /// <summary>
    /// Calculate where the player's hands should be positioned on the ledge
    /// </summary>
    private void CalculateHandIKPositions()
    {
        if (!currentLedge.IsValid)
        {
            DebugLog("Cannot calculate hand IK positions - invalid ledge");
            return;
        }

        Vector3 ledgePos = currentLedge.LedgePosition;
        Vector3 wallNormal = currentLedge.WallNormal;

        // Calculate the right direction along the ledge (perpendicular to wall normal)
        Vector3 ledgeRight = Vector3.Cross(Vector3.up, wallNormal).normalized;

        // Calculate base hand position (center of where hands should go)
        Vector3 baseHandPosition = ledgePos
            + (-wallNormal * handForwardOffset)  // Move slightly onto the ledge surface
            + (Vector3.down * handVerticalOffset); // Move slightly below the ledge surface

        // Calculate individual hand positions
        Vector3 halfSpacingOffset = ledgeRight * (handSpacing * 0.5f);

        calculatedLeftHandPosition = baseHandPosition + halfSpacingOffset;
        calculatedRightHandPosition = baseHandPosition - halfSpacingOffset;

        if (showHandIKDebug && enableDebugLogs)
        {
            DebugLog($"Hand IK Positions Calculated:");
            DebugLog($"  Ledge Position: {ledgePos}");
            DebugLog($"  Wall Normal: {wallNormal}");
            DebugLog($"  Ledge Right: {ledgeRight}");
            DebugLog($"  Left Hand: {calculatedLeftHandPosition}");
            DebugLog($"  Right Hand: {calculatedRightHandPosition}");
            DebugLog($"  Hand Spacing: {handSpacing}m, Forward Offset: {handForwardOffset}m, Vertical Offset: {handVerticalOffset}m");
        }
    }

    /// <summary>
    /// Begin the animation-driven climbing sequence
    /// </summary>
    private void BeginClimb()
    {
        isClimbing = true;
        climbCompleted = false;

        // Setup physics for climbing
        SetupClimbPhysics();

        OnClimbStarted?.Invoke();
        DebugLog($"Started enhanced climbing - Vertical: {verticalDistance:F2}m (x{calculatedUpwardMultiplier:F2}), " +
                $"Horizontal: {horizontalDistance:F2}m (x{calculatedForwardMultiplier:F2})");
    }

    /// <summary>
    /// Setup physics for climbing
    /// </summary>
    private void SetupClimbPhysics()
    {
        if (rb == null) return;

        // Apply climb physics settings
        rb.linearDamping = climbDrag;
        rb.angularDamping = climbDrag * 2f;

        // Keep gravity enabled but we'll counter it with forces
        rb.useGravity = true;

        DebugLog("Climb physics applied");
    }

    /// <summary>
    /// Stop climbing sequence
    /// </summary>
    private void StopClimbing()
    {
        if (!isClimbing) return;

        isClimbing = false;
        lastAppliedForce = Vector3.zero;
        debugCurrentForce = Vector3.zero;

        DebugLog("Climbing stopped");
    }


    private void FixedUpdate()
    {
        // Prevent player from falling whilst climbing (ie they won't slide down whilst grabbing the ledge)
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

    }

    #region Animation Event Methods

    /// <summary>
    /// PUBLIC: Called by animation event to apply upward climbing force
    /// Use this before the "pull up" phase of the climb animation, when the player jumps to grab the ledge
    /// Now uses dynamic force multiplier based on vertical climb distance
    /// </summary>
    public void ApplyPreClimbJumpForce()
    {
        if (!isClimbing || rb == null)
        {
            DebugLog("ApplyUpwardsClimbForce called but not climbing or no rigidbody");
            return;
        }

        // Calculate upward force with dynamic multiplier
        float dynamicUpwardForce = baseUpwardForce * calculatedUpwardMultiplier * preClimbJumpForceMultiplier;
        float dynamicForwardForce = baseForwardForce * calculatedForwardMultiplier * preClimbJumpForceMultiplier;
        Vector3 preClimbJumpForce = new Vector3(0f, dynamicUpwardForce, dynamicForwardForce);

        ApplyForce(preClimbJumpForce, "Pre Jump Upward");


    }

    /// <summary>
    /// PUBLIC: Called by animation event to apply upward climbing force
    /// Use this during the "pull up" phase of the climb animation
    /// Now uses dynamic force multiplier based on vertical climb distance
    /// </summary>
    public void ApplyUpwardsClimbForce()
    {
        if (!isClimbing || rb == null)
        {
            DebugLog("ApplyUpwardsClimbForce called but not climbing or no rigidbody");
            return;
        }

        // Calculate upward force with dynamic multiplier
        float dynamicUpwardForce = baseUpwardForce * calculatedUpwardMultiplier;
        Vector3 upwardForce = Vector3.up * dynamicUpwardForce;

        ApplyForce(upwardForce, "Dynamic Upward");

        DebugLog($"Applied dynamic upward climb force - Base: {baseUpwardForce}N, " +
                $"Multiplier: {calculatedUpwardMultiplier:F2}, Final: {dynamicUpwardForce:F0}N");
    }

    /// <summary>
    /// PUBLIC: Called by animation event to apply forward climbing force
    /// Use this during the "push forward onto ledge" phase of the climb animation
    /// Now uses dynamic force multiplier based on horizontal climb distance
    /// </summary>
    public void ApplyForwardsClimbForce()
    {
        if (!isClimbing || rb == null)
        {
            DebugLog("ApplyForwardsClimbForce called but not climbing or no rigidbody");
            return;
        }

        // Calculate forward force with dynamic multiplier
        float dynamicForwardForce = baseForwardForce * calculatedForwardMultiplier;
        Vector3 forwardForce = transform.forward * dynamicForwardForce;

        ApplyForce(forwardForce, "Dynamic Forward");

        DebugLog($"Applied dynamic forward climb force - Base: {baseForwardForce}N, " +
                $"Multiplier: {calculatedForwardMultiplier:F2}, Final: {dynamicForwardForce:F0}N");
    }

    /// <summary>
    /// PUBLIC: Called by animation event to complete the climbing sequence
    /// Use this at the very end of the climb animation
    /// </summary>
    public void CompleteClimb()
    {
        if (!isClimbing)
        {
            DebugLog("CompleteClimb called but not climbing");
            return;
        }

        isClimbing = false;
        climbCompleted = true;
        lastAppliedForce = Vector3.zero;
        debugCurrentForce = Vector3.zero;

        OnClimbCompleted?.Invoke();
        DebugLog("Enhanced climb completed");
    }

    #endregion

    #region Hand IK Animation Event Methods

    /// <summary>
    /// PUBLIC: Called by animation event to enable hand IK for ledge gripping
    /// Use this when the player's hands should start gripping the ledge
    /// </summary>
    public void EnableHandIK()
    {
        if (!isClimbing)
        {
            DebugLog("EnableHandIK called but not climbing");
            return;
        }

        if (playerController == null)
        {
            DebugLog("EnableHandIK failed - no PlayerController reference");
            return;
        }

        if (leftHandIKTarget == null || rightHandIKTarget == null)
        {
            DebugLog("EnableHandIK failed - hand IK targets not setup");
            return;
        }

        // Position the IK target transforms at the calculated positions
        leftHandIKTarget.position = calculatedLeftHandPosition;
        rightHandIKTarget.position = calculatedRightHandPosition;

        // Optional: Orient the hands to face the wall (more natural grip)
        if (currentLedge.IsValid)
        {
            Vector3 wallNormal = currentLedge.WallNormal;
            Vector3 handForward = -wallNormal; // Hands face into the wall
            Vector3 handUp = Vector3.up; // Hands point up

            // leftHandIKTarget.rotation = Quaternion.LookRotation(handForward, handUp);
            // rightHandIKTarget.rotation = Quaternion.LookRotation(handForward, handUp);
        }

        // Enable the IK effectors on the player
        playerController.SetPlayerLeftHandAndRightHandIKEffectors(
            leftHandIKTarget,
            rightHandIKTarget,
            enabled: true,
            matchRotationToo: false // As requested
        );

        handIKActive = true;

        DebugLog($"Hand IK enabled - Left: {calculatedLeftHandPosition}, Right: {calculatedRightHandPosition}");
    }

    /// <summary>
    /// PUBLIC: Called by animation event to disable hand IK
    /// Use this when the player should release their grip on the ledge
    /// </summary>
    public void DisableHandIK()
    {
        if (playerController == null)
        {
            DebugLog("DisableHandIK - no PlayerController reference");
            return;
        }

        // Disable the IK effectors
        playerController.SetPlayerLeftHandAndRightHandIKEffectors(
            null,
            null,
            enabled: false,
            matchRotationToo: false
        );

        handIKActive = false;

        DebugLog("Hand IK disabled - player hands released from ledge");
    }

    #endregion

    #region Force Calculation Helpers

    /// <summary>
    /// Apply force and update debug tracking
    /// </summary>
    private void ApplyForce(Vector3 force, string forceType)
    {
        if (rb == null) return;

        rb.AddForce(force, ForceMode.Impulse);
        lastAppliedForce = force;
        debugCurrentForce = force;

        if (showForceVectors && enableDebugLogs)
        {
            DebugLog($"{forceType} Force Applied: {force.magnitude:F1}N, Direction: {force.normalized}");
        }
    }

    /// <summary>
    /// Get the current upward force multiplier for external systems
    /// </summary>
    public float GetUpwardForceMultiplier()
    {
        return calculatedUpwardMultiplier;
    }

    /// <summary>
    /// Get the current forward force multiplier for external systems
    /// </summary>
    public float GetForwardForceMultiplier()
    {
        return calculatedForwardMultiplier;
    }

    /// <summary>
    /// Manually set force multipliers (useful for testing or special cases)
    /// </summary>
    public void SetForceMultipliers(float upwardMultiplier, float forwardMultiplier)
    {
        calculatedUpwardMultiplier = Mathf.Clamp(upwardMultiplier, minimumForceMultiplier, maximumForceMultiplier);
        calculatedForwardMultiplier = Mathf.Clamp(forwardMultiplier, minimumForceMultiplier, maximumForceMultiplier);

        DebugLog($"Force multipliers manually set - Upward: {calculatedUpwardMultiplier:F2}, Forward: {calculatedForwardMultiplier:F2}");
    }

    /// <summary>
    /// Get the calculated hand positions for debugging or external use
    /// </summary>
    public (Vector3 leftHand, Vector3 rightHand) GetCalculatedHandPositions()
    {
        return (calculatedLeftHandPosition, calculatedRightHandPosition);
    }

    /// <summary>
    /// Check if hand IK is currently active
    /// </summary>
    public bool IsHandIKActive()
    {
        return handIKActive;
    }

    /// <summary>
    /// Manually set hand spacing (useful for different ledge types)
    /// </summary>
    public void SetHandSpacing(float spacing)
    {
        handSpacing = Mathf.Max(0.2f, spacing);

        // Recalculate positions if currently climbing
        if (isClimbing && currentLedge.IsValid)
        {
            CalculateHandIKPositions();
        }

        DebugLog($"Hand spacing set to {handSpacing:F2}m");
    }

    #endregion

    /// <summary>
    /// Check if we can start climbing (called externally)
    /// </summary>
    public bool CanStartClimbing()
    {
        if (isClimbing) return false;
        if (ledgeDetector == null || !ledgeDetector.HasValidLedge) return false;

        // Quick validation check
        currentLedge = ledgeDetector.CurrentLedge;
        return ValidateClimbConditions();
    }

    #region IMovementController Implementation - Disabled During Climbing

    public void HandleMovement(Vector2 moveInput, bool isSpeedModified)
    {
        // Movement disabled during climbing
    }

    public void HandleHorizontalRotation(float targetRotationY)
    {
        // Rotation disabled during climbing
    }

    public void HandlePrimaryAction()
    {
        // Actions disabled during climbing
    }

    public void HandlePrimaryActionReleased()
    {
        // Actions disabled during climbing
    }

    public void HandleSecondaryAction()
    {
        // Actions disabled during climbing
    }

    public void HandleSecondaryActionReleased()
    {
        // Actions disabled during climbing
    }

    public Vector3 GetVelocity()
    {
        return rb != null ? rb.linearVelocity : Vector3.zero;
    }

    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        DebugLog($"Movement state changed during climb: {previousState} -> {newState}");
    }

    #endregion

    public void Cleanup()
    {
        StopClimbing();
        DisableHandIK();
        DebugLog("Enhanced ClimbingMovementController cleaned up");
    }

    // Add these methods to your ClimbingMovementController.cs class

    #region Save System Integration

    /// <summary>
    /// Gets the target position where the player should be placed when loading a save during climbing.
    /// Returns the calculated position on top of the ledge where the climb would end.
    /// </summary>
    public Vector3 GetClimbTargetPositionForSave()
    {
        if (!currentLedge.IsValid)
        {
            DebugLog("No valid ledge for save position calculation");
            return transform.position; // Fallback to current position
        }

        // Use the same calculation as the climbing system for target position
        Vector3 ledgePos = currentLedge.LedgePosition;
        Vector3 wallNormal = currentLedge.WallNormal;

        // Calculate where the player should end up after climbing
        Vector3 forwardOffset = -wallNormal; // Same as in CalculateClimbParameters
        Vector3 targetPosition = ledgePos + forwardOffset + (Vector3.up * playerController.capsuleCollider.height * 0.5f);


        DebugLog($"Calculated climb target position for save: {targetPosition}");
        return targetPosition;
    }

    /// <summary>
    /// Gets information about the current climb for save system debugging
    /// </summary>
    public string GetClimbSaveInfo()
    {
        if (!isClimbing || !currentLedge.IsValid)
        {
            return "Not climbing or no valid ledge";
        }

        Vector3 targetPos = GetClimbTargetPositionForSave();
        return $"Climbing to: {targetPos}, Ledge: {currentLedge.LedgePosition}, " +
               $"Current: {transform.position}, Progress: {(isClimbing ? "Active" : "Inactive")}";
    }

    /// <summary>
    /// Checks if the climbing controller has a valid target position for saving
    /// </summary>
    public bool HasValidClimbTarget()
    {
        return currentLedge.IsValid;
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ClimbingMovementController] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        if (Application.isPlaying && (isClimbing || climbCompleted))
        {
            // Draw climb path
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(debugStartPos, 0.2f);
            Gizmos.DrawLine(debugStartPos, debugTargetPos);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(debugTargetPos, 0.2f);

            // Draw current position during climb
            if (isClimbing)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.15f);

                // Draw distance indicators
                Vector3 playerPos = transform.position;

                // Vertical distance indicator
                Gizmos.color = Color.red;
                Gizmos.DrawLine(new Vector3(playerPos.x - 0.2f, debugStartPos.y, playerPos.z),
                               new Vector3(playerPos.x - 0.2f, debugTargetPos.y, playerPos.z));

                // Horizontal distance indicator
                Gizmos.color = Color.blue;
                Vector3 horizontalStart = debugStartPos;
                horizontalStart.y = debugTargetPos.y;
                Gizmos.DrawLine(horizontalStart, debugTargetPos);

                // Draw hand IK positions if active
                if (showHandIKDebug)
                {
                    // Draw calculated hand positions
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(calculatedLeftHandPosition, 0.08f);
                    Gizmos.DrawWireSphere(calculatedRightHandPosition, 0.08f);

                    // Draw line between hands to show spacing
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(calculatedLeftHandPosition, calculatedRightHandPosition);

                    // Draw connection from hands to ledge
                    if (currentLedge.IsValid)
                    {
                        Gizmos.color = Color.white;
                        Gizmos.DrawLine(calculatedLeftHandPosition, currentLedge.LedgePosition);
                        Gizmos.DrawLine(calculatedRightHandPosition, currentLedge.LedgePosition);
                    }

                    // Draw actual IK target positions if they exist
                    if (leftHandIKTarget != null && rightHandIKTarget != null && handIKActive)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireSphere(leftHandIKTarget.position, 0.06f);
                        Gizmos.DrawWireSphere(rightHandIKTarget.position, 0.06f);

                        // Draw arrows to show hand orientation
                        Gizmos.DrawRay(leftHandIKTarget.position, leftHandIKTarget.forward * 0.1f);
                        Gizmos.DrawRay(rightHandIKTarget.position, rightHandIKTarget.forward * 0.1f);
                    }
                }
            }

            // Draw force vectors
            if (showForceVectors && isClimbing && debugCurrentForce.magnitude > 0.1f)
            {
                Gizmos.color = Color.cyan;
                Vector3 forceVisualization = debugCurrentForce.normalized * Mathf.Min(debugCurrentForce.magnitude / 100f, 3f);
                Gizmos.DrawRay(transform.position + Vector3.up * 1f, forceVisualization);
            }
        }

        // Draw ledge info if available
        if (currentLedge.IsValid)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentLedge.LedgePosition, 0.1f);
            Gizmos.DrawRay(currentLedge.LedgePosition, currentLedge.WallNormal * 0.5f);
        }

        // Draw hand IK settings preview in editor
        if (!Application.isPlaying && showHandIKDebug)
        {
            // Preview hand spacing at current position
            Vector3 previewPos = transform.position + Vector3.up * 2f;
            Vector3 rightOffset = transform.right * (handSpacing * 0.5f);

            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(previewPos - rightOffset, 0.05f);
            Gizmos.DrawWireSphere(previewPos + rightOffset, 0.05f);
            Gizmos.DrawLine(previewPos - rightOffset, previewPos + rightOffset);
        }
    }

    /// <summary>
    /// Get debug info for troubleshooting
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Enhanced Climbing Movement Controller ===");
        info.AppendLine($"Is Climbing: {isClimbing}");
        info.AppendLine($"Climb Completed: {climbCompleted}");
        info.AppendLine($"Can Start Climbing: {CanStartClimbing()}");
        info.AppendLine($"Has Valid Ledge: {ledgeDetector?.HasValidLedge ?? false}");
        info.AppendLine($"Hand IK Active: {handIKActive}");

        if (isClimbing)
        {
            info.AppendLine($"Current Velocity: {rb?.linearVelocity.magnitude ?? 0:F2}m/s");
            info.AppendLine($"Last Force Applied: {lastAppliedForce.magnitude:F1}N");
            info.AppendLine($"Vertical Distance: {verticalDistance:F2}m");
            info.AppendLine($"Horizontal Distance: {horizontalDistance:F2}m");
            info.AppendLine($"Upward Multiplier: {calculatedUpwardMultiplier:F2}");
            info.AppendLine($"Forward Multiplier: {calculatedForwardMultiplier:F2}");

            if (handIKActive)
            {
                info.AppendLine($"Left Hand IK: {calculatedLeftHandPosition}");
                info.AppendLine($"Right Hand IK: {calculatedRightHandPosition}");
                info.AppendLine($"Hand Spacing: {handSpacing:F2}m");
            }
        }

        if (currentLedge.IsValid)
        {
            info.AppendLine($"Current Ledge: {currentLedge}");
            info.AppendLine($"Target Position: {climbTargetPosition}");
            info.AppendLine($"Distance to Target: {Vector3.Distance(transform.position, climbTargetPosition):F2}m");
        }

        return info.ToString();
    }

    #endregion
}