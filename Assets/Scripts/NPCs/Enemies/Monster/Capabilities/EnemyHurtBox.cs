using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

public class EnemyHurtBox : MonoBehaviour
{
    public float damage = 10f;
    public float attackKnockBackForce = 100f;
    public Vector3 forceDirection = Vector3.forward;

    public float cooldown = 1f;

    [ShowInInspector] private bool canDamage = true;

    private void OnTriggerEnter(Collider other)
    {
        if (canDamage)
        {
            PlayerManager player = other.GetComponent<PlayerManager>();

            if (player != null)
            {
                player.ModifyHealth(-damage);

                Rigidbody rb = player.GetComponent<Rigidbody>();
                rb.AddForce(forceDirection * attackKnockBackForce, ForceMode.Impulse);
            }

            StartCoroutine(Cooldown());
        }

    }

    private IEnumerator Cooldown()
    {
        canDamage = false;

        yield return new WaitForSeconds(cooldown);

        canDamage = true;

    }
}
