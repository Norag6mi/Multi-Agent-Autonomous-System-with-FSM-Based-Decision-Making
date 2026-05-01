using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform[] alphaSpawnPoints;
    [SerializeField] private Transform[] BetaSpawnPoints;

    [Header("References")]
    [SerializeField] private TeamPerceptionData teamPerception;

    private List<AgentIdentity> allAgents = new List<AgentIdentity>();
    private bool initialized = false;

    private void Start()
    {
        Invoke(nameof(Initialize), 0.3f);
    }

    private void Initialize()
    {
        allAgents.Clear();
        allAgents.AddRange(FactionManager.Instance.GetFactionMembers(FactionType.Alpha));
        allAgents.AddRange(FactionManager.Instance.GetFactionMembers(FactionType.Beta));
        initialized = true;
        Debug.Log($"[SPAWN] Initialized with {allAgents.Count} agents");
    }

    public void ResetAllAgents()
    {
        if (!initialized) return;

        var alphaAgents = FactionManager.Instance.GetFactionMembers(FactionType.Alpha);
        var BetaAgents = FactionManager.Instance.GetFactionMembers(FactionType.Beta);

        for (int i = 0; i < alphaAgents.Count && i < alphaSpawnPoints.Length; i++)
        {
            ResetAgent(alphaAgents[i], alphaSpawnPoints[i]);
        }

        for (int i = 0; i < BetaAgents.Count && i < BetaSpawnPoints.Length; i++)
        {
            ResetAgent(BetaAgents[i], BetaSpawnPoints[i]);
        }

        if (teamPerception != null)
            teamPerception.ResetPerception();

        SharedTacticalMemory alphaMemory = SharedTacticalMemory.GetInstance(FactionType.Alpha);
        if (alphaMemory != null) alphaMemory.ResetMemory();

        SharedTacticalMemory BetaMemory = SharedTacticalMemory.GetInstance(FactionType.Beta);
        if (BetaMemory != null) BetaMemory.ResetMemory();

        Debug.Log("[SPAWN] All agents reset to spawn positions");
    }

   private void ResetAgent(AgentIdentity agent, Transform spawnPoint)
    {
        if (agent == null || spawnPoint == null) return;

        GameObject go = agent.gameObject;

        if (!go.activeSelf)
            go.SetActive(true);

        // Stop all MonoBehaviour coroutines on critical components
        AgentFSM fsm = go.GetComponent<AgentFSM>();
        if (fsm != null)
            fsm.StopAllCoroutines();

        HealthComponent health = go.GetComponent<HealthComponent>();
        if (health != null)
            health.StopAllCoroutines();

        WeaponComponent weapon = go.GetComponent<WeaponComponent>();
        if (weapon != null)
            weapon.StopAllCoroutines();

        NavigationController nav = go.GetComponent<NavigationController>();
        if (nav != null)
            nav.StopAllCoroutines();

        CombatController combat = go.GetComponent<CombatController>();
        if (combat != null)
            combat.StopAllCoroutines();

        // Disable movement-related components before teleport
        UnityEngine.AI.NavMeshAgent navAgent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null && navAgent.enabled)
            navAgent.enabled = false;

        CharacterController cc = go.GetComponent<CharacterController>();
        if (cc != null && cc.enabled)
            cc.enabled = false;

        // Reset transform
        go.transform.position = spawnPoint.position;
        go.transform.rotation = spawnPoint.rotation;

        // Physics hard reset
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; // safer for NavMesh-driven agents
            rb.position = spawnPoint.position;
            rb.rotation = spawnPoint.rotation;
        }

        // Health reset
        if (health != null)
            health.Model.ResetHealth();

        // Weapon reset
        if (weapon != null)
            weapon.Model.ResetAmmo();

        // Combat reset
        if (combat != null)
            combat.ResetCombat();

        // Awareness reset
        AwarenessModel awareness = go.GetComponent<AwarenessModel>();
        if (awareness != null)
        {
            awareness.awareness = 0f;
            awareness.currentTarget = null;
            awareness.lastKnownPosition = Vector3.zero;
        }

        // Re-enable perception
        SensorSystem sensor = go.GetComponent<SensorSystem>();
        if (sensor != null)
            sensor.enabled = true;

        // Reset commands
        CommandReceiver cmdReceiver = go.GetComponent<CommandReceiver>();
        if (cmdReceiver != null)
            cmdReceiver.ClearAllCommands();

        PatrolRoute patrol = go.GetComponent<PatrolRoute>();
        if (patrol != null)
            patrol.ResetCommandState();

        AgentStatusReporter reporter = go.GetComponent<AgentStatusReporter>();
        if (reporter != null)
            reporter.ResetReporter();

        // Re-enable all renderers
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
            r.enabled = true;

        // Re-enable all colliders
        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in colliders)
            c.enabled = true;

        // Re-enable capsule specifically
        CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
        if (capsule != null)
            capsule.enabled = true;

        // Re-enable animator(s)
        Animator[] animators = go.GetComponentsInChildren<Animator>(true);
        foreach (Animator anim in animators)
        {
            anim.enabled = true;
            anim.applyRootMotion = false;
            anim.Rebind();
            anim.Update(0f);
        }

        CharacterAnimatorBridge bridge = go.GetComponent<CharacterAnimatorBridge>();
        if (bridge != null)
            bridge.enabled = true;

        AgentAnimator agentAnimator = go.GetComponentInChildren<AgentAnimator>(true);
        if (agentAnimator != null)
            agentAnimator.enabled = true;

        // Re-enable movement systems
        if (nav != null)
            nav.enabled = true;

        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.Warp(spawnPoint.position);
            navAgent.isStopped = false;
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }

        //

        if (health != null)
        health.ResetHealthComponent();

        if (weapon != null)
        weapon.ResetWeapon();


        if (cc != null)
            cc.enabled = true;

        // Reset FSM LAST
        if (fsm != null)
            fsm.ResetFSM();

        Debug.Log($"[SPAWN] Reset agent: {go.name} | FSM:{(fsm != null && fsm.enabled)} | Animator:{(go.GetComponent<Animator>() != null ? go.GetComponent<Animator>().enabled.ToString() : "none")} | Sensor:{(sensor != null && sensor.enabled)} | CC:{(cc != null && cc.enabled)}");
    
        Animator mainAnim = go.GetComponent<Animator>();
        SensorSystem sensorCheck = go.GetComponent<SensorSystem>();
        CharacterController ccCheck = go.GetComponent<CharacterController>();
        CapsuleCollider capCheck = go.GetComponent<CapsuleCollider>();
        Rigidbody rbCheck = go.GetComponent<Rigidbody>();
        AgentFSM fsmCheck = go.GetComponent<AgentFSM>();
        UnityEngine.AI.NavMeshAgent navCheck = go.GetComponent<UnityEngine.AI.NavMeshAgent>();

        Debug.Log(
            $"[RESET VERIFY] {go.name} | " +
            $"FSM={(fsmCheck != null && fsmCheck.enabled)} | " +
            $"NavCtrl={(nav != null && nav.enabled)} | " +
            $"NavAgent={(navCheck != null && navCheck.enabled)} | " +
            $"Sensor={(sensorCheck != null && sensorCheck.enabled)} | " +
            $"Animator={(mainAnim != null && mainAnim.enabled)} | " +
            $"CC={(ccCheck != null && ccCheck.enabled)} | " +
            $"Capsule={(capCheck != null && capCheck.enabled)} | " +
            $"RBKinematic={(rbCheck != null && rbCheck.isKinematic)}"
        );
    }



}