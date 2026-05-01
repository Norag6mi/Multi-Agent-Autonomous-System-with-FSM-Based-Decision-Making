// ============================================================================
// CombatStub.cs
// TEMPORARY mock implementation of IAgentCombat.
// 
// PURPOSE:
// --------
// This lets us test the entire AI system (FSM, perception, coordination)
// WITHOUT needing the real gun system connected yet.
// 
// It simulates:
// - Ammo depletion (fires N shots then triggers OnAmmoEmpty)
// - Reload timing (waits reloadDuration then refills ammo)
// - Heal timing (waits healDuration then restores health)
// - Death (when health reaches 0)
// - Damage taking (for testing engagements between agents)
//
// WHEN TO REMOVE:
// Once your real CombatController implements IAgentCombat,
// remove this component and attach CombatController instead.
// The FSM won't know the difference — that's the power of interfaces.
// ============================================================================

using System;
using System.Collections;
using UnityEngine;

public class CombatStub : MonoBehaviour, IAgentCombat
{
    [Header("=== Weapon Settings ===")]
    [Tooltip("Bullets per magazine")]
    [SerializeField] private int maxAmmo = 30;

    [Tooltip("Time between shots in seconds")]
    [SerializeField] private float fireRate = 0.2f;

    [Tooltip("Time to complete reload in seconds")]
    [SerializeField] private float reloadDuration = 2.0f;

    [Tooltip("Damage dealt per shot")]
    [SerializeField] private float damagePerShot = 10f;

    [Tooltip("Maximum effective shooting range")]
    [SerializeField] private float shootingRange = 15f;

    [Header("=== Health Settings ===")]
    [Tooltip("Maximum health points")]
    [SerializeField] private float maxHealth = 100f;

    [Header("=== Layer Settings ===")]
    [Tooltip("Layers that can be hit by raycast")]
    [SerializeField] private LayerMask hitLayers;

    [Header("=== Debug ===")]
    [Tooltip("Show debug rays and logs")]
    [SerializeField] private bool showDebug = true;

    // --- Internal State ---
    private int currentAmmo;
    private float currentHealth;
    private bool isDead = false;
    private bool isReloading = false;
    private bool isHealing = false;
    private bool isFiring = false;
    private float lastFireTime = 0f;

    // --- Muzzle point for raycasting ---
    // Will be set up to use eye height for now
    private float eyeHeight = 1.6f;

    // --- Events (IAgentCombat) ---
    public event Action OnAmmoEmptyEvent;
    public event Action OnAmmoLowEvent;
    public event Action OnDeathEvent;

    // --- Reference to who we're shooting at ---
    // Set externally by the EngageState
    [HideInInspector] public Transform currentTarget;

    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    private void Awake()
    {
        currentAmmo = maxAmmo;
        currentHealth = maxHealth;
    }

    // =========================================================================
    // UPDATE — Handles continuous firing
    // =========================================================================

    private void Update()
    {
        // If we're supposed to be firing, shoot at fire rate intervals
        if (isFiring && !isDead && !isReloading && !isHealing)
        {
            if (Time.time >= lastFireTime + fireRate)
            {
                FireOneShot();
                lastFireTime = Time.time;
            }
        }
    }

    // =========================================================================
    // COMMANDS (Called by FSM via IAgentCombat)
    // =========================================================================

    /// <summary>
    /// Begin continuous firing. Actual shots happen in Update at fireRate intervals.
    /// </summary>
    public void StartAttack()
    {
        if (isDead || isReloading || isHealing) return;
        isFiring = true;
    }

    /// <summary>
    /// Stop firing immediately.
    /// </summary>
    public void StopAttack()
    {
        isFiring = false;
    }

    /// <summary>
    /// Start reload coroutine. Agent cannot fire during reload.
    /// </summary>
    public void Reload()
    {
        if (isDead || isReloading) return;
        StartCoroutine(ReloadCoroutine());
    }

    /// <summary>
    /// Start healing coroutine. Agent cannot fire during heal.
    /// </summary>
    public void StartHeal(int amount, float duration)
    {
        if (isDead || isHealing) return;
        StartCoroutine(HealCoroutine(amount, duration));
    }

