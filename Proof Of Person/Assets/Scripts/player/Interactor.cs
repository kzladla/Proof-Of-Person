using UnityEngine;
using System.Collections;
using System.Collections.Generic;

interface IInteractable {
    public void Interact();
}

public class Interactor : MonoBehaviour
{
    // source of raycast to interact with npc
    public Transform InteractorSource;
    // range to interact with npc
    public float InteractRange;

    // references npc controller script 
    private NPC_Controller nPC_Controller;

    void Start() 
    {

    }
    
    void Update()
    {   // raycast to check range to interact with npc, when e pressed call interact function 
        if (Input.GetKeyDown(KeyCode.E)) {
            Ray r = new Ray(InteractorSource.position, InteractorSource. forward);
            if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange)) {
                if (hitInfo.collider.gameObject.TryGetComponent(out IInteractable interactObj)) {
                    interactObj.Interact();
                }
            }
        } // draws ray to show in scene view
        Debug.DrawRay(InteractorSource.position, InteractorSource.forward * InteractRange, Color.red);
    }
}
