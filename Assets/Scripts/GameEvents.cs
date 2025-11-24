using System;
using UnityEngine;

public static class GameEvents
{

    #region Player Events

    public static event Action<float, float> OnPlayerHealthChanged;
    public static event Action OnPlayerDeath;

    #endregion


    #region Game State Events
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;

    #endregion

    #region UI Events
    public static event Action OnInventoryOpened;
    public static event Action OnInventoryClosed;
    #endregion


    #region Trigger Methods
    public static void TriggerPlayerHealthChanged(float currentHealth, float maxHealth) =>
        OnPlayerHealthChanged?.Invoke(currentHealth, maxHealth);

    public static void TriggerPlayerDeath() => OnPlayerDeath?.Invoke();

    public static void TriggerGamePaused() => OnGamePaused?.Invoke();

    public static void TriggerGameResumed() => OnGameResumed?.Invoke();

    public static void TriggerInventoryOpened() => OnInventoryOpened?.Invoke();
    public static void TriggerInventoryClosed() => OnInventoryClosed?.Invoke();

    #endregion

}
