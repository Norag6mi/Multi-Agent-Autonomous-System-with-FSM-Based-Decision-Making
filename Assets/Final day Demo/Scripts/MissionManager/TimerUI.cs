using UnityEngine;
using TMPro;

public class TimerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Transform vrCameraTransform;

    [Header("VR Settings")]
    [SerializeField] private bool forceVRMode = false;
    [SerializeField] private float vrDistance = 2f;
    [SerializeField] private float vrScaleMultiplier = 0.0015f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color criticalColor = Color.red;
    [SerializeField] private float warningThreshold = 60f;
    [SerializeField] private float criticalThreshold = 30f;

    [Header("Pulse")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.12f;

    private Vector3 timerOriginalScale;
    private bool isPulsing = false;
    private bool isVRActive = false;

    private void Awake()
    {
        isVRActive = UnityEngine.XR.XRSettings.isDeviceActive || forceVRMode;

        if (timerText != null)
            timerOriginalScale = timerText.transform.localScale;

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        if (isVRActive && vrCameraTransform == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                vrCameraTransform = cam.transform;
        }

        ValidateSetup();
        UpdateVRElements();
    }

    private void ValidateSetup()
    {
        if (timerText == null)
            Debug.LogError("[TimerUI] timerText not assigned!");

        if (countdownText == null)
            Debug.LogWarning("[TimerUI] countdownText not assigned.");
    }

    private void Update()
    {
        if (isVRActive)
            UpdateVRElements();
    }

    private void UpdateVRElements()
    {
        if (!isVRActive || vrCameraTransform == null) return;

        // Position timer in front of camera
        if (timerText != null && timerText.gameObject.activeInHierarchy)
        {
            timerText.transform.position = vrCameraTransform.position +
                                         vrCameraTransform.forward * vrDistance +
                                         vrCameraTransform.up * 0.2f;
            timerText.transform.rotation = vrCameraTransform.rotation;
            timerText.transform.localScale = Vector3.one * vrScaleMultiplier;
        }

        // Position countdown in front of camera
        if (countdownText != null && countdownText.gameObject.activeInHierarchy)
        {
            countdownText.transform.position = vrCameraTransform.position +
                                             vrCameraTransform.forward * vrDistance +
                                             vrCameraTransform.up * 0.3f;
            countdownText.transform.rotation = vrCameraTransform.rotation;
            countdownText.transform.localScale = Vector3.one * vrScaleMultiplier * 1.5f;
        }
    }

    public void SetTimerVisible(bool visible)
    {
        if (timerText != null)
            timerText.gameObject.SetActive(visible);
    }

    public void ShowCountdown(bool show)
    {
        if (countdownText != null)
            countdownText.gameObject.SetActive(show);
    }

    public void UpdateCountdown(int value)
    {
        if (countdownText == null) return;

        countdownText.gameObject.SetActive(true);
        countdownText.text = value > 0 ? value.ToString() : "GO!";

        if (value == 0)
            countdownText.color = Color.green;
        else
            countdownText.color = Color.white;
    }

    public void UpdateTimer(float timeRemaining, float totalTime)
    {
        if (timerText == null) return;

        timerText.text = MissionManager_Demo.FormatTime(timeRemaining);

        if (timeRemaining <= criticalThreshold)
        {
            timerText.color = criticalColor;
            isPulsing = true;
        }
        else if (timeRemaining <= warningThreshold)
        {
            timerText.color = warningColor;
            isPulsing = false;
        }
        else
        {
            timerText.color = normalColor;
            isPulsing = false;
        }

        if (isPulsing && timerText != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            timerText.transform.localScale = Vector3.one * vrScaleMultiplier * pulse;
        }
    }
}