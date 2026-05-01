using UnityEngine;
using UnityEngine.XR;

public class KeyboardMouseController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 2f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float verticalLookLimit = 80f;
    [SerializeField] private Transform cameraTransform;

    [Header("Settings")]
    [Tooltip("Lock cursor to window on start")]
    [SerializeField] private bool lockCursorOnStart = true;

    private float verticalRotation = 0f;
    private bool isActive = false;
    private CharacterController characterController;

    private void Awake()
    {
        // Only activate if no VR headset is connected
        isActive = !XRSettings.isDeviceActive;

        if (debugLog)
            Debug.Log($"[KeyboardMouseController] Active: {isActive}");

        characterController = GetComponent<CharacterController>();
    }

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private void Start()
    {
        if (!isActive) return;

        if (lockCursorOnStart)
            LockCursor();

        // If no camera assigned, try to find Main Camera as child
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraTransform = cam.transform;
            else
                Debug.LogError("[KeyboardMouseController] No camera found. Assign cameraTransform.");
        }
    }

    private void Update()
    {
        if (!isActive) return;

        HandleCursorLock();
        HandleMouseLook();
        HandleMovement();
    }

    private void HandleMouseLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate body left/right
        transform.Rotate(Vector3.up * mouseX);

        // Rotate camera up/down (clamped)
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal"); // A/D
        float vertical = Input.GetAxis("Vertical");     // W/S

        Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            speed *= sprintMultiplier;

        if (characterController != null)
        {
            // Apply gravity manually since CharacterController doesnt do it
            moveDirection.y = -9.81f;
            characterController.Move(moveDirection * speed * Time.deltaTime);
        }
        else
        {
            // Fallback: direct transform move (no collision)
            transform.position += moveDirection * speed * Time.deltaTime;
        }
    }

    private void HandleCursorLock()
    {
        // Left click to re-lock cursor if it was unlocked
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();

        // Escape to unlock cursor (access Unity editor controls)
        if (Input.GetKeyDown(KeyCode.Escape))
            UnlockCursor();
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}