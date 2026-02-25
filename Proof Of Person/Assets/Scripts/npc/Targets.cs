using UnityEngine;

public class Targets : MonoBehaviour
{
    // // reference to the npc controller script for each npc
    [SerializeField] private NPC_Controller nPC_Controller;
    [SerializeField] private NPC_Controller nPC_Controller1;
    [SerializeField] private NPC_Controller nPC_Controller2;
    [SerializeField] private NPC_Controller nPC_Controller3;

        private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("NPC"))
        {   // destroy npc so they don't rejoin the queue
            Destroy(other.gameObject);
            Debug.Log("NPC object destroyed");
            // updates the queue for each npc
            nPC_Controller.SwitchTarget();
            nPC_Controller1.SwitchTarget();
            nPC_Controller2.SwitchTarget();
            nPC_Controller3.SwitchTarget();

        }
    }


}
