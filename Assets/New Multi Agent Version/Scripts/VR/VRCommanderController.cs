// // Assets/Scripts/VR/VRCommanderController.cs
// using UnityEngine;
// using System.Collections.Generic;
// using UnityEngine.AI;

// public class VRCommanderController : MonoBehaviour, ICommandInterface
// {
//     [Header("References")]
//     [SerializeField] private Transform rightController;
//     [SerializeField] private Transform leftController;
//     [SerializeField] private LayerMask groundMask;
//     [SerializeField] private LayerMask agentMask;

//     [Header("Selection")]
//     [SerializeField] private Color selectedColor = Color.cyan;
//     [SerializeField] private float selectionIndicatorScale = 0.5f;

//     [Header("Command Feedback")]
//     [SerializeField] private Color moveToColor = new Color(0.2f, 0.5f, 1f);
//     [SerializeField] private Color regroupColor = new Color(0.2f, 1f, 0.2f);
//     [SerializeField] private Color holdColor = Color.white;
//     [SerializeField] private Color queuedColor = new Color(1f, 0.6f, 0f);
//     [SerializeField] private Color rejectedColor = Color.red;
//     [SerializeField] private float feedbackDuration = 1.5f;

//     private List<CommandReceiver> allReceivers = new List<CommandReceiver>();
//     private List<CommandReceiver> selectedReceivers = new List<CommandReceiver>();
//     private int currentSelectionIndex = -1;
//     private bool allSelected = true;

//     private Dictionary<CommandReceiver, GameObject> selectionRings = new Dictionary<CommandReceiver, GameObject>();
//     private Dictionary<CommandReceiver, float> feedbackTimers = new Dictionary<CommandReceiver, float>();
//     private Dictionary<CommandReceiver, Color> feedbackColors = new Dictionary<CommandReceiver, Color>();



//     private bool initialized = false;

//     private TacticalHologram tacticalHologram;


//     private void Start()
//     {
//         Invoke(nameof(Initialize), 0.5f);
//     }

//     private void Initialize()
//     {
//         allReceivers.Clear();
//         foreach (var member in FactionManager.Instance.GetFactionMembers(FactionType.Alpha))
//         {
//             CommandReceiver receiver = member.GetComponent<CommandReceiver>();
//             if (receiver != null)
//             {
//                 allReceivers.Add(receiver);
//                 CreateSelectionRing(receiver);
//             }
//         }

//         SelectAllAllies();
//         initialized = true;
//     }

//     private void Update()
//     {
//         if (!initialized) return;

//         HandleSelection();
//         HandleCommands();
//         UpdateSelectionVisuals();
//     }

//     private void HandleSelection()
//     {
//         if (Input.GetKeyDown(KeyCode.Tab))
//         {
//             allSelected = false;
//             currentSelectionIndex = (currentSelectionIndex + 1) % allReceivers.Count;
//             SelectAgent(allReceivers[currentSelectionIndex].GetComponent<AgentIdentity>());
//         }

//         if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.A))
//             SelectAllAllies();
//     }

//     private void HandleCommands()
//     {
//         if (selectedReceivers.Count == 0) return;

//         // Cache hologram reference
//         if (tacticalHologram == null)
//             tacticalHologram = FindObjectOfType<TacticalHologram>();

//         // Check hologram hover every frame for preview dot
//         if (tacticalHologram != null && rightController != null)
//         {
//             Ray ray = new Ray(rightController.position, rightController.forward);
//             tacticalHologram.TryGetWorldPosition(ray, out _);
//         }

//         // T = Move To
//         if (Input.GetKeyDown(KeyCode.T))
//         {
//             // Priority 1: Try hologram
//             if (TryHologramMoveOrder()) return;

//             // Priority 2: Try world ground
//             Ray ray = new Ray(rightController.position, rightController.forward);
//             if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundMask))
//                 IssueMoveOrder(hit.point);
//         }

//         // G = Regroup (changed from R to avoid conflicts)
//         if (Input.GetKeyDown(KeyCode.G))
//         {
//             if (TryHologramRegroup()) return;

//             Ray ray = new Ray(rightController.position, rightController.forward);
//             if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundMask))
//                 IssueRegroup(hit.point);
//         }

//         // H = Hold
//         if (Input.GetKeyDown(KeyCode.H))
//             IssueHold();

//         // L = Release
//         if (Input.GetKeyDown(KeyCode.L))
//             IssueRelease();
//     }



//     private bool TryHologramMoveOrder()
//     {
//         if (tacticalHologram == null || rightController == null) return false;
//         if (!tacticalHologram.IsPointerOnHologram) return false;

