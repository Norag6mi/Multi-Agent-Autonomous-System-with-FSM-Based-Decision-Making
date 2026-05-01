using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MinimapUI : MonoBehaviour
{
    [Header("Map Settings")]
    [SerializeField] private float mapWorldSize = 150f;
    [SerializeField] private Vector3 mapCenter = Vector3.zero;

    [Header("UI References")]
    [SerializeField] private RectTransform minimapPanel;
    [SerializeField] private RawImage mapBackground;

    [Header("Icon Prefabs")]
    [SerializeField] private GameObject playerIconPrefab;
    [SerializeField] private GameObject friendlyIconPrefab;
    [SerializeField] private GameObject enemyIconPrefab;
    [SerializeField] private GameObject objectiveIconPrefab;

    [Header("Icon Sizes")]
    [SerializeField] private float playerIconSize = 14f;
    [SerializeField] private float agentIconSize = 10f;
    [SerializeField] private float enemyIconSize = 10f;
    [SerializeField] private float objectiveIconSize = 14f;

    [Header("Colors")]
    [SerializeField] private Color playerColor = Color.white;
    [SerializeField] private Color friendlyColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField] private Color enemyColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private Color objectiveColor = new Color(1f, 0.85f, 0f);
    [SerializeField] private Color objectiveCompleteColor = new Color(0.3f, 1f, 0.4f);

    [Header("Enemy Fade")]
    [SerializeField] private float enemyFadeTime = 3f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    // Tracked entities
    private Transform playerTransform;
    private RectTransform playerIcon;

    private Dictionary<AgentIdentity, RectTransform> friendlyIcons
        = new Dictionary<AgentIdentity, RectTransform>();

    private Dictionary<AgentIdentity, RectTransform> enemyIcons
        = new Dictionary<AgentIdentity, RectTransform>();

    private Dictionary<AgentIdentity, float> enemyLastSeenTime
        = new Dictionary<AgentIdentity, float>();

    private Dictionary<MissionObjective, RectTransform> objectiveIcons
        = new Dictionary<MissionObjective, RectTransform>();

    private float minimapSize;

    private void Awake()
    {
        minimapSize = minimapPanel.sizeDelta.x;
    }

    private void OnEnable()
    {
        ObjectiveDiscovery.OnObjectiveDiscovered += HandleObjectiveDiscovered;
        MissionObjective.OnObjectiveComplete += HandleObjectiveComplete;
    }

    private void OnDisable()
    {
        ObjectiveDiscovery.OnObjectiveDiscovered -= HandleObjectiveDiscovered;
        MissionObjective.OnObjectiveComplete -= HandleObjectiveComplete;
    }

    private void Start()
    {
        SetupPlayer();
        SetupFriendlyAgents();
        SetupEnemyTracking();
    }

    private void Update()
    {
        UpdatePlayerIcon();
        UpdateFriendlyIcons();
        UpdateEnemyIcons();
        UpdateObjectiveIcons();
    }

    // ── Setup ────────────────────────────────────────────

    private void SetupPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogWarning("[MinimapUI] No Player tag found.");
            return;
        }

        playerTransform = playerObj.transform;
        playerIcon = CreateIcon(playerIconPrefab, playerColor, playerIconSize, "PlayerIcon");
    }

    private void SetupFriendlyAgents()
    {
        if (FactionManager.Instance == null) return;

        foreach (AgentIdentity agent in FactionManager.Instance
                 .GetFactionMembers(FactionType.Alpha))
        {
            if (agent == null) continue;
            RectTransform icon = CreateIcon(friendlyIconPrefab, friendlyColor,
                                            agentIconSize, agent.agentName);
            friendlyIcons[agent] = icon;
        }

        if (debugLog)
            Debug.Log($"[MinimapUI] Tracking {friendlyIcons.Count} friendly agents.");
    }

    private void SetupEnemyTracking()
    {
        if (FactionManager.Instance == null) return;

        // Create icons for all enemies but hide them initially
        foreach (AgentIdentity agent in FactionManager.Instance
                 .GetFactionMembers(FactionType.Beta))
        {
            if (agent == null) continue;
            RectTransform icon = CreateIcon(enemyIconPrefab, enemyColor,
                                            enemyIconSize, agent.agentName);
            icon.gameObject.SetActive(false);
            enemyIcons[agent] = icon;
        }

        if (debugLog)
            Debug.Log($"[MinimapUI] Tracking {enemyIcons.Count} enemy agents.");
    }

    // ── Update ───────────────────────────────────────────

    private void UpdatePlayerIcon()
    {
        if (playerIcon == null || playerTransform == null) return;

        // Rotate player icon to show facing direction
        if (playerTransform != null)
        {
            float angle = playerTransform.eulerAngles.y;
            playerIcon.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        playerIcon.anchoredPosition = WorldToMinimap(playerTransform.position);
    }

    private void UpdateFriendlyIcons()
    {
        foreach (var kvp in friendlyIcons)
        {
            AgentIdentity agent = kvp.Key;
            RectTransform icon = kvp.Value;

            if (agent == null || icon == null) continue;

            // Hide if dead
            bool isDead = !agent.IsAlive;
            icon.gameObject.SetActive(!isDead);

            if (!isDead)
            {
                icon.anchoredPosition = WorldToMinimap(agent.transform.position);

                // Rotate icon to show facing direction
                float angle = agent.transform.eulerAngles.y;
                icon.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }
    }

    private void UpdateEnemyIcons()
    {
        foreach (var kvp in enemyIcons)
        {
            AgentIdentity enemy = kvp.Key;
            RectTransform icon = kvp.Value;

            if (enemy == null || icon == null) continue;

            // Is this enemy currently detected by MinimapSystem?
            bool detected = MinimapSystem.Instance != null &&
                            MinimapSystem.Instance.IsEnemyVisible(enemy);

            if (detected)
            {
                enemyLastSeenTime[enemy] = Time.time;
                icon.gameObject.SetActive(true);
                icon.anchoredPosition = WorldToMinimap(enemy.transform.position);

                // Full opacity when detected
                SetIconAlpha(icon, 1f);
            }
            else if (enemyLastSeenTime.ContainsKey(enemy))
            {
                // Fade out after losing detection
                float timeSinceSeen = Time.time - enemyLastSeenTime[enemy];
                float alpha = 1f - (timeSinceSeen / enemyFadeTime);

                if (alpha > 0f)
                {
                    icon.gameObject.SetActive(true);
                    SetIconAlpha(icon, alpha);
                }
                else
                {
                    icon.gameObject.SetActive(false);
                }
            }
        }
    }

    private void UpdateObjectiveIcons()
    {
        foreach (var kvp in objectiveIcons)
        {
            MissionObjective obj = kvp.Key;
            RectTransform icon = kvp.Value;

            if (obj == null || icon == null) continue;

            icon.anchoredPosition = WorldToMinimap(obj.transform.position);

            // Change color when complete
            Image img = icon.GetComponent<Image>();
            if (img != null)
                img.color = obj.IsComplete ? objectiveCompleteColor : objectiveColor;
        }
    }

    // ── Events ───────────────────────────────────────────

    private void HandleObjectiveDiscovered(ObjectiveDiscovery discovery)
    {
        MissionObjective obj = discovery.GetComponent<MissionObjective>();
        if (obj == null) return;
        if (objectiveIcons.ContainsKey(obj)) return;

        RectTransform icon = CreateIcon(objectiveIconPrefab, objectiveColor,
                                        objectiveIconSize, obj.ObjectiveName);
        icon.anchoredPosition = WorldToMinimap(obj.transform.position);
        objectiveIcons[obj] = icon;

        if (debugLog)
            Debug.Log($"[MinimapUI] Objective revealed: {obj.ObjectiveName}");
    }

    private void HandleObjectiveComplete(MissionObjective obj)
    {
        if (!objectiveIcons.ContainsKey(obj)) return;

        RectTransform icon = objectiveIcons[obj];
        Image img = icon?.GetComponent<Image>();
        if (img != null)
            img.color = objectiveCompleteColor;
    }

    // ── Coordinate Conversion ────────────────────────────

    private Vector2 WorldToMinimap(Vector3 worldPos)
    {
        // Convert world XZ position to minimap UV (0 to 1)
        float u = (worldPos.x - mapCenter.x) / mapWorldSize + 0.5f;
        float v = (worldPos.z - mapCenter.z) / mapWorldSize + 0.5f;

        // Clamp so icons dont leave minimap panel
        u = Mathf.Clamp(u, 0.02f, 0.98f);
        v = Mathf.Clamp(v, 0.02f, 0.98f);

        // Convert UV to minimap panel local position
        float x = (u - 0.5f) * minimapSize;
        float y = (v - 0.5f) * minimapSize;

        return new Vector2(x, y);
    }

    // ── Icon Creation ────────────────────────────────────

    private RectTransform CreateIcon(GameObject prefab, Color color,
                                     float size, string iconName)
    {
        GameObject icon;

        if (prefab != null)
        {
            icon = Instantiate(prefab, minimapPanel);
        }
        else
        {
            // No prefab assigned — create simple colored circle
            icon = new GameObject(iconName);
            icon.transform.SetParent(minimapPanel, false);

            Image img = icon.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        icon.name = iconName;

        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        // Set color even if prefab was used
        Image image = icon.GetComponent<Image>();
        if (image != null)
            image.color = color;

        return rect;
    }

    private void SetIconAlpha(RectTransform icon, float alpha)
    {
        Image img = icon.GetComponent<Image>();
        if (img == null) return;

        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}