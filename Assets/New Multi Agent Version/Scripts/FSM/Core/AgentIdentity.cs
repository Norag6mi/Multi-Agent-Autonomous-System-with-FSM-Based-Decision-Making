using UnityEngine;

public class AgentIdentity : MonoBehaviour
{
    [Header("Faction Assignment")]
    public FactionType Faction;

    [Header("Display")]
    public string agentName = "Agent";

    [HideInInspector] public AwarenessModel Awareness;
    [HideInInspector] public IAgentCombat Combat;
    [HideInInspector] public NavigationController Navigation;

    public int UnitIndex { get; private set; }

    // "Unit A-1" or "Unit B-2"
    public string DisplayName
    {
        get
        {
            string prefix = Faction == FactionType.Alpha ? "A" : "B";
            return $"Unit {prefix}-{UnitIndex}";
        }
    }

    private void Awake()
    {
        Awareness = GetComponent<AwarenessModel>();
        Navigation = GetComponent<NavigationController>();

        CombatController realCombat = GetComponent<CombatController>();
        if (realCombat != null)
        {
            Combat = realCombat;
        }
        else
        {
            CombatStub stub = GetComponent<CombatStub>();
            if (stub != null) Combat = stub;
        }

        // Extract unit number from GameObject name (Agent_A1 → 1)
        UnitIndex = ExtractIndex(gameObject.name);

        // Set agentName to display name
        agentName = DisplayName;
    }

    private int ExtractIndex(string name)
    {
        if (name.Length > 0 && char.IsDigit(name[name.Length - 1]))
            return int.Parse(name[name.Length - 1].ToString());
        return 0;
    }

    private void Start()
    {
        if (FactionManager.Instance != null)
            FactionManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        if (FactionManager.Instance != null)
            FactionManager.Instance.Unregister(this);
    }

    public bool IsAlive => Combat == null || !Combat.IsDead();
}