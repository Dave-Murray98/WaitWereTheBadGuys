using UnityEngine;

public class MonsterAnimationHandler : MonoBehaviour
{
    public Animator animator;


    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }



    public void TriggerAnimation(string triggerName)
    {
        if (animator != null)
        {
            animator.SetTrigger(triggerName);
        }
    }
}
