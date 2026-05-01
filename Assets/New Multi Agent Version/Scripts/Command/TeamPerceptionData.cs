using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Aggregates what one faction collectively perceives.
/// Checks each alive ally's AwarenessModel.currentTarget and awareness level.
/// Used by FogOfWarSystem (Desktop) and TacticalHologram (VR) to ensure
/// both commanders see ONLY what their team has detected.
/// </summary>
public class TeamPerceptionData : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private FactionType trackedFaction = FactionType.Alpha;
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private float memoryDuration = 3f;

    public struct DetectedEnemy
    {
        public AgentIdentity identity;
        public Vector3 lastKnownPosition;
        public float awareness;
        public float timeSinceLastSeen;
        public bool isCurrentlyVisible;
    }

    private Dictionary<AgentIdentity, DetectedEnemy> detectedEnemies = new Dictionary<AgentIdentity, DetectedEnemy>();
    private List<DetectedEnemy> detectedEnemyList = new List<DetectedEnemy>();
    private float updateTimer = 0f;
    private bool initialized = false;

    // Cached references
    private List<AgentIdentity> alliedAgents = new List<AgentIdentity>();
    private List<AgentIdentity> enemyAgents = new List<AgentIdentity>();

    //  Public Access 

    public List<DetectedEnemy> GetDetectedEnemies() => detectedEnemyList;

    public bool IsEnemyDetected(AgentIdentity enemy)
    {
        return detectedEnemies.ContainsKey(enemy) && detectedEnemies[enemy].isCurrentlyVisible;
    }

    public bool IsEnemyInMemory(AgentIdentity enemy)
    {
        return detectedEnemies.ContainsKey(enemy);
    }

    public bool TryGetEnemyData(AgentIdentity enemy, out DetectedEnemy data)
    {
        return detectedEnemies.TryGetValue(enemy, out data);
    }

    public List<AgentIdentity> GetAlliedAgents() => alliedAgents;
    public List<AgentIdentity> GetEnemyAgents() => enemyAgents;

    // Initialization 

    private void Start()
    {
        Invoke(nameof(Initialize), 0.2f);
    }

    private void Initialize()
    {
        if (FactionManager.Instance == null)
        {
            Debug.LogError("[PERCEPTION] FactionManager not found!");
            return;
        }

        alliedAgents.Clear();
        enemyAgents.Clear();

        // All allies (alive + dead — we cache at start)
        foreach (var ally in FactionManager.Instance.GetFactionMembers(trackedFaction))
            alliedAgents.Add(ally);

        // All enemies
        foreach (var enemy in FactionManager.Instance.GetEnemiesOf(trackedFaction))
            enemyAgents.Add(enemy);

        initialized = true;
        Debug.Log($"[PERCEPTION] Tracking {alliedAgents.Count} allies, {enemyAgents.Count} enemies");
    }

    // Update 

    private void Update()
    {
        if (!initialized) return;

        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval) return;
        updateTimer = 0f;

        UpdateDetectedEnemies();
    }

    private void UpdateDetectedEnemies()
    {
        HashSet<AgentIdentity> currentlyVisible = new HashSet<AgentIdentity>();

        // Check each alive ally's currentTarget
        foreach (var ally in alliedAgents)
        {
            if (ally == null) continue;
            if (ally.Combat != null && ally.Combat.IsDead()) continue;

            AwarenessModel awareness = ally.GetComponent<AwarenessModel>();
            if (awareness == null) continue;

            // This ally's current target (single-target tracking)
            if (awareness.currentTarget == null) continue;
            if (awareness.awareness <= 0f) continue;

            // Find which enemy this target belongs to
            AgentIdentity targetIdentity = awareness.currentTarget.GetComponentInParent<AgentIdentity>();
            if (targetIdentity == null) continue;

            // Skip if target is same faction (shouldn't happen, but safety check)
            if (targetIdentity.Faction == trackedFaction) continue;

            // Skip if target is dead
            if (targetIdentity.Combat != null && targetIdentity.Combat.IsDead()) continue;

            currentlyVisible.Add(targetIdentity);

            // Update or add detection entry
            if (detectedEnemies.ContainsKey(targetIdentity))
            {
                var existing = detectedEnemies[targetIdentity];
                detectedEnemies[targetIdentity] = new DetectedEnemy
                {
                    identity = targetIdentity,
                    lastKnownPosition = targetIdentity.transform.position,
                    awareness = Mathf.Max(existing.awareness, awareness.awareness),
                    timeSinceLastSeen = 0f,
                    isCurrentlyVisible = true
                };
            }
            else
            {
                detectedEnemies[targetIdentity] = new DetectedEnemy
                {
                    identity = targetIdentity,
                    lastKnownPosition = targetIdentity.transform.position,
                    awareness = awareness.awareness,
                    timeSinceLastSeen = 0f,
                    isCurrentlyVisible = true
                };
            }
        }

        // Age out enemies that are no longer visible
        List<AgentIdentity> toRemove = new List<AgentIdentity>();
        List<AgentIdentity> keys = new List<AgentIdentity>(detectedEnemies.Keys);

        foreach (var key in keys)
        {
            if (!currentlyVisible.Contains(key))
            {
                var data = detectedEnemies[key];
                float newTime = data.timeSinceLastSeen + updateInterval;

                if (newTime >= memoryDuration)
                {
                    toRemove.Add(key);
                }
                else
                {
                    detectedEnemies[key] = new DetectedEnemy
                    {
                        identity = data.identity,
                        lastKnownPosition = data.lastKnownPosition,
                        awareness = data.awareness * 0.8f,
                        timeSinceLastSeen = newTime,
                        isCurrentlyVisible = false
                    };
                }
            }
        }

        foreach (var key in toRemove)
            detectedEnemies.Remove(key);

        // Rebuild flat list
        detectedEnemyList.Clear();
        foreach (var kvp in detectedEnemies)
            detectedEnemyList.Add(kvp.Value);
    }

    // Reset 

    public void ResetPerception()
    {
        detectedEnemies.Clear();
        detectedEnemyList.Clear();
        initialized = false;
        Invoke(nameof(Initialize), 0.2f);
    }
}