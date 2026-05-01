// Assets/Scripts/Research/CommandQualityAnalyzer.cs
using UnityEngine;
using System.Collections.Generic;

public class CommandQualityAnalyzer : MonoBehaviour
{
    private struct TrackedCommand
    {
        public string agentName;
        public Vector3 targetPosition;
        public float issueTime;
        public bool wasRelevant; // agent was in Patrol when issued
        public bool ledToEngagement; // detection within 30s
        public bool resolved;
    }

    private List<TrackedCommand> pendingCommands = new List<TrackedCommand>();
    private int totalCommands = 0;
    private int relevantCommands = 0;
    private int effectiveCommands = 0;
    private List<float> interCommandIntervals = new List<float>();
    private float lastCommandTime = -1f;

    private void OnEnable()
    {
        CommandReceiver.OnCommandReceived += HandleCommand;
    }

    private void OnDisable()
    {
        CommandReceiver.OnCommandReceived -= HandleCommand;
    }

    private void HandleCommand(string agentName, CommandReceiver.CommandType type,
        Vector3 pos, AgentState stateAtReceipt, CommandReceiver.CommandStatus status)
    {
        if (type == CommandReceiver.CommandType.None) return;

        totalCommands++;

        bool relevant = (stateAtReceipt == AgentState.Patrol);
        if (relevant) relevantCommands++;

        // Track inter-command interval
        if (lastCommandTime > 0f)
            interCommandIntervals.Add(Time.time - lastCommandTime);
        lastCommandTime = Time.time;

        // Track move/regroup commands for effectiveness
        if (type == CommandReceiver.CommandType.MoveTo || type == CommandReceiver.CommandType.Regroup)
        {
            pendingCommands.Add(new TrackedCommand
            {
                agentName = agentName,
                targetPosition = pos,
                issueTime = Time.time,
                wasRelevant = relevant,
                ledToEngagement = false,
                resolved = false
            });
        }
    }

    private void Update()
    {
        // Check if pending commands led to engagement within 30 seconds
        for (int i = pendingCommands.Count - 1; i >= 0; i--)
        {
            var cmd = pendingCommands[i];
            if (cmd.resolved) continue;

            float elapsed = Time.time - cmd.issueTime;

            if (elapsed > 30f)
            {
                cmd.resolved = true;
                pendingCommands[i] = cmd;
                continue;
            }

            // Check if agent is now in Engage state
            AgentIdentity agent = FindAgent(cmd.agentName);
            if (agent != null)
            {
                AgentFSM fsm = agent.GetComponent<AgentFSM>();
                if (fsm != null && fsm.CurrentStateKey == AgentState.Engage)
                {
                    cmd.ledToEngagement = true;
                    cmd.resolved = true;
                    pendingCommands[i] = cmd;
                    effectiveCommands++;
                }
            }
        }
    }

    public float GetRelevanceRate()
    {
        return totalCommands > 0 ? (float)relevantCommands / totalCommands * 100f : 0f;
    }

    public float GetEffectivenessRate()
    {
        int positionCommands = 0;
        foreach (var cmd in pendingCommands) positionCommands++;
        return positionCommands > 0 ? (float)effectiveCommands / positionCommands * 100f : 0f;
    }

    public float GetAverageInterCommandInterval()
    {
        if (interCommandIntervals.Count == 0) return 0f;
        float sum = 0f;
        foreach (float v in interCommandIntervals) sum += v;
        return sum / interCommandIntervals.Count;
    }

    public void ResetAnalyzer()
    {
        pendingCommands.Clear();
        totalCommands = 0;
        relevantCommands = 0;
        effectiveCommands = 0;
        interCommandIntervals.Clear();
        lastCommandTime = -1f;
    }

    private AgentIdentity FindAgent(string name)
    {
        foreach (var a in FactionManager.Instance.GetFactionMembers(FactionType.Alpha))
            if (a.agentName == name) return a;
        return null;
    }
}