using UnityEngine;

/// <summary>
/// Active combat. Agent autonomously decides between shooting, reloading,
/// and healing based on resource states. Uses priority-based decision tree.
/// 
/// Decision Priority (checked every frame):
/// 1. Dead? → transition to Dead
/// 2. Target lost/dead? → let awareness decay → Alert/Patrol
/// 3. Currently healing? → wait
/// 4. Health critical? → heal
/// 5. Currently reloading? → wait (face target)
/// 6. No ammo? → reload
/// 7. Target too far? → pursue
/// 8. Default → face target + shoot
/// </summary>


public class EngageState : BaseState<AgentState>
{
    private AgentFSM fsm;
    private Transform currentTarget;
    private bool hasBroadcast = false;

    private float healthCriticalPercent;
    private float maxEngageRange;
    private float optimalRange;

    private float randomizedThreshold;

    public EngageState(AgentFSM fsm) : base(AgentState.Engage)
    {
        this.fsm = fsm;
        this.healthCriticalPercent = fsm.healthCriticalPercent;
        this.maxEngageRange = fsm.maxEngageRange;
        this.optimalRange = fsm.optimalEngageRange;
    }

    public override void EnterState()
    {

        // Pick a threshold around the base value (e.g., if base is 0.3, this picks between 0.25 and 0.35)
        randomizedThreshold = healthCriticalPercent + Random.Range(-0.05f, 0.05f);


        hasBroadcast = false;
        currentTarget = fsm.AwarenessModel.currentTarget;

        // Force immediate stop — no waiting for walk animation
        fsm.Navigation.StopImmediate();
        
        // Force speed to 0 in animator immediately
        AgentAnimator agentAnim = fsm.GetComponentInChildren<AgentAnimator>();
        if (agentAnim != null) agentAnim.StopAll();

        SetCombatTarget(currentTarget);
        BroadcastThreat();

        Debug.Log($"[STATE] {fsm.gameObject.name} → ENGAGE " +
                  $"(Target: {(currentTarget != null ? currentTarget.name : "NONE")})");
    }

    public override void UpdateState()
    {
        // Update target from awareness
        if (fsm.AwarenessModel.currentTarget != null)
        {
            currentTarget = fsm.AwarenessModel.currentTarget;
            SetCombatTarget(currentTarget);
        }

        IAgentCombat combat = fsm.Identity.Combat;
        if (combat == null) return;

        // Priority 1: Dead check
        if (combat.IsDead()) return;

        // Priority 2: Target lost or dead
        if (currentTarget == null || IsTargetDead())
        {
            combat.StopAttack();
            return;
        }

        // Priority 3: Already healing? Then just wait (This is already in your script)
        if (combat.IsHealing())
        {
            fsm.Navigation.LookAt(currentTarget);
            return;
        }

        // Priority 4: Health critical — heal
        float healthPercent = combat.GetCurrentHealth() / combat.GetMaxHealth();

        if (healthPercent <= randomizedThreshold)
        {
            // If we reach here, it means we are NOT currently healing 
            // because Priority 3 would have caught it.
            
            float reactionDelay = Random.Range(0.2f, 1.0f); // Increased range for better desync

            combat.StopAttack();
            combat.StartHeal(50, 2f + reactionDelay); 
            
            Debug.Log($"{fsm.gameObject.name} INITIAL HEAL START! Delay: {reactionDelay:F2}s");
            return;
        }

        // Priority 5: Reloading — face target and wait
        if (combat.IsReloading())
        {
            fsm.Navigation.LookAt(currentTarget);
            return;
        }

        // Priority 6: No ammo — reload
        if (combat.GetCurrentAmmo() <= 0)
        {
            combat.StopAttack();
            combat.Reload();
            return;
        }

        // Priority 7: Too far — pursue
        float distToTarget = fsm.Navigation.DistanceTo(currentTarget);
        if (distToTarget > maxEngageRange)
        {
            combat.StopAttack();
            fsm.Navigation.Pursue(currentTarget, optimalRange);
            fsm.Navigation.LookAt(currentTarget);
            return;
        }

        // Priority 8: In range — stop, face target, shoot
        fsm.Navigation.Stop();
        fsm.Navigation.LookAt(currentTarget, 10f);
        combat.StartAttack();
    }

    public override void ExitState()
    {
        if (fsm.Identity.Combat != null)
            fsm.Identity.Combat.StopAttack();
        fsm.Navigation.Stop();
    }

    public override AgentState GetNextState()
    {
        if (fsm.Identity.Combat != null && fsm.Identity.Combat.IsDead())
            return AgentState.Dead;

        return fsm.AwarenessModel.EvaluateState();
    }

    //For Goal Behaviour

//Old

    // private bool IsTargetDead()
    // {
    //     if (currentTarget == null) return true;
    //     AgentIdentity targetIdentity = currentTarget.GetComponentInParent<AgentIdentity>();
    //     return targetIdentity != null && !targetIdentity.IsAlive;
    // }

//New 

    private bool IsTargetDead()
    {
        if (currentTarget == null) return true;

        // Check via AgentIdentity (AI agents)
        AgentIdentity targetIdentity =
            currentTarget.GetComponentInParent<AgentIdentity>();

        if (targetIdentity != null)
        {
            // Use HealthComponent directly — more reliable than IsAlive property
            HealthComponent targetHealth =
                currentTarget.GetComponentInParent<HealthComponent>();

            if (targetHealth != null)
                return targetHealth.Model.IsDead;

            return !targetIdentity.IsAlive;
        }

        // No AgentIdentity — check HealthComponent directly (covers player)
        HealthComponent health =
            currentTarget.GetComponentInParent<HealthComponent>();

        if (health != null)
            return health.Model.IsDead;

        // Can't determine — assume alive so agent keeps trying
        return false;
    }

    private void SetCombatTarget(Transform target)
    {
        // For CombatStub — set target directly
        CombatStub stub = fsm.GetComponent<CombatStub>();
        if (stub != null)
            stub.currentTarget = target;

        // For real gun system — no need to set target
        // Agent faces target via LookAt(), gun raycasts forward from muzzle
        // The gun system handles everything once we call StartAttack()
    }

    private void BroadcastThreat()
    {
        if (hasBroadcast) return;
        if (AgentCoordinator.Instance == null) return;

        AgentCoordinator.Instance.BroadcastThreat(
            fsm.Identity,
            fsm.AwarenessModel.lastKnownPosition
        );
        hasBroadcast = true;
    }
}