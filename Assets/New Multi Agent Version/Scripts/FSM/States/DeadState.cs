// using UnityEngine;

// public class DeadState : BaseState<AgentState>
// {
//     private AgentFSM fsm;

//     public DeadState(AgentFSM fsm) : base(AgentState.Dead)
//     {
//         this.fsm = fsm;
//     }

//     public override void EnterState()
//     {
//         Debug.Log($"[STATE] {fsm.gameObject.name} → DEAD");

//         // Stop combat
//         if (fsm.Identity.Combat != null)
//             fsm.Identity.Combat.StopAttack();

//         // Stop navigation
//         fsm.Navigation.DisableCompletely();

//         // Disable perception
//         SensorSystem sensor = fsm.GetComponent<SensorSystem>();
//         if (sensor != null) sensor.enabled = false;

//         // Stop locomotion animation
//         AgentAnimator agentAnim = fsm.GetComponentInChildren<AgentAnimator>();
//         if (agentAnim != null) agentAnim.StopAll();

//         // Death animation is handled by CombatController.HandleDeath() 
//         // which calls CharacterAnimatorBridge.PlayDeath()
//         // So we don't need to trigger it here

//         // Force black indicator
//         Transform indicator = fsm.transform.Find("StateIndicator");
//         if (indicator != null)
//         {
//             Renderer r = indicator.GetComponent<Renderer>();
//             if (r != null) r.material.color = Color.black;
//         }

//         // Disable FSM
//         fsm.StartCoroutine(DisableAfterDelay());
//     }

//     private System.Collections.IEnumerator DisableAfterDelay()
//     {
//         yield return new WaitForSeconds(0.1f);
//         fsm.enabled = false;
//     }

//     public override void ExitState() { }
//     public override void UpdateState() { }
//     public override AgentState GetNextState() => AgentState.Dead;
// }


using UnityEngine;
using System.Collections;

public class DeadState : BaseState<AgentState>
{
    private AgentFSM fsm;

    public DeadState(AgentFSM fsm) : base(AgentState.Dead)
    {
        this.fsm = fsm;
    }

    public override void EnterState()
    {
        Debug.Log($"[STATE] {fsm.gameObject.name} -> DEAD");

        if (fsm.Identity != null && fsm.Identity.Combat != null)
            fsm.Identity.Combat.StopAttack();

        if (fsm.Navigation != null)
            fsm.Navigation.DisableCompletely();

        SensorSystem sensor = fsm.GetComponent<SensorSystem>();
        if (sensor != null)
            sensor.enabled = false;

        AgentAnimator agentAnim = fsm.GetComponentInChildren<AgentAnimator>();
        if (agentAnim != null)
            agentAnim.StopAll();

        Animator animator = fsm.GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsFiring", false);
        }

        Rigidbody rb = fsm.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        CharacterController cc = fsm.GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        CapsuleCollider capsule = fsm.GetComponent<CapsuleCollider>();
        if (capsule != null)
            capsule.enabled = false;

        Transform indicator = fsm.transform.Find("StateIndicator");
        if (indicator != null)
        {
            Renderer r = indicator.GetComponent<Renderer>();
            if (r != null)
                r.material.color = Color.black;
        }

        fsm.StartCoroutine(DisableAfterDelay());
    }

    private IEnumerator DisableAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        if (fsm != null)
            fsm.enabled = false;
    }

    public override void ExitState() { }
    public override void UpdateState() { }
    public override AgentState GetNextState() => AgentState.Dead;
}