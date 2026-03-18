using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class NPC_Controller : MonoBehaviour
{

    public NavMeshAgent agent;
    public Transform[] targets;   // array of targets to move position forward
    

 
    // targets for approve and deny
    [SerializeField] private Transform Approve_Target;
    [SerializeField] private Transform Deny_Target;


    // animation controller for the NPC 
    [SerializeField] private Animator animator;

  


    // current position in queue 0 = back - 3 = front
    public int currentTargetIndex = 0;


    public GameObject uiCanvas;

    void Start()
    {   // get navmesh agent component
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

    // set first target to move to
        if (targets.Length > 0)
            agent.SetDestination(targets[currentTargetIndex].position);
    }

    void Update()
    {
        // press e to switch target / move queue forward
        // for debugging - remove later 
        // if (Input.GetKeyDown(KeyCode.E))
        // {
        //     SwitchTarget();
        // }

    }

    public void SwitchTarget()
    {
        // if no targets, do nothing
        if (targets.Length == 0) return;

    

        // go to next queue target
        currentTargetIndex++;

        // go to back of queue when you reach the front
        if (currentTargetIndex >= targets.Length)
            currentTargetIndex = 0;

        agent.SetDestination(targets[currentTargetIndex].position);
    }

    public void showUI()
    {
        // show UI canvas and unlock cursor
        uiCanvas.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void hideUI()
    {
        // hide UI canvas and lock cursor
        uiCanvas.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    public void Approve()
    {   // only approve if at the front of the queue
        if (currentTargetIndex == 3)
        {
            // move to approve target position
            agent.SetDestination(Approve_Target.position);
            hideUI();
        }

    }
    public void Deny()
    {   
        if (currentTargetIndex == 3)
        {
            // move to deny target position
            agent.SetDestination(Deny_Target.position);
            hideUI();
        }

    }

}