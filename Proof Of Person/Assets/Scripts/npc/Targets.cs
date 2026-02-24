using UnityEngine;

public class Targets : MonoBehaviour
{
        private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("NPC"))
        {
            Destroy(other.gameObject);
            Debug.Log("NPC object destroyed");
        }
    }


}
