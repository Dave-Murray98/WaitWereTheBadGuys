using UnityEngine;

public class PositionDebugger : MonoBehaviour
{
    //anytime the position changes, log it
    private Vector3 lastPosition;
    private void Update()
    {
        if (transform.position != lastPosition)
        {
            Debug.Log($"[PositionDebugger] {gameObject.name} moved from {lastPosition} to {transform.position}");
            lastPosition = transform.position;
        }
    }
}
