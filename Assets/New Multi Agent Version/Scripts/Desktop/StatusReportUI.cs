// Assets/Scripts/Desktop/StatusReportUI.cs
using UnityEngine;
using System.Collections.Generic;

// Displays agent status reports as scrolling text notifications.
// Shows in bottom-right corner of desktop view.
public class StatusReportUI : MonoBehaviour
{
    [SerializeField] private int maxVisibleReports = 5;
    [SerializeField] private float reportDisplayDuration = 6f;

    private struct DisplayReport
    {
        public string text;
        public float timestamp;
        public Color color;
    }

    private List<DisplayReport> activeReports = new List<DisplayReport>();
    private GUIStyle reportStyle;
    private GUIStyle backgroundStyle;

    private void OnEnable()
    {
        AgentStatusReporter.OnStatusReport += HandleReport;
    }

    private void OnDisable()
    {
        AgentStatusReporter.OnStatusReport -= HandleReport;
    }

    private void HandleReport(AgentStatusReporter.StatusReport report)
    {
        // Only show reports from Alpha faction
        // (commander only sees their own team's reports)
        Color color = GetReportColor(report.type);

        activeReports.Insert(0, new DisplayReport
        {
            text = $"[{report.agentName}] {report.message}",
            timestamp = Time.time,
            color = color
        });

        // Trim old reports
        while (activeReports.Count > maxVisibleReports)
            activeReports.RemoveAt(activeReports.Count - 1);
    }

    private void Update()
    {
        // Remove expired reports
        activeReports.RemoveAll(r => Time.time - r.timestamp > reportDisplayDuration);
    }

    private void OnGUI()
    {
        if (activeReports.Count == 0) return;

        if (reportStyle == null)
        {
            reportStyle = new GUIStyle(GUI.skin.label);
            reportStyle.fontSize = 14;
            reportStyle.alignment = TextAnchor.MiddleRight;
            reportStyle.richText = true;

            backgroundStyle = new GUIStyle(GUI.skin.box);
        }

        float panelWidth = 350f;
        float lineHeight = 24f;
        float panelHeight = activeReports.Count * lineHeight + 10f;
        float panelX = Screen.width - panelWidth - 20f;
        float panelY = Screen.height - panelHeight - 20f;

        // Background
        GUI.Box(new Rect(panelX - 5f, panelY - 5f, panelWidth + 10f, panelHeight + 10f), "", backgroundStyle);

        // Reports
        for (int i = 0; i < activeReports.Count; i++)
        {
            var report = activeReports[i];
            float age = Time.time - report.timestamp;
            float alpha = 1f;

            // Fade out in last 2 seconds
            if (age > reportDisplayDuration - 2f)
                alpha = (reportDisplayDuration - age) / 2f;

            string hex = ColorUtility.ToHtmlStringRGB(report.color);
            string text = $"<color=#{hex}>{report.text}</color>";

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            Rect rect = new Rect(panelX, panelY + i * lineHeight, panelWidth, lineHeight);
            GUI.Label(rect, text, reportStyle);

            GUI.color = prevColor;
        }
    }

    private Color GetReportColor(AgentStatusReporter.ReportType type)
    {
        return type switch
        {
            AgentStatusReporter.ReportType.EnemySpotted => Color.red,
            AgentStatusReporter.ReportType.EnemyLost => Color.yellow,
            AgentStatusReporter.ReportType.TakingFire => new Color(1f, 0.5f, 0f),
            AgentStatusReporter.ReportType.HealthCritical => Color.red,
            AgentStatusReporter.ReportType.TargetNeutralized => Color.green,
            AgentStatusReporter.ReportType.ReachedPosition => Color.cyan,
            AgentStatusReporter.ReportType.AreaClear => Color.green,
            AgentStatusReporter.ReportType.RequestingBackup => Color.magenta,
            AgentStatusReporter.ReportType.AllyDown => Color.red,
            _ => Color.white
        };
    }
}