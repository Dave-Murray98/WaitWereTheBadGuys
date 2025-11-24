using Infohazard.HyperNav;
using Pathfinding;
using UnityEngine;

/// <summary>
/// CLEANED: Simple coordination between save system and movement systems
/// Clear separation of concerns - no complex restoration logic here
/// </summary>
public class NPCController : MonoBehaviour
{
    public NPCMovementStateMachine movementStateMachine;
    public NPCGroundMovementController groundController;
    public NPCWaterMovementController waterController;
    public NPCLedgeClimbingController climbingController;
    public Rigidbody rb;
    public FollowerEntity followerEntity;

    public Transform target;

    public NPCGroundDetector groundDetector;
    public NPCWaterDetector waterDetector;

    private void Awake()
    {
        FindCoreComponents();

        movementStateMachine.Initialize(this);
        groundController.Initialize(this);
        waterController.Initialize(this);
    }

    private void FindCoreComponents()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (movementStateMachine == null) movementStateMachine = GetComponent<NPCMovementStateMachine>();

        if (waterDetector == null) waterDetector = GetComponent<NPCWaterDetector>();
        if (groundDetector == null) groundDetector = GetComponent<NPCGroundDetector>();

        if (groundController == null) groundController = GetComponent<NPCGroundMovementController>();
        if (waterController == null) waterController = GetComponent<NPCWaterMovementController>();

        if (climbingController == null) climbingController = GetComponent<NPCLedgeClimbingController>();
    }

    #region Save/Load Coordination - CLEANED

    /// <summary>
    /// CLEANED: Called by NPCSaveComponent when restoration starts
    /// Simply passes the call to the state machine
    /// </summary>
    public void PrepareForPositionRestoration()
    {
        if (movementStateMachine != null)
        {
            movementStateMachine.PrepareForRestoration();
        }
    }

    /// <summary>
    /// CLEANED: Called by NPCSaveComponent when restoration completes
    /// Simply passes the call to the state machine
    /// </summary>
    public void CompletePositionRestoration()
    {
        if (movementStateMachine != null)
        {
            movementStateMachine.CompleteRestoration();
        }
    }

    #endregion
}