//         Ray ray = new Ray(rightController.position, rightController.forward);
//         if (tacticalHologram.TryGetWorldPosition(ray, out Vector3 worldPos))
//         {
//             // Validate position is on NavMesh
//             if (UnityEngine.AI.NavMesh.SamplePosition(worldPos, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
//             {
//                 IssueMoveOrder(hit.position);
//                 tacticalHologram.HidePreview();
//                 return true;
//             }
//         }
//         return false;
//     }

//     private bool TryHologramRegroup()
//     {
//         if (tacticalHologram == null || rightController == null) return false;
//         if (!tacticalHologram.IsPointerOnHologram) return false;

//         Ray ray = new Ray(rightController.position, rightController.forward);
//         if (tacticalHologram.TryGetWorldPosition(ray, out Vector3 worldPos))
//         {
//             if (UnityEngine.AI.NavMesh.SamplePosition(worldPos, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
//             {
//                 IssueRegroup(hit.position);
//                 tacticalHologram.HidePreview();
//                 return true;
//             }
//         }
//         return false;
//     }


//     public void IssueMoveOrder(Vector3 worldPosition)
//     {
//         foreach (var receiver in selectedReceivers)
//         {
//             var status = receiver.ReceiveMoveOrder(worldPosition);
//             ShowCommandFeedback(receiver, status, moveToColor);
//         }
//         CommandMarker.Spawn(worldPosition, moveToColor);
//     }

//     public void IssueRegroup(Vector3 regroupPosition)
//     {
//         foreach (var receiver in selectedReceivers)
//         {
//             var status = receiver.ReceiveRegroup(regroupPosition);
//             ShowCommandFeedback(receiver, status, regroupColor);
//         }
//         CommandMarker.Spawn(regroupPosition, regroupColor);
//     }

//     public void IssueHold()
//     {
//         foreach (var receiver in selectedReceivers)
//         {
//             var status = receiver.ReceiveHold();
//             ShowCommandFeedback(receiver, status, holdColor);
//         }
//     }

//     public void IssueRelease()
//     {
//         foreach (var receiver in selectedReceivers)
//         {
//             var status = receiver.ReceiveRelease();
//             ShowCommandFeedback(receiver, status, selectedColor);
//         }
//     }

//     public void SelectAgent(AgentIdentity agent)
//     {
//         if (agent == null) return;

//         allSelected = false;
//         selectedReceivers.Clear();

//         CommandReceiver receiver = agent.GetComponent<CommandReceiver>();
//         if (receiver != null)
//         {
//             selectedReceivers.Add(receiver);
//             currentSelectionIndex = allReceivers.IndexOf(receiver);
//         }
//     }

//     public void SelectAllAllies()
//     {
//         allSelected = true;
//         selectedReceivers.Clear();
//         selectedReceivers.AddRange(allReceivers);
//         currentSelectionIndex = -1;
//     }

//     private void CreateSelectionRing(CommandReceiver receiver)
//     {
//         GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
//         ring.name = "SelectionRing";
//         ring.transform.SetParent(receiver.transform);
//         ring.transform.localPosition = Vector3.up * 0.05f;
//         ring.transform.localScale = new Vector3(selectionIndicatorScale * 3f, 0.01f, selectionIndicatorScale * 3f);

//         Collider col = ring.GetComponent<Collider>();
//         if (col != null) Destroy(col);

//         Renderer rend = ring.GetComponent<Renderer>();
//         Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
//         mat.color = selectedColor;
//         rend.material = mat;

//         ring.SetActive(false);
//         selectionRings[receiver] = ring;
//     }

//     private void ShowCommandFeedback(CommandReceiver receiver, CommandReceiver.CommandStatus status, Color commandColor)
//     {
//         Color feedbackColor = status switch
//         {
//             CommandReceiver.CommandStatus.Accepted => commandColor,
//             CommandReceiver.CommandStatus.Queued => queuedColor,
//             CommandReceiver.CommandStatus.Rejected => rejectedColor,
//             _ => commandColor
//         };

//         feedbackTimers[receiver] = feedbackDuration;
//         feedbackColors[receiver] = feedbackColor;

//         if (selectionRings.ContainsKey(receiver))
//         {
//             var ring = selectionRings[receiver];
//             ring.SetActive(true);
//             ring.GetComponent<Renderer>().material.color = feedbackColor;
//             ring.transform.localScale = new Vector3(selectionIndicatorScale * 4f, 0.02f, selectionIndicatorScale * 4f);
//         }
//     }

//     private void UpdateSelectionVisuals()
//     {
//         foreach (var kvp in selectionRings)
//         {
//             CommandReceiver receiver = kvp.Key;
//             GameObject ring = kvp.Value;

