using UnityEngine;

public class Entry_Target : MonoBehaviour
{
public bool entryInside = false;
private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("NPC"))
        {
            entryInside = true;
            Debug.Log("NPC entered entry trigger");
        }
    }
}
