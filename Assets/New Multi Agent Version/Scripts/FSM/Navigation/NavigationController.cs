using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Single point of control for all agent movement.
/// Wraps NavMeshAgent + NavMeshObstacle (dynamic carving when idle).
/// States call this instead of touching NavMeshAgent directly.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NavigationController : MonoBehaviour
{
    [Header("Speed Profiles")]
    public float patrolSpeed = 2.0f;
    public float alertSpeed = 3.5f;
    public float engageSpeed = 4.5f;

    [Header("Stopping")]
    public float defaultStoppingDistance = 0.5f;
    public float engageStoppingDistance = 8f;

    [Header("Dynamic Obstacle Avoidance")]
    [Tooltip("When idle, agent becomes a NavMesh obstacle so others path around it")]
    public bool useObstacleCarving = true;
    public float carvingDelay = 0.5f;

    [Header("Stuck Detection")]
    public float stuckCheckInterval = 2f;
    public float stuckDistanceThreshold = 0.5f;

    private Vector3 lastStuckCheckPosition;
    private float stuckCheckTimer = 0f;
    private int stuckCount = 0;

    // References
    [HideInInspector] public NavMeshAgent Agent;
    private NavMeshObstacle obstacle;
    private Coroutine carvingCoroutine;
    private bool isCarving = false;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        obstacle = GetComponent<NavMeshObstacle>();

        // Ensure agent starts enabled, obstacle starts disabled
        Agent.enabled = true;
        if (obstacle != null) obstacle.enabled = false;
    }

    // =========================================================================
    // MOVEMENT COMMANDS
    // =========================================================================

    /// <summary>
    /// Move to a world position at specified speed.
    /// </summary>
    public void MoveTo(Vector3 destination, float speed)
    {
        StopCarving();
        EnsureAgentActive();

        Agent.isStopped = false;  // <-- ADD THIS LINE
        Agent.speed = speed;
        Agent.stoppingDistance = defaultStoppingDistance;
        Agent.SetDestination(destination);
    }

    /// <summary>
    /// Move to position using patrol speed.
    /// </summary>
    public void PatrolTo(Vector3 destination)
    {
        MoveTo(destination, patrolSpeed);
    }

    /// <summary>
    /// Move to position using alert/investigation speed.
    /// </summary>
    public void InvestigateTo(Vector3 destination)
    {
        MoveTo(destination, alertSpeed);
    }

    /// <summary>
    /// Pursue a moving target. Keeps updating destination.
    /// Call this every frame in EngageState.
    /// </summary>
    public void Pursue(Transform target, float stoppingDistance)
    {
        if (target == null) return;

        StopCarving();
        EnsureAgentActive();

        Agent.isStopped = false;  // <-- ADD THIS LINE
        Agent.speed = engageSpeed;
        Agent.stoppingDistance = stoppingDistance;
        Agent.SetDestination(target.position);
    }

    /// <summary>
    /// Stop all movement immediately.
    /// Optionally starts carving (becomes obstacle for other agents).
    /// </summary>
    public void Stop()
    {
        if (Agent.enabled && Agent.isOnNavMesh)
        {
            Agent.ResetPath();
            Agent.velocity = Vector3.zero;
        }

        if (useObstacleCarving)
            StartCarving();
    }

    /// <summary>
    /// Full shutdown — used when agent dies.
    /// </summary>
    
    public void DisableCompletely()
    {
        StopCarving();
        if (Agent.enabled && Agent.isOnNavMesh)
        {
            Agent.ResetPath();
            Agent.velocity = Vector3.zero;
            Agent.isStopped = true;
        }
        Agent.enabled = false;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    public bool HasReachedDestination()
    {
        if (!Agent.enabled || !Agent.isOnNavMesh) return true;
        if (Agent.pathPending) return false;
        return Agent.remainingDistance <= Agent.stoppingDistance;
    }

    public bool HasPath()
    {
        return Agent.enabled && Agent.hasPath;
    }

    public float DistanceTo(Vector3 position)
    {
        return Vector3.Distance(transform.position, position);
    }

    public float DistanceTo(Transform target)
    {
        return target != null ? Vector3.Distance(transform.position, target.position) : Mathf.Infinity;
    }

    // =========================================================================
    // ROTATION
    // =========================================================================

    /// <summary>
    /// Smoothly rotate toward a world position. Used in Alert/Engage.
    /// </summary>
    public void LookAt(Vector3 position, float rotationSpeed = 5f)
    {
        Vector3 direction = (position - transform.position).normalized;
        direction.y = 0;
        if (direction == Vector3.zero) return;

        Quaternion targetRot = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
    }

    /// <summary>
    /// Smoothly rotate toward a target transform.
    /// </summary>
    public void LookAt(Transform target, float rotationSpeed = 5f)
    {
        if (target != null)
            LookAt(target.position, rotationSpeed);
    }

    // =========================================================================
    // DYNAMIC OBSTACLE CARVING
    // When an agent stops (waiting during patrol, investigating),
    // it becomes a NavMesh obstacle so OTHER agents path around it.
    // This is "dynamic obstacle avoidance" for portfolio/interview.
    // =========================================================================

    private void StartCarving()
    {
        if (!useObstacleCarving || obstacle == null || isCarving) return;
        if (carvingCoroutine != null) StopCoroutine(carvingCoroutine);
        carvingCoroutine = StartCoroutine(CarvingCoroutine());
    }

    private void StopCarving()
    {
        if (carvingCoroutine != null)
        {
            StopCoroutine(carvingCoroutine);
            carvingCoroutine = null;
        }

        if (isCarving)
        {
            isCarving = false;
            if (obstacle != null) obstacle.enabled = false;
            StartCoroutine(ReenableAgent());
        }
    }

    private IEnumerator CarvingCoroutine()
    {
        yield return new WaitForSeconds(carvingDelay);

        // Disable agent, enable obstacle
        Agent.enabled = false;
        if (obstacle != null) obstacle.enabled = true;
        isCarving = true;
    }

    private IEnumerator ReenableAgent()
    {
        if (obstacle != null) obstacle.enabled = false;
        yield return null; // Wait one frame for NavMesh to update
        Agent.enabled = true;
    }

    private void EnsureAgentActive()
    {
        if (!Agent.enabled)
        {
            if (obstacle != null) obstacle.enabled = false;
            Agent.enabled = true;
        }
    }

    
    private void Update()
    {
        CheckIfStuck();
    }

    /// <summary>
    /// Detects if agent is stuck (not moving but has a path).
    /// If stuck, cancels current path so PatrolState picks a new destination.
    /// </summary>
    private void CheckIfStuck()
    {
        if (!Agent.enabled || !Agent.isOnNavMesh || isCarving) return;
        if (!Agent.hasPath) return;

        stuckCheckTimer += Time.deltaTime;
        if (stuckCheckTimer < stuckCheckInterval) return;

        stuckCheckTimer = 0f;
        float movedDistance = Vector3.Distance(transform.position, lastStuckCheckPosition);

        if (movedDistance < stuckDistanceThreshold && Agent.hasPath)
        {
            stuckCount++;
            if (stuckCount >= 2)
            {
                // Agent is stuck — clear path
                Debug.Log($"[NAV] {gameObject.name} stuck. Clearing path.");
                Agent.ResetPath();
                stuckCount = 0;
            }
        }
        else
        {
            stuckCount = 0;
        }

        lastStuckCheckPosition = transform.position;
    }

        /// <summary>
    /// Immediately stop movement with zero velocity. 
    /// Used when entering combat to prevent animation delay.
    /// </summary>
    public void StopImmediate()
    {
        if (Agent.enabled && Agent.isOnNavMesh)
        {
            Agent.ResetPath();
            Agent.velocity = Vector3.zero;
            Agent.isStopped = true;
        }

        // Force the agent speed to zero for animator
        Agent.speed = 0f;
    }
}