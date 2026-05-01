using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using System.Collections.Generic;
using UnityEngine.AI;

public class DemoCommandSystem : MonoBehaviour
{
    [Header("VR Input")]
    [SerializeField] private InputActionReference rightGripAction;
    [SerializeField] private InputActionReference rightTriggerAction;
    [SerializeField] private InputActionReference bButtonAction;

    [Header("References")]
    [SerializeField] private Transform rightHandController;
    [SerializeField] private Transform playerHead;

    [Header("Command Settings")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float commandRayLength = 50f;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject commandModeIndicator;
    [SerializeField] private LineRenderer commandRayVisual;
    [SerializeField] private Color commandRayColor = new Color(0.2f, 0.6f, 1f);

    [Header("Keyboard Fallback")]
    [SerializeField] private KeyCode commandModeKey = KeyCode.RightShift;
    [SerializeField] private KeyCode regroupKey = KeyCode.R;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private List<CommandReceiver> allReceivers = new List<CommandReceiver>();
    private VRCommander_Demo vrCommander;
    private bool isVRActive = false;
    private bool isCommandMode = false;
    private bool wasCommandMode = false;

    // Prevent shooting when command mode triggers fire
    private bool triggerConsumedByCommand = false;

    private void Awake()
    {
        vrCommander = GetComponent<VRCommander_Demo>();
        isVRActive = XRSettings.isDeviceActive;

        SetupCommandRayVisual();
    }

    private void OnEnable()
    {
        rightGripAction?.action.Enable();
        rightTriggerAction?.action.Enable();
        bButtonAction?.action.Enable();

        MissionManager_Demo.OnMissionStart += HandleMissionStart;
    }

    private void OnDisable()
    {
        rightGripAction?.action.Disable();
        rightTriggerAction?.action.Disable();
        bButtonAction?.action.Disable();

        MissionManager_Demo.OnMissionStart -= HandleMissionStart;
    }

    private void Start()
    {
        // Delay to let FactionManager register all agents
        Invoke(nameof(FindAllReceivers), 1f);
    }

    private void Update()
    {
        if (!MissionManager_Demo.Instance.IsMissionActive) return;

        UpdateCommandMode();

        if (isCommandMode)
        {
            HandleCommandInput();
            UpdateCommandRay();
        }
        else
        {
            HideCommandRay();
        }

        // Tell VRCommander to block shooting while in command mode
        // so grip+trigger doesn't fire weapon
        if (vrCommander != null)
            vrCommander.SetCommandModeActive(isCommandMode);

        // Show/hide command mode indicator
        if (commandModeIndicator != null)
            commandModeIndicator.SetActive(isCommandMode);

        // Log mode changes
        if (isCommandMode != wasCommandMode)
        {
            if (debugLog)
                Debug.Log($"[DemoCommand] Command mode: {(isCommandMode ? "ON" : "OFF")}");
            wasCommandMode = isCommandMode;
        }
    }

    // ── Command Mode Detection ────────────────────────────

    private void UpdateCommandMode()
    {
        if (isVRActive)
        {
            // Hold right grip = command mode
            float gripValue = rightGripAction?.action.ReadValue<float>() ?? 0f;
            isCommandMode = gripValue > 0.5f;
        }
        else
        {
            // Hold right shift = command mode
            isCommandMode = Input.GetKey(commandModeKey);
        }
    }

    // ── Command Input ────────────────────────────────────

    private void HandleCommandInput()
    {
        if (isVRActive)
            HandleVRCommandInput();
        else
            HandleKeyboardCommandInput();
    }

    private void HandleVRCommandInput()
    {
        // Right Trigger while in command mode = Move To
        float triggerVal = rightTriggerAction?.action.ReadValue<float>() ?? 0f;
        bool triggerPressed = triggerVal > 0.5f && !triggerConsumedByCommand;

        if (triggerPressed)
        {
            TryIssueMoveCommand();
            triggerConsumedByCommand = true;
        }

        if (triggerVal <= 0.1f)
            triggerConsumedByCommand = false;

        // B Button = Regroup
        float bVal = bButtonAction?.action.ReadValue<float>() ?? 0f;
        if (bVal > 0.5f)
            IssueRegroup();
    }

    private void HandleKeyboardCommandInput()
    {
        // Mouse0 while command mode = Move To
        if (Input.GetMouseButtonDown(0))
            TryIssueMoveCommand();

        // R key while command mode = Regroup
        if (Input.GetKeyDown(regroupKey))
            IssueRegroup();
    }

    // ── Commands ─────────────────────────────────────────

    private void TryIssueMoveCommand()
    {
        Ray ray = GetCommandRay();

        if (Physics.Raycast(ray, out RaycastHit hit, commandRayLength, groundMask))
        {
            // Snap to NavMesh
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            {
                IssueMoveOrder(navHit.position);
            }
        }
    }

    private void IssueMoveOrder(Vector3 position)
    {
        if (allReceivers.Count == 0)
        {
            Debug.LogWarning("[DemoCommand] No receivers found. Did agents spawn?");
            return;
        }

        int accepted = 0;
        foreach (var receiver in allReceivers)
        {
            if (receiver == null) continue;
            var status = receiver.ReceiveMoveOrder(position);
            if (status == CommandReceiver.CommandStatus.Accepted) accepted++;
        }

        // Spawn existing command marker
        CommandMarker.Spawn(position, new Color(0.2f, 0.6f, 1f));

        if (debugLog)
            Debug.Log($"[DemoCommand] Move To {position}. " +
                      $"Accepted by {accepted}/{allReceivers.Count} agents.");
    }

    private void IssueRegroup()
    {
        if (allReceivers.Count == 0) return;

        Vector3 regroupPos = transform.position;
        int accepted = 0;

        foreach (var receiver in allReceivers)
        {
            if (receiver == null) continue;
            var status = receiver.ReceiveRegroup(regroupPos);
            if (status == CommandReceiver.CommandStatus.Accepted) accepted++;
        }

        CommandMarker.Spawn(regroupPos, new Color(0.2f, 1f, 0.2f));

        if (debugLog)
            Debug.Log($"[DemoCommand] Regroup at player. " +
                      $"Accepted by {accepted}/{allReceivers.Count} agents.");
    }

    // ── Ray Visual ───────────────────────────────────────

    private void SetupCommandRayVisual()
    {
        if (commandRayVisual == null) return;

        Material mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = commandRayColor;
        commandRayVisual.material = mat;
        commandRayVisual.startWidth = 0.008f;
        commandRayVisual.endWidth = 0.002f;
        commandRayVisual.positionCount = 2;
        commandRayVisual.useWorldSpace = true;
        commandRayVisual.enabled = false;
    }

    private void UpdateCommandRay()
    {
        if (commandRayVisual == null) return;

        Ray ray = GetCommandRay();
        Vector3 endPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, commandRayLength, groundMask))
            endPoint = hit.point;
        else
            endPoint = ray.origin + ray.direction * commandRayLength;

