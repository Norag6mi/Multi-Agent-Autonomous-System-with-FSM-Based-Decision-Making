using UnityEngine;
using UnityEngine.AI;

public class AgentGoalBehavior : MonoBehaviour
{
    [Header("Search Settings")]
    [SerializeField] private float baseSearchRadius = 25f;
    [SerializeField] private float radiusExpansionPerSecond = 0.2f;
    [SerializeField] private float objectiveBiasWeight = 0.35f;
    [SerializeField] private float destinationUpdateInterval = 8f;
    [SerializeField] private bool debugLog = false;

    private AgentFSM fsm;
    private PatrolRoute patrolRoute;
    private float currentSearchRadius;
    private float updateTimer = 0f;
    private bool searchEnabled = false;
    private AgentState lastState = AgentState.Patrol;
    private float postCombatTimer = 0f;

    private void Awake()
    {
        fsm = GetComponent<AgentFSM>();
        patrolRoute = GetComponent<PatrolRoute>();
        currentSearchRadius = baseSearchRadius;
    }

    private void OnEnable()
    {
        MissionManager_Demo.OnMissionStart += StartSearch;
        MissionManager_Demo.OnMissionSuccess += StopSearch;
        MissionManager_Demo.OnMissionFailed += StopSearch;
    }

    private void OnDisable()
    {
        MissionManager_Demo.OnMissionStart -= StartSearch;
        MissionManager_Demo.OnMissionSuccess -= StopSearch;
        MissionManager_Demo.OnMissionFailed -= StopSearch;
    }

    private void Update()
    {
        if (!searchEnabled || fsm == null || patrolRoute == null) return;

        // Expand search area over time
        currentSearchRadius += radiusExpansionPerSecond * Time.deltaTime;

        AgentState current = fsm.CurrentStateKey;

        // Detect state changes
        if (current != lastState)
        {
            HandleStateChange(lastState, current);
            lastState = current;
        }

        // Only pick destinations while patrolling
        if (current != AgentState.Patrol) return;

        // Post-combat investigation cooldown
        if (postCombatTimer > 0f)
        {
            postCombatTimer -= Time.deltaTime;
            return;
        }

        // Respect hold mode and human commands
        if (patrolRoute.IsHolding) return;
        if (patrolRoute.HasCommandOverride && !WeSetOverride()) return;

        updateTimer += Time.deltaTime;
        if (updateTimer >= destinationUpdateInterval)
        {
            updateTimer = 0f;
            PickSearchDestination();
        }
    }

    private void HandleStateChange(AgentState from, AgentState to)
    {
        if (to == AgentState.Engage || to == AgentState.Alert)
        {
            // Combat started — clear our override immediately
            patrolRoute.ClearCommandOverride();
            updateTimer = 0f;
        }

        if (to == AgentState.Patrol && (from == AgentState.Engage || from == AgentState.Alert))
        {
            // Returned from combat — let FSM investigate naturally first
            postCombatTimer = 4f;
        }

        if (to == AgentState.Dead)
        {
            searchEnabled = false;
            patrolRoute.ClearCommandOverride();
        }
    }

    private void PickSearchDestination()
    {
        Vector3 origin = transform.position;

        // Base random direction
        Vector3 randomDir = Random.insideUnitSphere * currentSearchRadius;
        randomDir.y = 0f;
        Vector3 target = origin + randomDir;

        // Apply gentle pull toward nearest undiscovered objective
        MissionObjective nearestUndiscovered = GetNearestUndiscoveredObjective();
        if (nearestUndiscovered != null)
        {
            Vector3 toObj = (nearestUndiscovered.transform.position - origin).normalized;
            float biasDist = currentSearchRadius * objectiveBiasWeight;
            target += toObj * biasDist;
        }

        // Ensure point is walkable
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, currentSearchRadius, NavMesh.AllAreas))
        {
            patrolRoute.SetCommandOverride(hit.position);

            if (debugLog)
                Debug.Log($"[Search] {gameObject.name} → {hit.position:F1} " +
                          $"(Radius: {currentSearchRadius:F0}, Bias: {objectiveBiasWeight * 100:F0}%)");
        }
    }

    private MissionObjective GetNearestUndiscoveredObjective()
    {
        if (MissionManager_Demo.Instance == null) return null;
        var objectives = MissionManager_Demo.Instance.GetObjectives();
        if (objectives == null) return null;

        MissionObjective nearest = null;
        float nearestDist = Mathf.Infinity;

        foreach (var obj in objectives)
        {
            if (obj == null) continue;
            var discovery = obj.GetComponent<ObjectiveDiscovery>();
            if (discovery != null && discovery.IsDiscovered) continue;

            float dist = Vector3.Distance(transform.position, obj.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = obj;
            }
        }
        return nearest;
    }

    // We track if WE set the current override by checking timing
    // Simple heuristic: if override exists and we're in search mode, it's ours
    private bool WeSetOverride()
    {
        return searchEnabled && patrolRoute.HasCommandOverride;
    }

    private void StartSearch()
    {
        searchEnabled = true;
        currentSearchRadius = baseSearchRadius;
        updateTimer = destinationUpdateInterval;
        lastState = fsm != null ? fsm.CurrentStateKey : AgentState.Patrol;

        if (debugLog)
            Debug.Log($"[Search] {gameObject.name} search activated.");
    }

    private void StopSearch()
    {
        searchEnabled = false;
        patrolRoute.ClearCommandOverride();
    }
}