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
using V_AnimationSystem;

public class CM_Guest {

    private BattleRoyaleTycoon.Guest guest;
    private CM_Guest_AILogic guestAILogic;

    public CM_Guest(BattleRoyaleTycoon.Guest guest) {
        this.guest = guest;
        guestAILogic = new CM_Guest_AILogic();
        guest.AddAILogic(guestAILogic);
    }

    public void MoveTo(Vector3 position, Action onArrivedAtPosition = null) {
        guest.GetVObject().GetLogic<V_ObjectWalkerAnimated>().MoveTo(position, onArrivedAtPosition);
    }

    public void PlayAnimationToilet(Action onAnimComplete) {
        float framerate = UnityEngine.Random.Range(.3f, .7f);
        guest.GetVObject().GetLogic<V_UnitAnimation>().PlayAnimForced(BattleRoyaleTycoon.UnitAnimEnum.dBareHands_Victory, framerate, onAnimComplete);
    }

    private class CM_Guest_AILogic : V_IObjectActiveLogic {

        public CM_Guest_AILogic() {
        }
        
        public void Update(float deltaTime) {
        }
        public void UpdateAsSuperLogicActive(float deltaTime) {
        }
        public void UpdateAsSuperLogicInactive(float deltaTime) {
        }
    }

    public void GoBackToRoaming() {
        guest.RemoveAILogic(guestAILogic);
    }

    public static CM_Guest GetIdleGuest() {
        BattleRoyaleTycoon.Guest guest = BattleRoyaleTycoon.Guest.GetGuestSequential();
        CM_Guest cmGuest = new CM_Guest(guest);
        return cmGuest;
    }
}
