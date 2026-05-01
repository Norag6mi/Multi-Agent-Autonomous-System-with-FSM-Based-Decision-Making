using UnityEngine;

/// <summary>
/// Cone-based vision system with line-of-sight raycasting.
/// Detects enemy agents and feeds data to AwarenessModel.
/// Uses FactionManager to identify who is enemy vs ally.
/// </summary>
public class SensorSystem : MonoBehaviour
{
    [Header("Vision Cone")]
    public float viewRadius = 15f;
    [Range(0, 360)]
    public float viewAngle = 110f;

    [Header("Eye Settings")]
    public float eyeHeight = 1.6f;

    [Header("Detection Layers")]
    [Tooltip("Layer(s) that agents exist on")]
    public LayerMask agentMask;
    [Tooltip("Layer(s) that block line of sight")]
    public LayerMask obstacleMask;

    // References
    private AwarenessModel awarenessModel;
    private AgentIdentity myIdentity;

    private void Awake()
    {
        awarenessModel = GetComponent<AwarenessModel>();
        myIdentity = GetComponent<AgentIdentity>();
    }

    private void Update()
    {
        Scan();
    }

    /// <summary>
    /// Main detection loop. Scans for enemies within vision cone.
    /// Reports results to AwarenessModel.
    /// </summary>
    private void Scan()
    {
        bool foundEnemy = false;
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;

        // Find all agents within vision radius
        Collider[] hits = Physics.OverlapSphere(eyePos, viewRadius, agentMask);

        float closestDistance = Mathf.Infinity;
        Transform closestEnemy = null;

        foreach (Collider hit in hits)
        {
            // Skip self
            if (hit.transform == transform) continue;
            if (hit.transform.IsChildOf(transform)) continue;

            // Check faction — only detect enemies
            AgentIdentity otherIdentity = hit.GetComponentInParent<AgentIdentity>();
            if (otherIdentity == null) continue;
            if (otherIdentity.Faction == myIdentity.Faction) continue; // Ally — skip

            // Direction and angle check
            Vector3 targetPos = hit.transform.position + Vector3.up * eyeHeight * 0.8f;
            Vector3 dirToTarget = (targetPos - eyePos).normalized;
            float angle = Vector3.Angle(transform.forward, dirToTarget);

            if (angle > viewAngle / 2f) continue; // Outside vision cone

            // Distance
            float distance = Vector3.Distance(eyePos, targetPos);

            // Line of sight check — raycast for obstacles
            if (Physics.Raycast(eyePos, dirToTarget, distance, obstacleMask))
                continue; // Blocked by wall

            // Enemy is visible
            foundEnemy = true;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = hit.transform;
            }

            // Debug visualization
            Debug.DrawLine(eyePos, targetPos, Color.red);
        }

        // Report to awareness model
        if (foundEnemy && closestEnemy != null)
        {
            awarenessModel.ReportTargetVisible(closestDistance, closestEnemy);
        }
        else
        {
            awarenessModel.ReportTargetLost();
        }



        // Add at the end of Scan() method, after the awareness reporting:

        // Record explored area
        AgentIdentity myId = GetComponent<AgentIdentity>();
        SharedTacticalMemory memory = SharedTacticalMemory.GetInstance(myId.Faction);
        if (memory != null)
        {
            memory.RecordExplored(transform.position);

            if (foundEnemy && closestEnemy != null)
            {
                memory.RecordEnemySighting(closestEnemy.position);

                // NEW: Register with minimap system
                AgentIdentity enemyIdentity = 
                    closestEnemy.GetComponentInParent<AgentIdentity>();
                if (enemyIdentity != null && MinimapSystem.Instance != null)
                    MinimapSystem.Instance.RegisterEnemyDetection(enemyIdentity);
            }
        }


    }

    
    /// <summary>
    /// Emergency 360-degree scan ignoring vision cone.
    /// Used when agent takes damage but can't see attacker.
    /// Returns the nearest enemy transform regardless of angle.
    /// </summary>
    public Transform EmergencyScan()
    {
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Collider[] hits = Physics.OverlapSphere(eyePos, viewRadius * 1.5f, agentMask);

        float closestDist = Mathf.Infinity;
        Transform closestEnemy = null;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform) continue;
            if (hit.transform.IsChildOf(transform)) continue;

            AgentIdentity other = hit.GetComponentInParent<AgentIdentity>();
            if (other == null) continue;
            if (other.Faction == myIdentity.Faction) continue;
            if (!other.IsAlive) continue;

            // No angle check — full 360 scan
            Vector3 targetPos = hit.transform.position + Vector3.up * eyeHeight * 0.8f;
            float dist = Vector3.Distance(eyePos, targetPos);

            // Still check line of sight (can't detect through walls)
            Vector3 dir = (targetPos - eyePos).normalized;
            if (Physics.Raycast(eyePos, dir, dist, obstacleMask))
                continue;

            if (dist < closestDist)
            {
                closestDist = dist;
                closestEnemy = hit.transform;
            }
        }

        return closestEnemy;
    }

    // --- Editor visualization ---
    private void OnDrawGizmosSelected()
    {
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;

        // Vision radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(eyePos, viewRadius);

        // Vision cone
        Vector3 leftBound = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
        Vector3 rightBound = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(eyePos, eyePos + leftBound * viewRadius);
        Gizmos.DrawLine(eyePos, eyePos + rightBound * viewRadius);

        // Forward direction
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(eyePos, eyePos + transform.forward * viewRadius);

        // Eye point
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(eyePos, 0.1f);
    }
}