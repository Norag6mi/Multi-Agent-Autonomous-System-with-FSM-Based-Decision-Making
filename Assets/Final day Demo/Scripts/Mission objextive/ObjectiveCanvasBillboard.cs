using UnityEngine;

public class ObjectiveCanvasBillboard : MonoBehaviour
{
    [Header("VR Settings")]
    [SerializeField] private Transform vrCameraTransform;
    [SerializeField] private float vrDistance = 1.8f;
    [SerializeField] private float vrScaleMultiplier = 0.001f;
    [SerializeField] private bool forceVRMode = false;

    private Camera mainCamera;
    private bool isVRActive = false;

    private void Start()
    {
        isVRActive = UnityEngine.XR.XRSettings.isDeviceActive || forceVRMode;

        if (vrCameraTransform == null)
        {
            mainCamera = Camera.main;
            if (mainCamera != null)
                vrCameraTransform = mainCamera.transform;
        }
    }

    private void LateUpdate()
    {
        if (isVRActive)
        {
            UpdateVRPosition();
        }
        else
        {
            UpdateDesktopPosition();
        }
    }

    private void UpdateDesktopPosition()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            return;
        }

        transform.rotation = Quaternion.LookRotation(
            transform.position - mainCamera.transform.position
        );
    }

    private void UpdateVRPosition()
    {
        if (vrCameraTransform == null) return;

        // Position canvas in front of player's view
        transform.position = vrCameraTransform.position +
                           vrCameraTransform.forward * vrDistance +
                           vrCameraTransform.up * 0.2f;

        // Rotate to face player directly
        transform.rotation = vrCameraTransform.rotation;

        // Scale for readable size in VR
        transform.localScale = Vector3.one * vrScaleMultiplier;
    }
}