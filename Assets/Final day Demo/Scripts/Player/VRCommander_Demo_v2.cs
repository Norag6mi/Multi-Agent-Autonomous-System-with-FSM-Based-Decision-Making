using UnityEngine;
using UnityEngine.XR;

using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections.Generic;

public class VRCommander_Demo_v2 : MonoBehaviour, ICommandInterface
{
    // ─────────────────────────────────────────────
    // INSPECTOR — XR REFERENCES
    // ─────────────────────────────────────────────

    [Header("Controller Transforms")]
    [SerializeField] private Transform rightControllerTransform;
    [SerializeField] private Transform leftControllerTransform;
    [SerializeField] private Transform headTransform;

    [Header("Input Actions — Right Hand")]
    [SerializeField] private InputActionReference rightTriggerAction;
    [SerializeField] private InputActionReference rightGripAction;
    [SerializeField] private InputActionReference rightStickPressAction;

    [Header("Input Actions — Left Hand")]
    [SerializeField] private InputActionReference leftTriggerAction;
    [SerializeField] private InputActionReference leftGripAction;
    [SerializeField] private InputActionReference leftStickPressAction;

    // ─────────────────────────────────────────────
    // INSPECTOR — SHOOTING
    // ─────────────────────────────────────────────

    [Header("Shooting")]
    [SerializeField] private float shootRange = 60f;
    [SerializeField] private int shootDamage = 30;
    [SerializeField] private float fireRate = 0.25f;
    [SerializeField] private LayerMask shootableLayers;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private LineRenderer bulletTrail;
    [SerializeField] private float trailDuration = 0.08f;
    [SerializeField] private Color trailColor = new Color(1f, 0.85f, 0.3f);

    [Header("Reticle")]
    [SerializeField] private GameObject reticleDot;
    [SerializeField] private float reticleRange = 60f;

    // ─────────────────────────────────────────────
    // INSPECTOR — COMMANDS
    // ─────────────────────────────────────────────

    [Header("Commands")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float commandRayLength = 50f;
    [SerializeField] private Color moveToColor = new Color(0.2f, 0.5f, 1f);
    [SerializeField] private Color regroupColor = new Color(0.2f, 1f, 0.2f);
    [SerializeField] private Color queuedColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color rejectedColor = Color.red;
    [SerializeField] private Color selectedColor = Color.cyan;
    [SerializeField] private float feedbackDuration = 1.5f;
    [SerializeField] private float selectionIndicatorScale = 0.5f;

    [Header("Command Ray")]
    [SerializeField] private LineRenderer commandRayVisual;

    // ─────────────────────────────────────────────
    // INSPECTOR — MODE UI
    // ─────────────────────────────────────────────

    [Header("Mode Indicator (World Space Canvas on Controller)")]
    [SerializeField] private GameObject combatModeUI;
    [SerializeField] private GameObject commandModeUI;

    // ─────────────────────────────────────────────
    // INSPECTOR — HEALTH
    // ─────────────────────────────────────────────

    [Header("Player Health")]
    [SerializeField] private int maxHealth = 100;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    public enum ControllerMode { Combat, Command }
    private ControllerMode currentMode = ControllerMode.Combat;

    // Shooting
    private float lastFireTime = -999f;
    private bool canShoot = true;
    private LayerMask reticleExcludeMask;

    // Health
    private int currentHealth;
    private bool isDead = false;
    private HealthComponent healthComponent;
    private int lastKnownHealth;

    // Commands
    private List<CommandReceiver> allReceivers = new List<CommandReceiver>();
    private List<CommandReceiver> selectedReceivers = new List<CommandReceiver>();
    private int currentSelectionIndex = -1;
    private bool allSelected = true;
    private bool initialized = false;

    // Selection visuals
    private Dictionary<CommandReceiver, GameObject> selectionRings
        = new Dictionary<CommandReceiver, GameObject>();
    private Dictionary<CommandReceiver, float> feedbackTimers
        = new Dictionary<CommandReceiver, float>();
    private Dictionary<CommandReceiver, Color> feedbackColors
        = new Dictionary<CommandReceiver, Color>();

    // Input state (for edge detection)
    private bool rightTriggerWasPressed = false;
    private bool rightGripWasPressed = false;
    private bool leftGripWasPressed = false;
    private bool rightStickWasPressed = false;
    private bool leftStickWasPressed = false;

    // ─────────────────────────────────────────────
    // EVENTS
    // ─────────────────────────────────────────────

    public static event System.Action<int, int> OnPlayerHealthChanged;
    public static event System.Action OnPlayerDied;
    public static event System.Action<GameObject> OnPlayerHitEnemy;

    // ─────────────────────────────────────────────
    // PROPERTIES
    // ─────────────────────────────────────────────

    public bool IsAlive => !isDead;
    public Vector3 GetPosition() => transform.position;
    public ControllerMode CurrentMode => currentMode;
    public void SetCanShoot(bool value) => canShoot = value;



    // 

    [Header("Input Actions — Optional Button Fallbacks")]
    [SerializeField] private InputActionReference rightPrimaryButtonAction; // A / Primary
    [SerializeField] private InputActionReference leftPrimaryButtonAction;  // X / Primary


    private bool rightPrimaryWasPressed = false;
    private bool leftPrimaryWasPressed = false;

    //

    [SerializeField] private GameObject gunModel;
    [SerializeField] private Transform muzzlePoint;


    // ─────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        currentHealth = maxHealth;

        healthComponent = GetComponent<HealthComponent>();
        if (healthComponent != null)
        {
            lastKnownHealth = healthComponent.Model.MaxHealth;
            healthComponent.OnDeathEvent += OnHealthDeath;
        }

        SetupReticle();
        SetupBulletTrail();
        SetupCommandRay();
    }

