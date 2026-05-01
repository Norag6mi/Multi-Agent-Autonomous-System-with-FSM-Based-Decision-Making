# Multi-Agent-Hybrid-Autonomous-System-with-FSM-Based-Decision-Making
This project implements a multi-agent system (MAS) where autonomous agents navigate complex tasks using a Finite State Machine (FSM) for decision-making and control flow. By using FSMs, the system ensures predictable behavior, observability, and robust error handling through state-based logic.

Overview
Traditional autonomous systems often struggle with long-chain task execution or unpredictable branching. This framework addresses these challenges by:
>> State-Based Decomposition: Breaking complex tasks into discrete FSM states.
>> Dynamic Transitions: Using "Condition Verifiers" to determine if an agent should proceed, repeat a state, or trace back to a previous step.
>> Self-Refinement: Iteratively optimizing the FSM structure to remove redundant states and improve performance.

 Key Features
 >> State Traceback: Automatically returns to previous states when errors or hallucinations are detected
 >> Feedback Loops: Implements "LoopAgents" to create continuous verification between researchers and judges.
 >> Simulation: Simulated for both Desktop and VR platforms.
 
