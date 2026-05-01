// ============================================================================
// IAgentCombat.cs
// Interface that abstracts the combat system from the AI system.
// 
// WHY AN INTERFACE?
// -----------------
// Your CombatController is a fully built, event-driven combat system.
// The AI (FSM) should NOT depend directly on CombatController.
// Instead, the AI depends on this interface.
// 
// This means:
// 1. We can build a CombatStub for testing before plugging in real guns
// 2. Your real CombatController just implements this interface
// 3. The AI doesn't care HOW shooting/healing works — only THAT it can
// 4. Clean separation = easy to explain in interviews
//
// This is the "Dependency Inversion Principle" from SOLID.
// ============================================================================

using System;

/// <summary>
/// Contract between the AI decision-making system and the combat execution system.
/// The FSM calls these methods to command combat actions.
/// The combat system fires events to notify the FSM of state changes.
/// </summary>
public interface IAgentCombat
{
    // ===================
    // COMMANDS (AI → Combat)
    // The FSM tells the combat system WHAT to do.
    // The combat system decides HOW to do it.
    // ===================

    /// <summary>
    /// Begin firing at the current target.
    /// Combat system handles fire rate, animation, and raycast.
    /// </summary>
    void StartAttack();

    /// <summary>
    /// Stop firing immediately.
    /// Called when target is lost or state changes.
    /// </summary>
    void StopAttack();

    /// <summary>
    /// Initiate reload sequence.
    /// Combat system handles reload duration and ammo refill.
    /// </summary>
    void Reload();

    /// <summary>
    /// Begin healing process.
    /// Agent will be vulnerable during heal duration.
    /// </summary>
    /// <param name="amount">How much health to restore</param>
    /// <param name="duration">How long the heal takes (seconds)</param>
    void StartHeal(int amount, float duration);

    // ===================
    // QUERIES (AI reads combat state)
    // The FSM checks these to make decisions.
    // Example: "Should I reload?" → check GetCurrentAmmo() == 0
    // ===================

    /// <summary>
    /// Returns true if the agent's health has reached zero.
    /// </summary>
    bool IsDead();

    /// <summary>
    /// Returns true if a reload is currently in progress.
    /// AI should wait and not try to shoot during reload.
    /// </summary>
    bool IsReloading();

    /// <summary>
    /// Returns true if healing is currently in progress.
    /// AI should wait and not try to shoot during heal.
    /// </summary>
    bool IsHealing();

    /// <summary>
    /// Returns current ammo count in the magazine.
    /// AI uses this to decide: shoot (ammo > 0) or reload (ammo == 0).
    /// </summary>
    int GetCurrentAmmo();

    /// <summary>
    /// Returns current health value.
    /// AI uses this to decide: keep fighting or heal.
    /// </summary>
    float GetCurrentHealth();

    /// <summary>
    /// Returns maximum health value.
    /// Used to calculate health percentage for decision making.
    /// </summary>
    float GetMaxHealth();

    // ===================
    // EVENTS (Combat → AI)
    // The combat system notifies the FSM when important things happen.
    // The FSM subscribes to these in AgentFSM.
    // ===================

    /// <summary>
    /// Fired when magazine is completely empty.
    /// AI should transition to reload behavior.
    /// </summary>
    event Action OnAmmoEmptyEvent;

    /// <summary>
    /// Fired when ammo drops below a low threshold.
    /// AI could use this for preemptive reload decisions.
    /// </summary>
    event Action OnAmmoLowEvent;

    /// <summary>
    /// Fired when health reaches zero.
    /// AI should transition to Dead state immediately.
    /// </summary>
    event Action OnDeathEvent;
}