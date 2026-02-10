/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading this Code Monkey project
    I hope you find it useful in your own projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;

public class CM_WaitingQueue {

    public event EventHandler OnGuestAdded;
    public event EventHandler OnGuestArrivedAtFrontOfQueue;

    private const float POSITION_SIZE = 8f;

    private List<CM_Guest> guestList;
    private List<Vector3> positionList;
    private Vector3 entrancePosition;

    public CM_WaitingQueue(List<Vector3> positionList) {
        this.positionList = positionList;

        CalculateEntrancePosition();

        for (int i=0; i<positionList.Count; i++) {
            int tmpI = i;
            World_Sprite worldSprite = World_Sprite.Create(positionList[i], new Vector3(1, 1), Color.green);
            FunctionUpdater.Create(() => {
                if (positionList.Count <= tmpI) {
                    worldSprite.DestroySelf();
                }
            });
        }
        World_Sprite.Create(entrancePosition, new Vector3(1, 1), Color.magenta).SetPosition(() => entrancePosition);

        guestList = new List<CM_Guest>();
    }

    private void CalculateEntrancePosition() {
        if (positionList.Count <= 1) {
            entrancePosition = positionList[positionList.Count - 1];
        } else {
            Vector3 dir = positionList[positionList.Count - 1] - positionList[positionList.Count - 2];
            entrancePosition = positionList[positionList.Count - 1] + dir;
        }
    }

    public void AddPosition(Vector3 position) {
        positionList.Add(position);
        World_Sprite worldSprite = World_Sprite.Create(position, new Vector3(1, 1), Color.green);
        int index = positionList.Count - 1;
        FunctionUpdater.Create(() => {
            if (positionList.Count <= index) {
                worldSprite.DestroySelf();
            }
        });
        CalculateEntrancePosition();
    }

    public void AddPosition_Down() {
        AddPosition(positionList[positionList.Count - 1] + new Vector3(0, -1) * POSITION_SIZE);
    }

    public void AddPosition_Up() {
        AddPosition(positionList[positionList.Count - 1] + new Vector3(0, +1) * POSITION_SIZE);
    }

    public void AddPosition_Left() {
        AddPosition(positionList[positionList.Count - 1] + new Vector3(-1, 0) * POSITION_SIZE);
    }

    public void AddPosition_Right() {
        AddPosition(positionList[positionList.Count - 1] + new Vector3(+1, 0) * POSITION_SIZE);
    }

    public void RemovePosition() {
        if (guestList.Count < positionList.Count) {
            positionList.RemoveAt(positionList.Count - 1);
            CalculateEntrancePosition();
        }
    }

    public bool CanAddGuest() {
        return guestList.Count < positionList.Count;
    }

    public void AddGuest(CM_Guest guest) {
        guestList.Add(guest);
        guest.MoveTo(entrancePosition, () => {
            guest.MoveTo(positionList[guestList.IndexOf(guest)], () => { GuestArrivedAtQueuePosition(guest); });
        });
        if (OnGuestAdded != null) OnGuestAdded(this, EventArgs.Empty);
    }

    public CM_Guest GetFirstInQueue() {
        if (guestList.Count == 0) {
            return null;
        } else {
            CM_Guest guest = guestList[0];
            guestList.RemoveAt(0);
            RelocateAllGuests();
            return guest;
        }
    }

    private void RelocateAllGuests() {
        for (int i = 0; i < guestList.Count; i++) {
            CM_Guest guest = guestList[i];
            guest.MoveTo(positionList[i], () => { GuestArrivedAtQueuePosition(guest); });
        }
    }

    private void GuestArrivedAtQueuePosition(CM_Guest guest) {
        if (guest == guestList[0]) {
            if (OnGuestArrivedAtFrontOfQueue != null) OnGuestArrivedAtFrontOfQueue(this, EventArgs.Empty);
        }
    }

}
