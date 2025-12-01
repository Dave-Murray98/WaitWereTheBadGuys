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

    [Header("Attack Parameters")]
    [Tooltip("The force applied to the enemy when it charges to attack")]
    [SerializeField] private float attackChargeForce = 100f;
    [SerializeField] private float attackDuration = 2f;
    [SerializeField] private float attackChargeUpDelay = 0.5f;

    private Vector3 attackDirection;

    private void Awake()
    {
        if (controller == null) controller = GetComponentInParent<UnderwaterMonsterController>();
        if (animationHandler == null) animationHandler = GetComponentInParent<MonsterAnimationHandler>();
        if (rb == null) rb = controller.rb;
    }

    public IEnumerator AttackCoroutine()
    {
        isAttacking = true;

        //get player direction
        attackDirection = (controller.player.transform.position - transform.position).normalized;

        //wait for charge up (allows player to potentially dodge)
        yield return new WaitForSeconds(attackChargeUpDelay);

        PerformAttack();

        //wait for attack duration (cooldown)
        yield return new WaitForSeconds(attackDuration);
        OnAttackFinished();
    }

    private void PerformAttack()
    {
        rb.AddForce(attackDirection * attackChargeForce, ForceMode.Impulse);

        controller.DeactivateMovement();

        DebugLog("Attacking");
        animationHandler.PlayAttackAnimation();
    }

    private void OnAttackFinished()
    {
        isAttacking = false;
        controller.ActivateMovement();
    }

    private void OnTriggerEnter(Collider other)
    {
        playerInAttackRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        playerInAttackRange = false;
    }


    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MonsterAttack] {message}");
        }
    }
}
