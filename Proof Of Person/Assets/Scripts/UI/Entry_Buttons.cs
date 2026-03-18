using UnityEngine;

public class Entry_Buttons : MonoBehaviour
{

    private NPC_Controller nPC_Controller;


    public void Approve_Button()
    {
        nPC_Controller.Approve();
    }

    public void Deny_Button()
    {
        nPC_Controller.Deny();
    }
}
