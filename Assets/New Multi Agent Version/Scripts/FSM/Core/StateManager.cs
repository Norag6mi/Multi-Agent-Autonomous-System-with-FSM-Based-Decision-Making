using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Generic FSM engine. Manages state dictionary, transitions, and update loop.
/// Zero domain knowledge — doesn't know about awareness, combat, or navigation.
/// Subclasses (like AgentFSM) inject domain-specific context.
/// </summary>
public abstract class StateManager<EState> : MonoBehaviour where EState : Enum
{
    protected Dictionary<EState, BaseState<EState>> States = new Dictionary<EState, BaseState<EState>>();
    protected BaseState<EState> CurrentState;
    private bool isTransitioning = false;

    // Exposed for debug HUD
    public EState CurrentStateKey => CurrentState != null ? CurrentState.StateKey : default;

    protected virtual void Start()
    {
        if (CurrentState == null)
        {
            Debug.LogError($"[FSM] {gameObject.name}: No initial state set!");
            return;
        }
        CurrentState.EnterState();
    }

    protected virtual void Update()
    {
        if (CurrentState == null || isTransitioning) return;

        EState nextStateKey = CurrentState.GetNextState();

        if (nextStateKey.Equals(CurrentState.StateKey))
        {
            // Same state — keep updating
            CurrentState.UpdateState();
        }
        else
        {
            // Different state — transition
            TransitionToState(nextStateKey);
        }
    }

    public void TransitionToState(EState newStateKey)
    {
        if (isTransitioning) return;
        if (!States.ContainsKey(newStateKey))
        {
            Debug.LogError($"[FSM] {gameObject.name}: State {newStateKey} not found!");
            return;
        }

        isTransitioning = true;

        CurrentState.ExitState();
        CurrentState = States[newStateKey];
        CurrentState.EnterState();

        isTransitioning = false;
    }
}