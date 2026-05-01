using UnityEngine;

/// <summary>
/// Tracks how aware this agent is of a detected threat.
/// Awareness gradually increases when enemy is visible, decreases when not.
/// FSM reads awareness value to decide state transitions.
/// </summary>
public class AwarenessModel : MonoBehaviour
{
    [Header("Awareness")]
    [Range(0f, 100f)]
    public float awareness = 0f;
    public float maxAwareness = 100f;

    [Header("Thresholds (read by AgentFSM)")]
    public float alertThreshold = 30f;
    public float engageThreshold = 70f;

    [Header("Distance Ranges")]
    public float closeRange = 3f;
    public float mediumRange = 7f;
    public float farRange = 12f;

    [Header("Increase Rates (per second)")]
    public float closeIncrease = 40f;
    public float mediumIncrease = 20f;
    public float farIncrease = 8f;

    [Header("Decrease Rate (per second)")]
    public float decreaseRate = 15f;

    // Last known info about the detected threat
    [HideInInspector] public Vector3 lastKnownPosition;
    [HideInInspector] public Transform currentTarget;

    /// <summary>
    /// Called by SensorSystem when an enemy is visible this frame.
    /// </summary>
    public void ReportTargetVisible(float distance, Transform target)
    {
        currentTarget = target;
        lastKnownPosition = target.position;

        float rate = GetIncreaseRate(distance);
        awareness += rate * Time.deltaTime;
        awareness = Mathf.Clamp(awareness, 0f, maxAwareness);
    }

    /// <summary>
    /// Called by SensorSystem when NO enemy is visible this frame.
    /// </summary>
    public void ReportTargetLost()
    {
        awareness -= decreaseRate * Time.deltaTime;
        awareness = Mathf.Clamp(awareness, 0f, maxAwareness);

        // Clear target reference once awareness drops to zero
        if (awareness <= 0f)
        {
            currentTarget = null;
        }
    }

    /// <summary>
    /// Called by AgentCoordinator when an ally shares threat intel.
    /// Gives an instant awareness boost.
    /// </summary>
    public void ReceiveAllyThreatInfo(Vector3 threatPosition, float boostAmount)
    {
        lastKnownPosition = threatPosition;
        awareness += boostAmount;
        awareness = Mathf.Clamp(awareness, 0f, maxAwareness);
    }

    /// <summary>
    /// Force awareness to max. Used when agent takes damage
    /// (you know exactly where the threat is).
    /// </summary>
    public void ForceMaxAwareness(Transform attacker)
    {
        awareness = maxAwareness;
        currentTarget = attacker;
        lastKnownPosition = attacker.position;
    }

    // --- Evaluation helpers for FSM ---

    public AgentState EvaluateState()
    {
        if (awareness >= engageThreshold) return AgentState.Engage;
        if (awareness >= alertThreshold) return AgentState.Alert;
        return AgentState.Patrol;
    }

    private float GetIncreaseRate(float distance)
    {
        if (distance <= closeRange) return closeIncrease;
        if (distance <= mediumRange) return mediumIncrease;
        return farIncrease;
    }

    /// <summary>
    /// Called when agent takes damage but can't see attacker.
    /// Spins toward estimated threat direction.
    /// </summary>
    public void ForceAlertNoDitrection()
    {
        awareness = Mathf.Max(awareness, engageThreshold);
    }

}