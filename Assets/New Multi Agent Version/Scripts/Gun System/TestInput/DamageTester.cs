using UnityEngine;

public class HealthTestInput : MonoBehaviour
{
    private HealthComponent health;

    private void Start()
    {
        health = GetComponent<HealthComponent>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (!health.Model.IsDead)
            {
                health.TakeDamage(25);
                Debug.Log("Damage applied. Current HP: " + health.Model.CurrentHealth);
            }
            else
            {
                Debug.Log("Already dead.");
            }
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            if (!health.Model.IsDead)
            {
                health.TakeDamage(200);
                Debug.Log("Massive damage applied.");
            }
        }
    }
}
