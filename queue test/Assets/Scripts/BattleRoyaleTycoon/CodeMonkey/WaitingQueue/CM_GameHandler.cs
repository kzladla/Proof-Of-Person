using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey;
using CodeMonkey.Utils;

public class CM_GameHandler : MonoBehaviour {

    [SerializeField] private Sprite toiletSprite;
    private CM_WaitingQueue waitingQueue;

	private void Start () {
        List<Vector3> waitingQueuePositionList = new List<Vector3>();
        Vector3 firstPosition = new Vector3(740, 280);
        float positionSize = 8f;
        for (int i = 0; i < 5; i++) {
            waitingQueuePositionList.Add(firstPosition + new Vector3(-1, 0) * positionSize * i);
        }
        waitingQueue = new CM_WaitingQueue(waitingQueuePositionList);

        waitingQueue.AddPosition(waitingQueuePositionList[waitingQueuePositionList.Count - 1] + new Vector3(0, -positionSize));
        
        CMDebug.ButtonUI(new Vector2(0, 350), "", waitingQueue.AddPosition_Up);
        CMDebug.ButtonUI(new Vector2(0, 300), "", waitingQueue.AddPosition_Down);
        CMDebug.ButtonUI(new Vector2(-30, 325), "", waitingQueue.AddPosition_Left);
        CMDebug.ButtonUI(new Vector2(30, 325), "", waitingQueue.AddPosition_Right);
        
        CMDebug.ButtonUI(new Vector2(200, 325), "", waitingQueue.RemovePosition);

        FunctionPeriodic.Create(() => { 
            if (waitingQueue.CanAddGuest()) {
                CM_Guest guest = CM_Guest.GetIdleGuest();
                waitingQueue.AddGuest(guest);
            }
        }, 2f);

        waitingQueue.OnGuestArrivedAtFrontOfQueue += WaitingQueue_OnGuestArrivedAtFrontOfQueue;
        waitingQueue.OnGuestAdded += WaitingQueue_OnGuestAdded;

        List<Vector3> toiletPositionList = new List<Vector3>() { new Vector3(865, 288), new Vector3(865, 270) };
        CM_BuildingBathroom buildingBathroom = new CM_BuildingBathroom(waitingQueue, toiletPositionList, toiletSprite, new Vector3(745, 245));
	}

    private void WaitingQueue_OnGuestAdded(object sender, System.EventArgs e) {
        CMDebug.TextPopup("AddGuest", new Vector3(670, 275));
    }

    private void WaitingQueue_OnGuestArrivedAtFrontOfQueue(object sender, System.EventArgs e) {
        CMDebug.TextPopup("OnGuestArrivedAtFrontOfQueue", new Vector3(740, 280));
    }
}
