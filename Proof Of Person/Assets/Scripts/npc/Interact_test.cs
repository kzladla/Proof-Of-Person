using UnityEngine;
using UnityEngine.AI;

public class Interact_test : MonoBehaviour, IInteractable
{
    // [SerializeField] private Transform Target;
    // [SerializeField] private NavMeshAgent Agent;
    [SerializeField] private NPC_Controller nPC_Controller;

    public GameObject uiCanvas;

    public void Interact() {
        Debug.Log("Interacted with " + gameObject.name);


        // Agent.SetDestination(Target.position);
        // nPC_Controller.Target = Target;
        nPC_Controller.showUI();

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
