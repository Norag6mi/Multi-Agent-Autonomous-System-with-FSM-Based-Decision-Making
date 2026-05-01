using UnityEngine;

// Visual indicator at command target position.
// Spawns a glowing circle on the ground, fades out over time.
public class CommandMarker : MonoBehaviour
{
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float fadeStartTime = 3f;

    private Material material;
    private Color baseColor;
    private float spawnTime;

    public static CommandMarker Spawn(Vector3 position, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "CommandMarker";
        go.transform.position = position + Vector3.up * 0.05f;
        go.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f);

        // Remove collider
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        CommandMarker marker = go.AddComponent<CommandMarker>();
        marker.baseColor = color;
        marker.spawnTime = Time.time;

        // Set material
        Renderer renderer = go.GetComponent<Renderer>();
        marker.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        marker.material.color = new Color(color.r, color.g, color.b, 0.6f);

        // Enable transparency
        marker.material.SetFloat("_Surface", 1); // Transparent
        marker.material.SetFloat("_Blend", 0);
        marker.material.SetOverrideTag("RenderType", "Transparent");
        marker.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        marker.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        marker.material.SetInt("_ZWrite", 0);
        marker.material.DisableKeyword("_ALPHATEST_ON");
        marker.material.EnableKeyword("_ALPHABLEND_ON");
        marker.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        marker.material.renderQueue = 3000;

        renderer.material = marker.material;

        return marker;
    }

    private void Update()
    {
        float elapsed = Time.time - spawnTime;

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Fade out
        if (elapsed >= fadeStartTime)
        {
            float fadeProgress = (elapsed - fadeStartTime) / (lifetime - fadeStartTime);
            float alpha = Mathf.Lerp(0.6f, 0f, fadeProgress);
            material.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
    }
}