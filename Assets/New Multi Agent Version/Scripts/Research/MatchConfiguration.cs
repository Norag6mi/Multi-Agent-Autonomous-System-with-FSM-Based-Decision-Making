// Assets/Scripts/Research/MatchConfiguration.cs
using UnityEngine;

[CreateAssetMenu(fileName = "MatchConfig", menuName = "Research/Match Configuration")]
public class MatchConfiguration : ScriptableObject
{
    public enum Condition
    {
        Autonomous,
        VRCommanded,
        DesktopCommanded
    }

    [Header("Match Settings")]
    public Condition condition = Condition.Autonomous;
    public int numberOfMatches = 30;
    public float matchTimeLimit = 180f;

    [Header("Participant")]
    public string participantID = "P001";
    public int sessionNumber = 1;

    [Header("Options")]
    public bool enableVisualizations = true;
    public bool autoStartNextMatch = true;
    public bool logPerformanceData = true;
    public float performanceLogInterval = 5f;
    public float snapshotLogInterval = 0.5f;
}