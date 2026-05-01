using UnityEngine;
using System;

[RequireComponent(typeof(WeaponComponent))]
[RequireComponent(typeof(HealthComponent))]
[RequireComponent(typeof(CharacterAnimatorBridge))]
public class CombatController : MonoBehaviour, IAgentCombat
{
    private WeaponComponent weapon;
    private HealthComponent health;
    private CharacterAnimatorBridge animatorBridge;

    private bool isDead;

    // IAgentCombat events
    public event Action OnAmmoEmptyEvent;
    public event Action OnAmmoLowEvent;
    public event Action OnDeathEvent;

    private void Awake()
    {
        weapon = GetComponent<WeaponComponent>();
        health = GetComponent<HealthComponent>();
        animatorBridge = GetComponent<CharacterAnimatorBridge>();
    }

    private void Start()
    {
        health.OnDeathEvent += HandleDeath;

        if (weapon.Model != null)
        {
            weapon.Model.OnAmmoEmpty += HandleAmmoEmpty;
            weapon.Model.OnAmmoLow += HandleAmmoLow;
        }
    }

    // === IAgentCombat Commands ===

    public void StartAttack()
    {
        if (isDead) return;
        weapon.TryStartFiring();
    }

    public void StopAttack()
    {
        if (isDead) return;
        weapon.StopFiring();
    }

    public void Reload()
    {
        if (isDead) return;
        weapon.TryReload();

        // Log reload
        AgentIdentity identity = GetComponent<AgentIdentity>();
        if (MetricsLogger.Instance != null && identity != null)
            MetricsLogger.Instance.RecordReload(identity.agentName);

    }

    public void StartHeal(int amount, float duration)
    {
        if (isDead) return;
        animatorBridge.PlayHeal();
        health.StartHeal(amount, duration);

         // Log heal
         AgentIdentity identity = GetComponent<AgentIdentity>();
        if (MetricsLogger.Instance != null && identity != null)
            MetricsLogger.Instance.RecordHeal(identity.agentName);
    }

    // === IAgentCombat Queries ===

    public bool IsDead() => isDead;
    public bool IsReloading() => weapon.Model.IsReloading;
    public bool IsHealing() => health.IsHealing;
    public int GetCurrentAmmo() => weapon.Model.CurrentAmmo;
    public float GetCurrentHealth() => health.Model.CurrentHealth;
    public float GetMaxHealth() => health.Model.MaxHealth;

    // === Internal Handlers ===

    private void HandleDeath()
    {
        isDead = true;
        weapon.StopFiring();
        animatorBridge.PlayDeath();
        OnDeathEvent?.Invoke();
    }

    private void HandleAmmoEmpty()
    {
        StopAttack();
        OnAmmoEmptyEvent?.Invoke();
    }

    private void HandleAmmoLow()
    {
        OnAmmoLowEvent?.Invoke();
    }

   public void ResetCombat()
    {
        isDead = false;

        if (weapon != null)
            weapon.StopFiring();

        if (animatorBridge != null)
        {
            animatorBridge.SetFiring(false);
        }
    }
}