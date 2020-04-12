
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class pickup_information : UdonSharpBehaviour
{
    public VRC_Pickup pickup;

    public Text currentlyheldby;
    public Text currentlocal;
    public Text currentplayer;
    public Text isheld;



    void Update()
    {
        if(pickup != null)
        {
            if (pickup.currentlyHeldBy != null)
            {
                currentlyheldby.text = "currentlyheldby: " + pickup.currentlyHeldBy.ToString();
            }
            else
            {

                currentlyheldby.text = "currentlyheldby: null";
            }

            if (pickup.currentLocalPlayer != null)
            {
                currentlocal.text = "currentlocal: " + pickup.currentLocalPlayer.displayName;
            }
            else
            {

                currentlocal.text = "currentlocal: null";
            }

            if (pickup.currentPlayer != null)
            {
                currentplayer.text ="currentplayer: " + pickup.currentPlayer.displayName;
            }
            else
            {

                currentplayer.text = "currentplayer: null";
            }

            isheld.text = "owned by: " + Networking.GetOwner(pickup.gameObject).displayName;

        }
    }

    /*VRCSDK3ComponentsVRCPickup.__get_MomentumTransferMethod__UnityEngineForceMode
VRCSDK3ComponentsVRCPickup.__set_MomentumTransferMethod__UnityEngineForceMode
VRCSDK3ComponentsVRCPickup.__get_DisallowTheft__SystemBoolean
VRCSDK3ComponentsVRCPickup.__set_DisallowTheft__SystemBoolean
VRCSDK3ComponentsVRCPickup.__get_ExactGun__UnityEngineTransform
VRCSDK3ComponentsVRCPickup.__set_ExactGun__UnityEngineTransform
VRCSDK3ComponentsVRCPickup.__get_ExactGrip__UnityEngineTransform
VRCSDK3ComponentsVRCPickup.__set_ExactGrip__UnityEngineTransform
VRCSDK3ComponentsVRCPickup.__get_allowManipulationWhenEquipped__SystemBoolean
VRCSDK3ComponentsVRCPickup.__set_allowManipulationWhenEquipped__SystemBoolean
VRCSDK3ComponentsVRCPickup.__get_orientation__VRCSDKBaseVRC_PickupPickupOrientation
VRCSDK3ComponentsVRCPickup.__set_orientation__VRCSDKBaseVRC_PickupPickupOrientation
VRCSDK3ComponentsVRCPickup.__get_AutoHold__VRCSDKBaseVRC_PickupAutoHoldMode
VRCSDK3ComponentsVRCPickup.__set_AutoHold__VRCSDKBaseVRC_PickupAutoHoldMode
VRCSDK3ComponentsVRCPickup.__get_InteractionText__SystemString
VRCSDK3ComponentsVRCPickup.__set_InteractionText__SystemString
VRCSDK3ComponentsVRCPickup.__get_UseText__SystemString
VRCSDK3ComponentsVRCPickup.__set_UseText__SystemString
VRCSDK3ComponentsVRCPickup.__get_ThrowVelocityBoostMinSpeed__SystemSingle
VRCSDK3ComponentsVRCPickup.__set_ThrowVelocityBoostMinSpeed__SystemSingle
VRCSDK3ComponentsVRCPickup.__get_ThrowVelocityBoostScale__SystemSingle
VRCSDK3ComponentsVRCPickup.__set_ThrowVelocityBoostScale__SystemSingle
VRCSDK3ComponentsVRCPickup.__get_currentlyHeldBy__UnityEngineComponent
VRCSDK3ComponentsVRCPickup.__set_currentlyHeldBy__UnityEngineComponent
VRCSDK3ComponentsVRCPickup.__get_currentLocalPlayer__VRCSDKBaseVRCPlayerApi
VRCSDK3ComponentsVRCPickup.__set_currentLocalPlayer__VRCSDKBaseVRCPlayerApi
VRCSDK3ComponentsVRCPickup.__get_pickupable__SystemBoolean
VRCSDK3ComponentsVRCPickup.__set_pickupable__SystemBoolean
VRCSDK3ComponentsVRCPickup.__get_proximity__SystemSingle
VRCSDK3ComponentsVRCPickup.__set_proximity__SystemSingle
VRCSDK3ComponentsVRCPickup.__get_currentPlayer__VRCSDKBaseVRCPlayerApi
VRCSDK3ComponentsVRCPickup.__get_IsHeld__SystemBoolean
VRCSDK3ComponentsVRCPickup.__get_currentHand__VRCSDKBaseVRC_PickupPickupHand
VRCSDK3ComponentsVRCPickup.__Drop__SystemVoid
VRCSDK3ComponentsVRCPickup.__Drop__VRCSDKBaseVRCPlayerApi__SystemVoid
VRCSDK3ComponentsVRCPickup.__GenerateHapticEvent__SystemSingle_SystemSingle_SystemSingle__SystemVoid
VRCSDK3ComponentsVRCPickup.__PlayHaptics__SystemVoid
*/
}
