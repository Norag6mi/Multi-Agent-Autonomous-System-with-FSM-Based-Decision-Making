// Assets/Scripts/VR/VRLaserPointer.cs
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class VRLaserPointer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxLength = 50f;
    [SerializeField] private float lineWidth = 0.01f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask agentMask;

    [Header("Colors")]
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color groundColor = new Color(0.2f, 0.5f, 1f);
    [SerializeField] private Color enemyColor = Color.red;
    [SerializeField] private Color allyColor = Color.green;

    private LineRenderer lineRenderer;
    private Transform controller;
    private GameObject hitMarker;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        controller = transform;

        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        lineRenderer.startColor = neutralColor;
        lineRenderer.endColor = neutralColor;
        lineRenderer.useWorldSpace = true;

        hitMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hitMarker.transform.localScale = Vector3.one * 0.1f;
        hitMarker.SetActive(false);
        Collider col = hitMarker.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    private void Update()
    {
        Ray ray = new Ray(controller.position, controller.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxLength, groundMask | agentMask))
        {
            lineRenderer.SetPosition(0, controller.position);
            lineRenderer.SetPosition(1, hit.point);
            lineRenderer.enabled = true;

            hitMarker.transform.position = hit.point + Vector3.up * 0.05f;
            hitMarker.SetActive(true);

            AgentIdentity identity = hit.collider.GetComponentInParent<AgentIdentity>();
            if (identity != null)
            {
                if (identity.Faction == FactionType.Alpha)
                {
                    lineRenderer.startColor = allyColor;
                    lineRenderer.endColor = allyColor;
                }
                else
                {
                    lineRenderer.startColor = enemyColor;
                    lineRenderer.endColor = enemyColor;
                }
            }
            else
            {
                lineRenderer.startColor = groundColor;
                lineRenderer.endColor = groundColor;
            }
        }
        else
        {
            lineRenderer.SetPosition(0, controller.position);
            lineRenderer.SetPosition(1, controller.position + controller.forward * maxLength);
            lineRenderer.enabled = true;
            lineRenderer.startColor = neutralColor;
            lineRenderer.endColor = neutralColor;
            hitMarker.SetActive(false);
        }
    }
}