    private void OnEnable()
    {
        EnableAllActions();
    }

    private void OnDisable()
    {
        DisableAllActions();
    }

    private void Start()
    {
        Invoke(nameof(InitializeReceivers), 1f);
        SetMode(ControllerMode.Combat);

        if (headTransform == null)
            headTransform = Camera.main?.transform;
    }

    private void OnDestroy()
    {
        if (healthComponent != null)
            healthComponent.OnDeathEvent -= OnHealthDeath;
    }

    private void Update()
    {
        if (isDead) return;

        MonitorHealth();
        ReadInput();
        UpdateReticle();
        UpdateSelectionVisuals();

        if (currentMode == ControllerMode.Command)
            UpdateCommandRay();
        else
            HideCommandRay();
    }

    // ─────────────────────────────────────────────
    // INPUT ACTIONS ENABLE/DISABLE
    // ─────────────────────────────────────────────

    private void EnableAllActions()
    {
        rightTriggerAction?.action.Enable();
        rightGripAction?.action.Enable();
        rightStickPressAction?.action.Enable();
        leftTriggerAction?.action.Enable();
        leftGripAction?.action.Enable();
        leftStickPressAction?.action.Enable();

        rightPrimaryButtonAction?.action.Enable();
        leftPrimaryButtonAction?.action.Enable();
    }

    private void DisableAllActions()
    {
        rightTriggerAction?.action.Disable();
        rightGripAction?.action.Disable();
        rightStickPressAction?.action.Disable();
        leftTriggerAction?.action.Disable();
        leftGripAction?.action.Disable();
        leftStickPressAction?.action.Disable();

        rightPrimaryButtonAction?.action.Disable();
        leftPrimaryButtonAction?.action.Disable();
    }

    // ─────────────────────────────────────────────
    // INPUT READING
    // ─────────────────────────────────────────────

    private void ReadInput()
    {
        ReadVRInput();
        ReadKeyboardInput();
    }

