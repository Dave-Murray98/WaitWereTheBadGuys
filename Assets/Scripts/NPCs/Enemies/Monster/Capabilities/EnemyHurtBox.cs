using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

public class EnemyHurtBox : MonoBehaviour
{
    public float damage = 10f;

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
