using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshAnim : MonoBehaviour
{
    public Animator animator;
    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        bool moving = agent.velocity.sqrMagnitude > 0.01f && !agent.isStopped;
        animator.SetBool("IsMoving", moving);
    }
}
