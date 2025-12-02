using System.Collections;
using UnityEditor.EditorTools;
using UnityEngine;

public class MonsterAttack : MonoBehaviour
{
    [Header("Components")]
    [SerializeField]
    private UnderwaterMonsterController controller;
    [SerializeField] private MonsterAnimationHandler animationHandler;

    [SerializeField] private Rigidbody rb;

    [SerializeField] private bool enableDebugLogs = false;

    [Header("Attack State")]
    public bool playerInAttackRange = false;
    public bool isAttacking = false;
    // public bool didAttack = false; // True if attack was successful, and will trigger monster to pause (cooldown) after attack

    [Header("Attack Parameters")]
    [Tooltip("The force applied to the enemy when it charges to attack")]
    [SerializeField] private float attackChargeForce = 400f;
    [SerializeField] private float attackDuration = 1f;

    [Header("Improved Attack Timing")]
    [Tooltip("Time to gradually slow down before attacking")]
    [SerializeField] private float decelerationTime = 0.3f;
    [Tooltip("Brief pause at zero velocity before lunging")]
    [SerializeField] private float preparationPause = 0.2f;

    [Header("Attack Rotation")]
    [Tooltip("How fast the monster rotates to face the player during attack preparation")]
    [SerializeField] private float attackRotationSpeed = 5f;

    private Vector3 attackDirection;
    private Vector3 initialVelocity; // Store velocity at start of attack
    private Quaternion targetRotation; // Target rotation to face player

    private void Awake()
    {
        if (controller == null) controller = GetComponentInParent<UnderwaterMonsterController>();
        if (animationHandler == null) animationHandler = GetComponentInParent<MonsterAnimationHandler>();
        if (rb == null) rb = controller.rb;
    }

    public IEnumerator AttackCoroutine()
    {
        isAttacking = true;
        // didAttack = false;

        // Calculate attack direction and store initial velocity
        attackDirection = (controller.player.transform.position - transform.position).normalized;
        initialVelocity = rb.linearVelocity;

        // Calculate target rotation to face the player (only Z-axis rotation)
        targetRotation = Quaternion.LookRotation(attackDirection, Vector3.up);

        DebugLog($"Starting attack sequence. Initial velocity: {initialVelocity.magnitude:F2}");

        // Disable movement system during attack
        controller.DeactivateMovement();

        // Phase 1: Gradual deceleration + rotation towards player
        yield return StartCoroutine(GradualDecelerationAndRotation());

        // Phase 2: Brief preparation pause (monster is now facing player and stationary)
        DebugLog("Preparation pause - monster is locked on target");
        yield return new WaitForSeconds(preparationPause);

        // Phase 3: Perform the actual attack
        PerformAttack();

        // Phase 4: Attack duration (cooldown)
        yield return new WaitForSeconds(attackDuration);

        OnAttackFinished();
    }

    /// <summary>
    /// Smoothly reduces the monster's velocity to zero while rotating to face the player.
    /// This creates a natural "winding up" effect with predator-like tracking behavior.
    /// </summary>
    private IEnumerator GradualDecelerationAndRotation()
    {
        DebugLog("Starting gradual deceleration and rotation towards player");

        float elapsedTime = 0f;
        Vector3 startVelocity = rb.linearVelocity;
        Vector3 startAngularVelocity = rb.angularVelocity;

        // Store starting rotation for smooth interpolation
        Quaternion startRotation = transform.rotation;

        while (elapsedTime < decelerationTime)
        {
            // Calculate progress (0 to 1)
            float progress = elapsedTime / decelerationTime;

            // Use smooth interpolation for natural feel
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            // === MOVEMENT DECELERATION ===
            Vector3 currentVelocity = Vector3.Lerp(startVelocity, Vector3.zero, smoothProgress);
            rb.linearVelocity = currentVelocity;

            // === ROTATION TOWARDS PLAYER ===
            // Smoothly rotate towards the target rotation
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothProgress);

            // Stop any existing angular velocity during the rotation
            rb.angularVelocity = Vector3.Lerp(startAngularVelocity, Vector3.zero, smoothProgress);

            elapsedTime += Time.deltaTime;
            yield return null; // Wait for next frame
        }

        // Ensure we're completely stopped and facing the right direction
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.rotation = targetRotation;

        DebugLog("Deceleration and rotation complete - monster locked on target");
    }

    private void PerformAttack()
    {
        // Use the attack direction that was calculated at the start
        // (Monster is already facing this direction from the rotation phase)
        rb.AddForce(attackDirection * attackChargeForce, ForceMode.Impulse);

        DebugLog($"Lunging towards player with force: {attackChargeForce}");
        animationHandler.PlayAttackAnimation();
    }

    private void OnAttackFinished()
    {
        isAttacking = false;
        //if true, monster will pause afterwards via behaviour tree
        //didAttack = managedToAttack;
        controller.ActivateMovement();
        DebugLog("Attack sequence complete - movement reactivated");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // More specific check
        {
            playerInAttackRange = true;
            DebugLog("Player entered attack range");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) // More specific check
        {
            playerInAttackRange = false;
            OnAttackFinished();
            DebugLog("Player left attack range");
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MonsterAttack] {message}");
        }
    }

    // Public methods for tuning during gameplay/testing
    [Header("Runtime Tuning (for testing)")]
    [SerializeField] private bool allowRuntimeTuning = false;

    private void OnValidate()
    {
        // Ensure timing values make sense
        if (decelerationTime + preparationPause > attackDuration * 0.8f)
        {
            Debug.LogWarning($"[MonsterAttack] Total preparation time ({decelerationTime + preparationPause:F2}s) is very close to attack duration ({attackDuration:F2}s). Consider adjusting values.");
        }
    }

    /// <summary>
    /// Public method to adjust deceleration timing during runtime for testing
    /// </summary>
    public void SetDecelerationTime(float newTime)
    {
        if (allowRuntimeTuning)
        {
            decelerationTime = Mathf.Max(0.1f, newTime);
            DebugLog($"Deceleration time changed to: {decelerationTime:F2}s");
        }
    }

    /// <summary>
    /// Public method to adjust preparation pause during runtime for testing
    /// </summary>
    public void SetPreparationPause(float newPause)
    {
        if (allowRuntimeTuning)
        {
            preparationPause = Mathf.Max(0f, newPause);
            DebugLog($"Preparation pause changed to: {preparationPause:F2}s");
        }
    }

    /// <summary>
    /// Public method to adjust rotation speed during runtime for testing
    /// </summary>
    public void SetRotationSpeed(float newSpeed)
    {
        if (allowRuntimeTuning)
        {
            attackRotationSpeed = Mathf.Max(0.1f, newSpeed);
            DebugLog($"Attack rotation speed changed to: {attackRotationSpeed:F2}");
        }
    }
}