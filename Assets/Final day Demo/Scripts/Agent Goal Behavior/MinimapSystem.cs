using UnityEngine;
using System.Collections.Generic;

public class MinimapSystem : MonoBehaviour
{
    public static MinimapSystem Instance { get; private set; }

    // Discovered objectives — minimap reads this list
    private HashSet<MissionObjective> discoveredObjectives 
        = new HashSet<MissionObjective>();

    // Detected enemies — minimap reads this list
    private Dictionary<AgentIdentity, float> detectedEnemies 
        = new Dictionary<AgentIdentity, float>();

    [SerializeField] private float enemyFadeTime = 3f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        ObjectiveDiscovery.OnObjectiveDiscovered += HandleObjectiveDiscovered;
    }

    private void OnDisable()
    {
        ObjectiveDiscovery.OnObjectiveDiscovered -= HandleObjectiveDiscovered;
    }

    private void Update()
    {
        CleanStaleEnemies();
    }

    // ── Objectives ───────────────────────────────────────

    private void HandleObjectiveDiscovered(ObjectiveDiscovery discovery)
    {
        MissionObjective obj = discovery.GetComponent<MissionObjective>();
        if (obj != null)
            discoveredObjectives.Add(obj);
    }

    public bool IsObjectiveDiscovered(MissionObjective objective)
    {
        return discoveredObjectives.Contains(objective);
    }

    // ── Enemies ──────────────────────────────────────────

    public void RegisterEnemyDetection(AgentIdentity enemy)
    {
        if (enemy == null) return;
        detectedEnemies[enemy] = Time.time;
    }

    public bool IsEnemyVisible(AgentIdentity enemy)
    {
        if (!detectedEnemies.ContainsKey(enemy)) return false;
        return Time.time - detectedEnemies[enemy] <= enemyFadeTime;
    }

    public Dictionary<AgentIdentity, float> GetDetectedEnemies()
    {
        return detectedEnemies;
    }

    private void CleanStaleEnemies()
    {
        List<AgentIdentity> toRemove = new List<AgentIdentity>();
        foreach (var kvp in detectedEnemies)
        {
            if (Time.time - kvp.Value > enemyFadeTime)
                toRemove.Add(kvp.Key);
        }
        foreach (var key in toRemove)
            detectedEnemies.Remove(key);
    }

    public HashSet<MissionObjective> GetDiscoveredObjectives()
    {
        return discoveredObjectives;
    }
}