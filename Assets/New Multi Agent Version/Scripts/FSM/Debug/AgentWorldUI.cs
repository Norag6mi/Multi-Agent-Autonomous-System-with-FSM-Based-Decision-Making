using UnityEngine;

public class AgentWorldUI : MonoBehaviour
{
    private AgentFSM fsm;
    private AgentIdentity identity;
    private Camera cam;

    public Vector3 offset = new Vector3(0, 2.8f, 0);

    private void Start()
    {
        fsm = GetComponent<AgentFSM>();
        identity = GetComponent<AgentIdentity>();
    }

    private void OnGUI()
    {
        if (fsm == null) return;

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) cam = FindObjectOfType<Camera>();
            if (cam == null) return;
        }

        Vector3 screenPos = cam.WorldToScreenPoint(transform.position + offset);
        if (screenPos.z < 0) return;

        float dist = Vector3.Distance(cam.transform.position, transform.position);
        if (dist > 40f) return;

        float x = screenPos.x;
        float y = Screen.height - screenPos.y;

        // State text only — clean and minimal
        string state = fsm.CurrentStateKey.ToString().ToUpper();
        Color stateColor = fsm.CurrentStateKey switch
        {
            AgentState.Patrol => Color.green,
            AgentState.Alert => Color.yellow,
            AgentState.Engage => Color.red,
            AgentState.Dead => Color.gray,
            _ => Color.white
        };

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 11
        };

        // Name
        string agentName = identity != null ? identity.agentName : gameObject.name;
        GUI.color = Color.white;
        GUI.Label(new Rect(x - 40, y - 18, 80, 16), agentName, style);

        // State
        GUI.color = stateColor;
        style.fontSize = 10;
        GUI.Label(new Rect(x - 40, y - 4, 80, 16), state, style);

        // Thin health bar only — no text on it
        if (identity != null && identity.Combat != null)
        {
            float hp = identity.Combat.GetCurrentHealth();
            float maxHp = identity.Combat.GetMaxHealth();
            float percent = Mathf.Clamp01(hp / maxHp);

            float barW = 40f;
            float barH = 4f;
            float barX = x - barW / 2f;
            float barY = y + 12;

            GUI.color = new Color(0.2f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

            GUI.color = percent > 0.5f ? Color.green : percent > 0.25f ? Color.yellow : Color.red;
            GUI.DrawTexture(new Rect(barX, barY, barW * percent, barH), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
    }
}