//             if (feedbackTimers.ContainsKey(receiver) && feedbackTimers[receiver] > 0f)
//             {
//                 feedbackTimers[receiver] -= Time.deltaTime;

//                 float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.15f;
//                 float scale = selectionIndicatorScale * 3.5f * pulse;
//                 ring.transform.localScale = new Vector3(scale, 0.02f, scale);
//                 ring.SetActive(true);
//                 ring.GetComponent<Renderer>().material.color = feedbackColors[receiver];

//                 if (feedbackTimers[receiver] <= 0f)
//                 {
//                     feedbackTimers.Remove(receiver);
//                     feedbackColors.Remove(receiver);
//                     ring.transform.localScale = new Vector3(selectionIndicatorScale * 3f, 0.01f, selectionIndicatorScale * 3f);
//                 }
//             }
//             else
//             {
//                 bool isSelected = selectedReceivers.Contains(receiver);
//                 ring.SetActive(isSelected);

//                 if (isSelected)
//                     ring.GetComponent<Renderer>().material.color = selectedColor;
//             }
//         }
//     }
// }


// Assets/Scripts/VR/VRCommanderController.cs
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

public class VRCommanderController : MonoBehaviour, ICommandInterface
{
    [Header("XR References")]
    [SerializeField] private Transform rightControllerTransform;
    [SerializeField] private Transform leftControllerTransform;

    [Header("Layers")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask agentMask;

    [Header("Selection")]
    [SerializeField] private Color selectedColor = Color.cyan;
    [SerializeField] private float selectionIndicatorScale = 0.5f;

    [Header("Command Feedback")]
    [SerializeField] private Color moveToColor = new Color(0.2f, 0.5f, 1f);
    [SerializeField] private Color regroupColor = new Color(0.2f, 1f, 0.2f);
    [SerializeField] private Color holdColor = Color.white;
    [SerializeField] private Color queuedColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color rejectedColor = Color.red;
    [SerializeField] private float feedbackDuration = 1.5f;

    // Receivers
    private List<CommandReceiver> allReceivers = new List<CommandReceiver>();
    private List<CommandReceiver> selectedReceivers = new List<CommandReceiver>();
    private int currentSelectionIndex = -1;
    private bool allSelected = true;

    // Visuals
    private Dictionary<CommandReceiver, GameObject> selectionRings = new Dictionary<CommandReceiver, GameObject>();
    private Dictionary<CommandReceiver, float> feedbackTimers = new Dictionary<CommandReceiver, float>();
    private Dictionary<CommandReceiver, Color> feedbackColors = new Dictionary<CommandReceiver, Color>();

    // XR Input
    private InputDevice rightHand;
    private InputDevice leftHand;
    private bool rightTriggerPrev = false;
    private bool rightGripPrev = false;
    private bool leftTriggerPrev = false;
    private bool leftGripPrev = false;
    private bool rightStickPrev = false;
    private bool leftStickPrev = false;

    // Hologram
    private TacticalHologram tacticalHologram;

    private bool initialized = false;

    // Public
    public List<CommandReceiver> GetSelectedReceivers() => selectedReceivers;
    public bool IsAllSelected => allSelected;

    private MatchResetManager matchManager;

    private void Start()
    {
         matchManager = FindObjectOfType<MatchResetManager>();
        Invoke(nameof(Initialize), 0.5f);
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

        tacticalHologram = FindObjectOfType<TacticalHologram>();
        SelectAllAllies();
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;
        if (matchManager != null && !matchManager.IsMatchActive()) return;

        RefreshXRDevices();
        HandleXRInput();
        HandleKeyboardFallback(); // Keep keyboard for desktop testing
        UpdateHologramPreview();
        UpdateSelectionVisuals();
    }

    // XR Device

    private void RefreshXRDevices()
    {
        if (!rightHand.isValid)
            rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!leftHand.isValid)
            leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
    }

    private bool GetButtonDown(InputDevice device, InputFeatureUsage<bool> button, ref bool previous)
    {
        device.TryGetFeatureValue(button, out bool current);
        bool pressed = current && !previous;
        previous = current;
        return pressed;
    }

    // XR Input

