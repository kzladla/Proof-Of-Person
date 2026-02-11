using UnityEngine;
using System.Collections;
using System.Collections.Generic;

interface IInteractable {
    public void Interact();
}

public class Interactor : MonoBehaviour
{
    public Transform InteractorSource;
    public float InteractRange;

    void Start() 
    {

    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E)) {
            Ray r = new Ray(InteractorSource.position, InteractorSource. forward);
            if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange)) {
                if (hitInfo.collider.gameObject.TryGetComponent(out IInteractable interactObj)) {
                    interactObj.Interact();
                }
            }
        }
        Debug.DrawRay(InteractorSource.position, InteractorSource.forward * InteractRange, Color.red);
    }
}
