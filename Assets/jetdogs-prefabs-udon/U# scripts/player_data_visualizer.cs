
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class player_data_visualizer : UdonSharpBehaviour
{
    public VRC_Pickup pickup;
    public bool changeapi = false;
    private bool updated = false;

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
        if (!changeapi)
        {
            PlayerApiref = Networking.LocalPlayer;
        }

        if (PlayerApiref != null)
        {
            initupdate();
        }
        
    }
    

    private void Update()
    {
        if (changeapi && pickup.IsHeld && !updated)
        {
            PlayerApiref = pickup.currentPlayer;
            initupdate();
            updated = true;
        }
        else if(changeapi && !pickup.IsHeld && updated)
        {
            updated = false;
        }

        if (PlayerApiref != null)
        {
            constupdate();

        }
        
    }

    public void customupdate()
    {
        PlayerApiref.EnablePickups(enablepickups.isOn);
        if (!changeapi)
        {
            PlayerApiref.Immobilize(immobilize.isOn);
        }
        
    }

    public void constupdate()
    {
        ismaster.text = "ismaster: " + PlayerApiref.isMaster.ToString();
        playerid.text = "playerid: " + PlayerApiref.playerId.ToString();
        playergrounded.text = "isplayergrounded: " + PlayerApiref.IsPlayerGrounded().ToString();
        isowner.text = "isowner: " + PlayerApiref.IsOwner(ownercheck).ToString();
        gravitystrength.text = "gravitystrength: " + PlayerApiref.GetGravityStrength().ToString();
        if (!changeapi)
        {
            runspeed.text = "runspeed: " + PlayerApiref.GetRunSpeed().ToString();
            walkspeed.text = "walkspeed: " + PlayerApiref.GetWalkSpeed().ToString();
            jumpimpulse.text = "jumpimpulse: " + PlayerApiref.GetJumpImpulse().ToString();
        }
        else
        {
            runspeed.text = "runspeed: NULL";
            walkspeed.text = "walkspeed: NULL";
            jumpimpulse.text = "jumpimpulse: NULL";
        }
        
        Debug.Log("get velocity");
        velocity.text = "velocity: " + PlayerApiref.GetVelocity().ToString();
        Debug.Log("get position");
        position.text = "position: " + PlayerApiref.GetPosition().ToString();
        Debug.Log("get rotation");
        rotation.text = "rotation: " + PlayerApiref.GetRotation().ToString();
    }

    public void initupdate()
    {
        Debug.Log("get local");
        islocal.text = "islocal: " + PlayerApiref.isLocal.ToString();
        Debug.Log("get name");
        displayname.text = "displayname: " + PlayerApiref.displayName;
        enablepickups.isOn = true;
        Debug.Log("get in vr");
        isvr.text = "isuserinvr: " + PlayerApiref.IsUserInVR().ToString();
        immobilize.isOn = false;
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
