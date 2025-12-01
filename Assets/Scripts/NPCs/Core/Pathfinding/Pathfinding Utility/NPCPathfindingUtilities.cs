using UnityEngine;
using Infohazard.HyperNav;
using Sirenix.OdinInspector;


public class NPCPathfindingUtilities : MonoBehaviour
{
    public static NPCPathfindingUtilities Instance { get; private set; }

    [SerializeField] private NavVolume[] navVolumes;

    [SerializeField] private Transform playerTransform;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public Vector3 GetRandomValidPositionToMoveTo(Vector3 agentPos)
    {
        NavVolume closestVolume = GetClosestVolume(agentPos);

        if (closestVolume == null)
            return Vector3.zero;

        int randomIndex = Random.Range(0, closestVolume.Data.Regions.Count - 1);

        Debug.Log($"Patrol Position set to {closestVolume.Data.Regions[randomIndex].Bounds.center}");

        return closestVolume.Data.Regions[randomIndex].Bounds.center;

    }

    private NavVolume GetClosestVolume(Vector3 position)
    {
        NavVolume closest = navVolumes[0];

        float closestDistance = float.MaxValue;

        foreach (NavVolume volume in navVolumes)
        {
            float distance = Vector3.Distance(position, volume.Bounds.center);
            if (distance < closestDistance)
            {
                closest = volume;
            }
        }

        return closest;
    }

    [Button]
    public void FindVolumes()
    {
        navVolumes = FindObjectsByType<NavVolume>(FindObjectsSortMode.None);
    }

}
