// Assets/Scripts/Coordination/SharedTacticalMemory.cs
using UnityEngine;
using System.Collections.Generic;

// Team-wide shared knowledge base.
// Agents contribute sightings and death locations.
// PatrolRoute queries this for smarter patrol destinations.
// Both factions get their own instance — fair for both sides.
public class SharedTacticalMemory : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private FactionType faction;
    [SerializeField] private float gridCellSize = 8f;
    [SerializeField] private float sightingMemoryDuration = 30f;
    [SerializeField] private float dangerMemoryDuration = 60f;
    [SerializeField] private float exploredDecayTime = 45f;

    // Singleton per faction
    private static Dictionary<FactionType, SharedTacticalMemory> instances =
        new Dictionary<FactionType, SharedTacticalMemory>();

    public static SharedTacticalMemory GetInstance(FactionType type)
    {
        instances.TryGetValue(type, out SharedTacticalMemory instance);
        return instance;
    }

    // Memory entries
    public struct EnemySighting
    {
        public Vector3 position;
        public float timestamp;
        public bool isActive; // still being tracked
    }

    public struct DangerZone
    {
        public Vector3 position;
        public float timestamp;
    }

    private Dictionary<Vector2Int, float> exploredCells = new Dictionary<Vector2Int, float>();
    private List<EnemySighting> enemySightings = new List<EnemySighting>();
    private List<DangerZone> dangerZones = new List<DangerZone>();

    private void Awake()
    {
        instances[faction] = this;
    }

    private void OnDestroy()
    {
        if (instances.ContainsKey(faction) && instances[faction] == this)
            instances.Remove(faction);
    }

    // Record that an agent visited this area
    public void RecordExplored(Vector3 position)
    {
        Vector2Int cell = WorldToGrid(position);
        exploredCells[cell] = Time.time;
    }

    // Record enemy sighting
    public void RecordEnemySighting(Vector3 position)
    {
        // Update existing nearby sighting or add new
        for (int i = 0; i < enemySightings.Count; i++)
        {
            if (Vector3.Distance(enemySightings[i].position, position) < gridCellSize)
            {
                enemySightings[i] = new EnemySighting
                {
                    position = position,
                    timestamp = Time.time,
                    isActive = true
                };
                return;
            }
        }

        enemySightings.Add(new EnemySighting
        {
            position = position,
            timestamp = Time.time,
            isActive = true
        });
    }

    // Record where an ally died
    public void RecordAllyDeath(Vector3 position)
    {
        dangerZones.Add(new DangerZone
        {
            position = position,
            timestamp = Time.time
        });
        Debug.Log($"[TACTICAL] {faction}: Danger zone recorded at {position}");
    }

    // Get a smart patrol destination considering tactical memory
    public Vector3? GetSmartPatrolTarget(Vector3 agentPosition, float searchRadius)
    {
        CleanExpiredEntries();

        // Priority 1: Recent enemy sighting (go investigate)
        EnemySighting? nearestSighting = GetNearestRecentSighting(agentPosition, searchRadius);
        if (nearestSighting.HasValue && Random.value < 0.3f)
        {
            return nearestSighting.Value.position;
        }

        // Priority 2: Unexplored area (go explore)
        Vector3? unexplored = GetNearestUnexploredArea(agentPosition, searchRadius);
        if (unexplored.HasValue && Random.value < 0.4f)
        {
            return unexplored.Value;
        }

        // Priority 3: Return null — let PatrolRoute use default behavior
        return null;
    }

    // Check if a position is in a danger zone
    public bool IsDangerZone(Vector3 position)
    {
        foreach (var danger in dangerZones)
        {
            if (Time.time - danger.timestamp > dangerMemoryDuration) continue;
            if (Vector3.Distance(position, danger.position) < gridCellSize * 2f)
                return true;
        }
        return false;
    }

    // Get exploration percentage
    public float GetExplorationPercentage(int totalExpectedCells)
    {
        if (totalExpectedCells <= 0) return 0f;
        return (float)exploredCells.Count / totalExpectedCells * 100f;
    }

    // Internal helpers

    private EnemySighting? GetNearestRecentSighting(Vector3 from, float maxDist)
    {
        EnemySighting? nearest = null;
        float nearestDist = maxDist;

        foreach (var sighting in enemySightings)
        {
            if (Time.time - sighting.timestamp > sightingMemoryDuration) continue;
            float dist = Vector3.Distance(from, sighting.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = sighting;
            }
        }
        return nearest;
    }

    private Vector3? GetNearestUnexploredArea(Vector3 from, float searchRadius)
    {
        Vector2Int agentCell = WorldToGrid(from);
        int searchCells = Mathf.CeilToInt(searchRadius / gridCellSize);

        Vector2Int bestCell = agentCell;
        float bestScore = -1f;

        for (int x = -searchCells; x <= searchCells; x++)
        {
            for (int z = -searchCells; z <= searchCells; z++)
            {
                Vector2Int cell = new Vector2Int(agentCell.x + x, agentCell.y + z);
                Vector3 worldPos = GridToWorld(cell);

                // Skip if explored recently
                if (exploredCells.ContainsKey(cell))
                {
                    float timeSinceExplored = Time.time - exploredCells[cell];
                    if (timeSinceExplored < exploredDecayTime) continue;
                }

                // Skip danger zones
                if (IsDangerZone(worldPos)) continue;

                // Score: prefer closer unexplored areas
                float distance = Vector3.Distance(from, worldPos);
                float score = searchRadius - distance;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                }
            }
        }

        if (bestScore > 0f)
            return GridToWorld(bestCell);

        return null;
    }

    private void CleanExpiredEntries()
    {
        enemySightings.RemoveAll(s => Time.time - s.timestamp > sightingMemoryDuration);
        dangerZones.RemoveAll(d => Time.time - d.timestamp > dangerMemoryDuration);

        // Remove old explored entries
        List<Vector2Int> expiredCells = new List<Vector2Int>();
        foreach (var kvp in exploredCells)
        {
            if (Time.time - kvp.Value > exploredDecayTime * 2f)
                expiredCells.Add(kvp.Key);
        }
        foreach (var cell in expiredCells)
            exploredCells.Remove(cell);
    }

    private Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / gridCellSize),
            Mathf.FloorToInt(worldPos.z / gridCellSize)
        );
    }

    private Vector3 GridToWorld(Vector2Int cell)
    {
        return new Vector3(
            (cell.x + 0.5f) * gridCellSize,
            0f,
            (cell.y + 0.5f) * gridCellSize
        );
    }

    public void ResetMemory()
    {
        exploredCells.Clear();
        enemySightings.Clear();
        dangerZones.Clear();
    }
}