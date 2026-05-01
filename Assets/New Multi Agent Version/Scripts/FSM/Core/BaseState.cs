using UnityEngine;
using System;

/// <summary>
/// Abstract base for any state in any FSM. Pure generic — no domain logic.
/// </summary>
/// 
public abstract class BaseState<EState> where EState : Enum
{
    public EState StateKey { get; private set; }

    public BaseState(EState key)
    {
        StateKey = key;
    }

    public abstract void EnterState();
    public abstract void ExitState();
    public abstract void UpdateState();
    public abstract EState GetNextState();
}