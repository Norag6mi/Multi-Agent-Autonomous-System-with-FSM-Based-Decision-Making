using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class VRSimulationWindow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI hudText;
    [SerializeField] private Transform followTarget;

    [Header("Placement")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.7f, 1.3f);
    [SerializeField] private bool faceTarget = true;

    [Header("Settings")]
    [SerializeField] private bool showHUD = true;

    private readonly List<AgentFSM> allAgents = new List<AgentFSM>();

    private void Start()
    {
        allAgents.Clear();
        allAgents.AddRange(FindObjectsOfType<AgentFSM>());

        if (followTarget == null && Camera.main != null)
            followTarget = Camera.main.transform;
    }

    private void LateUpdate()
    {
        UpdatePlacement();

        if (hudText == null) return;

        if (!showHUD)
        {
            hudText.text = "";
            return;
        }

        hudText.text = BuildHUDText();
    }

    private void UpdatePlacement()
    {
        // if (followTarget == null) return;

        // // Position the window relative to the target
        // transform.position = followTarget.position
        //     + followTarget.right * localOffset.x
        //     + Vector3.up * localOffset.y
        //     + followTarget.forward * localOffset.z;

        // if (faceTarget)
        // {
        //     // Simple way to make the panel face the player/camera
        //     transform.LookAt(followTarget.position);
            
        //     // Optional: Keep it upright so it doesn't tilt weirdly
        //     transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
        // }
    }

    private string BuildHUDText()
    {
        if (FactionManager.Instance == null) return "System unavailable";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Autonomous Unit Status</b>");
        sb.AppendLine();

        foreach (FactionType faction in System.Enum.GetValues(typeof(FactionType)))
        {
            var factionAgents = allAgents
                .Where(a => a != null && a.Identity != null && a.Identity.Faction == faction)
                .ToList();

            int alive = FactionManager.Instance.GetAliveCount(faction);
            int total = factionAgents.Count;

            string factionColor = faction == FactionType.Alpha ? "#66B3FF" : "#FF6A5C";
            sb.AppendLine($"<color={factionColor}><b>{GetFactionDisplayName(faction)}  {alive}/{total}</b></color>");

            foreach (AgentFSM agent in factionAgents)
            {
                AgentIdentity id = agent.Identity;
                IAgentCombat combat = id != null ? id.Combat : null;

                string agentName = id != null ? id.agentName : agent.gameObject.name;
                string state = agent.CurrentStateKey.ToString().ToUpper();
                string stateColor = GetStateColor(agent.CurrentStateKey);

                string hp = combat != null ? $"{combat.GetCurrentHealth():F0}" : "?";
                string ammo = combat != null ? $"{combat.GetCurrentAmmo()}" : "?";

                sb.AppendLine($"{agentName} | <color={stateColor}>{state}</color> | HP:{hp} | A:{ammo}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string GetStateColor(AgentState state)
    {
        switch (state)
        {
            case AgentState.Patrol: return "#00FF66";
            case AgentState.Alert: return "#FFD966";
            case AgentState.Engage: return "#FF4D4D";
            case AgentState.Dead: return "#888888";
            default: return "#FFFFFF";
        }
    }

    private string GetFactionDisplayName(FactionType faction)
    {
        switch (faction)
        {
            case FactionType.Alpha: return "Faction Alpha";
            case FactionType.Beta: return "Faction Beta";
            default: return faction.ToString();
        }
    }

    public void SetVisible(bool visible)
    {
        showHUD = visible;
        if (hudText != null && !visible)
            hudText.text = "";
    }
}