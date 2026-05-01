using UnityEngine;

[RequireComponent(typeof(AgentIdentity))]
[RequireComponent(typeof(AwarenessModel))]
[RequireComponent(typeof(SensorSystem))]
[RequireComponent(typeof(NavigationController))]
[RequireComponent(typeof(PatrolRoute))]
public class AgentFSM : StateManager<AgentState>
{
    [Header("Engage Settings")]
    public float healthCriticalPercent = 0.3f;
    public float maxEngageRange = 15f;
    public float optimalEngageRange = 10f;

    [HideInInspector] public AgentIdentity Identity;
    [HideInInspector] public AwarenessModel AwarenessModel;
    [HideInInspector] public NavigationController Navigation;
    [HideInInspector] public PatrolRoute Patrol;

    private GameObject stateIndicator;

    public GameObject StateIndicator => stateIndicator; // new in command integration

    private Renderer indicatorRenderer;
    private bool deathHandled = false;

    // Tracks health between frames to detect damage
    private float lastKnownHealth;

    protected override void Start()
    {
        Identity = GetComponent<AgentIdentity>();
        AwarenessModel = GetComponent<AwarenessModel>();
        Navigation = GetComponent<NavigationController>();
        Patrol = GetComponent<PatrolRoute>();

        States.Add(AgentState.Patrol, new PatrolState(this));
        States.Add(AgentState.Alert, new AlertState(this));
        States.Add(AgentState.Engage, new EngageState(this));
        States.Add(AgentState.Dead, new DeadState(this));

        CurrentState = States[AgentState.Patrol];

        if (Identity.Combat != null)
        {
            Identity.Combat.OnDeathEvent += OnAgentDeath;
            lastKnownHealth = Identity.Combat.GetCurrentHealth();
        }

        CreateStateIndicator();
        base.Start();
    }

    protected override void Update()
    {
        if (deathHandled) return;

        if (Identity.Combat != null && Identity.Combat.IsDead()
            && CurrentStateKey != AgentState.Dead)
        {
            OnAgentDeath();
            return;
        }

        DetectDamage();
        base.Update();
        UpdateStateIndicator();
    }

    /// <summary>
    /// Compares health each frame. If it dropped, agent took damage.
    /// Runs emergency scan to find attacker even outside vision cone.
    /// </summary>
    private void DetectDamage()
    {
        if (Identity.Combat == null) return;

        float currentHealth = Identity.Combat.GetCurrentHealth();

        // No damage taken this frame
        if (currentHealth >= lastKnownHealth)
        {
            lastKnownHealth = currentHealth;
            return;
        }

        Debug.Log($"[FSM] {gameObject.name} took damage! Finding attacker...");

        // Case 1: Already have a target — face them and fight
        if (AwarenessModel.currentTarget != null)
        {
            AwarenessModel.ForceMaxAwareness(AwarenessModel.currentTarget);
            lastKnownHealth = currentHealth;
            return;
        }

        // Case 2: No target — emergency 360 scan
        SensorSystem sensor = GetComponent<SensorSystem>();
        if (sensor != null)
        {
            Transform attacker = sensor.EmergencyScan();
            if (attacker != null)
            {
                AwarenessModel.ForceMaxAwareness(attacker);
                Debug.Log($"[FSM] {gameObject.name} found attacker: {attacker.name}");
                lastKnownHealth = currentHealth;
                return;
            }
        }

        // Case 3: Can't find attacker — ask allies
        if (AgentCoordinator.Instance != null)
        {
            AgentCoordinator.Instance.RequestTargetInfo(Identity);
        }

        // Case 4: Still nothing — force alert and turn around
        if (AwarenessModel.currentTarget == null)
        {
            AwarenessModel.awareness = Mathf.Max(
                AwarenessModel.awareness,
                AwarenessModel.engageThreshold
            );
            transform.Rotate(0f, 180f, 0f);
            Debug.Log($"[FSM] {gameObject.name} can't find attacker, turning around!");
        }

        lastKnownHealth = currentHealth;
    }

    private void OnAgentDeath()
    {
        if (deathHandled) return;
        deathHandled = true;

        // Record death location in tactical memory
        SharedTacticalMemory memory = SharedTacticalMemory.GetInstance(Identity.Faction);
        if (memory != null)
            memory.RecordAllyDeath(transform.position);

        
        Debug.Log($"[FSM] {gameObject.name} death event received");
        TransitionToState(AgentState.Dead);
    }

    private void OnDestroy()
    {
        if (Identity != null && Identity.Combat != null)
        {
            Identity.Combat.OnDeathEvent -= OnAgentDeath;
        }
        if (stateIndicator != null) Destroy(stateIndicator);
    }

    private void CreateStateIndicator()
    {
        stateIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        stateIndicator.name = "StateIndicator";
        stateIndicator.transform.SetParent(transform);
        stateIndicator.transform.localPosition = Vector3.up * 2.5f;
        stateIndicator.transform.localScale = Vector3.one * 0.3f;

        Collider col = stateIndicator.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        indicatorRenderer = stateIndicator.GetComponent<Renderer>();
        indicatorRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    }

    private void UpdateStateIndicator()
    {
        if (indicatorRenderer == null) return;

        indicatorRenderer.material.color = CurrentStateKey switch
        {
            AgentState.Patrol => Color.green,
            AgentState.Alert => Color.yellow,
            AgentState.Engage => Color.red,
            AgentState.Dead => Color.black,
            _ => Color.white
        };
    }

    public void ResetFSM()
    {
        StopAllCoroutines();

        deathHandled = false;
        enabled = true;

        SensorSystem sensor = GetComponent<SensorSystem>();
        if (sensor != null)
            sensor.enabled = true;

        if (Navigation != null)
        {
            Navigation.enabled = true;

            if (Navigation.Agent != null)
            {
                Navigation.Agent.enabled = true;
                Navigation.Agent.isStopped = false;
                Navigation.Agent.ResetPath();
                Navigation.Agent.velocity = Vector3.zero;
            }
        }

        AwarenessModel awareness = GetComponent<AwarenessModel>();
        if (awareness != null)
        {
            awareness.awareness = 0f;
            awareness.currentTarget = null;
            awareness.lastKnownPosition = Vector3.zero;
        }

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = true;

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
            capsule.enabled = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0f);
        }

        if (Identity != null && Identity.Combat != null)
        lastKnownHealth = Identity.Combat.GetCurrentHealth();

            if (States[AgentState.Patrol] is PatrolState patrolState)
            {
                patrolState.ForceFreshPatrol();
            }

        CurrentState = States[AgentState.Patrol];
        CurrentState.EnterState();

        UpdateStateIndicator();

        Debug.Log($"[FSM RESET] {gameObject.name} -> Patrol");
    }
}