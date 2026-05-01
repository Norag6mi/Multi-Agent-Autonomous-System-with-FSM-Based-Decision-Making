using System;
using UnityEngine;

public class HealthModel
{
    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }

    public bool IsDead => CurrentHealth <= 0;

    public event Action<int> OnDamaged;

    public bool IsHealing { get; private set; }

    public event Action OnHealStarted;
    public event Action OnHealFinished;
    public event Action OnDeath;

    public HealthModel(int maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        CurrentHealth -= amount;
        CurrentHealth = Math.Max(CurrentHealth, 0);

        OnDamaged?.Invoke(CurrentHealth);

        if (CurrentHealth == 0)
        {
            OnDeath?.Invoke();
        }

        //Debug.Log("HP: " + CurrentHealth + " / " + MaxHealth);
    }

    public void Heal(int amount)
    {
        if (IsDead) return;

        CurrentHealth += amount;
        CurrentHealth = Math.Min(CurrentHealth, MaxHealth);

        //Debug.Log("Healed → HP: " + CurrentHealth);
    }

    public void StartHeal()
    {
        if (IsDead || IsHealing) return;

        IsHealing = true;
        OnHealStarted?.Invoke();
    }

    public void FinishHeal(int amount)
    {
        Heal(amount);
        IsHealing = false;
        OnHealFinished?.Invoke();
    }

    public void ResetHealth()
    {
        CurrentHealth = MaxHealth;
        // IsDead is computed from CurrentHealth, no assignment needed
        IsHealing = false;
    }
}