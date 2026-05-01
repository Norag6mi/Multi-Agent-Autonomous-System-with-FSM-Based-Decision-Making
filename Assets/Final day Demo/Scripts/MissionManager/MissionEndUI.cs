using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Keep this for other input features
using TMPro;
using UnityEngine.XR;

// Add these two lines to resolve the "Ambiguous Reference" errors:
using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;
public class MissionEndUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject endScreenRoot;

    [Header("Success Panel")]
    [SerializeField] private GameObject successPanel;
    [SerializeField] private TextMeshProUGUI successTimeTakenText;
    [SerializeField] private TextMeshProUGUI successObjectivesText;
    [SerializeField] private TextMeshProUGUI successAgentsAliveText;

    [Header("Failure Panel")]
    [SerializeField] private GameObject failurePanel;
    [SerializeField] private TextMeshProUGUI failureReasonText;
    [SerializeField] private TextMeshProUGUI failureObjectivesText;
    [SerializeField] private TextMeshProUGUI failureAgentsAliveText;

    [Header("Settings")]
    [SerializeField] private float showDelay = 1.5f;
    [SerializeField] private KeyCode restartKey = KeyCode.R;
    [SerializeField] private KeyCode quitKey = KeyCode.Q;

    [Header("XR End Screen Input (Optional)")]
    [SerializeField] private InputActionReference restartAction;
    [SerializeField] private InputActionReference quitAction;
    [SerializeField] private float actionPressThreshold = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private bool isShowing = false;
    private bool restartActionWasPressed = false;
    private bool quitActionWasPressed = false;
    private Coroutine showRoutine;


    // 

    [Header("Direct XR Button Fallback (Quest-safe)")]
    [SerializeField] private bool useDirectXRButtons = true;
    [SerializeField] private float quitHoldSeconds = 0.75f; // hold to avoid accidental quit

