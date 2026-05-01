using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Singleton registry of all agents in the simulation.
/// Provides faction-based queries: who is ally, who is enemy, who is alive.
/// Every agent registers here on spawn and deregisters on death.
/// 
/// Interview point: "Centralized registry pattern for O(1) faction lookups
/// and multi-agent coordination."
/// </summary>
public class FactionManager : MonoBehaviour
{
    public static FactionManager Instance { get; private set; }

    // All registered agents grouped by faction
    private Dictionary<FactionType, List<AgentIdentity>> factionRegistry 
        = new Dictionary<FactionType, List<AgentIdentity>>();

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize registry for each faction
        foreach (FactionType faction in System.Enum.GetValues(typeof(FactionType)))
        {
            factionRegistry[faction] = new List<AgentIdentity>();
        }
    }

    // =========================================================================
    // REGISTRATION
    // =========================================================================

    public void Register(AgentIdentity agent)
    {
        if (agent == null) return;
        if (!factionRegistry[agent.Faction].Contains(agent))
        {
            factionRegistry[agent.Faction].Add(agent);
            Debug.Log($"[FACTION] Registered {agent.agentName} to {agent.Faction}");
        }
    }

    public void Unregister(AgentIdentity agent)
    {
        if (agent == null) return;
        if (factionRegistry.ContainsKey(agent.Faction))
        {
            factionRegistry[agent.Faction].Remove(agent);
            Debug.Log($"[FACTION] Unregistered {agent.agentName} from {agent.Faction}");
        }
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    /// <summary>
    /// Get all agents belonging to a specific faction.
    /// </summary>
    public List<AgentIdentity> GetFactionMembers(FactionType faction)
    {
        return factionRegistry.ContainsKey(faction) 
            ? factionRegistry[faction] 
            : new List<AgentIdentity>();
    }

    /// <summary>
    /// Get all alive allies of a specific faction (excluding the asking agent).
    /// </summary>
    public List<AgentIdentity> GetAliveAllies(AgentIdentity agent)
    {
        return factionRegistry[agent.Faction]
            .Where(a => a != agent && a.IsAlive)
            .ToList();
    }

    /// <summary>
    /// Get all agents from enemy factions.
    /// </summary>
    public List<AgentIdentity> GetEnemiesOf(FactionType faction)
    {
        List<AgentIdentity> enemies = new List<AgentIdentity>();
        foreach (var kvp in factionRegistry)
        {
            if (kvp.Key != faction)
            {
                enemies.AddRange(kvp.Value);
            }
        }
        return enemies;
    }

    /// <summary>
    /// Get all ALIVE agents from enemy factions.
    /// </summary>
    public List<AgentIdentity> GetAliveEnemiesOf(FactionType faction)
    {
        return GetEnemiesOf(faction).Where(e => e.IsAlive).ToList();
    }

    /// <summary>
    /// Check if two agents are enemies.
    /// </summary>
    public bool AreEnemies(AgentIdentity a, AgentIdentity b)
    {
        if (a == null || b == null) return false;
        return a.Faction != b.Faction;
    }

    /// <summary>
    /// Get count of alive agents per faction. Useful for win condition checks.
    /// </summary>
    public int GetAliveCount(FactionType faction)
    {
        return factionRegistry[faction].Count(a => a.IsAlive);
    }

    /// <summary>
    /// Check if a faction has been eliminated.
    /// </summary>
    public bool IsFactionEliminated(FactionType faction)
    {
        return GetAliveCount(faction) == 0;
    }
}