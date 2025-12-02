using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class MonsterHealth : MonoBehaviour
{
    public event Action OnDeath;
    public event Action OnMonsterTakeHit;

    public bool isAlive = true;
    public bool isHit = false;

    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private float maxHealth = 100f;
    public float CurrentHealth { get => currentHealth; set => currentHealth = value; }
    public float MaxHealth { get => maxHealth; set => maxHealth = value; }

    [Button]
    public void TakeDamage(float damage)
    {
        if (!isAlive)
            return;

        currentHealth -= damage;

        OnMonsterTakeHit?.Invoke();
        isHit = true;

        if (currentHealth <= 0)
        {
            OnDeath?.Invoke();
            isAlive = false;
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    [Button]
    public void KillMonster()
    {
        TakeDamage(currentHealth);
    }

}
