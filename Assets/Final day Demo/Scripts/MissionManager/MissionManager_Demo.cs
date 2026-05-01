using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MissionManager_Demo : MonoBehaviour
{
    public static MissionManager_Demo Instance { get; private set; }

    public enum MissionState
    {
        WaitingToStart,
        InProgress,
        Success,
        Failed
    }

    [Header("Mission Settings")]
    [SerializeField] private float missionDuration = 300f;
    [SerializeField] private float countdownBeforeStart = 3f;

    [Header("Objectives")]
    [SerializeField] private List<MissionObjective> objectives = new List<MissionObjective>();

    [Header("UI References")]
    [SerializeField] private TimerUI timerUI;
    [SerializeField] private MissionEndUI missionEndUI;

    [Header("Agent References")]
    [SerializeField] private List<GameObject> alphaAgents = new List<GameObject>();

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;
    [SerializeField] private bool autoStartOnPlay = true;

    private float timeRemaining;
    private int objectivesCompleted = 0;
    private MissionState currentState = MissionState.WaitingToStart;
    private float missionStartTime;

    public MissionState CurrentState => currentState;
    public float TimeRemaining => timeRemaining;
    public float TimeRemainingNormalized => timeRemaining / missionDuration;
    public int ObjectivesCompleted => objectivesCompleted;
    public int TotalObjectives => objectives.Count;
    public bool IsMissionActive => currentState == MissionState.InProgress;

    public static event System.Action OnMissionStart;
    public static event System.Action OnMissionSuccess;
    public static event System.Action OnMissionFailed;

    // 

    private readonly HashSet<MissionObjective> completedObjectives = new HashSet<MissionObjective>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        MissionObjective.OnObjectiveComplete += HandleObjectiveComplete;
        VRCommander_Demo_v2.OnPlayerDied += HandlePlayerDied;

        Debug.Log("[MissionManager] Subscribed to VRCommander_Demo_v2.OnPlayerDied in Awake.");
    }

    private void OnDestroy()
    {
        MissionObjective.OnObjectiveComplete -= HandleObjectiveComplete;
        VRCommander_Demo_v2.OnPlayerDied -= HandlePlayerDied;
    }


    // Add this field at the top with other fields
    // Change existing private list to expose via getter
    // The list is already there as:
    // [SerializeField] private List<MissionObjective> objectives

    // Add this public getter method:
    public List<MissionObjective> GetObjectives()
    {
        return objectives;
    }


    private void Start()
    {
        timeRemaining = missionDuration;
        ValidateSetup();

        if (autoStartOnPlay)
            StartCoroutine(CountdownThenStart());

        completedObjectives.Clear();
        objectivesCompleted = 0;
    }

    private void ValidateSetup()
    {
        // Remove null entries from objectives list
        objectives.RemoveAll(o => o == null);

        if (objectives.Count == 0)
            Debug.LogError("[MissionManager] No objectives assigned!");

        if (timerUI == null)
            Debug.LogWarning("[MissionManager] TimerUI not assigned.");

        if (missionEndUI == null)
            Debug.LogError("[MissionManager] MissionEndUI not assigned — " +
                           "end screen will not show.");

        if (debugLog)
            Debug.Log($"[MissionManager] Setup valid. " +
                      $"Objectives: {objectives.Count}, " +
                      $"Agents: {alphaAgents.Count}");
    }

    private void Update()
    {
        if (currentState != MissionState.InProgress) return;

        timeRemaining -= Time.deltaTime;
        timerUI?.UpdateTimer(timeRemaining, missionDuration);

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            MissionFailed();
        }
    }

    // ── Mission Flow ─────────────────────────────────────

    private IEnumerator CountdownThenStart()
    {
        // Show countdown, hide timer
        timerUI?.SetTimerVisible(false);
        timerUI?.ShowCountdown(true);

        float t = countdownBeforeStart;
        while (t > 0f)
        {
            timerUI?.UpdateCountdown(Mathf.CeilToInt(t));

            if (debugLog)
                Debug.Log($"[MissionManager] Starting in {Mathf.CeilToInt(t)}...");

            yield return null;
            t -= Time.deltaTime;
        }

        timerUI?.UpdateCountdown(0);
        yield return new WaitForSeconds(0.3f);

        timerUI?.ShowCountdown(false);
        timerUI?.SetTimerVisible(true);

        StartMission();
    }

    public void StartMission()
    {
        if (currentState != MissionState.WaitingToStart) return;

        currentState = MissionState.InProgress;
        missionStartTime = Time.time;
        objectivesCompleted = 0;
        timeRemaining = missionDuration;

        foreach (var obj in objectives)
        {
            if (obj != null)
                obj.Activate();
        }

        OnMissionStart?.Invoke();

        if (debugLog)
            Debug.Log($"[MissionManager] Mission STARTED. " +
                      $"Total objectives to complete: {objectives.Count}");
    }

    private void HandleObjectiveComplete(MissionObjective objective)
    {
        if (currentState != MissionState.InProgress) return;
        if (objective == null) return;
        if (!completedObjectives.Add(objective)) return; // ignore duplicates

        objectivesCompleted = completedObjectives.Count;

        if (debugLog)
            Debug.Log($"[MissionManager] '{objective.ObjectiveName}' complete. Progress: {objectivesCompleted}/{objectives.Count}");

        if (objectivesCompleted >= objectives.Count)
            MissionSuccess();
    }

    private void HandlePlayerDied()
    {
        Debug.LogError("[MissionManager] HandlePlayerDied() received! " +
                    $"Current state: {currentState}");

        if (currentState != MissionState.InProgress)
        {
            Debug.LogWarning("[MissionManager] Player died but mission not InProgress — ignoring.");
            return;
        }

        MissionFailed();
    }

    private System.Collections.IEnumerator DelayedMissionFailed()
    {
        // Small delay to let death animation/effects play
        yield return new WaitForSecondsRealtime(0.5f);
        
        MissionFailed();
    }

    private void MissionSuccess()
    {
        if (currentState != MissionState.InProgress) return;

        currentState = MissionState.Success;
        float timeTaken = Time.time - missionStartTime;
        int agentsAlive = CountAliveAgents();

        if (debugLog)
            Debug.Log($"[MissionManager] MISSION SUCCESS. " +
                      $"Time: {FormatTime(timeTaken)}, " +
                      $"Agents alive: {agentsAlive}/{alphaAgents.Count}");

        OnMissionSuccess?.Invoke();

        if (missionEndUI != null)
            missionEndUI.ShowSuccess(timeTaken, objectivesCompleted,
                                     objectives.Count, agentsAlive);
        else
            Debug.LogError("[MissionManager] MissionEndUI is null — " +
                           "cannot show success screen. Check Inspector assignment.");
    }

    private void MissionFailed()
    {
        if (currentState != MissionState.InProgress) return;

        currentState = MissionState.Failed;
        int agentsAlive = CountAliveAgents();

        if (debugLog)
            Debug.Log($"[MissionManager] MISSION FAILED. " +
                    $"Objectives done: {objectivesCompleted}/{objectives.Count}");

        OnMissionFailed?.Invoke();

        if (missionEndUI != null)
            missionEndUI.ShowFailure(objectivesCompleted,
                                    objectives.Count, agentsAlive);
        else
            Debug.LogError("[MissionManager] MissionEndUI is null — " +
                        "cannot show failure screen.");

        // Pause game and unlock cursor
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ── Utility ──────────────────────────────────────────

    private int CountAliveAgents()
    {
        int alive = 0;
        foreach (var agent in alphaAgents)
        {
            if (agent == null) continue;
            HealthComponent h = agent.GetComponent<HealthComponent>();
            if (h != null && !h.Model.IsDead)
                alive++;
        }
        return alive;
    }

    public static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }

    public MissionResultData GetResultData()
    {
        return new MissionResultData
        {
            success = currentState == MissionState.Success,
            timeTaken = Time.time - missionStartTime,
            timeRemaining = timeRemaining,
            objectivesCompleted = objectivesCompleted,
            totalObjectives = objectives.Count,
            agentsAlive = CountAliveAgents(),
            totalAgents = alphaAgents.Count
        };
    }
}

[System.Serializable]
public class MissionResultData
{
    public bool success;
    public float timeTaken;
    public float timeRemaining;
    public int objectivesCompleted;
    public int totalObjectives;
    public int agentsAlive;
    public int totalAgents;
}