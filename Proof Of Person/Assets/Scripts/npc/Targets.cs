using UnityEngine;

public class Targets : MonoBehaviour
{
    public bool npcInside = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("NPC"))
        {
            npcInside = true;
            Debug.Log("NPC entered trigger");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("NPC"))
        {
            npcInside = false;
            Debug.Log("NPC left trigger");
        }
    }
}
