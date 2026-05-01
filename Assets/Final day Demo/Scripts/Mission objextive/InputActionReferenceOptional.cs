using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class InputActionReferenceOptional
{
    [SerializeField] private InputActionReference actionReference;

    public void Enable()
    {
        actionReference?.action.Enable();
    }

    public void Disable()
    {
        actionReference?.action.Disable();
    }

    public float ReadValue()
    {
        if (actionReference == null) return 0f;
        return actionReference.action.ReadValue<float>();
    }

    public bool IsAssigned => actionReference != null;
}