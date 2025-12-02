using System;
using UnityEngine;

public class MonsterHealth : MonoBehaviour
{
    public event Action OnDeath;

    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private float maxHealth = 100f;
    public float CurrentHealth { get => currentHealth; set => currentHealth = value; }
    public float MaxHealth { get => maxHealth; set => maxHealth = value; }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            OnDeath?.Invoke();
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

}
