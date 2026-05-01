using UnityEngine;

public class SurfaceManager : MonoBehaviour
{
    public static SurfaceManager Instance;
    void Awake() => Instance = this;

    public void HandleImpact(GameObject obj, Vector3 pos, Vector3 normal, ImpactType type, int count)
    {
        // Placeholder: No effects will play yet
    }
}
