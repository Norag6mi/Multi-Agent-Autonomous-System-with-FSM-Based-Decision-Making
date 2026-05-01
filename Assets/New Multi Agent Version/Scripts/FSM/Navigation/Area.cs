


// using UnityEngine;
// using UnityEngine.AI;

// public class Area : MonoBehaviour
// {
//     [Header("Settings")]
//     [Tooltip("Define the width, height, and depth of the patrol zone here.")]
//     [SerializeField] private Vector3 patrolSize = new Vector3(10f, 1f, 10f); 

//     public Vector3 GetRandomPoint()
//     {
//         Vector3 center = transform.position;
//         // Using our custom variable instead of transform.localScale
//         Vector3 size = patrolSize;

//         float x = Random.Range(center.x - size.x / 2f, center.x + size.x / 2f);
//         float z = Random.Range(center.z - size.z / 2f, center.z + size.z / 2f);
//         Vector3 randomPoint = new Vector3(x, center.y, z);

//         if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 10f, NavMesh.AllAreas))
//         {
//             return hit.position;
//         }

//         return center;
//     }

//     private void OnDrawGizmos()
//     {
//         Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
//         // Visualizing the custom size
//         Gizmos.DrawCube(transform.position, patrolSize);
//         Gizmos.color = Color.green;
//         Gizmos.DrawWireCube(transform.position, patrolSize);
//     }
// }



using UnityEngine;
using UnityEngine.AI;

public class Area : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Define the width, height, and depth of the patrol zone here.")]
    [SerializeField] private Vector3 patrolSize = new Vector3(10f, 1f, 10f); 

    [Tooltip("Minimum distance from NavMesh edges for sampled points")]
    public float edgeBuffer = 1.5f;

    public Vector3 GetRandomPoint()
    {
        Vector3 center = transform.position;
        // Use the custom patrolSize instead of transform.localScale
        Vector3 size = patrolSize;

        // Try multiple times to find a point that satisfies the edge buffer
        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(center.x - size.x / 2f, center.x + size.x / 2f);
            float z = Random.Range(center.z - size.z / 2f, center.z + size.z / 2f);
            Vector3 randomPoint = new Vector3(x, center.y, z);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                // Check if point is far enough from NavMesh edges/walls
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
                {
                    if (edgeHit.distance >= edgeBuffer)
                        return hit.position;
                }
                else
                {
                    // Couldn't find edge info — point is likely in the open
                    return hit.position;
                }
            }
        }

        // Fallback — return center if no valid point found after 10 tries
        return center;
    }

    private void OnDrawGizmos()
    {
        // Use patrolSize for the visualization
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawCube(transform.position, patrolSize);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, patrolSize);
    }
}
