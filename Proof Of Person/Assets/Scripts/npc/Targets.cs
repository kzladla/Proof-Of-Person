using UnityEngine;

public class Targets : MonoBehaviour
{
    public bool npcInside = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("NPC"))
        {
            npcInside = true;
            Destroy(other.gameObject);
            npcInside = false;
        }
    }


}
