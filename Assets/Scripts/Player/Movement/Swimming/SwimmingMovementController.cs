using NWH.DWP2.WaterObjects;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// SIMPLIFIED: Swimming movement controller that integrates cleanly with HeadBobbingController.
/// Provides Subnautica-style 3D movement with simple surface bobbing integration.
/// No complex depth management
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SwimmingMovementController : MonoBehaviour, IMovementController
{
    [Header("Swimming Movement")]
    [SerializeField] private float swimSpeed = 4f;
    [SerializeField] private float fastSwimSpeed = 6f;
    [SerializeField] private float swimAcceleration = 15f;
    [SerializeField] private float swimDeceleration = 10f;
    [SerializeField] private float maxSwimSpeed = 8f;

    [Header("Surface/Dive Forces")]
    [SerializeField] private float surfaceForce = 15f;
    [SerializeField] private float diveForce = 12f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = true;

    // Interface properties
    public MovementMode MovementMode => MovementMode.Swimming;
    public bool IsGrounded => false;
    public bool IsMoving { get; private set; }
    public bool IsSpeedModified { get; private set; }
    public bool IsSecondaryActive { get; private set; }

    // Component references
    private PlayerController playerController;
    private Rigidbody rb;
    private PlayerWaterDetector waterDetector;
    [SerializeField] private PlayerSwimmingDepthManager depthManager;
    private CameraController cameraController;

    // Input state
    private Vector2 movementInput;
    private bool isFastSwimming;
    [ShowInInspector] private bool isSurfacing;
    private bool isDiving;

    // Movement calculation
    private Vector3 swimDirection;

    public Vector3 GetVelocity() => rb != null ? rb.linearVelocity : Vector3.zero;

    [Header("Surface Swimming")]
    public WaterObject waterObject; // this will control the player's buoyancy when at the water's surface (he'll move up and down with the waves)

    // this will effectively disable gravity when underwater to allow the player to float (it's applied when underwater, 
    // but not when at the surface (so the player will fall while at water's surface, but won't sink underwater))
    private float antigravity;

    #region Initialization

    public void Initialize(PlayerController controller)
    {
        playerController = controller;
        rb = GetComponent<Rigidbody>();
        waterDetector = GetComponent<PlayerWaterDetector>();
        depthManager = GetComponent<PlayerSwimmingDepthManager>();
        cameraController = controller.cameraController;

        if (waterObject == null)
            waterObject = GetComponent<WaterObject>();

        ValidateComponents();
        //StoreOriginalPhysics();

        DebugLog("Swimming controller initialized");
    }

    private void ValidateComponents()
    {
        if (rb == null)
        {
            Debug.LogError("[SwimmingController] Rigidbody not found!");
            return;
        }

        if (waterDetector == null)
        {
            Debug.LogError("[SwimmingController] PlayerWaterDetector not found!");
            return;
        }

        if (cameraController == null)
        {
            Debug.LogError("[SwimmingController] CameraController not found!");
        }

    }

    #endregion

    #region Input Handling

    public void HandleMovement(Vector2 moveInput, bool isSpeedModified)
    {
        movementInput = moveInput;
        isFastSwimming = isSpeedModified;

        IsMoving = moveInput.magnitude > 0.1f;
        IsSpeedModified = isFastSwimming && IsMoving;

        CalculateSwimDirection();
    }

    public void HandleHorizontalRotation(float currentYRotation)
    {
        // Preserve X rotation from swimming body rotation system
        Vector3 currentEuler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(currentEuler.x, currentYRotation, currentEuler.z);
    }

    public void HandlePrimaryAction()
    {
        isSurfacing = true;
        DebugLog("Surface input started");
    }

    public void HandlePrimaryActionReleased()
    {
        isSurfacing = false;
        DebugLog("Surface input stopped");
    }

    public void HandleSecondaryAction()
    {
        isDiving = true;
        IsSecondaryActive = true;
        DebugLog("Dive input started");
    }

    public void HandleSecondaryActionReleased()
    {
        isDiving = false;
        IsSecondaryActive = false;
        DebugLog("Dive input stopped");
    }

    #endregion

    #region Movement Calculation

    /// <summary>
    /// Calculate 3D swim direction
    /// </summary>
    private void CalculateSwimDirection()
    {
        swimDirection = Vector3.zero;

        if (movementInput.magnitude > 0.1f && cameraController != null)
        {
            // Get camera's 3D look direction (includes vertical component)
            Vector3 cameraForward = cameraController.GetCameraLookDirection();
            Vector3 cameraRight = cameraController.CameraRight;

            // Calculate movement direction relative to camera
            Vector3 forwardMovement = cameraForward * movementInput.y;
            Vector3 rightMovement = cameraRight * movementInput.x;

            swimDirection = (forwardMovement + rightMovement).normalized;
        }
    }

    #endregion

    #region Controller Lifecycle

    public void OnControllerActivated()
    {
        //SetupSwimmingPhysics();
        ResetState();
        DebugLog("Swimming controller activated");
    }

    public void OnControllerDeactivated()
    {
        //RestoreOriginalPhysics();
        ResetState();
        DebugLog("Swimming controller deactivated");
    }

    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        DebugLog($"Swimming state changed: {previousState} -> {newState}");
    }

    public void Cleanup()
    {
        //RestoreOriginalPhysics();
        DebugLog("Swimming controller cleaned up");
    }


    private void ResetState()
    {
        movementInput = Vector2.zero;
        isFastSwimming = false;
        isSurfacing = false;
        isDiving = false;
        swimDirection = Vector3.zero;

        IsMoving = false;
        IsSpeedModified = false;
        IsSecondaryActive = false;
    }

    #endregion

    #region Physics Update

    private void FixedUpdate()
    {
        if (rb == null || playerController == null) return;

        DebugLog($"Swimming movement controller fixed update being called, applying forces, etc.");

        ApplySwimmingMovement();
        ApplySurfaceDiveForces();
        ApplyPsuedoGravity();

        HandleWaterObject();

    }

    /// <summary>
    /// We want the water object to affect the player's buoyancy when at the water's surface, so he can rise and fall with the water level and waves
    /// But we don't want the water object to affect the player's buoyancy when the player is underwater (otherwise he would continually rise upwards)
    /// </summary>
    private void HandleWaterObject()
    {
        if (depthManager.IsPlayerPositionedAtSurfaceOfWater)
        {
            waterObject.enabled = true;
        }
        else
        {
            waterObject.enabled = false;
        }
    }

    /// <summary>
    /// Apply pseudo-gravity when at the surface (by removing antigravity force)
    /// and apply antigravity when underwater (by adding antigravity force)
    /// </summary>
    private void ApplyPsuedoGravity()
    {
        // If player is at the surface of water, remove antigravity force - we want gravity to be applied when the player is above/at the water's surface
        // and antigravity to be applied when the player is underwater (so the player doesn't sink)
        antigravity = depthManager.IsPlayerPositionedAtSurfaceOfWater ? 0 : -Physics.gravity.y;
        rb.AddForce(Vector3.up * antigravity, ForceMode.Acceleration);
    }

    /// <summary>
    /// Apply camera-relative swimming movement
    /// </summary>
    private void ApplySwimmingMovement()
    {
        Vector3 targetVelocity = Vector3.zero;

        if (swimDirection.magnitude > 0.01f)
        {
            float currentSpeed = isFastSwimming ? fastSwimSpeed : swimSpeed;
            targetVelocity = swimDirection * currentSpeed;
        }

        // Apply acceleration/deceleration
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 velocityDifference = targetVelocity - currentVelocity;

        float acceleration = swimDirection.magnitude > 0.01f ? swimAcceleration : swimDeceleration;
        Vector3 force = velocityDifference * acceleration;

        // If the player is already at the water's surface, we don't want him to be able to continue moving upwards (or he could jump out of the water)
        if (depthManager.IsPlayerPositionedAtSurfaceOfWater)
        {
            if (force.y > 0f)
                force.y = 0f;
        }

        // Clamp to max speed
        force = Vector3.ClampMagnitude(force, maxSwimSpeed * acceleration);

        rb.AddForce(force, ForceMode.Acceleration);

        //DebugLog($"Swimming movement applied - Velocity: {rb.linearVelocity}, Force: {force}");
    }

    /// <summary>
    /// Apply surface and dive force
    /// </summary>
    private void ApplySurfaceDiveForces()
    {
        if (!waterDetector.IsInWaterState) return;

        // Surface input
        if (isSurfacing)
        {
            // If player is not positioned at the surface of water, apply surface force (we don't want the player to be able to continue surfacing when he's already at the water's surface) - or he could jump out of the water
            if (!depthManager.IsPlayerPositionedAtSurfaceOfWater)
                rb.AddForce(Vector3.up * surfaceForce, ForceMode.Acceleration);
        }

        // Diving input
        if (isDiving)
            rb.AddForce(Vector3.down * diveForce, ForceMode.Acceleration);

    }

    #endregion

    #region Public API

    /// <summary>
    /// Get current swimming state information
    /// </summary>
    public string GetSwimmingStateInfo()
    {
        float currentSpeed = isFastSwimming ? fastSwimSpeed : swimSpeed;

        return $"Swimming - Speed: {currentSpeed:F1}m/s, FastSwim: {isFastSwimming}, " +
               $"Surfacing: {isSurfacing}, Diving: {isDiving}, Direction: {swimDirection}";
    }

    /// <summary>
    /// Check if player can exit water
    /// </summary>
    public bool CanExitWater()
    {
        if (waterDetector == null) return false;

        // Simple exit logic - chest must be shallow and not moving fast vertically
        return waterDetector.ChestDepthInWater < 0.2f && Mathf.Abs(rb.linearVelocity.y) < 2f;
    }

    /// <summary>
    /// Force surface movement (emergency)
    /// </summary>
    public void ForceSurface()
    {
        if (rb != null && waterDetector.IsInWaterState)
        {
            rb.AddForce(Vector3.up * surfaceForce * 2f, ForceMode.Acceleration);
            DebugLog("Emergency surface applied");
        }
    }

    /// <summary>
    /// Clean state for transitions
    /// </summary>
    public void ForceCleanState()
    {
        ResetState();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        DebugLog("Swimming state cleaned");
    }

    /// <summary>
    /// Set swimming speeds
    /// </summary>
    public void SetSwimmingSpeeds(float normalSpeed, float fastSpeed)
    {
        swimSpeed = Mathf.Max(0.5f, normalSpeed);
        fastSwimSpeed = Mathf.Max(swimSpeed, fastSpeed);
        DebugLog($"Swimming speeds set - Normal: {swimSpeed}, Fast: {fastSwimSpeed}");
    }

    /// <summary>
    /// Set surface/dive forces
    /// </summary>
    public void SetSurfaceDiveForces(float surface, float dive)
    {
        surfaceForce = Mathf.Max(1f, surface);
        diveForce = Mathf.Max(1f, dive);
        DebugLog($"Surface/Dive forces set - Surface: {surfaceForce}, Dive: {diveForce}");
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SwimmingController] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        // Draw movement direction
        if (swimDirection.magnitude > 0.01f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, swimDirection * 3f);
        }
    }

    #endregion
}