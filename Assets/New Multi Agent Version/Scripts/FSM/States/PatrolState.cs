using UnityEngine;

public class PatrolState : BaseState<AgentState>
{
    private AgentFSM fsm;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float currentWaitDuration = 0f;
    private float stuckTimer = 0f;
    private float maxTimeToReachPoint = 10f;

    private bool returningFromCombat = false;
    private float postCombatTimer = 0f;
    private float postCombatDuration = 8f;

    // NEW: force clean patrol entry after reset
    private bool forceFreshPatrol = false;

    private float timeSinceStartedPatrol = 0f;

    public PatrolState(AgentFSM fsm) : base(AgentState.Patrol)
    {
        this.fsm = fsm;
    }

    public void ForceFreshPatrol()
    {
        forceFreshPatrol = true;
    }

    public override void EnterState()
    {

        timeSinceStartedPatrol = 0f;

        isWaiting = false;
        waitTimer = 0f;
        stuckTimer = 0f;

        if (fsm.Patrol == null || fsm.Navigation == null)
            return;

        if (fsm.Patrol.IsHolding)
        {
            returningFromCombat = false;
            fsm.Navigation.Stop();
            return;
        }

        // After reset, skip any post-combat logic
        if (forceFreshPatrol)
        {
            forceFreshPatrol = false;
            returningFromCombat = false;
            SetNextPatrolDestination();
            return;
        }

        // Only investigate last known position if awareness is still meaningful
        if (fsm.AwarenessModel != null &&
            fsm.AwarenessModel.lastKnownPosition != Vector3.zero &&
            fsm.AwarenessModel.awareness > 5f)
        {
            returningFromCombat = true;
            postCombatTimer = 0f;

            Vector3 investigatePos = fsm.AwarenessModel.lastKnownPosition;
            fsm.Navigation.PatrolTo(investigatePos);
            return;
        }

        returningFromCombat = false;
        SetNextPatrolDestination();
    }

    public override void UpdateState()
    {
        // 1. Update the 10-second buffer timer
        timeSinceStartedPatrol += Time.deltaTime;

        IAgentCombat combat = fsm.Identity.Combat;
        if (combat != null && !combat.IsDead() && !combat.IsHealing())
        {
            float currentHP = combat.GetCurrentHealth();
            float maxHP = combat.GetMaxHealth();
            float threshold = maxHP * 0.7f; // This is the 70% mark

            // 2. Only heal if HP is below 70% AND 10 seconds have passed
            if (currentHP < threshold && timeSinceStartedPatrol >= 10f) 
            {
                // 3. Calculate exactly how much is needed to reach 70%
                int amountNeeded = Mathf.CeilToInt(threshold - currentHP);

                fsm.Navigation.Stop();
                combat.StartHeal(amountNeeded, 3f); 
                
                timeSinceStartedPatrol = 0f; // Reset timer
                return;
            }
        }


        if (fsm.Patrol == null || fsm.Navigation == null)
            return;

        if (fsm.Patrol.IsHolding)
        {
            fsm.Navigation.Stop();
            return;
        }

        if (returningFromCombat)
        {
            postCombatTimer += Time.deltaTime;

            if (postCombatTimer >= postCombatDuration ||
                fsm.Navigation.HasReachedDestination())
            {
                returningFromCombat = false;
                SetNextPatrolDestination();
            }
            return;
        }

        if (isWaiting)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= currentWaitDuration)
            {
                isWaiting = false;
                stuckTimer = 0f;
                SetNextPatrolDestination();
            }
            return;
        }

        stuckTimer += Time.deltaTime;
        if (stuckTimer > maxTimeToReachPoint)
        {
            stuckTimer = 0f;
            SetNextPatrolDestination();
            return;
        }

        if (!fsm.Navigation.HasPath())
        {
            SetNextPatrolDestination();
            return;
        }

        if (fsm.Navigation.HasReachedDestination())
        {
            isWaiting = true;
            waitTimer = 0f;
            currentWaitDuration = fsm.Patrol.GetRandomWaitTime();
            fsm.Navigation.Stop();
        }
    }

    public override void ExitState()
    {
        isWaiting = false;
        returningFromCombat = false;
        fsm.Navigation.Stop();
    }

    public override AgentState GetNextState()
    {
        AgentState evaluated = fsm.AwarenessModel.EvaluateState();
        if (evaluated != AgentState.Patrol)
            return evaluated;

        return AgentState.Patrol;
    }

    private void SetNextPatrolDestination()
    {
        if (fsm.Patrol == null || fsm.Navigation == null)
            return;

        Vector3 destination = fsm.Patrol.GetNextDestination();
        fsm.Navigation.PatrolTo(destination);
    }
}