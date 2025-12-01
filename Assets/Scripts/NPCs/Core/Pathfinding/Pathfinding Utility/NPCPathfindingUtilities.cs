using UnityEngine;
using Infohazard.HyperNav;
using Sirenix.OdinInspector;


public class NPCPathfindingUtilities : MonoBehaviour
{
    public static NPCPathfindingUtilities Instance { get; private set; }

    [SerializeField] private NavVolume[] navVolumes;

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

    // public Vector3 GetRandomValidPosition(SplineNavAgent agent)
    // {
    //     if (agent == null)
    //         return Vector3.zero;

    //     // Get the last waypoint
    //     int furthestWaypointIndex = agent.CurrentPath.Waypoints.Count - 1;
    //     NavWaypoint furthestWaypoint = agent.CurrentPath.Waypoints[furthestWaypointIndex];

    //     if (furthestWaypoint.IsVolume)
    //         return furthestWaypoint.Position;
    //     else
    //     {
    //         while (furthestWaypointIndex > 0)
    //         {
    //             furthestWaypointIndex--;
    //             furthestWaypoint = agent.CurrentPath.Waypoints[furthestWaypointIndex];
    //             if (furthestWaypoint.IsVolume)
    //                 return furthestWaypoint.Position;
    //         }
    //     }

    //     return Vector3.zero;
    // }

    public Vector3 GetRandomValidPositionToMoveTo(Vector3 agentPos, float radius = 10f)
    {
        NavVolume closestVolume = GetClosestVolume(agentPos);

        if (closestVolume == null)
            return Vector3.zero;

        // Draw a sphere around the agent's radius and pick a random position from within it 
        // that's valid (ie in the nav volume, and not in a wall)
        Vector3 randomPosition = Random.insideUnitSphere * radius;
        randomPosition += agentPos;

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