    // =========================================================================
    // QUERIES (Read by FSM to make decisions)
    // =========================================================================

    public bool IsDead() => isDead;
    public bool IsReloading() => isReloading;
    public bool IsHealing() => isHealing;
    public int GetCurrentAmmo() => currentAmmo;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;

    // =========================================================================
    // DAMAGE — Called by OTHER agents' shooting raycasts
    // =========================================================================

    /// <summary>
    /// Apply damage to this agent. Called when another agent's raycast hits us.
    /// This is the entry point for the damage flow.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        if (showDebug)
            Debug.Log($"[COMBAT] {gameObject.name} took {damage} damage. " +
                      $"Health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // =========================================================================
    // INTERNAL — Firing Logic
    // =========================================================================

    /// <summary>
    /// Fire a single shot. Uses raycast to check for hits.
    /// Reduces ammo and triggers events when empty.
    /// </summary>
    private void FireOneShot()
    {
        if (currentAmmo <= 0)
        {
            isFiring = false;
            OnAmmoEmptyEvent?.Invoke();
            return;
        }

        // Reduce ammo
        currentAmmo--;

        // Raycast from eye position toward current target
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 direction;

        if (currentTarget != null)
        {
            // Aim at target center mass
            Vector3 targetPoint = currentTarget.position + Vector3.up * eyeHeight * 0.8f;
            direction = (targetPoint - origin).normalized;
        }
        else
        {
            // No target — fire forward (shouldn't happen in practice)
            direction = transform.forward;
        }

        // Perform raycast
        if (Physics.Raycast(origin, direction, out RaycastHit hit, shootingRange, hitLayers))
        {
            // Check if we hit something with a CombatStub (another agent)
            CombatStub targetCombat = hit.collider.GetComponentInParent<CombatStub>();
            if (targetCombat != null && targetCombat != this)
            {
                targetCombat.TakeDamage(damagePerShot);
            }

            if (showDebug)
                Debug.DrawLine(origin, hit.point, Color.red, 0.1f);
        }
        else
        {
            if (showDebug)
                Debug.DrawRay(origin, direction * shootingRange, Color.yellow, 0.1f);
        }

        // Check ammo warnings
        if (currentAmmo <= 0)
        {
            isFiring = false;
            OnAmmoEmptyEvent?.Invoke();
        }
        else if (currentAmmo <= maxAmmo * 0.2f) // Below 20% ammo
        {
            OnAmmoLowEvent?.Invoke();
        }

        if (showDebug)
            Debug.Log($"[COMBAT] {gameObject.name} fired. Ammo: {currentAmmo}/{maxAmmo}");
    }

    // =========================================================================
    // INTERNAL — Reload Coroutine
    // =========================================================================

    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        isFiring = false;

        if (showDebug)
            Debug.Log($"[COMBAT] {gameObject.name} RELOADING... ({reloadDuration}s)");

        yield return new WaitForSeconds(reloadDuration);

        currentAmmo = maxAmmo;
        isReloading = false;

        if (showDebug)
            Debug.Log($"[COMBAT] {gameObject.name} RELOAD COMPLETE. Ammo: {currentAmmo}");
    }

    // =========================================================================
    // INTERNAL — Heal Coroutine
    // =========================================================================

    private IEnumerator HealCoroutine(int amount, float duration)
    {
        isHealing = true;
        isFiring = false;

        if (showDebug)
            Debug.Log($"[COMBAT] {gameObject.name} HEALING... " +
                      $"(+{amount}HP over {duration}s)");

        yield return new WaitForSeconds(duration);

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        isHealing = false;

        if (showDebug)
            Debug.Log($"[COMBAT] {gameObject.name} HEAL COMPLETE. " +
                      $"Health: {currentHealth}/{maxHealth}");
    }

    // =========================================================================
    // INTERNAL — Death
    // =========================================================================

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        isFiring = false;

        if (showDebug)
            Debug.Log($"[COMBAT] {gameObject.name} has DIED.");

        OnDeathEvent?.Invoke();
    }
}