using UnityEngine;
using System;

public class CommandReceiver : MonoBehaviour
{
    public enum CommandType { None, MoveTo, Regroup, Hold }
    public enum CommandStatus { Accepted, Queued, Rejected }

    // Events for MetricsLogger
    public static event Action<string, CommandType, Vector3, AgentState, CommandStatus> OnCommandReceived;
    public static event Action<string, Vector3> OnCommandCompleted;
    public static event Action<string, CommandType> OnCommandOverridden;

    [Header("Acknowledgment")]
    [SerializeField] private float flashDuration = 0.4f;

    [Header("Command Settings")]
    [SerializeField] private float destinationReachedThreshold = 1.5f;

    // Colors
    private readonly Color acceptedColor = Color.white;
    private readonly Color queuedColor = new Color(1f, 0.6f, 0f);
    private readonly Color rejectedColor = Color.red;
    private readonly Color completedColor = Color.green;
    private readonly Color overriddenColor = Color.yellow;

    private AgentFSM fsm;
    private Renderer indicatorRenderer;

    // Command state
    private CommandType currentCommand = CommandType.None;
    private Vector3 commandTargetPosition;
    private bool hasActiveCommand = false;
    private bool isMovingToCommandTarget = false;

    // Pending command (queued during combat)
    private CommandType pendingCommand = CommandType.None;
    private Vector3 pendingTargetPosition;
    private bool hasPendingCommand = false;

    // Tracking
    private bool wasInCombat = false;
    private float flashTimer = 0f;
    private bool isFlashing = false;

    // Public properties
    public bool HasPendingCommand => hasPendingCommand;
    public bool HasActiveCommand => hasActiveCommand;
    public CommandType CurrentCommand => currentCommand;
    public bool IsHolding => hasActiveCommand && currentCommand == CommandType.Hold;
    public bool IsUnderCommand() => hasActiveCommand;
    public Vector3 GetCommandTargetPosition() => hasActiveCommand ? commandTargetPosition : transform.position;

// In CommandReceiver.cs, replace Awake and Start with:

    private void Awake()
    {
        fsm = GetComponent<AgentFSM>();
        if (fsm == null)
            Debug.LogError($"[COMMAND] {gameObject.name}: No AgentFSM found!");
    }

    private void Start()
    {
        // Delay indicator caching to ensure AgentFSM.Start() has run
        Invoke(nameof(LateInit), 0.1f);
    }

    private void LateInit()
    {
        if (fsm.StateIndicator != null)
            indicatorRenderer = fsm.StateIndicator.GetComponent<Renderer>();

        if (fsm.Patrol == null || fsm.Navigation == null)
            Debug.LogError($"[COMMAND] {gameObject.name}: FSM subsystems still null after delay!");
    }

    
    //  Command Reception 

    public CommandStatus ReceiveMoveOrder(Vector3 position)
    {

        Debug.Log($"[COMMAND] {gameObject.name}: ReceiveMoveOrder called. State={GetCurrentState()}, IsDead={IsDead()}, InCombat={IsInCombatState()}");


        if (IsDead()) return RejectCommand(CommandType.MoveTo, position);
        if (IsInCombatState()) return QueueCommand(CommandType.MoveTo, position);
        ExecuteMoveOrder(position);
        FlashIndicator(acceptedColor);
        OnCommandReceived?.Invoke(gameObject.name, CommandType.MoveTo, position, GetCurrentState(), CommandStatus.Accepted);
        return CommandStatus.Accepted;
    }

    public CommandStatus ReceiveRegroup(Vector3 position)
    {
        if (IsDead()) return RejectCommand(CommandType.Regroup, position);
        if (IsInCombatState()) return QueueCommand(CommandType.Regroup, position);
        ExecuteMoveOrder(position);
        currentCommand = CommandType.Regroup;
        FlashIndicator(acceptedColor);
        OnCommandReceived?.Invoke(gameObject.name, CommandType.Regroup, position, GetCurrentState(), CommandStatus.Accepted);
        return CommandStatus.Accepted;
    }

    public CommandStatus ReceiveHold()
    {
        if (IsDead()) return RejectCommand(CommandType.Hold, transform.position);
        if (IsInCombatState()) return QueueCommand(CommandType.Hold, transform.position);
        ExecuteHold();
        FlashIndicator(acceptedColor);
        OnCommandReceived?.Invoke(gameObject.name, CommandType.Hold, transform.position, GetCurrentState(), CommandStatus.Accepted);
        return CommandStatus.Accepted;
    }

    public CommandStatus ReceiveRelease()
    {
        if (IsDead())
        {
            FlashIndicator(rejectedColor);
            return CommandStatus.Rejected;
        }
        ClearAllCommands();
        FlashIndicator(acceptedColor);
        OnCommandReceived?.Invoke(gameObject.name, CommandType.None, transform.position, GetCurrentState(), CommandStatus.Accepted);
        return CommandStatus.Accepted;
    }

