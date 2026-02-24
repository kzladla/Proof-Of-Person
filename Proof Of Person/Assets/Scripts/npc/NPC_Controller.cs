using System.Security.AccessControl;
using UnityEngine;
using UnityEngine.AI;

public class NPC_Controller : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform[] targets;   // Assign in Inspector

    [SerializeField] private Transform Approve_Target;
    [SerializeField] private Transform Deny_Target;

    public int currentTargetIndex = 0;

    public GameObject uiCanvas;

    void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (targets.Length > 0)
            agent.SetDestination(targets[currentTargetIndex].position);
    }

    void Update()
    {
        // press e to switch target
        if (Input.GetKeyDown(KeyCode.E))
        {
            SwitchTarget();
        }
    }

    void SwitchTarget()
    {
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
        uiCanvas.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void hideUI()
    {
        uiCanvas.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    public void Approve()
    {
        agent.SetDestination(Approve_Target.position);
        Debug.Log("Approved ");
        hideUI();
    }
    public void Deny()
    {
        agent.SetDestination(Deny_Target.position);
        Debug.Log("Denied ");
        hideUI();
    }
}