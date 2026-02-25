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
        // create raycast from interactor source forward
        Ray r = new Ray(InteractorSource.position, InteractorSource.forward);
        // check if ray hits an interactable object within range
        if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange))
        {   // if it does, check if it has an NPC_Controller script attached
            if (hitInfo.collider.TryGetComponent(out NPC_Controller npc))
            {   // if a is pressed send to approve target from controller script
                if (Input.GetKeyDown(KeyCode.A))
                {   // 3 is the front of the queue, only approve if at the front
                    if (npc.currentTargetIndex == 3)
                    {
                        npc.Approve();
                    }
                }// if d is pressed send to deny target from controller script
                if (Input.GetKeyDown(KeyCode.D))
                {
                    if (npc.currentTargetIndex == 3)
                    {
                        npc.Deny();
                    }
                }
            }
        }
        // show ray in scene view for debugging
        Debug.DrawRay(InteractorSource.position, InteractorSource.forward * InteractRange, Color.red);
    }

}