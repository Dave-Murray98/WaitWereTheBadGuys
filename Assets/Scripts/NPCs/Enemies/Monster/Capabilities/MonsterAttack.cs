using UnityEngine;

public class MonsterAttack : MonoBehaviour
{
    [SerializeField] private MonsterAnimationHandler animationHandler;
    [SerializeField] private bool enableDebugLogs = false;


    public void PerformAttack()
    {
        // Placeholder for bite logic
        DebugLog("Attacking");
        animationHandler.PlayAttackAnimation();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MonsterAttack] {message}");
        }
    }
}
