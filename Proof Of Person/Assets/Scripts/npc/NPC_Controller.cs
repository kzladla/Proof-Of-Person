using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class NPC_Controller : MonoBehaviour
{
    public GameObject uiCanvas;

    [Header("Queue Setup")]
    public List<NavMeshAgent> npcs = new List<NavMeshAgent>();
    public Transform[] queueSpots;   // Front spot = index 0

    [Header("Decision Targets")]
    public Transform entryPoint;
    public Transform denyPoint;



    void Start()
    {
        UpdateQueuePositions();
    }

    void Update()
    {
        // For testing: Press 'A' to approve front NPC, 'D' to deny front NPC
        if (Input.GetKeyDown(KeyCode.A)) ApproveFront();
        if (Input.GetKeyDown(KeyCode.D)) DenyFront();
        // if (Input.GetKeyDown(KeyCode.E)) UpdateQueuePositions();
    }

    // 🔹 UI BUTTON → Approve front NPC
    public void ApproveFront()
    {
        if (npcs.Count == 0) return;

        NavMeshAgent frontNPC = npcs[0];
        frontNPC.SetDestination(entryPoint.position);

        npcs.RemoveAt(0);  // Remove front NPC from queue
        UpdateQueuePositions();
    }

    // 🔹 UI BUTTON → Deny front NPC
    public void DenyFront()
    {
        if (npcs.Count == 0) return;

        NavMeshAgent frontNPC = npcs[0];
        frontNPC.SetDestination(denyPoint.position);

        npcs.RemoveAt(0);  // Remove front NPC from queue
        UpdateQueuePositions();
    }

    // 🔹 Move remaining NPCs forward in queue
    void UpdateQueuePositions()
    {
        for (int i = 0; i < npcs.Count; i++)
        {
            if (i < queueSpots.Length)
                npcs[i].SetDestination(queueSpots[i].position);
        }
    }

    public void HideUI() 
    {
        uiCanvas.SetActive(false);
    }
}