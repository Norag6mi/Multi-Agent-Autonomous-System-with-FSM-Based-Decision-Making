using UnityEngine;

public class ObjectiveZoneVisualizer : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField] private float radius = 3f;
    [SerializeField] private float pulseSpeed = 1.5f;
    [SerializeField] private float minAlpha = 0.15f;
    [SerializeField] private float maxAlpha = 0.55f;
    [SerializeField] private Color availableColor = new Color(1f, 0.8f, 0f);
    [SerializeField] private Color inProgressColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color completeColor = new Color(0.2f, 1f, 0.4f);

    private GameObject zoneDisc;
    private GameObject zoneRing;
    private Material discMaterial;
    private Material ringMaterial;
    private MissionObjective objective;
    private float pulseTimer = 0f;

    private void Awake()
    {
        objective = GetComponent<MissionObjective>();
        CreateZoneDisc();
        CreateZoneRing();
    }

    private void CreateZoneDisc()
    {
        zoneDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        zoneDisc.name = "ZoneDisc";
        zoneDisc.transform.SetParent(transform);
        
        // HEIGHT FIX: Set height to 2 meters (or whatever feels right)
        float zoneHeight = 2.0f; 
        
        // POSITION FIX: Move it up by half its height so the base is at floor level (Y=0)
        // We add a tiny offset (0.05f) to ensure it doesn't flicker with the floor
        zoneDisc.transform.localPosition = new Vector3(0f, (zoneHeight / 2f) + 0.05f, 0f);
        
        // SCALE FIX: Make it wider and taller
        // Cylinder default height is 2 units, so scale Y=1 is 2m tall.
        zoneDisc.transform.localScale = new Vector3(radius * 2.5f, zoneHeight / 2f, radius * 2.5f);

        Destroy(zoneDisc.GetComponent<Collider>());

        discMaterial = CreateTransparentMaterial(availableColor, 0.2f);
        zoneDisc.GetComponent<Renderer>().material = discMaterial;
    }

    private void CreateZoneRing()
    {
        zoneRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        zoneRing.name = "ZoneRing";
        zoneRing.transform.SetParent(transform);
        
        // Move it slightly higher than the disc base to avoid flickering
        zoneRing.transform.localPosition = new Vector3(0f, 0.1f, 0f);

        // Make the ring wider than the disc to act as a "rim"
        zoneRing.transform.localScale = new Vector3(radius * 2.6f, 0.02f, radius * 2.6f);

        Destroy(zoneRing.GetComponent<Collider>());

        ringMaterial = CreateTransparentMaterial(availableColor, 0.8f);
        zoneRing.GetComponent<Renderer>().material = ringMaterial;
    }

    private Material CreateTransparentMaterial(Color color, float alpha)
    {
        // Use Unlit for better visibility of "hologram" zones
        Shader urpShader = Shader.Find("Universal Render Pipeline/Unlit");
        Material mat = new Material(urpShader);

        // 1. Force Surface Type to Transparent
        mat.SetFloat("_Surface", 1f); 
        
        // 2. Set Blend Modes (SrcAlpha and OneMinusSrcAlpha)
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        
        // 3. Disable Depth Writing (so it doesn't block other transparent things)
        mat.SetInt("_ZWrite", 0);
        
        // 4. Enable Keywords (Crucial for URP)
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        // 5. Use _BaseColor for URP shaders (standard .color often fails)
        Color c = color;
        c.a = alpha;
        mat.SetColor("_BaseColor", c); 

        return mat;
    }


    private void Update()
    {
        if (objective == null) return;

        UpdateColors();
        UpdatePulse();

        // Hide completely when done
        if (objective.IsComplete)
        {
            zoneDisc.SetActive(false);
            zoneRing.SetActive(false);
        }
    }

    private void UpdateColors()
    {
        Color targetColor = objective.CurrentState switch
        {
            MissionObjective.ObjectiveState.InProgress => inProgressColor,
            MissionObjective.ObjectiveState.Complete => completeColor,
            _ => availableColor
        };

        SetMaterialColor(discMaterial, targetColor,
            Mathf.Lerp(minAlpha, maxAlpha, Mathf.Sin(pulseTimer) * 0.5f + 0.5f));

        SetMaterialColor(ringMaterial, targetColor, 0.9f);
    }

    private void UpdatePulse()
    {
        // Pulse faster when in progress
        float speed = objective.CurrentState == MissionObjective.ObjectiveState.InProgress
            ? pulseSpeed * 2.5f
            : pulseSpeed;

        pulseTimer += Time.deltaTime * speed;

        // Scale ring slightly on pulse
        float pulse = 1f + Mathf.Sin(pulseTimer) * 0.015f;
        zoneRing.transform.localScale = new Vector3(
            radius * 2f * pulse,
            0.015f,
            radius * 2f * pulse
        );
    }
    private void SetMaterialColor(Material mat, Color color, float alpha)
    {
        Color c = color;
        c.a = alpha;
        mat.SetColor("_BaseColor", c); // Use _BaseColor for URP
        mat.SetColor("_EmissionColor", color * 1.5f); // Boost for glow
    }

}