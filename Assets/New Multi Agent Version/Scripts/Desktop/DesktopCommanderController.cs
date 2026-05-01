// Assets/Scripts/Desktop/DesktopCommanderController.cs
using UnityEngine;
using System.Collections.Generic;

public class DesktopCommanderController : MonoBehaviour, ICommandInterface
{
    [Header("Camera Movement")]
    [SerializeField] private float panSpeed = 30f;
    [SerializeField] private float zoomSpeed = 15f;
    [SerializeField] private float rotateSpeed = 80f;
    [SerializeField] private float minHeight = 15f;
    [SerializeField] private float maxHeight = 50f;

    [Header("Camera Bounds")]
    [SerializeField] private float mapMinX = -50f;
    [SerializeField] private float mapMaxX = 50f;
    [SerializeField] private float mapMinZ = -50f;
    [SerializeField] private float mapMaxZ = 50f;

    [Header("Selection")]
    [SerializeField] private Color selectedColor = Color.cyan;
    [SerializeField] private float selectionIndicatorScale = 0.5f;

    [Header("Command")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask agentMask;

    // Receivers
    private List<CommandReceiver> allReceivers = new List<CommandReceiver>();
    private List<CommandReceiver> selectedReceivers = new List<CommandReceiver>();
    private int currentSelectionIndex = -1;
    private bool allSelected = true;

    // Selection visuals
    private Dictionary<CommandReceiver, GameObject> selectionRings = new Dictionary<CommandReceiver, GameObject>();

    // Camera ref
    private Camera cam;
    private bool initialized = false;



    [Header("Command Feedback")]
    [SerializeField] private Color moveToColor = new Color(0.2f, 0.5f, 1f);      // Blue
    [SerializeField] private Color regroupColor = new Color(0.2f, 1f, 0.2f);     // Green
    [SerializeField] private Color holdColor = Color.white;
    [SerializeField] private Color queuedColor = new Color(1f, 0.6f, 0f);        // Orange
    [SerializeField] private Color rejectedColor = Color.red;
    [SerializeField] private float feedbackDuration = 1.5f;

    private Dictionary<CommandReceiver, float> feedbackTimers = new Dictionary<CommandReceiver, float>();
    private Dictionary<CommandReceiver, Color> feedbackColors = new Dictionary<CommandReceiver, Color>();


    private MatchResetManager matchManager;




    private void Start()
    {

        matchManager = FindObjectOfType<MatchResetManager>();

    
        cam = GetComponentInChildren<Camera>();
        if (cam == null)
        {
            Debug.LogError("[DESKTOP] No camera found as child!");
            return;
        }

        Invoke(nameof(Initialize), 0.3f);
    }

    private void Initialize()
    {
        allReceivers.Clear();
        foreach (var member in FactionManager.Instance.GetFactionMembers(FactionType.Alpha))
        {
            CommandReceiver receiver = member.GetComponent<CommandReceiver>();
            if (receiver != null)
            {
                allReceivers.Add(receiver);
                CreateSelectionRing(receiver);
            }
        }

        // Start with all selected
        SelectAllAllies();
        initialized = true;
        Debug.Log($"[DESKTOP] Initialized with {allReceivers.Count} agents");
    }

    private void Update()
    {
        if (!initialized) return;
        if (matchManager != null && !matchManager.IsMatchActive()) return;

        HandleCameraMovement();
        HandleSelection();
        HandleCommands();
    }

    // Camera
    private void HandleCameraMovement()
    {
        Vector3 move = Vector3.zero;

        Vector3 forward = cam.transform.forward;
        Vector3 right = cam.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // Block WASD when Ctrl held
        if (!Input.GetKey(KeyCode.LeftControl))
        {
            if (Input.GetKey(KeyCode.W)) move += forward;
            if (Input.GetKey(KeyCode.S)) move -= forward;
            if (Input.GetKey(KeyCode.D)) move += right;
            if (Input.GetKey(KeyCode.A)) move -= right;
        }

        transform.position += move * panSpeed * Time.deltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            Vector3 pos = transform.position;
            pos.y -= scroll * zoomSpeed;
            pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
            transform.position = pos;
        }

        if (Input.GetKey(KeyCode.Q))
            transform.Rotate(Vector3.up, -rotateSpeed * Time.deltaTime, Space.World);
        if (Input.GetKey(KeyCode.E))
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

        Vector3 clamped = transform.position;
        clamped.x = Mathf.Clamp(clamped.x, mapMinX, mapMaxX);
        clamped.z = Mathf.Clamp(clamped.z, mapMinZ, mapMaxZ);
        transform.position = clamped;
    }

    private void HandleSelection()
    {
        // Tab = cycle agents
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            allSelected = false;
            currentSelectionIndex = (currentSelectionIndex + 1) % allReceivers.Count;
            SelectAgent(allReceivers[currentSelectionIndex].GetComponent<AgentIdentity>());
        }

