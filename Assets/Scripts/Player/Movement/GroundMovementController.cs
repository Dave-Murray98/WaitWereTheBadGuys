using System;
using Sirenix.OdinInspector;
using Unity.Entities.UniversalDelegates;
using UnityEngine;

/// <summary>
/// REFACTORED: Ground movement controller using HumanoidLandController's slope handling approach.
/// Maintains jump buffering system and IMovementController interface while adopting the reference
/// script's movement vector pipeline and comprehensive slope physics.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class GroundMovementController : MonoBehaviour, IMovementController
{
    [Header("Ground Detection")]
    [SerializeField] private PlayerGroundDetector groundDetector;
    [SerializeField] private bool autoFindGroundDetector = true;

    [Header("Movement")]
    [Tooltip("How quickly the player reaches target speed")]
    public float acceleration = 50f;
    [Tooltip("How quickly the player stops when no input")]
    public float deceleration = 50f;
    [Tooltip("Multiplier for movement in air")]
    public float notGroundedMovementMultiplier = 1.25f;

    [Header("Slope Handling")]
    [Tooltip("Maximum angle (in degrees) that the player can walk up")]
    [SerializeField] private float maxSlopeAngle = 45f;
    [Tooltip("Small downward force to maintain ground contact on flat/walkable surfaces")]
    [SerializeField] private float groundedGravity = -1f;
    [Tooltip("How far ahead to check for steep slopes")]
    [SerializeField] private float slopeCheckDistance = 0.75f;
    [Tooltip("Force applied when sliding down too-steep slopes")]
    [SerializeField] private float slopeSlidingForce = 100f;

    [Header("Jump Buffering")]
    [Tooltip("How long before landing can the player press jump and have it execute on landing")]
    [SerializeField] private float jumpBufferTime = 0.15f;
    [Tooltip("How long after leaving ground can the player still jump (grace period)")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("Enable coyote time feature")]
    [SerializeField] private bool useCoyoteTime = true;

    [Header("Debug")]
    public bool showMovementDebug = false;
    [SerializeField] private bool enableDebugLogs = false;

    // Interface properties
    public MovementMode MovementMode => MovementMode.Ground;
    public bool IsGrounded => groundDetector != null ? groundDetector.IsGrounded : false;
    public bool IsMoving { get; private set; }
    public bool IsSpeedModified { get; private set; }
    public bool IsSecondaryActive { get; private set; }

    // Component references
    private PlayerController playerController;
    private PlayerData playerData;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;

    // Movement state
    private Vector2 movementInput;

    // Jump buffering state
    private bool jumpInputBuffered = false;
    private float jumpBufferTimer = 0f;
    private float coyoteTimer = 0f;
    private bool wasGroundedLastFrame = false;

    // Ground state
    private Vector3 groundNormal = Vector3.up;
    private GroundType currentGroundType = GroundType.Default;
    private Vector3 playerCenterPoint = Vector3.zero;

    // Crouch system - physics setup only
    private float originalHeight;

    [Tooltip("Height multiplier when crouching")]
    [SerializeField]
    private float crouchHeight = 1.5f;
    private Vector3 originalCenter;
    private Vector3 crouchCenter;
    private bool physicalCrouchApplied = false;

    // Ground state tracking
    private bool wasGrounded = false;

    // Properties for external access
    public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
    public GroundType CurrentGroundType => currentGroundType;

    public bool IsCrouching => playerController != null && playerController.IsCrouching;
    public bool IsSprinting => IsSpeedModified && IsMoving && !IsCrouching;

    public void Initialize(PlayerController controller)
    {
        playerController = controller;
        playerData = GameManager.Instance?.playerData;

        SetUpComponents();
        SetupGroundDetector();
        SetupRigidbody();
        SetupCrouchSystem();

        if (playerController != null)
        {
            playerController.OnStartedCrouching += OnPlayerStartedCrouching;
            playerController.OnStoppedCrouching += OnPlayerStoppedCrouching;
        }

        DebugLog("GroundMovementController initialized with HumanoidLandController movement system");
    }

    private void SetUpComponents()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
    }

    private void SetupGroundDetector()
    {
        if (autoFindGroundDetector && groundDetector == null)
        {
            groundDetector = GetComponentInChildren<PlayerGroundDetector>();

            if (groundDetector == null)
            {
                groundDetector = GetComponentInParent<PlayerGroundDetector>();
            }

            if (groundDetector != null)
            {
                DebugLog($"Auto-found ground detector: {groundDetector.name}");
            }
            else
            {
                Debug.LogError("[GroundMovementController] No PlayerGroundDetector found! Please assign one or create a ground detector.");
                return;
            }
        }

        if (groundDetector != null)
        {
            groundDetector.OnGrounded += OnGrounded;
            groundDetector.OnLeftGround += OnLeftGround;
            groundDetector.OnGroundHitChanged += OnGroundHitChanged;

            DebugLog("Subscribed to ground detector events");
        }
    }

    public Vector3 GetVelocity() => Velocity;

    public void HandleMovement(Vector2 moveInput, bool isSpeedModified)
    {
        movementInput = moveInput;

        IsMoving = moveInput.magnitude > 0.1f;
        IsSpeedModified = isSpeedModified && IsMoving && !IsCrouching;

        // if (enableDebugLogs)
        // {
        //     Debug.Log($"HandleMovement - Input: {moveInput.magnitude:F2}, SpeedMod Input: {isSpeedModified}, " +
        //              $"IsMoving: {IsMoving}, IsGrounded: {IsGrounded}, IsCrouching: {IsCrouching}, " +
        //              $"Final IsSpeedModified: {IsSpeedModified}");
        // }
    }

    public void HandleHorizontalRotation(float currentYRotation)
    {
        transform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
    }

    public void HandlePrimaryAction()
    {
        // Check if we can jump immediately (grounded or within coyote time)
        bool canJumpNow = IsGrounded || (useCoyoteTime && coyoteTimer > 0f);

        if (canJumpNow && !IsCrouching)
        {
            // SLOPE CHECK: Prevent jumping on too-steep slopes
            if (IsOnTooSteepSlope())
            {
                DebugLog($"Jump blocked - on too steep slope ({groundDetector.currentSlopeAngle:F1}°)");
                return;
            }

            Jump();
        }
        else if (!IsGrounded && !IsCrouching)
        {
            // Player is airborne and pressed jump - buffer the input
            BufferJumpInput();
        }
        else
        {
            DebugLog($"Jump blocked - IsGrounded: {IsGrounded}, IsCrouching: {IsCrouching}, CoyoteTime: {coyoteTimer:F2}");
        }
    }

    public void HandlePrimaryActionReleased()
    {
        // Ground movement doesn't need primary action release handling
    }

    private void BufferJumpInput()
    {
        jumpInputBuffered = true;
        jumpBufferTimer = jumpBufferTime;
        DebugLog($"Jump input buffered (will be valid for {jumpBufferTime:F2}s)");
    }

    private void ClearJumpBuffer()
    {
        jumpInputBuffered = false;
        jumpBufferTimer = 0f;
    }

    public void HandleSecondaryAction()
    {
        DebugLog("HandleCrouch called - crouch logic is now handled by PlayerController");
    }

    public void HandleSecondaryActionReleased()
    {
        // For unified crouch system, release handling is done by PlayerController
    }

    #region Unified Crouch System Integration

    public void StartCrouch()
    {
        if (physicalCrouchApplied || capsuleCollider == null)
        {
            DebugLog("StartCrouch called but already crouching or no collider");
            return;
        }

        physicalCrouchApplied = true;
        IsSecondaryActive = true;
        capsuleCollider.height = crouchHeight;
        capsuleCollider.center = crouchCenter;

        DebugLog("Applied physical crouch state");
    }

    public void StopCrouch()
    {
        if (!physicalCrouchApplied || capsuleCollider == null)
        {
            DebugLog("StopCrouch called but not crouching or no collider");
            return;
        }

        if (CanStandUp())
        {
            physicalCrouchApplied = false;
            IsSecondaryActive = false;
            capsuleCollider.height = originalHeight;
            capsuleCollider.center = originalCenter;

            DebugLog("Applied physical stand-up state");
        }
        else
        {
            DebugLog("Cannot stand up - blocked by ceiling. Keeping crouch state.");
        }
    }

    public bool CanStandUp()
    {
        if (capsuleCollider == null) return false;

        Vector3 checkPosition = transform.position + Vector3.up * (originalHeight - crouchHeight);
        bool canStand = !Physics.CheckSphere(checkPosition, capsuleCollider.radius * 0.9f, LayerMask.GetMask("Default"));
        return canStand;
    }

    #endregion

    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        DebugLog($"Ground movement state changed: {previousState} -> {newState}");
    }

    public void OnControllerActivated()
    {
        DebugLog("Ground movement controller activated");

        if (rb != null)
        {
            rb.useGravity = true;
            rb.freezeRotation = true;
            DebugLog($"Physics confirmed - Gravity: {rb.useGravity}, FreezeRotation: {rb.freezeRotation}");
        }

        SyncCrouchState();
    }

    public void OnControllerDeactivated()
    {
        DebugLog("Ground movement controller deactivated");

        // Clear jump buffer when deactivating
        ClearJumpBuffer();
        coyoteTimer = 0f;
    }

    public void Cleanup()
    {
        if (playerController != null)
        {
            playerController.OnStartedCrouching -= OnPlayerStartedCrouching;
            playerController.OnStoppedCrouching -= OnPlayerStoppedCrouching;
        }

        if (groundDetector != null)
        {
            groundDetector.OnGrounded -= OnGrounded;
            groundDetector.OnLeftGround -= OnLeftGround;
            groundDetector.OnGroundHitChanged -= OnGroundHitChanged;
        }

        DebugLog("GroundMovementController cleaned up");
    }

    private void SyncCrouchState()
    {
        if (playerController == null) return;

        bool shouldBeCrouching = playerController.IsCrouching;

        if (shouldBeCrouching && !physicalCrouchApplied)
        {
            StartCrouch();
        }
        else if (!shouldBeCrouching && physicalCrouchApplied)
        {
            StopCrouch();
        }

        DebugLog($"Synced crouch state - Logical: {shouldBeCrouching}, Physical: {physicalCrouchApplied}");
    }

    private void SetupRigidbody()
    {
        if (rb == null) return;

        rb.freezeRotation = true;
        rb.useGravity = true;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
    }

    private void SetupCrouchSystem()
    {
        if (capsuleCollider == null) return;

        originalHeight = capsuleCollider.height;
        originalCenter = capsuleCollider.center;

        crouchCenter = new Vector3(
            originalCenter.x,
            originalCenter.y - (originalHeight - crouchHeight) * 0.5f,
            originalCenter.z
        );
    }

    private void FixedUpdate()
    {
        if (playerController == null) return;

        UpdatePlayerCenterPoint();
        UpdateGroundState();
        UpdateJumpBuffers();
        ApplyMovement();
    }

    /// <summary>
    /// Calculate the player's center point for raycasting (matches HumanoidLandController approach)
    /// </summary>
    private void UpdatePlayerCenterPoint()
    {
        if (capsuleCollider == null) return;
        playerCenterPoint = rb.position + capsuleCollider.center;
    }

    private void UpdateGroundState()
    {
        if (groundDetector == null) return;

        groundNormal = groundDetector.GroundNormal;
        currentGroundType = groundDetector.CurrentGroundType;

        bool currentlyGrounded = IsGrounded;

        // Track when we leave the ground to start coyote time
        if (wasGroundedLastFrame && !currentlyGrounded)
        {
            // Just left ground - start coyote timer
            if (useCoyoteTime)
            {
                coyoteTimer = coyoteTime;
                DebugLog($"Left ground - coyote time started ({coyoteTime:F2}s)");
            }
        }

        // Check for landing
        if (!wasGrounded && currentlyGrounded && rb.linearVelocity.y < -2f)
        {
            OnLanded();
        }

        wasGrounded = currentlyGrounded;
        wasGroundedLastFrame = currentlyGrounded;
    }

    private void UpdateJumpBuffers()
    {
        // Update jump buffer timer
        if (jumpInputBuffered)
        {
            jumpBufferTimer -= Time.fixedDeltaTime;

            if (jumpBufferTimer <= 0f)
            {
                // Buffer expired
                ClearJumpBuffer();
                DebugLog("Jump buffer expired");
            }
            else if (IsGrounded && !IsCrouching)
            {
                // We're grounded and have a buffered jump - execute it!
                DebugLog($"Executing buffered jump (was buffered for {jumpBufferTime - jumpBufferTimer:F2}s)");
                Jump();
                ClearJumpBuffer();
            }
        }

        // Update coyote timer (only when not grounded)
        if (!IsGrounded && coyoteTimer > 0f)
        {
            coyoteTimer -= Time.fixedDeltaTime;

            if (coyoteTimer <= 0f)
            {
                DebugLog("Coyote time expired");
            }
        }
        else if (IsGrounded)
        {
            // Reset coyote timer when grounded
            coyoteTimer = 0f;
        }
    }

    /// <summary>
    /// REFACTORED: Apply movement combining HumanoidLandController's slope physics 
    /// with velocity-based acceleration/deceleration for good movement feel.
    /// </summary>
    private void ApplyMovement()
    {
        if (rb == null || playerController == null) return;

        // Step 1: Get target movement speed
        float targetSpeed = GetCurrentMovementSpeed();

        // Step 2: Get player's forward and right vectors
        Vector3 forward = playerController.Forward;
        Vector3 right = playerController.Right;

        // Step 3: Calculate target velocity from input
        Vector3 targetVelocity = Vector3.zero;

        if (movementInput.magnitude > 0.1f)
        {
            // Create movement direction from input
            Vector3 moveDirection = (forward * movementInput.y + right * movementInput.x).normalized;

            // If grounded, project onto ground surface (this is where slope physics starts)
            if (IsGrounded)
            {
                moveDirection = Vector3.ProjectOnPlane(moveDirection, groundNormal).normalized;
            }

            targetVelocity = moveDirection * targetSpeed;
        }

        // Step 4: Get current horizontal velocity (ignore Y component)
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 currentHorizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
        Vector3 targetHorizontalVelocity = new Vector3(targetVelocity.x, 0, targetVelocity.z);

        // Step 5: Calculate velocity difference
        Vector3 horizontalVelocityDifference = targetHorizontalVelocity - currentHorizontalVelocity;

        // Step 6: Choose acceleration or deceleration based on input
        float currentAcceleration = movementInput.magnitude > 0.1f ? acceleration : deceleration;

        // Step 7: Calculate force from velocity difference
        Vector3 force = horizontalVelocityDifference * currentAcceleration;

        // Step 8: Apply slope physics modifications (THE KEY ADDITION from HumanoidLandController)
        if (IsGrounded)
        {
            force = ApplySlopePhysics(force);
        }

        // Step 9: Debug visualization
        if (showMovementDebug)
        {
            Debug.DrawRay(playerCenterPoint, force, Color.red, 0.5f);
            DebugLog($"Force - Horizontal: {new Vector3(force.x, 0, force.z).magnitude:F2}, Y: {force.y:F2}, " +
                    $"CurrentSpeed: {currentHorizontalVelocity.magnitude:F2}, TargetSpeed: {targetSpeed:F2}");
        }

        // Step 10: Apply force
        rb.AddForce(force, ForceMode.Acceleration);
    }

    /// <summary>
    /// CORE NEW METHOD: Apply slope physics to the calculated force vector.
    /// This modifies the force based on the current ground slope and upcoming terrain.
    /// Now works with force vectors instead of movement vectors.
    /// </summary>
    private Vector3 ApplySlopePhysics(Vector3 force)
    {
        if (groundDetector == null) return force;

        // Get the ground slope angle
        float groundSlopeAngle = groundDetector.currentSlopeAngle;

        // CASE 1: Flat ground (slope angle = 0)
        if (groundSlopeAngle == 0f)
        {
            return HandleFlatGroundSlopes(force);
        }
        // CASE 2: On a slope (angle > 0)
        else
        {
            return HandleSlopedGround(force, groundSlopeAngle);
        }
    }

    /// <summary>
    /// Handle movement on flat ground - check ahead for steep slopes to prevent climbing.
    /// </summary>
    private Vector3 HandleFlatGroundSlopes(Vector3 force)
    {
        // If moving, check ahead for steep slopes
        if (IsMoving)
        {
            // Create a normalized movement direction from current force
            Vector3 moveDirection = new Vector3(force.x, 0f, force.z).normalized;

            // Check if there's a steep slope ahead that we shouldn't be able to climb
            if (CheckForSteepSlopeAhead(moveDirection))
            {
                // Apply downward force to prevent climbing (but not extreme)
                DebugLog($"Steep slope detected ahead - preventing climb");
                force.y = -50f; // Moderate downward force to stop forward momentum
            }
        }

        return force;
    }

    /// <summary>
    /// Check if there's a steep slope ahead in the movement direction.
    /// Raycasts forward at ground level to detect upcoming slopes.
    /// </summary>
    private bool CheckForSteepSlopeAhead(Vector3 moveDirection)
    {
        if (groundDetector == null) return false;

        // Calculate ray origin at ground level (slightly above ground)
        float rayHeight = playerCenterPoint.y - groundDetector.DistanceToGround + 0.05f;
        Vector3 rayOrigin = new Vector3(playerCenterPoint.x, rayHeight, playerCenterPoint.z);

        // Raycast forward in movement direction
        if (Physics.Raycast(rayOrigin, moveDirection, out RaycastHit hit, slopeCheckDistance))
        {
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

            if (showMovementDebug)
            {
                Debug.DrawRay(rayOrigin, moveDirection * slopeCheckDistance,
                    slopeAngle > maxSlopeAngle ? Color.red : Color.green, 0.5f);
            }

            // Check if the slope ahead is too steep
            if (slopeAngle > maxSlopeAngle)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Handle movement on sloped ground - apply additional forces for realistic slope physics.
    /// </summary>
    private Vector3 HandleSlopedGround(Vector3 force, float groundSlopeAngle)
    {
        // CASE 2A: Walkable slope (within max angle)
        if (groundSlopeAngle < maxSlopeAngle)
        {
            // Movement force is already projected onto the slope from ProjectOnPlane earlier
            // Just ensure we stick to the ground
            if (IsMoving)
            {
                force.y += groundedGravity * 10f; // Small downward force to stick to slope
            }
        }
        // CASE 2B: Too steep slope (exceeds max angle) - PREVENT CLIMBING
        else
        {
            // Calculate a reasonable sliding force based on slope angle
            // Use a much gentler formula to prevent extreme forces
            float slideMultiplier = Mathf.Clamp((groundSlopeAngle - maxSlopeAngle) / 45f, 0f, 1f);
            float slidingForce = -slopeSlidingForce * slideMultiplier; // Gentle sliding force

            DebugLog($"On too-steep slope - Angle: {groundSlopeAngle:F1}°, Sliding force: {slidingForce:F2}");

            // Apply sliding force (but don't override if already falling faster)
            if (force.y > slidingForce)
            {
                force.y = slidingForce;
            }

            // Reduce horizontal force on slopes we can't climb (but not to zero - allow some sliding movement)
            force.x *= 0.5f;
            force.z *= 0.5f;
        }

        if (showMovementDebug)
        {
            Debug.DrawRay(rb.position, force,
                groundSlopeAngle > maxSlopeAngle ? Color.yellow : Color.cyan, 0.5f);
        }

        return force;
    }

    private float GetCurrentMovementSpeed()
    {
        if (playerData == null) return 5f;

        if (IsCrouching) return playerData.crouchSpeed;
        if (IsSpeedModified) return playerData.runSpeed;

        return playerData.walkSpeed;
    }

    private void Jump()
    {
        if (!IsGrounded || rb == null)
        {
            DebugLog($"Jump failed - IsGrounded: {IsGrounded}, rb: {rb != null}");
            return;
        }

        // Additional safety check: Don't allow jumping on too-steep slopes
        if (IsOnTooSteepSlope())
        {
            DebugLog($"Jump blocked in Jump() - on too steep slope ({groundDetector.currentSlopeAngle:F1}°)");
            return;
        }

        float jumpHeight = playerData?.jumpHeight ?? 2f;
        float jumpForce = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);

        Vector3 currentVelocity = rb.linearVelocity;
        currentVelocity.y = 0f;
        rb.linearVelocity = currentVelocity;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

        // Clear coyote timer after successful jump
        coyoteTimer = 0f;

        DebugLog($"Jump executed - Force: {jumpForce}");
    }

    #region Ground Detector Event Handlers

    private void OnGrounded()
    {
        DebugLog("Ground detector: Player grounded");

        // Check if we have a buffered jump waiting
        // But DON'T execute it if we landed on a too-steep slope
        if (jumpInputBuffered && !IsCrouching && !IsOnTooSteepSlope())
        {
            DebugLog("Executing buffered jump on landing");
            Jump();
            ClearJumpBuffer();
        }
        else if (jumpInputBuffered && IsOnTooSteepSlope())
        {
            DebugLog("Buffered jump cleared - landed on too steep slope");
            ClearJumpBuffer();
        }
    }

    private void OnLeftGround()
    {
        DebugLog("Ground detector: Player left ground");
    }

    private void OnGroundHitChanged(RaycastHit hit)
    {
        if (hit.collider != null)
        {
            var groundTypeId = hit.collider.GetComponent<GroundTypeIdentifier>();
            currentGroundType = groundTypeId?.groundType ?? GroundType.Default;
        }
    }

    #endregion

    #region Slope Helper Methods

    /// <summary>
    /// Check if the player is currently standing on a slope that's too steep to walk/jump on
    /// </summary>
    private bool IsOnTooSteepSlope()
    {
        if (groundDetector == null) return false;
        if (!IsGrounded) return false;

        return groundDetector.currentSlopeAngle > maxSlopeAngle;
    }

    /// <summary>
    /// Check if the player is trying to move UP the slope (in any direction - forward, backward, or strafe)
    /// </summary>
    private bool IsMovingUpSlope(Vector3 movementForce)
    {
        if (groundDetector == null) return false;
        if (!IsMoving) return false;

        // Get the slope's ground normal
        Vector3 groundNormal = groundDetector.GroundNormal;

        // Get the horizontal component of the movement force
        Vector3 horizontalMovement = new Vector3(movementForce.x, 0f, movementForce.z);

        if (horizontalMovement.magnitude < 0.01f)
        {
            return false;
        }

        // Calculate the cross product to find the "uphill" direction
        // Cross product of world up and ground normal gives us the direction perpendicular to both
        // This is the direction along the slope (either uphill or downhill)
        Vector3 slopeSideDirection = Vector3.Cross(Vector3.up, groundNormal);

        // If cross product is nearly zero, the slope is flat
        if (slopeSideDirection.magnitude < 0.01f)
        {
            return false;
        }

        slopeSideDirection.Normalize();

        // Now cross the slope side direction with the ground normal to get the uphill direction
        Vector3 uphillDirection = Vector3.Cross(slopeSideDirection, groundNormal);
        uphillDirection.Normalize();

        // The uphill direction should point upward - if it points down, flip it
        if (uphillDirection.y < 0f)
        {
            uphillDirection = -uphillDirection;
        }

        // Calculate the dot product between movement and uphill direction
        float dotProduct = Vector3.Dot(horizontalMovement.normalized, uphillDirection);

        if (showMovementDebug)
        {
            Debug.DrawRay(rb.position, uphillDirection * 2f, Color.magenta, 0.5f);
            Debug.DrawRay(rb.position + Vector3.up * 0.1f, slopeSideDirection * 2f, Color.cyan, 0.5f);
            Debug.DrawRay(rb.position, horizontalMovement.normalized * 2f,
                dotProduct > -0.05f ? Color.red : Color.green, 0.5f);
            Debug.DrawRay(rb.position, groundNormal * 1.5f, Color.blue, 0.5f);

            DebugLog($"Slope check - Normal: {groundNormal}, Uphill: {uphillDirection}, " +
                    $"Movement: {horizontalMovement.normalized}, Dot: {dotProduct:F3}, Blocked: {dotProduct > -0.05f}");
        }

        // If dot product is greater than -0.05, player has ANY upward component
        return dotProduct > -0.05f;
    }

    #endregion

    #region Unified Crouch Event Handlers

    private void OnPlayerStartedCrouching()
    {
        if (!physicalCrouchApplied)
        {
            ApplyPhysicalCrouch();
        }
    }

    private void OnPlayerStoppedCrouching()
    {
        if (physicalCrouchApplied)
        {
            RemovePhysicalCrouch();
        }
    }

    private void ApplyPhysicalCrouch()
    {
        if (capsuleCollider == null) return;

        physicalCrouchApplied = true;
        IsSecondaryActive = true;
        capsuleCollider.height = crouchHeight;
        capsuleCollider.center = crouchCenter;

        DebugLog("Applied physical crouch");
    }

    private void RemovePhysicalCrouch()
    {
        if (capsuleCollider == null) return;

        if (CanStandUp())
        {
            physicalCrouchApplied = false;
            IsSecondaryActive = false;
            capsuleCollider.height = originalHeight;
            capsuleCollider.center = originalCenter;

            DebugLog("Removed physical crouch");
        }
        else
        {
            DebugLog("Cannot stand up - blocked by ceiling, keeping physical crouch");
        }
    }

    #endregion

    private void OnLanded()
    {
        DebugLog($"Player landed on {currentGroundType}");
    }

    public void ForceCleanState()
    {
        movementInput = Vector2.zero;
        IsSpeedModified = false;
        IsMoving = false;

        // Clear jump buffering state
        ClearJumpBuffer();
        coyoteTimer = 0f;
        wasGroundedLastFrame = IsGrounded;

        if (playerController != null)
        {
            if (playerController.IsCrouching && !physicalCrouchApplied)
            {
                ApplyPhysicalCrouch();
            }
            else if (!playerController.IsCrouching && physicalCrouchApplied)
            {
                if (CanStandUp())
                {
                    RemovePhysicalCrouch();
                }
            }
        }

        DebugLog("Ground movement state force cleaned and synced with PlayerController");
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GroundMovementController] {message}");
        }
    }
}