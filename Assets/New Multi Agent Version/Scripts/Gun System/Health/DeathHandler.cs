using UnityEngine;
using System.Collections;

[RequireComponent(typeof(HealthComponent))]
public class DeathHandler : MonoBehaviour
{
    private HealthComponent health;
    private CharacterAnimatorBridge animatorBridge;

    private Collider[] colliders;
    private MonoBehaviour[] behaviours;

    private void Awake()
    {
        health = GetComponent<HealthComponent>();
        animatorBridge = GetComponent<CharacterAnimatorBridge>();

        colliders = GetComponentsInChildren<Collider>();
        behaviours = GetComponents<MonoBehaviour>();

        health.OnDeathEvent += OnDeath;
    }

    private void OnDeath()
    {
        animatorBridge.PlayDeath();
        StartCoroutine(DisableAfterAnimation());
    }

    private IEnumerator DisableAfterAnimation()
    {
        yield return new WaitForSeconds(3f); // match animation length

        // Disable colliders
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // Disable all scripts except this one
        foreach (var comp in behaviours)
        {
            if (comp != this && !(comp is CharacterAnimatorBridge))
            {
                comp.enabled = false;
            }
        }

        Debug.Log("Character fully disabled after death.");
    }
}