        commandRayVisual.enabled = true;
        commandRayVisual.SetPosition(0, ray.origin);
        commandRayVisual.SetPosition(1, endPoint);
    }

    private void HideCommandRay()
    {
        if (commandRayVisual != null)
            commandRayVisual.enabled = false;
    }

    private Ray GetCommandRay()
    {
        if (isVRActive && rightHandController != null)
            return new Ray(rightHandController.position, rightHandController.forward);

        // Keyboard: ray from camera center
        Camera cam = Camera.main;
        return new Ray(cam.transform.position, cam.transform.forward);
    }

    // ── Agent Discovery ──────────────────────────────────

    private void FindAllReceivers()
    {
        allReceivers.Clear();

        if (FactionManager.Instance == null)
        {
            Debug.LogWarning("[DemoCommand] FactionManager not found.");
            return;
        }

        foreach (AgentIdentity agent in FactionManager.Instance
                 .GetFactionMembers(FactionType.Alpha))
        {
            if (agent == null) continue;

            // Skip the player (player has AgentIdentity too)
            if (agent.gameObject.CompareTag("Player")) continue;

            CommandReceiver receiver = agent.GetComponent<CommandReceiver>();
            if (receiver != null)
                allReceivers.Add(receiver);
        }

        if (debugLog)
            Debug.Log($"[DemoCommand] Found {allReceivers.Count} command receivers.");
    }

    private void HandleMissionStart()
    {
        // Refresh receivers when mission starts
        Invoke(nameof(FindAllReceivers), 0.5f);
    }
}