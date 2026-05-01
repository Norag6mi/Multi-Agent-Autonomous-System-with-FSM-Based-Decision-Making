using UnityEngine;

public class MatchResetManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchConfiguration config;
    [SerializeField] private SpawnManager spawnManager;
    [SerializeField] private CommandQualityAnalyzer commandAnalyzer;
    [SerializeField] private FogOfWarSystem fogOfWar;

    [Header("UI")]
    [SerializeField] private DebriefUI debriefUI;

    private int currentMatch = 0;
    private float matchTimer = 0f;
    private bool matchActive = false;
    private bool waitingForReady = false;

    private void Start()
    {
        Invoke(nameof(StartFirstMatch), 1f);
    }

    private void StartFirstMatch()
    {
        currentMatch = 0;
        StartMatch();
    }

    private void Update()
    {
        if (!matchActive) return;

        matchTimer += Time.deltaTime;

        bool alphaEliminated = FactionManager.Instance.IsFactionEliminated(FactionType.Alpha);
        bool BetaEliminated = FactionManager.Instance.IsFactionEliminated(FactionType.Beta);

        if (alphaEliminated || BetaEliminated)
        {
            string winner = BetaEliminated ? "Alpha" : "Beta";
            EndMatch(winner);
            return;
        }

        if (matchTimer >= config.matchTimeLimit)
        {
            int alphaAlive = FactionManager.Instance.GetAliveCount(FactionType.Alpha);
            int BetaAlive = FactionManager.Instance.GetAliveCount(FactionType.Beta);

            string winner;
            if (alphaAlive > BetaAlive) winner = "Alpha";
            else if (BetaAlive > alphaAlive) winner = "Beta";
            else winner = "Draw";

            EndMatch(winner);
        }
    }

    private void StartMatch()
    {
        if (currentMatch >= config.numberOfMatches)
        {
            Debug.Log("[MATCH] All matches completed!");
            return;
        }

        currentMatch++;
        matchTimer = 0f;
        matchActive = true;
        waitingForReady = false;

        // Cancel any pending invokes to prevent double-start
        CancelInvoke();

        spawnManager.ResetAllAgents();

        if (commandAnalyzer != null)
            commandAnalyzer.ResetAnalyzer();

        if (fogOfWar != null)
            fogOfWar.ResetFog();

        MetricsLogger.Instance.StartMatch(currentMatch);

        Debug.Log($"[MATCH] Match {currentMatch}/{config.numberOfMatches} started");
    }

    private void EndMatch(string winner)
    {
        if (!matchActive) return; // Prevent double-end
        matchActive = false;

        MetricsLogger.Instance.EndMatch(winner);

        Debug.Log($"[MATCH] Match {currentMatch} ended. Winner: {winner}");

        int alphaAlive = FactionManager.Instance.GetAliveCount(FactionType.Alpha);
        int BetaAlive = FactionManager.Instance.GetAliveCount(FactionType.Beta);

        if (debriefUI != null)
        {
            debriefUI.ShowDebrief(currentMatch, winner, matchTimer,
                alphaAlive, BetaAlive, commandAnalyzer);
        }

        // Let DebriefUI handle ALL advancement (both auto and manual)
        waitingForReady = true;
    }

    public void OnReadyForNextMatch()
    {
        if (!waitingForReady) return;
        waitingForReady = false;
        StartMatch();
    }

    public float GetMatchTimer() => matchTimer;
    public int GetCurrentMatch() => currentMatch;
    public int GetTotalMatches() => config != null ? config.numberOfMatches : 0;
    public bool IsMatchActive() => matchActive;
    public MatchConfiguration GetConfig() => config;
}