using UnityEngine;

public class ExperimentModeManager : MonoBehaviour
{
    [Header("Commander GameObjects")]
    [SerializeField] private GameObject desktopCommanderRoot;
    [SerializeField] private GameObject vrCommanderRoot;

    [Header("Autonomous Mode")]
    [SerializeField] private GameObject autonomousCameraRoot;

    [Header("Configuration")]
    [SerializeField] private MatchConfiguration matchConfig;

    private void Awake()
    {
        ApplyMode();
    }

    public void ApplyMode()
    {
        if (matchConfig == null) return;

        // Disable everything first
        if (desktopCommanderRoot != null) desktopCommanderRoot.SetActive(false);
        if (vrCommanderRoot != null) vrCommanderRoot.SetActive(false);
        if (autonomousCameraRoot != null) autonomousCameraRoot.SetActive(false);

        switch (matchConfig.condition)
        {
            case MatchConfiguration.Condition.Autonomous:
                SetAutonomousMode();
                break;
            case MatchConfiguration.Condition.VRCommanded:
                SetVRMode();
                break;
            case MatchConfiguration.Condition.DesktopCommanded:
                SetDesktopMode();
                break;
        }

        Debug.Log($"[MODE] Set to {matchConfig.condition}");
    }

    private void SetAutonomousMode()
    {
        if (autonomousCameraRoot != null)
            autonomousCameraRoot.SetActive(true);
    }

    private void SetVRMode()
    {
        if (vrCommanderRoot != null)
            vrCommanderRoot.SetActive(true);
    }

    private void SetDesktopMode()
    {
        if (desktopCommanderRoot != null)
            desktopCommanderRoot.SetActive(true);
    }
}