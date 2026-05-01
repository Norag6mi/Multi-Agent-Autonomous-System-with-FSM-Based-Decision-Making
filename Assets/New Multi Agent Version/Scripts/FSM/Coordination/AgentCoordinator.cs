using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles inter-agent communication within the same faction.
/// When one agent detects a threat, it broadcasts to nearby allies.
/// Allies receive awareness boosts and last known enemy position.
/// 
/// Interview point: "Simulates radio communication between agents.
/// Creates emergent coordinated behavior — allies converge on threats
/// instead of each agent operating in isolation."
/// </summary>

public class AgentCoordinator : MonoBehaviour
{

    // Add near the top of AgentCoordinator class:
    public static event System.Action<AgentIdentity, Vector3, List<AgentIdentity>> OnThreatBroadcast;

    public static AgentCoordinator Instance { get; private set; }

    [Header("Communication Settings")]
    [Tooltip("Maximum range for ally-to-ally threat sharing")]
    public float communicationRange = 25f;

    [Tooltip("Awareness boost allies receive when threat is shared")]
    public float allyAwarenessBoost = 20f;

    [Tooltip("Minimum time between broadcasts from same agent (prevents spam)")]
    public float broadcastCooldown = 2f;

    // Track last broadcast time per agent to prevent spam
    private Dictionary<AgentIdentity, float> lastBroadcastTime 
        = new Dictionary<AgentIdentity, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // =========================================================================
    // THREAT BROADCASTING
    // =========================================================================

    /// <summary>
    /// Called when an agent's awareness crosses the alert threshold.
    /// Broadcasts threat info to all allies within communication range.
    /// </summary>
    /// <param name="broadcaster">The agent that detected the threat</param>
    /// <param name="threatPosition">World position of the detected enemy</param>
    /// 
    public void BroadcastThreat(AgentIdentity broadcaster, Vector3 threatPosition)
    {
        if (broadcaster == null) return;
        if (FactionManager.Instance == null) return;

        // Check cooldown
        if (!CanBroadcast(broadcaster)) return;
        lastBroadcastTime[broadcaster] = Time.time;

        // Get alive allies
        List<AgentIdentity> allies = FactionManager.Instance.GetAliveAllies(broadcaster);

        int alliesNotified = 0;
        foreach (AgentIdentity ally in allies)
        {
            // Check if ally is within communication range
            float distance = Vector3.Distance(
                broadcaster.transform.position, 
                ally.transform.position
            );

            if (distance > communicationRange) continue;

            // Boost ally's awareness and share threat position
            if (ally.Awareness != null)
            {
                ally.Awareness.ReceiveAllyThreatInfo(threatPosition, allyAwarenessBoost);
                alliesNotified++;
            }
        }

        // Add right before the "if (alliesNotified > 0)" debug log:
        List<AgentIdentity> notifiedAllies = allies.FindAll(a => 
        Vector3.Distance(broadcaster.transform.position, a.transform.position) <= communicationRange 
        && a.Awareness != null);
        OnThreatBroadcast?.Invoke(broadcaster, threatPosition, notifiedAllies);


        if (alliesNotified > 0)
        {
            Debug.Log($"[COORD] {broadcaster.agentName} broadcast threat. " +
                      $"{alliesNotified} allies notified.");
        }
    }

    /// <summary>
    /// Called when an agent takes damage but doesn't know where from.
    /// Requests nearby allies to share their target info.
    /// </summary>
    public void RequestTargetInfo(AgentIdentity requester)
    {
        if (requester == null || FactionManager.Instance == null) return;

        List<AgentIdentity> allies = FactionManager.Instance.GetAliveAllies(requester);

        foreach (AgentIdentity ally in allies)
        {
            float distance = Vector3.Distance(
                requester.transform.position, 
                ally.transform.position
            );

            if (distance > communicationRange) continue;

            // If ally has a target, share it
            if (ally.Awareness != null && ally.Awareness.currentTarget != null)
            {
                requester.Awareness.ReceiveAllyThreatInfo(
                    ally.Awareness.lastKnownPosition,
                    allyAwarenessBoost
                );

                Debug.Log($"[COORD] {ally.agentName} shared target info with {requester.agentName}");
                return; // One source is enough
            }
        }
    }

    /// <summary>
    /// Find the nearest alive enemy to a given position.
    /// Used by agents who received threat info but don't have a direct target.
    /// </summary>
    public Transform FindNearestEnemy(AgentIdentity agent)
    {
        if (FactionManager.Instance == null) return null;

        List<AgentIdentity> enemies = FactionManager.Instance.GetAliveEnemiesOf(agent.Faction);

        float closestDist = Mathf.Infinity;
        Transform closest = null;

        foreach (AgentIdentity enemy in enemies)
        {
            float dist = Vector3.Distance(agent.transform.position, enemy.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = enemy.transform;
            }
        }

        return closest;
    }

    // =========================================================================
    // INTERNAL
    // =========================================================================

    private bool CanBroadcast(AgentIdentity agent)
    {
        if (!lastBroadcastTime.ContainsKey(agent)) return true;
        return Time.time - lastBroadcastTime[agent] >= broadcastCooldown;
    }

    // Editor visualization of communication range
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, communicationRange);
    }
}