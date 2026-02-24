using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
interface IInteractable 
{ 
    public void Interact(); 
    
}

public class Interactor : MonoBehaviour
{
    public Transform InteractorSource;
    public float InteractRange = 5f;

    void Update()
    {
        Ray r = new Ray(InteractorSource.position, InteractorSource.forward);

        if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange))
        {
            if (hitInfo.collider.TryGetComponent(out NPC_Controller npc))
            {
                if (Input.GetKeyDown(KeyCode.A))
                {
                    if (npc.currentTargetIndex == 3)
                    {
                        npc.Approve();
                    }
                }
                if (Input.GetKeyDown(KeyCode.D))
                {
                    if (npc.currentTargetIndex == 3)
                    {
                        npc.Deny();
                    }
                }
            }
        }

        Debug.DrawRay(InteractorSource.position, InteractorSource.forward * InteractRange, Color.red);
    }

}