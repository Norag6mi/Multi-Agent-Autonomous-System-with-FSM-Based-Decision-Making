using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionObjective : MonoBehaviour
{
    public enum ObjectiveState
    {
        Inactive,
        Available,
        InProgress,
        Complete
    }

    [Header("Objective Settings")]
    [SerializeField] private string objectiveName = "Objective A";
    [SerializeField] private float interactionDuration = 12f;
    [SerializeField] private float activationRadius = 3f;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKeyboard = KeyCode.Mouse1;
    [SerializeField] private float vrTriggerThreshold = 0.7f;
    [SerializeField] private UnityEngine.InputSystem.InputActionReference leftTriggerAction;

    [Header("Objective Visuals")]
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private GameObject completeVisual;
    [SerializeField] private ParticleSystem completionParticles;

    [Header("Canvas UI")]
    [SerializeField] private Canvas objectiveCanvas;
    [SerializeField] private GameObject progressBarRoot;
    [SerializeField] private Image progressBarFill;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI objectiveNameText;
    [SerializeField] private float canvasHeightAboveGround = 2f;

    [Header("Colors")]
    [SerializeField] private Color availableColor = new Color(1f, 0.8f, 0f);
    [SerializeField] private Color inProgressColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color completeColor = new Color(0.2f, 1f, 0.4f);

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private ObjectiveState currentState = ObjectiveState.Inactive;
    private float interactionProgress = 0f;
    private bool playerInRange = false;
    private bool isVRActive = false;
    private Transform playerTransform;
  

    public float InteractionProgress => interactionDuration > 0
        ? interactionProgress / interactionDuration : 0f;
    public ObjectiveState CurrentState => currentState;
    public string ObjectiveName => objectiveName;
    public bool IsComplete => currentState == ObjectiveState.Complete;

    public static event System.Action<MissionObjective> OnObjectiveComplete;
    public static event System.Action<MissionObjective> OnInteractionStart;
    public static event System.Action<MissionObjective> OnInteractionEnd;


    // Goal behaviour
    public bool IsDiscovered => 
    GetComponent<ObjectiveDiscovery>()?.IsDiscovered ?? true;

    //

    private VRCommander_Demo_v2 playerCommander;
    private float nextFindPlayerTime = 0f;

    private void Awake()
    {
        isVRActive = UnityEngine.XR.XRSettings.isDeviceActive;
        leftTriggerAction?.action.Enable();
        ValidateAndFixCanvas();
    }

    private void OnDestroy()
    {
        leftTriggerAction?.action.Disable();
    }

    private void Start()
    {
        FindPlayer();
        // State starts Available — world visuals visible always
        // Minimap visibility controlled by ObjectiveDiscovery
        SetState(ObjectiveState.Available);

        if (objectiveNameText != null)
            objectiveNameText.text = objectiveName;
    }

    private void ValidateAndFixCanvas()
    {
        if (objectiveCanvas == null) return;

        objectiveCanvas.renderMode = RenderMode.WorldSpace;
        objectiveCanvas.worldCamera = Camera.main;

        // Keep attached to objective
        if (objectiveCanvas.transform.parent != transform)
            objectiveCanvas.transform.SetParent(transform, false);

        // Null-safe camera check
        Camera cam = Camera.main;
        if (cam != null && objectiveCanvas.transform.IsChildOf(cam.transform))
        {
            objectiveCanvas.transform.SetParent(transform, false);
            Debug.LogWarning($"[MissionObjective: {objectiveName}] Canvas was child of camera. Reparented.");
        }

        RectTransform canvasRect = objectiveCanvas.GetComponent<RectTransform>();
        if (canvasRect != null)
            canvasRect.sizeDelta = new Vector2(420f, 180f);

        objectiveCanvas.transform.localPosition = new Vector3(0f, canvasHeightAboveGround, 0f);
        objectiveCanvas.transform.localRotation = Quaternion.identity;
        objectiveCanvas.transform.localScale = Vector3.one * 0.01f; // bigger for visibility

        if (objectiveCanvas.GetComponent<ObjectiveCanvasBillboard>() == null)
            objectiveCanvas.gameObject.AddComponent<ObjectiveCanvasBillboard>();

        FixProgressBarAnchors();
    }

    private void FixProgressBarAnchors()
    {
        if (progressBarFill == null) return;

        // Anchors must be set for fill to work correctly
        RectTransform fillRect = progressBarFill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        progressBarFill.type = Image.Type.Filled;
        progressBarFill.fillMethod = Image.FillMethod.Horizontal;
        progressBarFill.fillOrigin = 0;
        progressBarFill.fillAmount = 0f;
    }

    private void FindPlayer()
    {
        playerTransform = null;
        playerCommander = null;

        GameObject tagged = GameObject.FindWithTag("Player");
        if (tagged != null)
        {
            playerTransform = tagged.transform;
            playerCommander = tagged.GetComponent<VRCommander_Demo_v2>();
            if (playerCommander == null)
                playerCommander = tagged.GetComponentInChildren<VRCommander_Demo_v2>();
        }

        if (playerTransform == null)
        {
            var commander = FindFirstObjectByType<VRCommander_Demo_v2>();
            if (commander != null)
            {
                playerCommander = commander;
                playerTransform = commander.transform;
            }
        }

        if (debugLog)
            Debug.Log($"[MissionObjective:{objectiveName}] PlayerTransform={(playerTransform ? playerTransform.name : "NULL")}");
    }
    public void Activate()
    {
        if (currentState == ObjectiveState.Complete) return;
        SetState(ObjectiveState.Available);
    }

    private void SetState(ObjectiveState newState)
    {
        currentState = newState;
        RefreshVisuals();

        if (debugLog)
            Debug.Log($"[MissionObjective: {objectiveName}] State → {newState}");
    }

    private void Update()
    {
        if (currentState == ObjectiveState.Complete) return;
        if (currentState == ObjectiveState.Inactive) return;

        if (playerTransform == null && Time.time >= nextFindPlayerTime)
        {
            nextFindPlayerTime = Time.time + 1f;
            FindPlayer();
        }

        CheckPlayerRange();
        HandleInteractionInput();
        RefreshCanvasUI();
    }
    private void CheckPlayerRange()
    {
        if (playerTransform == null) return;

        // Use flat distance (ignore Y) so crouching doesnt affect detection
        Vector3 flat = transform.position - playerTransform.position;
        flat.y = 0f;
        float distance = flat.magnitude;
        bool inRange = distance <= activationRadius;

        if (inRange != playerInRange)
        {
            playerInRange = inRange;
            if (!inRange && currentState == ObjectiveState.InProgress)
                CancelInteraction();
        }
    }

    private void HandleInteractionInput()
    {
        if (!playerInRange) return;
        if (currentState != ObjectiveState.Available &&
            currentState != ObjectiveState.InProgress) return;

        bool holding = IsInteractHeld();

        if (holding)
        {
            if (currentState == ObjectiveState.Available)
                BeginInteraction();
            AdvanceProgress();
        }
        else
        {
            if (currentState == ObjectiveState.InProgress)
                PauseInteraction();
        }
    }
    private bool IsInteractHeld()
    {
        bool vrHeld = leftTriggerAction != null && leftTriggerAction.action.IsPressed();
        bool keyboardHeld = Input.GetKey(interactKeyboard); // Mouse1 if set
        bool mouseHeld = Input.GetMouseButton(1);

        return vrHeld || keyboardHeld || mouseHeld;
    }
    private void BeginInteraction()
    {
        SetState(ObjectiveState.InProgress);
        playerCommander?.SetCanShoot(false);
        OnInteractionStart?.Invoke(this);

        if (debugLog)
            Debug.Log($"[MissionObjective: {objectiveName}] Interaction started.");
    }

    private void AdvanceProgress()
    {
        interactionProgress += Time.deltaTime;
        if (interactionProgress >= interactionDuration)
            CompleteObjective();
    }

    private void PauseInteraction()
    {
        SetState(ObjectiveState.Available);
        playerCommander?.SetCanShoot(true);
        OnInteractionEnd?.Invoke(this);

        if (debugLog)
            Debug.Log($"[MissionObjective: {objectiveName}] Paused at " +
                      $"{InteractionProgress * 100f:F0}%");
    }

    private void CancelInteraction()
    {
        interactionProgress = 0f;
        SetState(ObjectiveState.Available);
        playerCommander?.SetCanShoot(true);
        OnInteractionEnd?.Invoke(this);

        if (debugLog)
            Debug.Log($"[MissionObjective: {objectiveName}] Cancelled — player left.");
    }

    private void CompleteObjective()
    {
        interactionProgress = interactionDuration;
        SetState(ObjectiveState.Complete);
        playerCommander?.SetCanShoot(true);
        completionParticles?.Play();

        if (progressBarRoot != null)
            progressBarRoot.SetActive(false);

        OnInteractionEnd?.Invoke(this);
        OnObjectiveComplete?.Invoke(this);

        if (debugLog)
            Debug.Log($"[MissionObjective: {objectiveName}] COMPLETE.");
    }

    private void RefreshVisuals()
    {
        if (activeVisual != null)
            activeVisual.SetActive(currentState == ObjectiveState.Available ||
                                   currentState == ObjectiveState.InProgress);

        if (completeVisual != null)
            completeVisual.SetActive(currentState == ObjectiveState.Complete);
    }

    private void RefreshCanvasUI()
    {
        if (objectiveCanvas == null) return;

        bool showCanvas = Application.isEditor ||
                        playerInRange ||
                        currentState == ObjectiveState.InProgress ||
                        currentState == ObjectiveState.Complete;

        objectiveCanvas.gameObject.SetActive(showCanvas);
        if (!showCanvas) return;

        if (currentState == ObjectiveState.Complete)
        {
            if (progressBarRoot != null) progressBarRoot.SetActive(false);
            if (promptText != null) promptText.text = "OBJECTIVE COMPLETE";
            return;
        }

        bool hasProgress = interactionProgress > 0f;
        if (progressBarRoot != null) progressBarRoot.SetActive(hasProgress);

        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = InteractionProgress;
            progressBarFill.color = currentState == ObjectiveState.InProgress ? inProgressColor : availableColor;
        }

        if (promptText != null)
            promptText.text = currentState == ObjectiveState.InProgress
                ? "Activating..."
                : "Hold Left Trigger / Mouse1 to Activate";
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, activationRadius);
        Gizmos.color = new Color(1f, 0.8f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, activationRadius);
    }



    public void DebugForceComplete()
    {
        if (IsComplete) return;
        CompleteObjective();
    }

}