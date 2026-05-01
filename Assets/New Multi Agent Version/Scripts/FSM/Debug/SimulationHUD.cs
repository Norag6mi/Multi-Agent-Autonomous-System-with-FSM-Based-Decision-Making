using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SimulationHUD : MonoBehaviour
{
    public bool showHUD = true;
    public KeyCode toggleKey = KeyCode.Tab;

    private List<AgentFSM> allAgents = new List<AgentFSM>();

    private void Start()
    {
        allAgents.AddRange(FindObjectsOfType<AgentFSM>());
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            showHUD = !showHUD;
    }

    private void OnGUI()
    {
        if (!showHUD) return;

        float panelWidth = 280f;
        float x = 10f;
        float y = 10f;

        // Title
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(x, y, panelWidth, 30), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Label(new Rect(x, y + 2, panelWidth, 28), "Multi-Agent System", titleStyle);
        y += 34f;

        // Draw each faction grouped
        foreach (FactionType faction in System.Enum.GetValues(typeof(FactionType)))
        {
            y = DrawFactionBlock(faction, x, y, panelWidth);
            y += 6f; // Gap between factions
        }
    }

    private float DrawFactionBlock(FactionType faction, float x, float y, float width)
    {
        if (FactionManager.Instance == null) return y;

        List<AgentFSM> factionAgents = allAgents
            .Where(a => a != null && a.Identity != null && a.Identity.Faction == faction)
            .ToList();

        int alive = FactionManager.Instance.GetAliveCount(faction);
        int total = factionAgents.Count;

        // Faction header color
        Color factionColor = faction == FactionType.Alpha
            ? new Color(0.2f, 0.5f, 1f)
            : new Color(1f, 0.35f, 0.25f);

        // Header background
        GUI.color = new Color(factionColor.r * 0.3f, factionColor.g * 0.3f, factionColor.b * 0.3f, 0.8f);
        GUI.DrawTexture(new Rect(x, y, width, 26), Texture2D.whiteTexture);

        // Header text
        GUI.color = factionColor;
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(x + 10, y + 2, width - 20, 24), $"{faction}  {alive}/{total}", headerStyle);
        GUI.color = Color.white;
        y += 28f;

        // Agent rows
        GUIStyle rowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };

        foreach (AgentFSM agent in factionAgents)
        {
            AgentIdentity id = agent.Identity;
            IAgentCombat combat = id != null ? id.Combat : null;
            string agentName = id != null ? id.agentName : agent.gameObject.name;
            string state = agent.CurrentStateKey.ToString().ToUpper();

            Color stateColor = agent.CurrentStateKey switch
            {
                AgentState.Patrol => Color.green,
                AgentState.Alert => Color.yellow,
                AgentState.Engage => Color.red,
                AgentState.Dead => Color.gray,
                _ => Color.white
            };

            // Row background
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(x, y, width, 22), Texture2D.whiteTexture);

            // State color bar on left edge
            GUI.color = stateColor;
            GUI.DrawTexture(new Rect(x, y, 4, 22), Texture2D.whiteTexture);

            // Agent info
            string hp = combat != null ? $"{combat.GetCurrentHealth():F0}" : "?";
            string ammo = combat != null ? $"{combat.GetCurrentAmmo()}" : "?";

            // Name (more space)
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12, y + 1, 90, 20), agentName, rowStyle);

            // State with color
            GUI.color = stateColor;
            GUI.Label(new Rect(x + 105, y + 1, 70, 20), state, rowStyle);

            // HP and Ammo
            GUI.color = Color.white;
            GUIStyle statStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12
            };
            GUI.Label(new Rect(x + 175, y + 2, 55, 20), $"HP:{hp}", statStyle);
            GUI.Label(new Rect(x + 225, y + 2, 50, 20), $"A:{ammo}", statStyle);

            GUI.color = Color.white;
            y += 23f;
        }

        return y;
    }
}