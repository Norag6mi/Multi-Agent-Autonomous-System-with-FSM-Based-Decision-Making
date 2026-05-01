// Assets/Scripts/VR/TacticalHologram.cs
using UnityEngine;
using System.Collections.Generic;

public class TacticalHologram : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamPerceptionData teamPerception;

    [Header("Hologram Size")]
    [SerializeField] private float holoWidth = 1.4f;
    [SerializeField] private float holoDepth = 1.2f;

    [Header("Map Bounds (your actual map world coordinates)")]
    [SerializeField] private float mapMinX = -45f;
    [SerializeField] private float mapMaxX = 45f;
    [SerializeField] private float mapMinZ = -45f;
    [SerializeField] private float mapMaxZ = 45f;

    [Header("Dots")]
    [SerializeField] private float dotSize = 0.03f;
    [SerializeField] private float dotHeight = 0.035f;

    [Header("Interaction")]
    [SerializeField] private LayerMask hologramLayer;
    [SerializeField] private Color commandPreviewColor = new Color(1f, 1f, 0f, 0.8f);

    [Header("Colors")]
    [SerializeField] private Color allyDotColor = new Color(0.1f, 0.5f, 1f, 1f);
    [SerializeField] private Color enemyDotColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Header("Update")]
    [SerializeField] private float updateInterval = 0.4f;

    private List<Transform> allyDots = new List<Transform>();
    private List<Transform> enemyDots = new List<Transform>();
    private int maxDots = 8;
    private float updateTimer = 0f;
    private bool initialized = false;

    // Interaction
    private GameObject commandPreviewDot;
    private BoxCollider hologramCollider;
    private bool isPointerOnHologram = false;
    private Vector3 lastHologramHitLocal;

    // Public
    public float HologramWidth => holoWidth;
    public float HologramDepth => holoDepth;

    private void Start()
    {
        if (teamPerception == null)
            teamPerception = FindObjectOfType<TeamPerceptionData>();

        BuildMapGeometry();
        CreateDotPools();
        CreateCommandPreview();
        CreateInteractionCollider();
        Invoke(nameof(Initialize), 0.5f);
    }

    private void Initialize()
    {
        if (teamPerception == null)
        {
            Debug.LogError("[HOLOGRAM] TeamPerceptionData not found!");
            return;
        }
        initialized = true;
    }

    // ── Map Geometry ──

    private void BuildMapGeometry()
    {
        float hw = holoWidth / 2f;
        float hd = holoDepth / 2f;

        // ── BASE FLOOR ──
        MakeBox(Vector3.zero, new Vector3(holoWidth, 0.006f, holoDepth),
            new Color(0.05f, 0.07f, 0.1f, 0.95f), "Base");

        // ── OUTER WALLS ──
        float wt = 0.012f;
        float wh = 0.025f;
        Color wallColor = new Color(0.7f, 0.72f, 0.75f, 0.95f);

        MakeBox(V(0, wh / 2, hd), V(holoWidth + wt, wh, wt), wallColor, "WallN");
        MakeBox(V(0, wh / 2, -hd), V(holoWidth + wt, wh, wt), wallColor, "WallS");
        MakeBox(V(-hw, wh / 2, 0), V(wt, wh, holoDepth), wallColor, "WallW");
        MakeBox(V(hw, wh / 2, 0), V(wt, wh, holoDepth), wallColor, "WallE");

        // ── BASE AREAS (MORE VISIBLE) ──
        float baseW = holoWidth * 0.38f;
        float baseD = holoDepth * 0.22f;

        Color alphaColor = new Color(1f, 0.55f, 0.2f, 0.75f); // brighter orange
        Color betaColor  = new Color(1f, 1f, 0.25f, 0.75f);   // brighter yellow

        // Alpha (bottom)
        Vector3 alphaL = V(-hw * 0.5f, 0.007f, -hd * 0.75f);
        Vector3 alphaR = V(hw * 0.5f, 0.007f, -hd * 0.75f);

        MakeBox(alphaL, V(baseW, 0.003f, baseD), alphaColor, "AlphaL");
        MakeBox(alphaR, V(baseW, 0.003f, baseD), alphaColor, "AlphaR");

        // Beta (top)
        Vector3 betaL = V(-hw * 0.5f, 0.007f, hd * 0.75f);
        Vector3 betaR = V(hw * 0.5f, 0.007f, hd * 0.75f);

        MakeBox(betaL, V(baseW, 0.003f, baseD), betaColor, "BetaL");
        MakeBox(betaR, V(baseW, 0.003f, baseD), betaColor, "BetaR");

        // ── CLEAN L-SHAPED WALLS (FIXED) ──
        float wallH = 0.025f;
        float wallT = 0.015f;
        Color innerWall = new Color(0.85f, 0.88f, 0.9f, 0.95f);

        float offsetX = baseW * 0.3f;
        float offsetZ = baseD * 0.3f;

        // Alpha Left
        MakeBox(alphaL + new Vector3(-offsetX, wallH/2, 0), V(wallT, wallH, baseD), innerWall, "A_L_Vert");
        MakeBox(alphaL + new Vector3(0, wallH/2, offsetZ), V(baseW * 0.6f, wallH, wallT), innerWall, "A_L_Horz");

        // Alpha Right
        MakeBox(alphaR + new Vector3(offsetX, wallH/2, 0), V(wallT, wallH, baseD), innerWall, "A_R_Vert");
        MakeBox(alphaR + new Vector3(0, wallH/2, offsetZ), V(baseW * 0.6f, wallH, wallT), innerWall, "A_R_Horz");

        // Beta Left
        MakeBox(betaL + new Vector3(-offsetX, wallH/2, 0), V(wallT, wallH, baseD), innerWall, "B_L_Vert");
        MakeBox(betaL + new Vector3(0, wallH/2, -offsetZ), V(baseW * 0.6f, wallH, wallT), innerWall, "B_L_Horz");

        // Beta Right
        MakeBox(betaR + new Vector3(offsetX, wallH/2, 0), V(wallT, wallH, baseD), innerWall, "B_R_Vert");
        MakeBox(betaR + new Vector3(0, wallH/2, -offsetZ), V(baseW * 0.6f, wallH, wallT), innerWall, "B_R_Horz");

        // ── CENTER STRUCTURE ──
        MakeBox(V(0, 0.02f, 0), V(0.1f, 0.05f, 0.1f),
            new Color(0.7f, 0.75f, 0.8f, 1f), "CenterCore");

        // ── MINIMAL STRATEGIC COVER (FIXED) ──
        Color coverColor = new Color(0.8f, 0.8f, 0.8f, 0.9f);

        float spacingX = holoWidth * 0.18f;
        float spacingZ = holoDepth * 0.18f;

        for (int x = -2; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                if (x == 0 && z == 0) continue; // center clear

                Vector3 pos = new Vector3(x * spacingX, 0.01f, z * spacingZ);

                // keep center open vertically
                if (Mathf.Abs(pos.z) < holoDepth * 0.15f) continue;

                MakeBox(pos,
                    new Vector3(0.03f, 0.015f, 0.03f),
                    coverColor,
                    "Cover_" + x + "_" + z);
            }
        }
    }
    // ── Interaction ──

    private void CreateInteractionCollider()
    {
        hologramCollider = gameObject.AddComponent<BoxCollider>();
        hologramCollider.size = new Vector3(holoWidth, 0.05f, holoDepth);
        hologramCollider.center = Vector3.zero;

        // Set hologram to a specific layer for raycasting
        gameObject.layer = LayerMask.NameToLayer("UI");
    }

    private void CreateCommandPreview()
    {
        commandPreviewDot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        commandPreviewDot.name = "CommandPreview";
        commandPreviewDot.transform.SetParent(transform);
        commandPreviewDot.transform.localScale = new Vector3(0.04f, 0.002f, 0.04f);

        Collider col = commandPreviewDot.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = commandPreviewDot.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 1f, 0f, 0.9f);
        rend.material = mat;

        commandPreviewDot.SetActive(false);
    }

    // Call this from VRCommanderController with the ray
    public bool TryGetWorldPosition(Ray ray, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (hologramCollider == null) return false;

        if (hologramCollider.Raycast(ray, out RaycastHit hit, 100f))
        {
            Vector3 localHit = transform.InverseTransformPoint(hit.point);

            float halfW = holoWidth / 2f;
            float halfD = holoDepth / 2f;

            if (Mathf.Abs(localHit.x) <= halfW && Mathf.Abs(localHit.z) <= halfD)
            {
                // Show preview dot
                commandPreviewDot.SetActive(true);
                commandPreviewDot.transform.localPosition = new Vector3(localHit.x, dotHeight + 0.01f, localHit.z);
                lastHologramHitLocal = localHit;
                isPointerOnHologram = true;

                // Convert to world
                worldPosition = HologramToWorld(hit.point);
                return true;
            }
        }

        commandPreviewDot.SetActive(false);
        isPointerOnHologram = false;
        return false;
    }

    public void HidePreview()
    {
        if (commandPreviewDot != null)
            commandPreviewDot.SetActive(false);
        isPointerOnHologram = false;
    }

    public Vector3 HologramToWorld(Vector3 hologramWorldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(hologramWorldPos);

        float nx = Mathf.InverseLerp(-holoWidth / 2f, holoWidth / 2f, localPos.x);
        float nz = Mathf.InverseLerp(-holoDepth / 2f, holoDepth / 2f, localPos.z);

        float worldX = Mathf.Lerp(mapMinX, mapMaxX, nx);
        float worldZ = Mathf.Lerp(mapMinZ, mapMaxZ, nz);

        return new Vector3(worldX, 0f, worldZ);
    }

    public bool IsPointerOnHologram => isPointerOnHologram;

    // ── Dot Pools ──

    private void CreateDotPools()
    {
        for (int i = 0; i < maxDots; i++)
        {
            allyDots.Add(MakeDot(allyDotColor, "AllyDot"));
            enemyDots.Add(MakeDot(enemyDotColor, "EnemyDot"));
        }
    }

    private Transform MakeDot(Color color, string name)
    {
        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = name;
        dot.transform.SetParent(transform);
        dot.transform.localScale = Vector3.one * dotSize;

        Collider col = dot.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = dot.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;

        dot.SetActive(false);
        return dot.transform;
    }

    // ── Update ──

    private void Update()
    {
        if (!initialized) return;

        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval) return;
        updateTimer = 0f;

        UpdateDots();
    }

    private void UpdateDots()
    {
        foreach (var d in allyDots) d.gameObject.SetActive(false);
        foreach (var d in enemyDots) d.gameObject.SetActive(false);

        var allies = teamPerception.GetAlliedAgents();
        int ai = 0;
        foreach (var ally in allies)
        {
            if (ai >= allyDots.Count) break;
            if (ally == null || (ally.Combat != null && ally.Combat.IsDead())) continue;
            allyDots[ai].localPosition = WorldToHologram(ally.transform.position);
            allyDots[ai].gameObject.SetActive(true);
            ai++;
        }

        var detected = teamPerception.GetDetectedEnemies();
        int ei = 0;
        foreach (var enemy in detected)
        {
            if (ei >= enemyDots.Count) break;
            if (enemy.identity == null || !enemy.isCurrentlyVisible) continue;
            enemyDots[ei].localPosition = WorldToHologram(enemy.lastKnownPosition);
            enemyDots[ei].gameObject.SetActive(true);
            ei++;
        }
    }

    private Vector3 WorldToHologram(Vector3 worldPos)
    {
        float nx = Mathf.InverseLerp(mapMinX, mapMaxX, worldPos.x);
        float nz = Mathf.InverseLerp(mapMinZ, mapMaxZ, worldPos.z);
        return new Vector3(
            Mathf.Lerp(-holoWidth / 2f, holoWidth / 2f, nx),
            dotHeight,
            Mathf.Lerp(-holoDepth / 2f, holoDepth / 2f, nz)
        );
    }

    public void ResetHologram()
    {
        foreach (var d in allyDots) d.gameObject.SetActive(false);
        foreach (var d in enemyDots) d.gameObject.SetActive(false);
        HidePreview();
    }

    // ── Helpers ──

    private Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    private void MakeBox(Vector3 pos, Vector3 scale, Color color, string name)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(transform);
        box.transform.localPosition = pos;
        box.transform.localScale = scale;
        box.transform.localRotation = Quaternion.identity;

        Collider col = box.GetComponent<Collider>();
        if (col != null) Destroy(col);

        SetTransparent(box.GetComponent<Renderer>(), color);
    }

    private void MakeSphere(Vector3 pos, float size, Color color, string name)
    {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = name;
        s.transform.SetParent(transform);
        s.transform.localPosition = pos;
        s.transform.localScale = Vector3.one * size;

        Collider col = s.GetComponent<Collider>();
        if (col != null) Destroy(col);

        SetTransparent(s.GetComponent<Renderer>(), color);
    }

    private void SetTransparent(Renderer rend, Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.SetFloat("_Surface", 1);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        rend.material = mat;
    }
}