    private void ReadVRInput()
    {
        // Read raw values
        float rightTriggerVal = rightTriggerAction?.action.ReadValue<float>() ?? 0f;
        float rightGripVal    = rightGripAction?.action.ReadValue<float>() ?? 0f;
        float leftGripVal     = leftGripAction?.action.ReadValue<float>() ?? 0f;
        float rightStickVal   = rightStickPressAction?.action.ReadValue<float>() ?? 0f;
        float leftStickVal    = leftStickPressAction?.action.ReadValue<float>() ?? 0f;

        // Edge detection
        bool rightTriggerDown = rightTriggerVal > 0.5f && !rightTriggerWasPressed;
        bool rightTriggerHeld = rightTriggerVal > 0.5f;
        bool rightGripDown    = rightGripVal > 0.5f && !rightGripWasPressed;
        bool leftGripDown     = leftGripVal > 0.5f && !leftGripWasPressed;
        bool rightStickDown   = rightStickVal > 0.5f && !rightStickWasPressed;
        bool leftStickDown    = leftStickVal > 0.5f && !leftStickWasPressed;

        // Update previous states
        rightTriggerWasPressed = rightTriggerVal > 0.5f;
        rightGripWasPressed    = rightGripVal > 0.5f;
        leftGripWasPressed     = leftGripVal > 0.5f;
        rightStickWasPressed   = rightStickVal > 0.5f;
        leftStickWasPressed    = leftStickVal > 0.5f;

        //reads + edges

        float rightPrimaryVal = rightPrimaryButtonAction?.action.ReadValue<float>() ?? 0f;
        float leftPrimaryVal  = leftPrimaryButtonAction?.action.ReadValue<float>() ?? 0f;

        bool rightPrimaryDown = rightPrimaryVal > 0.5f && !rightPrimaryWasPressed;
        bool leftPrimaryDown  = leftPrimaryVal > 0.5f && !leftPrimaryWasPressed;

        rightPrimaryWasPressed = rightPrimaryVal > 0.5f;
        leftPrimaryWasPressed  = leftPrimaryVal > 0.5f;


        // RIGHT GRIP → toggle mode
        if (rightGripDown)
            ToggleMode();

        if (currentMode == ControllerMode.Combat)
        {
            // Right Trigger held → shoot (auto fire)
            if (rightTriggerHeld)
                TryShoot();
        }
        else // Command mode
        {
            // Right Trigger press → Move To
            if (rightTriggerDown)
                TryIssueMoveCommand();

            // Left Grip press → Regroup
            if (leftGripDown)
                IssueRegroup(transform.position);

            // Right Stick press → Cycle selection
            if (rightStickDown)
                CycleSelection();

            // Left Stick press → Select all
            if (leftStickDown)
                SelectAllAllies();

            if (rightStickDown || rightPrimaryDown)
            CycleSelection();

            if (leftStickDown || leftPrimaryDown)
            SelectAllAllies();

        }
    }

    private void ReadKeyboardInput()
    {
        // F = toggle mode (changed from Tab to avoid simulator conflict)
        if (Input.GetKeyDown(KeyCode.F))
            ToggleMode();

        if (currentMode == ControllerMode.Combat)
        {
            if (Input.GetMouseButton(0))
                TryShoot();
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
                TryIssueMoveCommand();

            // C = Regroup (changed from R — R is simulator reposition)
            if (Input.GetKeyDown(KeyCode.C))
                IssueRegroup(transform.position);

            // Z = Cycle selection
            if (Input.GetKeyDown(KeyCode.Z))
                CycleSelection();

            // X = Select all
            if (Input.GetKeyDown(KeyCode.X))
                SelectAllAllies();
        }

        // Debug keys — use numpad to avoid simulator conflicts
        if (Input.GetKeyDown(KeyCode.Keypad4))
            healthComponent?.TakeDamage(25);

        if (Input.GetKeyDown(KeyCode.Keypad7))
            DebugKillAllEnemies();

        if (Input.GetKeyDown(KeyCode.Keypad8))
            DebugForceWin();

        if (Input.GetKeyDown(KeyCode.Keypad9))
            DebugForceLose();
    }

    // ─────────────────────────────────────────────
    // MODE
    // ─────────────────────────────────────────────

    private void ToggleMode()
    {
        SetMode(currentMode == ControllerMode.Combat
            ? ControllerMode.Command
            : ControllerMode.Combat);
    }

    private void SetMode(ControllerMode mode)
    {
        currentMode = mode;

        if (combatModeUI != null)
            combatModeUI.SetActive(mode == ControllerMode.Combat);
        if (commandModeUI != null)
            commandModeUI.SetActive(mode == ControllerMode.Command);

        // Hide reticle in command mode
        if (reticleDot != null && mode == ControllerMode.Command)
            reticleDot.SetActive(false);

        if (debugLog)
            Debug.Log($"[VRCommander] Mode → {mode}");


            if (gunModel != null)
            gunModel.SetActive(mode == ControllerMode.Combat);

            if (mode == ControllerMode.Command && bulletTrail != null)
                bulletTrail.enabled = false;
            if (muzzleFlash != null)
            muzzleFlash.gameObject.SetActive(mode == ControllerMode.Combat);


    }

