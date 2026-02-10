/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading this Code Monkey project
    I hope you find it useful in your own projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */
 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;

public class CM_BuildingBathroom {

    private CM_WaitingQueue waitingQueue;
    private List<Toilet> toiletList;
    private Vector3 exitPosition;

    public CM_BuildingBathroom(CM_WaitingQueue waitingQueue, List<Vector3> toiletPositionList, Sprite toiletSprite, Vector3 exitPosition) {
        this.waitingQueue = waitingQueue;
        this.exitPosition = exitPosition;

        toiletList = new List<Toilet>();
        foreach (Vector3 toiletPosition in toiletPositionList) {
            Toilet toilet = new Toilet() { toiletPosition = toiletPosition };
            toiletList.Add(toilet);
            World_Sprite.Create(toiletPosition, toiletSprite);
            World_Sprite debugSprite = World_Sprite.Create(toiletPosition + new Vector3(20, 0), Vector3.one, Color.green);
            FunctionUpdater.Create(() => {
                debugSprite.SetColor(toilet.IsEmpty() ? Color.green : Color.red);
            });
        }

        waitingQueue.OnGuestArrivedAtFrontOfQueue += WaitingQueue_OnGuestArrivedAtFrontOfQueue;
    }

    private void WaitingQueue_OnGuestArrivedAtFrontOfQueue(object sender, System.EventArgs e) {
        TrySendGuestToToilet();
    }

    private void TrySendGuestToToilet() {
        Toilet emptyToilet = GetEmptyToilet();
        if (emptyToilet != null) {
            CM_Guest guest = waitingQueue.GetFirstInQueue();
            if (guest != null) {
                emptyToilet.SetGuest(guest);
                guest.MoveTo(emptyToilet.GetPosition(), () => {
                    guest.PlayAnimationToilet(() => {
                        emptyToilet.ClearGuest();
                        guest.MoveTo(exitPosition, () => {
                            guest.GoBackToRoaming();
                            TrySendGuestToToilet();
                        });
                    });
                });
            }
        }
    }

    private Toilet GetEmptyToilet() {
        foreach (Toilet toilet in toiletList) {
            if (toilet.IsEmpty()) {
                return toilet;
            }
        }
        return null;
    }

    private class Toilet {
        public CM_Guest guest;
        public Vector3 toiletPosition;

        public bool IsEmpty() {
            return guest == null;
        }

        public void SetGuest(CM_Guest guest) {
            this.guest = guest;
        }

        public void ClearGuest() {
            guest = null;
        }

        public Vector3 GetPosition() {
            return toiletPosition;
        }
    }

}
