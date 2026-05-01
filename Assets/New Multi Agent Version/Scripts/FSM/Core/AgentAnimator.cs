using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
public class AgentAnimator : MonoBehaviour
{
    private Animator animator;
    private NavMeshAgent agent;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    [Header("Settings")]
    public float speedSmoothing = 0.1f;

    private float smoothedSpeed = 0f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponentInParent<NavMeshAgent>();
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        UpdateLocomotion();
    }

    private void UpdateLocomotion()
    {
        if (agent == null || animator == null) return;

        float currentSpeed = agent.enabled ? agent.velocity.magnitude : 0f;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, currentSpeed, Time.deltaTime / speedSmoothing);
        animator.SetFloat(SpeedHash, smoothedSpeed);
    }

    public void StopAll()
    {
        smoothedSpeed = 0f;
        if (animator != null)
            animator.SetFloat(SpeedHash, 0f);
    }
}