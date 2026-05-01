using UnityEngine;

public class HealthComponent : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;

    [SerializeField] private float healThresholdPercent = 0.7f; // 70% threshold

    public HealthModel Model { get; private set; }

    public System.Action OnDeathEvent;

    public bool IsHealing => Model.IsHealing;//to check healing state in my state machine

    private void Awake()
    {
        Model = new HealthModel(maxHealth);
        Model.OnDeath += HandleDeath;
    }

    public void TakeDamage(int amount)
    {
        Model.TakeDamage(amount);
        Debug.Log(gameObject.name + " HP: " + Model.CurrentHealth);

    }

    public void StartHeal(int amount, float duration)
    {
        // Threshold Check: Only heal if current HP is less than 70% of Max
        float currentPercent = (float)Model.CurrentHealth / Model.MaxHealth;
        
        if (Model.IsDead || currentPercent >= healThresholdPercent) 
        {
            Debug.Log("Health too high to heal or entity is dead.");
            return;
        }

        StartCoroutine(HealRoutine(amount, duration));
    }

    private System.Collections.IEnumerator HealRoutine(int amount, float duration)
    {
        Model.StartHeal();

        yield return new WaitForSeconds(duration);

        Model.FinishHeal(amount);
    }


    private void HandleDeath()
    {
        OnDeathEvent?.Invoke();
    }

    public void ResetHealthComponent()
    {
        StopAllCoroutines();
        Model.ResetHealth();
    }

}
