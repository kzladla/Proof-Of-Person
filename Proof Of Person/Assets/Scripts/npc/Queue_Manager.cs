using System.Collections.Generic;
using UnityEngine;

public class Queue_Manager : MonoBehaviour
{
    [Header("Queue Positions")]
    public Transform[] queuePositions;

    [Header("Queue Data")]
    public List<NPC_Controller> queue = new List<NPC_Controller>();

    // Add NPC to the queue
    public void AddCustomer(NPC_Controller npc)
    {
        queue.Add(npc);
        UpdateQueuePositions();
    }

    // Remove the front NPC
    public void RemoveFrontCustomer()
    {
        if (queue.Count == 0) return;

        NPC_Controller front = queue[0];
        queue.RemoveAt(0);

        // For prototype: just destroy them
        Destroy(front.gameObject);

        UpdateQueuePositions();
    }

    // Update all NPCs to their correct positions
    private void UpdateQueuePositions()
    {
        for (int i = 0; i < queue.Count; i++)
        {
            if (i < queuePositions.Length)
                queue[i].MoveTo(queuePositions[i].position);
        }
    }
}
