using UnityEngine;

public class AlertState : BaseState<AgentState>
{
    private AgentFSM fsm;
    private bool hasBroadcast = false;
    private bool hasReachedInvestigationPoint = false;
    private float lookAroundTimer = 0f;
    private float lookAroundDuration = 3f;
    private bool wasHitWithoutTarget = false;

    public AlertState(AgentFSM fsm) : base(AgentState.Alert)
    {
        this.fsm = fsm;
    }

    public override void EnterState()
    {
        hasBroadcast = false;
        hasReachedInvestigationPoint = false;
        lookAroundTimer = 0f;

        // Check if we have a known position to investigate
        if (fsm.AwarenessModel.currentTarget != null)
        {
            wasHitWithoutTarget = false;
            Vector3 investigatePos = fsm.AwarenessModel.lastKnownPosition;
            fsm.Navigation.InvestigateTo(investigatePos);
        }
        else
        {
            // Got hit but don't know from where — stay and search
            wasHitWithoutTarget = true;
            fsm.Navigation.Stop();
        }

        BroadcastThreatToAllies();

        Debug.Log($"[STATE] {fsm.gameObject.name} → ALERT");
    }

    public override void UpdateState()
    {
        // Keep trying emergency scan every frame if we don't have a target
        if (fsm.AwarenessModel.currentTarget == null)
        {
            TryFindAttacker();
        }

        // If we found a target mid-search, move toward them
        if (fsm.AwarenessModel.currentTarget != null && wasHitWithoutTarget)
        {
            wasHitWithoutTarget = false;
            fsm.Navigation.InvestigateTo(fsm.AwarenessModel.lastKnownPosition);
        }

        // Face threat direction
        if (fsm.AwarenessModel.currentTarget != null)
        {
            fsm.Navigation.LookAt(fsm.AwarenessModel.lastKnownPosition);
        }

        // Update destination if target moved
        if (fsm.AwarenessModel.currentTarget != null && !hasReachedInvestigationPoint)
        {
            fsm.Navigation.InvestigateTo(fsm.AwarenessModel.lastKnownPosition);
        }

        // Check if reached investigation point
        if (!hasReachedInvestigationPoint && fsm.Navigation.HasReachedDestination())
        {
            hasReachedInvestigationPoint = true;
            lookAroundTimer = 0f;
            fsm.Navigation.Stop();
        }

        // Look around at investigation point OR when searching
        if (hasReachedInvestigationPoint || wasHitWithoutTarget)
        {
            lookAroundTimer += Time.deltaTime;
            LookAround();
        }
    }

    public override void ExitState()
    {
        fsm.Navigation.Stop();
    }

    public override AgentState GetNextState()
    {
        AgentState evaluated = fsm.AwarenessModel.EvaluateState();

        if (evaluated == AgentState.Engage)
            return AgentState.Engage;

        if (evaluated == AgentState.Patrol)
            return AgentState.Patrol;

        return AgentState.Alert;
    }

    private void TryFindAttacker()
    {
        SensorSystem sensor = fsm.GetComponent<SensorSystem>();
        if (sensor == null) return;

        Transform found = sensor.EmergencyScan();
        if (found != null)
        {
            fsm.AwarenessModel.ForceMaxAwareness(found);
            Debug.Log($"[ALERT] {fsm.gameObject.name} found attacker during search: {found.name}");
        }
    }

    private void BroadcastThreatToAllies()
    {
        if (hasBroadcast) return;
        if (AgentCoordinator.Instance == null) return;

        AgentCoordinator.Instance.BroadcastThreat(
            fsm.Identity,
            fsm.AwarenessModel.lastKnownPosition
        );
        hasBroadcast = true;
    }

    private void LookAround()
    {
        // Rotate faster when searching for unknown attacker
        float rotateSpeed = wasHitWithoutTarget ? 120f : 30f;
        fsm.transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f);
    }
}