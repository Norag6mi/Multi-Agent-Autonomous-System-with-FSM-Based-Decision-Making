using UnityEngine;

public class DebriefUI : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float autoAdvanceDelay = 5f;

    private bool showing = false;
    private string debriefText = "";
    private MatchResetManager matchManager;
    private MatchConfiguration matchConfig;
    private float showTimer = 0f;

    private void Start()
    {
        matchManager = FindObjectOfType<MatchResetManager>();

        if (matchManager != null)
            matchConfig = matchManager.GetConfig();
    }

    // Helper to determine if the current match condition should advance automatically
    private bool ShouldAutoAdvance()
    {
        if (matchConfig == null) return false;

        return matchConfig.condition == MatchConfiguration.Condition.Autonomous ||
               matchConfig.condition == MatchConfiguration.Condition.VRCommanded;
    }

    public void ShowDebrief(int matchNumber, string winner, float duration,
        int alphaAlive, int BetaAlive, CommandQualityAnalyzer analyzer)
    {
        string commandInfo = "";
        if (analyzer != null)
        {
            commandInfo = $"\nCommand Relevance: {analyzer.GetRelevanceRate():F0}%" +
                $"\nCommand Effectiveness: {analyzer.GetEffectivenessRate():F0}%" +
                $"\nAvg Interval: {analyzer.GetAverageInterCommandInterval():F1}s";
        }

        // Updated to use the auto-advance helper
        bool autoAdvance = ShouldAutoAdvance();

        string advanceText = autoAdvance
            ? $"\n\nNext match in {autoAdvanceDelay:F0}s..."
            : "\n\nPress SPACE for next match";

        debriefText = $"MATCH {matchNumber} COMPLETE" +
            $"\n\nWinner: {winner}" +
            $"\nDuration: {duration:F1}s" +
            $"\nAlpha Surviving: {alphaAlive}" +
            $"\nBeta Surviving: {BetaAlive}" +
            commandInfo +
            advanceText;

        showing = true;
        showTimer = 0f;
    }

    private void Update()
    {
        if (!showing) return;

        // Updated to use the auto-advance helper
        if (ShouldAutoAdvance())
        {
            showTimer += Time.deltaTime;
            if (showTimer >= autoAdvanceDelay)
            {
                AdvanceToNextMatch();
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                AdvanceToNextMatch();
            }
        }
    }

    private void AdvanceToNextMatch()
    {
        showing = false;
        showTimer = 0f;
        if (matchManager != null)
            matchManager.OnReadyForNextMatch();
    }

    public bool IsShowing()
    {
        return showing;
    }

    private void OnGUI()
    {
        if (matchManager != null && matchManager.IsMatchActive())
        {
            float remaining = matchManager.GetConfig().matchTimeLimit - matchManager.GetMatchTimer();
            GUIStyle timerStyle = new GUIStyle(GUI.skin.label);
            timerStyle.fontSize = 20;
            timerStyle.fontStyle = FontStyle.Bold;
            timerStyle.alignment = TextAnchor.UpperCenter;
            timerStyle.normal.textColor = remaining < 30f ? Color.red : Color.white;

            GUI.Label(new Rect(Screen.width / 2 - 100, 10, 200, 40),
                $"Match {matchManager.GetCurrentMatch()} | {remaining:F0}s", timerStyle);
        }

        if (!showing) return;

        float panelW = 400f;
        float panelH = 300f;
        float panelX = Screen.width / 2 - panelW / 2;
        float panelY = Screen.height / 2 - panelH / 2;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), "");

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 16;
        style.alignment = TextAnchor.UpperCenter;
        style.normal.textColor = Color.white;
        style.wordWrap = true;

        GUI.Label(new Rect(panelX + 20, panelY + 20, panelW - 40, panelH - 40),
            debriefText, style);
    }
}