        // Ctrl+A = select all
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.A))
        {
            SelectAllAllies();
        }

        // Click near agent = select that agent
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                CommandReceiver closest = null;
                float closestDist = 3f;

                foreach (var receiver in allReceivers)
                {
                    if (receiver == null) continue;
                    float dist = Vector3.Distance(hit.point, receiver.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = receiver;
                    }
                }

                if (closest != null)
                {
                    SelectAgent(closest.GetComponent<AgentIdentity>());
                    return;
                }
            }
        }

        UpdateSelectionVisuals();
    }

    // Commands

    private void HandleCommands()
    {
        if (selectedReceivers.Count == 0) return;

        // Left click on ground = Move To
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                // Skip if clicked near an Alpha agent (handled by selection)
                foreach (var receiver in allReceivers)
                {
                    if (receiver != null && Vector3.Distance(hit.point, receiver.transform.position) < 3f)
                        return;
                }

                // Ground click — issue move
                IssueMoveOrder(hit.point);
            }
        }

        // Right click = Regroup
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                IssueRegroup(hit.point);
            }
        }

        if (Input.GetKeyDown(KeyCode.H))
            IssueHold();

        if (Input.GetKeyDown(KeyCode.R))
            IssueRelease();
    }
    
    // ICommandInterface

    public void IssueMoveOrder(Vector3 worldPosition)
    {
        foreach (var receiver in selectedReceivers)
        {
            var status = receiver.ReceiveMoveOrder(worldPosition);
            ShowCommandFeedback(receiver, status, moveToColor);
        }
        CommandMarker.Spawn(worldPosition, moveToColor);
    }

    public void IssueRegroup(Vector3 regroupPosition)
    {
        foreach (var receiver in selectedReceivers)
        {
            var status = receiver.ReceiveRegroup(regroupPosition);
            ShowCommandFeedback(receiver, status, regroupColor);
        }
        CommandMarker.Spawn(regroupPosition, regroupColor);
    }

    public void IssueHold()
    {
        foreach (var receiver in selectedReceivers)
        {
            var status = receiver.ReceiveHold();
            ShowCommandFeedback(receiver, status, holdColor);
        }
    }

    public void IssueRelease()
    {
        foreach (var receiver in selectedReceivers)
        {
            var status = receiver.ReceiveRelease();
            ShowCommandFeedback(receiver, status, selectedColor);
        }
    }



    // new for command 

    private void ShowCommandFeedback(CommandReceiver receiver, CommandReceiver.CommandStatus status, Color commandColor)
    {
        Color feedbackColor = status switch
        {
            CommandReceiver.CommandStatus.Accepted => commandColor,
            CommandReceiver.CommandStatus.Queued => queuedColor,
            CommandReceiver.CommandStatus.Rejected => rejectedColor,
            _ => commandColor
        };

        feedbackTimers[receiver] = feedbackDuration;
        feedbackColors[receiver] = feedbackColor;

        // Immediately set ring color
        if (selectionRings.ContainsKey(receiver))
        {
            var ring = selectionRings[receiver];
            ring.SetActive(true);
            ring.GetComponent<Renderer>().material.color = feedbackColor;

            // Scale up slightly for emphasis
            ring.transform.localScale = new Vector3(
                selectionIndicatorScale * 4f, 0.02f, selectionIndicatorScale * 4f);
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

        Debug.Log($"[DESKTOP] Selected: {agent.gameObject.name}");
    }

    public void SelectAllAllies()
    {
        allSelected = true;
        selectedReceivers.Clear();
        selectedReceivers.AddRange(allReceivers);
        currentSelectionIndex = -1;
        Debug.Log($"[DESKTOP] Selected ALL ({selectedReceivers.Count} agents)");
    }

    // Selection Ring Visuals

    private void CreateSelectionRing(CommandReceiver receiver)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "SelectionRing";
        ring.transform.SetParent(receiver.transform);
        ring.transform.localPosition = Vector3.up * 0.05f;
        ring.transform.localScale = new Vector3(selectionIndicatorScale * 3f, 0.01f, selectionIndicatorScale * 3f);

        Collider col = ring.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = ring.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        rend.material.color = selectedColor;

        ring.SetActive(false);
        selectionRings[receiver] = ring;
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var kvp in selectionRings)
        {
            CommandReceiver receiver = kvp.Key;
            GameObject ring = kvp.Value;

            // Check if this agent has active command feedback
            if (feedbackTimers.ContainsKey(receiver) && feedbackTimers[receiver] > 0f)
            {
                feedbackTimers[receiver] -= Time.deltaTime;

                // Pulse effect — scale oscillates
                float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.15f;
                float scale = selectionIndicatorScale * 3.5f * pulse;
                ring.transform.localScale = new Vector3(scale, 0.02f, scale);

                ring.SetActive(true);
                ring.GetComponent<Renderer>().material.color = feedbackColors[receiver];

                // Feedback expired — return to normal
                if (feedbackTimers[receiver] <= 0f)
                {
                    feedbackTimers.Remove(receiver);
                    feedbackColors.Remove(receiver);

                    // Return to normal selection state
                    ring.transform.localScale = new Vector3(
                        selectionIndicatorScale * 3f, 0.01f, selectionIndicatorScale * 3f);
                }
            }
            else
            {
                // Normal selection display
                bool isSelected = selectedReceivers.Contains(receiver);
                ring.SetActive(isSelected);

                if (isSelected)
                    ring.GetComponent<Renderer>().material.color = selectedColor;
            }
        }
    }

    // Public access for other systems
    public List<CommandReceiver> GetSelectedReceivers() => selectedReceivers;
    public bool IsAllSelected => allSelected;
}