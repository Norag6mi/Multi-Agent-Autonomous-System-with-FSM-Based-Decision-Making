using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class VRSelectionPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VRCommanderController commander;
    [SerializeField] private TacticalHologram hologram;
    [SerializeField] private TextMeshProUGUI selectionText;

    [Header("Selection Ring on Hologram")]
    [SerializeField] private Color selectionHighlight = new Color(0f, 1f, 1f, 0.9f);
    [SerializeField] private float ringSize = 0.04f;

    private readonly List<GameObject> holoSelectionRings = new List<GameObject>();
    private int maxRings = 12;

    private void Start()
    {
        if (commander == null)
            commander = FindObjectOfType<VRCommanderController>();

        if (hologram == null)
            hologram = FindObjectOfType<TacticalHologram>();

        for (int i = 0; i < maxRings; i++)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "HoloSelectRing";
            ring.transform.SetParent(hologram != null ? hologram.transform : transform);
            ring.transform.localScale = new Vector3(ringSize, 0.002f, ringSize);

            Collider col = ring.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = ring.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = selectionHighlight;
            rend.material = mat;

            ring.SetActive(false);
            holoSelectionRings.Add(ring);
        }
    }

    private void Update()
    {
        if (commander == null || hologram == null) return;

        foreach (var ring in holoSelectionRings)
            ring.SetActive(false);

        var selected = commander.GetSelectedReceivers();
        if (selected == null || selected.Count == 0)
        {
            if (selectionText != null)
                selectionText.text = "No unit selected";
            return;
        }

        int idx = 0;
        List<string> lines = new List<string>();

        foreach (var receiver in selected)
        {
            if (receiver == null || idx >= holoSelectionRings.Count) continue;

            AgentIdentity id = receiver.GetComponent<AgentIdentity>();
            AgentFSM fsm = receiver.GetComponent<AgentFSM>();

            if (id == null || fsm == null) continue;
            if (id.Combat != null && id.Combat.IsDead()) continue;

            Vector3 agentWorld = receiver.transform.position;
            float nx = Mathf.InverseLerp(-45f, 45f, agentWorld.x);
            float nz = Mathf.InverseLerp(-45f, 45f, agentWorld.z);

            float hx = Mathf.Lerp(-hologram.HologramWidth / 2f, hologram.HologramWidth / 2f, nx);
            float hz = Mathf.Lerp(-hologram.HologramDepth / 2f, hologram.HologramDepth / 2f, nz);

            holoSelectionRings[idx].transform.localPosition = new Vector3(hx, 0.04f, hz);
            holoSelectionRings[idx].SetActive(true);

            float hp = id.Combat != null ? id.Combat.GetCurrentHealth() : 0f;
            string state = fsm.CurrentStateKey.ToString();
            string cmdStatus = receiver.HasActiveCommand ? receiver.CurrentCommand.ToString() : "Autonomous";

            lines.Add($"{id.agentName} | {state} | HP:{hp:F0} | {cmdStatus}");
            idx++;
        }

        if (selectionText != null)
        {
            if (commander.IsAllSelected)
                selectionText.text = $"ALL UNITS SELECTED ({lines.Count})\n" + string.Join("\n", lines);
            else
                selectionText.text = string.Join("\n", lines);
        }
    }
}