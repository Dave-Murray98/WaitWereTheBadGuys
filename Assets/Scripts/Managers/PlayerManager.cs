using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlayerManager : MonoBehaviour, IManager
{
    [Header("Current Stats")]
    public float currentHealth;

    private PlayerData playerData;
    private bool isDead = false;

    // Public Getters
    public bool IsDead => isDead;
    public float HealthPercentage => playerData != null ? currentHealth / playerData.maxHealth : 0f;

    public void Initialize()
    {
        //        Debug.Log("PlayerManager Initialized");
        RefreshReferences();
        GameEvents.OnPlayerDeath += HandlePlayerDeath;
    }

    public void RefreshReferences()
    {
        //   Debug.Log("PlayerManager: Refreshing references");
        playerData = GameManager.Instance?.playerData;

        if (playerData != null)
        {
            // Simple initialization - just use max health if no specific health is set
            if (currentHealth <= 0)
            {
                currentHealth = playerData.maxHealth;
            }

            GameEvents.TriggerPlayerHealthChanged(currentHealth, playerData.maxHealth);
        }
    }

    public void Cleanup()
    {
        //       Debug.Log("PlayerManager: Cleaning up");
        GameEvents.OnPlayerDeath -= HandlePlayerDeath;
    }

    private void Update()
    {
        if (isDead || playerData == null) return;

        // Health regeneration
        if (playerData.healthRegenRate > 0)
        {
            ModifyHealth(playerData.healthRegenRate * Time.deltaTime);
        }
    }

    public void ApplyConsumableEffects(ConsumableData consumable)
    {
        if (consumable == null) return;

        Debug.Log($"Applying consumable effects: {consumable}");
    }

    [Button]
    public void ModifyHealth(float amount)
    {
        if (isDead || playerData == null) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0, playerData.maxHealth);
        GameEvents.TriggerPlayerHealthChanged(currentHealth, playerData.maxHealth);

        Debug.Log("Modifying player health by " + amount);

        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            Debug.Log("Player has died.");
            GameEvents.TriggerPlayerDeath();
        }
    }

    public void Respawn()
    {
        isDead = false;
        currentHealth = playerData.maxHealth;
        GameEvents.TriggerPlayerHealthChanged(currentHealth, playerData.maxHealth);
        Debug.Log("Player has respawned.");
    }

    private void HandlePlayerDeath()
    {
        if (isDead) return;
        Debug.Log("Handling Player Death...");
    }

    private void OnDestroy()
    {
        Cleanup();
    }
}