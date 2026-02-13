using UnityEngine;
using UnityEngine.AI;

public class NPC_Controller : MonoBehaviour
{
    public NavMeshAgent agent;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    public void MoveTo(Vector3 targetPosition)
    {
        agent.SetDestination(targetPosition);
    }
}
