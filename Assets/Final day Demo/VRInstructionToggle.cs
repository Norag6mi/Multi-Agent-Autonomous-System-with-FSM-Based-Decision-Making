using UnityEngine;
using UnityEngine.InputSystem;

public class VRInstructionToggle : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot; // assign InstructionCanvas or child root
    [SerializeField] private InputActionReference toggleAction; // map to left menu or Y button
    [SerializeField] private KeyCode keyboardToggle = KeyCode.I;

    private bool wasPressed = false;

    private void OnEnable()
    {
        toggleAction?.action.Enable();
    }

    private void OnDisable()
    {
        toggleAction?.action.Disable();
    }

    private void Update()
    {
        if (Input.GetKeyDown(keyboardToggle))
            Toggle();

        float v = toggleAction?.action.ReadValue<float>() ?? 0f;
        bool pressed = v > 0.5f;
        if (pressed && !wasPressed)
            Toggle();
        wasPressed = pressed;
    }

    private void Toggle()
    {
        if (panelRoot != null)
            panelRoot.SetActive(!panelRoot.activeSelf);
    }
}