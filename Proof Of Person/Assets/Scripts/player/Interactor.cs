using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
interface IInteractable 
{ 
    public void Interact(); 
    
}

public class Interactor : MonoBehaviour
{
    private NPC_Controller nPC_Controller;
    public Transform InteractorSource;
    public float InteractRange = 5f;

    [SerializeField] private GameObject IDCanvas;

    void Update()
    {
        // create raycast from interactor source forward
        Ray r = new Ray(InteractorSource.position, InteractorSource.forward);
        // check if ray hits an interactable object within range
        if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange))
        {   // if it does, check if it has an NPC_Controller script attached
            if (hitInfo.collider.TryGetComponent(out NPC_Controller npc))
            {   
                if (Input.GetKeyDown(KeyCode.E))
                {
                    IDCanvas.SetActive(true);
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;

                }
                // // if a is pressed send to approve target from controller script
                // if (Input.GetKeyDown(KeyCode.A))
                // {   
                //     npc.Approve();
                // }   // if d is pressed send to deny target from controller script
                // if (Input.GetKeyDown(KeyCode.D))
                // {
                //     npc.Deny();
                // }
            }
        }
        // show ray in scene view for debugging
        Debug.DrawRay(InteractorSource.position, InteractorSource.forward * InteractRange, Color.red);
    }

}