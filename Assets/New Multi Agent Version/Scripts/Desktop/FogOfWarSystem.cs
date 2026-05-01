// Assets/Scripts/Desktop/FogOfWarSystem.cs
using UnityEngine;
using System.Collections.Generic;

// Hides enemy agents that haven't been detected by the allied team.
// Uses TeamPerceptionData as single source of truth.
// Smooth fade in/out for detected/lost enemies.
// Ghost marker at last known position when contact is lost.
public class FogOfWarSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamPerceptionData teamPerception;

    [Header("Fade Settings")]
    [SerializeField] private float fadeInSpeed = 5f;
    [SerializeField] private float fadeOutSpeed = 3f;

    [Header("Ghost Marker")]
    [SerializeField] private float ghostLifetime = 4f;
    [SerializeField] private Color ghostColor = new Color(1f, 0.3f, 0.3f, 0.4f);

    // Track each enemy's visibility state
    private class EnemyVisibilityData
    {
        public AgentIdentity identity;
        public Renderer[] renderers;
        public Canvas worldCanvas;
        public float currentAlpha;
        public bool wasVisible;
        public Vector3 lastSeenPosition;
        public GameObject ghostMarker;
    }

    private Dictionary<AgentIdentity, EnemyVisibilityData> enemyData = new Dictionary<AgentIdentity, EnemyVisibilityData>();
    private bool initialized = false;

    private void Start()
    {
        Invoke(nameof(Initialize), 0.5f);
    }

    private void Initialize()
    {
        if (teamPerception == null)
        {
            teamPerception = FindObjectOfType<TeamPerceptionData>();
            if (teamPerception == null)
            {
                Debug.LogError("[FOG] TeamPerceptionData not found!");
                return;
            }
        }

        // Find all enemy agents and cache their renderers
        FactionType enemyFaction = FactionType.Beta;
        var enemies = FactionManager.Instance.GetFactionMembers(enemyFaction);

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            var data = new EnemyVisibilityData
            {
                identity = enemy,
                renderers = enemy.GetComponentsInChildren<Renderer>(),
                worldCanvas = enemy.GetComponentInChildren<Canvas>(),
                currentAlpha = 0f,
                wasVisible = false,
                lastSeenPosition = enemy.transform.position,
                ghostMarker = null
            };

            enemyData[enemy] = data;

            // Start hidden
            SetEnemyVisible(data, 0f);
        }

        initialized = true;
        Debug.Log($"[FOG] Tracking {enemyData.Count} enemies for fog-of-war");
    }

    private void Update()
    {
        if (!initialized) return;

        foreach (var kvp in enemyData)
        {
            var data = kvp.Value;
            if (data.identity == null) continue;

            bool isDetected = teamPerception.IsEnemyDetected(data.identity);
            bool inMemory = teamPerception.IsEnemyInMemory(data.identity);

            if (isDetected)
            {
                // Fade in
                data.currentAlpha = Mathf.MoveTowards(data.currentAlpha, 1f, fadeInSpeed * Time.deltaTime);
                data.lastSeenPosition = data.identity.transform.position;

                // Remove ghost if exists
                if (data.ghostMarker != null)
                {
                    Destroy(data.ghostMarker);
                    data.ghostMarker = null;
                }

                if (!data.wasVisible)
                {
                    data.wasVisible = true;
                }
            }
            else if (inMemory)
            {
                // In memory but not currently visible — partial fade
                data.currentAlpha = Mathf.MoveTowards(data.currentAlpha, 0.3f, fadeOutSpeed * Time.deltaTime);

                // Spawn ghost marker at last known position
                if (data.wasVisible && data.ghostMarker == null)
                {
                    data.ghostMarker = CreateGhostMarker(data.lastSeenPosition);
                }

                data.wasVisible = false;
            }
            else
            {
                // Not detected, not in memory — fully hidden
                data.currentAlpha = Mathf.MoveTowards(data.currentAlpha, 0f, fadeOutSpeed * Time.deltaTime);

                if (data.wasVisible && data.ghostMarker == null)
                {
                    data.ghostMarker = CreateGhostMarker(data.lastSeenPosition);
                }

                data.wasVisible = false;
            }

            SetEnemyVisible(data, data.currentAlpha);
        }
    }

    private void SetEnemyVisible(EnemyVisibilityData data, float alpha)
    {
        bool shouldShow = alpha > 0.01f;

        foreach (var rend in data.renderers)
        {
            if (rend == null) continue;

            // Skip the state indicator sphere — let it follow main visibility
            if (rend.gameObject.name == "StateIndicator")
            {
                rend.enabled = shouldShow;
                continue;
            }

            rend.enabled = shouldShow;
        }

        // World UI canvas
        if (data.worldCanvas != null)
            data.worldCanvas.enabled = shouldShow;
    }

    private GameObject CreateGhostMarker(Vector3 position)
    {
        GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ghost.name = "GhostMarker";
        ghost.transform.position = position + Vector3.up * 1f;
        ghost.transform.localScale = Vector3.one * 0.6f;

        Collider col = ghost.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = ghost.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = ghostColor;

        // Make transparent
        mat.SetFloat("_Surface", 1);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;

        rend.material = mat;

        // Auto destroy after lifetime
        Destroy(ghost, ghostLifetime);

        return ghost;
    }

    // Called by MatchResetManager
    public void ResetFog()
    {
        foreach (var kvp in enemyData)
        {
            var data = kvp.Value;
            data.currentAlpha = 0f;
            data.wasVisible = false;
            SetEnemyVisible(data, 0f);

            if (data.ghostMarker != null)
            {
                Destroy(data.ghostMarker);
                data.ghostMarker = null;
            }
        }

        initialized = false;
        Invoke(nameof(Initialize), 0.5f);
    }
}