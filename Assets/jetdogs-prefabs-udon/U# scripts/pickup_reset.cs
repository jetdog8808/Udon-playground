
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class pickup_reset : UdonSharpBehaviour
{
    public VRC_Pickup pickup;
    private VRCPlayerApi PlayerApiref;
    private Transform pickuptransform;
    public Transform resetpoint;
    public bool forcedrop = false;
    public bool synced = false;
    

    void Start()
    {
        pickuptransform = pickup.transform;
        PlayerApiref = Networking.LocalPlayer;
    }

    void Interact()
    {

        if (synced)
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "syncdrop");
        }
        else
        {
            pickup.Drop();
            resetpickup();
        }

       
    }

    public void syncdrop()
    {
        if (Networking.IsOwner(PlayerApiref, pickup.gameObject) && (forcedrop || !pickup.IsHeld))
        {

            resetpickup();
        }
    }

    public void resetpickup()
    {
        pickup.Drop();
        pickuptransform.position = resetpoint.position;
        pickuptransform.rotation = resetpoint.rotation;
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
