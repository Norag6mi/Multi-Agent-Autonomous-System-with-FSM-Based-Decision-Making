using UnityEngine;

public class ObjectiveDiscovery : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float discoveryRadius = 10f;
    [SerializeField] private bool debugLog = false;

    private MissionObjective objective;
    private bool isDiscovered = false;
    private Transform playerTransform;

    public bool IsDiscovered => isDiscovered;

    public static event System.Action<ObjectiveDiscovery> OnObjectiveDiscovered;

    private void Awake()
    {
        objective = GetComponent<MissionObjective>();
    }

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
    }

    private void Update()
    {
        if (isDiscovered) return;
        if (objective != null && objective.IsComplete) return;

        CheckDiscovery();
    }

    private void CheckDiscovery()
    {
        // Check player distance
        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= discoveryRadius)
            {
                Discover("Player");
                return;
            }
        }

        // Check all Alpha agents
        FactionManager fm = FactionManager.Instance;
        if (fm == null) return;

        foreach (AgentIdentity agent in fm.GetFactionMembers(FactionType.Alpha))
        {
            if (agent == null) return;
            if (!agent.IsAlive) continue;

            float dist = Vector3.Distance(transform.position, agent.transform.position);
            if (dist <= discoveryRadius)
            {
                Discover(agent.agentName);
                return;
            }
        }
    }

    private void Discover(string discoveredBy)
    {
        isDiscovered = true;

        if (debugLog)
            Debug.Log($"[ObjectiveDiscovery] {objective?.ObjectiveName} " +
                      $"discovered by {discoveredBy}.");

        OnObjectiveDiscovered?.Invoke(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawSphere(transform.position, discoveryRadius);
        Gizmos.color = new Color(0f, 1f, 1f, 1f);
        Gizmos.DrawWireSphere(transform.position, discoveryRadius);
    }
}