// Assets/Scripts/VR/VisionConeVisualizer.cs
using UnityEngine;

public class VisionConeVisualizer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.3f;
    [SerializeField] private float alpha = 0.15f;

    [Header("Colors")]
    [SerializeField] private Color patrolColor = new Color(0f, 1f, 0f, 0.15f);
    [SerializeField] private Color alertColor = new Color(1f, 1f, 0f, 0.15f);
    [SerializeField] private Color engageColor = new Color(1f, 0f, 0f, 0.15f);

    private MeshRenderer coneRenderer;
    private MeshFilter coneFilter;
    private SensorSystem sensor;
    private AwarenessModel awareness;
    private AgentFSM fsm;
    private float updateTimer = 0f;
    private bool visible = true;

    private void Awake()
    {
        sensor = GetComponent<SensorSystem>();
        awareness = GetComponent<AwarenessModel>();
        fsm = GetComponent<AgentFSM>();

        CreateConeMesh();
    }

    private void CreateConeMesh()
    {
        GameObject cone = new GameObject("VisionCone");
        cone.transform.SetParent(transform);
        cone.transform.localPosition = Vector3.up * (sensor.eyeHeight * 0.5f);
        cone.transform.localRotation = Quaternion.identity;

        coneFilter = cone.AddComponent<MeshFilter>();
        coneRenderer = cone.AddComponent<MeshRenderer>();

        Mesh coneMesh = GenerateConeMesh(sensor.viewAngle, sensor.viewRadius, 32);
        coneFilter.mesh = coneMesh;

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = patrolColor;
        mat.SetFloat("_Surface", 1);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;

        coneRenderer.material = mat;
        coneRenderer.enabled = false;
    }

    private Mesh GenerateConeMesh(float angle, float radius, int segments)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        float halfAngle = angle * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(-halfAngle, halfAngle, (float)i / segments);
            float rad = a * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad)) * radius;
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void Update()
    {
        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval) return;
        updateTimer = 0f;

        if (!visible) return;

        coneRenderer.enabled = true;

        Color stateColor = fsm.CurrentStateKey switch
        {
            AgentState.Patrol => patrolColor,
            AgentState.Alert => alertColor,
            AgentState.Engage => engageColor,
            _ => patrolColor
        };

        coneRenderer.material.color = stateColor;

        float scale = sensor.viewRadius / 15f;
        coneRenderer.transform.localScale = new Vector3(scale, scale, scale);
    }

    public void SetVisible(bool v)
    {
        visible = v;
        coneRenderer.enabled = v;
    }
}