    //  Helpers for Reception 

    private CommandStatus RejectCommand(CommandType type, Vector3 pos)
    {
        FlashIndicator(rejectedColor);
        OnCommandReceived?.Invoke(gameObject.name, type, pos, GetCurrentState(), CommandStatus.Rejected);
        return CommandStatus.Rejected;
    }

    private CommandStatus QueueCommand(CommandType type, Vector3 pos)
    {
        pendingCommand = type;
        pendingTargetPosition = pos;
        hasPendingCommand = true;
        FlashIndicator(queuedColor);
        OnCommandReceived?.Invoke(gameObject.name, type, pos, GetCurrentState(), CommandStatus.Queued);
        return CommandStatus.Queued;
    }

    // Update

    private void Update()
    {
        UpdateFlash();
        CheckCombatStateTransitions();
        CheckCommandDestinationReached();
    }

    private void CheckCombatStateTransitions()
    {
        bool inCombat = IsInCombatState();

        // Returned from combat → execute pending command
        if (wasInCombat && !inCombat && hasPendingCommand)
        {
            ExecutePendingCommand();
        }

        // Entered combat while executing command → override
        if (!wasInCombat && inCombat && hasActiveCommand && isMovingToCommandTarget)
        {
            FlashIndicator(overriddenColor);
            OnCommandOverridden?.Invoke(gameObject.name, currentCommand);

            // Save current command as pending so it resumes after combat
            pendingCommand = currentCommand;
            pendingTargetPosition = commandTargetPosition;
            hasPendingCommand = true;
            isMovingToCommandTarget = false;
        }

        wasInCombat = inCombat;
    }

    private void CheckCommandDestinationReached()
    {
        if (!isMovingToCommandTarget || IsInCombatState()) return;

        if (Vector3.Distance(transform.position, commandTargetPosition) <= destinationReachedThreshold)
        {
            isMovingToCommandTarget = false;
            FlashIndicator(completedColor);
            OnCommandCompleted?.Invoke(gameObject.name, commandTargetPosition);

            if (currentCommand == CommandType.Hold)
                fsm.Navigation.Stop();
        }
    }

    //  Command Execution 

    private void ExecuteMoveOrder(Vector3 position)
    {
        currentCommand = CommandType.MoveTo;
        commandTargetPosition = position;
        hasActiveCommand = true;
        isMovingToCommandTarget = true;
        hasPendingCommand = false;

        fsm.Patrol.SetCommandOverride(position);
        fsm.Navigation.PatrolTo(position);
    }

    private void ExecuteHold()
    {
        currentCommand = CommandType.Hold;
        commandTargetPosition = transform.position;
        hasActiveCommand = true;
        isMovingToCommandTarget = false;
        hasPendingCommand = false;

        fsm.Patrol.SetHoldMode(true);
        fsm.Navigation.Stop();
    }

    private void ExecutePendingCommand()
    {
        switch (pendingCommand)
        {
            case CommandType.MoveTo:
            case CommandType.Regroup:
                ExecuteMoveOrder(pendingTargetPosition);
                currentCommand = pendingCommand;
                break;
            case CommandType.Hold:
                ExecuteHold();
                break;
        }

        hasPendingCommand = false;
        pendingCommand = CommandType.None;
        FlashIndicator(acceptedColor);
    }

    public void ClearAllCommands()
    {
        currentCommand = CommandType.None;
        hasActiveCommand = false;
        isMovingToCommandTarget = false;
        pendingCommand = CommandType.None;
        hasPendingCommand = false;

        fsm.Patrol.ClearCommandOverride();
        fsm.Patrol.SetHoldMode(false);
    }

    // State Queries 

    private AgentState GetCurrentState()
    {
        // Access the current state key from StateManager
        return fsm.CurrentStateKey;
    }

    private bool IsInCombatState()
    {
        AgentState state = GetCurrentState();
        return state == AgentState.Alert || state == AgentState.Engage;
    }

    private bool IsDead()
    {
        return GetCurrentState() == AgentState.Dead;
    }

    // Flash System 

    private void FlashIndicator(Color color)
    {
        if (indicatorRenderer == null) return;
        indicatorRenderer.material.color = color;
        flashTimer = flashDuration;
        isFlashing = true;
    }

    private void UpdateFlash()
    {
        if (!isFlashing) return;
        flashTimer -= Time.deltaTime;
        if (flashTimer <= 0f)
        {
            isFlashing = false;
            // Restore FSM state color
            indicatorRenderer.material.color = GetCurrentState() switch
            {
                AgentState.Patrol => Color.green,
                AgentState.Alert => Color.yellow,
                AgentState.Engage => Color.red,
                AgentState.Dead => Color.black,
                _ => Color.white
            };
        }
    }
}