private bool prevRestartXRPressed = false;
private bool prevQuitXRPressed = false;
private float quitHoldTimer = 0f;

    private void Awake()
    {
        HideAll();
        ValidateSetup();
    }

    private void OnEnable()
    {
        restartAction?.action.Enable();
        quitAction?.action.Enable();
    }

    private void OnDisable()
    {
        restartAction?.action.Disable();
        quitAction?.action.Disable();
    }

    private void HideAll()
    {
        SetActive(endScreenRoot, false);
        SetActive(successPanel, false);
        SetActive(failurePanel, false);
    }

    private void ValidateSetup()
    {
        if (endScreenRoot == null) Debug.LogError("[MissionEndUI] endScreenRoot not assigned!");
        if (successPanel == null) Debug.LogError("[MissionEndUI] successPanel not assigned!");
        if (failurePanel == null) Debug.LogError("[MissionEndUI] failurePanel not assigned!");
    }

    private void Update()
    {
        if (!isShowing) return;

        // Keyboard always works
        if (Input.GetKeyDown(restartKey))
        {
            Restart();
            return;
        }

        if (Input.GetKeyDown(quitKey))
        {
            Quit();
            return;
        }

        // Existing InputActionReference path (if assigned)
        bool restartActionDown = GetActionDown(restartAction, ref restartActionWasPressed);
        bool quitActionDown = GetActionDown(quitAction, ref quitActionWasPressed);

        if (restartActionDown)
        {
            Restart();
            return;
        }

        if (quitActionDown)
        {
            Quit();
            return;
        }

        // Direct XR device polling path (most reliable on Quest)
        if (!useDirectXRButtons) return;

        bool restartXRPressed = IsRestartXRPressed(); // A or X
        bool quitXRPressed = IsQuitXRPressed();       // B or Y

        bool restartXRDown = restartXRPressed && !prevRestartXRPressed;
        if (restartXRDown)
        {
            Restart();
            return;
        }

        if (quitXRPressed)
        {
            quitHoldTimer += Time.unscaledDeltaTime;
            if (quitHoldTimer >= quitHoldSeconds)
            {
                Quit();
                return;
            }
        }
        else
        {
            quitHoldTimer = 0f;
        }

        prevRestartXRPressed = restartXRPressed;
        prevQuitXRPressed = quitXRPressed;
    }



        private bool IsRestartXRPressed()
        {
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            bool leftPrimary = false;   // X
            bool rightPrimary = false;  // A

            left.TryGetFeatureValue(CommonUsages.primaryButton, out leftPrimary);
            right.TryGetFeatureValue(CommonUsages.primaryButton, out rightPrimary);

            return leftPrimary || rightPrimary;
        }

        private bool IsQuitXRPressed()
        {
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            bool leftSecondary = false;   // Y
            bool rightSecondary = false;  // B

            left.TryGetFeatureValue(CommonUsages.secondaryButton, out leftSecondary);
            right.TryGetFeatureValue(CommonUsages.secondaryButton, out rightSecondary);

            return leftSecondary || rightSecondary;
        }



    private bool GetActionDown(InputActionReference actionRef, ref bool wasPressed)
    {
        float value = actionRef?.action.ReadValue<float>() ?? 0f;
        bool isPressed = value >= actionPressThreshold;
        bool down = isPressed && !wasPressed;
        wasPressed = isPressed;
        return down;
    }

    public void ShowSuccess(float timeTaken, int objectivesDone, int totalObjectives, int agentsAlive)
    {
        if (isShowing || showRoutine != null) return;
        if (debugLog) Debug.Log("[MissionEndUI] ShowSuccess called.");
        showRoutine = StartCoroutine(ShowRoutine(true, timeTaken, objectivesDone, totalObjectives, agentsAlive));
    }

    public void ShowFailure(int objectivesDone, int totalObjectives, int agentsAlive)
    {
        if (isShowing || showRoutine != null) return;
        if (debugLog) Debug.Log("[MissionEndUI] ShowFailure called.");
        showRoutine = StartCoroutine(ShowRoutine(false, 0f, objectivesDone, totalObjectives, agentsAlive));
    }

    private System.Collections.IEnumerator ShowRoutine(bool success, float timeTaken, int objectivesDone, int totalObjectives, int agentsAlive)
    {
        float elapsed = 0f;
        while (elapsed < showDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetActive(endScreenRoot, true);
        SetActive(successPanel, success);
        SetActive(failurePanel, !success);

        if (success)
        {
            SetText(successTimeTakenText, $"Time: {MissionManager_Demo.FormatTime(timeTaken)}");
            SetText(successObjectivesText, $"Objectives Completed: {objectivesDone} / {totalObjectives}");
            SetText(successAgentsAliveText, $"Agents Alive: {agentsAlive}");
        }
        else
        {
            SetText(failureReasonText,
                objectivesDone == 0
                    ? "No objectives completed."
                    : $"{objectivesDone} of {totalObjectives} objectives completed.");
            SetText(failureObjectivesText, $"Objectives Completed: {objectivesDone} / {totalObjectives}");
            SetText(failureAgentsAliveText, $"Agents Alive: {agentsAlive}");
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        restartActionWasPressed = false;
        quitActionWasPressed = false;
        
        //
        quitHoldTimer = 0f;
        prevRestartXRPressed = false;
        prevQuitXRPressed = false;

        isShowing = true;
        showRoutine = null;
        Time.timeScale = 0f;

        if (debugLog) Debug.Log($"[MissionEndUI] Showing {(success ? "SUCCESS" : "FAILURE")} screen.");
    }

    private void SetActive(GameObject obj, bool active)
    {
        if (obj != null) obj.SetActive(active);
    }

    private void SetText(TextMeshProUGUI tmp, string value)
    {
        if (tmp != null) tmp.text = value;
        else Debug.LogWarning($"[MissionEndUI] Text field null — value was: {value}");
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Quit()
    {
        Time.timeScale = 1f;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}