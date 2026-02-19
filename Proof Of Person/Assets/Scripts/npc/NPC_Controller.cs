using UnityEngine;
using UnityEngine.AI;

public class NPC_Controller : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform[] targets;   // Assign in Inspector

    [SerializeField] private int currentTargetIndex = 0;

    void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (targets.Length > 0)
            agent.SetDestination(targets[currentTargetIndex].position);
    }

    void Update()
    {
        // Press E to switch target
        if (Input.GetKeyDown(KeyCode.E))
        {
            SwitchTarget();
        }
    }

    void SwitchTarget()
    {
        if (targets.Length == 0) return;

        // Go to next target
        currentTargetIndex++;

        // Loop back to start if we reach the end
        if (currentTargetIndex >= targets.Length)
            currentTargetIndex = 0;

        agent.SetDestination(targets[currentTargetIndex].position);
    }
}