    private void HandleXRInput()
    {
        if (!rightHand.isValid && !leftHand.isValid) return;

        // Right Trigger = Move To
        if (GetButtonDown(rightHand, CommonUsages.triggerButton, ref rightTriggerPrev))
        {
            TryIssueMoveCommand();
        }

        // Right Grip = Regroup
        if (GetButtonDown(rightHand, CommonUsages.gripButton, ref rightGripPrev))
        {
            TryIssueRegroupCommand();
        }

        // Left Trigger = Hold
        if (GetButtonDown(leftHand, CommonUsages.triggerButton, ref leftTriggerPrev))
        {
            IssueHold();
        }

        // Left Grip = Release
        if (GetButtonDown(leftHand, CommonUsages.gripButton, ref leftGripPrev))
        {
            IssueRelease();
        }

        // Right Thumbstick Press = Cycle selection
        if (GetButtonDown(rightHand, CommonUsages.primary2DAxisClick, ref rightStickPrev))
        {
            CycleSelection();
        }

        // Left Thumbstick Press = Select all
        if (GetButtonDown(leftHand, CommonUsages.primary2DAxisClick, ref leftStickPrev))
        {
            SelectAllAllies();
        }
    }

    // Keyboard fallback for desktop testing
    private void HandleKeyboardFallback()
    {
        if (Input.GetKeyDown(KeyCode.T)) TryIssueMoveCommand();
        if (Input.GetKeyDown(KeyCode.G)) TryIssueRegroupCommand();
        if (Input.GetKeyDown(KeyCode.H)) IssueHold();
        if (Input.GetKeyDown(KeyCode.L)) IssueRelease();
        if (Input.GetKeyDown(KeyCode.Tab)) CycleSelection();
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.A)) SelectAllAllies();
    }

    // Command Issuing

    private void TryIssueMoveCommand()
    {
        if (selectedReceivers.Count == 0) return;

        // Priority 1: Hologram
        if (TryHologramCommand(true)) return;

        // Priority 2: World raycast
        if (rightControllerTransform == null) return;
        Ray ray = new Ray(rightControllerTransform.position, rightControllerTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundMask))
            IssueMoveOrder(hit.point);
    }

    private void TryIssueRegroupCommand()
    {
        if (selectedReceivers.Count == 0) return;

        if (TryHologramCommand(false)) return;

        if (rightControllerTransform == null) return;
        Ray ray = new Ray(rightControllerTransform.position, rightControllerTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundMask))
            IssueRegroup(hit.point);
    }

    private bool TryHologramCommand(bool isMoveOrder)
    {
        if (tacticalHologram == null || rightControllerTransform == null) return false;
        if (!tacticalHologram.IsPointerOnHologram) return false;

        Ray ray = new Ray(rightControllerTransform.position, rightControllerTransform.forward);
        if (tacticalHologram.TryGetWorldPosition(ray, out Vector3 worldPos))
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(worldPos, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (isMoveOrder)
                    IssueMoveOrder(hit.position);
                else
                    IssueRegroup(hit.position);

                tacticalHologram.HidePreview();
                return true;
            }
        }
        return false;
    }

    private void UpdateHologramPreview()
    {
        if (tacticalHologram == null || rightControllerTransform == null) return;
        Ray ray = new Ray(rightControllerTransform.position, rightControllerTransform.forward);
        tacticalHologram.TryGetWorldPosition(ray, out _);
    }

    // Selection

    private void CycleSelection()
    {
        if (allReceivers.Count == 0) return;
        allSelected = false;
        currentSelectionIndex = (currentSelectionIndex + 1) % allReceivers.Count;
        SelectAgent(allReceivers[currentSelectionIndex].GetComponent<AgentIdentity>());
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
    }

    // Visuals

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
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = selectedColor;
        rend.material = mat;

        ring.SetActive(false);
        selectionRings[receiver] = ring;
    }

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

        if (selectionRings.ContainsKey(receiver))
        {
            var ring = selectionRings[receiver];
            ring.SetActive(true);
            ring.GetComponent<Renderer>().material.color = feedbackColor;
            ring.transform.localScale = new Vector3(selectionIndicatorScale * 4f, 0.02f, selectionIndicatorScale * 4f);
        }
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var kvp in selectionRings)
        {
            CommandReceiver receiver = kvp.Key;
            GameObject ring = kvp.Value;

            if (feedbackTimers.ContainsKey(receiver) && feedbackTimers[receiver] > 0f)
            {
                feedbackTimers[receiver] -= Time.deltaTime;

                float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.15f;
                float scale = selectionIndicatorScale * 3.5f * pulse;
                ring.transform.localScale = new Vector3(scale, 0.02f, scale);
                ring.SetActive(true);
                ring.GetComponent<Renderer>().material.color = feedbackColors[receiver];

                if (feedbackTimers[receiver] <= 0f)
                {
                    feedbackTimers.Remove(receiver);
                    feedbackColors.Remove(receiver);
                    ring.transform.localScale = new Vector3(selectionIndicatorScale * 3f, 0.01f, selectionIndicatorScale * 3f);
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
}