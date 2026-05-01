using UnityEngine;
using UnityEngine.AI;

public class PatrolRoute : MonoBehaviour
{
    public enum PatrolMode { AreaRandom, Waypoints }

    [Header("Patrol Mode")]
    public PatrolMode mode = PatrolMode.AreaRandom;

    [Header("Area Mode")]
    public Area patrolArea;

    [Header("Waypoint Mode")]
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;

    [Header("Wait Time")]
    public float minWaitTime = 1f;
    public float maxWaitTime = 4f;

    // Command override state
    private bool hasCommandOverride = false;
    private Vector3 commandOverrideDestination;
    private bool isCommandDestinationUsed = false;
    private bool isHoldMode = false;

    public bool IsHolding => isHoldMode;
    public bool HasCommandOverride => hasCommandOverride;

    // Goal Behaviour
    public delegate void DestinationRequestedHandler(PatrolRoute requester, ref Vector3 destination);
    public static event DestinationRequestedHandler OnDestinationRequested;

    // Centered weight

    [Header("Smart Patrol")]
    [SerializeField] private float centerBiasChance = 0.6f;
    [SerializeField] private Transform mapCenter;


    public Vector3 GetNextDestination()
    {
        if (isHoldMode)
            return transform.position;

        if (hasCommandOverride && !isCommandDestinationUsed)
        {
            isCommandDestinationUsed = true;
            return commandOverrideDestination;
        }

        if (hasCommandOverride && isCommandDestinationUsed)
            return GetRandomPointNear(commandOverrideDestination, 10f);

        // Normal autonomous patrol — this is where we bias
        Vector3 destination = mode switch
        {
            PatrolMode.AreaRandom => GetRandomAreaPoint(),
            PatrolMode.Waypoints => GetNextWaypoint(),
            _ => transform.position
        };

        // Fire event — AgentGoalBehavior can modify destination here
        // Only fires during normal patrol (not hold, not command override)
        OnDestinationRequested?.Invoke(this, ref destination);

        return destination;
    }

    public float GetRandomWaitTime()
    {
        return Random.Range(minWaitTime, maxWaitTime);
    }

    //  Command Integration 

    public void SetCommandOverride(Vector3 destination)
    {
        hasCommandOverride = true;
        commandOverrideDestination = destination;
        isCommandDestinationUsed = false;
        isHoldMode = false;
    }

    public void ClearCommandOverride()
    {
        hasCommandOverride = false;
        isCommandDestinationUsed = false;
        isHoldMode = false;
    }

    public void SetHoldMode(bool hold)
    {
        isHoldMode = hold;
        if (hold) hasCommandOverride = false;
    }

public void ResetCommandState()
    {
        hasCommandOverride = false;
        commandOverrideDestination = Vector3.zero;
        isCommandDestinationUsed = false;
        isHoldMode = false;
        currentWaypointIndex = 0;
    }

    // Private 

    // Replace GetRandomAreaPoint with:
    private Vector3 GetRandomAreaPoint()
    {
        if (patrolArea == null)
        {
            Debug.LogWarning($"[PATROL] {gameObject.name}: No patrol area assigned!");
            return transform.position;
        }

        // Bias toward center of map for more natural movement
        if (mapCenter != null && Random.value < centerBiasChance)
        {
            // Random point in a large area around center (covers corridors too)
            return GetRandomPointNear(mapCenter.position, 35f);
        }

        return patrolArea.GetRandomPoint();
    }

    private Vector3 GetNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
            return transform.position;

        Vector3 point = waypoints[currentWaypointIndex].position;
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        return point;
    }

    private Vector3 GetRandomPointNear(Vector3 center, float radius)
    {
        Vector3 randomDir = Random.insideUnitSphere * radius;
        randomDir.y = 0f;

        if (NavMesh.SamplePosition(center + randomDir, out NavMeshHit hit, radius, NavMesh.AllAreas))
            return hit.position;

        return center;
    }
}