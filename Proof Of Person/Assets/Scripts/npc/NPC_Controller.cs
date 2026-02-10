using UnityEngine;
using UnityEngine.AI;

public class NPC_Controller : MonoBehaviour
{
    public Transform Target;
    public NavMeshAgent Agent;
   

    // Update is called once per frame
    void Update()
    {
        Agent.SetDestination(Target.position);
    }
}