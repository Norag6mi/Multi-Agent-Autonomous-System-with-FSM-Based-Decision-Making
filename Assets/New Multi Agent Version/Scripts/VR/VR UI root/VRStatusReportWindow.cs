using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class VRStatusReportWindow : MonoBehaviour
{
    private class ReportEntry
    {
        public AgentStatusReporter.StatusReport report;
        public float timeRemaining;
    }

    [Header("References")]
    [SerializeField] private TextMeshProUGUI reportText;
    [SerializeField] private Transform followTarget;

    [Header("Placement")]
    [SerializeField] private Vector3 localOffset = new Vector3(-0.45f, 1.45f, 1.2f);
    [SerializeField] private bool faceTarget = true;

    [Header("Settings")]
    [SerializeField] private int maxVisible = 5;
    [SerializeField] private float displayDuration = 6f;

    private readonly List<ReportEntry> reports = new List<ReportEntry>();

    private void OnEnable()
    {
        AgentStatusReporter.OnStatusReport += HandleStatusReport;
    }

    private void OnDisable()
    {
        AgentStatusReporter.OnStatusReport -= HandleStatusReport;
    }

    private void Start()
    {
        if (followTarget == null && Camera.main != null)
            followTarget = Camera.main.transform;
    }

    private void Update()
    {
        UpdatePlacement();
        UpdateReportTimers();
        RefreshText();
    }

    private void UpdatePlacement()
    {
        if (followTarget == null) return;

        transform.position = followTarget.position
            + followTarget.right * localOffset.x
            + Vector3.up * localOffset.y
            + followTarget.forward * localOffset.z;

        if (faceTarget)
        {
            Vector3 lookDir = transform.position - followTarget.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }
    }

    private void HandleStatusReport(AgentStatusReporter.StatusReport report)
    {
        reports.Insert(0, new ReportEntry
        {
            report = report,
            timeRemaining = displayDuration
        });

        if (reports.Count > maxVisible)
            reports.RemoveAt(reports.Count - 1);
    }

    private void UpdateReportTimers()
    {
        for (int i = reports.Count - 1; i >= 0; i--)
        {
            reports[i].timeRemaining -= Time.deltaTime;
            if (reports[i].timeRemaining <= 0f)
                reports.RemoveAt(i);
        }
    }

    private void RefreshText()
    {
        if (reportText == null) return;

        if (reports.Count == 0)
        {
            reportText.text = "No recent reports";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Status Reports</b>");

        foreach (var entry in reports)
        {
            string color = GetReportColor(entry.report.type);
            sb.AppendLine($"<color={color}>{entry.report.agentName}: {entry.report.message}</color>");
        }

        reportText.text = sb.ToString();
    }

    private string GetReportColor(AgentStatusReporter.ReportType type)
    {
        switch (type)
        {
            case AgentStatusReporter.ReportType.EnemySpotted: return "#FFD966";
            case AgentStatusReporter.ReportType.EnemyLost: return "#CCCCCC";
            case AgentStatusReporter.ReportType.TakingFire: return "#FF9966";
            case AgentStatusReporter.ReportType.HealthCritical: return "#FF3333";
            case AgentStatusReporter.ReportType.TargetNeutralized: return "#66FF99";
            case AgentStatusReporter.ReportType.ReachedPosition: return "#66CCFF";
            case AgentStatusReporter.ReportType.AreaClear: return "#66FFFF";
            case AgentStatusReporter.ReportType.RequestingBackup: return "#FFCC66";
            case AgentStatusReporter.ReportType.AllyDown: return "#FF5555";
            default: return "#FFFFFF";
        }
    }

    public void ClearReports()
    {
        reports.Clear();
        if (reportText != null)
            reportText.text = "No recent reports";
    }
}