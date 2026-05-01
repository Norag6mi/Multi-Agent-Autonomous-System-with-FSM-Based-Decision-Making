// Assets/Scripts/Coordination/AgentStatusReporter.cs
using UnityEngine;
using System;
using System.Collections.Generic;

// Generates status reports from agents based on state transitions and events.
// Reports are consumed by UI systems (desktop notifications / VR floating text).
public class AgentStatusReporter : MonoBehaviour
{
    public enum ReportType
    {
        EnemySpotted,
        EnemyLost,
        TakingFire,
        HealthCritical,
        TargetNeutralized,
        ReachedPosition,
        AreaClear,
        RequestingBackup,
        AllyDown
    }

    public struct StatusReport
    {
        public string agentName;
        public ReportType type;
        public string message;
        public Vector3 position;
        public float timestamp;
    }

    // Global event — UI systems subscribe to this
    public static event Action<StatusReport> OnStatusReport;

    // Cooldown to prevent spam
    private static Dictionary<string, float> reportCooldowns = new Dictionary<string, float>();
    private static float cooldownDuration = 5f;

    private AgentFSM fsm;
    private AgentIdentity identity;
    private AwarenessModel awareness;
    private AgentState lastState;
    private float lastHealth;
    private int killCount = 0;

    private void Start()
    {
        fsm = GetComponent<AgentFSM>();
        identity = GetComponent<AgentIdentity>();
        awareness = GetComponent<AwarenessModel>();

        if (identity.Combat != null)
        {
            lastHealth = identity.Combat.GetCurrentHealth();
        }

        Invoke(nameof(LateInit), 0.2f);
    }

    private void LateInit()
    {
        lastState = fsm.CurrentStateKey;
    }

    private void Update()
    {
        if (fsm == null || identity == null) return;
        if (identity.Combat != null && identity.Combat.IsDead()) return;

        AgentState currentState = fsm.CurrentStateKey;

        // State transition reports
        if (currentState != lastState)
        {
            OnStateChanged(lastState, currentState);
            lastState = currentState;
        }

        // Health monitoring
        if (identity.Combat != null)
        {
            float currentHealth = identity.Combat.GetCurrentHealth();
            float maxHealth = identity.Combat.GetMaxHealth();

            // Taking fire
            if (currentHealth < lastHealth)
            {
                if (currentHealth / maxHealth < 0.3f)
                    SendReport(ReportType.HealthCritical, "Health critical! Need support!");
                else if (lastHealth - currentHealth > 10f)
                    SendReport(ReportType.TakingFire, "Taking fire!");
            }

            lastHealth = currentHealth;
        }

        // Area clear check (in patrol, no enemies detected for a while)
        if (currentState == AgentState.Patrol && awareness.awareness <= 0f)
        {
            CommandReceiver receiver = GetComponent<CommandReceiver>();
            if (receiver != null && receiver.HasActiveCommand &&
                receiver.CurrentCommand == CommandReceiver.CommandType.MoveTo)
            {
                if (Vector3.Distance(transform.position, receiver.GetCommandTargetPosition()) < 3f)
                {
                    SendReport(ReportType.AreaClear, "Reached position. Area clear.");
                    SendReport(ReportType.ReachedPosition, "In position.");
                }
            }
        }
    }

    private void OnStateChanged(AgentState from, AgentState to)
    {
        // Patrol → Alert: Enemy spotted
        if (from == AgentState.Patrol && to == AgentState.Alert)
        {
            string location = GetLocationDescription();
            SendReport(ReportType.EnemySpotted, $"Contact! Enemy spotted at {location}.");
        }

        // Alert → Engage: Engaging enemy
        if (to == AgentState.Engage)
        {
            SendReport(ReportType.TakingFire, "Engaging hostile!");
        }

        // Engage → Patrol: Combat over
        if (from == AgentState.Engage && to == AgentState.Patrol)
        {
            SendReport(ReportType.AreaClear, "Threat neutralized. Resuming patrol.");
        }

        // Alert → Patrol: Lost contact
        if (from == AgentState.Alert && to == AgentState.Patrol)
        {
            SendReport(ReportType.EnemyLost, "Lost contact with enemy.");
        }

        // Anyone → Dead: Agent down
        if (to == AgentState.Dead)
        {
            SendReport(ReportType.AllyDown, "Agent down!");
        }
    }

    // Call this from combat system when this agent gets a kill
    public void RecordKill()
    {
        killCount++;
        SendReport(ReportType.TargetNeutralized, "Target neutralized.");
    }

    private void SendReport(ReportType type, string message)
    {
        string key = $"{identity.agentName}_{type}";

        // Cooldown check
        if (reportCooldowns.ContainsKey(key))
        {
            if (Time.time - reportCooldowns[key] < cooldownDuration) return;
        }
        reportCooldowns[key] = Time.time;

        StatusReport report = new StatusReport
        {
            agentName = identity.agentName,
            type = type,
            message = message,
            position = transform.position,
            timestamp = Time.time
        };

        OnStatusReport?.Invoke(report);
    }

    private string GetLocationDescription()
    {
        // Simple location based on position
        float x = transform.position.x;
        float z = transform.position.z;

        string ns = z > 0 ? "North" : "South";
        string ew = x > 0 ? "East" : "West";

        if (Mathf.Abs(x) < 10f && Mathf.Abs(z) < 10f) return "Center";
        if (Mathf.Abs(x) < 10f) return ns;
        if (Mathf.Abs(z) < 10f) return ew + " Corridor";
        return ns + "-" + ew;
    }

    // Reset for new match
    public void ResetReporter()
    {
        killCount = 0;
        lastState = AgentState.Patrol;
        reportCooldowns.Clear();
    }
}