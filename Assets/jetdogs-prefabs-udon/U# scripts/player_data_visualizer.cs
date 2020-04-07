
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class player_data_visualizer : UdonSharpBehaviour
{
    public VRCPlayerApi PlayerApiref;
    public Text islocal;
    public Text displayname;
    public Text ismaster;
    public Text playerid;
    public Text playergrounded;
    public GameObject ownercheck;
    public Text isowner;
    public Toggle enablepickups;
    public Text gravitystrength;
    public Text runspeed;
    public Text walkspeed;
    public Text jumpimpulse;
    public Text isvr;
    public Toggle immobilize;
    public Text velocity;
    public Text position;
    public Text rotation;


    void Start()
    {
        PlayerApiref = Networking.LocalPlayer;
        if (PlayerApiref != null)
        {
            islocal.text = "islocal: " + PlayerApiref.isLocal.ToString();
            displayname.text = "displayname: " + PlayerApiref.displayName;
            enablepickups.isOn = true;
            isvr.text = "isuserinvr: " + PlayerApiref.IsUserInVR().ToString();
            immobilize.isOn = false;

        }
        else
        {
            Debug.LogWarning("Playermods, playerapi is Null");
        }
    }

    private void Update()
    {
        PlayerApiref = Networking.LocalPlayer;
        if (PlayerApiref != null)
        {
            ismaster.text = "ismaster: " + PlayerApiref.isMaster.ToString();
            playerid.text = "playerid: " + PlayerApiref.playerId.ToString();
            playergrounded.text = "isplayergrounded: " + PlayerApiref.IsPlayerGrounded().ToString();
            isowner.text = "isowner: " + PlayerApiref.IsOwner(ownercheck).ToString();
            PlayerApiref.EnablePickups(enablepickups.isOn);
            gravitystrength.text = "gravitystrength: " + PlayerApiref.GetGravityStrength().ToString();
            runspeed.text = "runspeed: " + PlayerApiref.GetRunSpeed().ToString();
            walkspeed.text = "walkspeed: " + PlayerApiref.GetWalkSpeed().ToString();
            jumpimpulse.text = "jumpimpulse: " + PlayerApiref.GetJumpImpulse().ToString();
            PlayerApiref.Immobilize(immobilize.isOn);
            velocity.text = "velocity: " + PlayerApiref.GetVelocity().ToString();
            position.text = "position: " + PlayerApiref.GetPosition().ToString();
            rotation.text = "rotation: " + PlayerApiref.GetRotation().ToString();


        }
        else
        {
            Debug.LogWarning("Playermods, playerapi is Null");
        }
    }
}

/* 
VRCSDKBaseVRCPlayerApi.__get_isLocal__SystemBoolean
VRCSDKBaseVRCPlayerApi.__get_displayName__SystemString
VRCSDKBaseVRCPlayerApi.__get_isMaster__SystemBoolean
VRCSDKBaseVRCPlayerApi.__get_playerId__SystemInt32
VRCSDKBaseVRCPlayerApi.__IsPlayerGrounded__SystemBoolean
VRCSDKBaseVRCPlayerApi.__IsOwner__UnityEngineGameObject__SystemBoolean
VRCSDKBaseVRCPlayerApi.__EnablePickups__SystemBoolean__SystemVoid
VRCSDKBaseVRCPlayerApi.__GetGravityStrength__SystemSingle
VRCSDKBaseVRCPlayerApi.__GetRunSpeed__SystemSingle
VRCSDKBaseVRCPlayerApi.__GetWalkSpeed__SystemSingle
VRCSDKBaseVRCPlayerApi.__GetJumpImpulse__SystemSingle
VRCSDKBaseVRCPlayerApi.__IsUserInVR__SystemBoolean
VRCSDKBaseVRCPlayerApi.__Immobilize__SystemBoolean__SystemVoid
VRCSDKBaseVRCPlayerApi.__GetVelocity__UnityEngineVector3
VRCSDKBaseVRCPlayerApi.__GetPosition__UnityEngineVector3
VRCSDKBaseVRCPlayerApi.__GetRotation__UnityEngineQuaternion

VRCSDKBaseVRCPlayerApi.__GetTrackingData__VRCSDKBaseVRCPlayerApiTrackingDataType__VRCSDKBaseVRCPlayerApiTrackingData
VRCSDKBaseVRCPlayerApiTrackingData.__get_position__UnityEngineVector3
VRCSDKBaseVRCPlayerApiTrackingData.__get_rotation__UnityEngineQuaternion

VRCSDKBaseVRCPlayerApi.__GetBonePosition__UnityEngineHumanBodyBones__UnityEngineVector3
VRCSDKBaseVRCPlayerApi.__GetBoneRotation__UnityEngineHumanBodyBones__UnityEngineQuaternion

*/
