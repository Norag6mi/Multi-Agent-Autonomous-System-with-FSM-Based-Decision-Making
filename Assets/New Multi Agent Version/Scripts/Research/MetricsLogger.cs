// Assets/Scripts/Research/MetricsLogger.cs
using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class MetricsLogger : MonoBehaviour
{
    public static MetricsLogger Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private MatchConfiguration config;

    private string dataFolder;
    private string sessionID;
    private int currentMatchID = 0;

    // Per-match tracking
    private float matchStartTime;
private int alphaKills, betaKills;
private int alphaCasualties, betaCasualties;
    private int totalShotsFired, totalShotsHit;
    private int commandsIssued, moveToCount, regroupCount, holdCount, releaseCount;
    private int commandsAccepted, commandsQueued, commandsRejected;
    private int broadcastCount;
    private List<float> detectionTimes = new List<float>();

    // Per-agent tracking
    private Dictionary<string, AgentMatchData> agentData = new Dictionary<string, AgentMatchData>();

    // Performance tracking
    private float perfTimer = 0f;
    private StringBuilder perfLog = new StringBuilder();

    // Snapshot tracking
    private float snapshotTimer = 0f;
    private StringBuilder snapshotLog = new StringBuilder();

    // Broadcast event tracking
    private StringBuilder broadcastLog = new StringBuilder();

    // First contact tracking
    private bool firstContactRecorded = false;
    private float firstContactTime = 0f;

    private class AgentMatchData
    {
        public string agentName;
        public string faction;
        public bool survived;
        public float survivalTime;
        public int kills;
        public int deaths;
        public int stateTransitions;
        public float timeInPatrol, timeInAlert, timeInEngage;
        public int commandsReceived, commandsExecuted, commandsOverridden;
        public int shotsHit, shotsMissed;
        public int healCount, reloadCount;
        public int broadcastsSent, broadcastsReceived;
        public AgentState lastState;
        public float lastStateChangeTime;
    }

        private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        string conditionName = config != null ? config.condition.ToString() : "UnknownCondition";
        string participant = config != null ? config.participantID : "P000";
        int session = config != null ? config.sessionNumber : 1;

        sessionID = $"{conditionName}_{participant}_S{session}_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        dataFolder = Path.Combine(Application.persistentDataPath, "Data", sessionID);

        Directory.CreateDirectory(dataFolder);

        Debug.Log($"[METRICS] Data folder: {dataFolder}");
    }

    private void Start()
    {
        SubscribeToEvents();

    }

    private void Update()
    {
        if (config == null) return;

        // Performance logging
        if (config.logPerformanceData)
        {
            perfTimer += Time.deltaTime;
            if (perfTimer >= config.performanceLogInterval)
            {
                perfTimer = 0f;
                LogPerformance();
            }
        }

        // Position/state snapshots
        snapshotTimer += Time.deltaTime;
        if (snapshotTimer >= config.snapshotLogInterval)
        {
            snapshotTimer = 0f;
            LogSnapshot();
        }

        // Track per-agent state time
        UpdateAgentStateTimes();
    }

    // Match lifecycle

    public void StartMatch(int matchID)
    {
        currentMatchID = matchID;
        matchStartTime = Time.time;
        alphaKills = betaKills = 0;
        alphaCasualties = betaCasualties = 0;
        totalShotsFired = totalShotsHit = 0;
        commandsIssued = moveToCount = regroupCount = holdCount = releaseCount = 0;
        commandsAccepted = commandsQueued = commandsRejected = 0;
        broadcastCount = 0;
        detectionTimes.Clear();
        agentData.Clear();

        firstContactRecorded = false;
        firstContactTime = 0f;

        InitializeAgentData();
        Debug.Log($"[METRICS] Match {matchID} started");
    }

    public void EndMatch(string winner)
    {
        float duration = Time.time - matchStartTime;
        float avgDetectionTime = detectionTimes.Count > 0 ? Average(detectionTimes) : 0f;
        float ammoEfficiency = totalShotsFired > 0 ? (float)totalShotsHit / totalShotsFired * 100f : 0f;
        float complianceRate = commandsIssued > 0 ? (float)commandsAccepted / commandsIssued * 100f : 0f;

        // Write match CSV
        string matchFile = Path.Combine(dataFolder, "match_data.csv");
        bool writeHeader = !File.Exists(matchFile);

        StringBuilder sb = new StringBuilder();
        if (writeHeader)
        {
            sb.AppendLine("matchID,condition,participantID,sessionNumber,duration,winner," +
            "alphaKills,betaKills,alphaCasualties,betaCasualties," +
            "totalCommands,moveTo,regroup,hold,release," +
            "accepted,queued,rejected,complianceRate," +
            "broadcastCount,avgDetectionTime," +
            "shotsFired,shotsHit,ammoEfficiency");
        }

        sb.AppendLine($"{currentMatchID},{config.condition},{config.participantID},{config.sessionNumber}," +
        $"{duration:F2},{winner}," +
        $"{alphaKills},{betaKills},{alphaCasualties},{betaCasualties}," + // <-- Check Beta vs Bravo here
        $"{commandsIssued},{moveToCount},{regroupCount},{holdCount},{releaseCount}," +
        $"{commandsAccepted},{commandsQueued},{commandsRejected},{complianceRate:F1}," +
        $"{broadcastCount},{avgDetectionTime:F2}," +
        $"{totalShotsFired},{totalShotsHit},{ammoEfficiency:F1}");

        File.AppendAllText(matchFile, sb.ToString());

        // Write per-agent CSV
        WriteAgentData();

        // Flush snapshot and broadcast logs
        FlushLogs();

        Debug.Log($"[METRICS] Match {currentMatchID} ended. Winner: {winner}, Duration: {duration:F1}s");
    }

    // Initialization

    private void InitializeAgentData()
    {
        var allAgents = new List<AgentIdentity>();
        allAgents.AddRange(FactionManager.Instance.GetFactionMembers(FactionType.Alpha));
        allAgents.AddRange(FactionManager.Instance.GetFactionMembers(FactionType.Beta));

        foreach (var agent in allAgents)
        {
            agentData[agent.agentName] = new AgentMatchData
            {
                agentName = agent.agentName,
                faction = agent.Faction.ToString(),
                survived = true,
                lastState = AgentState.Patrol,
                lastStateChangeTime = Time.time
            };
        }
    }

    // Event subscriptions

    private void SubscribeToEvents()
    {
        CommandReceiver.OnCommandReceived += HandleCommandReceived;
        CommandReceiver.OnCommandOverridden += HandleCommandOverridden;
        AgentCoordinator.OnThreatBroadcast += HandleBroadcast;
    }

    private void OnDestroy()
    {
        CommandReceiver.OnCommandReceived -= HandleCommandReceived;
        CommandReceiver.OnCommandOverridden -= HandleCommandOverridden;
        AgentCoordinator.OnThreatBroadcast -= HandleBroadcast;
        FlushLogs();
    }

    // Event handlers

    private void HandleCommandReceived(string agentName, CommandReceiver.CommandType type,
        Vector3 pos, AgentState state, CommandReceiver.CommandStatus status)
    {
        commandsIssued++;
        switch (type)
        {
            case CommandReceiver.CommandType.MoveTo: moveToCount++; break;
            case CommandReceiver.CommandType.Regroup: regroupCount++; break;
            case CommandReceiver.CommandType.Hold: holdCount++; break;
        }

        switch (status)
        {
            case CommandReceiver.CommandStatus.Accepted: commandsAccepted++; break;
            case CommandReceiver.CommandStatus.Queued: commandsQueued++; break;
            case CommandReceiver.CommandStatus.Rejected: commandsRejected++; break;
        }

        if (agentData.ContainsKey(agentName))
        {
            agentData[agentName].commandsReceived++;
            if (status == CommandReceiver.CommandStatus.Accepted)
                agentData[agentName].commandsExecuted++;
        }
    }

    private void HandleCommandOverridden(string agentName, CommandReceiver.CommandType type)
    {
        if (agentData.ContainsKey(agentName))
            agentData[agentName].commandsOverridden++;
    }

    private void HandleBroadcast(AgentIdentity broadcaster, Vector3 pos, List<AgentIdentity> receivers)
    {
        broadcastCount++;

        if (agentData.ContainsKey(broadcaster.agentName))
            agentData[broadcaster.agentName].broadcastsSent++;

        foreach (var receiver in receivers)
        {
            if (agentData.ContainsKey(receiver.agentName))
                agentData[receiver.agentName].broadcastsReceived++;
        }

        // Log broadcast event
        foreach (var receiver in receivers)
        {
            AgentFSM receiverFSM = receiver.GetComponent<AgentFSM>();
            string receiverState = receiverFSM != null ? receiverFSM.CurrentStateKey.ToString() : "Unknown";

            broadcastLog.AppendLine($"{Time.time:F3},{currentMatchID},{config.condition},{broadcaster.agentName},{receiver.agentName}," +
            $"enemy,{broadcaster.GetComponent<AgentFSM>()?.CurrentStateKey},{receiverState}");
        }
    }

    // Public recording methods (called by other systems)

    public void RecordKill(string killerName, string victimName)
    {
        AgentIdentity killer = FindAgent(killerName);
        AgentIdentity victim = FindAgent(victimName);

        if (killer != null && killer.Faction == FactionType.Alpha) alphaKills++;
        if (killer != null && killer.Faction == FactionType.Beta) betaKills++;
        if (victim != null && victim.Faction == FactionType.Alpha) alphaCasualties++;
        if (victim != null && victim.Faction == FactionType.Beta) betaCasualties++;

        if (agentData.ContainsKey(killerName)) agentData[killerName].kills++;
        if (agentData.ContainsKey(victimName))
        {
            agentData[victimName].deaths++;
            agentData[victimName].survived = false;
            agentData[victimName].survivalTime = Time.time - matchStartTime;
        }
    }

    public void RecordShotFired(string agentName)
    {
        totalShotsFired++;

        if (agentData.ContainsKey(agentName))
            agentData[agentName].shotsMissed++;
    }

    public void RecordShotHit(string agentName)
    {
        totalShotsHit++;

        if (agentData.ContainsKey(agentName))
        {
            agentData[agentName].shotsHit++;
            // Remove the miss we added when fired
            agentData[agentName].shotsMissed =
                Mathf.Max(0, agentData[agentName].shotsMissed - 1);
        }
    }

    public void RecordHeal(string agentName)
    {
        if (agentData.ContainsKey(agentName))
            agentData[agentName].healCount++;
    }

    public void RecordReload(string agentName)
    {
        if (agentData.ContainsKey(agentName))
            agentData[agentName].reloadCount++;
    }

    public void RecordDetection(float detectionTime)
    {
        detectionTimes.Add(detectionTime);
    }

    public void RecordStateTransition(string agentName, AgentState newState)
    {
        if (!agentData.ContainsKey(agentName)) return;

        var data = agentData[agentName];

        float elapsed = Time.time - data.lastStateChangeTime;

        switch (data.lastState)
        {
            case AgentState.Patrol: data.timeInPatrol += elapsed; break;
            case AgentState.Alert: data.timeInAlert += elapsed; break;
            case AgentState.Engage: data.timeInEngage += elapsed; break;
        }

        // ✅ FIRST CONTACT DETECTION
        if (!firstContactRecorded &&
            data.lastState == AgentState.Patrol &&
            newState == AgentState.Alert)
        {
            firstContactTime = Time.time - matchStartTime;
            firstContactRecorded = true;

            detectionTimes.Add(firstContactTime);

            Debug.Log($"[METRICS] First contact at {firstContactTime:F2}s");
        }

        data.stateTransitions++;
        data.lastState = newState;
        data.lastStateChangeTime = Time.time;
    }

    // State time tracking (called every frame)

    private void UpdateAgentStateTimes()
    {
        var allAgents = new List<AgentIdentity>();
        allAgents.AddRange(FactionManager.Instance.GetFactionMembers(FactionType.Alpha));
        allAgents.AddRange(FactionManager.Instance.GetFactionMembers(FactionType.Beta));

        foreach (var agent in allAgents)
        {
            if (agent == null || !agentData.ContainsKey(agent.agentName)) continue;

            AgentFSM fsm = agent.GetComponent<AgentFSM>();
            if (fsm == null) continue;

            AgentState currentState = fsm.CurrentStateKey;
            var data = agentData[agent.agentName];

            if (currentState != data.lastState)
            {
                RecordStateTransition(agent.agentName, currentState);
            }
        }
    }

    // Per-agent CSV

    private void WriteAgentData()
    {
        string agentFile = Path.Combine(dataFolder, "agent_data.csv");
        bool writeHeader = !File.Exists(agentFile);

        StringBuilder sb = new StringBuilder();
        if (writeHeader)
        {
            sb.AppendLine("matchID,condition,participantID,agentName,faction,survived,survivalTime," +
            "kills,deaths,stateTransitions," +
            "timeInPatrol,timeInAlert,timeInEngage," +
            "commandsReceived,commandsExecuted,commandsOverridden," +
            "shotsHit,shotsMissed,healCount,reloadCount," +
            "broadcastsSent,broadcastsReceived");
        }

        foreach (var kvp in agentData)
        {
            var d = kvp.Value;
            // Finalize state times
            float elapsed = Time.time - d.lastStateChangeTime;
            switch (d.lastState)
            {
                case AgentState.Patrol: d.timeInPatrol += elapsed; break;
                case AgentState.Alert: d.timeInAlert += elapsed; break;
                case AgentState.Engage: d.timeInEngage += elapsed; break;
            }

            float survTime = d.survived ? Time.time - matchStartTime : d.survivalTime;

            sb.AppendLine($"{currentMatchID},{config.condition},{config.participantID},{d.agentName},{d.faction},{d.survived},{survTime:F2}," +
            $"{d.kills},{d.deaths},{d.stateTransitions}," +
            $"{d.timeInPatrol:F2},{d.timeInAlert:F2},{d.timeInEngage:F2}," +
            $"{d.commandsReceived},{d.commandsExecuted},{d.commandsOverridden}," +
            $"{d.shotsHit},{d.shotsMissed},{d.healCount},{d.reloadCount}," +
            $"{d.broadcastsSent},{d.broadcastsReceived}");

        }

        File.AppendAllText(agentFile, sb.ToString());


    }

    // Performance logging

    private void WritePerfHeader()
    {
        perfLog.AppendLine("timestamp,fps,cpuFrameTime,gpuFrameTime,memoryMB");
    }

    private void LogPerformance()
    {
        float fps = 1f / Time.unscaledDeltaTime;
        float cpu = Time.unscaledDeltaTime * 1000f;
        float memory = (float)System.GC.GetTotalMemory(false) / (1024f * 1024f);

        perfLog.AppendLine($"{Time.time:F2},{currentMatchID},{config.condition},{fps:F1},{cpu:F2},0,{memory:F1}");
    }

    // Snapshot logging

    private void WriteSnapshotHeader()
    {
        snapshotLog.AppendLine("timestamp,agentName,faction,state,posX,posY,posZ," +
            "targetName,awareness,inCombat");
    }

    private void LogSnapshot()
    {
        var allAgents = new List<AgentIdentity>();
        allAgents.AddRange(FactionManager.Instance.GetFactionMembers(FactionType.Alpha));
        allAgents.AddRange(FactionManager.Instance.GetFactionMembers(FactionType.Beta));

        foreach (var agent in allAgents)
        {
            if (agent == null) continue;

            AgentFSM fsm = agent.GetComponent<AgentFSM>();
            AwarenessModel awareness = agent.GetComponent<AwarenessModel>();

            if (fsm == null) continue;

            string state = fsm.CurrentStateKey.ToString();
            Vector3 pos = agent.transform.position;
            string target = awareness?.currentTarget != null ? awareness.currentTarget.name : "none";
            float awareVal = awareness != null ? awareness.awareness : 0f;
            bool inCombat = fsm.CurrentStateKey == AgentState.Engage;

            snapshotLog.AppendLine($"{Time.time:F3},{currentMatchID},{config.condition},{agent.agentName},{agent.Faction}," +
            $"{state},{pos.x:F2},{pos.y:F2},{pos.z:F2}," +
            $"{target},{awareVal:F1},{inCombat}");
        }
    }

    // Broadcast logging header

    private void WriteBroadcastHeader()
    {
        broadcastLog.AppendLine("timestamp,broadcaster,receiver,threat,broadcasterState,receiverState");
    }

    // Flush all logs to files

    private void FlushLogs()
    {
        FlushBufferToFile(
            "performance.csv",
            perfLog,
            "timestamp,matchID,condition,fps,cpuFrameTime,gpuFrameTime,memoryMB");

        FlushBufferToFile(
            "snapshots.csv",
            snapshotLog,
            "timestamp,matchID,condition,agentName,faction,state,posX,posY,posZ,targetName,awareness,inCombat");

        FlushBufferToFile(
            "broadcasts.csv",
            broadcastLog,
            "timestamp,matchID,condition,broadcaster,receiver,threat,broadcasterState,receiverState");
    }

    private void FlushBufferToFile(string fileName, StringBuilder buffer, string header)
    {
        if (buffer.Length <= 0) return;

        string filePath = Path.Combine(dataFolder, fileName);
        bool writeHeader = !File.Exists(filePath);

        StringBuilder output = new StringBuilder();

        if (writeHeader)
            output.AppendLine(header);

        output.Append(buffer.ToString());
        File.AppendAllText(filePath, output.ToString());

        buffer.Clear();
    }

    // Helpers

    private float Average(List<float> list)
    {
        float sum = 0f;
        foreach (float v in list) sum += v;
        return list.Count > 0 ? sum / list.Count : 0f;
    }

    private AgentIdentity FindAgent(string name)
    {
        var all = new List<AgentIdentity>();
        all.AddRange(FactionManager.Instance.GetFactionMembers(FactionType.Alpha));
        all.AddRange(FactionManager.Instance.GetFactionMembers(FactionType.Beta));
        foreach (var a in all)
            if (a.agentName == name) return a;
        return null;
    }

    public int GetCurrentMatchID() => currentMatchID;
    public string GetDataFolder() => dataFolder;


    private string GetDataPath()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "Data");
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);
        return path;
    }

    private void OnApplicationQuit()
    {
        FlushLogs();
        Debug.Log($"[METRICS] Logs flushed on application quit: {dataFolder}");
    }





}