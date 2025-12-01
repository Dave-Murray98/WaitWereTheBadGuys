using UnityEngine;

public class PathfindingTargetTest : MonoBehaviour
{
    [SerializeField] UnderwaterMonsterController controller;

    private void Update()
    {
        transform.position = controller.targetPosition;
    }
}