    // ─────────────────────────────────────────────
    // SHOOTING
    // ─────────────────────────────────────────────

    private void TryShoot()
    {
        if (!canShoot) return;
        if (isDead) return;
        if (currentMode != ControllerMode.Combat) return;
        if (Time.time - lastFireTime < fireRate) return;

        lastFireTime = Time.time;

        Vector3 origin = muzzlePoint != null
            ? muzzlePoint.position
            : (rightControllerTransform != null ? rightControllerTransform.position : headTransform.position);

        Vector3 direction = muzzlePoint != null
            ? muzzlePoint.forward
            : (rightControllerTransform != null ? rightControllerTransform.forward : headTransform.forward);

        muzzleFlash?.Play();

        LayerMask mask = shootableLayers & reticleExcludeMask;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, shootRange, mask))
        {
            ShowBulletTrail(origin, hit.point);

            HealthComponent health =
                hit.collider.GetComponentInParent<HealthComponent>();

            if (health != null)
            {
                health.TakeDamage(shootDamage);
                OnPlayerHitEnemy?.Invoke(hit.collider.gameObject);

                if (debugLog)
                    Debug.Log($"[VRCommander] Hit {hit.collider.name}");
            }
        }
        else
        {
            ShowBulletTrail(origin, origin + direction * shootRange);
        }
    }

    // ─────────────────────────────────────────────
    // COMMANDS
    // ─────────────────────────────────────────────

    private void TryIssueMoveCommand()
    {
        if (selectedReceivers.Count == 0)
        {
            Debug.LogWarning("[VRCommander] No receivers. Refreshing...");
            InitializeReceivers();
            return;
        }

        Ray ray = GetCommandRay();

        if (Physics.Raycast(ray, out RaycastHit hit, commandRayLength, groundMask))
        {
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit,
                                       3f, NavMesh.AllAreas))
                IssueMoveOrder(navHit.position);
        }
        else
        {
            if (debugLog)
                Debug.Log("[VRCommander] Move To raycast missed ground. " +
                          "Check groundMask layer assignment.");
        }
    }

    public void IssueMoveOrder(Vector3 worldPosition)
    {
        int accepted = 0;
        foreach (var receiver in selectedReceivers)
        {
            if (receiver == null) continue;
            var status = receiver.ReceiveMoveOrder(worldPosition);
            ShowCommandFeedback(receiver, status, moveToColor);
            if (status == CommandReceiver.CommandStatus.Accepted) accepted++;
        }

        CommandMarker.Spawn(worldPosition, moveToColor);

        if (debugLog)
            Debug.Log($"[VRCommander] Move To {worldPosition:F1}. " +
                      $"Accepted: {accepted}/{selectedReceivers.Count}");
    }

    public void IssueRegroup(Vector3 regroupPosition)
    {
        int count = selectedReceivers.Count;
        if (count == 0) return;

        float radius = Mathf.Clamp(1.5f + count * 0.25f, 2f, 4f); // dynamic spread
        int accepted = 0;

        for (int i = 0; i < count; i++)
        {
            var receiver = selectedReceivers[i];
            if (receiver == null) continue;

            float angle = (360f / count) * i;
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
            Vector3 target = regroupPosition + offset;

            // Keep target on NavMesh
            if (NavMesh.SamplePosition(target, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                target = navHit.position;

            var status = receiver.ReceiveMoveOrder(target); // use spread move points
            ShowCommandFeedback(receiver, status, regroupColor);
            if (status == CommandReceiver.CommandStatus.Accepted) accepted++;
        }

        CommandMarker.Spawn(regroupPosition, regroupColor);

        if (debugLog)
            Debug.Log($"[VRCommander] Regroup spread. Accepted: {accepted}/{count}");
    }

    public void IssueHold()
    {
        foreach (var receiver in selectedReceivers)
        {
            if (receiver == null) continue;
            var status = receiver.ReceiveHold();
            ShowCommandFeedback(receiver, status, Color.white);
        }
    }

    public void IssueRelease()
    {
        foreach (var receiver in selectedReceivers)
        {
            if (receiver == null) continue;
            var status = receiver.ReceiveRelease();
            ShowCommandFeedback(receiver, status, selectedColor);
        }
    }

    public void SelectAgent(AgentIdentity agent)
    {
        if (agent == null) return;
        allSelected = false;
        selectedReceivers.Clear();

        CommandReceiver receiver = agent.GetComponent<CommandReceiver>();
        if (receiver != null)
        {
            selectedReceivers.Add(receiver);
            currentSelectionIndex = allReceivers.IndexOf(receiver);
        }
    }

    public void SelectAllAllies()
    {
        allSelected = true;
        selectedReceivers.Clear();
        selectedReceivers.AddRange(allReceivers);
        currentSelectionIndex = -1;

        if (debugLog)
            Debug.Log("[VRCommander] All agents selected.");
    }

    private void CycleSelection()
    {
        if (allReceivers.Count == 0) return;
        allSelected = false;
        currentSelectionIndex = (currentSelectionIndex + 1) % allReceivers.Count;

        var identity = allReceivers[currentSelectionIndex]
            .GetComponent<AgentIdentity>();
        SelectAgent(identity);

        if (debugLog)
            Debug.Log($"[VRCommander] Selected: {identity?.agentName}");
    }

    // ─────────────────────────────────────────────
    // HEALTH
    // ─────────────────────────────────────────────

    private void MonitorHealth()
    {
        if (healthComponent == null) return;

        int current = healthComponent.Model.CurrentHealth;
        if (current != lastKnownHealth)
        {
            OnPlayerHealthChanged?.Invoke(current, healthComponent.Model.MaxHealth);
            lastKnownHealth = current;
        }

        if (current <= 0 && !isDead)
            Die();
    }

    private void OnHealthDeath()
    {
        if (!isDead) Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        canShoot = false;

        Debug.Log("[VRCommander] Player died.");
        OnPlayerDied?.Invoke();

        var providers = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>();
        foreach (var p in providers) p.enabled = false;
    }

    // ─────────────────────────────────────────────
    // RETICLE
    // ─────────────────────────────────────────────

    private void SetupReticle()
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
        if (currentMode != ControllerMode.Combat)
        {
            reticleDot.SetActive(false);
            return;
        }

        Vector3 origin = rightControllerTransform != null
            ? rightControllerTransform.position : headTransform.position;
        Vector3 dir = rightControllerTransform != null
            ? rightControllerTransform.forward : headTransform.forward;

        if (Physics.Raycast(origin, dir, out RaycastHit hit,
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

    // ─────────────────────────────────────────────
    // BULLET TRAIL
    // ─────────────────────────────────────────────

    private void SetupBulletTrail()
    {
        if (bulletTrail == null) return;
        Material mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = trailColor;
        bulletTrail.material = mat;
        bulletTrail.startWidth = 0.04f;
        bulletTrail.endWidth = 0.01f;
        bulletTrail.positionCount = 2;
        bulletTrail.useWorldSpace = true;
        bulletTrail.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
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
        if (bulletTrail != null) bulletTrail.enabled = false;
    }

    // ─────────────────────────────────────────────
    // COMMAND RAY
    // ─────────────────────────────────────────────

    private void SetupCommandRay()
    {
        if (commandRayVisual == null) return;
        Material mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = new Color(0.2f, 0.6f, 1f);
        commandRayVisual.material = mat;
        commandRayVisual.startWidth = 0.01f;
        commandRayVisual.endWidth = 0.003f;
        commandRayVisual.positionCount = 2;
        commandRayVisual.useWorldSpace = true;
        commandRayVisual.enabled = false;
    }

    private void UpdateCommandRay()
    {
        if (commandRayVisual == null) return;
        Ray ray = GetCommandRay();
        Vector3 end = Physics.Raycast(ray, out RaycastHit hit,
                                      commandRayLength, groundMask)
            ? hit.point
            : ray.origin + ray.direction * commandRayLength;

        commandRayVisual.enabled = true;
        commandRayVisual.SetPosition(0, ray.origin);
        commandRayVisual.SetPosition(1, end);
    }

    private void HideCommandRay()
    {
        if (commandRayVisual != null) commandRayVisual.enabled = false;
    }

    private Ray GetCommandRay()
    {
        if (rightControllerTransform != null)
            return new Ray(rightControllerTransform.position,
                           rightControllerTransform.forward);

        Camera cam = Camera.main;
        return new Ray(cam.transform.position, cam.transform.forward);
    }

    // ─────────────────────────────────────────────
    // AGENT INITIALIZATION
    // ─────────────────────────────────────────────

    private void InitializeReceivers()
    {
        allReceivers.Clear();

        if (FactionManager.Instance == null)
        {
            Debug.LogWarning("[VRCommander] FactionManager not found.");
            return;
        }

        foreach (var member in FactionManager.Instance
                 .GetFactionMembers(FactionType.Alpha))
        {
            if (member == null) continue;
            if (member.gameObject.CompareTag("Player")) continue;

            CommandReceiver receiver = member.GetComponent<CommandReceiver>();
            if (receiver != null)
            {
                allReceivers.Add(receiver);
                CreateSelectionRing(receiver);
            }
        }

        SelectAllAllies();
        initialized = true;

        if (debugLog)
            Debug.Log($"[VRCommander] Ready. {allReceivers.Count} receivers.");
    }

    // ─────────────────────────────────────────────
    // SELECTION VISUALS
    // ─────────────────────────────────────────────

    private void CreateSelectionRing(CommandReceiver receiver)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "SelectionRing";
        ring.transform.SetParent(receiver.transform);
        ring.transform.localPosition = Vector3.up * 0.05f;
        ring.transform.localScale = new Vector3(
            selectionIndicatorScale * 3f, 0.01f, selectionIndicatorScale * 3f);

        Collider col = ring.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = ring.GetComponent<Renderer>();
        Material mat = new Material(
            Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = selectedColor;
        rend.material = mat;

        ring.SetActive(false);
        selectionRings[receiver] = ring;
    }

    private void ShowCommandFeedback(CommandReceiver receiver,
                                     CommandReceiver.CommandStatus status,
                                     Color commandColor)
    {
        Color c = status switch
        {
            CommandReceiver.CommandStatus.Accepted => commandColor,
            CommandReceiver.CommandStatus.Queued   => queuedColor,
            CommandReceiver.CommandStatus.Rejected => rejectedColor,
            _ => commandColor
        };

        feedbackTimers[receiver] = feedbackDuration;
        feedbackColors[receiver] = c;

        if (selectionRings.ContainsKey(receiver))
        {
            var ring = selectionRings[receiver];
            ring.SetActive(true);
            ring.GetComponent<Renderer>().material.color = c;
            ring.transform.localScale = new Vector3(
                selectionIndicatorScale * 4f, 0.02f, selectionIndicatorScale * 4f);
        }
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var kvp in selectionRings)
        {
            CommandReceiver receiver = kvp.Key;
            GameObject ring = kvp.Value;
            if (ring == null) continue;

            if (feedbackTimers.ContainsKey(receiver) && feedbackTimers[receiver] > 0f)
            {
                feedbackTimers[receiver] -= Time.deltaTime;
                float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.15f;
                float s = selectionIndicatorScale * 3.5f * pulse;
                ring.transform.localScale = new Vector3(s, 0.02f, s);
                ring.SetActive(true);
                ring.GetComponent<Renderer>().material.color = feedbackColors[receiver];

                if (feedbackTimers[receiver] <= 0f)
                {
                    feedbackTimers.Remove(receiver);
                    feedbackColors.Remove(receiver);
                    ring.transform.localScale = new Vector3(
                        selectionIndicatorScale * 3f, 0.01f,
                        selectionIndicatorScale * 3f);
                }
            }
            else
            {
                bool isSelected = selectedReceivers.Contains(receiver);
                ring.SetActive(isSelected);
                if (isSelected)
                    ring.GetComponent<Renderer>().material.color = selectedColor;
            }
        }
    }

    // ─────────────────────────────────────────────
    // DEBUG HELPERS
    // ─────────────────────────────────────────────

    private void DebugKillAllEnemies()
    {
        if (FactionManager.Instance == null) return;
        foreach (var agent in FactionManager.Instance
                 .GetFactionMembers(FactionType.Beta))
        {
            if (agent == null) continue;
            agent.GetComponent<HealthComponent>()?.TakeDamage(9999);
        }
        Debug.Log("[DEBUG] Killed all enemies.");
    }

    private void DebugForceWin()
    {
        var objs = MissionManager_Demo.Instance?.GetObjectives();
        if (objs == null) return;
        foreach (var o in objs) o?.DebugForceComplete();
    }

    private void DebugForceLose()
    {
        healthComponent?.TakeDamage(9999);
    }
}