using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;


public class VRCommander_Demo : MonoBehaviour
{
    [Header("Controller References")]
    [SerializeField] private Transform rightHandController;
    [SerializeField] private Transform playerHead;

    [Header("Shooting")]
    [SerializeField] private InputActionReference rightTriggerAction;
    [SerializeField] private float shootRange = 60f;
    [SerializeField] private int shootDamage = 30;
    [SerializeField] private float fireRate = 0.25f;
    [SerializeField] private LayerMask shootableLayers;
    [SerializeField] private float triggerThreshold = 0.5f;

    [Header("Keyboard Fallback")]
    [SerializeField] private KeyCode shootKey = KeyCode.Mouse0;
    [SerializeField] private bool useKeyboardFallback = true;

    [Header("Reticle")]
    [SerializeField] private GameObject reticleDot;
    [SerializeField] private float reticleRange = 60f;

    [Header("Muzzle Flash")]
    [SerializeField] private ParticleSystem muzzleFlash;

    [Header("Bullet Trail")]
    [SerializeField] private LineRenderer bulletTrail;
    [SerializeField] private float trailDuration = 0.08f;
    [SerializeField] private float trailStartWidth = 0.04f;
    [SerializeField] private float trailEndWidth = 0.01f;
    [SerializeField] private Color trailColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Header("Feedback")]
    [SerializeField] private GameObject impactEffectPrefab;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    // Internal state
    private bool canShoot = true;
    private bool isDead = false;
    private bool triggerWasPressed = false;
    private bool isVRActive = false;
    private float lastFireTime = -999f;

    // Reticle layer
    private LayerMask reticleExcludeMask;

    // Health — read directly from HealthComponent on this GameObject
    private HealthComponent healthComponent;
    private int lastKnownHealth;

    public bool IsAlive => !isDead;
    public Vector3 GetPosition() => transform.position;

    // Add this field with other private state fields:
    private bool commandModeActive = false;

    // Add this public method:
    public void SetCommandModeActive(bool active)
    {
        commandModeActive = active;
    }


    public void SetCanShoot(bool value)
    {
        canShoot = value;
    }

    public static event System.Action<int, int> OnPlayerHealthChanged;
    public static event System.Action OnPlayerDied;
    public static event System.Action<GameObject> OnPlayerHitEnemy;

    private void Awake()
    {
        isVRActive = XRSettings.isDeviceActive;

        // Get HealthComponent on this same GameObject
        healthComponent = GetComponent<HealthComponent>();

        if (healthComponent == null)
        {
            Debug.LogError("[VRCommander] NO HealthComponent found on XR Rig! " +
                           "Add HealthComponent to XR Origin.");
        }
        else
        {
            lastKnownHealth = healthComponent.Model.MaxHealth;
            Debug.Log($"[VRCommander] HealthComponent found. " +
                      $"Max HP: {lastKnownHealth}");
        }

        SetupReticleLayer();
        SetupBulletTrail();
    }

    private void OnEnable()
    {
        rightTriggerAction?.action.Enable();

        // Subscribe directly to HealthComponent death event
        if (healthComponent != null)
            healthComponent.OnDeathEvent += OnHealthDeath;
    }

    private void OnDisable()
    {
        rightTriggerAction?.action.Disable();

        if (healthComponent != null)
            healthComponent.OnDeathEvent -= OnHealthDeath;
    }

    private void Update()
    {
        if (isDead) return;

        // Poll health every frame — catches ALL damage sources
        MonitorHealth();

        UpdateReticle();
        HandleShooting();
    }

    // ── Health Monitoring ────────────────────────────────

    private void MonitorHealth()
    {
        if (healthComponent == null) return;

        int currentHp = healthComponent.Model.CurrentHealth;

        if (currentHp != lastKnownHealth)
        {
            if (debugLog)
                Debug.Log($"[VRCommander] Health changed: {lastKnownHealth} → {currentHp}");

            OnPlayerHealthChanged?.Invoke(currentHp, healthComponent.Model.MaxHealth);
            lastKnownHealth = currentHp;
        }

        // Direct check — if HP is 0 and we haven't died yet, die now
        if (currentHp <= 0 && !isDead)
        {
            Debug.LogError("[VRCommander] HP reached 0 detected in MonitorHealth. Calling Die().");
            Die();
        }
    }

    private void OnHealthDeath()
    {
        // Called directly by HealthComponent.OnDeathEvent
        Debug.LogError("[VRCommander] OnHealthDeath() called from HealthComponent event.");

        if (!isDead)
            Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        canShoot = false;

        Debug.LogError("[VRCommander] Die() executing. " +
                       "Firing OnPlayerDied event now.");

        // Check if anyone is listening
        if (OnPlayerDied == null)
        {
            Debug.LogError("[VRCommander] OnPlayerDied has NO subscribers. " +
                           "MissionManager is not listening. " +
                           "Check MissionManager Awake() subscription.");
        }
        else
        {
            Debug.LogError($"[VRCommander] OnPlayerDied has " +
                           $"{OnPlayerDied.GetInvocationList().Length} subscriber(s). Firing.");
        }

        OnPlayerDied?.Invoke();

        if (isVRActive)
            DisableLocomotion();
    }

    // ── Shooting ─────────────────────────────────────────

    private void HandleShooting()
    {
        if (!canShoot) return;
        if (isDead) return;
        if (commandModeActive) return;   // ← ADD THIS LINE
        if (Time.time - lastFireTime < fireRate) return;

        bool shouldFire = false;

        if (isVRActive && rightTriggerAction != null)
        {
            float val = rightTriggerAction.action.ReadValue<float>();
            bool pressed = val >= triggerThreshold;
            if (pressed && !triggerWasPressed) shouldFire = true;
            triggerWasPressed = pressed;
        }

        if ((!isVRActive || useKeyboardFallback) && Input.GetKeyDown(shootKey))
            shouldFire = true;

        if (shouldFire) Fire();
    }

    private void Fire()
    {
        if (isDead) return;

        lastFireTime = Time.time;
        GetAimRay(out Vector3 origin, out Vector3 direction);

        PlayMuzzleFlash();

        if (debugLog)
            Debug.DrawRay(origin, direction * shootRange, Color.red, 0.15f);

        LayerMask combined = shootableLayers & reticleExcludeMask;
        bool hit = Physics.Raycast(origin, direction, out RaycastHit hitInfo,
                                   shootRange, combined);

        if (hit) ProcessHit(hitInfo, origin);
        else ShowBulletTrail(origin, origin + direction * shootRange);
    }

    private void GetAimRay(out Vector3 origin, out Vector3 direction)
    {
        if (isVRActive && rightHandController != null)
        {
            origin = rightHandController.position;
            direction = rightHandController.forward;
        }
        else
        {
            Camera cam = Camera.main;
            origin = cam.transform.position;
            direction = cam.transform.forward;
        }
    }

    private void ProcessHit(RaycastHit hit, Vector3 origin)
    {
        ShowBulletTrail(origin, hit.point);

        if (impactEffectPrefab != null)
            Instantiate(impactEffectPrefab, hit.point,
                        Quaternion.LookRotation(hit.normal));

        HealthComponent health = hit.collider.GetComponentInParent<HealthComponent>();
        if (health != null)
        {
            health.TakeDamage(shootDamage);
            OnPlayerHitEnemy?.Invoke(hit.collider.gameObject);

            if (debugLog)
                Debug.Log($"[VRCommander] Hit {hit.collider.name} for {shootDamage} dmg");
        }
    }

    // ── Reticle ──────────────────────────────────────────

    private void SetupReticleLayer()
    {
        if (reticleDot != null)
        {
            reticleDot.layer = 2;
            foreach (Transform child in reticleDot.transform)
                child.gameObject.layer = 2;
        }
        reticleExcludeMask = ~(1 << 2);
    }

    private void UpdateReticle()
    {
        if (reticleDot == null) return;

        GetAimRay(out Vector3 origin, out Vector3 direction);

        if (Physics.Raycast(origin, direction, out RaycastHit hit,
                            reticleRange, reticleExcludeMask))
        {
            reticleDot.transform.position = hit.point + hit.normal * 0.02f;
            reticleDot.transform.rotation = Quaternion.LookRotation(hit.normal);
            reticleDot.SetActive(true);
        }
        else
        {
            reticleDot.SetActive(false);
        }
    }

    // ── Bullet Trail ─────────────────────────────────────

    private void SetupBulletTrail()
    {
        if (bulletTrail == null) return;

        Material mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = trailColor;

        bulletTrail.material = mat;
        bulletTrail.startWidth = trailStartWidth;
        bulletTrail.endWidth = trailEndWidth;
        bulletTrail.positionCount = 2;
        bulletTrail.useWorldSpace = true;
        bulletTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bulletTrail.enabled = false;
    }

    private void ShowBulletTrail(Vector3 from, Vector3 to)
    {
        if (bulletTrail == null) return;
        bulletTrail.SetPosition(0, from);
        bulletTrail.SetPosition(1, to);
        bulletTrail.enabled = true;
        CancelInvoke(nameof(HideTrail));
        Invoke(nameof(HideTrail), trailDuration);
    }

    private void HideTrail()
    {
        if (bulletTrail != null)
            bulletTrail.enabled = false;
    }

    // ── Muzzle Flash ─────────────────────────────────────

    private void PlayMuzzleFlash()
    {
        if (muzzleFlash == null) return;
        if (muzzleFlash.isPlaying) muzzleFlash.Stop();
        muzzleFlash.Play();
    }

    // ── Locomotion ───────────────────────────────────────

    private void DisableLocomotion()
    {
        var providers = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>();
        foreach (var p in providers) p.enabled = false;
    }
}