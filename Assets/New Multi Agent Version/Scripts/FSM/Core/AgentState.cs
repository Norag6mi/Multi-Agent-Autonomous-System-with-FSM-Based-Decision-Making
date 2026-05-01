// ============================================================================
// AgentState.cs
// Defines all possible states for our autonomous agents.
// Used by the FSM framework to identify and transition between states.
// 
// Maps to the portfolio description:
// "3-state FSM (Patrol → Alert → Engage)" + Dead as terminal state
// ============================================================================

/// <summary>
/// The primary behavioral states an autonomous agent can be in.
/// Transitions are driven by the AwarenessModel thresholds.
/// 
/// Patrol → Alert → Engage → Dead
///    ↑        ↓
///    ←--------← (awareness decay)
/// </summary>
public enum AgentState
{
    /// <summary>
    /// Default state. Agent patrols designated area using NavMesh pathfinding.
    /// Transitions to Alert when awareness >= alertThreshold.
    /// </summary>
    Patrol,

    /// <summary>
    /// Agent has detected suspicious activity. Moves to investigate
    /// last known position and broadcasts threat to nearby allies.
    /// Transitions to Engage when awareness >= engageThreshold.
    /// Transitions back to Patrol when awareness decays below alertThreshold.
    /// </summary>
    Alert,

    /// <summary>
    /// Active combat state. Agent autonomously decides between:
    /// shooting, reloading, or healing based on resource states.
    /// Transitions back to Alert when awareness decays below engageThreshold.
    /// Transitions to Dead when health reaches zero.
    /// </summary>
    Engage,

    /// <summary>
    /// Terminal state. Agent is dead. No transitions out.
    /// Disables all AI systems and notifies FactionManager.
    /// </summary>
    Dead
}